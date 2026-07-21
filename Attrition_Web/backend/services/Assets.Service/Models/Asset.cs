namespace Assets.Service.Models;

public class Asset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string AssetType { get; set; } = "image"; // concept-art | screenshot | sprite | document | lore
    public string MimeType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Tags { get; set; }

    // Optional link back to the entity this asset depicts (e.g. an enemy sprite or item icon),
    // so game-content images surface in the gallery and can be traced to their source.
    // SourceType: enemy | item | null (general gallery asset).
    public string? SourceType { get; set; }
    public string? SourceId { get; set; }
    public string? ContentHash { get; set; }

    // Uploader ref: plain Guid + denormalized name (no cross-schema FK).
    public Guid? UploadedById { get; set; }
    public string? UploadedByName { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
