namespace Enemy.Service.DTOs;

public record ItemModifierDto(string Stat, int Amount);

public record ItemResponse(
    string ItemId,
    string Name,
    string Category,
    string Rarity,
    string? IconKey,
    string? Description,
    int MaxStack,
    bool IsKeyItem,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<ItemModifierDto> Modifiers,
    string? ImageUrl = null
);

public record ItemCreateRequest(
    string ItemId,
    string Name,
    string Category,
    string Rarity,
    string? IconKey,
    string? Description,
    int MaxStack,
    bool IsKeyItem,
    List<ItemModifierDto>? Modifiers,
    string? ImageUrl = null
);

public record ItemUpdateRequest(
    string Name,
    string Category,
    string Rarity,
    string? IconKey,
    string? Description,
    int MaxStack,
    bool IsKeyItem,
    List<ItemModifierDto>? Modifiers,
    string? ImageUrl = null
);

/// <summary>Cục item config gộp cho GAME tải 1 lần. Version = MAX(UpdatedAt)|count (giống enemy).</summary>
public record ItemConfigBundle(string Version, int Count, List<ItemResponse> Items);
