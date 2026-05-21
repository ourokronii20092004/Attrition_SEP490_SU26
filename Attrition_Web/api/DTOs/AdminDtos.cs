namespace Attrition.API.DTOs;

public record WikiCategoryRequest(string Name, string? Description, string? IconUrl);
public record RemovePostRequest(string Reason);
