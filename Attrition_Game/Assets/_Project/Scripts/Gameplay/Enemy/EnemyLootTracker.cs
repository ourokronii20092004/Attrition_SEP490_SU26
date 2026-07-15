using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Attrition.Controllers
{
    /// <summary>
    /// Theo dõi các Elite/Boss ĐÃ rơi đồ trong phiên chơi, để khi rest hồi sinh chúng
    /// thì KHÔNG phát đồ lần nữa (theo concept: elite/boss chỉ thưởng 1 lần).
    /// Khóa = enemyId + vị trí spawn làm tròn (ổn định qua các lần respawn cùng chỗ).
    /// TỰ reset khi load scene mới (đổi map / chơi lại từ menu) — đăng ký sceneLoaded 1 lần lúc khởi
    /// động, KHÔNG cần ai gọi Clear (NetworkLauncher ở assembly khác không ref được class này).
    /// </summary>
    public static class EnemyLootTracker
    {
        private static readonly HashSet<string> _looted = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void HookSceneReset()
        {
            // Gỡ trước khi gắn để tránh nhân đôi handler nếu domain không reload (Enter Play Mode Options).
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // Vào scene gameplay mới = phiên loot mới → xoá cờ để elite/boss ở map mới rớt đồ đúng.
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => _looted.Clear();

        private static string Key(string enemyId, Vector3 spawnPos)
            => $"{enemyId}@{Mathf.RoundToInt(spawnPos.x)},{Mathf.RoundToInt(spawnPos.y)}";

        public static bool AlreadyLooted(string enemyId, Vector3 spawnPos)
            => _looted.Contains(Key(enemyId, spawnPos));

        public static void MarkLooted(string enemyId, Vector3 spawnPos)
            => _looted.Add(Key(enemyId, spawnPos));

        public static void Clear() => _looted.Clear();
    }
}
