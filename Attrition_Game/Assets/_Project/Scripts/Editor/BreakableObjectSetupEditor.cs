using UnityEditor;
using UnityEngine;
using Fusion;
using Attrition.Gameplay.World;

namespace Attrition.Editor
{
    /// <summary>
    /// Tool tạo nhanh 1 BreakableObject (vật phá được): NetworkObject + Collider rắn + visual placeholder.
    /// Menu: Tools/Attrition/Create Breakable Object
    /// Sau khi chạy: đặt vị trí, gán sprite, chỉnh 'Hits To Break', đặt layer nằm trong targetLayers của
    /// PlayerCombat (mặc định layer 'Enemy' để đòn đánh player quét trúng), rồi SAVE scene để Fusion bake.
    /// </summary>
    public static class BreakableObjectSetupEditor
    {
        [MenuItem("Tools/Attrition/Create Breakable Object")]
        public static void CreateBreakable()
        {
            var go = new GameObject("Breakable");
            Undo.RegisterCreatedObjectUndo(go, "Create Breakable Object");

            var sv = SceneView.lastActiveSceneView;
            if (sv != null) go.transform.position = sv.pivot;

            // Layer 'Breakable' (KHÔNG dùng 'Enemy'): matrix Physics2D tắt Player↔Enemy nên vật trên layer
            // Enemy sẽ bị player đi xuyên qua. Breakable vẫn nằm trong targetLayers của PlayerCombat.
            int breakableLayer = LayerMask.NameToLayer("Breakable");
            if (breakableLayer >= 0) go.layer = breakableLayer;

            go.AddComponent<NetworkObject>();

            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1f, 1f);
            col.isTrigger = false; // rắn: OverlapCircle của đòn đánh (useTriggers=false) mới quét trúng.

            // Visual placeholder (đổi sprite sau) — để riêng child để rung không lệch collider.
            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform);
            visual.transform.localPosition = Vector3.zero;
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.color = new Color(0.55f, 0.4f, 0.25f);
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(1f, 1f);
            sr.sortingOrder = 4;

            var breakable = go.AddComponent<BreakableObject>();
            SetPrivate(breakable, "hitsToBreak", 6);
            SetPrivate(breakable, "shakeTarget", visual.transform);
            // Mặc định chỉ vỡ khi đánh TỪ BÊN PHẢI (đánh từ trái chỉ rung). Đổi ở Inspector:
            // Any = phía nào cũng vỡ, FromLeft = ngược lại.
            SetPrivate(breakable, "breakOnlyFromSide", (int)BreakableObject.BreakSide.FromRight);

            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
            Debug.Log("[Attrition] Đã tạo Breakable Object (6 đòn, chỉ vỡ khi đánh TỪ PHẢI, layer 'Breakable'). " +
                      "Gán sprite cho 'Visual', chỉnh Hits To Break / Break Only From Side nếu cần, " +
                      "rồi SAVE scene để Fusion bake NetworkObject.");
        }

        private static void SetPrivate(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) { Debug.LogWarning($"[Attrition] Field '{field}' không thấy trên {target.GetType().Name}"); return; }

            switch (value)
            {
                // enum lưu dưới dạng int trong SerializedProperty → dùng chung nhánh với int.
                case int n: prop.intValue = n; break;
                case bool b: prop.boolValue = b; break;
                case Object o: prop.objectReferenceValue = o; break;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
