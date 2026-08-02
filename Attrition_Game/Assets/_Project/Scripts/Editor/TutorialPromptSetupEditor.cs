using UnityEditor;
using UnityEngine;
using Attrition.Data;
using Attrition.Gameplay.Environment;

namespace Attrition.Editor
{
    /// <summary>
    /// Tool tạo nhanh bảng hướng dẫn tân thủ (TutorialPrompt). KHÔNG cần NetworkObject — hướng dẫn là
    /// UI cục bộ mỗi player. Hai menu:
    ///  - Map 1 (di chuyển/đánh cơ bản): vùng OnEnterZone đặt ở đầu map.
    ///  - Map 2 (double jump + shadow dash): OnAbilityUnlocked, tự hiện khi local player đủ ability.
    /// Sau khi chạy: đặt vị trí (Map 1: đầu map; Map 2: đâu cũng được vì kích hoạt theo ability), chỉnh
    /// nội dung dòng nếu muốn.
    /// </summary>
    public static class TutorialPromptSetupEditor
    {
        [MenuItem("Tools/Attrition/Create Tutorial - Map 1 Basics")]
        public static void CreateMap1()
        {
            var go = NewPrompt("Tutorial_Map1_Basics");
            var t = go.AddComponent<TutorialPrompt>();
            SetStr(t, "tutorialId", "map1_basics");
            SetEnum(t, "trigger", (int)TutorialPrompt.TriggerMode.OnEnterZone);
            SetStr(t, "title", "BASIC CONTROLS");
            // stepByStep: hiện LẦN LƯỢT — WASD trước, xong mới tới các phím sau.
            SetBool(t, "stepByStep", true);
            SetLines(t, new[]
            {
                ("W A S D", "Di chuyển"),
                ("Space", "Nhảy"),
                ("Shift", "Lướt (Dash)"),
                ("J", "Tấn công"),
                ("F", "Tương tác / Nghỉ tại checkpoint"),
                ("Tab", "Túi đồ"),
            });
            SetStr(t, "creditsTitle", "ATTRITION — SEP490");
            SetStrArray(t, "creditsMembers", new[]
            {
                "Nguyễn Thiện Nhơn",
                "Lê Trung Hậu",
                "Nguyễn Nhật Đăng",
                "Trần Thiên Đăng",
                "Phan Phúc Bình",
            });
            Finish(go, "Đặt vùng này ở ĐẦU Map 1. Player local đi vào → hiện hướng dẫn 1 lần/lượt chơi. " +
                       "Credits team hiện góc phải-dưới NGAY SAU khi đóng bảng — sửa creditsMembers thành tên thật.");
        }

        [MenuItem("Tools/Attrition/Update Tutorial - Map 1 Intro Sequence")]
        public static void UpdateMap1Intro()
        {
            var go = GameObject.Find("Tutorial_Map1_Basics");
            var t = go != null ? go.GetComponent<TutorialPrompt>() : null;
            if (t == null)
            {
                Debug.LogError("[Attrition] Không thấy Tutorial_Map1_Basics trong scene Map 1.");
                return;
            }

            SetBool(t, "stepByStep", true);
            SetStr(t, "creditsTitle", "ATTRITION — SEP490");
            SetStrArray(t, "creditsMembers", new[]
            {
                "Nguyễn Thiện Nhơn",
                "Lê Trung Hậu",
                "Nguyễn Nhật Đăng",
                "Trần Thiên Đăng",
                "Phan Phúc Bình",
            });

            EditorUtility.SetDirty(t);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(t.gameObject.scene);
            Selection.activeGameObject = go;
            Debug.Log("[Attrition] Đã cập nhật intro Map 1: tutorial từng bước, sau đó 5 tên thành viên lần lượt.");
        }

        [MenuItem("Tools/Attrition/Create Tutorial - Map 2 Abilities")]
        public static void CreateMap2()
        {
            var go = NewPrompt("Tutorial_Map2_Abilities");
            var t = go.AddComponent<TutorialPrompt>();
            SetStr(t, "tutorialId", "map2_abilities");
            SetEnum(t, "trigger", (int)TutorialPrompt.TriggerMode.OnAbilityUnlocked);
            SetStr(t, "title", "KỸ NĂNG MỚI");
            SetAbilities(t, new[] { GrantedAbility.DoubleJump, GrantedAbility.ShadowDash });
            SetBool(t, "stepByStep", true);
            SetLines(t, new[]
            {
                ("Space, Space", "Nhảy đúp (Double Jump)"),
                ("Shift", "Shadow Dash — lướt xuyên đòn, có thời gian hồi"),
            });
            Finish(go, "OnAbilityUnlocked: tự hiện khi local player sở hữu ĐỦ Double Jump + Shadow Dash. " +
                       "Đặt đâu trong Map 2 cũng được (không cần vào vùng).");
        }

        private static GameObject NewPrompt(string name)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create Tutorial Prompt");
            var sv = SceneView.lastActiveSceneView;
            if (sv != null) go.transform.position = sv.pivot;
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(3f, 4f);
            col.isTrigger = true;
            return go;
        }

        private static void Finish(GameObject go, string msg)
        {
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
            Debug.Log("[Attrition] Đã tạo Tutorial Prompt. " + msg);
        }

        private static void SetStr(Object target, string field, string value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p != null) { p.stringValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        }

        private static void SetEnum(Object target, string field, int value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p != null) { p.enumValueIndex = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        }

        private static void SetBool(Object target, string field, bool value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p != null) { p.boolValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        }

        private static void SetLines(Object target, (string key, string desc)[] lines)
        {
            var so = new SerializedObject(target);
            var arr = so.FindProperty("lines");
            if (arr == null) return;
            arr.arraySize = lines.Length;
            for (int i = 0; i < lines.Length; i++)
            {
                var el = arr.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("key").stringValue = lines[i].key;
                el.FindPropertyRelative("description").stringValue = lines[i].desc;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetStrArray(Object target, string field, string[] values)
        {
            var so = new SerializedObject(target);
            var arr = so.FindProperty(field);
            if (arr == null) return;
            arr.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                arr.GetArrayElementAtIndex(i).stringValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetAbilities(Object target, GrantedAbility[] abilities)
        {
            var so = new SerializedObject(target);
            var arr = so.FindProperty("requiredAbilities");
            if (arr == null) return;
            arr.arraySize = abilities.Length;
            for (int i = 0; i < abilities.Length; i++)
                arr.GetArrayElementAtIndex(i).enumValueIndex = (int)abilities[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
