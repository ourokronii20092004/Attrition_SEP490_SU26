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

        // Màu hiển thị của cửa khi ĐÓNG. Nếu doorVisual có SpriteRenderer chưa gán sprite, Door tự tạo
        // 1 sprite trắng 1×1 runtime dùng chung để màu hiện được (sprite null thì màu không render).
        [Header("---- MÀU CỬA (khi đóng) ----")]
        [Tooltip("Màu hiển thị khi cửa đóng. Để phân biệt 2 cửa của puzzle.")]
        [SerializeField] private Color closedColor = new Color(0.4f, 0.25f, 0.15f, 1f);

        private static Sprite _fallbackSprite;   // sprite trắng 1×1 dùng chung (cache)

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
            if (doorVisual != null)
            {
                doorVisual.SetActive(!open);
                // Nếu visual có SpriteRenderer chưa gán sprite → gán sprite trắng dùng chung + màu riêng.
                var sr = doorVisual.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    if (sr.sprite == null)
                    {
                        sr.sprite = FallbackSprite();
                        // BẮT BUỘC đặt Simple: prefab gán DrawMode=Sliced (9-slice) nhưng sprite trắng
                        // 1×1 không có border → Unity render lỗi/trong suốt. Simple thì hiện đúng màu.
                        sr.drawMode = SpriteDrawMode.Simple;
                    }
                    sr.color = closedColor;

                    // Đảm bảo cửa hiển thị TRÊN ground. Visual cửa Map 5 là tile "Dungeon Tile Set_45"
                    // ở SortingLayer Default(0) + order 0, còn ground ở layer Ground(order cao hơn) →
                    // ground vẽ sau và che mất cửa, nhìn như trong suốt. Nâng order lên để cửa luôn thấy.
                    if (sr.sortingOrder < 5) sr.sortingOrder = 5;
                }
            }
            if (openedVisual != null) openedVisual.SetActive(open);

            if (open && playVfx && openVfxPrefab != null)
                Instantiate(openVfxPrefab, transform.position, Quaternion.identity);
        }

        /// <summary>Sprite trắng 1×1 dùng chung khi visual cửa chưa có sprite. Tạo 1 lần rồi cache.</summary>
        private static Sprite FallbackSprite()
        {
            if (_fallbackSprite != null) return _fallbackSprite;
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _fallbackSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
            return _fallbackSprite;
        }
    }
}
