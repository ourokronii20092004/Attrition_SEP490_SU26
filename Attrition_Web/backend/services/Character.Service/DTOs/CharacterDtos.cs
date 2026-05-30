namespace Character.Service.DTOs;

// ─── Snapshot views ───
public record SnapshotDto(
    int Level, int Hp, int MaxHp, int Gold, bool IsAlive,
    string? RoomCode, string EventType, int PlaytimeSeconds, DateTime CapturedAt);

// Character with its latest snapshot flattened in (for list views). LatestSnapshot null = no snapshots yet.
public record CharacterSummaryDto(
    Guid Id, Guid OwnerId, string Name, string Archetype,
    DateTime CreatedAt, DateTime UpdatedAt, int SnapshotCount, SnapshotDto? LatestSnapshot);

// Character with full snapshot history (detail view).
public record CharacterDetailDto(
    Guid Id, Guid OwnerId, string Name, string Archetype,
    DateTime CreatedAt, DateTime UpdatedAt, List<SnapshotDto> Snapshots);

// Admin row — adds owner display fields resolved from Identity (kept null in the stub for now).
public record AdminCharacterDto(
    Guid Id, Guid OwnerId, string? OwnerUsername, string Name, string Archetype,
    DateTime UpdatedAt, SnapshotDto? LatestSnapshot);

// ─── Game-client ingestion (internal, X-Internal-Key) ───
public record SnapshotIngestRequest(
    Guid OwnerId, Guid? CharacterId, string Name, string Archetype,
    int Level, int Hp, int MaxHp, int Gold, bool IsAlive,
    string? RoomCode, string EventType, int PlaytimeSeconds);
