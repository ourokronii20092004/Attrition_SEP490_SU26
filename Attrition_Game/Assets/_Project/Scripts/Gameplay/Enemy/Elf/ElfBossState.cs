namespace Attrition.Gameplay.Enemy.Elf
{
    /// <summary>
    /// Lớp trừu tượng cho mỗi trạng thái của Boss ELF (Boss 3, hệ Sấm) — mirror
    /// <see cref="Druid.DruidBossState"/>. Mô hình Enter → Update → Exit; ElfBossAI chỉ gọi 3 hook này.
    /// </summary>
    public abstract class ElfBossState
    {
        /// <summary>Gọi MỘT LẦN khi chuyển VÀO state này.</summary>
        public virtual void Enter(ElfBossAI ai) { }

        /// <summary>Gọi MỖI TICK (FixedUpdateNetwork) khi state này active.</summary>
        public virtual void Update(ElfBossAI ai) { }

        /// <summary>Gọi MỘT LẦN khi chuyển RA KHỎI state này.</summary>
        public virtual void Exit(ElfBossAI ai) { }
    }
}
