using UnityEngine;

namespace Attrition.Gameplay.Environment
{
    /// <summary>
    /// Vùng KHU VỰC: đặt 1 BoxCollider2D (IsTrigger) phủ một khu. Khi player LOCAL vào khu mới →
    /// hiện banner tên khu giữa màn hình (AreaNameBanner). Đặt nhiều vùng cho các khu khác nhau.
    ///
    /// Chống lặp: lưu tĩnh tên khu hiện tại — chỉ hiện banner khi player ĐỔI sang khu khác
    /// (đi qua lại trong cùng khu không spam). Hiệu ứng local mỗi máy.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class AreaNameZone : MonoBehaviour
    {
        [Tooltip("Tên khu vực hiển thị (vd 'Forgotten Crossroads').")]
        [SerializeField] private string areaName = "";

        private static string _currentArea;

        private void Awake()
        {
            GetComponent<Collider2D>().isTrigger = true;
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            // Dùng Stay (không phải Enter) để bắt cả trường hợp player SPAWN SẴN trong zone sau khi
            // load scene — lúc đó Enter đã fire mất trong lúc đang fade. Guard _currentArea chống spam.
            TryShow(other);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryShow(other);
        }

        private void TryShow(Collider2D other)
        {
            // KHÔNG hiện tên khu ngay khi vừa load scene (player spawn sẵn trong zone / đang fade).
            // timeSinceLevelLoad tự reset =0 mỗi lần load scene mới → chờ scene ổn định rồi mới hiện.
            if (Time.timeSinceLevelLoad < LoadGrace) return;
            if (Attrition.Gameplay.Environment.SceneFader.IsTransitioning) return;

            if (!other.CompareTag("Player")) return;
            var pc = other.GetComponentInParent<PlayerController>();
            if (pc == null || !pc.HasInputAuthority) return; // chỉ player local

            if (string.IsNullOrEmpty(areaName) || areaName == _currentArea) return;
            _currentArea = areaName;
            AreaNameBanner.Show(areaName);
        }

        private const float LoadGrace = 1.2f;

        private void OnDrawGizmos()
        {
            var col = GetComponent<Collider2D>();
            if (col is BoxCollider2D box)
            {
                Gizmos.color = new Color(1f, 0.85f, 0.4f, 0.12f);
                Vector3 c = transform.position + (Vector3)box.offset;
                Vector3 s = new Vector3(box.size.x * transform.lossyScale.x, box.size.y * transform.lossyScale.y, 1f);
                Gizmos.DrawCube(c, s);
                Gizmos.color = new Color(1f, 0.85f, 0.4f, 0.9f);
                Gizmos.DrawWireCube(c, s);
            }
        }
    }
}
