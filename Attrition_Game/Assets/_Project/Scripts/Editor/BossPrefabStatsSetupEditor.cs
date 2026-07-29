#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Fusion;
using Attrition.Data;
using Attrition.Controllers;
using Attrition.Gameplay.Enemy;

namespace Attrition.Editor
{
    /// <summary>
    /// Gắn `NetworkObject` + `EnemyController` + `EnemyStats` (kèm statsSO đúng boss) vào 3 prefab boss
    /// Druid / Elf / DemonKin.
    ///
    /// VÌ SAO CẦN: quest "giết boss" đếm qua `NetworkNPC.NotifyEnemyKilled`, mà hàm này CHỈ được gọi từ
    /// `EnemyController.DieFinal()` và đọc id từ `EnemyStats.EnemyId`. Ba prefab trên hiện chỉ có
    /// SpriteRenderer/Animator (style ArchDemon visual-only) → không có EnemyController thì boss KHÔNG
    /// bao giờ "chết" theo nghĩa logic, quest accessory map 2-4 sẽ không bao giờ hoàn thành.
    ///
    /// Tool KHÔNG đụng AI: Druid đã có DruidBossAI; DemonKin/Elf chưa có AI riêng nên sau khi chạy tool
    /// chúng vẫn đứng yên (đánh được, chết được, nhưng không tấn công). Viết AI sau nếu cần.
    ///
    /// Menu: Tools/Attrition/Enemy/Setup Boss Prefabs (Controller + Stats)
    /// Idempotent: chạy lại không thêm trùng component.
    /// </summary>
    public static class BossPrefabStatsSetupEditor
    {
        private const string PrefabDir = "Assets/_Project/Prefabs/Enemy";
        private const string StatsDir = "Assets/_Project/Data/Enemies";

        /// <summary>(tên prefab, tên file stats asset) — boss map 2, 3, 4.</summary>
        private static readonly (string prefab, string stats)[] Targets =
        {
            ("Druid",    "Druid_Stats"),      // Map 2
            ("Elf",      "Elf_Stats"),        // Map 3
            ("DemonKin", "DemonKin_Stats"),   // Map 4
        };

        [MenuItem("Tools/Attrition/Enemy/Setup Boss Prefabs (Controller + Stats)")]
        public static void Setup()
        {
            int done = 0;

            foreach (var (prefabName, statsName) in Targets)
            {
                string prefabPath = $"{PrefabDir}/{prefabName}.prefab";
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (go == null)
                {
                    Debug.LogWarning($"[BossSetup] Không thấy prefab: {prefabPath} — bỏ qua.");
                    continue;
                }

                var statsSO = AssetDatabase.LoadAssetAtPath<EnemyStatsSO>($"{StatsDir}/{statsName}.asset");
                if (statsSO == null)
                {
                    Debug.LogWarning($"[BossSetup] Không thấy stats: {StatsDir}/{statsName}.asset — bỏ qua {prefabName}.");
                    continue;
                }

                // Sửa trên nội dung prefab (không instantiate) để giữ nguyên mọi thứ đã cấu hình.
                var root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    // Fusion: EnemyController/EnemyStats là NetworkBehaviour → BẮT BUỘC có NetworkObject.
                    if (root.GetComponent<NetworkObject>() == null) root.AddComponent<NetworkObject>();

                    var stats = root.GetComponent<EnemyStats>();
                    if (stats == null) stats = root.AddComponent<EnemyStats>();

                    // statsSO là private [SerializeField] → set qua SerializedObject.
                    var so = new SerializedObject(stats);
                    var prop = so.FindProperty("statsSO");
                    if (prop != null)
                    {
                        prop.objectReferenceValue = statsSO;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }

                    if (root.GetComponent<EnemyController>() == null) root.AddComponent<EnemyController>();

                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    done++;
                    Debug.Log($"[BossSetup] {prefabName}: đã có NetworkObject + EnemyStats({statsSO.enemyId}) + EnemyController.");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[BossSetup] Xong {done}/{Targets.Length} prefab. LƯU Ý:\n" +
                      "• Kiểm tra Inspector: EnemyController cần gán aiComp/animationComp/combatComp nếu boss có AI.\n" +
                      "• DemonKin/Elf chưa có AI riêng → sẽ đứng yên (vẫn đánh/chết được).\n" +
                      "• Boss đặt sẵn trong scene: mở scene rồi SAVE để Fusion bake NetworkObject.");
        }
    }
}
#endif
