using Fusion;
using UnityEngine;

namespace Attrition.Gameplay.World
{
    /// <summary>
    /// Puzzle COOP "gạt cần nối tiếp" (chỉ coop, ẩn ở solo — giống <see cref="CoopPlateDoorController"/>):
    ///   - Chặng i gồm 1 Lever + 1 Door. Gạt lever[i] → door[i] mở.
    ///   - Bố trí sao cho lever[i+1] nằm SAU door[i]: P1 gạt lever[0] mở door[0] cho P2 đi vào,
    ///     P2 gạt lever[1] mở door[1] để cả hai đi tiếp. Tính "nối tiếp" đến từ gating vật lý (cửa chặn).
    ///
    /// SOLO: ẩn toàn bộ lever + MỞ SẴN mọi cửa để 1 người không bị kẹt.
    ///
    /// Host đọc Lever.FlipCount rồi mở Door tương ứng; client thấy cửa mở qua Door.IsOpen (đã networked).
    /// Gắn lên 1 GameObject (NetworkObject). Kéo lever[] và door[] KHỚP INDEX (lever[i] ↔ door[i]).
    /// </summary>
    public class CoopSequentialLeverPuzzle : NetworkBehaviour
    {
        [Header("---- CÁC CHẶNG (lever[i] ↔ door[i], khớp index) ----")]
        [Tooltip("Cần gạt từng chặng. Gạt lever[i] → mở door[i].")]
        [SerializeField] private Lever[] levers = new Lever[0];
        [Tooltip("Cửa từng chặng. Phải cùng số lượng + khớp index với levers.")]
        [SerializeField] private Door[] doors = new Door[0];

        private bool _soloHandled;

        public override void Spawned()
        {
            // SOLO: puzzle 2 người vô nghĩa → ẩn lever + mở sẵn mọi cửa để đi qua thoải mái.
            if (Attrition.Persistence.GameLaunch.Mode != Attrition.Persistence.LaunchMode.Coop)
            {
                if (levers != null)
                    foreach (var l in levers)
                        if (l != null) l.gameObject.SetActive(false);

                if (doors != null)
                    foreach (var d in doors)
                        if (d != null) d.gameObject.SetActive(false);

                _soloHandled = true;
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (_soloHandled || !HasStateAuthority || levers == null || doors == null) return;

            int n = Mathf.Min(levers.Length, doors.Length);
            for (int i = 0; i < n; i++)
            {
                if (levers[i] != null && doors[i] != null && levers[i].FlipCount > 0)
                    doors[i].Open(); // idempotent; mở vĩnh viễn (không đóng lại)
            }
        }
    }
}
