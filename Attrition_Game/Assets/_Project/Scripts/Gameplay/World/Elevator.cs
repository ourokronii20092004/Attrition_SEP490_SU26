using Fusion;
using UnityEngine;

namespace Attrition.Gameplay.World
{
    /// <summary>
    /// Thang máy kiểu Hollow Knight (Map 5): 1 bệ (platform rắn) chạy giữa CÁC ĐIỂM DỪNG.
    /// KHÔNG tự chạy — chờ <see cref="Lever"/> đánh vào để đi tới điểm dừng kế tiếp.
    ///
    /// NHIỀU ĐIỂM DỪNG: `stopOffsets` là danh sách offset so với vị trí đặt ban đầu. 2 phần tử = thang
    /// thường A↔B; 3 phần tử = thang có tầng giữa (Map 5 thang số 12). Mỗi lần gạt cần → đi tới điểm kế
    /// tiếp; tới điểm cuối thì lần gạt sau QUAY NGƯỢC lại (kiểu con lắc), nên không bao giờ kẹt ở đầu.
    ///
    /// Đồng bộ: chỉ host ghi TargetIndex/Progress ([Networked]); bệ chạy SIMULATED trên MỌI peer (giống
    /// Player/Enemy) nên mỗi máy tự đọc rồi MovePosition → collider luôn đúng vị trí trong physics scene
    /// cục bộ, va chạm đẩy player local chính xác trên mọi máy.
    ///
    /// Player ĐỨNG lên bệ để đi lên/xuống nhờ va chạm rắn (bệ kinematic đẩy player dynamic). Đặt bệ trên
    /// groundLayer để CheckGround của player nhận ra là mặt đất.
    /// Gắn lên GameObject có Rigidbody2D (Kinematic) + Collider2D KHÔNG trigger + NetworkObject.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(NetworkObject))]
    public class Elevator : NetworkBehaviour
    {
        [Header("---- ĐIỂM DỪNG (offset so với vị trí đặt ban đầu) ----")]
        [Tooltip("Danh sách điểm dừng theo thứ tự. Tối thiểu 2. VD thang thường: (0,0) và (0,8). " +
                 "Thang 3 tầng: (0,0), (0,6), (0,12).")]
        [SerializeField]
        private Vector2[] stopOffsets = { Vector2.zero, new Vector2(0f, 8f) };

        [Header("---- CHUYỂN ĐỘNG ----")]
        [Tooltip("Tốc độ đi (unit/giây).")]
        [SerializeField] private float speed = 3f;
        [Tooltip("Điểm dừng lúc bắt đầu (chỉ số trong stopOffsets).")]
        [SerializeField] private int startStopIndex = 0;

        /// <summary>Chỉ số điểm dừng ĐÍCH hiện tại. Host ghi; mọi peer đọc.</summary>
        [Networked] private int TargetIndex { get; set; }
        /// <summary>Chỉ số điểm dừng ĐANG RỜI (gốc của đoạn đang đi).</summary>
        [Networked] private int FromIndex { get; set; }
        /// <summary>Tiến trình 0..1 trên đoạn FromIndex → TargetIndex.</summary>
        [Networked] public float Progress { get; set; }
        /// <summary>Chiều đi hiện tại (+1 = tiến về cuối danh sách, -1 = lùi về đầu).</summary>
        [Networked] private int Direction { get; set; }

        private Rigidbody2D _rb;
        private Vector2 _origin;   // vị trí gốc lúc spawn (để tính các điểm dừng theo offset)
        private bool _originSet;

        /// <summary>Số điểm dừng hợp lệ (luôn >= 1 để tránh chia 0 khi Inspector để trống).</summary>
        private int StopCount => stopOffsets != null ? stopOffsets.Length : 0;

        public override void Spawned()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.bodyType = RigidbodyType2D.Kinematic;
            _origin = _rb.position;
            _originSet = true;

            if (HasStateAuthority)
            {
                int start = StopCount > 0 ? Mathf.Clamp(startStopIndex, 0, StopCount - 1) : 0;
                FromIndex = start;
                TargetIndex = start;      // đứng yên tại điểm bắt đầu, chờ gạt cần
                Progress = 1f;            // đã "tới đích" = ở đúng điểm dừng
                Direction = 1;
            }

            // Chạy simulated trên MỌI peer để FixedUpdateNetwork áp vị trí vào physics scene cục bộ →
            // va chạm đẩy player local đúng trên cả host lẫn client (giống PlayerController/EnemyController).
            if (Object != null) Runner.SetIsSimulated(Object, true);
        }

        /// <summary>
        /// Lever gọi (host): đi tới điểm dừng KẾ TIẾP theo chiều hiện tại. Đang ở điểm cuối/đầu thì đảo
        /// chiều rồi mới đi, nên thang luôn có chỗ để tới (không kẹt).
        /// Đang di chuyển giữa đường thì bỏ qua — tránh giật ngược khi player gạt liên tục.
        /// </summary>
        public void Toggle()
        {
            if (!HasStateAuthority) return;
            if (StopCount < 2) return;

            // Còn đang chạy → không nhận lệnh mới (Progress < 1 nghĩa là chưa tới điểm dừng).
            if (Progress < 0.999f) return;

            int dir = Direction == 0 ? 1 : Direction;
            int next = TargetIndex + dir;

            // Chạm biên danh sách → đảo chiều (con lắc).
            if (next < 0 || next >= StopCount)
            {
                dir = -dir;
                next = TargetIndex + dir;
                next = Mathf.Clamp(next, 0, StopCount - 1);
            }

            Direction = dir;
            FromIndex = TargetIndex;
            TargetIndex = next;
            Progress = 0f;
        }

        public override void FixedUpdateNetwork()
        {
            if (!_originSet || StopCount == 0) return;

            // Chỉ host điều khiển Progress; các peer khác chỉ ĐỌC giá trị đã sync.
            if (HasStateAuthority && Progress < 1f)
            {
                float span = Vector2.Distance(StopAt(FromIndex), StopAt(TargetIndex));
                if (span > 0.0001f)
                {
                    float step = (speed * Runner.DeltaTime) / span; // unit/s → progress/s
                    Progress = Mathf.MoveTowards(Progress, 1f, step);
                }
                else Progress = 1f;   // 2 điểm trùng nhau → coi như đã tới
            }

            ApplyPosition();
        }

        /// <summary>Vị trí world của điểm dừng thứ i (kẹp chỉ số cho an toàn).</summary>
        private Vector2 StopAt(int i)
        {
            if (StopCount == 0) return _origin;
            return _origin + stopOffsets[Mathf.Clamp(i, 0, StopCount - 1)];
        }

        private void ApplyPosition()
        {
            Vector2 pos = Vector2.Lerp(StopAt(FromIndex), StopAt(TargetIndex), Progress);
            _rb.MovePosition(pos);
        }

        private void OnDrawGizmos()
        {
            // Vẽ tuyến đường qua tất cả điểm dừng trong Scene để đặt điểm dễ hơn.
            if (stopOffsets == null || stopOffsets.Length == 0) return;

            Vector3 baseP = Application.isPlaying && _originSet ? (Vector3)_origin : transform.position;
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 1f);

            for (int i = 0; i < stopOffsets.Length; i++)
            {
                Vector3 p = baseP + (Vector3)stopOffsets[i];
                Gizmos.DrawWireSphere(p, 0.3f);
                if (i > 0) Gizmos.DrawLine(baseP + (Vector3)stopOffsets[i - 1], p);
            }
        }
    }
}
