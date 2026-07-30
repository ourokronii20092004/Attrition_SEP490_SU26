using Fusion;
using UnityEngine;
using Attrition.Controllers;

namespace Attrition.Gameplay.World
{
    /// <summary>
    /// Cổng vào phòng BOSS CUỐI của Map 5: chỉ mở khi player đã hạ HẾT các BOSS CŨ đặt trong các room nhỏ
    /// (boss 2 Druid / 3 Elf / 4 DemonKin). Thứ tự đánh boss nào trước KHÔNG quan trọng — cổng chỉ xét
    /// "đã hạ đủ số chưa".
    ///
    /// Dùng lại được cho Map 4 (room chứa boss 1 SeveredFang): kéo boss đó vào `requiredBosses` là xong.
    ///
    /// VÌ SAO KHÔNG DÙNG BossGateController: cái đó quản 1 boss + chuỗi chết + thoại + reset sau wipe cho
    /// ĐÚNG MỘT encounter. Ở đây cần điều kiện GỘP nhiều boss độc lập, mỗi boss tự có gate riêng — nên tách
    /// thành component nhỏ chỉ làm một việc: canh đủ số rồi mở cửa.
    ///
    /// Chỉ host xét (IsDead là [Networked] nên client thấy cửa mở theo Door.IsOpen đã sync).
    /// </summary>
    public class BossRematchGate : NetworkBehaviour
    {
        [Header("---- ĐIỀU KIỆN ----")]
        [Tooltip("Các boss phải hạ hết trước khi cổng mở. Map 5: kéo boss 2/3/4 (bản đánh lại) vào đây.")]
        [SerializeField] private EnemyController[] requiredBosses = new EnemyController[0];

        [Header("---- CỔNG ----")]
        [Tooltip("Cửa sẽ MỞ khi hạ đủ boss. Bỏ trống = chỉ dùng cờ AllCleared (script khác đọc).")]
        [SerializeField] private Door gateDoor;

        [Tooltip("Đóng cửa lúc bắt đầu? Bật = cửa khoá tới khi hạ đủ boss.")]
        [SerializeField] private bool closeOnStart = true;

        [Header("---- THOẠI (tùy chọn) ----")]
        [Tooltip("Thoại phát MỘT LẦN khi cổng vừa mở. Bỏ trống = không có thoại.")]
        [SerializeField] private Attrition.Data.DialogueSO openDialogue;

        /// <summary>Đã hạ hết boss yêu cầu chưa (host ghi, mọi peer đọc).</summary>
        [Networked] public NetworkBool AllCleared { get; set; }

        /// <summary>Đã mở cửa + phát thoại rồi chưa — tránh mở/phát lại mỗi tick.</summary>
        [Networked] private NetworkBool Opened { get; set; }

        public override void Spawned()
        {
            if (!HasStateAuthority) return;

            if (closeOnStart && gateDoor != null && !AllCleared) gateDoor.Close();
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || Opened) return;
            if (Attrition.Persistence.GamePause.IsPaused) return;

            if (!AllBossesDown()) return;

            AllCleared = true;
            Opened = true;

            if (gateDoor != null) gateDoor.Open();
            if (openDialogue != null) RpcShowOpenDialogue();
        }

        /// <summary>
        /// Đã hạ hết boss yêu cầu?
        ///
        /// Boss bị despawn sau khi chết (EnemyController tự Despawn) → tham chiếu thành null. Coi NULL LÀ
        /// ĐÃ HẠ, nếu không cổng sẽ không bao giờ mở khi boss vừa biến mất.
        /// Danh sách rỗng → chưa cấu hình, KHÔNG mở (thà cửa khoá còn hơn mở toang vì thiếu setup).
        /// </summary>
        private bool AllBossesDown()
        {
            if (requiredBosses == null || requiredBosses.Length == 0) return false;

            foreach (var b in requiredBosses)
            {
                if (b == null) continue;                       // đã chết + despawn
                if (b.Object == null || !b.Object.IsValid) continue;
                if (!b.IsDead) return false;                   // còn sống → chưa đủ
            }
            return true;
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcShowOpenDialogue()
        {
            if (openDialogue == null) return;
            Attrition.Data.DialogueEvents.OnOpenCustomDialogue?.Invoke(openDialogue, null);
        }
    }
}
