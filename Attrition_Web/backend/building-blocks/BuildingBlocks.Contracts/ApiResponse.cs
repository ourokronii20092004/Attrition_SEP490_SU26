namespace BuildingBlocks.Contracts;

public record ApiResponse<T>(bool Success, T? Data = default, string? Error = null)
{
    public static ApiResponse<T> Ok(T data) => new(true, data);
    public static ApiResponse<T> Fail(string error) => new(false, default, error);
}

public record ApiResponse(bool Success, string? Error = null)
{
    public static ApiResponse Ok() => new(true);
    public static ApiResponse Fail(string error) => new(false, error);
}

public record PaginatedResponse<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
