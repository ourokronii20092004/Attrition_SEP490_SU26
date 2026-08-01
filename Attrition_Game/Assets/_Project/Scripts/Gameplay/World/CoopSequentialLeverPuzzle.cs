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

        public override void FixedUpdateNetwork()
        {
            if (_soloHandled || !HasStateAuthority || plates == null || doors == null) return;

            int n = Mathf.Min(plates.Length, doors.Length);
            for (int i = 0; i < n; i++)
            {
                if (plates[i] != null && doors[i] != null && plates[i].IsActive)
                    doors[i].Open(); // idempotent; mở vĩnh viễn (không đóng lại)
            }
        }
    }
}
