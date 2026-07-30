#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Fusion;
using Attrition.Controllers;
using Attrition.Gameplay.Enemy;

namespace Attrition.Editor
{
    /// <summary>
    /// Dựng TOÀN BỘ phần "khung" cho moveset của boss 3 (Elf), 4 (DemonKin), 5 (ArchDemon):
    /// gắn NetworkObject + EnemyController + EnemyStats + EnemyAnimation + AI riêng vào prefab, nối các ô
    /// inject cho nhau, và BỔ SUNG THAM SỐ ANIMATOR còn thiếu.
    ///
    /// VÌ SAO CẦN TOOL NÀY (đã kiểm tra thực tế trong repo):
    ///  1. Ba prefab Elf/DemonKin/ArchDemon hiện CHỈ có SpriteRenderer + Animator + Rigidbody2D +
    ///     CapsuleCollider2D — không có NetworkObject/EnemyController nên chúng không thể chết, không chạy AI.
    ///  2. Animator của Elf và DemonKin có `m_AnimatorParameters: []` — KHÔNG có tham số nào. Mọi lệnh
    ///     `PlayAnim("Attack")` sẽ là no-op, boss "tung skill" mà đứng bất động. ArchDemon có tham số nhưng
    ///     tên CHỮ THƯỜNG ("attack") nên cũng không khớp quy ước "Attack" của boss 1/2.
    ///  3. Các ô inject (animationComp/controller/statsComp) là private [SerializeField] → phải set qua
    ///     SerializedObject, không gán được bằng tay nhanh và dễ sót.
    ///
    /// Tool KHÔNG gán prefab skill — việc đó do `BossSkillPrefabSetupEditor` (menu kế bên) lo, để hai việc
    /// độc lập: đổi art skill thì chạy lại tool kia, không cần dựng lại boss.
    ///
    /// Menu: Tools/Attrition/Enemy/Setup Boss Moveset (Elf + DemonKin + ArchDemon)
    /// Idempotent: chạy lại không thêm trùng component, không thêm trùng tham số animator.
    /// </summary>
    public static class BossMovesetSetupEditor
    {
        private const string PrefabDir = "Assets/_Project/Prefabs/Enemy";
        private const string StatsDir = "Assets/_Project/Data/Enemies";

        /// <summary>Tham số animator mà EnemyAnimation + AI boss cần. Thiếu cái nào thì boss câm cái đó.</summary>
        private static readonly (string name, AnimatorControllerParameterType type)[] RequiredParams =
        {
            ("Speed",       AnimatorControllerParameterType.Float),
            ("Attack",      AnimatorControllerParameterType.Trigger),
            ("AttackIndex", AnimatorControllerParameterType.Int),
            ("AttackSpeed", AnimatorControllerParameterType.Float),
            ("Skill",       AnimatorControllerParameterType.Trigger),
            ("SkillIndex",  AnimatorControllerParameterType.Int),
            ("Idle",        AnimatorControllerParameterType.Trigger),
            ("Hit",         AnimatorControllerParameterType.Trigger),
            ("IsDead",      AnimatorControllerParameterType.Bool),
            ("DieTrigger",  AnimatorControllerParameterType.Trigger),
            ("Resurrect",   AnimatorControllerParameterType.Trigger),
        };

        [MenuItem("Tools/Attrition/Enemy/Setup Boss Moveset (Elf + DemonKin + ArchDemon)")]
        public static void Setup()
        {
            int done = 0;
            done += SetupBoss<Attrition.Gameplay.Enemy.Elf.ElfBossAI>("Elf", "Elf_Stats") ? 1 : 0;
            done += SetupBoss<Attrition.Gameplay.Enemy.DemonKin.DemonKinBossAI>("DemonKin", "DemonKin_Stats") ? 1 : 0;
            done += SetupBoss<Attrition.Gameplay.Enemy.ArchDemon.ArchDemonBossAI>("ArchDemon", "ArchDemon_Stats") ? 1 : 0;

            int players = EnsurePlayerStatusEffects();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[BossMoveset] Xong {done}/3 boss; {players} prefab player có PlayerStatusEffects.\n" +
                      "BƯỚC TIẾP: chạy Tools/Attrition/Enemy/Setup Boss Skill Prefabs để sinh prefab skill.\n" +
                      "• Boss/player đặt sẵn trong scene: mở scene rồi SAVE để Fusion bake NetworkObject.\n" +
                      "• Gán 'boss' của BossEncounterTrigger + 'bossAI' của BossGateController = AI boss (ô nhận " +
                      "MonoBehaviour nên mọi boss đều kéo vào được).");
        }

        /// <summary>Dựng 1 boss. TAI là kiểu AI riêng của boss đó (ElfBossAI / DemonKinBossAI / ArchDemonBossAI).</summary>
        private static bool SetupBoss<TAI>(string prefabName, string statsName) where TAI : EnemyAI
        {
            string path = $"{PrefabDir}/{prefabName}.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            if (root == null)
            {
                Debug.LogError($"[BossMoveset] Không mở được prefab: {path}");
                return false;
            }

            try
            {
                // ─── Fusion: NetworkBehaviour BẮT BUỘC có NetworkObject ───
                if (root.GetComponent<NetworkObject>() == null) root.AddComponent<NetworkObject>();

                // ─── EnemyStats + stats SO ───
                var stats = root.GetComponent<EnemyStats>() ?? root.AddComponent<EnemyStats>();
                var statsSO = LoadOrCreateStats(prefabName, statsName);
                if (statsSO != null) SetRef(stats, "statsSO", statsSO);

                // ─── EnemyAnimation (cần cho FaceDirection/UpdateSpeed/Freeze) ───
                var anim = root.GetComponent<EnemyAnimation>() ?? root.AddComponent<EnemyAnimation>();
                var animator = root.GetComponentInChildren<Animator>();
                if (animator != null) SetRef(anim, "anim", animator);

                // ─── AI riêng của boss ───
                var ai = root.GetComponent<TAI>() ?? root.AddComponent<TAI>();

                // ─── EnemyController + nối các ô inject ───
                var ctrl = root.GetComponent<EnemyController>() ?? root.AddComponent<EnemyController>();
                SetRef(ctrl, "aiComp", ai);
                SetRef(ctrl, "animationComp", anim);
                SetRef(ctrl, "statsComp", stats);

                SetRef(ai, "animationComp", anim);
                SetRef(ai, "controller", ctrl);

                // ─── BossController: thanh máu + phase (boss nào cũng cần) ───
                if (root.GetComponent<BossController>() == null) root.AddComponent<BossController>();

                // ─── Animator: bổ sung tham số còn thiếu ───
                int added = EnsureAnimatorParams(animator);

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[BossMoveset] {prefabName}: NetworkObject + EnemyController + EnemyStats + " +
                          $"EnemyAnimation + {typeof(TAI).Name} + BossController; animator +{added} tham số.");
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// Thêm các tham số animator còn thiếu. Trả về số tham số ĐÃ THÊM.
        ///
        /// Chỉ THÊM, không tạo transition: state/clip do designer nối trong Animator window (tool tự nối
        /// transition sẽ ghi đè bố cục người ta đã dựng). Có tham số là đủ để `SetTrigger` không còn no-op;
        /// khi chưa nối transition thì boss vẫn chạy đúng logic skill, chỉ chưa đổi clip.
        /// </summary>
        private static int EnsureAnimatorParams(Animator animator)
        {
            if (animator == null) return 0;

            var ac = animator.runtimeAnimatorController as AnimatorController;
            if (ac == null)
            {
                Debug.LogWarning("[BossMoveset] Animator không dùng AnimatorController thường " +
                                 "(có thể là Override) → bỏ qua phần thêm tham số.");
                return 0;
            }

            var existing = new HashSet<string>();
            foreach (var p in ac.parameters) existing.Add(p.name);

            int added = 0;
            foreach (var (name, type) in RequiredParams)
            {
                if (existing.Contains(name)) continue;
                ac.AddParameter(name, type);
                added++;
            }

            if (added > 0) EditorUtility.SetDirty(ac);
            return added;
        }

        /// <summary>
        /// Gắn `PlayerStatusEffects` vào prefab player. Trả về số prefab đã có component.
        ///
        /// VÌ SAO NẰM TRONG TOOL BOSS: skill "đất bọc khống chế" (DemonKin) và "lốc nước làm chậm"
        /// (ArchDemon) áp hiệu ứng qua component này. Đã kiểm: cả `Player.prefab` và `Player 1.prefab` đều
        /// CHƯA có nó → hai skill kia sẽ chạy mà không có tác dụng gì, im lặng (AI đã null-check nên không
        /// crash, chỉ là không ăn). Gắn ở đây để một lần chạy tool là moveset hoạt động đủ.
        /// </summary>
        private static int EnsurePlayerStatusEffects()
        {
            string[] paths =
            {
                "Assets/_Project/Prefabs/Player/Player.prefab",
                "Assets/_Project/Prefabs/Player/Player 1.prefab",
            };

            int count = 0;
            foreach (var path in paths)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                if (root == null)
                {
                    Debug.LogWarning($"[BossMoveset] Không mở được prefab player: {path}");
                    continue;
                }

                try
                {
                    if (root.GetComponent<Attrition.Gameplay.Player.PlayerStatusEffects>() == null)
                    {
                        root.AddComponent<Attrition.Gameplay.Player.PlayerStatusEffects>();
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        Debug.Log($"[BossMoveset] {System.IO.Path.GetFileName(path)}: + PlayerStatusEffects " +
                                  "(slow/root từ skill boss).");
                    }
                    count++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            return count;
        }

        /// <summary>
        /// Nạp stats SO của boss; CHƯA CÓ thì tạo mới với chỉ số leo thang hợp tiến trình.
        ///
        /// Đã kiểm: repo có `Elf_Stats.asset` và `DemonKin_Stats.asset` nhưng THIẾU `ArchDemon_Stats.asset`.
        /// Không có stats thì `EnemyStats` rơi về fallback (maxHP 30) — boss cuối 30 máu, và tệ hơn:
        /// `EnemyId` trả null nên quest "giết boss" KHÔNG BAO GIỜ đếm được (NetworkNPC.NotifyEnemyKilled
        /// đọc id từ đây).
        ///
        /// Chỉ số bậc thang theo map: Elf (map 3) &lt; DemonKin (map 4) &lt; ArchDemon (map 5, boss cuối).
        /// Lấy Elf_Stats thật trong repo làm mốc (HP 2800 / AD 36 / AP 44 / poise 420).
        /// </summary>
        private static Attrition.Data.EnemyStatsSO LoadOrCreateStats(string prefabName, string statsName)
        {
            string path = $"{StatsDir}/{statsName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Attrition.Data.EnemyStatsSO>(path);
            if (existing != null) return existing;

            var so = ScriptableObject.CreateInstance<Attrition.Data.EnemyStatsSO>();
            so.enemyId = prefabName.ToLowerInvariant();
            so.tier = Attrition.Data.EnemyTier.Boss;

            // Chỉ boss chưa có asset mới rơi vào đây (hiện tại: ArchDemon). Đặt mạnh hơn DemonKin một bậc.
            so.maxHP = 4200;
            so.ad = 52;
            so.ap = 64;
            so.def = 30;
            so.res = 32;
            so.poise = 600;
            so.poiseRecoveryTime = 3f;
            so.patrolSpeed = 3f;
            so.chaseSpeed = 6.5f;
            so.attackSpeed = 1.3f;
            so.expReward = 260;
            so.coopHpMultiplier = 1.6f;
            so.coopDamageMultiplier = 1.2f;

            AssetDatabase.CreateAsset(so, path);
            Debug.Log($"[BossMoveset] Đã TẠO {statsName}.asset (enemyId='{so.enemyId}', HP {so.maxHP}) — " +
                      "chỉnh lại trong Inspector nếu muốn. Cần enemyId để quest 'giết boss' đếm được.");
            return so;
        }

        /// <summary>Set 1 field private [SerializeField] qua SerializedObject.</summary>
        private static void SetRef(Object target, string field, Object value)
        {
            if (target == null) return;
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogWarning($"[BossMoveset] {target.GetType().Name} không có field '{field}' — bỏ qua.");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
