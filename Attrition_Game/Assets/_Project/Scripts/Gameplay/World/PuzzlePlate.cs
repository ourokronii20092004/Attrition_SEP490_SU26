using Fusion;
using UnityEngine;

namespace Attrition.Gameplay.World
{
    /// <summary>
    /// Bệ kích hoạt (pressure plate) cho puzzle. Active khi có ít nhất 1 player đứng lên.
    /// Mặc định: nhả ra thì tắt (momentary). Bật "latching" để giữ active sau lần đầu đạp.
    /// IsActive là Networked → PuzzleController (host) đọc để kiểm tra điều kiện giải.
    /// Gắn lên GameObject có Collider2D (isTrigger).
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class PuzzlePlate : NetworkBehaviour
    {
        [Tooltip("True = đạp 1 lần là giữ luôn (cho puzzle cần đạp đúng tổ hợp). False = nhả ra thì tắt.")]
        [SerializeField] private bool latching = false;

        [Networked] public NetworkBool IsActive { get; set; }

        private int _occupants;

        private void Reset()
        {
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!HasStateAuthority) return;
            if (other.GetComponentInParent<PlayerController>() == null) return;

            _occupants++;
            IsActive = true;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!HasStateAuthority || latching) return;
            if (other.GetComponentInParent<PlayerController>() == null) return;

            _occupants = Mathf.Max(0, _occupants - 1);
            if (_occupants == 0) IsActive = false;
        }
    }
}
