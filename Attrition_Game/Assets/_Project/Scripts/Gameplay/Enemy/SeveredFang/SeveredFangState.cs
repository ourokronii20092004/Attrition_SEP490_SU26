using UnityEngine;

namespace Attrition.Gameplay.Enemy.SeveredFang
{
    /// <summary>
    /// Lớp trừu tượng cho mỗi trạng thái (State) của Boss SeveredFang.
    /// Mô hình: Entry → Update → Exit — tham khảo cách boss Hollow Knight / Afterimage hoạt động.
    /// Mỗi state tự quản lý logic riêng, SeveredFangAI chỉ gọi Enter/Update/Exit.
    /// </summary>
    public abstract class SeveredFangState
    {
        /// <summary>Gọi MỘT LẦN khi chuyển VÀO state này.</summary>
        public virtual void Enter(SeveredFangAI ai) { }

        /// <summary>Gọi MỖI TICK (FixedUpdateNetwork) trong khi state này đang active.</summary>
        public virtual void Update(SeveredFangAI ai) { }

        /// <summary>Gọi MỘT LẦN khi chuyển RA KHỎI state này.</summary>
        public virtual void Exit(SeveredFangAI ai) { }
    }
}
