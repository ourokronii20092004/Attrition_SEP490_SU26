namespace Character.Service.DTOs;

/// <summary>
/// One row in the save-file list. Deliberately light: the list renders dozens of these, and the
/// heavy blobs (inventory, allocated points) are only needed once a save is opened.
/// </summary>
public record SaveListItemDto(
    long Id,
    Guid? SessionId,
    string? RoomCode,
    string? CurrentScene,
    string EventType,
    int CurrentLevel,
    int CurrentHp,
    int MaxHp,
    int DeathCount,
    int PlaytimeSeconds,
    bool IsAlive,
    DateTime CapturedAt,
    /// <summary>True for the newest save — the one that is the character's current progress.</summary>
    bool IsCurrent);

/// <summary>
/// A save file in full: everything needed to re-render every number on the character page as it
/// was at that moment. This is what makes clicking a save change the display.
/// </summary>
public record SaveDetailDto(
    long Id,
    Guid CharacterId,
    Guid? SessionId,
    string? RoomCode,
    string? CurrentScene,
    string EventType,
    short PlayerRole,
    int CurrentLevel,
    int CurrentExp,
    int DeathCount,
    int PlaytimeSeconds,
    bool IsAlive,
    string? AllocatedPointsJson,
    int MaxHp, int CurrentHp, int MaxMana, int CurrentMana, int MaxStamina,
    float AttackSpeed,
    int PotionMaxFlasks, int PotionMaxManaFlasks, int HealthCharges, int ManaCharges,
    int Ad, int Ap, int Def, int Res,
    float PosX, float PosY, float PosZ,
    string? LastRestPointId,
    string? InventoryJson,
    DateTime CapturedAt,
    bool IsCurrent);

/// <summary>Paged save history.</summary>
public record SaveListDto(List<SaveListItemDto> Items, int TotalCount, int Page, int PageSize);

/// <summary>
/// Result of deleting a save, shaped so the client can report what actually happened rather than
/// assuming. <paramref name="RolledBackCharacter"/> is true when the deleted save was the newest and
/// live state was rewritten from the previous one.
/// </summary>
public record DeleteSaveResultDto(
    bool WasCurrent,
    bool RolledBackCharacter,
    bool RolledBackWorldState,
    DateTime? NowCurrentAt,
    int RemainingSaves);

/// <summary>
/// Deleting a save. <paramref name="AlsoRollBackWorldState"/> is honoured only for the room owner
/// and only when the save being deleted is the newest — rolling the world back rewrites progress
/// for every player in the room, so it is opt-in and never the default.
/// </summary>
public record DeleteSaveRequest(bool AlsoRollBackWorldState = false);

/// <summary>Room-state snapshot in a timeline (admin, and the owner's rollback preview).</summary>
public record RoomStateSaveDto(
    long Id,
    DateTime CapturedAt,
    string EventType,
    string? CurrentScene,
    int PlayTimeSeconds,
    int WorldStateCount,
    int FogCellCount);

/// <summary>One member of a room's party — who played with whom, and in what role.</summary>
public record RoomPartyMemberDto(
    Guid CharacterId,
    string CharacterName,
    Guid OwnerId,
    string? OwnerUsername,
    short PlayerRole,
    int CurrentLevel,
    int DeathCount,
    DateTime UpdatedAt);

/// <summary>A room in the admin list: who hosted, who joined, and how far the world has progressed.</summary>
public record AdminRoomListItemDto(
    Guid Id,
    string RoomCode,
    string Name,
    Guid OwnerId,
    string? OwnerUsername,
    bool IsMultiplayer,
    int PlayerCount,
    string? CurrentScene,
    int PlayTimeSeconds,
    DateTime LastPlayedAt,
    int WorldStateCount);

public record AdminRoomListDto(List<AdminRoomListItemDto> Items, int TotalCount, int Page, int PageSize);

/// <summary>Full room view for an admin: the party, the shared world state, and its history.</summary>
public record AdminRoomDetailDto(
    Guid Id,
    string RoomCode,
    string Name,
    Guid OwnerId,
    string? OwnerUsername,
    bool IsMultiplayer,
    string? CurrentScene,
    int PlayTimeSeconds,
    DateTime CreatedAt,
    DateTime LastPlayedAt,
    List<RoomPartyMemberDto> Party,
    List<WorldStateDto> WorldStates,
    string? FogJson,
    List<RoomStateSaveDto> StateHistory);