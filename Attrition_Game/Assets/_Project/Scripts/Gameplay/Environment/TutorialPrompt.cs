using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Attrition.Gameplay.Player;

namespace Attrition.Gameplay.Environment
{
    /// <summary>
    /// Bảng HƯỚNG DẪN tân thủ kiểu Hollow Knight (hiện ở DƯỚI màn hình). Tự dựng Canvas riêng
    /// (giống WorldMapController/SceneFader) nên KHÔNG đụng GameUI.uxml.
    ///
    /// 2 chế độ kích hoạt (trigger):
    ///  - OnEnterZone: player LOCAL đi vào vùng (BoxCollider2D isTrigger) → hiện. Dùng ở đầu Map 1.
    ///  - OnAbilityUnlocked: khi local player VỪA mở khoá ĐỦ các ability yêu cầu (double jump / shadow
    ///    dash qua PlayerInventory.HasAbility) → hiện. Dùng ở Map 2 sau khi nhặt vật phẩm.
    ///
    /// Mỗi bảng chỉ hiện MỘT LẦN mỗi lượt chơi (guard static theo tutorialId; reset khi mở game mới =
    /// process mới). Không cần network: hướng dẫn là UI cục bộ của từng người chơi.
    ///
    /// Nội dung: mỗi dòng 1 cặp "phím — mô tả" điền trong Inspector. Bấm phím bất kỳ / click để đóng
    /// (hoặc tự đóng sau autoHideSeconds nếu > 0).
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class TutorialPrompt : MonoBehaviour
    {
        public enum TriggerMode { OnEnterZone, OnAbilityUnlocked }

        [System.Serializable]
        public struct Line
        {
            [Tooltip("Phím hiển thị, vd 'A / D', 'Space', 'J'.")] public string key;
            [Tooltip("Mô tả hành động, vd 'Di chuyển'.")] public string description;
        }

        [Header("---- ĐỊNH DANH ----")]
        [Tooltip("Id duy nhất — bảng cùng id chỉ hiện 1 lần mỗi lượt chơi. VD 'map1_basics', 'map2_abilities'.")]
        [SerializeField] private string tutorialId = "map1_basics";

        [Header("---- KÍCH HOẠT ----")]
        [SerializeField] private TriggerMode trigger = TriggerMode.OnEnterZone;
        [Tooltip("OnAbilityUnlocked: các ability phải ĐỀU sở hữu thì mới hiện (vd DoubleJump + ShadowDash).")]
        [SerializeField] private Attrition.Data.GrantedAbility[] requiredAbilities = new Attrition.Data.GrantedAbility[0];

        [Header("---- NỘI DUNG ----")]
        [Tooltip("Tiêu đề bảng (vd 'ĐIỀU KHIỂN CƠ BẢN').")]
        [SerializeField] private string title = "ĐIỀU KHIỂN CƠ BẢN";
        [SerializeField] private Line[] lines = new Line[0];

        [Header("---- HÀNH VI ----")]
        [Tooltip("Tự ẩn sau bao nhiêu giây (0 = chỉ ẩn khi người chơi bấm phím / click).")]
        [SerializeField] private float autoHideSeconds = 0f;

        [Header("---- CREDITS (tùy chọn) ----")]
        [Tooltip("Hiện credits team ở góc phải-dưới NGAY SAU khi đóng bảng hướng dẫn. Bỏ trống = không hiện.")]
        [SerializeField] private string creditsTitle = "";
        [Tooltip("Mỗi dòng 1 tên thành viên (hoặc 'Tên — Vai trò').")]
        [SerializeField] private string[] creditsMembers = new string[0];

        // Guard static: bảng đã hiện trong lượt chơi này chưa (reset khi process mới = mở game lại).
        private static readonly HashSet<string> _shown = new HashSet<string>();

        private bool _consumed;

        private void Reset()
        {
            var col = GetComponent<BoxCollider2D>();
            if (col != null) col.isTrigger = true;
        }

        private void Update()
        {
            if (trigger != TriggerMode.OnAbilityUnlocked || _consumed) return;
            if (AlreadyShown()) { _consumed = true; return; }

            var local = FindLocalPlayerInventory();
            if (local == null) return;

            foreach (var ab in requiredAbilities)
                if (!local.HasAbility(ab)) return; // chưa đủ ability → chờ

            Trigger();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (trigger != TriggerMode.OnEnterZone || _consumed) return;

            var pc = other.GetComponentInParent<PlayerController>();
            if (pc == null || !pc.HasInputAuthority) return; // chỉ player LOCAL mới hiện hướng dẫn

            Trigger();
        }

        private void Trigger()
        {
            _consumed = true;
            if (AlreadyShown()) return;
            _shown.Add(tutorialId);

            // Có cấu hình credits → hiện góc phải-dưới NGAY SAU khi người chơi đóng bảng hướng dẫn.
            System.Action onClosed = (creditsMembers != null && creditsMembers.Length > 0)
                ? () => TeamCreditsPanel.Show(tutorialId + "_credits", creditsTitle, creditsMembers, 0.35f, 3f)
                : (System.Action)null;

            TutorialPanel.Show(title, lines, autoHideSeconds, onClosed);
        }

        private bool AlreadyShown() => _shown.Contains(tutorialId);

        private static Attrition.Gameplay.Player.Inventory.PlayerInventory FindLocalPlayerInventory()
        {
            foreach (var pc in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
                if (pc != null && pc.HasInputAuthority)
                    return pc.GetComponent<Attrition.Gameplay.Player.Inventory.PlayerInventory>();
            return null;
        }

        private void OnDrawGizmos()
        {
            if (trigger != TriggerMode.OnEnterZone) return;
            var col = GetComponent<BoxCollider2D>();
            if (col == null) return;
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.25f);
            Vector3 center = transform.position + (Vector3)col.offset;
            Vector3 size = new Vector3(col.size.x * transform.lossyScale.x, col.size.y * transform.lossyScale.y, 1f);
            Gizmos.DrawCube(center, size);
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 1f);
            Gizmos.DrawWireCube(center, size);
        }
    }
}
