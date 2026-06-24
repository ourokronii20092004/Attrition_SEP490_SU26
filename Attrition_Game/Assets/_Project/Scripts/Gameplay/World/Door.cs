using Fusion;
using UnityEngine;

namespace Attrition.Gameplay.World
{
    /// <summary>
    /// Cửa networked: đóng/mở đồng bộ mọi máy. Khi ĐÓNG → bật collider chặn đường + hiện visual;
    /// khi MỞ → tắt collider + ẩn (hoặc đổi sprite/animation). Trạng thái IsOpen là [Networked]
    /// nên client join muộn vẫn thấy đúng.
    ///
    /// Dùng cho:
    ///  - Cửa mở sau khi đánh bại Boss (BossEncounterController.OpenDoor()).
    ///  - Cửa mở khi giải puzzle 2 nút (CoopPlateDoorController).
    ///
    /// Gắn lên 1 GameObject. Kéo blockingCollider (collider rắn chặn player) + doorVisual (sprite/đồ hoạ).
    /// Host gọi Open()/Close(); client tự cập nhật qua OnChanged.
    /// </summary>
    public class Door : NetworkBehaviour
    {
        [Header("---- TRẠNG THÁI BAN ĐẦU ----")]
        [Tooltip("Cửa mở sẵn khi spawn? Thường để FALSE (đóng).")]
        [SerializeField] private bool startOpen = false;

        [Header("---- THÀNH PHẦN ----")]
        [Tooltip("Collider RẮN chặn player khi cửa đóng (KHÔNG phải trigger). Mở cửa → tắt collider này.")]
        [SerializeField] private Collider2D blockingCollider;
        [Tooltip("Đồ hoạ cửa (sprite/object) hiện khi ĐÓNG, ẩn khi MỞ. Bỏ trống = không đổi hình.")]
        [SerializeField] private GameObject doorVisual;
        [Tooltip("Đồ hoạ hiện khi MỞ (vd cửa đã mở). Bỏ trống = không có.")]
        [SerializeField] private GameObject openedVisual;

        [Header("---- FEEDBACK ----")]
        [Tooltip("VFX phát 1 lần tại vị trí cửa khi mở. Bỏ trống = không.")]
        [SerializeField] private GameObject openVfxPrefab;

        [Networked] public NetworkBool IsOpen { get; set; }

        // Theo dõi thay đổi cục bộ trên MỌI máy (giống BossController.Render) để client cũng cập nhật.
        private bool _hasLocalState;
        private bool _lastAppliedOpen;

        public override void Spawned()
        {
            if (HasStateAuthority) IsOpen = startOpen;
            ApplyVisualState(IsOpen, playVfx: false);
            _hasLocalState = true;
            _lastAppliedOpen = IsOpen;
        }

        public override void Render()
        {
            // Phát hiện IsOpen đổi (host set hoặc nhận từ server) → áp visual + VFX khi vừa mở.
            if (!_hasLocalState) return;
            bool open = IsOpen;
            if (open == _lastAppliedOpen) return;
            _lastAppliedOpen = open;
            ApplyVisualState(open, playVfx: open);
        }

        /// <summary>Mở cửa (host). Idempotent — gọi nhiều lần không sao.</summary>
        public void Open()
        {
            if (!HasStateAuthority || IsOpen) return;
            IsOpen = true;
        }

        /// <summary>Đóng cửa (host).</summary>
        public void Close()
        {
            if (!HasStateAuthority || !IsOpen) return;
            IsOpen = false;
        }

        private void ApplyVisualState(bool open, bool playVfx)
        {
            if (blockingCollider != null) blockingCollider.enabled = !open;
            if (doorVisual != null) doorVisual.SetActive(!open);
            if (openedVisual != null) openedVisual.SetActive(open);

            if (open && playVfx && openVfxPrefab != null)
                Instantiate(openVfxPrefab, transform.position, Quaternion.identity);
        }
    }
}
