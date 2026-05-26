using Attrition.API.DTOs;
using Attrition.API.Models;
using Attrition.API.Repositories;
using System.Text.RegularExpressions;
using System.Linq.Expressions;

namespace Attrition.API.Services;

public class WikiService : IWikiService
{
    private readonly IWikiRepository _wikiRepo;
    private readonly IUserRepository _userRepo;
    private readonly IRepository<WikiCategory> _wikiCategoryRepo;
    private readonly IRepository<WikiRevision> _wikiRevisionRepo;
    private readonly IRepository<WikiContribution> _wikiContributionRepo;
    private readonly ICacheService _cache;

    public WikiService(
        IWikiRepository wikiRepo,
        IUserRepository userRepo,
        IRepository<WikiCategory> wikiCategoryRepo,
        IRepository<WikiRevision> wikiRevisionRepo,
        IRepository<WikiContribution> wikiContributionRepo,
        ICacheService cache)
    {
        _wikiRepo = wikiRepo;
        _userRepo = userRepo;
        _wikiCategoryRepo = wikiCategoryRepo;
        _wikiRevisionRepo = wikiRevisionRepo;
        _wikiContributionRepo = wikiContributionRepo;
        _cache = cache;
    }

    public async Task<List<WikiCategoryDto>> GetCategoriesAsync()
    {
        var cacheKey = "wiki:categories";
        var cached = await _cache.GetAsync<List<WikiCategoryDto>>(cacheKey);
        if (cached != null) return cached;

        var (categories, _) = await _wikiCategoryRepo.GetPagedAsync(
            1, int.MaxValue, null,
            q => q.OrderBy(c => c.SortOrder)
        );

        var dtos = new List<WikiCategoryDto>();
        foreach (var c in categories)
        {
            var count = await _wikiRepo.CountAsync(a => a.CategoryId == c.Id && a.Status == "Published");
            dtos.Add(new WikiCategoryDto(c.Id, c.Name, c.Slug, c.Description, c.IconUrl, count));
        }

        await _cache.SetAsync(cacheKey, dtos, TimeSpan.FromMinutes(30));
        return dtos;
    }

    public async Task<PaginatedResponse<WikiArticleListDto>> GetArticlesAsync(string? categorySlug, string? search, int page, int pageSize)
    {
        Expression<Func<WikiArticle, bool>> filter = a => a.Status == "Published";

        if (!string.IsNullOrEmpty(categorySlug))
        {
            var (categories, _) = await _wikiCategoryRepo.GetPagedAsync(1, 1, c => c.Slug == categorySlug);
            var category = categories.FirstOrDefault();
            if (category != null)
            {
                if (!string.IsNullOrEmpty(search))
                    filter = a => a.Status == "Published" && a.CategoryId == category.Id && a.Title.ToLower().Contains(search.ToLower());
                else
                    filter = a => a.Status == "Published" && a.CategoryId == category.Id;
            }
            else
            {
                return new PaginatedResponse<WikiArticleListDto>(new List<WikiArticleListDto>(), 0, page, pageSize);
            }
        }
        else if (!string.IsNullOrEmpty(search))
        {
            filter = a => a.Status == "Published" && a.Title.ToLower().Contains(search.ToLower());
        }

        var (items, total) = await _wikiRepo.GetPagedAsync(
            page, pageSize, filter,
            q => q.OrderByDescending(a => a.UpdatedAt)
        );

        var dtos = new List<WikiArticleListDto>();
        foreach (var a in items)
        {
            var category = await _wikiCategoryRepo.GetByIdAsync(a.CategoryId);
            var author = a.CreatedById.HasValue ? await _userRepo.GetByIdAsync(a.CreatedById.Value) : null;
            dtos.Add(new WikiArticleListDto(
                a.Id, a.Title, a.Slug,
                category?.Slug ?? string.Empty,
                author?.Username,
                a.UpdatedAt
            ));
        }

        return new PaginatedResponse<WikiArticleListDto>(dtos, total, page, pageSize);
    }

    public async Task<WikiArticleDto?> GetArticleBySlugAsync(string slug)
    {
        var cacheKey = $"wiki:article:{slug}";
        var cached = await _cache.GetAsync<WikiArticleDto>(cacheKey);
        if (cached != null) return cached;

        var (articles, _) = await _wikiRepo.GetPagedAsync(1, 1, a => a.Slug == slug);
        var article = articles.FirstOrDefault();

        if (article == null) return null;

        var category = await _wikiCategoryRepo.GetByIdAsync(article.CategoryId);
        var author = article.CreatedById.HasValue ? await _userRepo.GetByIdAsync(article.CreatedById.Value) : null;
        var lastEditor = article.LastEditedById.HasValue ? await _userRepo.GetByIdAsync(article.LastEditedById.Value) : null;

        var dto = new WikiArticleDto(
            article.Id,
            article.Title,
            article.Slug,
            category?.Slug ?? string.Empty,
            article.Content,
            author?.Username,
            lastEditor?.Username,
            article.Status,
            article.CreatedAt,
            article.UpdatedAt
        );

        await _cache.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(10));
        return dto;
    }

    public async Task<List<WikiRevision>> GetRevisionsAsync(Guid articleId)
    {
        var (revisions, _) = await _wikiRevisionRepo.GetPagedAsync(
            1, int.MaxValue,
            r => r.ArticleId == articleId,
            q => q.OrderByDescending(r => r.EditedAt)
        );
        return revisions;
    }

    public async Task<ApiResponse<string>> CreateArticleAsync(CreateArticleRequest request, Guid userId)
    {
        var slug = GenerateSlug(request.Title);
        var existing = await _wikiRepo.GetPagedAsync(1, 1, a => a.Slug == slug);
        if (existing.TotalCount > 0)
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

        await _wikiRepo.AddAsync(article);

        var revision = new WikiRevision
        {
            ArticleId = article.Id,
            Content = article.Content,
            EditedById = userId,
            ChangeNote = "Initial creation"
        };
        await _wikiRevisionRepo.AddAsync(revision);

        await _cache.RemoveByPatternAsync("wiki:categories");

        return new ApiResponse<string>(true, slug);
    }

    public async Task<ApiResponse> UpdateArticleAsync(Guid id, UpdateArticleRequest request, Guid userId)
    {
        var article = await _wikiRepo.GetByIdAsync(id);
        if (article == null) return new ApiResponse(false, "Article not found.");

        var revision = new WikiRevision
        {
            ArticleId = article.Id,
            Content = article.Content,
            EditedById = userId,
            ChangeNote = request.ChangeNote ?? "Update"
        };
        await _wikiRevisionRepo.AddAsync(revision);

        if (request.Title != null)
        {
            article.Title = request.Title;
            article.Slug = GenerateSlug(request.Title);
        }
        if (request.Content != null) article.Content = request.Content;
        if (request.Status != null) article.Status = request.Status;

        article.LastEditedById = userId;
        article.UpdatedAt = DateTime.UtcNow;

        await _wikiRepo.UpdateAsync(article);

        await _cache.RemoveAsync($"wiki:article:{article.Slug}");
        await _cache.RemoveByPatternAsync("wiki:categories");

        return new ApiResponse(true);
    }

    public async Task<ApiResponse> SubmitContributionAsync(Guid articleId, SuggestEditRequest request, Guid userId)
    {
        var article = await _wikiRepo.GetByIdAsync(articleId);
        if (article == null) return new ApiResponse(false, "Article not found.");

        var contribution = new WikiContribution
        {
            ArticleId = articleId,
            ContributorId = userId,
            SuggestedContent = request.SuggestedContent,
            ChangeNote = request.ChangeNote
        };

        await _wikiContributionRepo.AddAsync(contribution);

        return new ApiResponse(true);
    }

    public async Task<List<WikiContributionDto>> GetContributionsAsync(string status)
    {
        var (contributions, _) = await _wikiContributionRepo.GetPagedAsync(
            1, int.MaxValue, c => c.Status == status,
            q => q.OrderByDescending(c => c.SubmittedAt)
        );

        var dtos = new List<WikiContributionDto>();
        foreach (var c in contributions)
        {
            var article = await _wikiRepo.GetByIdAsync(c.ArticleId);
            var contributor = await _userRepo.GetByIdAsync(c.ContributorId);
            dtos.Add(new WikiContributionDto(
                c.Id,
                c.ArticleId,
                article?.Title ?? string.Empty,
                contributor?.Username ?? string.Empty,
                c.SuggestedContent,
                c.ChangeNote,
                c.Status,
                c.SubmittedAt
            ));
        }

        return dtos;
    }

    public async Task<ApiResponse> ReviewContributionAsync(Guid contributionId, ReviewContributionRequest request, Guid reviewerId)
    {
        if (request.Status != "Approved" && request.Status != "Rejected")
            return new ApiResponse(false, "Invalid status.");

        var contribution = await _wikiContributionRepo.GetByIdAsync(contributionId);
        if (contribution == null || contribution.Status != "Pending")
            return new ApiResponse(false, "Contribution not found or already reviewed.");

        contribution.Status = request.Status;
        contribution.ReviewedById = reviewerId;
        contribution.ReviewedAt = DateTime.UtcNow;

        if (request.Status == "Approved")
        {
            var article = await _wikiRepo.GetByIdAsync(contribution.ArticleId);
            if (article != null)
            {
                var revision = new WikiRevision
                {
                    ArticleId = article.Id,
                    Content = article.Content,
                    EditedById = contribution.ContributorId,
                    ChangeNote = contribution.ChangeNote ?? "Approved contribution"
                };
                await _wikiRevisionRepo.AddAsync(revision);

                article.Content = contribution.SuggestedContent;
                article.LastEditedById = contribution.ContributorId;
                article.UpdatedAt = DateTime.UtcNow;
                await _wikiRepo.UpdateAsync(article);

                var contributor = await _userRepo.GetByIdAsync(contribution.ContributorId);
                if (contributor != null)
                {
                    contributor.ContributionCount++;
                    await _userRepo.UpdateAsync(contributor);
                }

                await _cache.RemoveAsync($"wiki:article:{article.Slug}");
            }
        }
        else
        {
            await _wikiContributionRepo.UpdateAsync(contribution);
        }

        return new ApiResponse(true);
    }

    public async Task<ApiResponse> DeleteArticleAsync(Guid articleId)
    {
        var article = await _wikiRepo.GetByIdAsync(articleId);
        if (article == null) return new ApiResponse(false, "Article not found.");

        await _wikiRepo.DeleteAsync(article);

        await _cache.RemoveAsync($"wiki:article:{article.Slug}");
        await _cache.RemoveByPatternAsync("wiki:categories");

        return new ApiResponse(true);
    }

    public async Task<WikiRevision?> GetRevisionByIdAsync(Guid articleId, Guid revisionId)
    {
        var revision = await _wikiRevisionRepo.GetByIdAsync(revisionId);
        if (revision == null || revision.ArticleId != articleId) return null;
        return revision;
    }

    private string GenerateSlug(string title)
    {
        return Attrition.API.Utils.SlugHelper.GenerateSlug(title);
    }
}