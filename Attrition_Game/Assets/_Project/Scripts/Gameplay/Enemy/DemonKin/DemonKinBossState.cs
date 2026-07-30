namespace Attrition.Gameplay.Enemy.DemonKin
{
    /// <summary>
    /// Lớp trừu tượng cho mỗi trạng thái của Boss DEMONKIN (Boss 4, hệ ĐẤT) — mirror
    /// <see cref="Druid.DruidBossState"/>. Mô hình Enter → Update → Exit.
    /// </summary>
    public abstract class DemonKinBossState
    {
        /// <summary>Gọi MỘT LẦN khi chuyển VÀO state này.</summary>
        public virtual void Enter(DemonKinBossAI ai) { }

        /// <summary>Gọi MỖI TICK (FixedUpdateNetwork) khi state này active.</summary>
        public virtual void Update(DemonKinBossAI ai) { }

        /// <summary>Gọi MỘT LẦN khi chuyển RA KHỎI state này.</summary>
        public virtual void Exit(DemonKinBossAI ai) { }
    }
}
