using UnityEngine;

namespace Attrition.Gameplay.Environment
{
    /// <summary>
    /// Vùng đổi background theo khu vực (kiểu Afterimage). Đặt 1 BoxCollider2D (IsTrigger) ở ranh giới
    /// khu vực (vd ranh giới dưới-đất / trên-mặt-đất TRONG CÙNG 1 room). Khi LOCAL player vào vùng →
    /// đổi sang background của khu vực này.
    ///
    /// 2 chế độ (tùy cái nào được gán):
    ///   - CROSSFADE (khuyến nghị khi 2 bg khác SỐ LAYER): gán 'crossfade' + đặt 'regionId' = index background.
    ///     Giữ nguyên 2 object ParallaxBackground (3 lớp / 4 lớp), chỉ fade tắt cái này / hiện cái kia.
    ///   - SWITCHER (đổi sprite trên cùng layer): gán 'switcher' + 'backgroundSet'.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class BackgroundZone : MonoBehaviour
    {
        [Header("---- CHẾ ĐỘ CROSSFADE (đổi giữa các object ParallaxBackground) ----")]
        [Tooltip("Crossfade giữa các ParallaxBackground object (giữ nguyên số layer). Bỏ trống = tự tìm trong scene.")]
        [SerializeField] private ParallaxBackgroundCrossfade crossfade;

        [Header("---- CHẾ ĐỘ SWITCHER (đổi sprite trên cùng layer) ----")]
        [Tooltip("Bộ sprite cho khu vực này (mỗi phần tử ứng 1 layer của RegionBackgroundSwitcher, xa→gần).")]
        [SerializeField] private Sprite[] backgroundSet;
        [Tooltip("Switcher đổi sprite. Bỏ trống = không dùng chế độ này.")]
        [SerializeField] private RegionBackgroundSwitcher switcher;

        [Header("---- CHUNG ----")]
        [Tooltip("ID/INDEX khu vực (DUY NHẤT mỗi khu). Crossfade dùng làm index background; switcher dùng chống đổi trùng.")]
        [SerializeField] private int regionId = 0;

        private void Awake()
        {
            GetComponent<Collider2D>().isTrigger = true;
            if (crossfade == null) crossfade = FindAnyObjectByType<ParallaxBackgroundCrossfade>();
            if (switcher == null) switcher = FindAnyObjectByType<RegionBackgroundSwitcher>();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null || !player.HasInputAuthority) return; // chỉ player local đổi bg của mình

            // Ưu tiên crossfade (đổi giữa các object, giữ nguyên layer).
            if (crossfade != null) crossfade.ShowBackground(regionId);
            else if (switcher != null) switcher.SwitchTo(backgroundSet, regionId);
        }

        private void OnDrawGizmos()
        {
            var col = GetComponent<Collider2D>();
            if (col is BoxCollider2D box)
            {
                Gizmos.color = new Color(0.6f, 0.3f, 0.9f, 0.18f);
                Vector3 c = transform.position + (Vector3)box.offset;
                Vector3 s = new Vector3(box.size.x * transform.lossyScale.x, box.size.y * transform.lossyScale.y, 1f);
                Gizmos.DrawCube(c, s);
                Gizmos.color = new Color(0.6f, 0.3f, 0.9f, 0.9f);
                Gizmos.DrawWireCube(c, s);
            }
        }
    }
}
