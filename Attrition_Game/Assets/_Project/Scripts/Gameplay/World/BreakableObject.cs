using Fusion;
using UnityEngine;

namespace Attrition.Gameplay.World
{
    /// <summary>
    /// Vật thể phá được (thùng, đá, hộp...). Player đánh trúng (qua IDamageable) → RUNG nhẹ báo hiệu;
    /// sau đủ số đòn (hitsToBreak) → VỠ (biến mất, chưa có animation vỡ nên chỉ despawn + VFX tùy chọn).
    ///
    /// CHẶN THEO HƯỚNG (breakOnlyFromSide): chỉ đòn đánh từ ĐÚNG PHÍA mới tính vào tiến độ vỡ; đánh từ
    /// phía kia CHỈ rung (báo cho player biết "có phản hồi nhưng sai hướng"). Dùng cho câu đố kiểu
    /// "phải vòng sang bên kia mới phá được".
    ///
    /// Đánh được vì implement IDamageable (PlayerCombat.DealDamage quét IDamageable trong tầm). Số đòn
    /// đã trúng là [Networked] nên đồng bộ host↔client; chỉ host đếm + despawn, client thấy rung qua RPC.
    /// Gắn lên GameObject có Collider2D KHÔNG trigger (để lọt vào OverlapCircle của đòn đánh) + NetworkObject.
    /// Đặt layer nằm trong targetLayers của PlayerCombat (thường là 'Enemy' hoặc layer đánh được).
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class BreakableObject : NetworkBehaviour, IDamageable
    {
        /// <summary>Phía mà đòn đánh phải xuất phát từ đó để tính vào tiến độ vỡ.</summary>
        public enum BreakSide
        {
            [Tooltip("Đánh từ phía nào cũng vỡ.")] Any,
            [Tooltip("Chỉ vỡ khi player đứng BÊN PHẢI vật thể đánh tới.")] FromRight,
            [Tooltip("Chỉ vỡ khi player đứng BÊN TRÁI vật thể đánh tới.")] FromLeft,
        }

        [Header("---- ĐỘ BỀN ----")]
        [Tooltip("Số đòn đánh để vỡ.")]
        [SerializeField] private int hitsToBreak = 6;

        [Tooltip("Chỉ đòn từ phía này mới tính vào tiến độ vỡ. Đòn từ phía kia CHỈ rung, không cộng.")]
        [SerializeField] private BreakSide breakOnlyFromSide = BreakSide.FromRight;

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

        /// <summary>
        /// Tăng ở MỌI đòn trúng, kể cả đòn sai hướng (không cộng HitCount). Cần riêng vì rung phải phát
        /// cho cả đòn sai hướng — nếu Render chỉ theo HitCount thì đánh từ phía sai sẽ không có phản hồi
        /// nào, player tưởng vật thể không đánh được.
        /// </summary>
        [Networked] private int ShakeTick { get; set; }

        // Đếm đòn đã áp visual cục bộ (mọi peer) để phát rung khi ShakeTick tăng.
        private int _lastAppliedShakes;
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

            // Phía người đánh suy ra TỪ knockbackDir, không cần truyền thêm tham số: PlayerCombat dựng
            // pushDir = (target - attacker).normalized → đẩy RA XA người đánh. Nên pushDir.x < 0 nghĩa là
            // vật bị đẩy sang trái, tức người đánh đứng BÊN PHẢI.
            bool fromRight = knockbackDir.x < 0f;
            RPC_Hit(fromRight);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_Hit(NetworkBool fromRight)
        {
            if (Broken) return;

            ShakeTick++;             // luôn rung, kể cả sai hướng

            if (!SideCounts(fromRight)) return;

            HitCount++;
            if (HitCount >= hitsToBreak) Break();
        }

        /// <summary>Đòn từ phía này có tính vào tiến độ vỡ không?</summary>
        private bool SideCounts(bool fromRight) => breakOnlyFromSide switch
        {
            BreakSide.FromRight => fromRight,
            BreakSide.FromLeft => !fromRight,
            _ => true,
        };

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
            // ShakeTick tăng (host đếm, sync mọi peer) → rung 1 nhịp trên mọi máy. Bỏ qua khi đã vỡ.
            // Dùng ShakeTick chứ KHÔNG phải HitCount: đòn sai hướng không cộng HitCount nhưng vẫn phải rung.
            if (Broken) return;
            if (ShakeTick == _lastAppliedShakes) return;
            _lastAppliedShakes = ShakeTick;
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
                // unscaledDeltaTime: solo dừng game bằng timeScale=0 (GamePause) → deltaTime = 0 sẽ treo
                // vòng lặp và vật thể kẹt ở vị trí lệch. Xem ghi chú tương tự ở SceneFader.
                t += Time.unscaledDeltaTime;
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
