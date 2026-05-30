using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public class EnemyAnimatorOverrideBuilder : EditorWindow
{
    // ═══════════════════════════════════════════════════════════════
    // Enum phân loại animation clip
    // ═══════════════════════════════════════════════════════════════
    private enum ClipCategory
    {
        Idle, Walk, Run, Fly,
        Attack1, Attack2, Attack3, Attack4, Attack5,
        Teleport,
        Hit, Dead, Corps, Reborn,
        Healing,
        Sleep, WakeUp,
        Jump, Fall, Land,
        Skill1, Skill2,
        Summon,
        Appear,
        Unknown
    }

    private class ClipEntry
    {
        public AnimationClip clip;
        public ClipCategory category;
        public bool include = true;
    }

    // ═══════════════════════════════════════════════════════════════
    // Fields
    // ═══════════════════════════════════════════════════════════════
    private string prefix = "MonsterName";
    private string targetFolder = "Assets/Animations/Enemies";
    private float runSpeedThreshold = 3f;
    private List<ClipEntry> detectedClips = new List<ClipEntry>();
    private Vector2 scrollPos;
    private bool clipsScanned = false;
    private bool showAdvanced = false;

    // Keyword → Category mapping
    private static readonly (string[] keywords, ClipCategory cat)[] CategoryRules =
    {
        (new[]{"attack_1","attack1"}, ClipCategory.Attack1),
        (new[]{"attack_2","attack2"}, ClipCategory.Attack2),
        (new[]{"attack_3","attack3"}, ClipCategory.Attack3),
        (new[]{"attack_4","attack4"}, ClipCategory.Attack4),
        (new[]{"attack_5","attack5"}, ClipCategory.Attack5),
        (new[]{"attack"}, ClipCategory.Attack1), // fallback nếu chỉ có "attack" không số
        (new[]{"teleport","dash","blink","warp"}, ClipCategory.Teleport),
        (new[]{"idle"}, ClipCategory.Idle),
        (new[]{"walk"}, ClipCategory.Walk),
        (new[]{"run"}, ClipCategory.Run),
        (new[]{"fly","flying"}, ClipCategory.Fly),
        (new[]{"hit","hurt","damage"}, ClipCategory.Hit),
        (new[]{"dead","death","die"}, ClipCategory.Dead),
        (new[]{"corps","corpse"}, ClipCategory.Corps),
        (new[]{"reborn","resurrect","revive","stand_up","standup"}, ClipCategory.Reborn),
        (new[]{"healing","heal","recovery"}, ClipCategory.Healing),
        (new[]{"sleep","sleeping"}, ClipCategory.Sleep),
        (new[]{"wakeup","wake_up","wake","awake"}, ClipCategory.WakeUp),
        (new[]{"jump"}, ClipCategory.Jump),
        (new[]{"fall","fallback"}, ClipCategory.Fall),
        (new[]{"land","landing"}, ClipCategory.Land),
        (new[]{"skill_1","skill1"}, ClipCategory.Skill1),
        (new[]{"skill_2","skill2"}, ClipCategory.Skill2),
        (new[]{"summon","summoning"}, ClipCategory.Summon),
        (new[]{"appear","spawn","emerge"}, ClipCategory.Appear),
    };

    [MenuItem("Tools/Enemy Animator Builder (Pro) %#e")]
    public static void ShowWindow()
    {
        var w = GetWindow<EnemyAnimatorOverrideBuilder>("⚔ Enemy Animator Builder");
        w.minSize = new Vector2(480, 600);
    }

    // ═══════════════════════════════════════════════════════════════
    // GUI
    // ═══════════════════════════════════════════════════════════════
    private void OnGUI()
    {
        // Header
        EditorGUILayout.Space(5);
        var headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
        GUILayout.Label("⚔ Enemy Animator Builder (Pro)", headerStyle);
        EditorGUILayout.Space(3);

        EditorGUILayout.HelpBox(
            "Công cụ tự động tạo Animator Controller hoàn chỉnh cho Enemy.\n" +
            "• Tự động scan & phân loại Animation Clips theo tên\n" +
            "• Hỗ trợ số lượng Attack tùy ý (1~5 đòn)\n" +
            "• Hỗ trợ Teleport, Walk/Run/Fly, Jump/Fall/Land, Corps/Reborn\n" +
            "• Tự động tạo Parameters, States, Transitions",
            MessageType.Info);

        EditorGUILayout.Space(5);

        // ── Cấu hình ──
        prefix = EditorGUILayout.TextField("Monster Prefix", prefix);

        GUILayout.BeginHorizontal();
        targetFolder = EditorGUILayout.TextField("Thư mục Animation", targetFolder);
        if (GUILayout.Button("Browse", GUILayout.Width(60)))
        {
            string path = EditorUtility.OpenFolderPanel("Chọn thư mục chứa clips", "Assets", "");
            if (!string.IsNullOrEmpty(path) && path.StartsWith(Application.dataPath))
                targetFolder = "Assets" + path.Substring(Application.dataPath.Length);
        }
        GUILayout.EndHorizontal();

        showAdvanced = EditorGUILayout.Foldout(showAdvanced, "Cấu hình nâng cao");
        if (showAdvanced)
        {
            EditorGUI.indentLevel++;
            runSpeedThreshold = EditorGUILayout.FloatField("Run Speed Threshold", runSpeedThreshold);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(5);

        // ── Nút Scan ──
        GUI.backgroundColor = new Color(0.3f, 0.8f, 1f);
        if (GUILayout.Button("🔍  Scan Animation Clips", GUILayout.Height(28)))
        {
            ScanClips();
        }
        GUI.backgroundColor = Color.white;

        // ── Danh sách clips ──
        if (clipsScanned)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Clips tìm được:", EditorStyles.boldLabel);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.MinHeight(200));

            if (detectedClips.Count == 0)
            {
                EditorGUILayout.HelpBox($"Không tìm thấy clip nào với prefix \"{prefix}\" trong thư mục.", MessageType.Warning);
            }
            else
            {
                // Header
                GUILayout.BeginHorizontal(EditorStyles.toolbar);
                GUILayout.Label("✓", GUILayout.Width(25));
                GUILayout.Label("Clip Name", GUILayout.MinWidth(180));
                GUILayout.Label("Phân loại", GUILayout.Width(120));
                GUILayout.EndHorizontal();

                for (int i = 0; i < detectedClips.Count; i++)
                {
                    var entry = detectedClips[i];
                    var bgColor = entry.include ? (i % 2 == 0 ? new Color(0.25f, 0.25f, 0.25f) : new Color(0.28f, 0.28f, 0.28f)) : new Color(0.4f, 0.2f, 0.2f);
                    GUI.backgroundColor = bgColor;

                    GUILayout.BeginHorizontal("box");
                    entry.include = EditorGUILayout.Toggle(entry.include, GUILayout.Width(25));
                    EditorGUILayout.ObjectField(entry.clip, typeof(AnimationClip), false, GUILayout.MinWidth(180));
                    entry.category = (ClipCategory)EditorGUILayout.EnumPopup(entry.category, GUILayout.Width(120));
                    GUILayout.EndHorizontal();
                }
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.EndScrollView();

            // Summary
            var included = detectedClips.Where(c => c.include).ToList();
            int attackCount = included.Count(c => c.category >= ClipCategory.Attack1 && c.category <= ClipCategory.Attack5);
            EditorGUILayout.HelpBox(
                $"Sẽ tạo: {included.Count} states | {attackCount} đòn Attack | " +
                $"{(included.Any(c => c.category == ClipCategory.Teleport) ? "Có Teleport" : "Không Teleport")} | " +
                $"{(included.Any(c => c.category == ClipCategory.Healing) ? "Có Healing" : "Không Healing")} | " +
                $"{(included.Any(c => c.category == ClipCategory.Sleep) ? "Có Sleep" : "Không Sleep")} | " +
                $"{(included.Any(c => c.category == ClipCategory.Run) ? "Có Run" : "Không Run")} | " +
                $"{(included.Any(c => c.category == ClipCategory.Fly) ? "Có Fly" : "Không Fly")} | " +
                $"{(included.Any(c => c.category == ClipCategory.Reborn) ? "Có Reborn" : "Không Reborn")} | " +
                $"{(included.Any(c => c.category == ClipCategory.Skill1 || c.category == ClipCategory.Skill2) ? "Có Skill" : "Không Skill")} | " +
                $"{(included.Any(c => c.category == ClipCategory.Summon) ? "Có Summon" : "Không Summon")} | " +
                $"{(included.Any(c => c.category == ClipCategory.Appear) ? "Có Appear" : "Không Appear")}",
                MessageType.None);

            EditorGUILayout.Space(5);

            // ── Nút Build ──
            GUI.backgroundColor = new Color(0.2f, 0.9f, 0.3f);
            if (GUILayout.Button("⚡  Tạo Animator Controller", GUILayout.Height(35)))
            {
                BuildAnimatorController();
            }
            GUI.backgroundColor = Color.white;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // SCAN: Tìm và phân loại clips
    // ═══════════════════════════════════════════════════════════════
    private void ScanClips()
    {
        detectedClips.Clear();
        clipsScanned = true;

        if (string.IsNullOrEmpty(targetFolder) || !AssetDatabase.IsValidFolder(targetFolder))
        {
            EditorUtility.DisplayDialog("Lỗi", "Thư mục không hợp lệ!", "OK");
            return;
        }

        // Tìm tất cả assets trong folder (bao gồm subfolder)
        // SỬA: Dùng "t:Object" thay vì "t:AnimationClip" để tìm được cả clip nằm
        // bên trong file .ase/.aseprite/.png (sub-asset) mà FindAssets("t:AnimationClip") bỏ qua
        string[] guids = AssetDatabase.FindAssets("", new[] { targetFolder });
        string prefixLower = prefix.ToLower();
        HashSet<int> addedClipIds = new HashSet<int>(); // Tránh trùng lặp

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            // Bỏ qua thư mục
            if (AssetDatabase.IsValidFolder(path)) continue;

            // Load tất cả assets tại path (bao gồm sub-assets)
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (Object asset in assets)
            {
                AnimationClip clip = asset as AnimationClip;
                if (clip == null || clip.name.StartsWith("__preview__")) continue;

                // Tránh thêm trùng clip (cùng clip có thể xuất hiện nhiều lần)
                int clipId = clip.GetInstanceID();
                if (addedClipIds.Contains(clipId)) continue;

                string nameLower = clip.name.ToLower();
                // Chỉ lấy clip có chứa prefix
                if (!nameLower.Contains(prefixLower)) continue;

                ClipEntry entry = new ClipEntry
                {
                    clip = clip,
                    category = DetectCategory(nameLower, prefixLower),
                    include = true
                };
                detectedClips.Add(entry);
                addedClipIds.Add(clipId);
            }
        }

        // Sắp xếp theo category
        detectedClips.Sort((a, b) => a.category.CompareTo(b.category));
        Debug.Log($"[AnimatorBuilder] Scan xong: tìm được {detectedClips.Count} clips cho prefix \"{prefix}\"");
    }

    private ClipCategory DetectCategory(string clipNameLower, string prefixLower)
    {
        // Bỏ prefix để lấy phần suffix (vd: "imp_axe_demon_attack1" → "attack1")
        string suffix = clipNameLower;
        int prefixIdx = suffix.IndexOf(prefixLower);
        if (prefixIdx >= 0)
        {
            suffix = suffix.Substring(prefixIdx + prefixLower.Length).TrimStart('_', ' ');
        }

        // Ưu tiên match dài hơn trước (attack_1 trước attack)
        foreach (var rule in CategoryRules)
        {
            foreach (string kw in rule.keywords)
            {
                if (suffix.Contains(kw) || clipNameLower.EndsWith(kw))
                    return rule.cat;
            }
        }
        return ClipCategory.Unknown;
    }

    // ═══════════════════════════════════════════════════════════════
    // BUILD: Tạo Animator Controller hoàn chỉnh
    // ═══════════════════════════════════════════════════════════════
    private void BuildAnimatorController()
    {
        var included = detectedClips.Where(c => c.include).ToList();
        if (included.Count == 0)
        {
            EditorUtility.DisplayDialog("Lỗi", "Không có clip nào được chọn!", "OK");
            return;
        }

        // Tạo controller
        string savePath = $"{targetFolder}/{prefix}_Animator.controller";
        savePath = AssetDatabase.GenerateUniqueAssetPath(savePath);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(savePath);
        AnimatorStateMachine rootSM = controller.layers[0].stateMachine;

        // ── Thêm Parameters ──
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("AttackIndex", AnimatorControllerParameterType.Int);
        controller.AddParameter("AttackSpeed", AnimatorControllerParameterType.Float);
        SetParameterDefault(controller, "AttackSpeed", 1f);
        controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("IsDead", AnimatorControllerParameterType.Bool);
        controller.AddParameter("DieTrigger", AnimatorControllerParameterType.Trigger);

        bool hasReborn = included.Any(c => c.category == ClipCategory.Reborn);
        bool hasJump = included.Any(c => c.category == ClipCategory.Jump);
        bool hasRun = included.Any(c => c.category == ClipCategory.Run);
        bool hasTeleport = included.Any(c => c.category == ClipCategory.Teleport);
        bool hasHealing = included.Any(c => c.category == ClipCategory.Healing);
        bool hasSleep = included.Any(c => c.category == ClipCategory.Sleep);

        if (hasReborn) controller.AddParameter("Resurrect", AnimatorControllerParameterType.Trigger);
        if (hasJump) controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
        if (hasTeleport) controller.AddParameter("Teleport", AnimatorControllerParameterType.Trigger);
        if (hasHealing)
        {
            controller.AddParameter("Heal", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("IsHealing", AnimatorControllerParameterType.Bool);
        }
        if (hasSleep)
        {
            controller.AddParameter("Sleep", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("WakeUp", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("IsSleeping", AnimatorControllerParameterType.Bool);
        }

        // Skill parameters
        bool hasSkill = included.Any(c => c.category == ClipCategory.Skill1 || c.category == ClipCategory.Skill2);
        if (hasSkill)
        {
            controller.AddParameter("Skill", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("SkillIndex", AnimatorControllerParameterType.Int);
        }

        // Summon parameter
        bool hasSummon = included.Any(c => c.category == ClipCategory.Summon);
        if (hasSummon)
        {
            controller.AddParameter("Summon", AnimatorControllerParameterType.Trigger);
        }

        // Appear parameter
        bool hasAppear = included.Any(c => c.category == ClipCategory.Appear);
        if (hasAppear)
        {
            controller.AddParameter("Appear", AnimatorControllerParameterType.Trigger);
        }

        // ── Tạo States ──
        Dictionary<ClipCategory, AnimatorState> states = new Dictionary<ClipCategory, AnimatorState>();

        // Layout vị trí cho đẹp
        Dictionary<ClipCategory, Vector3> positions = new Dictionary<ClipCategory, Vector3>
        {
            { ClipCategory.Idle,    new Vector3(300, 60, 0) },
            { ClipCategory.Walk,    new Vector3(300, -70, 0) },
            { ClipCategory.Run,     new Vector3(610, -70, 0) },
            { ClipCategory.Fly,     new Vector3(300, -70, 0) },
            { ClipCategory.Attack1, new Vector3(40, 200, 0) },
            { ClipCategory.Attack2, new Vector3(40, 310, 0) },
            { ClipCategory.Attack3, new Vector3(40, 420, 0) },
            { ClipCategory.Attack4, new Vector3(40, 530, 0) },
            { ClipCategory.Attack5, new Vector3(40, 640, 0) },
            { ClipCategory.Teleport, new Vector3(300, 310, 0) },
            { ClipCategory.Hit,     new Vector3(560, 200, 0) },
            { ClipCategory.Dead,    new Vector3(830, 230, 0) },
            { ClipCategory.Corps,   new Vector3(830, 60, 0) },
            { ClipCategory.Reborn,  new Vector3(570, 60, 0) },
            { ClipCategory.Healing, new Vector3(570, 310, 0) },
            { ClipCategory.Sleep,   new Vector3(570, 420, 0) },
            { ClipCategory.WakeUp,  new Vector3(570, 530, 0) },
            { ClipCategory.Jump,    new Vector3(820, -70, 0) },
            { ClipCategory.Fall,    new Vector3(820, 60, 0) },
            { ClipCategory.Land,    new Vector3(820, 170, 0) },
            { ClipCategory.Skill1,  new Vector3(300, 420, 0) },
            { ClipCategory.Skill2,  new Vector3(300, 530, 0) },
            { ClipCategory.Summon,  new Vector3(300, 640, 0) },
            { ClipCategory.Appear,  new Vector3(300, 750, 0) },
        };

        foreach (var entry in included)
        {
            if (entry.category == ClipCategory.Unknown) continue;

            Vector3 pos = positions.ContainsKey(entry.category) ? positions[entry.category] : new Vector3(500, 400, 0);
            AnimatorState state = rootSM.AddState(entry.clip.name, pos);
            state.motion = entry.clip;

            // Attack states dùng AttackSpeed parameter
            if (entry.category >= ClipCategory.Attack1 && entry.category <= ClipCategory.Attack5)
            {
                state.speedParameterActive = true;
                state.speedParameter = "AttackSpeed";
            }

            states[entry.category] = state;
        }

        // ── Set default state = Idle ──
        if (states.ContainsKey(ClipCategory.Idle))
            rootSM.defaultState = states[ClipCategory.Idle];

        // ── Tạo Transitions ──
        AnimatorState idleState = states.ContainsKey(ClipCategory.Idle) ? states[ClipCategory.Idle] : null;
        AnimatorState walkState = states.ContainsKey(ClipCategory.Walk) ? states[ClipCategory.Walk] : null;
        AnimatorState flyState = states.ContainsKey(ClipCategory.Fly) ? states[ClipCategory.Fly] : null;
        AnimatorState runState = states.ContainsKey(ClipCategory.Run) ? states[ClipCategory.Run] : null;
        AnimatorState hitState = states.ContainsKey(ClipCategory.Hit) ? states[ClipCategory.Hit] : null;
        AnimatorState deadState = states.ContainsKey(ClipCategory.Dead) ? states[ClipCategory.Dead] : null;
        AnimatorState corpsState = states.ContainsKey(ClipCategory.Corps) ? states[ClipCategory.Corps] : null;
        AnimatorState rebornState = states.ContainsKey(ClipCategory.Reborn) ? states[ClipCategory.Reborn] : null;
        AnimatorState jumpState = states.ContainsKey(ClipCategory.Jump) ? states[ClipCategory.Jump] : null;

        // Locomotion state (ưu tiên: walk > fly)
        AnimatorState locoState = walkState ?? flyState;

        // ─── Idle ↔ Walk/Fly ───
        if (idleState != null && locoState != null)
        {
            // Idle → Walk: Speed > 0.1
            var t1 = idleState.AddTransition(locoState);
            t1.hasExitTime = false;
            t1.duration = 0;
            t1.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

            // Walk → Idle: Speed < 0.1
            var t2 = locoState.AddTransition(idleState);
            t2.hasExitTime = false;
            t2.duration = 0;
            t2.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
        }

        // ─── Walk ↔ Run ───
        if (walkState != null && runState != null)
        {
            var t1 = walkState.AddTransition(runState);
            t1.hasExitTime = false;
            t1.duration = 0;
            t1.AddCondition(AnimatorConditionMode.Greater, runSpeedThreshold, "Speed");

            var t2 = runState.AddTransition(walkState);
            t2.hasExitTime = false;
            t2.duration = 0;
            t2.AddCondition(AnimatorConditionMode.Less, runSpeedThreshold, "Speed");
        }

        // ─── Attacks (AnyState) ───
        ClipCategory[] attackCats = { ClipCategory.Attack1, ClipCategory.Attack2, ClipCategory.Attack3, ClipCategory.Attack4, ClipCategory.Attack5 };
        for (int i = 0; i < attackCats.Length; i++)
        {
            if (!states.ContainsKey(attackCats[i])) continue;
            AnimatorState atkState = states[attackCats[i]];

            // AnyState → Attack: Attack trigger + AttackIndex == i
            var t = rootSM.AddAnyStateTransition(atkState);
            t.hasExitTime = false;
            t.duration = 0;
            t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            t.AddCondition(AnimatorConditionMode.Equals, i, "AttackIndex");

            // Attack → Idle: ExitTime = 1
            if (idleState != null)
            {
                var tBack = atkState.AddTransition(idleState);
                tBack.hasExitTime = true;
                tBack.exitTime = 1f;
                tBack.duration = 0.1f;
            }
        }

        // ─── Hit (AnyState) ───
        if (hitState != null)
        {
            var t = rootSM.AddAnyStateTransition(hitState);
            t.hasExitTime = false;
            t.duration = 0;
            t.canTransitionToSelf = true;
            t.AddCondition(AnimatorConditionMode.If, 0, "Hit");

            if (idleState != null)
            {
                var tBack = hitState.AddTransition(idleState);
                tBack.hasExitTime = true;
                tBack.exitTime = 1f;
                tBack.duration = 0.1f;
            }
        }

        // ─── Dead (AnyState → Dead via DieTrigger) ───
        if (deadState != null)
        {
            var t = rootSM.AddAnyStateTransition(deadState);
            t.hasExitTime = false;
            t.duration = 0;
            t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.If, 0, "DieTrigger");

            // Dead → Corps (nếu có)
            if (corpsState != null)
            {
                var tCorps = deadState.AddTransition(corpsState);
                tCorps.hasExitTime = true;
                tCorps.exitTime = 1f;
                tCorps.duration = 0.25f;

                // Corps → Reborn (nếu có)
                if (rebornState != null)
                {
                    var tReborn = corpsState.AddTransition(rebornState);
                    tReborn.hasExitTime = false;
                    tReborn.duration = 0.25f;
                    tReborn.AddCondition(AnimatorConditionMode.If, 0, "Resurrect");

                    // Reborn → Idle
                    if (idleState != null)
                    {
                        var tIdle = rebornState.AddTransition(idleState);
                        tIdle.hasExitTime = true;
                        tIdle.exitTime = 1f;
                        tIdle.duration = 0.25f;
                    }
                }
            }
            else if (rebornState != null)
            {
                // Dead không có corps, nhưng có reborn
                // Dead giữ nguyên (không transition), đợi Resurrect trigger mới chuyển
                // Không dùng AnyState để tránh loop, dùng transition từ dead
                // Thêm kiểu: nếu không có corps thì dead frame cuối giữ nguyên
                // Chờ Resurrect
            }
        }

        // ─── Teleport (AnyState) ───
        if (states.ContainsKey(ClipCategory.Teleport))
        {
            AnimatorState teleportState = states[ClipCategory.Teleport];

            var tTele = rootSM.AddAnyStateTransition(teleportState);
            tTele.hasExitTime = false;
            tTele.duration = 0;
            tTele.canTransitionToSelf = false;
            tTele.AddCondition(AnimatorConditionMode.If, 0, "Teleport");

            // Teleport → Idle: ExitTime = 1
            if (idleState != null)
            {
                var tBack = teleportState.AddTransition(idleState);
                tBack.hasExitTime = true;
                tBack.exitTime = 1f;
                tBack.duration = 0.1f;
            }
        }

        // ─── Healing (AnyState → Healing via Heal trigger) ───
        if (states.ContainsKey(ClipCategory.Healing))
        {
            AnimatorState healingState = states[ClipCategory.Healing];

            var tHeal = rootSM.AddAnyStateTransition(healingState);
            tHeal.hasExitTime = false;
            tHeal.duration = 0;
            tHeal.canTransitionToSelf = false;
            tHeal.AddCondition(AnimatorConditionMode.If, 0, "Heal");

            // Healing → Idle: khi IsHealing = false
            if (idleState != null)
            {
                var tBack = healingState.AddTransition(idleState);
                tBack.hasExitTime = false;
                tBack.duration = 0.1f;
                tBack.AddCondition(AnimatorConditionMode.IfNot, 0, "IsHealing");
            }
        }

        // ─── Sleep / WakeUp ───
        if (states.ContainsKey(ClipCategory.Sleep))
        {
            AnimatorState sleepState = states[ClipCategory.Sleep];

            // AnyState → Sleep: Sleep trigger
            var tSleep = rootSM.AddAnyStateTransition(sleepState);
            tSleep.hasExitTime = false;
            tSleep.duration = 0;
            tSleep.canTransitionToSelf = false;
            tSleep.AddCondition(AnimatorConditionMode.If, 0, "Sleep");

            // Sleep giữ nguyên (loop) khi IsSleeping = true
            // Sleep → WakeUp (hoặc Idle) khi IsSleeping = false
            if (states.ContainsKey(ClipCategory.WakeUp))
            {
                AnimatorState wakeUpState = states[ClipCategory.WakeUp];

                // AnyState → WakeUp: WakeUp trigger
                var tWake = rootSM.AddAnyStateTransition(wakeUpState);
                tWake.hasExitTime = false;
                tWake.duration = 0;
                tWake.canTransitionToSelf = false;
                tWake.AddCondition(AnimatorConditionMode.If, 0, "WakeUp");

                // WakeUp → Idle: ExitTime = 1
                if (idleState != null)
                {
                    var tBack = wakeUpState.AddTransition(idleState);
                    tBack.hasExitTime = true;
                    tBack.exitTime = 1f;
                    tBack.duration = 0.1f;
                }
            }
            else if (idleState != null)
            {
                // Không có WakeUp clip → Sleep → Idle khi IsSleeping = false
                var tBack = sleepState.AddTransition(idleState);
                tBack.hasExitTime = false;
                tBack.duration = 0.1f;
                tBack.AddCondition(AnimatorConditionMode.IfNot, 0, "IsSleeping");
            }
        }

        // ─── Jump (AnyState) ───
        if (jumpState != null)
        {
            var t = rootSM.AddAnyStateTransition(jumpState);
            t.hasExitTime = false;
            t.duration = 0.1f;
            t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.If, 0, "Jump");

            // Jump → Idle hoặc Fall
            AnimatorState jumpTarget = states.ContainsKey(ClipCategory.Fall) ? states[ClipCategory.Fall] : idleState;
            if (jumpTarget != null)
            {
                var tBack = jumpState.AddTransition(jumpTarget);
                tBack.hasExitTime = true;
                tBack.exitTime = 1f;
                tBack.duration = 0.1f;
            }
        }

        // ─── Fall → Land ───
        if (states.ContainsKey(ClipCategory.Fall) && states.ContainsKey(ClipCategory.Land))
        {
            var t = states[ClipCategory.Fall].AddTransition(states[ClipCategory.Land]);
            t.hasExitTime = true;
            t.exitTime = 1f;
            t.duration = 0.1f;

            if (idleState != null)
            {
                var t2 = states[ClipCategory.Land].AddTransition(idleState);
                t2.hasExitTime = true;
                t2.exitTime = 1f;
                t2.duration = 0.1f;
            }
        }

        // ─── Skill1, Skill2 (AnyState → Skill via Skill trigger + SkillIndex) ───
        ClipCategory[] skillCats = { ClipCategory.Skill1, ClipCategory.Skill2 };
        for (int i = 0; i < skillCats.Length; i++)
        {
            if (!states.ContainsKey(skillCats[i])) continue;
            AnimatorState skillState = states[skillCats[i]];

            var tSkill = rootSM.AddAnyStateTransition(skillState);
            tSkill.hasExitTime = false;
            tSkill.duration = 0;
            tSkill.canTransitionToSelf = false;
            tSkill.AddCondition(AnimatorConditionMode.If, 0, "Skill");
            tSkill.AddCondition(AnimatorConditionMode.Equals, i, "SkillIndex");

            // Skill → Idle: ExitTime = 1
            if (idleState != null)
            {
                var tBack = skillState.AddTransition(idleState);
                tBack.hasExitTime = true;
                tBack.exitTime = 1f;
                tBack.duration = 0.1f;
            }
        }

        // ─── Summon (AnyState → Summon via Summon trigger) ───
        if (states.ContainsKey(ClipCategory.Summon))
        {
            AnimatorState summonState = states[ClipCategory.Summon];

            var tSummon = rootSM.AddAnyStateTransition(summonState);
            tSummon.hasExitTime = false;
            tSummon.duration = 0;
            tSummon.canTransitionToSelf = false;
            tSummon.AddCondition(AnimatorConditionMode.If, 0, "Summon");

            if (idleState != null)
            {
                var tBack = summonState.AddTransition(idleState);
                tBack.hasExitTime = true;
                tBack.exitTime = 1f;
                tBack.duration = 0.1f;
            }
        }

        // ─── Appear (AnyState → Appear via Appear trigger) ───
        if (states.ContainsKey(ClipCategory.Appear))
        {
            AnimatorState appearState = states[ClipCategory.Appear];

            var tAppear = rootSM.AddAnyStateTransition(appearState);
            tAppear.hasExitTime = false;
            tAppear.duration = 0;
            tAppear.canTransitionToSelf = false;
            tAppear.AddCondition(AnimatorConditionMode.If, 0, "Appear");

            if (idleState != null)
            {
                var tBack = appearState.AddTransition(idleState);
                tBack.hasExitTime = true;
                tBack.exitTime = 1f;
                tBack.duration = 0.1f;
            }
        }

        // ── Save ──
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Selection.activeObject = controller;
        EditorGUIUtility.PingObject(controller);

        // ── Log kết quả ──
        int atkCount = included.Count(c => c.category >= ClipCategory.Attack1 && c.category <= ClipCategory.Attack5);
        int skillCount = included.Count(c => c.category == ClipCategory.Skill1 || c.category == ClipCategory.Skill2);
        AnimatorState teleportStateLog = states.ContainsKey(ClipCategory.Teleport) ? states[ClipCategory.Teleport] : null;
        Debug.Log($"[AnimatorBuilder] ✅ Tạo thành công: {savePath}\n" +
                  $"  States: {states.Count} | Attacks: {atkCount} | Skills: {skillCount} | " +
                  $"Teleport: {(teleportStateLog != null ? "✓" : "✗")} | " +
                  $"Summon: {(states.ContainsKey(ClipCategory.Summon) ? "✓" : "✗")} | " +
                  $"Appear: {(states.ContainsKey(ClipCategory.Appear) ? "✓" : "✗")} | " +
                  $"Run: {(runState != null ? "✓" : "✗")} | " +
                  $"Fly: {(flyState != null ? "✓" : "✗")} | " +
                  $"Reborn: {(rebornState != null ? "✓" : "✗")} | " +
                  $"Jump: {(jumpState != null ? "✓" : "✗")} | " +
                  $"Sleep: {(states.ContainsKey(ClipCategory.Sleep) ? "✓" : "✗")}");

        EditorUtility.DisplayDialog("Thành công!",
            $"Đã tạo Animator Controller tại:\n{savePath}\n\n" +
            $"States: {states.Count} | Attacks: {atkCount} | Skills: {skillCount}\n" +
            $"Summon: {(states.ContainsKey(ClipCategory.Summon) ? "✓" : "✗")} | " +
            $"Appear: {(states.ContainsKey(ClipCategory.Appear) ? "✓" : "✗")}\n" +
            $"Hãy gán controller này vào Animator component của Enemy.",
            "OK");
    }

    private void SetParameterDefault(AnimatorController controller, string name, float value)
    {
        var parameters = controller.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == name)
            {
                parameters[i].defaultFloat = value;
                controller.parameters = parameters;
                return;
            }
        }
    }
}
