using Fusion;
using UnityEngine;

namespace Attrition.Gameplay.World
{
    /// <summary>
    /// Thang máy kiểu Hollow Knight (Map 5): 1 bệ (platform rắn) di chuyển giữa 2 điểm A↔B.
    /// KHÔNG tự chạy — chờ <see cref="Lever"/> đánh vào để đổi chiều đi (Toggle) về đầu kia.
    ///
    /// Đồng bộ: chỉ host ghi Progress ([Networked], 0=A .. 1=B); bệ chạy SIMULATED trên MỌI peer
    /// (giống Player/Enemy) nên mỗi máy tự đọc Progress rồi MovePosition → collider luôn đúng vị trí
    /// trong physics scene cục bộ, va chạm đẩy player local chính xác trên mọi máy.
    ///
    /// Player ĐỨNG lên bệ để đi lên/xuống nhờ va chạm rắn (bệ kinematic đẩy player dynamic). Đặt bệ
    /// trên groundLayer để CheckGround của player nhận ra là mặt đất.
    /// Gắn lên GameObject có Rigidbody2D (Kinematic) + Collider2D KHÔNG trigger + NetworkObject.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(NetworkObject))]
    public class Elevator : NetworkBehaviour
    {
        [Header("---- ĐIỂM ĐẦU/CUỐI (local offset so với vị trí gốc) ----")]
        [Tooltip("Offset điểm A so với vị trí đặt ban đầu (thường (0,0) = chỗ đặt).")]
        [SerializeField] private Vector2 pointAOffset = Vector2.zero;
        [Tooltip("Offset điểm B so với vị trí đặt ban đầu (vd (0,8) = lên cao 8 unit).")]
        [SerializeField] private Vector2 pointBOffset = new Vector2(0f, 8f);

        [Header("---- CHUYỂN ĐỘNG ----")]
        [Tooltip("Tốc độ đi (unit/giây).")]
        [SerializeField] private float speed = 3f;
        [Tooltip("Bắt đầu ở A? (false = bắt đầu ở B).")]
        [SerializeField] private bool startAtA = true;

        // 0 = tại A, 1 = tại B. Host ghi; mọi peer đọc để đặt vị trí bệ.
        [Networked] public float Progress { get; set; }
        // Đích hiện tại: true = đang đi tới B, false = đang đi tới A.
        [Networked] private NetworkBool MovingToB { get; set; }

        private Rigidbody2D _rb;
        private Vector2 _origin;   // vị trí gốc lúc spawn (để tính A/B theo offset)
        private bool _originSet;

        public override void Spawned()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.bodyType = RigidbodyType2D.Kinematic;
            _origin = _rb.position;
            _originSet = true;

            if (HasStateAuthority)
            {
                Progress = startAtA ? 0f : 1f;
                MovingToB = !startAtA; // nếu đang ở A thì lần gạt đầu đi tới B
            }

            // Chạy simulated trên MỌI peer để FixedUpdateNetwork áp vị trí vào physics scene cục bộ →
            // va chạm đẩy player local đúng trên cả host lẫn client (giống PlayerController/EnemyController).
            if (Object != null) Runner.SetIsSimulated(Object, true);
        }

        /// <summary>Lever gọi (host): đổi chiều đi về đầu còn lại.</summary>
        public void Toggle()
        {
            if (!HasStateAuthority) return;
            MovingToB = !MovingToB;
        }

        public override void FixedUpdateNetwork()
        {
            if (!_originSet) return;

            // Chỉ host điều khiển Progress; các peer khác chỉ ĐỌC giá trị đã sync.
            if (HasStateAuthority)
            {
                float target = MovingToB ? 1f : 0f;
                Vector2 a = _origin + pointAOffset;
                Vector2 b = _origin + pointBOffset;
                float span = Vector2.Distance(a, b);
                if (span > 0.0001f)
                {
                    float step = (speed * Runner.DeltaTime) / span; // tốc độ unit/s → progress/s
                    Progress = Mathf.MoveTowards(Progress, target, step);
                }
            }

            ApplyPosition();
        }

        private void ApplyPosition()
        {
            Vector2 a = _origin + pointAOffset;
            Vector2 b = _origin + pointBOffset;
            Vector2 pos = Vector2.Lerp(a, b, Progress);
            _rb.MovePosition(pos);
        }

        private void OnDrawGizmos()
        {
            // Vẽ tuyến đường A↔B trong Scene để đặt điểm dễ hơn.
            Vector3 baseP = Application.isPlaying && _originSet
                ? (Vector3)_origin
                : transform.position;
            Vector3 a = baseP + (Vector3)pointAOffset;
            Vector3 b = baseP + (Vector3)pointBOffset;
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 1f);
            Gizmos.DrawLine(a, b);
            Gizmos.DrawWireSphere(a, 0.3f);
            Gizmos.DrawWireSphere(b, 0.3f);
        }
    }
}
