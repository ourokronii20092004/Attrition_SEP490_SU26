using Fusion;
using UnityEngine;

namespace Attrition.Gameplay.World
{
    /// <summary>
    /// Vật thể phá được (thùng, đá, hộp...). Player đánh trúng (qua IDamageable) → RUNG nhẹ báo hiệu;
    /// sau đủ số đòn (hitsToBreak) → VỠ (biến mất, chưa có animation vỡ nên chỉ despawn + VFX tùy chọn).
    ///
    /// Đánh được vì implement IDamageable (PlayerCombat.DealDamage quét IDamageable trong tầm). Số đòn
    /// đã trúng là [Networked] nên đồng bộ host↔client; chỉ host đếm + despawn, client thấy rung qua RPC.
    /// Gắn lên GameObject có Collider2D KHÔNG trigger (để lọt vào OverlapCircle của đòn đánh) + NetworkObject.
    /// Đặt layer nằm trong targetLayers của PlayerCombat (thường là 'Enemy' hoặc layer đánh được).
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class BreakableObject : NetworkBehaviour, IDamageable
    {
        [Header("---- ĐỘ BỀN ----")]
        [Tooltip("Số đòn đánh để vỡ.")]
        [SerializeField] private int hitsToBreak = 5;

        [Header("---- RUNG (SHAKE) ----")]
        [Tooltip("Transform để rung khi trúng đòn. Bỏ trống = dùng chính transform này.")]
        [SerializeField] private Transform shakeTarget;
        [Tooltip("Biên độ rung (world units).")]
        [SerializeField] private float shakeAmount = 0.12f;
        [Tooltip("Thời gian rung mỗi đòn (giây).")]
        [SerializeField] private float shakeDuration = 0.15f;

        [Header("---- FEEDBACK (tùy chọn) ----")]
        [Tooltip("VFX spawn 1 lần khi vỡ. Bỏ trống = không có.")]
        [SerializeField] private GameObject breakVfxPrefab;

        [Networked] private int HitCount { get; set; }
        [Networked] private NetworkBool Broken { get; set; }

        // Đếm đòn đã áp visual cục bộ (mọi peer) để phát rung khi HitCount tăng.
        private int _lastAppliedHits;
        private Vector3 _shakeBasePos;
        private Coroutine _shakeRoutine;

        // IDamageable: chưa vỡ = còn "sống" (nhận đòn). PlayerCombat bỏ qua mục tiêu IsDead.
        public bool IsDead => Broken;

        private void Awake()
        {
            if (shakeTarget == null) shakeTarget = transform;
            _shakeBasePos = shakeTarget.localPosition;
        }

        public void TakeDamage(int damage, Vector2 knockbackDir, float knockbackForce, Attrition.Core.DamageType type = Attrition.Core.DamageType.Physical)
        {
            // Vật thể không nhận sát thương theo số — mỗi lần trúng = 1 đòn. Chỉ host đếm.
            if (Broken) return;
            RPC_Hit();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_Hit()
        {
            if (Broken) return;
            HitCount++;
            if (HitCount >= hitsToBreak) Break();
        }

        private void Break()
        {
            Broken = true;
            RpcOnBreak(transform.position);
            Runner.Despawn(Object);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcOnBreak(Vector3 pos)
        {
            if (breakVfxPrefab != null) Instantiate(breakVfxPrefab, pos, Quaternion.identity);
        }

        public override void Render()
        {
            // HitCount tăng (host đếm, sync mọi peer) → rung 1 nhịp trên mọi máy. Bỏ qua khi đã vỡ.
            if (Broken) return;
            if (HitCount == _lastAppliedHits) return;
            _lastAppliedHits = HitCount;
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
