using UnityEngine;

namespace Attrition.Gameplay.World
{
    /// <summary>
    /// Bẫy môi trường (gai, hố, dung nham...). Player chạm vào → mất 15% Max HP +
    /// hồi sinh tại điểm đất an toàn cuối (BR-38/39). Logic ở PlayerController.HazardHit.
    /// Gắn vào GameObject có Collider2D (isTrigger). Không cần NetworkObject:
    /// mỗi PlayerController tự xử lý phần networked của mình.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Hazard : MonoBehaviour
    {
        [Tooltip("Giãn cách tối thiểu giữa 2 lần trúng bẫy của cùng 1 player (giây).")]
        [SerializeField] private float retriggerCooldown = 1.0f;

        private float _lastHitTime = -999f;

        private void Reset()
        {
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other) => TryHit(other);
        private void OnTriggerStay2D(Collider2D other) => TryHit(other);

        private void TryHit(Collider2D other)
        {
            if (Time.time - _lastHitTime < retriggerCooldown) return;

            var player = other.GetComponentInParent<PlayerController>();
            if (player == null || player.IsDead) return;

            _lastHitTime = Time.time;
            player.HazardHit();
        }
    }
}
