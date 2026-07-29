using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Fusion;
using Attrition.UI;
using Attrition.Gameplay.Environment;

namespace Attrition.Editor
{
    /// <summary>
    /// Dựng ĐỦ BỘ UI + system cho scene gameplay đang mở, sao đúng cấu hình của Map 1.
    ///
    /// Vì sao cần: CHỈ Map 1 có `GameUI` (UIDocument + GameUIController), `WorldMapSystem`
    /// (WorldMapController + FogTracker) và `PendingTravelSpawner`. Map 2..5 KHÔNG có gì → sang map
    /// khác là mất HUD (HP/mana/bình), ESC (pause), Tab (túi đồ/nhân vật), thanh máu boss, bảng
    /// checkpoint (F), world map và hội thoại NPC.
    ///
    /// Tool tạo (bỏ qua nếu đã có — chạy lại an toàn):
    ///  - GameUI            : UIDocument(GameUI.uxml + Panel Settings) + GameUIController + icon bình.
    ///  - DialogueUI        : instance prefab Prefabs/DialogueUI.prefab (hội thoại + quest tracker).
    ///  - WorldMapSystem    : WorldMapController (+ icon campfire) + FogTracker.
    ///  - PendingTravelSpawner : NetworkObject + PendingTravelSpawner (fast-travel cross-map).
    ///
    /// Menu: Tools/Attrition/Scene UI/Setup Game UI For Current Scene
    /// SAU KHI CHẠY: SAVE scene (PendingTravelSpawner có NetworkObject → Fusion cần bake).
    /// </summary>
    public static class SceneUiBundleSetupEditor
    {
        private const string GameUiUxml = "Assets/_Project/UI/GameUI.uxml";
        // Tên KHÁC type PanelSettings để không che (shadow) chính type khi khai báo biến bên dưới.
        private const string PanelSettingsPath = "Assets/_Project/UI/Panel Settings.asset";
        private const string DialogueUiPrefab = "Assets/_Project/Prefabs/DialogueUI.prefab";
        private const string HpPotionIcon = "Assets/_Project/Art/UI_Elements/16x16/hp potion.png";
        private const string ManaPotionIcon = "Assets/_Project/Art/UI_Elements/16x16/mana potion.png";
        private const string CampfireIcon = "Assets/_Project/Art/UI_Elements/Sprite-sheet-campfire.png";

        [MenuItem("Tools/Attrition/Scene UI/Setup Game UI For Current Scene")]
        public static void SetupCurrent()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.name))
            {
                EditorUtility.DisplayDialog("Scene UI", "Mở một scene gameplay trước đã.", "OK");
                return;
            }

            int created = 0;
            if (EnsureGameUI()) created++;
            if (EnsureDialogueUI()) created++;
            if (EnsureWorldMapSystem()) created++;
            if (EnsurePendingTravelSpawner()) created++;

            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log($"[Attrition] Scene '{scene.name}': đã bổ sung {created} object UI/system " +
                      "(cái nào có sẵn thì giữ nguyên). SAVE scene để Fusion bake NetworkObject của " +
                      "PendingTravelSpawner.");
        }

        //  GAME UI (HUD + ESC + Tab + boss bar + bảng checkpoint)

        private static bool EnsureGameUI()
        {
            if (Object.FindFirstObjectByType<GameUIController>() != null) return false;

            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(GameUiUxml);
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (uxml == null || panel == null)
            {
                Debug.LogError($"[Attrition] Không load được {GameUiUxml} hoặc {PanelSettingsPath}.");
                return false;
            }

            var go = new GameObject("GameUI");
            Undo.RegisterCreatedObjectUndo(go, "Create GameUI");
            go.layer = 5; // UI (giống Map 1)

            var doc = go.AddComponent<UIDocument>();
            var docSo = new SerializedObject(doc);
            docSo.FindProperty("m_PanelSettings").objectReferenceValue = panel;
            docSo.FindProperty("sourceAsset").objectReferenceValue = uxml;
            docSo.ApplyModifiedPropertiesWithoutUndo();

            var ui = go.AddComponent<GameUIController>();
            var uiSo = new SerializedObject(ui);
            SetSprite(uiSo, "healthFlaskIcon", HpPotionIcon);
            SetSprite(uiSo, "manaFlaskIcon", ManaPotionIcon);
            uiSo.ApplyModifiedPropertiesWithoutUndo();

            return true;
        }

        //  DIALOGUE UI (hội thoại NPC + quest tracker)

        private static bool EnsureDialogueUI()
        {
            if (Object.FindFirstObjectByType<DialogueUI>() != null) return false;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DialogueUiPrefab);
            if (prefab == null)
            {
                Debug.LogWarning($"[Attrition] Không tìm thấy {DialogueUiPrefab} — bỏ qua DialogueUI.");
                return false;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(go, "Create DialogueUI");
            return true;
        }

        //  WORLD MAP + FOG

        private static bool EnsureWorldMapSystem()
        {
            bool hasMap = Object.FindFirstObjectByType<WorldMapController>() != null;
            bool hasFog = Object.FindFirstObjectByType<FogTracker>() != null;
            if (hasMap && hasFog) return false;

            var go = new GameObject("WorldMapSystem");
            Undo.RegisterCreatedObjectUndo(go, "Create WorldMapSystem");

            if (!hasMap)
            {
                var wm = go.AddComponent<WorldMapController>();
                var so = new SerializedObject(wm);
                SetSprite(so, "teleIcon", CampfireIcon);
                var sizeProp = so.FindProperty("playerIconSize");
                if (sizeProp != null) sizeProp.floatValue = 30f;   // khớp Map 1
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // FogTracker tự tra MapData theo tên scene (MapRegistry) nên không cần gán tay.
            if (!hasFog) go.AddComponent<FogTracker>();

            return true;
        }

        //  PENDING TRAVEL (fast-travel cross-map đặt player tại checkpoint đích)

        private static bool EnsurePendingTravelSpawner()
        {
            if (Object.FindFirstObjectByType<PendingTravelSpawner>() != null) return false;

            var go = new GameObject("PendingTravelSpawner");
            Undo.RegisterCreatedObjectUndo(go, "Create PendingTravelSpawner");
            go.AddComponent<NetworkObject>();
            go.AddComponent<PendingTravelSpawner>();
            return true;
        }

        //  HELPERS

        private static void SetSprite(SerializedObject so, string field, string assetPath)
        {
            var prop = so.FindProperty(field);
            if (prop == null) return;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null)
            {
                Debug.LogWarning($"[Attrition] Không load được sprite: {assetPath}");
                return;
            }
            prop.objectReferenceValue = sprite;
        }
    }
}
