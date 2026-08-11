#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Attrition.Data;

namespace Attrition.Editor
{
    /// <summary>
    /// Sinh 10 QUEST NPC trao 10 accessory, theo ĐÚNG THỨ TỰ user chốt (2026-07-30).
    ///
    /// BẢNG TIẾN TRÌNH (mọi accessory đều qua quest — KHÔNG còn món nhặt tự do ngoài map):
    ///   Map 1: elite → burn (coop-only)  | boss → shadow_dash
    ///   Map 2: elite → double_jump        | boss → regen
    ///   Map 3: elite → shield (coop-only)   | boss → stamina
    ///          NPC đầu map kể tình hình khu vực → báo cho NPC cuối map → acc_potion (coop-only)
    ///   Map 4: elite → slow (coop-only)     | boss → postskill
    ///   Map 5: elite → lifesteal (coop-only)
    ///
    /// COOP-ONLY: 5 món (burn, shield, slow, lifesteal, acc_potion) có `coopOnlyReward = true` trong
    /// AccessorySO. Quest VẪN hiện ở cả 2 chế độ (tiến trình/lore không lệch) nhưng solo hoàn thành thì
    /// KHÔNG nhận được món đó — chặn tại `NetworkNPC.DistributeRewards`, không nhân đôi điều kiện ở đây.
    ///
    /// GATING tự nhiên theo map: NPC của map nào đặt trong map đó; muốn nhận quest sau phải đi tới map
    /// sau (cửa map chỉ mở khi hạ boss map trước). Không cần hệ prerequisite riêng.
    ///
    /// ⚠ ELITE THEO MAP: `enemySpawnConfigs` của MỌI scene hiện đang RỖNG và chỉ Map 1 có elite đặt tay
    /// (Cultist). Bảng dưới chỉ định elite cho từng map theo thiết kế; muốn quest đếm được thì phải ĐẶT
    /// elite đó vào scene tương ứng (hoặc khai trong enemySpawnConfigs). Tool sẽ cảnh báo elite nào chưa
    /// xuất hiện trong scene nào.
    ///
    /// Menu: Tools/Attrition/NPC/Generate Accessory Quests (theo tiến trình)
    /// </summary>
    public static class AccessoryQuestSetupEditor
    {
        private const string QuestDir = "Assets/_Project/Data/NPC/AccessoryQuests";

        /// <summary>Loại nhiệm vụ — quyết định objectiveType và cách viết thoại.</summary>
        private enum QKind
        {
            Elite,      // giết N elite
            Boss,       // giết 1 boss
            Deliver     // đưa tin cho NPC khác (objectiveType = Custom)
        }

        /// <summary>Một quest trao accessory.</summary>
        private struct Q
        {
            public int map;             // map đặt NPC (1..5)
            public QKind kind;
            public string questId;
            public string title;
            public string targetId;     // Elite/Boss: enemyId. Deliver: custom key.
            public string[] targetIds;  // Elite multi-target: mỗi enemyId chỉ tính 1 lần.
            public int amount;
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
            // ── MAP 1 ── hai kỹ năng di chuyển nền tảng (mở đường cho toàn bộ game về sau)
            new Q { map = 1, kind = QKind.Elite, questId = "q_m1_elite_shadowdash",
                    title = "Trial of Shadows",
                    targetId = "cultist", amount = 1,
                    rewardItemId = "acc_burn", rewardName = "Ember Charm", exp = 180,
                    npcName = "Summer Fairy",
                    flavor = "The cultists bend the dark to their will. Break them, and the dark will carry you instead." },
            new Q { map = 1, kind = QKind.Boss, questId = "q_m1_boss_doublejump",
                    title = "The Severed Fang",
                    targetId = "severed_fang", amount = 1,
                    rewardItemId = "acc_shadow_dash", rewardName = "Shadow Cloak", exp = 400,
                    npcName = "Summer Fairy",
                    flavor = "A blade of fire guards the way onward. End it, and take the dark as your own." },

            // ── MAP 2 ── công cụ tấn công + duy trì
            new Q { map = 2, kind = QKind.Elite, questId = "q_m2_elite_burn",
                    title = "Embers in the Grove",
                    targetId = "gollux", amount = 1,
                    rewardItemId = "acc_double_jump", rewardName = "Feather Charm", exp = 320,
                    npcName = "Summer Fairy",
                    flavor = "A stone giant guards the grove. Break it, and the wind will lift you twice." },
            new Q { map = 2, kind = QKind.Boss, questId = "q_m2_boss_regen",
                    title = "Warden of the Wood",
                    targetId = "druid", amount = 1,
                    rewardItemId = "acc_regen", rewardName = "Renewal Charm", exp = 650,
                    npcName = "Summer Fairy",
                    flavor = "Our warden has turned. Free the grove, and the wood's own healing will answer to you." },

            // ── MAP 3 ── phòng thủ + thể lực + nhiệm vụ đưa tin
            new Q { map = 3, kind = QKind.Elite, questId = "q_m3_elite_shield",
                    title = "Stillness in the Valley",
                    targetId = "crab_frogger", targetIds = new[] { "crab", "frogger" }, amount = 2,
                    rewardItemId = "acc_shield", rewardName = "Aegis Charm", exp = 520,
                    npcName = "Summer Fairy",
                    flavor = "The crab and the frogger guard opposite sides of the valley. Defeat one of each, and their ward becomes yours." },
            new Q { map = 3, kind = QKind.Boss, questId = "q_m3_boss_stamina",
                    title = "The Fallen Archer",
                    targetId = "elf", amount = 1,
                    rewardItemId = "acc_stamina_charm", rewardName = "Vigor Charm", exp = 900,
                    npcName = "Summer Fairy",
                    flavor = "One of our own bars the valley's end. Take back what was stolen, and endure as we do." },

            // Nhiệm vụ ĐƯA TIN: Spring Fairy đầu map kể tình hình khu vực → báo cho Autumn Fairy cuối map.
            // objectiveType = Custom; NPC cuối map gọi NetworkNPC.NotifyCustomObjective(targetId) khi nói xong.
            new Q { map = 3, kind = QKind.Deliver, questId = "q_m3_deliver_potion",
                    title = "Word from the Valley's Mouth",
                    targetId = "deliver_m3_report", amount = 1,
                    rewardItemId = "acc_potion", rewardName = "Alchemist Charm", exp = 600,
                    npcName = "Spring Fairy",
                    flavor = "I have watched this valley rot from its mouth. Carry my account to the far end and let them hear it." },

            // ── MAP 4 ── kiểm soát + thưởng kỹ năng
            new Q { map = 4, kind = QKind.Elite, questId = "q_m4_elite_slow",
                    title = "Shadows of the Dark Wood",
                    targetId = "nightborne", amount = 1,
                    rewardItemId = "acc_slow", rewardName = "Frost Charm", exp = 780,
                    npcName = "Summer Fairy",
                    flavor = "The nightborne stalk the dark wood. Lay one to rest, and frost will heed your strikes." },
            new Q { map = 4, kind = QKind.Boss, questId = "q_m4_boss_postskill",
                    title = "Kin of the Abyss",
                    targetId = "demon_kin", amount = 1,
                    rewardItemId = "acc_postskill", rewardName = "Focus Charm", exp = 1300,
                    npcName = "Summer Fairy",
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

            var plan = new System.Text.StringBuilder();

            foreach (var q in Quests)
            {
                if (db != null && !HasItem(db, q.rewardItemId)) missingItems.Add(q.rewardItemId);

                // Deliver dùng CUSTOM key (không phải enemyId) → không kiểm trong Data/Enemies.
                if (q.kind != QKind.Deliver)
                {
                    var ids = q.targetIds != null && q.targetIds.Length > 0 ? q.targetIds : new[] { q.targetId };
                    foreach (var id in ids) if (!EnemyIdExists(id)) missingEnemies.Add(id);
                }

                created.Add(BuildQuest(q));

                string what = q.kind == QKind.Deliver ? "deliver" : $"{q.kind} {q.targetId} x{q.amount}";
                plan.AppendLine($"  Map {q.map}  {what,-26} → {q.rewardItemId}"
                                + (CoopOnlyRewards.Contains(q.rewardItemId) ? "  [COOP-ONLY]" : ""));
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[AccessoryQuests] Đã tạo/cập nhật {created.Count} quest tại {QuestDir}.\n" +
                      "THỨ TỰ NHẬN ACCESSORY:\n" + plan +
                      "\nGán từng QuestSO vào field 'quest' của NPC tương ứng — Summer Fairy giao quest, " +
                      "Autumn Fairy nhận nộp (xem tool 'Setup Season Fairies').\n" +
                      "Quest ĐƯA TIN map 3: NPC cuối map phải gọi NetworkNPC.NotifyCustomObjective(\"deliver_m3_report\") " +
                      "khi nói chuyện xong — tool Season Fairies đã cấu hình sẵn.");

            if (missingItems.Count > 0)
                Debug.LogWarning("[AccessoryQuests] Accessory CHƯA có trong ItemDatabase (thưởng sẽ không vào túi): "
                                 + string.Join(", ", missingItems) + " → chạy 'Attrition/Setup Item System'.");

            if (missingEnemies.Count > 0)
                Debug.LogWarning("[AccessoryQuests] enemyId CHƯA tồn tại trong Data/Enemies (quest sẽ KHÔNG đếm "
                                 + "tiến độ): " + string.Join(", ", missingEnemies));

            Debug.LogWarning("[AccessoryQuests] LƯU Ý ELITE: enemySpawnConfigs của MỌI scene đang RỖNG và chỉ " +
                             "Map 1 có elite đặt tay (Cultist). Các quest elite map 2-5 (frogger/crab/nightborne/" +
                             "gollux) chỉ đếm được sau khi ĐẶT elite đó vào scene tương ứng.");
        }

        //  BUILD

        private static QuestSO BuildQuest(Q q)
        {
            string tag = q.kind == QKind.Boss ? "boss" : q.kind == QKind.Deliver ? "deliver" : "elite";
            string prettyTarget = q.targetIds != null && q.targetIds.Length > 0
                ? string.Join(" and ", q.targetIds)
                : q.targetId.Replace('_', ' ');
            bool coopOnly = CoopOnlyRewards.Contains(q.rewardItemId);

            // Câu mô tả mục tiêu — khác nhau theo loại nhiệm vụ.
            string ask = q.kind switch
            {
                QKind.Boss => $"Slay it, and I will give you the {q.rewardName}.",
                QKind.Deliver => $"Carry my words to the far end of this valley. Do that, and the {q.rewardName} is yours.",
                _ when q.targetIds != null && q.targetIds.Length > 0 =>
                    $"Defeat one {q.targetIds[0]} and one {q.targetIds[1]}. The {q.rewardName} will be yours.",
                _ => $"Slay {q.amount} {prettyTarget}, and the {q.rewardName} is yours.",
            };

            var offerLines = new List<DialogueLine> { Line(q.npcName, q.flavor), Line(q.npcName, ask) };

            // Món coop-only: nói rõ ngay lúc nhận để player solo không tưởng bị bug khi không được thưởng.
            if (coopOnly)
                offerLines.Add(Line(q.npcName,
                    "But a charm like this only binds when two souls walk together. Alone, you will earn nothing but the deed."));

            var offer = Dialogue($"Dlg_{q.questId}_Offer", offerLines.ToArray());

            string nag = q.kind switch
            {
                QKind.Boss => "It still stands. You are not finished.",
                QKind.Deliver => "My account has not been heard yet. Go — the far end of the valley waits.",
                _ => $"Not yet. The {prettyTarget} still draw breath.",
            };

            var prog = Dialogue($"Dlg_{q.questId}_InProgress", new[] { Line(q.npcName, nag) });

            var done = Dialogue($"Dlg_{q.questId}_Complete", new[]
            {
                Line(q.npcName, "It is done. You have earned this."),
                Line(q.npcName, $"Take the {q.rewardName}. Use it well."),
            });

            var fin = Dialogue($"Dlg_{q.questId}_Finished", new[]
            {
                Line(q.npcName, "The road ahead is darker still. Go carefully."),
            });

            string objective = q.kind switch
            {
                QKind.Boss => $"Defeat the boss ({prettyTarget}).",
                QKind.Deliver => "Deliver the report to the fairy at the far end of the valley.",
                _ when q.targetIds != null && q.targetIds.Length > 0 =>
                    $"Defeat one {q.targetIds[0]} and one {q.targetIds[1]}.",
                _ => $"Slay {q.amount} {prettyTarget}.",
            };

            var quest = ScriptableObject.CreateInstance<QuestSO>();
            quest.questId = q.questId;
            quest.title = q.title;
            quest.description = $"[Map {q.map}] {objective} Reward: {q.rewardName}"
                                + (coopOnly ? " (co-op only)." : ".");

            // Deliver = Custom: NPC nhận tin gọi NetworkNPC.NotifyCustomObjective(targetId) sau khi nói xong.
            quest.objectiveType = q.kind == QKind.Deliver
                ? QuestObjectiveType.Custom
                : QuestObjectiveType.Kill;
            quest.targetId = q.targetId;
            quest.requiredTargetIds = q.targetIds ?? new string[0];
            quest.requiredAmount = q.amount;
            quest.expReward = q.exp;
            quest.itemRewards = new[] { new QuestItemReward { itemId = q.rewardItemId, amount = 1 } };
            quest.dialogueNotStarted = offer;
            quest.dialogueInProgress = prog;
            quest.dialogueCompleted = done;
            quest.dialogueFinished = fin;

            return Save(quest, $"Quest_M{q.map}_{tag}_{q.rewardItemId}");
        }

        /// <summary>
        /// 5 accessory CHỈ nhận được khi chơi coop (khớp `coopOnlyReward = true` trong asset). Dùng ở đây
        /// chỉ để viết thoại/mô tả cho đúng — việc CHẶN thưởng thật nằm ở `NetworkNPC.DistributeRewards`.
        /// </summary>
        private static readonly HashSet<string> CoopOnlyRewards = new HashSet<string>
        {
            "acc_burn", "acc_shield", "acc_slow", "acc_lifesteal", "acc_potion",
        };

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
