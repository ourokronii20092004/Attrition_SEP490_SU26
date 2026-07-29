using System.Collections.Generic;
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

        // Cooldown THEO TỪNG PLAYER, không dùng 1 biến chung: cả map chỉ có 1 Tilemap hazard nên nếu
        // dùng chung, player A trúng bẫy sẽ khoá luôn player B trong coop (B đi vào gai mà không mất máu).
        private readonly Dictionary<PlayerController, float> _lastHitByPlayer = new Dictionary<PlayerController, float>();

        private void Reset()
        {
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other) => TryHit(other);
        private void OnTriggerStay2D(Collider2D other) => TryHit(other);

        private void TryHit(Collider2D other)
        {
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null || player.IsDead) return;

            if (_lastHitByPlayer.TryGetValue(player, out float last)
                && Time.time - last < retriggerCooldown) return;

            _lastHitByPlayer[player] = Time.time;
            player.HazardHit();
        }
    }
}
