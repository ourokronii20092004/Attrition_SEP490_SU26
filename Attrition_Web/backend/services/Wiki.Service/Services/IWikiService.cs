using BuildingBlocks.Contracts;
using Wiki.Service.DTOs;
using Wiki.Service.Models;

namespace Wiki.Service.Services;

public interface IWikiService
{
    Task<List<WikiCategoryDto>> GetCategoriesAsync();
    Task<PaginatedResponse<WikiArticleListDto>> GetArticlesAsync(string? categorySlug, string? search, int page, int pageSize, Guid? authorId = null);
    Task<WikiArticleDto?> GetArticleBySlugAsync(string slug, bool includeUnpublished = false);
    Task<List<WikiRevisionDto>> GetRevisionsAsync(Guid articleId);
    Task<WikiRevisionDto?> GetRevisionByIdAsync(Guid articleId, Guid revisionId);
    Task<ApiResponse<string>> CreateArticleAsync(CreateArticleRequest request, Guid userId, string userName);
    Task<ApiResponse> UpdateArticleAsync(Guid id, UpdateArticleRequest request, Guid userId, string userName);
    Task<ApiResponse> DeleteArticleAsync(Guid articleId);
    Task<ApiResponse> SubmitContributionAsync(Guid articleId, SuggestEditRequest request, Guid userId, string userName);
    Task<List<WikiContributionDto>> GetContributionsAsync(string status);
    Task<ApiResponse> ReviewContributionAsync(Guid contributionId, ReviewContributionRequest request, Guid reviewerId);

    // Category management (admin)
    Task<ApiResponse<int>> CreateCategoryAsync(WikiCategoryRequest request);
    Task<ApiResponse> UpdateCategoryAsync(int id, WikiCategoryRequest request);
    Task<(bool Found, bool HasArticles)> DeleteCategoryAsync(int id);

    // Aggregator support
    Task<List<WikiSearchResultDto>> SearchAsync(string query, int limit);
    Task<int> CountArticlesAsync();
    Task<int> CountPendingContributionsAsync();
}
