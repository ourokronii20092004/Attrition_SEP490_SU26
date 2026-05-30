namespace Music.Service.Models;

public class MusicAlbum
{
    public int AlbumId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public List<string> Artists { get; set; } = new();
    public string? Description { get; set; }
    public string? CoverPath { get; set; }
    public bool IsCoverUserDefined { get; set; }
    public string? Genre { get; set; }
    public DateTime? ReleaseDate { get; set; }
    public string AlbumType { get; set; } = "soundtrack";
    public int TrackCount { get; set; }
    public int TotalDuration { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<MusicTrack> Tracks { get; set; } = new List<MusicTrack>();
}

public class MusicTrack
{
    public int TrackId { get; set; }
    public int AlbumId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public List<string> Artists { get; set; } = new();
    public string? CoverPath { get; set; }
    public int TrackNumber { get; set; }
    public int Duration { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public long? FileSize { get; set; }
    public string? Genre { get; set; }
    public int? Bpm { get; set; }
    public int PlayCount { get; set; }
    public bool IsFeatured { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class MusicPlaylist
{
    public Guid PlaylistId { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }                  // no FK to identity
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsPublic { get; set; }
    public int TrackCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PlaylistTrack> Tracks { get; set; } = new List<PlaylistTrack>();
}

public class PlaylistTrack
{
    public Guid PlaylistId { get; set; }
    public int TrackId { get; set; }
    public int Position { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}

public class UserFavorite
{
    public Guid UserId { get; set; }                  // no FK to identity
    public int TrackId { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
