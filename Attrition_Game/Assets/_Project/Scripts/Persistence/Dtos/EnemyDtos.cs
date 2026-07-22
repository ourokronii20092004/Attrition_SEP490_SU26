using System;
using System.Collections.Generic;

namespace Attrition.Persistence.Dtos
{
    /// <summary>Khớp BuildingBlocks.Contracts.ApiResponse&lt;T&gt; của backend.</summary>
    [Serializable]
    public class ApiResponse<T>
    {
        public bool Success;
        public T Data;
        public string Error;
    }

    /// <summary>
    /// Khớp Enemy.Service EnemyResponse (GET api/enemies/{id}).
    /// Dùng để override chỉ số quái mặc định bằng giá trị admin sửa trên web.
    /// </summary>
    [Serializable]
    public class EnemyResponseDto
    {
        public string EnemyId;
        public string Name;
        public string Tier;
        public string SpawnBiome;
        public int Hp;
        public int Ad;
        public int Ap;
        public int Def;
        public int Res;
        public float AttackSpeed;
        public bool IsRanged;
        public int ExpReward;
        public int GoldReward;
        public string Lore;
        public string ImageUrl;
        public int Poise;
        public float PoiseRecoveryTime;
        public float PatrolSpeed;
        public float ChaseSpeed;
        public List<LootEntryDto> LootTable;
    }

    [Serializable]
    public class LootEntryDto
    {
        public string ItemName;
        public string Rarity;
        public string IconKey;
        public float DropChance;
        public short MinQty;
        public short MaxQty;
    }

    /// <summary>Khớp Enemy.Service GameConfigVersion (GET /api/gameconfig/version) — nhẹ, chỉ so version.</summary>
    [Serializable]
    public class GameConfigVersionDto
    {
        public string Version;
        public int Count;
    }

    /// <summary>Khớp Enemy.Service GameConfigVersions (GET /api/gameconfig/versions) — version gộp enemy + item.</summary>
    [Serializable]
    public class GameConfigVersionsDto
    {
        public string EnemyVersion;
        public int EnemyCount;
        public string ItemVersion;
        public int ItemCount;
    }

    /// <summary>Khớp Enemy.Service GameConfigBundle (GET /api/gameconfig) — cục config game tải 1 lần.</summary>
    [Serializable]
    public class GameConfigBundleDto
    {
        public string Version;
        public int Count;
        public List<EnemyResponseDto> Enemies;
    }
}
