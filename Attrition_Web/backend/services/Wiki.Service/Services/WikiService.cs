using System.Linq.Expressions;
using BuildingBlocks.Caching;
using BuildingBlocks.Contracts;
using BuildingBlocks.Persistence;
using BuildingBlocks.Web;
using Microsoft.EntityFrameworkCore;
using Wiki.Service.DTOs;
using Wiki.Service.Models;
using Wiki.Service.Repositories;

namespace Wiki.Service.Services;

public class WikiService : IWikiService
{
    private readonly IWikiRepository _wikiRepo;
    private readonly IRepository<WikiCategory> _categoryRepo;
    private readonly IRepository<WikiRevision> _revisionRepo;
    private readonly IRepository<WikiContribution> _contributionRepo;
    private readonly DbContext _db;
    private readonly ICacheService _cache;

    public WikiService(
        IWikiRepository wikiRepo,
        IRepository<WikiCategory> categoryRepo,
        IRepository<WikiRevision> revisionRepo,
        IRepository<WikiContribution> contributionRepo,
        DbContext db,
        ICacheService cache)
    {
        _wikiRepo = wikiRepo;
        _categoryRepo = categoryRepo;
        _revisionRepo = revisionRepo;
        _contributionRepo = contributionRepo;
        _db = db;
        _cache = cache;
    }

    public async Task<List<WikiCategoryDto>> GetCategoriesAsync()
    {
        // Categories + per-category article counts change rarely but are read on every wiki page.
        return await _cache.GetOrSetAsync("categories", async () =>
        {
            var categories = await _wikiRepo.GetCategoriesAsync();
            var counts = await _wikiRepo.CountPublishedArticlesByCategoryAsync();
            var dtos = new List<WikiCategoryDto>();
            foreach (var c in categories)
            {
                var count = counts.GetValueOrDefault(c.Id);
                dtos.Add(new WikiCategoryDto(c.Id, c.Name, c.Slug, c.Description, c.IconUrl, count));
            }
            return dtos;
        }, TimeSpan.FromMinutes(10));
    }

    /// <summary>Drop the cached category listing after any write that could change it.
    /// Only "categories" is cached today; if article-body caching is added later, give it its
    /// own scoped invalidator rather than widening this back to a blanket wiki:* wipe.</summary>
    private Task InvalidateAsync() => _cache.RemoveAsync("categories");

    public async Task<PaginatedResponse<WikiArticleListDto>> GetArticlesAsync(string? categorySlug, string? search, int page, int pageSize, Guid? authorId = null)
    {
        int? categoryId = null;

        if (!string.IsNullOrEmpty(categorySlug))
        {
            var category = await _wikiRepo.GetCategoryBySlugAsync(categorySlug);
            if (category == null)
                return new PaginatedResponse<WikiArticleListDto>(new List<WikiArticleListDto>(), 0, page, pageSize);
            categoryId = category.Id;
        }

        var search_ = search?.ToLower();
        // Published only; layer on the optional category, search, and author filters.
        Expression<Func<WikiArticle, bool>> filter = a =>
            a.Status == ArticleStatus.Published &&
            (categoryId == null || a.CategoryId == categoryId.Value) &&
            (search_ == null || a.Title.ToLower().Contains(search_)) &&
            (authorId == null || a.CreatedById == authorId.Value);

        var (items, total) = await _wikiRepo.GetPagedAsync(page, pageSize, filter,
            q => q.OrderByDescending(a => a.UpdatedAt));

        var dtos = new List<WikiArticleListDto>();
        foreach (var a in items)
        {
            var category = await _wikiRepo.GetCategoryByIdAsync(a.CategoryId);
            dtos.Add(new WikiArticleListDto(a.Id, a.Title, a.Slug, category?.Slug ?? string.Empty,
                a.CreatedById, a.CreatedByName, a.UpdatedAt));
        }
        return new PaginatedResponse<WikiArticleListDto>(dtos, total, page, pageSize);
    }

    public async Task<WikiArticleDto?> GetArticleBySlugAsync(string slug, bool includeUnpublished = false)
    {
        var (articles, _) = await _wikiRepo.GetPagedAsync(1, 1,
            a => a.Slug == slug && (includeUnpublished || a.Status == ArticleStatus.Published));
        var article = articles.FirstOrDefault();
        if (article == null) return null;

        var category = await _wikiRepo.GetCategoryByIdAsync(article.CategoryId);
        return new WikiArticleDto(article.Id, article.Title, article.Slug,
            category?.Slug ?? string.Empty, article.Content,
            article.CreatedById, article.CreatedByName, article.LastEditedByName,
            article.Status, article.CreatedAt, article.UpdatedAt);
    }

    public async Task<List<WikiRevisionDto>> GetRevisionsAsync(Guid articleId)
    {
        var revisions = await _revisionRepo.ListAsync(
            r => r.ArticleId == articleId, q => q.OrderByDescending(r => r.EditedAt));
        return revisions.Select(ToRevisionDto).ToList();
    }

    public async Task<WikiRevisionDto?> GetRevisionByIdAsync(Guid articleId, Guid revisionId)
    {
        var revision = await _revisionRepo.GetByIdAsync(revisionId);
        if (revision == null || revision.ArticleId != articleId) return null;
        return ToRevisionDto(revision);
    }

    public async Task<ApiResponse<string>> CreateArticleAsync(CreateArticleRequest request, Guid userId, string userName)
    {
        var slug = SlugHelper.GenerateSlug(request.Title);
        var existing = await _wikiRepo.GetPagedAsync(1, 1, a => a.Slug == slug);
        if (existing.TotalCount > 0)
            return ApiResponse<string>.Fail("An article with a similar title already exists.");

        // Don't create an article pointing at a non-existent category (no FK enforces this).
        if (await _categoryRepo.GetByIdAsync(request.CategoryId) is null)
            return ApiResponse<string>.Fail("The specified category does not exist.");

        var article = new WikiArticle
        {
            Title = request.Title,
            Slug = slug,
            CategoryId = request.CategoryId,
            Content = ContentSanitizer.Sanitize(request.Content),
            CreatedById = userId,
            CreatedByName = userName,
            LastEditedById = userId,
            LastEditedByName = userName,
            Status = request.Status
        };
        // Execution strategy so the transaction is retried as one unit under retry-on-failure.
        var strategy = _db.Database.CreateExecutionStrategy();
        var result = await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                await _wikiRepo.AddAsync(article);

                await _revisionRepo.AddAsync(new WikiRevision
                {
                    ArticleId = article.Id,
                    Content = article.Content,
                    EditedById = userId,
                    EditedByName = userName,
                    ChangeNote = "Initial creation"
                });

                await tx.CommitAsync();
                return ApiResponse<string>.Ok(slug);
            }
            catch (DbUpdateException)
            {
                // Lost the slug race between the check above and commit. (await using rolls back.)
                await tx.RollbackAsync();
                return ApiResponse<string>.Fail("An article with a similar title already exists.");
            }
        });

        if (result.Success) await InvalidateAsync();
        return result;
    }

    public async Task<ApiResponse> UpdateArticleAsync(Guid id, UpdateArticleRequest request, Guid userId, string userName)
    {
        var article = await _wikiRepo.GetByIdAsync(id);
        if (article == null) return ApiResponse.Fail("Article not found.");

        // Validate the slug change FIRST. Previously the revision was written before this check, so a
        // slug clash returned failure but left an orphaned revision committed (W-1).
        string? newSlug = null;
        if (request.Title != null)
        {
            newSlug = SlugHelper.GenerateSlug(request.Title);
            if (newSlug != article.Slug)
            {
                var (clash, _) = await _wikiRepo.GetPagedAsync(1, 1, a => a.Slug == newSlug && a.Id != id);
                if (clash.Count > 0) return ApiResponse.Fail("An article with a similar title already exists.");
            }
        }

        var strategy = _db.Database.CreateExecutionStrategy();
        var result = await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                await _revisionRepo.AddAsync(new WikiRevision
                {
                    ArticleId = article.Id,
                    Content = article.Content,
                    EditedById = userId,
                    EditedByName = userName,
                    ChangeNote = request.ChangeNote ?? "Update"
                });

                if (request.Title != null)
                {
                    article.Title = request.Title;
                    article.Slug = newSlug!;
                }
                if (request.Content != null) article.Content = ContentSanitizer.Sanitize(request.Content);
                if (request.Status != null) article.Status = request.Status;
                article.LastEditedById = userId;
                article.LastEditedByName = userName;
                article.UpdatedAt = DateTime.UtcNow;

                await _wikiRepo.UpdateAsync(article);
                await tx.CommitAsync();
                return ApiResponse.Ok();
            }
            catch (DbUpdateException)
            {
                await tx.RollbackAsync();
                return ApiResponse.Fail("An article with a similar title already exists.");
            }
        });

        if (result.Success) await InvalidateAsync();
        return result;
    }

    public async Task<ApiResponse> DeleteArticleAsync(Guid articleId)
    {
        var article = await _wikiRepo.GetByIdAsync(articleId);
        if (article == null) return ApiResponse.Fail("Article not found.");
        await _wikiRepo.DeleteAsync(article);
        await InvalidateAsync();
        return ApiResponse.Ok();
    }

    public async Task<ApiResponse> SubmitContributionAsync(Guid articleId, SuggestEditRequest request, Guid userId, string userName)
    {
        var article = await _wikiRepo.GetByIdAsync(articleId);
        if (article == null) return ApiResponse.Fail("Article not found.");

        await _contributionRepo.AddAsync(new WikiContribution
        {
            ArticleId = articleId,
            ContributorId = userId,
            ContributorName = userName,
            SuggestedContent = ContentSanitizer.Sanitize(request.SuggestedContent),
            ChangeNote = request.ChangeNote
        });
        return ApiResponse.Ok();
    }

    public async Task<List<WikiContributionDto>> GetContributionsAsync(string status)
    {
        var contributions = await _contributionRepo.ListAsync(
            c => c.Status == status, q => q.OrderByDescending(c => c.SubmittedAt));

        var articleIds = contributions.Select(c => c.ArticleId).Distinct().ToList();
        var articles = (await _wikiRepo.ListAsync(a => articleIds.Contains(a.Id)))
            .ToDictionary(a => a.Id);

        var dtos = new List<WikiContributionDto>();
        foreach (var c in contributions)
        {
            articles.TryGetValue(c.ArticleId, out var article);
            dtos.Add(new WikiContributionDto(c.Id, c.ArticleId, article?.Title ?? string.Empty,
                article?.Slug ?? string.Empty, c.ContributorName ?? string.Empty, c.SuggestedContent,
                article?.Content ?? string.Empty, c.ChangeNote, c.Status, c.SubmittedAt));
        }
        return dtos;
    }

    public async Task<ApiResponse> ReviewContributionAsync(Guid contributionId, ReviewContributionRequest request, Guid reviewerId)
    {
        if (request.Status != ContributionStatus.Approved && request.Status != ContributionStatus.Rejected)
            return ApiResponse.Fail("Invalid status.");

        var contribution = await _contributionRepo.GetByIdAsync(contributionId);
        if (contribution == null || contribution.Status != ContributionStatus.Pending)
            return ApiResponse.Fail("Contribution not found or already reviewed.");

        // If approving, make sure the target article still exists — otherwise we'd mark the
        // contribution Approved while silently applying nothing (W-4).
        if (request.Status == ContributionStatus.Approved && await _wikiRepo.GetByIdAsync(contribution.ArticleId) is null)
            return ApiResponse.Fail("The target article has been deleted; this contribution can no longer be applied.");

        contribution.Status = request.Status;
        contribution.ReviewedById = reviewerId;
        contribution.ReviewedAt = DateTime.UtcNow;

        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync();
            await _contributionRepo.UpdateAsync(contribution);

            if (request.Status == ContributionStatus.Approved)
            {
                var article = await _wikiRepo.GetByIdAsync(contribution.ArticleId);
                if (article != null)
                {
                    await _revisionRepo.AddAsync(new WikiRevision
                    {
                        ArticleId = article.Id,
                        Content = article.Content,
                        EditedById = contribution.ContributorId,
                        EditedByName = contribution.ContributorName,
                        ChangeNote = contribution.ChangeNote ?? "Approved contribution"
                    });
                    article.Content = contribution.SuggestedContent;
                    article.LastEditedById = contribution.ContributorId;
                    article.LastEditedByName = contribution.ContributorName;
                    article.UpdatedAt = DateTime.UtcNow;
                    await _wikiRepo.UpdateAsync(article);
                }
            }

            await tx.CommitAsync();
        });

        return ApiResponse.Ok();
    }

    public async Task<ApiResponse<int>> CreateCategoryAsync(WikiCategoryRequest request)
    {
        var slug = SlugHelper.GenerateSlug(request.Name);
        if (await _wikiRepo.GetCategoryBySlugAsync(slug) != null)
            return ApiResponse<int>.Fail("A category with a similar name already exists.");

        var category = new WikiCategory
        {
            Name = request.Name,
            Slug = slug,
            Description = request.Description ?? string.Empty,
            IconUrl = request.IconUrl
        };
        // Optimistic check above for the message; TryAddAsync makes the unique-slug insert race-safe.
        if (!await _categoryRepo.TryAddAsync(category))
            return ApiResponse<int>.Fail("A category with a similar name already exists.");
        await InvalidateAsync();
        return ApiResponse<int>.Ok(category.Id);
    }

    public async Task<ApiResponse> UpdateCategoryAsync(int id, WikiCategoryRequest request)
    {
        var category = await _wikiRepo.GetCategoryByIdAsync(id);
        if (category == null) return ApiResponse.Fail("Category not found.");

        category.Name = request.Name;
        var newSlug = SlugHelper.GenerateSlug(request.Name);
        if (newSlug != category.Slug)
        {
            var clash = await _wikiRepo.GetCategoryBySlugAsync(newSlug);
            if (clash != null && clash.Id != id)
                return ApiResponse.Fail("A category with a similar name already exists.");
        }
        category.Slug = newSlug;
        category.Description = request.Description ?? string.Empty;
        category.IconUrl = request.IconUrl;
        try
        {
            await _categoryRepo.UpdateAsync(category);
        }
        catch (DbUpdateException)
        {
            return ApiResponse.Fail("A category with a similar name already exists.");
        }
        await InvalidateAsync();
        return ApiResponse.Ok();
    }

    public async Task<(bool Found, bool HasArticles)> DeleteCategoryAsync(int id)
    {
        var category = await _wikiRepo.GetCategoryByIdAsync(id);
        if (category == null) return (false, false);

        var count = await _wikiRepo.CountArticlesInCategoryAsync(id);
        if (count > 0) return (true, true);

        await _categoryRepo.DeleteAsync(category);
        await InvalidateAsync();
        return (true, false);
    }

    public async Task<List<WikiSearchResultDto>> SearchAsync(string query, int limit)
    {
        var articles = await _wikiRepo.SearchAsync(query, limit);
        var results = new List<WikiSearchResultDto>();
        foreach (var a in articles)
        {
            var category = await _wikiRepo.GetCategoryByIdAsync(a.CategoryId);
            results.Add(new WikiSearchResultDto(a.Id, a.Title, a.Slug, category?.Slug ?? string.Empty));
        }
        return results;
    }

    public Task<int> CountArticlesAsync() => _wikiRepo.CountAsync(a => a.Status == ArticleStatus.Published);
    public Task<int> CountPendingContributionsAsync() => _contributionRepo.CountAsync(c => c.Status == ContributionStatus.Pending);

    private static WikiRevisionDto ToRevisionDto(WikiRevision r) =>
        new(r.Id, r.ArticleId, r.Content, r.EditedByName, r.EditedAt, r.ChangeNote);
}
