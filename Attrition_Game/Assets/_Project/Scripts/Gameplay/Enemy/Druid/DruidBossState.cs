using UnityEngine;

namespace Attrition.Gameplay.Enemy.Druid
{
    /// <summary>
    /// Lớp trừu tượng cho mỗi trạng thái (State) của Boss Druid — mirror <see cref="SeveredFang.SeveredFangState"/>.
    /// Mô hình Enter → Update → Exit; DruidBossAI chỉ gọi 3 hook này, mỗi state tự quản logic riêng.
    /// </summary>
    public abstract class DruidBossState
    {
        /// <summary>Gọi MỘT LẦN khi chuyển VÀO state này.</summary>
        public virtual void Enter(DruidBossAI ai) { }

        /// <summary>Gọi MỖI TICK (FixedUpdateNetwork) khi state này active.</summary>
        public virtual void Update(DruidBossAI ai) { }

        /// <summary>Gọi MỘT LẦN khi chuyển RA KHỎI state này.</summary>
        public virtual void Exit(DruidBossAI ai) { }
    }
}
