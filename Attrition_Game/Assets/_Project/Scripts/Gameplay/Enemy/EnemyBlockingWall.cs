using Fusion;
using UnityEngine;

namespace Attrition.Gameplay.Enemy
{
    /// <summary>
    /// Bức tường đất boss triệu hồi (DemonKin skill 4): TRỒI LÊN từ dưới đất, CHẶN ĐẠN của player, gây
    /// sát thương khi mới nhô lên, rồi tự lún xuống và despawn.
    ///
    /// CÁCH CHẶN ĐẠN — không cần code chặn riêng: `EnemyProjectile` và đạn skill của player đều dò va chạm
    /// bằng `CircleCast(..., hitLayer)` rồi Despawn khi trúng bất cứ collider nào trong mask đó. Đạn player
    /// vốn đã có `Ground` trong hitLayer (xem FixProjectileDamageEditor: hitLayer = Player | Ground), nên
    /// chỉ cần đặt collider của tường Ở LAYER GROUND là đạn tự nổ khi đụng. Tool setup lo phần layer.
    ///
    /// Sát thương lúc trồi lên do `EnemyAoEDamage` GẮN CÙNG prefab lo (không nhân đôi logic ở đây); script
    /// này chỉ quản HÌNH DÁNG + VÒNG ĐỜI: trồi lên → đứng chặn → lún xuống → despawn.
    ///
    /// COOP: host-authoritative về vòng đời (Despawn) và vị trí (`transform` sync qua NetworkTransform).
    /// Chuyển động trồi/lún tính từ `RiseProgress` [Networked] nên client nội suy khớp, không lệch hình.
    /// </summary>
    public class EnemyBlockingWall : NetworkBehaviour
    {
        [Header("---- KÍCH THƯỚC ----")]
        [Tooltip("Chiều cao tường khi trồi hết lên (units).")]
        public float wallHeight = 3f;

        [Header("---- THỜI GIAN ----")]
        [Tooltip("Thời gian trồi từ dưới đất lên hết (giây).")]
        public float riseTime = 0.35f;
        [Tooltip("Thời gian đứng chặn sau khi trồi xong (giây).")]
        public float holdTime = 3.5f;
        [Tooltip("Thời gian lún xuống trước khi despawn (giây).")]
        public float sinkTime = 0.4f;

        /// <summary>Tiến trình trồi 0..1 (host ghi, client đọc để vẽ đúng độ cao).</summary>
        [Networked] private float RiseProgress { get; set; }
        [Networked] private TickTimer LifeTimer { get; set; }

        private float _elapsed;
        private Vector3 _baseLocalPos;   // vị trí "đã trồi hết" — tính lúc Spawned
        private Transform _visual;

        public override void Spawned()
        {
            _elapsed = 0f;

            // Con đầu tiên có SpriteRenderer = phần hình để đẩy lên/xuống. Không có thì đẩy chính transform
            // này (nhưng khi đó collider cũng chạy theo — vẫn đúng, chỉ kém mượt).
            var sr = GetComponentInChildren<SpriteRenderer>();
            _visual = sr != null ? sr.transform : transform;
            _baseLocalPos = _visual.localPosition;

            // Bắt đầu CHÌM hoàn toàn dưới đất rồi mới trồi lên.
            ApplyRise(0f);

            if (HasStateAuthority)
            {
                RiseProgress = 0f;
                LifeTimer = TickTimer.CreateFromSeconds(Runner, riseTime + holdTime + sinkTime);
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (Attrition.Persistence.GamePause.IsPaused) return;
            if (!HasStateAuthority) return;

            _elapsed += Runner.DeltaTime;

            float sinkStart = riseTime + holdTime;
            if (_elapsed <= riseTime)
                RiseProgress = riseTime > 0f ? Mathf.Clamp01(_elapsed / riseTime) : 1f;
            else if (_elapsed >= sinkStart)
                RiseProgress = sinkTime > 0f ? Mathf.Clamp01(1f - (_elapsed - sinkStart) / sinkTime) : 0f;
            else
                RiseProgress = 1f;

            if (LifeTimer.Expired(Runner)) Runner.Despawn(Object);
        }

        public override void Render()
        {
            ApplyRise(RiseProgress);
        }

        /// <summary>Đặt độ cao theo tiến trình: 0 = chìm hẳn (thấp hơn 1 thân tường), 1 = trồi hết.</summary>
        private void ApplyRise(float t)
        {
            if (_visual == null) return;
            _visual.localPosition = _baseLocalPos + new Vector3(0f, (t - 1f) * wallHeight, 0f);
        }
    }
}
