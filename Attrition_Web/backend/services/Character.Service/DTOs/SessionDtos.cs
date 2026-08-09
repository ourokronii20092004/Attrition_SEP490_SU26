namespace Character.Service.DTOs;

// One character's progress within a room (for the room detail / load view).
public record CharacterSessionDto(
    Guid CharacterId, Guid SessionId, short PlayerRole,
    int CurrentLevel, int CurrentExp, string? AllocatedPointsJson,
    int MaxHp, int CurrentHp, int MaxMana, int CurrentMana, int MaxStamina,
    int PotionMaxFlasks, int PotionMaxManaFlasks, float AttackSpeed, float PosX, float PosY,
    string? LastRestPointId, string? InventoryJson, string? EquipmentJson,
    DateTime UpdatedAt,
    // Added with the consolidated bulk save: deaths in this room, and CURRENT potion charges
    // (only the maxima were persisted before, so charges always reloaded full).
    int DeathCount = 0, int HealthCharges = 0, int ManaCharges = 0, float PosZ = 0,
    // Joined from the characters table for display — the session row stores only CharacterId, so a
    // room view would otherwise show raw GUIDs. Null when the character row no longer exists.
    string? Name = null, string? Archetype = null,
    // Final combat stats (base + allocated points + equipped gear). PlayerStats computes these at
    // runtime and never stored them; the web has no gear stat table, so it can't recompute them.
    int Ad = 0, int Ap = 0, int Def = 0, int Res = 0);

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
    List<CharacterSessionDto> Characters, List<WorldStateDto> WorldStates,
    // Fog-of-war revealed cells for the whole party ("scene:cellX:cellY" keys). Room-level, not
    // per-character: co-op players explore one shared map.
    string? FogJson = null);

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
    int PotionMaxFlasks, int PotionMaxManaFlasks, float AttackSpeed, float PosX, float PosY,
    string? LastRestPointId, string? InventoryJson = null, string? EquipmentJson = null);

// Host pushes world/quest progress for a room. Upsert keyed on (SessionId, EventId).
public record SaveWorldStateRequest(Guid SessionId, string EventId, short StateValue, int Progress);

// Host reports room-level metadata on save/quit (playtime, current scene).
public record UpdateSessionRequest(Guid SessionId, int PlayTimeSeconds, string? CurrentScene = null);

// ── Consolidated save (one push per save) ────────────────────────────────────────────────────
// Replaces the old fan-out of N snapshots + N character-sessions + 1 meta call with a single
// request. The host builds the whole party's state and posts it once; the server applies every
// row in ONE transaction so a partial failure can't leave two players' progress inconsistent.

// One character inside a bulk save. OwnerId is the user this character belongs to — the server
// VERIFIES it against the characters table rather than trusting it, so a host cannot write rows
// for a character they don't own (see BulkSaveAsync).
public record BulkCharacterDto(
    Guid CharacterId, Guid OwnerId, short PlayerRole,
    string Name, string Archetype,
    int CurrentLevel, int CurrentExp, string? AllocatedPointsJson,
    int MaxHp, int CurrentHp, int MaxMana, int CurrentMana, int MaxStamina,
    int PotionMaxFlasks, int PotionMaxManaFlasks, int HealthCharges, int ManaCharges,
    float AttackSpeed, int Ad, int Ap, int Def, int Res,
    float PosX, float PosY, float PosZ,
    string? LastRestPointId, string? InventoryJson, string? EquipmentJson,
    int DeathCount, bool IsAlive);

// One world/quest flag in a bulk save (defeated bosses, discovered checkpoints, quest progress).
public record BulkWorldStateDto(string EventId, short StateValue, int Progress);

// The whole party's state in one payload. EventType mirrors the client's SaveEvent
// ("rest" | "quit"). Blob fields null = keep existing, matching SaveCharacterSessionRequest.
public record BulkSaveRequest(
    Guid SessionId, int PlayTimeSeconds, string? CurrentScene, string? FogJson,
    string EventType, string? RoomCode,
    List<BulkCharacterDto> Characters, List<BulkWorldStateDto> WorldStates);

// What the server actually committed, so the client can log/diagnose a partial accept. Skipped
// lists a characterId the caller was not allowed to write (ownership mismatch) — the rest still saved.
public record BulkSaveResultDto(
    Guid SessionId, int CharactersSaved, int WorldStatesSaved, List<Guid> Skipped);