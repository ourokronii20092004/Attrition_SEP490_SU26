using System.Collections.Generic;
using UnityEngine;

namespace Attrition.Controllers
{
    /// <summary>
    /// Theo dõi các Elite/Boss ĐÃ rơi đồ trong phiên chơi, để khi rest hồi sinh chúng
    /// thì KHÔNG phát đồ lần nữa (theo concept: elite/boss chỉ thưởng 1 lần).
    /// Khóa = enemyId + vị trí spawn làm tròn (ổn định qua các lần respawn cùng chỗ).
    /// Reset khi load lại scene/đổi map (host gọi Clear).
    /// </summary>
    public static class EnemyLootTracker
    {
        private static readonly HashSet<string> _looted = new();

        private static string Key(string enemyId, Vector3 spawnPos)
            => $"{enemyId}@{Mathf.RoundToInt(spawnPos.x)},{Mathf.RoundToInt(spawnPos.y)}";

        public static bool AlreadyLooted(string enemyId, Vector3 spawnPos)
            => _looted.Contains(Key(enemyId, spawnPos));

        public static void MarkLooted(string enemyId, Vector3 spawnPos)
            => _looted.Add(Key(enemyId, spawnPos));

        public static void Clear() => _looted.Clear();
    }
}
