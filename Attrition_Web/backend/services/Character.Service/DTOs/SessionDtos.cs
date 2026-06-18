namespace Character.Service.DTOs;

// ─── Player-facing views (JWT) ───

// One character's progress within a room (for the room detail / load view).
public record CharacterSessionDto(
    Guid CharacterId, Guid SessionId, short PlayerRole,
    int CurrentLevel, int CurrentExp, string? AllocatedPointsJson,
    int MaxHp, int CurrentHp, int MaxMana, int CurrentMana, int MaxStamina,
    int PotionMaxFlasks, float AttackSpeed, float PosX, float PosY,
    string? LastRestPointId, string? InventoryJson, string? EquipmentJson,
    DateTime UpdatedAt);

public record WorldStateDto(string EventId, short StateValue, int Progress, DateTime UpdatedAt);

// A saved journey (room) in list views. CharacterCount lets the menu show "1/2 players".
public record SessionSummaryDto(
    Guid Id, Guid OwnerId, string RoomCode, string Name, bool IsMultiplayer,
    int PlayTimeSeconds, string? CurrentScene,
    DateTime CreatedAt, DateTime UpdatedAt, DateTime LastPlayedAt,
    int CharacterCount);

// Full room load: the session plus every character's progress and the world state.
public record SessionDetailDto(
    Guid Id, Guid OwnerId, string RoomCode, string Name, bool IsMultiplayer,
    int PlayTimeSeconds, string? CurrentScene,
    DateTime CreatedAt, DateTime UpdatedAt, DateTime LastPlayedAt,
    List<CharacterSessionDto> Characters, List<WorldStateDto> WorldStates);

// ─── Game-client ingestion (internal, X-Internal-Key) ───

// Host creates (or re-opens) a room. RoomCode null/empty → server generates a fixed unique code.
// When RoomCode matches an existing row owned by OwnerId, that room is returned (re-open), not duplicated.
public record CreateSessionRequest(
    Guid OwnerId, string Name, string? RoomCode = null, string? CurrentScene = null);

// Host pushes one character's progress for a room. Upsert keyed on (CharacterId, SessionId).
// JSON blob fields null = keep existing (don't wipe inventory when a save omits it).
public record SaveCharacterSessionRequest(
    Guid CharacterId, Guid SessionId, short PlayerRole,
    int CurrentLevel, int CurrentExp, string? AllocatedPointsJson,
    int MaxHp, int CurrentHp, int MaxMana, int CurrentMana, int MaxStamina,
    int PotionMaxFlasks, float AttackSpeed, float PosX, float PosY,
    string? LastRestPointId, string? InventoryJson = null, string? EquipmentJson = null);

// Host pushes world/quest progress for a room. Upsert keyed on (SessionId, EventId).
public record SaveWorldStateRequest(Guid SessionId, string EventId, short StateValue, int Progress);

// Host reports room-level metadata on save/quit (playtime, current scene).
public record UpdateSessionRequest(Guid SessionId, int PlayTimeSeconds, string? CurrentScene = null);
