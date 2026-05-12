using Attrition.API.Data;
using Attrition.API.DTOs;
using Attrition.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Attrition.API.Services;

public class WikiService
{
    private readonly AppDbContext _db;
    private readonly CacheService _cache;

    public WikiService(AppDbContext db, CacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<List<WikiCategoryDto>> GetCategoriesAsync()
    {
        var cacheKey = "wiki:categories";
        var cached = await _cache.GetAsync<List<WikiCategoryDto>>(cacheKey);
        if (cached != null) return cached;

        var categories = await _db.WikiCategories
            .OrderBy(c => c.SortOrder)
            .Select(c => new WikiCategoryDto(
                c.Id,
                c.Name,
                c.Slug,
                c.Description,
                c.IconUrl,
                _db.WikiArticles.Count(a => a.CategoryId == c.Id && a.Status == "Published")
            ))
            .ToListAsync();

        await _cache.SetAsync(cacheKey, categories, TimeSpan.FromMinutes(30));
        return categories;
    }

    public async Task<PaginatedResponse<WikiArticleListDto>> GetArticlesAsync(string? categorySlug, string? search, int page, int pageSize)
    {
        var query = _db.WikiArticles
            .Where(a => a.Status == "Published")
            .AsQueryable();

        if (!string.IsNullOrEmpty(categorySlug))
        {
            var categoryId = await _db.WikiCategories
                .Where(c => c.Slug == categorySlug)
                .Select(c => c.Id)
                .FirstOrDefaultAsync();
            query = query.Where(a => a.CategoryId == categoryId);
        }

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(a => a.Title.ToLower().Contains(search.ToLower()));
        }

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(a => a.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new WikiArticleListDto(
                a.Id,
                a.Title,
                a.Slug,
                _db.WikiCategories.First(c => c.Id == a.CategoryId).Slug,
                _db.Users.Where(u => u.Id == a.CreatedById).Select(u => u.Username).FirstOrDefault(),
                a.UpdatedAt
            ))
            .ToListAsync();

        return new PaginatedResponse<WikiArticleListDto>(items, total, page, pageSize);
    }

    public async Task<WikiArticleDto?> GetArticleBySlugAsync(string slug)
    {
        var cacheKey = $"wiki:article:{slug}";
        var cached = await _cache.GetAsync<WikiArticleDto>(cacheKey);
        if (cached != null) return cached;

        var article = await _db.WikiArticles
            .FirstOrDefaultAsync(a => a.Slug == slug);

        if (article == null) return null;

        var categorySlug = await _db.WikiCategories.Where(c => c.Id == article.CategoryId).Select(c => c.Slug).FirstAsync();
        var authorName = await _db.Users.Where(u => u.Id == article.CreatedById).Select(u => u.Username).FirstOrDefaultAsync();
        var lastEditorName = await _db.Users.Where(u => u.Id == article.LastEditedById).Select(u => u.Username).FirstOrDefaultAsync();

        var dto = new WikiArticleDto(
            article.Id,
            article.Title,
            article.Slug,
            categorySlug,
            article.Content,
            authorName,
            lastEditorName,
            article.Status,
            article.CreatedAt,
            article.UpdatedAt
        );

        await _cache.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(10));
        return dto;
    }

    public async Task<List<WikiRevision>> GetRevisionsAsync(Guid articleId)
    {
        return await _db.WikiRevisions
            .Where(r => r.ArticleId == articleId)
            .OrderByDescending(r => r.EditedAt)
            .ToListAsync();
    }

    public async Task<ApiResponse<string>> CreateArticleAsync(CreateArticleRequest request, Guid userId)
    {
        var slug = GenerateSlug(request.Title);
        if (await _db.WikiArticles.AnyAsync(a => a.Slug == slug))
            return new ApiResponse<string>(false, Error: "An article with a similar title already exists.");

        var article = new WikiArticle
        {
            Title = request.Title,
            Slug = slug,
            CategoryId = request.CategoryId,
            Content = request.Content,
            CreatedById = userId,
            LastEditedById = userId,
            Status = request.Status
        };

        _db.WikiArticles.Add(article);

        var revision = new WikiRevision
        {
            ArticleId = article.Id,
            Content = article.Content,
            EditedById = userId,
            ChangeNote = "Initial creation"
        };
        _db.WikiRevisions.Add(revision);

        await _db.SaveChangesAsync();
        await _cache.RemoveByPatternAsync("wiki:categories");

        return new ApiResponse<string>(true, slug);
    }

    public async Task<ApiResponse> UpdateArticleAsync(Guid id, UpdateArticleRequest request, Guid userId)
    {
        var article = await _db.WikiArticles.FindAsync(id);
        if (article == null) return new ApiResponse(false, "Article not found.");

        // Create revision from old content
        var revision = new WikiRevision
        {
            ArticleId = article.Id,
            Content = article.Content,
            EditedById = userId,
            ChangeNote = request.ChangeNote ?? "Update"
        };
        _db.WikiRevisions.Add(revision);

        if (request.Title != null)
        {
            article.Title = request.Title;
            article.Slug = GenerateSlug(request.Title);
        }
        if (request.Content != null) article.Content = request.Content;
        if (request.Status != null) article.Status = request.Status;

        article.LastEditedById = userId;
        article.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await _cache.RemoveAsync($"wiki:article:{article.Slug}");
        await _cache.RemoveByPatternAsync("wiki:categories");

        return new ApiResponse(true);
    }

    public async Task<ApiResponse> SubmitContributionAsync(Guid articleId, SuggestEditRequest request, Guid userId)
    {
        if (!await _db.WikiArticles.AnyAsync(a => a.Id == articleId))
            return new ApiResponse(false, "Article not found.");

        var contribution = new WikiContribution
        {
            ArticleId = articleId,
            ContributorId = userId,
            SuggestedContent = request.SuggestedContent,
            ChangeNote = request.ChangeNote
        };

        _db.WikiContributions.Add(contribution);
        await _db.SaveChangesAsync();

        return new ApiResponse(true);
    }

    public async Task<List<WikiContributionDto>> GetContributionsAsync(string status)
    {
        return await _db.WikiContributions
            .Where(c => c.Status == status)
            .OrderByDescending(c => c.SubmittedAt)
            .Select(c => new WikiContributionDto(
                c.Id,
                c.ArticleId,
                _db.WikiArticles.Where(a => a.Id == c.ArticleId).Select(a => a.Title).FirstOrDefault()!,
                _db.Users.Where(u => u.Id == c.ContributorId).Select(u => u.Username).FirstOrDefault()!,
                c.SuggestedContent,
                c.ChangeNote,
                c.Status,
                c.SubmittedAt
            ))
            .ToListAsync();
    }

    public async Task<ApiResponse> ReviewContributionAsync(Guid contributionId, ReviewContributionRequest request, Guid reviewerId)
    {
        if (request.Status != "Approved" && request.Status != "Rejected")
            return new ApiResponse(false, "Invalid status.");

        var contribution = await _db.WikiContributions.FindAsync(contributionId);
        if (contribution == null || contribution.Status != "Pending")
            return new ApiResponse(false, "Contribution not found or already reviewed.");

        contribution.Status = request.Status;
        contribution.ReviewedById = reviewerId;
        contribution.ReviewedAt = DateTime.UtcNow;

        if (request.Status == "Approved")
        {
            var article = await _db.WikiArticles.FindAsync(contribution.ArticleId);
            if (article != null)
            {
                var revision = new WikiRevision
                {
                    ArticleId = article.Id,
                    Content = article.Content,
                    EditedById = contribution.ContributorId,
                    ChangeNote = contribution.ChangeNote ?? "Approved contribution"
                };
                _db.WikiRevisions.Add(revision);

                article.Content = contribution.SuggestedContent;
                article.LastEditedById = contribution.ContributorId;
                article.UpdatedAt = DateTime.UtcNow;

                var contributor = await _db.Users.FindAsync(contribution.ContributorId);
                if (contributor != null) contributor.ContributionCount++;

                await _cache.RemoveAsync($"wiki:article:{article.Slug}");
            }
        }

        await _db.SaveChangesAsync();
        return new ApiResponse(true);
    }

    public async Task<ApiResponse> DeleteArticleAsync(Guid articleId)
    {
        var article = await _db.WikiArticles.FindAsync(articleId);
        if (article == null) return new ApiResponse(false, "Article not found.");

        _db.WikiArticles.Remove(article);
        await _db.SaveChangesAsync();

        await _cache.RemoveAsync($"wiki:article:{article.Slug}");
        await _cache.RemoveByPatternAsync("wiki:categories");

        return new ApiResponse(true);
    }

    private string GenerateSlug(string title)
    {
        var slug = title.ToLowerInvariant();
        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = Regex.Replace(slug, @"\s+", "-").Trim('-');
        return slug;
    }
}