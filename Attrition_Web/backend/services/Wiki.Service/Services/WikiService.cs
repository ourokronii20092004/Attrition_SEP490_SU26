using BuildingBlocks.Caching;
using BuildingBlocks.Contracts;
using BuildingBlocks.Web;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using Wiki.Service.DTOs;
using Wiki.Service.Models;

namespace Wiki.Service.Services;

public class WikiService : IWikiService
{
    private readonly IWikiRepository _wikiRepo;
    private readonly ICacheService _cache;

    public WikiService(IWikiRepository wikiRepo, ICacheService cache)
    {
        _wikiRepo = wikiRepo;
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

    public async Task<PaginatedResponse<WikiArticleListDto>> GetArticlesAsync(string? categorySlug, string? search, int page, int pageSize, Guid? authorId = null, bool includeUnpublished = false)
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
        // Published only for public callers; admins may pass includeUnpublished to also see drafts.
        // Layer on the optional category, search, and author filters.
        Expression<Func<WikiArticle, bool>> filter = a =>
            (includeUnpublished || a.Status == ArticleStatus.Published) &&
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
                a.CreatedById, a.CreatedByName, a.UpdatedAt, a.Status));
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
        var revisions = await _wikiRepo.Revisions.ListAsync(
            r => r.ArticleId == articleId, q => q.OrderByDescending(r => r.EditedAt));
        return revisions.Select(ToRevisionDto).ToList();
    }

    public async Task<WikiRevisionDto?> GetRevisionByIdAsync(Guid articleId, Guid revisionId)
    {
        var revision = await _wikiRepo.Revisions.GetByIdAsync(revisionId);
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
        if (await _wikiRepo.Categories.GetByIdAsync(request.CategoryId) is null)
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
        ApiResponse<string> result;
        try
        {
            result = await _wikiRepo.ExecuteInTransactionAsync(async () =>
            {
                await _wikiRepo.AddAsync(article);
                await _wikiRepo.Revisions.AddAsync(new WikiRevision
                {
                    ArticleId = article.Id,
                    Content = article.Content,
                    EditedById = userId,
                    EditedByName = userName,
                    ChangeNote = "Initial creation"
                });
                return ApiResponse<string>.Ok(slug);
            });
        }
        catch (DbUpdateException)
        {
            result = ApiResponse<string>.Fail("An article with a similar title already exists.");
        }

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

        ApiResponse result;
        try
        {
            result = await _wikiRepo.ExecuteInTransactionAsync(async () =>
            {
                await _wikiRepo.Revisions.AddAsync(new WikiRevision
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
                return ApiResponse.Ok();
            });
        }
        catch (DbUpdateException)
        {
            result = ApiResponse.Fail("An article with a similar title already exists.");
        }

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

        await _wikiRepo.Contributions.AddAsync(new WikiContribution
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
        // "all" returns every contribution regardless of review state (admin queue filter);
        // anything else is an exact status match.
        var contributions = await _wikiRepo.Contributions.ListAsync(
            status == "all" ? null : c => c.Status == status, q => q.OrderByDescending(c => c.SubmittedAt));

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

        var contribution = await _wikiRepo.Contributions.GetByIdAsync(contributionId);
        if (contribution == null || contribution.Status != ContributionStatus.Pending)
            return ApiResponse.Fail("Contribution not found or already reviewed.");

        // If approving, make sure the target article still exists — otherwise we'd mark the
        // contribution Approved while silently applying nothing (W-4).
        if (request.Status == ContributionStatus.Approved && await _wikiRepo.GetByIdAsync(contribution.ArticleId) is null)
            return ApiResponse.Fail("The target article has been deleted; this contribution can no longer be applied.");

        contribution.Status = request.Status;
        contribution.ReviewedById = reviewerId;
        contribution.ReviewedAt = DateTime.UtcNow;

        await _wikiRepo.ExecuteInTransactionAsync(async () =>
        {
            await _wikiRepo.Contributions.UpdateAsync(contribution);

            if (request.Status == ContributionStatus.Approved)
            {
                var article = await _wikiRepo.GetByIdAsync(contribution.ArticleId);
                if (article != null)
                {
                    await _wikiRepo.Revisions.AddAsync(new WikiRevision
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
        if (!await _wikiRepo.Categories.TryAddAsync(category))
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
            await _wikiRepo.Categories.UpdateAsync(category);
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

        await _wikiRepo.Categories.DeleteAsync(category);
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

    public Task<int> CountPendingContributionsAsync() => _wikiRepo.Contributions.CountAsync(c => c.Status == ContributionStatus.Pending);

    // A user's wiki contributions = published articles they authored (admins) + suggested edits of
    // theirs that were approved (regular users, who can't author articles directly). This is what
    // the profile "Wiki contributions" stat shows, so approving an edit makes the count go up.
    public async Task<int> CountUserContributionsAsync(Guid userId)
    {
        var authored = await _wikiRepo.CountAsync(a => a.CreatedById == userId && a.Status == ArticleStatus.Published);
        var approvedEdits = await _wikiRepo.Contributions.CountAsync(c => c.ContributorId == userId && c.Status == ContributionStatus.Approved);
        return authored + approvedEdits;
    }

    // Mirrors CountUserContributionsAsync but returns the actual history: published articles the user
    // authored, plus their approved suggested edits — merged and sorted newest-first. This backs the
    // profile "Wiki" activity tab (regular users only ever have approved edits, never authored articles).
    public async Task<List<UserWikiContributionDto>> GetUserContributionsAsync(Guid userId)
    {
        var items = new List<UserWikiContributionDto>();

        var authored = await _wikiRepo.ListAsync(a => a.CreatedById == userId && a.Status == ArticleStatus.Published);
        foreach (var a in authored)
            items.Add(new UserWikiContributionDto(a.Id, a.Title, a.Slug, "Authored", null, a.CreatedAt));

        var approvedEdits = await _wikiRepo.Contributions.ListAsync(
            c => c.ContributorId == userId && c.Status == ContributionStatus.Approved);
        if (approvedEdits.Count > 0)
        {
            var articleIds = approvedEdits.Select(c => c.ArticleId).Distinct().ToList();
            var articles = (await _wikiRepo.ListAsync(a => articleIds.Contains(a.Id))).ToDictionary(a => a.Id);
            foreach (var c in approvedEdits)
            {
                // Skip edits whose target article was since deleted.
                if (!articles.TryGetValue(c.ArticleId, out var article)) continue;
                items.Add(new UserWikiContributionDto(
                    article.Id, article.Title, article.Slug, "Edited", c.ChangeNote, c.ReviewedAt ?? c.SubmittedAt));
            }
        }

        return items.OrderByDescending(i => i.At).ToList();
    }

    private static WikiRevisionDto ToRevisionDto(WikiRevision r) =>
        new(r.Id, r.ArticleId, r.Content, r.EditedByName, r.EditedAt, r.ChangeNote);
}