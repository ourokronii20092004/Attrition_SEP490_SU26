using System;

namespace Attrition.API.Models;

public class Asset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string AssetType { get; set; } = "image"; // "concept-art" | "screenshot" | "sprite" | "document" | "lore"
    public string MimeType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Tags { get; set; }
    public Guid? UploadedById { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
