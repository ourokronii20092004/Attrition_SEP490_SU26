using Fusion;
using UnityEngine;
using Attrition.Data;
using Attrition.Gameplay.Player;
using Attrition.Gameplay.Player.Inventory;

namespace Attrition.Gameplay.World
{
    /// <summary>
    /// Khung giải đố cơ bản: gom N "plate" (bệ kích hoạt). Khi TẤT CẢ plate active đồng thời
    /// → puzzle SOLVED (host), phát phần thưởng cho MỖI player (BR-33: loot cố định instanced).
    /// Phần thưởng: item vào inventory + có thể tăng cap bình (giống elite/puzzle theo concept).
    /// Đã solve thì khoá, không phát lại (Networked).
    /// </summary>
    public class PuzzleController : NetworkBehaviour
    {
        [Header("---- PLATES ----")]
        [Tooltip("Các bệ thuộc puzzle này. Đủ tất cả active cùng lúc → giải xong.")]
        [SerializeField] private PuzzlePlate[] plates = new PuzzlePlate[0];

        [Header("---- REWARD (instanced cho mỗi player) ----")]
        [Tooltip("Item thưởng khi giải xong (mỗi player nhận 1 bản).")]
        [SerializeField] private ItemSO rewardItem;
        [SerializeField] private int rewardAmount = 1;
        [Tooltip("Tăng cap bình máu tối đa cho mỗi player (0 = không).")]
        [SerializeField] private int bonusMaxHealthCharges = 0;
        [Tooltip("Tăng cap bình mana tối đa cho mỗi player (0 = không).")]
        [SerializeField] private int bonusMaxManaCharges = 0;

        [Header("---- FEEDBACK ----")]
        [Tooltip("Object mở ra khi giải xong (cửa/rương). Bỏ trống = không.")]
        [SerializeField] private GameObject onSolvedActivate;
        [SerializeField] private GameObject solvedVfxPrefab;

        [Networked] public NetworkBool IsSolved { get; set; }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || IsSolved || plates == null || plates.Length == 0) return;

            foreach (var p in plates)
                if (p == null || !p.IsActive) return; // chưa đủ → thoát

            Solve();
        }

        private void Solve()
        {
            IsSolved = true;

            // BR-33: thưởng instanced cho TỪNG player.
            var players = FindObjectsByType<PlayerStats>(FindObjectsSortMode.None);
            var db = ItemDatabaseSO.Instance;
            int idx = (rewardItem != null && db != null) ? db.GetIndex(rewardItem) : -1;

            foreach (var st in players)
            {
                if (idx >= 0)
                {
                    var inv = st.GetComponent<PlayerInventory>();
                    if (inv != null) inv.TryAddItem(idx, rewardAmount);
                }
                var pot = st.GetComponent<PotionSystem>();
                if (pot != null)
                {
                    if (bonusMaxHealthCharges > 0) pot.IncreaseMaxHealthCharges(bonusMaxHealthCharges);
                    if (bonusMaxManaCharges > 0) pot.IncreaseMaxManaCharges(bonusMaxManaCharges);
                }
            }

            RpcOnSolved(transform.position);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcOnSolved(Vector3 pos)
        {
            if (onSolvedActivate != null) onSolvedActivate.SetActive(true);
            if (solvedVfxPrefab != null) Instantiate(solvedVfxPrefab, pos, Quaternion.identity);
        }
    }
}
