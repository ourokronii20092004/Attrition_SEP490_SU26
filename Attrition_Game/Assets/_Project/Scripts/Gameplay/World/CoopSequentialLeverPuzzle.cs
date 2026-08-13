using Fusion;
using UnityEngine;

namespace Attrition.Gameplay.World
{
    /// <summary>
    /// Puzzle COOP "dẫm plate nối tiếp" (chỉ coop, ẩn ở solo — giống <see cref="CoopPlateDoorController"/>):
    ///   - Chặng i gồm 1 PuzzlePlate + 1 Door. Dẫm plate[i] → door[i] mở.
    ///   - Bố trí sao cho plate[i+1] nằm SAU door[i]: P1 dẫm plate[0] mở door[0] cho P2 đi vào,
    ///     P2 dẫm plate[1] mở door[1] để cả hai đi tiếp. Tính "nối tiếp" đến từ gating vật lý.
    ///
    /// SOLO: ẩn toàn bộ plate + cửa để một người đi qua, giống CoopPlateDoorController.
    /// Host đọc PuzzlePlate.IsActive rồi mở Door tương ứng; cửa đã mở không đóng lại.
    /// Kéo plates[] và doors[] KHỚP INDEX (plate[i] ↔ door[i]).
    /// </summary>
    public class CoopSequentialLeverPuzzle : NetworkBehaviour
    {
        [Header("---- CÁC CHẶNG (plate[i] ↔ door[i], khớp index) ----")]
        [Tooltip("Plate từng chặng. Dẫm plate[i] → mở door[i].")]
        [SerializeField] private PuzzlePlate[] plates = new PuzzlePlate[0];
        [Tooltip("Cửa từng chặng. Phải cùng số lượng + khớp index với plates.")]
        [SerializeField] private Door[] doors = new Door[0];

        private bool _soloHandled;

        public override void Spawned()
        {
            // SOLO: puzzle 2 người vô nghĩa → ẩn lever + mở sẵn mọi cửa để đi qua thoải mái.
            if (Attrition.Persistence.GameLaunch.Mode != Attrition.Persistence.LaunchMode.Coop)
            {
                if (plates != null)
                    foreach (var plate in plates)
                        if (plate != null) plate.gameObject.SetActive(false);

                if (doors != null)
                    foreach (var d in doors)
                        if (d != null) d.gameObject.SetActive(false);

                _soloHandled = true;
            }
        }

        // CHẨN ĐOÁN: chỉ log KHI ĐỔI trạng thái, không log mỗi tick (60 dòng/giây là vô dụng).
        private bool[] _lastLoggedActive;

        public override void FixedUpdateNetwork()
        {
            if (_soloHandled || plates == null || doors == null) return;

            // Log 1 lần nếu không phải state authority — để biết controller có quyền chạy hay không.
            if (!HasStateAuthority)
            {
                if (!_loggedNoAuthority)
                {
                    _loggedNoAuthority = true;
                    Debug.Log($"[SeqPuzzle:{name}] KHÔNG có StateAuthority → không mở cửa. "
                              + "Đây là bản proxy (client). Bình thường nếu bạn đang xem máy client.");
                }
                return;
            }

            int n = Mathf.Min(plates.Length, doors.Length);
            if (_lastLoggedActive == null || _lastLoggedActive.Length != n) _lastLoggedActive = new bool[n];

            for (int i = 0; i < n; i++)
            {
                if (plates[i] == null || doors[i] == null)
                {
                    Debug.LogError($"[SeqPuzzle:{name}] chặng {i}: plate={(plates[i] == null ? "NULL" : "ok")} "
                                   + $"door={(doors[i] == null ? "NULL" : "ok")} → chặng này KHÔNG BAO GIỜ mở.");
                    continue;
                }

                bool active = plates[i].IsActive;
                if (active != _lastLoggedActive[i])
                {
                    _lastLoggedActive[i] = active;
                    Debug.Log($"[SeqPuzzle:{name}] plate[{i}].IsActive → {active} "
                              + $"(door[{i}].IsOpen hiện tại = {doors[i].IsOpen})");
                }

                if (active) doors[i].Open(); // idempotent; mở vĩnh viễn (không đóng lại)
            }
        }

        private bool _loggedNoAuthority;
    }
}
