using UnityEditor;
using UnityEngine;
using Fusion;
using Attrition.Data;
using Attrition.Gameplay.World;

namespace Attrition.Editor
{
    /// <summary>
    /// Tool tạo vật phẩm "Feather Charm" (acc_double_jump) đặt trong Map 2 — nhặt vào túi là
    /// MỞ KHOÁ double jump (PlayerController đọc qua PlayerInventory.HasAbility).
    /// Menu: Tools/Attrition/Create Double Jump Pickup (Map 2)
    /// Sau khi chạy: đặt vị trí trong Room 1 của Map 2, gán Sprite/VFX cho đẹp, SAVE scene
    /// để Fusion bake NetworkObject.
    /// </summary>
    public static class DoubleJumpPickupSetupEditor
    {
        private const string AccessoryAssetPath = "Assets/_Project/Data/Items/acc_double_jump.asset";

        [MenuItem("Tools/Attrition/Create Double Jump Pickup (Map 2)")]
        public static void CreatePickup()
        {
            var accessory = AssetDatabase.LoadAssetAtPath<AccessorySO>(AccessoryAssetPath);
            if (accessory == null)
            {
                Debug.LogError($"[Attrition] Không tìm thấy {AccessoryAssetPath}. " +
                               "Chạy tool tạo item (ItemSystemSetup) trước để sinh acc_double_jump.");
                return;
            }

            var go = new GameObject("Pickup_DoubleJump_FeatherCharm");
            Undo.RegisterCreatedObjectUndo(go, "Create Double Jump Pickup");

            var sv = SceneView.lastActiveSceneView;
            if (sv != null) go.transform.position = sv.pivot;

            go.AddComponent<NetworkObject>();

            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1f, 1f);
            col.isTrigger = true;

            // Visual placeholder (đổi sprite sau).
            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform);
            visual.transform.localPosition = Vector3.zero;
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = accessory.icon; // dùng icon item nếu có
            sr.color = new Color(0.7f, 0.9f, 1f);
            sr.sortingOrder = 6;
            if (sr.sprite == null) { sr.drawMode = SpriteDrawMode.Sliced; sr.size = new Vector2(0.8f, 0.8f); }

            var pickup = go.AddComponent<PickupItem>();
            SetPrivate(pickup, "kind", (int)PickupKind.InventoryItem);
            SetPrivate(pickup, "amount", 1);
            SetPrivate(pickup, "itemData", accessory);

            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
            Debug.Log("[Attrition] Đã tạo Pickup Double Jump (Feather Charm) trong scene hiện tại. " +
                      "Đặt nó vào Room 1 của Map 2, SAVE scene để Fusion bake NetworkObject. " +
                      "Nhặt item này = mở khoá double jump (sở hữu là đủ, không cần trang bị).");
        }

        private static void SetPrivate(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) { Debug.LogWarning($"[Attrition] Field '{field}' không thấy trên {target.GetType().Name}"); return; }

            switch (value)
            {
                case int n: prop.intValue = n; break;
                case bool b: prop.boolValue = b; break;
                case Object o: prop.objectReferenceValue = o; break;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
