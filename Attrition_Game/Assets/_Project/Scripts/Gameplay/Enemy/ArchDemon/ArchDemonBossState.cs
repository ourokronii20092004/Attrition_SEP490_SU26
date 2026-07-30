namespace Attrition.Gameplay.Enemy.ArchDemon
{
    /// <summary>
    /// Lớp trừu tượng cho mỗi trạng thái của Boss ARCH DEMON (Boss 5, hệ NƯỚC/BÓNG TỐI) — mirror
    /// <see cref="Druid.DruidBossState"/>. Mô hình Enter → Update → Exit.
    /// </summary>
    public abstract class ArchDemonBossState
    {
        /// <summary>Gọi MỘT LẦN khi chuyển VÀO state này.</summary>
        public virtual void Enter(ArchDemonBossAI ai) { }

        /// <summary>Gọi MỖI TICK (FixedUpdateNetwork) khi state này active.</summary>
        public virtual void Update(ArchDemonBossAI ai) { }

        /// <summary>Gọi MỘT LẦN khi chuyển RA KHỎI state này.</summary>
        public virtual void Exit(ArchDemonBossAI ai) { }
    }
}
