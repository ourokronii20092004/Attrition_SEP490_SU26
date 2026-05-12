namespace Attrition.API.DTOs;

public record WikiCategoryDto(int Id, string Name, string Slug, string Description, string? IconUrl, int ArticleCount);
public record WikiArticleListDto(Guid Id, string Title, string Slug, string CategorySlug, string? AuthorName, DateTime UpdatedAt);
public record WikiArticleDto(Guid Id, string Title, string Slug, string CategorySlug, string Content,
    string? AuthorName, string? LastEditorName, string Status, DateTime CreatedAt, DateTime UpdatedAt);
public record CreateArticleRequest(string Title, int CategoryId, string Content, string Status);
public record UpdateArticleRequest(string? Title, string? Content, string? Status, string? ChangeNote);
public record SuggestEditRequest(string SuggestedContent, string? ChangeNote);
public record WikiContributionDto(Guid Id, Guid ArticleId, string ArticleTitle, string ContributorName,
    string SuggestedContent, string? ChangeNote, string Status, DateTime SubmittedAt);
public record ReviewContributionRequest(string Status); // "Approved" or "Rejected"