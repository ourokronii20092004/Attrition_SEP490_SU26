#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Attrition.Data;

namespace Attrition.Editor
{
    /// <summary>
    /// Sinh 8 QUEST NPC trao 8 accessory, sắp theo TIẾN TRÌNH CHƠI (map 1 → 4).
    ///
    /// Concept accessory (chốt với user):
    ///  - 2 cái NHẶT ngoài map, KHÔNG qua quest: acc_shadow_dash (Map 1), acc_double_jump (Map 2).
    ///  - 8 cái còn lại: thưởng quest NPC — mỗi map 2 quest (1 giết ELITE, 1 giết BOSS).
    ///
    /// Thứ tự thiết kế: mỗi map cho 1 accessory SINH TỒN/tiện dụng (elite, lấy sớm trong map) rồi 1
    /// accessory MẠNH HƠN (boss, cuối map). Độ mạnh tăng dần theo map:
    ///   Map 1: Vigor (stamina)   → Alchemist (bình máu tốt hơn)
    ///   Map 2: Ember (burn DoT)  → Renewal (tự hồi máu)
    ///   Map 3: Frost (làm chậm)  → Vampiric (hút máu)
    ///   Map 4: Aegis (lá chắn)   → Focus (combo sau skill)
    /// Lý do: người chơi mới cần sống sót trước (stamina/bình/hồi máu), giữa game mới có công cụ
    /// kiểm soát (burn/slow), cuối game mới có các hiệu ứng thưởng kỹ năng (lifesteal/shield/postskill).
    ///
    /// GATING tự nhiên theo map: NPC của map nào đặt trong map đó; muốn nhận quest sau phải đi tới map
    /// sau (cửa map chỉ mở khi hạ boss map trước). Không cần hệ prerequisite riêng.
    ///
    /// Menu: Tools/Attrition/NPC/Generate Accessory Quests (theo tiến trình)
    /// </summary>
    public static class AccessoryQuestSetupEditor
    {
        private const string QuestDir = "Assets/_Project/Data/NPC/AccessoryQuests";

        /// <summary>Một quest trao accessory.</summary>
        private struct Q
        {
            public int map;             // map đặt NPC (1..4)
            public string questId;
            public string title;
            public string targetId;     // enemyId cần giết
            public int amount;
            public bool isBoss;
            public string rewardItemId; // accessory thưởng
            public string rewardName;   // tên hiển thị (dùng trong thoại)
            public int exp;
            public string npcName;
            public string flavor;       // 1 câu dẫn truyện
        }

        /// <summary>
        /// BẢNG THỨ TỰ NHẬN ACCESSORY. Sửa `targetId`/`amount` ở đây khi đã chốt elite của từng map
        /// (hiện dùng elite có sẵn: crab/cultist/frogger/gollux/nightborne/undead).
        ///
        /// ⚠ enemyId của BOSS map 2..4 (druid/elf/demon_kin) hiện CHƯA tồn tại — các boss prefab đó chưa
        /// có EnemyStats + EnemyStatsSO. Quest vẫn tạo được, nhưng chỉ đếm tiến độ khi boss có EnemyStats
        /// với đúng enemyId bên dưới (xem log cảnh báo khi chạy tool).
        /// </summary>
        private static readonly Q[] Quests =
        {
            // ── MAP 1 ── nền tảng sinh tồn
            new Q { map = 1, questId = "q_m1_elite_vigor", title = "Trial of Endurance",
                    targetId = "undead", amount = 6, isBoss = false,
                    rewardItemId = "acc_stamina_charm", rewardName = "Vigor Charm", exp = 180,
                    npcName = "Warden of the Path",
                    flavor = "The restless dead drain the living. Thin their ranks and you will learn to endure." },
            new Q { map = 1, questId = "q_m1_boss_alchemist", title = "The Severed Fang",
                    targetId = "severed_fang", amount = 1, isBoss = true,
                    rewardItemId = "acc_potion", rewardName = "Alchemist Charm", exp = 400,
                    npcName = "Warden of the Path",
                    flavor = "A blade of fire guards the way onward. End it, and my elixirs are yours." },

            // ── MAP 2 ── công cụ tấn công đầu tiên
            new Q { map = 2, questId = "q_m2_elite_ember", title = "Embers in the Grove",
                    targetId = "frogger", amount = 8, isBoss = false,
                    rewardItemId = "acc_burn", rewardName = "Ember Charm", exp = 320,
                    npcName = "Druid Elder",
                    flavor = "The marsh-spawn multiply unchecked. Burn them out and take the ember for yourself." },
            new Q { map = 2, questId = "q_m2_boss_renewal", title = "Warden of the Wood",
                    targetId = "druid", amount = 1, isBoss = true,
                    rewardItemId = "acc_regen", rewardName = "Renewal Charm", exp = 650,
                    npcName = "Druid Elder",
                    flavor = "Our warden has turned. Free the grove, and the wood's own healing will answer to you." },

            // ── MAP 3 ── kiểm soát + duy trì
            new Q { map = 3, questId = "q_m3_elite_frost", title = "Stillness in the Valley",
                    targetId = "crab", amount = 10, isBoss = false,
                    rewardItemId = "acc_slow", rewardName = "Frost Charm", exp = 520,
                    npcName = "Elf Sentinel",
                    flavor = "The shelled ones swarm the shallows. Still them, and frost will heed your strikes." },
            new Q { map = 3, questId = "q_m3_boss_vampiric", title = "The Fallen Archer",
                    targetId = "elf", amount = 1, isBoss = true,
                    rewardItemId = "acc_lifesteal", rewardName = "Vampiric Charm", exp = 900,
                    npcName = "Elf Sentinel",
                    flavor = "One of our own bars the valley's end. Take back what was stolen — and drink from it." },

            // ── MAP 4 ── phần thưởng cuối game
            new Q { map = 4, questId = "q_m4_elite_aegis", title = "Shadows of the Dark Wood",
                    targetId = "nightborne", amount = 8, isBoss = false,
                    rewardItemId = "acc_shield", rewardName = "Aegis Charm", exp = 780,
                    npcName = "Demonkin Exile",
                    flavor = "Night-born things hunt in the dark. Break them, and their ward becomes your shield." },
            new Q { map = 4, questId = "q_m4_boss_focus", title = "Kin of the Abyss",
                    targetId = "demon_kin", amount = 1, isBoss = true,
                    rewardItemId = "acc_postskill", rewardName = "Focus Charm", exp = 1300,
                    npcName = "Demonkin Exile",
                    flavor = "My kin rules this wood by force. Unseat it, and your focus will never falter again." },
        };

        [MenuItem("Tools/Attrition/NPC/Generate Accessory Quests (theo tien trinh)")]
        public static void Generate()
        {
            EnsureFolders();

            var db = AssetDatabase.LoadAssetAtPath<ItemDatabaseSO>("Assets/_Project/Data/Items/ItemDatabase.asset");
            var missingItems = new List<string>();
            var missingEnemies = new List<string>();
            var created = new List<QuestSO>();

            foreach (var q in Quests)
            {
                if (db != null && !HasItem(db, q.rewardItemId)) missingItems.Add(q.rewardItemId);
                if (!EnemyIdExists(q.targetId)) missingEnemies.Add(q.targetId);
                created.Add(BuildQuest(q));
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[AccessoryQuests] Đã tạo/cập nhật {created.Count} quest tại {QuestDir}.\n" +
                      "Gán từng QuestSO vào field 'quest' của NPC tương ứng (Map 1..4, mỗi map 2 NPC " +
                      "hoặc 1 NPC làm 2 quest lần lượt).");

            if (missingItems.Count > 0)
                Debug.LogWarning("[AccessoryQuests] Accessory CHƯA có trong ItemDatabase (thưởng sẽ không vào túi): "
                                 + string.Join(", ", missingItems) + " → chạy 'Attrition/Setup Item System'.");

            if (missingEnemies.Count > 0)
                Debug.LogWarning("[AccessoryQuests] enemyId CHƯA tồn tại trong Data/Enemies (quest sẽ KHÔNG đếm "
                                 + "tiến độ): " + string.Join(", ", missingEnemies)
                                 + ". Boss map 2..4 cần EnemyStatsSO + gắn EnemyStats vào prefab với đúng id.");
        }

        //  BUILD

        private static QuestSO BuildQuest(Q q)
        {
            string tag = q.isBoss ? "boss" : "elite";
            string prettyTarget = q.targetId.Replace('_', ' ');

            var offer = Dialogue($"Dlg_{q.questId}_Offer", new[]
            {
                Line(q.npcName, q.flavor),
                Line(q.npcName, q.isBoss
                    ? $"Slay it, and I will give you the {q.rewardName}."
                    : $"Slay {q.amount} {prettyTarget}, and the {q.rewardName} is yours."),
            });

            var prog = Dialogue($"Dlg_{q.questId}_InProgress", new[]
            {
                Line(q.npcName, q.isBoss
                    ? "It still stands. You are not finished."
                    : $"Not yet. The {prettyTarget} still draw breath."),
            });

            var done = Dialogue($"Dlg_{q.questId}_Complete", new[]
            {
                Line(q.npcName, "It is done. You have earned this."),
                Line(q.npcName, $"Take the {q.rewardName}. Use it well."),
            });

            var fin = Dialogue($"Dlg_{q.questId}_Finished", new[]
            {
                Line(q.npcName, "The road ahead is darker still. Go carefully."),
            });

            var quest = ScriptableObject.CreateInstance<QuestSO>();
            quest.questId = q.questId;
            quest.title = q.title;
            quest.description = q.isBoss
                ? $"[Map {q.map}] Defeat the boss ({prettyTarget}). Reward: {q.rewardName}."
                : $"[Map {q.map}] Slay {q.amount} {prettyTarget}. Reward: {q.rewardName}.";
            quest.objectiveType = QuestObjectiveType.Kill;
            quest.targetId = q.targetId;
            quest.requiredAmount = q.amount;
            quest.expReward = q.exp;
            quest.itemRewards = new[] { new QuestItemReward { itemId = q.rewardItemId, amount = 1 } };
            quest.dialogueNotStarted = offer;
            quest.dialogueInProgress = prog;
            quest.dialogueCompleted = done;
            quest.dialogueFinished = fin;

            return Save(quest, $"Quest_M{q.map}_{tag}_{q.rewardItemId}");
        }

        private static DialogueLine Line(string who, string text)
            => new DialogueLine { speakerName = who, text = text };

        private static DialogueSO Dialogue(string assetName, DialogueLine[] lines)
        {
            var d = ScriptableObject.CreateInstance<DialogueSO>();
            d.lines = lines;
            return Save(d, assetName);
        }

        //  HELPERS

        /// <summary>Ghi asset, giữ GUID nếu đã tồn tại (tránh làm đứt tham chiếu đã gán trên NPC).</summary>
        private static T Save<T>(T so, string fileName) where T : ScriptableObject
        {
            string path = $"{QuestDir}/{fileName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(so, existing);
                Object.DestroyImmediate(so);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(so, path);
            return so;
        }

        private static bool HasItem(ItemDatabaseSO db, string itemId)
        {
            foreach (var it in db.EditorItems)
                if (it != null && it.itemId == itemId) return true;
            return false;
        }

        /// <summary>Có EnemyStatsSO nào dùng enemyId này chưa? (quest Kill đếm theo EnemyStats.EnemyId)</summary>
        private static bool EnemyIdExists(string enemyId)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:EnemyStatsSO"))
            {
                var so = AssetDatabase.LoadAssetAtPath<EnemyStatsSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (so != null && so.enemyId == enemyId) return true;
            }
            return false;
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Data/NPC"))
                AssetDatabase.CreateFolder("Assets/_Project/Data", "NPC");
            if (!AssetDatabase.IsValidFolder(QuestDir))
                AssetDatabase.CreateFolder("Assets/_Project/Data/NPC", "AccessoryQuests");
        }
    }
}
#endif
