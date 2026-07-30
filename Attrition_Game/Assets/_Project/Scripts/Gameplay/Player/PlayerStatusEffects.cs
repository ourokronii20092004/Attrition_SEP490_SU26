using Fusion;
using UnityEngine;

namespace Attrition.Gameplay.Player
{
    /// <summary>
    /// Hiệu ứng khống chế ĐẶT LÊN PLAYER: SLOW (giảm % tốc chạy) và ROOT (giam tại chỗ, không chạy/nhảy).
    ///
    /// VÌ SAO CẦN: enemy đã có `EnemyController.ApplySlow` (accessory của player làm chậm quái) nhưng chiều
    /// NGƯỢC LẠI thì chưa có gì. Boss 4 skill 2 (đất bọc mục tiêu → khống chế) và boss 5 skill 3 (lốc nước
    /// làm chậm 30%) cần nó.
    ///
    /// COOP: cả 2 trạng thái là [Networked] và CHỈ host ghi (skill boss chạy host-authoritative). Client
    /// đọc để hiển thị + để `PlayerController` kẹp tốc độ — quan trọng là client tự đọc được, vì client
    /// mô phỏng chuyển động player CỦA MÌNH (ClientPhysicsSimulation) nên nếu chỉ host biết thì client vẫn
    /// chạy full speed rồi bị host kéo về, thành giật.
    ///
    /// Gắn lên player prefab (cùng cấp PlayerController). Không có component này thì mọi thứ chạy như cũ —
    /// PlayerController đọc qua null-check.
    /// </summary>
    public class PlayerStatusEffects : NetworkBehaviour
    {
        /// <summary>Hệ số tốc độ còn lại (1 = bình thường, 0.7 = chậm 30%). Hết hiệu lực thì về 1.</summary>
        [Networked] private float SlowFactor { get; set; }
        [Networked] private TickTimer SlowTimer { get; set; }

        /// <summary>Đang bị giam tại chỗ (đất bọc). Không chạy, không nhảy, không dash.</summary>
        [Networked] public NetworkBool IsRooted { get; set; }
        [Networked] private TickTimer RootTimer { get; set; }

        /// <summary>
        /// Nhân vào tốc độ chạy. Trả 1 khi không bị gì — PlayerController nhân trực tiếp nên không cần
        /// nhánh if.
        /// </summary>
        public float MoveSpeedMultiplier
        {
            get
            {
                if (Object == null || !Object.IsValid) return 1f;
                if (SlowTimer.ExpiredOrNotRunning(Runner)) return 1f;
                // SlowFactor mặc định 0 khi chưa ai set → phải kẹp, nếu không player đứng chết tại chỗ.
                return SlowFactor <= 0f ? 1f : Mathf.Clamp01(SlowFactor);
            }
        }

        /// <summary>Bị giam? Đọc kèm guard vì UI/AI có thể hỏi lúc player đang spawn.</summary>
        public bool Rooted
        {
            get
            {
                if (Object == null || !Object.IsValid) return false;
                return IsRooted && !RootTimer.ExpiredOrNotRunning(Runner);
            }
        }

        /// <summary>
        /// HOST gọi: làm chậm trong `duration` giây. `factor` = phần tốc độ CÒN LẠI (0.7 = chậm 30%).
        /// Chồng hiệu ứng: giữ cái NẶNG hơn (factor nhỏ hơn) và gia hạn thời gian, để 2 lốc nước liên tiếp
        /// không tự nâng nhau về 1.
        /// </summary>
        public void ApplySlow(float factor, float duration)
        {
            if (!HasStateAuthority) return;
            if (duration <= 0f) return;

            float f = Mathf.Clamp01(factor);
            bool active = !SlowTimer.ExpiredOrNotRunning(Runner);
            SlowFactor = active ? Mathf.Min(SlowFactor <= 0f ? 1f : SlowFactor, f) : f;
            SlowTimer = TickTimer.CreateFromSeconds(Runner, duration);
        }

        /// <summary>HOST gọi: giam tại chỗ `duration` giây (đất bọc của DemonKin).</summary>
        public void ApplyRoot(float duration)
        {
            if (!HasStateAuthority) return;
            if (duration <= 0f) return;
            IsRooted = true;
            RootTimer = TickTimer.CreateFromSeconds(Runner, duration);
        }

        /// <summary>HOST gọi: thả sớm (đất nổ xong thì nhả người chơi ra).</summary>
        public void ClearRoot()
        {
            if (!HasStateAuthority) return;
            IsRooted = false;
            RootTimer = default;
        }

        /// <summary>Xoá sạch mọi hiệu ứng — dùng khi respawn/hồi sinh để không mang debuff qua mạng chết.</summary>
        public void ClearAll()
        {
            if (!HasStateAuthority) return;
            IsRooted = false;
            RootTimer = default;
            SlowFactor = 1f;
            SlowTimer = default;
        }

        public override void Spawned()
        {
            if (HasStateAuthority) SlowFactor = 1f;
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;
            // Hết hạn root → tự nhả. Cần dọn cờ vì `IsRooted` là trạng thái bền, không tự tắt theo timer.
            if (IsRooted && RootTimer.ExpiredOrNotRunning(Runner)) IsRooted = false;
        }
    }
}
