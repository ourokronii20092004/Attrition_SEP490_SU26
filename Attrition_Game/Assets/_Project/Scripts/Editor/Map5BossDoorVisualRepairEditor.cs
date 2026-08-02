#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Attrition.Gameplay.Environment;
using Attrition.Gameplay.World;

namespace Attrition.Editor
{
    public static class Map5BossDoorVisualRepairEditor
    {
        [MenuItem("Tools/Attrition/World/Fix Map 5 Room 5 Boss Doors")]
        public static void Fix()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.name != "Castle - Map 5")
            {
                EditorUtility.DisplayDialog("Room 5 Boss Doors", "Mở scene 'Castle - Map 5' trước.", "OK");
                return;
            }

            var gate = Object.FindFirstObjectByType<BossGateController>();
            if (gate == null)
            {
                Debug.LogError("[Room5Doors] Không tìm thấy BossGateController của ArchDemon.");
                return;
            }

            var gateSo = new SerializedObject(gate);
            var entry = gateSo.FindProperty("entryDoor")?.objectReferenceValue as Door;
            var exit = gateSo.FindProperty("exitDoor")?.objectReferenceValue as Door;
            if (entry == null || exit == null)
            {
                Debug.LogError("[Room5Doors] BossGateController thiếu EntryDoor hoặc ExitDoor.");
                return;
            }

            Repair(entry, new Color(0.3f, 0.15f, 0.3f, 1f));
            Repair(exit, new Color(0.35f, 0.2f, 0.1f, 1f));

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = exit.gameObject;
            Debug.Log("[Room5Doors] Đã tạo visual riêng cho EntryDoor/ExitDoor, sortingOrder=20, " +
                      "và xoá openedVisual trỏ nhầm. SAVE scene để giữ thay đổi.");
        }

        private static void Repair(Door door, Color color)
        {
            var old = door.transform.Find("DoorVisual_Runtime");
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

            var visual = new GameObject("DoorVisual_Runtime");
            Undo.RegisterCreatedObjectUndo(visual, "Repair boss door visual");
            visual.transform.SetParent(door.transform, false);

            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            sr.color = color;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(1f, 4f);
            sr.sortingOrder = 20;

            var so = new SerializedObject(door);
            so.FindProperty("doorVisual").objectReferenceValue = visual;
            so.FindProperty("openedVisual").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
