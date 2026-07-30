#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Fusion;
using Attrition.Gameplay.World;

namespace Attrition.Editor
{
    /// <summary>
    /// Dựng prefab "BÌNH HP GIẤU TRONG MAP" (4 cái, map 1-4) + bật hiệu ứng NHẤP NHÔ cho mọi vật phẩm
    /// nằm dưới sàn.
    ///
    /// CƠ CHẾ SEKIRO (đã có sẵn trong code, tool chỉ nối lại):
    ///   nhặt → `PickupItem` kind = MaxHealthCharge → `PotionSystem.IncreaseMaxHealthCharges(+1)`
    ///        → chỉ tăng CAP (`MaxHealthCharges`), số bình đang có KHÔNG đổi
    ///   rest  → `Checkpoint.DoRest` → `PotionSystem.RefillAll()` → `HealthCharges = MaxHealthCharges`
    ///        → lúc này mới thấy bình mới.
    /// Đúng như yêu cầu: nhặt được → đi rest → bình HP mới cộng thêm.
    ///
    /// Cap cứng là `PotionConfigSO.hardMaxHealthCharges` (mặc định 8). Tổng thiết kế 9 bình
    /// (5 elite + 4 giấu) nên PHẢI nâng cap ≥ 9, nếu không bình thứ 9 bị `Mathf.Min` bỏ im lặng —
    /// tool tự nâng và báo lại.
    ///
    /// Menu: Tools/Attrition/World/Setup Hidden HP Flasks (+ hieu ung noi)
    /// </summary>
    public static class HiddenFlaskSetupEditor
    {
        private const string PrefabDir = "Assets/_Project/Prefabs";
        private const string FlaskPrefabPath = PrefabDir + "/HiddenHealthFlask.prefab";

        /// <summary>Tổng số bình thiết kế: 5 rơi từ elite + 4 giấu trong map 1-4.</summary>
        private const int TotalFlasks = 9;

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
                "CÁCH ĐẶT 4 BÌNH GIẤU (làm tay, tool không đoán được đường ẩn):\n" +
                "  1. Mở map 1..4, kéo HiddenHealthFlask vào scene.\n" +
                "  2. Đặt ở CUỐI ĐƯỜNG ẨN — chỗ phải phá tường (BreakableObject), đi qua HiddenGround,\n" +
                "     hoặc nhảy tới bằng double jump / shadow dash.\n" +
                "  3. SAVE scene (Fusion bake NetworkObject).\n" +
                "  4. Mỗi map ĐÚNG 1 bình → tổng 4 (map 5 không có, vì 5 bình còn lại rơi từ elite).\n\n" +
                "LƯU Ý: nhặt xong bình mới KHÔNG hiện ngay — phải đi REST mới thấy (đúng kiểu Sekiro).");
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
                sr.color = new Color(1f, 0.45f, 0.5f);   // đỏ hồng — gán sprite bình thật sau
                sr.drawMode = SpriteDrawMode.Sliced;
                sr.size = new Vector2(0.7f, 0.9f);
                int playerLayer = SortingLayer.NameToID("Player");
                if (SortingLayer.IsValid(playerLayer)) sr.sortingLayerID = playerLayer;
                sr.sortingOrder = 4;

                go.AddComponent<FloatBobEffect>();

                PrefabUtility.SaveAsPrefabAsset(go, FlaskPrefabPath);
                Debug.Log($"[HiddenFlask] Đã tạo {FlaskPrefabPath} (PickupItem: MaxHealthCharge +1). " +
                          "Gán sprite bình thật vào child 'BobVisual'.");
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
        /// Nâng cap bình máu lên >= tổng số bình thiết kế.
        ///
        /// VÌ SAO: `PotionSystem.IncreaseMaxHealthCharges` kẹp bằng `Mathf.Min(hardMaxHealthCharges, ...)`.
        /// Cap mặc định 8 < 9 bình thiết kế → bình cuối cùng bị bỏ IM LẶNG, player nhặt mà không tăng gì.
        /// </summary>
        private static void RaiseFlaskCap()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:PotionConfigSO"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var cfg = AssetDatabase.LoadAssetAtPath<Attrition.Data.PotionConfigSO>(path);
                if (cfg == null) continue;

                var so = new SerializedObject(cfg);
                var prop = so.FindProperty("hardMaxHealthCharges");
                if (prop == null) continue;

                if (prop.intValue >= TotalFlasks)
                {
                    Debug.Log($"[HiddenFlask] {System.IO.Path.GetFileName(path)}: cap bình máu = "
                              + $"{prop.intValue} (>= {TotalFlasks}) — không cần sửa.");
                    continue;
                }

                int old = prop.intValue;
                prop.intValue = TotalFlasks;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(cfg);
                Debug.Log($"[HiddenFlask] {System.IO.Path.GetFileName(path)}: nâng cap bình máu "
                          + $"{old} → {TotalFlasks} (5 elite + 4 giấu). Không nâng thì bình thứ 9 bị bỏ im lặng.");
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
