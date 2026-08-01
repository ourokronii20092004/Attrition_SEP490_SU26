#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Fusion;
using Attrition.Gameplay.World;

namespace Attrition.Editor
{
    /// <summary>
    /// Dựng prefab "BÌNH HP GIẤU TRONG MAP" (9 cái: map 1-4 mỗi map 2, map 5 có 1) + bật hiệu ứng
    /// NHẤP NHÔ cho mọi vật phẩm nằm dưới sàn.
    ///
    /// CƠ CHẾ SEKIRO (đã có sẵn trong code, tool chỉ nối lại):
    ///   nhặt → `PickupItem` kind = MaxHealthCharge → `PotionSystem.IncreaseMaxHealthCharges(+1)`
    ///        → chỉ tăng CAP (`MaxHealthCharges`), số bình đang có KHÔNG đổi
    ///   rest  → `Checkpoint.DoRest` → `PotionSystem.RefillAll()` → `HealthCharges = MaxHealthCharges`
    ///        → lúc này mới thấy bình mới.
    /// Đúng như yêu cầu: nhặt được → đi rest → bình HP mới cộng thêm.
    ///
    /// Có 9 pickup giấu. Cap cần ít nhất = số bình khởi đầu + 9; tool tự nâng và báo lại.
    ///
    /// Menu: Tools/Attrition/World/Setup Hidden HP Flasks (+ hieu ung noi)
    /// </summary>
    public static class HiddenFlaskSetupEditor
    {
        private const string PrefabDir = "Assets/_Project/Prefabs";
        private const string FlaskPrefabPath = PrefabDir + "/HiddenHealthFlask.prefab";

        /// <summary>Tổng pickup giấu: map 1-4 mỗi map 2, map 5 có 1.</summary>
        private const int HiddenFlaskCount = 9;

        [MenuItem("Tools/Attrition/World/Add Hidden HP Flasks To Current Map")]
        public static void AddToCurrentMap()
        {
            var scene = SceneManager.GetActiveScene();
            int wanted = scene.name.Contains("Map 5") ? 1 : scene.name.Contains("Map ") ? 2 : 0;
            if (wanted == 0)
            {
                Debug.LogError("[HiddenFlask] Scene hiện tại không phải Map 1-5.");
                return;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FlaskPrefabPath);
            if (prefab == null)
            {
                Setup();
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FlaskPrefabPath);
            }
            if (prefab == null)
            {
                Debug.LogError($"[HiddenFlask] Không tạo được prefab: {FlaskPrefabPath}");
                return;
            }

            int existing = 0;
            foreach (var root in scene.GetRootGameObjects())
                if (root.name.StartsWith("HiddenHealthFlask")) existing++;

            Vector3 origin = SceneView.lastActiveSceneView != null
                ? SceneView.lastActiveSceneView.pivot
                : Vector3.zero;
            for (int i = existing; i < wanted; i++)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.name = $"HiddenHealthFlask_{i + 1}";
                instance.transform.position = origin + Vector3.right * (i - existing);
                Undo.RegisterCreatedObjectUndo(instance, "Add Hidden HP Flask");
            }

            if (existing < wanted) EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[HiddenFlask] {scene.name}: {Mathf.Max(existing, wanted)}/{wanted} bình. " +
                      "Di chuyển bình mới vào đường ẩn rồi Save scene để Fusion bake NetworkObject.");
        }

        [MenuItem("Tools/Attrition/World/Setup Hidden HP Flasks (+ hieu ung noi)")]
        public static void Setup()
        {
            BuildFlaskPrefab();
            AddBobToPrefab(PrefabDir + "/DroppedItem.prefab", "DroppedItem");
            AddBobToPrefab(PrefabDir + "/PickupItem.prefab", "PickupItem");
            RaiseFlaskCap();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "[HiddenFlask] XONG.\n" +
                $"• Prefab bình giấu: {FlaskPrefabPath}\n" +
                "• Hiệu ứng nhấp nhô: đã gắn FloatBobEffect vào DroppedItem + PickupItem (item rơi ra & bình nhặt).\n\n" +
                "CÁCH ĐẶT 9 BÌNH GIẤU (làm tay, tool không đoán được đường ẩn):\n" +
                "  1. Mở từng map và kéo HiddenHealthFlask vào scene.\n" +
                "  2. Map 1-4: mỗi map ĐÚNG 2 bình. Map 5: ĐÚNG 1 bình.\n" +
                "  3. Đặt ở cuối đường ẩn — chỗ phải phá tường (BreakableObject), đi qua HiddenGround,\n" +
                "     hoặc nhảy tới bằng double jump / shadow dash.\n" +
                "  4. SAVE scene và bake Fusion scene objects.\n" +
                "  5. Tổng cả 5 map phải là 9 bình.\n\n" +
                "LƯU Ý: nhặt xong bình mới KHÔNG hiện ngay — phải đi REST mới thấy.");
        }

        /// <summary>
        /// Tạo/ghi lại prefab bình HP giấu. `PickupItem` với kind = MaxHealthCharge, amount = 1.
        /// Không cần Rigidbody: bình đứng yên tại chỗ đặt, chỉ cần trigger để phát hiện player.
        /// </summary>
        private static void BuildFlaskPrefab()
        {
            var go = new GameObject("HiddenHealthFlask");
            try
            {
                go.AddComponent<NetworkObject>();

                var col = go.AddComponent<CircleCollider2D>();
                col.radius = 0.6f;
                col.isTrigger = true;   // PickupItem dùng OnTriggerEnter2D

                var pickup = go.AddComponent<PickupItem>();
                var so = new SerializedObject(pickup);
                // kind là enum PickupKind → MaxHealthCharge (index 2).
                SetEnum(so, "kind", (int)PickupKind.MaxHealthCharge);
                SetInt(so, "amount", 1);
                so.ApplyModifiedPropertiesWithoutUndo();

                // Hình: child riêng để FloatBobEffect nhấp nhô mà không đụng gốc networked.
                var visual = new GameObject("BobVisual");
                visual.transform.SetParent(go.transform, false);
                var sr = visual.AddComponent<SpriteRenderer>();
                sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/_Project/Art/UI_Elements/16x16/hp potion.png");
                sr.color = Color.white;
                sr.drawMode = SpriteDrawMode.Sliced;
                sr.size = new Vector2(0.7f, 0.9f);
                int playerLayer = SortingLayer.NameToID("Player");
                if (SortingLayer.IsValid(playerLayer)) sr.sortingLayerID = playerLayer;
                sr.sortingOrder = 4;

                go.AddComponent<FloatBobEffect>();

                PrefabUtility.SaveAsPrefabAsset(go, FlaskPrefabPath);
                Debug.Log($"[HiddenFlask] Đã tạo {FlaskPrefabPath} (PickupItem: MaxHealthCharge +1).");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>Gắn FloatBobEffect vào 1 prefab sẵn có (item rơi ra / vật phẩm nhặt).</summary>
        private static void AddBobToPrefab(string path, string label)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            if (root == null)
            {
                Debug.LogWarning($"[HiddenFlask] Không mở được prefab: {path}");
                return;
            }

            try
            {
                if (root.GetComponent<FloatBobEffect>() != null)
                {
                    Debug.Log($"[HiddenFlask] {label}: đã có FloatBobEffect — bỏ qua.");
                    return;
                }

                root.AddComponent<FloatBobEffect>();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[HiddenFlask] {label}: + FloatBobEffect (nhấp nhô lên xuống).");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// Nâng cap bình máu lên >= số bình khởi đầu + tổng pickup giấu.
        /// `IncreaseMaxHealthCharges` kẹp bằng hard cap nên cap thiếu sẽ làm pickup biến mất mà không tăng.
        /// </summary>
        private static void RaiseFlaskCap()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:PotionConfigSO"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var cfg = AssetDatabase.LoadAssetAtPath<Attrition.Data.PotionConfigSO>(path);
                if (cfg == null) continue;

                int requiredCap = cfg.startingHealthCharges + HiddenFlaskCount;
                var so = new SerializedObject(cfg);
                var prop = so.FindProperty("hardMaxHealthCharges");
                if (prop == null) continue;

                if (prop.intValue >= requiredCap)
                {
                    Debug.Log($"[HiddenFlask] {System.IO.Path.GetFileName(path)}: cap bình máu = "
                              + $"{prop.intValue} (>= {requiredCap}) — không cần sửa.");
                    continue;
                }

                int old = prop.intValue;
                prop.intValue = requiredCap;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(cfg);
                Debug.Log($"[HiddenFlask] {System.IO.Path.GetFileName(path)}: nâng cap bình máu "
                          + $"{old} → {requiredCap} ({cfg.startingHealthCharges} khởi đầu + "
                          + $"{HiddenFlaskCount} pickup giấu).");
            }
        }

        private static void SetEnum(SerializedObject so, string field, int value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.enumValueIndex = value;
            else Debug.LogWarning($"[HiddenFlask] Không thấy field '{field}'.");
        }

        private static void SetInt(SerializedObject so, string field, int value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.intValue = value;
            else Debug.LogWarning($"[HiddenFlask] Không thấy field '{field}'.");
        }
    }
}
#endif
