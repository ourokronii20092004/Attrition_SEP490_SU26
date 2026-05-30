namespace Assets.Service.DTOs;

public record AssetDto(
    Guid Id,
    string FileName,
    string FilePath,
    string AssetType,
    string MimeType,
    long FileSize,
    string? Title,
    string? Description,
    string? Tags,
    string? UploadedBy,
    DateTime UploadedAt,
    DateTime? UpdatedAt
);

public record UpdateAssetReq(string? Title, string? Description, string? Tags, string? AssetType);
