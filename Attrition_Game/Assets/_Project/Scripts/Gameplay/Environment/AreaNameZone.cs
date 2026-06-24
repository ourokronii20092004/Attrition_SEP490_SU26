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

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            var pc = other.GetComponentInParent<PlayerController>();
            if (pc == null || !pc.HasInputAuthority) return; // chỉ player local

            if (string.IsNullOrEmpty(areaName) || areaName == _currentArea) return;
            _currentArea = areaName;
            AreaNameBanner.Show(areaName);
        }

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
