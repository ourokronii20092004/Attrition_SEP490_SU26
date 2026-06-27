using Fusion;
using UnityEngine;

namespace Attrition.Gameplay.World
{
    /// <summary>
    /// Nhiệm vụ "2 nút" (COOP): cần TẤT CẢ plate được đạp ĐỒNG THỜI thì cửa mới mở.
    /// Đặt 2 plate cách xa nhau để bắt buộc 2 player mỗi người đứng 1 nút.
    ///
    /// SOLO (1 người không thể giải): ẨN toàn bộ nút + MỞ SẴN cửa để không bị kẹt.
    ///
    /// Host kiểm tra điều kiện và điều khiển Door; client thấy cửa mở/đóng qua Door.IsOpen.
    /// </summary>
    public class CoopPlateDoorController : NetworkBehaviour
    {
        [Header("---- NÚT (PLATES) ----")]
        [Tooltip("Tất cả plate phải active cùng lúc → mở cửa. Đặt 2 nút cách xa để cần 2 player.")]
        [SerializeField] private PuzzlePlate[] plates = new PuzzlePlate[0];

        [Header("---- CỬA ----")]
        [Tooltip("Cửa sẽ mở khi đủ điều kiện.")]
        [SerializeField] private Door door;

        [Header("---- HÀNH VI ----")]
        [Tooltip("TRUE = phải GIỮ đủ nút thì cửa mới mở (rời nút → đóng lại). FALSE = mở 1 lần là vĩnh viễn.")]
        [SerializeField] private bool requireHold = true;

        [Header("---- CAMERA ----")]
        [Tooltip("Lia camera tới cửa khi GIẢI XONG (lần đầu mở) để khoe cửa đã mở. Bỏ trống = lia tới vị trí Door.")]
        [SerializeField] private Transform cameraFocusPoint;
        [Tooltip("Số giây giữ camera tại cửa trước khi trả về player.")]
        [SerializeField] private float focusHoldSeconds = 1.6f;

        [Networked] public NetworkBool SolvedOnce { get; set; }
        private bool _focusShown;
        private bool _soloHandled;

        public override void Spawned()
        {
            // SOLO: puzzle 2 nút vô nghĩa (1 người không giải được) → ẩn TOÀN BỘ puzzle: nút + CỬA luôn,
            // để khu vực này không xuất hiện vật cản coop trong solo. Player đi qua thoải mái.
            if (Attrition.Persistence.GameLaunch.Mode != Attrition.Persistence.LaunchMode.Coop)
            {
                if (plates != null)
                    foreach (var p in plates)
                        if (p != null) p.gameObject.SetActive(false);

                // Ẩn hẳn cửa (cả collider chặn + visual) — solo không thấy cửa coop.
                if (door != null) door.gameObject.SetActive(false);
                _soloHandled = true;
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (_soloHandled) return; // solo: cửa đã mở sẵn, không xử lý puzzle
            if (!HasStateAuthority || door == null || plates == null || plates.Length == 0) return;

            // Đã giải 1 lần → GIỮ CỬA MỞ VĨNH VIỄN (không đóng lại dù người chơi rời nút).
            if (SolvedOnce) { door.Open(); return; }

            bool allActive = true;
            foreach (var p in plates)
            {
                if (p == null || !p.IsActive) { allActive = false; break; }
            }

            if (allActive)
            {
                door.Open();
                SolvedOnce = true; // đánh dấu đã giải → mở vĩnh viễn + lia camera 1 lần (sync mọi máy)
            }
            else if (requireHold && door.IsOpen)
            {
                door.Close();
            }
        }

        public override void Render()
        {
            // Lia camera tới cửa LẦN ĐẦU giải xong — chạy trên MỌI máy (mỗi player tự lia camera mình).
            if (_focusShown || !SolvedOnce) return;
            _focusShown = true;
            Vector3 pos = cameraFocusPoint != null ? cameraFocusPoint.position
                        : (door != null ? door.transform.position : transform.position);
            Attrition.Gameplay.Environment.CameraFocusController.FocusOn(pos, focusHoldSeconds);
        }
    }
}
