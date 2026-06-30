using Enemy.Service.Models;

namespace Enemy.Service.Data;

/// <summary>
/// Dữ liệu gốc trích từ ScriptableObject game (Assets/_Project/Data/Enemies + /Items).
/// Field game không có (SpawnBiome/IsRanged/GoldReward/Lore cho enemy; Rarity cho item) → để default.
/// Tier: 0=Normal,1=Elite,2=Boss. Stat enum game → chuỗi: 0=MaxHP,2=MaxStamina,3=AD,5=DEF,6=RES.
/// </summary>
internal static class SeedData
{
    private static EnemyEntity E(string id, string name, string tier, int hp, int ad, int ap, int def, int res, float atkSpd, int exp) =>
        new() { EnemyId = id, Name = name, Tier = tier, Hp = hp, Ad = ad, Ap = ap, Def = def, Res = res, AttackSpeed = atkSpd, ExpReward = exp };

    public static List<EnemyEntity> Enemies() => new()
    {
        E("axe_demon", "Axe Demon", "Normal", 40, 30, 0, 4, 0, 1f, 10),
        E("bat", "Bat", "Normal", 40, 10, 0, 0, 0, 1f, 10),
        E("crab", "Crab", "Elite", 120, 10, 10, 20, 10, 1f, 50),
        E("cultist", "Cultist", "Elite", 120, 18, 0, 5, 0, 1f, 50),
        E("flying_demon", "Flying Demon", "Normal", 20, 10, 0, 0, 0, 1f, 10),
        E("frogger", "Frogger", "Elite", 120, 18, 0, 5, 0, 1f, 50),
        E("Gollux", "Gollux", "Elite", 120, 18, 0, 20, 0, 1f, 50),
        E("huntress_bow", "Huntress Bow", "Normal", 40, 10, 0, 0, 0, 1f, 10),
        E("huntress", "Huntress", "Normal", 50, 10, 0, 0, 0, 1f, 10),
        E("mimic", "Mimic", "Normal", 40, 10, 0, 0, 0, 1f, 10),
        E("mushroom", "Mushroom", "Normal", 40, 10, 0, 0, 0, 1f, 10),
        E("nightborne", "NightBorne", "Elite", 120, 18, 0, 5, 0, 1f, 50),
        E("rat", "Rat", "Normal", 20, 10, 0, 0, 0, 1f, 10),
        E("red_bat", "Red Bat", "Normal", 40, 10, 0, 0, 0, 1f, 10),
        E("severed_fang", "Severed Fang", "Boss", 1500, 25, 30, 15, 15, 1.1f, 50),
        E("skeleton_sword", "Skeleton", "Normal", 30, 10, 0, 0, 0, 1f, 10),
        E("slime2", "Slime II", "Normal", 30, 10, 0, 15, 0, 1f, 10),
        E("slime", "Slime", "Normal", 30, 10, 0, 15, 0, 1f, 10),
        E("summon_of_undead", "Summon of Undead", "Normal", 10, 0, 0, 0, 0, 1f, 0),
        E("the_dark", "The Dark", "Normal", 80, 10, 0, 5, 5, 1f, 10),
        E("undead", "Undead", "Elite", 120, 18, 0, 5, 0, 1f, 50),
    };

    private static ItemEntity I(string id, string name, string category, string rarity, int maxStack, params (string stat, int amount)[] mods) =>
        new()
        {
            ItemId = id, Name = name, Category = category, Rarity = rarity, MaxStack = maxStack,
            Modifiers = mods.Select(m => new ItemModifierEntry { Stat = m.stat, Amount = m.amount }).ToList()
        };

    public static List<ItemEntity> Items() => new()
    {
        // Equipment — leather (Common), iron/bronze (Uncommon), gold (Rare).
        I("leather_helm", "Leather Helm", "Equipment", "Common", 1, ("DEF", 2)),
        I("leather_chest", "Leather Armor", "Equipment", "Common", 1, ("DEF", 4), ("MaxHP", 10)),
        I("leather_boots", "Leather Boots", "Equipment", "Common", 1, ("DEF", 1), ("RES", 1)),
        I("iron_helm", "Iron Helm", "Equipment", "Uncommon", 1, ("DEF", 4)),
        I("iron_chest", "Iron Chestplate", "Equipment", "Uncommon", 1, ("DEF", 8), ("MaxHP", 20)),
        I("iron_legs", "Iron Greaves", "Equipment", "Uncommon", 1, ("DEF", 5)),
        I("iron_boots", "Iron Boots", "Equipment", "Uncommon", 1, ("DEF", 3), ("RES", 3)),
        I("bronze_helm", "Bronze Helm", "Equipment", "Uncommon", 1, ("DEF", 3)),
        I("bronze_chest", "Bronze Armor", "Equipment", "Uncommon", 1, ("DEF", 6), ("MaxHP", 15)),
        I("bronze_boots", "Bronze Boots", "Equipment", "Uncommon", 1, ("DEF", 2), ("RES", 2)),
        I("gold_helm", "Gilded Helm", "Equipment", "Rare", 1, ("DEF", 6), ("RES", 2)),
        I("gold_chest", "Gilded Armor", "Equipment", "Rare", 1, ("DEF", 5), ("MaxHP", 30)),
        I("gold_boots", "Gilded Boots", "Equipment", "Rare", 1, ("DEF", 4), ("RES", 4)),

        // Accessory.
        I("acc_double_jump", "Feather Charm", "Accessory", "Rare", 1),
        I("acc_power_ring", "Power Ring", "Accessory", "Rare", 1, ("AD", 6)),
        I("acc_shadow_dash", "Shadow Cloak", "Accessory", "Rare", 1),
        I("acc_stamina_charm", "Vigor Charm", "Accessory", "Rare", 1, ("MaxStamina", 20)),

        // Skill.
        I("skill_fire", "Fireball", "Skill", "Rare", 1),
        I("skill_wood", "Thorn Lash", "Skill", "Rare", 1),
        I("skill_earth", "Stone Spike", "Skill", "Rare", 1),
        I("skill_thunder", "Chain Bolt", "Skill", "Rare", 1),
        I("skill_thrust", "Phantom Thrust", "Skill", "Rare", 1),

        // Consumable (potions) → Material category, stackable.
        I("health_potion", "Health Potion", "Material", "Common", 99),
        I("mana_potion", "Mana Potion", "Material", "Common", 99),
    };
}
