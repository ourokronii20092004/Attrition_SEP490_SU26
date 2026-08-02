#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Attrition.Data;

namespace Attrition.Editor
{
    public static class ItemDatabaseContractRepairEditor
    {
        private const string Root = "Assets/_Project/Data/Items/";
        private static readonly string[] LegacyOrder =
        {
            "leather_helm", "leather_chest", "leather_boots",
            "bronze_helm", "bronze_chest", "bronze_boots",
            "iron_helm", "iron_chest", "iron_boots",
            "gold_helm", "gold_chest", "gold_boots",
            "acc_double_jump", "acc_shadow_dash", "acc_stamina_charm",
            "skill_fire", "skill_wind", "skill_earth", "skill_thunder", "skill_water",
        };

        [MenuItem("Tools/Attrition/Data/Repair ItemDatabase Append-Only Contract")]
        public static void Repair()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(Root + "skill_wind.asset", ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(Root + "skill_water.asset", ImportAssetOptions.ForceUpdate);

            var db = AssetDatabase.LoadAssetAtPath<ItemDatabaseSO>(Root + "ItemDatabase.asset");
            if (db == null) { Debug.LogError("[ItemDatabase] Không tìm thấy ItemDatabase.asset."); return; }

            var existing = new List<ItemSO>(db.EditorItems);
            var ordered = new List<ItemSO>();
            foreach (var id in LegacyOrder)
            {
                var item = AssetDatabase.LoadAssetAtPath<ItemSO>(Root + id + ".asset");
                if (item == null || item.itemId != id)
                {
                    Debug.LogError($"[ItemDatabase] Asset '{id}.asset' thiếu hoặc có itemId sai.");
                    return;
                }
                ordered.Add(item);
            }

            foreach (var item in existing)
                if (item != null && !ordered.Contains(item)) ordered.Add(item);

            db.EditorItems.Clear();
            db.EditorItems.AddRange(ordered);
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            Debug.Log($"[ItemDatabase] Repair xong: {ordered.Count} item; contract index 0..19 đã khôi phục.");
        }
    }
}
#endif
