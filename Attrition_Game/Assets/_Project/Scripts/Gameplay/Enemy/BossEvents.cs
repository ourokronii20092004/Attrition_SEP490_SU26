using System;

namespace Attrition.Controllers
{
    /// <summary>
    /// Cầu nối Boss → UI không tạo phụ thuộc ngược (Gameplay không ref UI).
    /// BossController phát sự kiện; GameUIController (ở assembly UI) lắng nghe để bật/cập nhật thanh máu.
    /// </summary>
    public static class BossEvents
    {
        /// <summary>(tên boss, maxHP) — boss xuất hiện, hiện thanh máu.</summary>
        public static event Action<string, int> OnBossSpawned;
        /// <summary>(currentHP, maxHP) — cập nhật thanh máu.</summary>
        public static event Action<int, int> OnBossHpChanged;
        /// <summary>Boss chết/biến mất — ẩn thanh máu.</summary>
        public static event Action OnBossDespawned;

        public static void RaiseSpawned(string name, int maxHp) => OnBossSpawned?.Invoke(name, maxHp);
        public static void RaiseHpChanged(int hp, int maxHp) => OnBossHpChanged?.Invoke(hp, maxHp);
        public static void RaiseDespawned() => OnBossDespawned?.Invoke();
    }
}
