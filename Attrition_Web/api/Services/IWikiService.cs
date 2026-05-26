using Attrition.API.DTOs;
using Attrition.API.Models;

namespace Attrition.API.Services;

public interface IWikiService
{
    Task<List<WikiCategoryDto>> GetCategoriesAsync();
    Task<PaginatedResponse<WikiArticleListDto>> GetArticlesAsync(string? categorySlug, string? search, int page, int pageSize);
    Task<WikiArticleDto?> GetArticleBySlugAsync(string slug);
    Task<List<WikiRevision>> GetRevisionsAsync(Guid articleId);
    Task<WikiRevision?> GetRevisionByIdAsync(Guid articleId, Guid revisionId);
    Task<ApiResponse<string>> CreateArticleAsync(CreateArticleRequest request, Guid userId);
    Task<ApiResponse> UpdateArticleAsync(Guid id, UpdateArticleRequest request, Guid userId);
    Task<ApiResponse> SubmitContributionAsync(Guid articleId, SuggestEditRequest request, Guid userId);
    Task<List<WikiContributionDto>> GetContributionsAsync(string status);
    Task<ApiResponse> ReviewContributionAsync(Guid contributionId, ReviewContributionRequest request, Guid reviewerId);
    Task<ApiResponse> DeleteArticleAsync(Guid articleId);
}
