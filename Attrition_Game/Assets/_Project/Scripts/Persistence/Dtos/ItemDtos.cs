using System;
using System.Collections.Generic;

namespace Attrition.Persistence.Dtos
{
    /// <summary>Khớp Enemy.Service ItemModifierDto — 1 dòng cộng chỉ số (stat + amount).</summary>
    [Serializable]
    public class ItemModifierDto
    {
        public string Stat;
        public int Amount;
    }

    /// <summary>
    /// Khớp Enemy.Service ItemResponse (GET /api/items, /api/itemconfig).
    /// Override config item mặc định bằng giá trị admin sửa trên web.
    /// </summary>
    [Serializable]
    public class ItemResponseDto
    {
        public string ItemId;
        public string Name;
        public string Category;
        public string Rarity;
        public string IconKey;
        public string Description;
        public int MaxStack;
        public bool IsKeyItem;
        public List<ItemModifierDto> Modifiers;
    }

    /// <summary>Khớp Enemy.Service ItemConfigBundle (GET /api/itemconfig) — cục item game tải 1 lần.</summary>
    [Serializable]
    public class ItemConfigBundleDto
    {
        public string Version;
        public int Count;
        public List<ItemResponseDto> Items;
    }
}
