using Fusion;
using UnityEngine;

namespace Attrition.Gameplay.World
{
    /// <summary>
    /// Cần gạt kiểu Hollow Knight: player ĐÁNH vào (qua IDamageable) → gạt cần → gọi
    /// <see cref="Elevator.Toggle"/> để thang máy đổi chiều đi lên/xuống.
    ///
    /// Đánh được vì implement IDamageable (PlayerCombat quét IDamageable trong tầm). Chỉ host xử lý
    /// (đếm/toggle); rung phản hồi phát trên mọi máy qua sync Flips (giống BreakableObject.HitCount).
    /// Có cooldown chống gạt liên tục trong 1 chuỗi đòn đánh.
    ///
    /// Gắn lên GameObject có Collider2D KHÔNG trigger + NetworkObject, layer nằm trong targetLayers
    /// của PlayerCombat (thường 'Enemy'). Kéo Elevator vào ô 'elevator'.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(NetworkObject))]
    public class Lever : NetworkBehaviour, IDamageable
    {
        [Header("---- LIÊN KẾT ----")]
        [Tooltip("Thang máy sẽ đổi chiều khi gạt cần. Bỏ trống = chỉ rung (không điều khiển gì).")]
        [SerializeField] private Elevator elevator;

        [Header("---- HÀNH VI ----")]
        [Tooltip("Giãn cách tối thiểu giữa 2 lần gạt (giây) — chống 1 chuỗi đòn gạt nhiều lần.")]
        [SerializeField] private float cooldown = 0.6f;

        [Header("---- RUNG (SHAKE) ----")]
        [Tooltip("Transform để rung khi gạt. Bỏ trống = dùng transform này.")]
        [SerializeField] private Transform shakeTarget;
        [SerializeField] private float shakeAmount = 0.1f;
        [SerializeField] private float shakeDuration = 0.15f;

        // Số lần đã gạt (host tăng, sync mọi peer) → dùng để phát rung cục bộ khi đổi.
        [Networked] private int Flips { get; set; }
        [Networked] private TickTimer _cooldownTimer { get; set; }

        /// <summary>Số lần đã gạt (đồng bộ). >0 = đã bị gạt ít nhất 1 lần. Puzzle controller đọc để mở cửa.</summary>
        public int FlipCount => Flips;

        private int _lastAppliedFlips;
        private Vector3 _shakeBasePos;
        private Coroutine _shakeRoutine;

        // Không phải mục tiêu "chết" được — luôn nhận đòn.
        public bool IsDead => false;

        private void Awake()
        {
            if (shakeTarget == null) shakeTarget = transform;
            _shakeBasePos = shakeTarget.localPosition;
        }

        public void TakeDamage(int damage, Vector2 knockbackDir, float knockbackForce, Attrition.Core.DamageType type = Attrition.Core.DamageType.Physical)
        {
            RPC_Hit();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_Hit()
        {
            if (!_cooldownTimer.ExpiredOrNotRunning(Runner)) return;
            _cooldownTimer = TickTimer.CreateFromSeconds(Runner, cooldown);

            Flips++;
            if (elevator != null) elevator.Toggle();
        }

        public override void Render()
        {
            if (Flips == _lastAppliedFlips) return;
            _lastAppliedFlips = Flips;
            PlayShake();
        }

        private void PlayShake()
        {
            if (shakeTarget == null) return;
            if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);
            _shakeRoutine = StartCoroutine(ShakeRoutine());
        }

        private System.Collections.IEnumerator ShakeRoutine()
        {
            float t = 0f;
            while (t < shakeDuration)
            {
                t += Time.deltaTime;
                float decay = 1f - (t / shakeDuration);
                Vector2 offset = Random.insideUnitCircle * shakeAmount * decay;
                shakeTarget.localPosition = _shakeBasePos + (Vector3)offset;
                yield return null;
            }
            shakeTarget.localPosition = _shakeBasePos;
            _shakeRoutine = null;
        }
    }
}
