namespace Attrition.API.DTOs;

public record ApiResponse<T>(bool Success, T? Data = default, string? Error = null);
public record ApiResponse(bool Success, string? Error = null);