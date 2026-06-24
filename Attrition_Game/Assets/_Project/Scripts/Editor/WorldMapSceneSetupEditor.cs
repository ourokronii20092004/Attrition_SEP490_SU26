using UnityEditor;
using UnityEngine;
using Attrition.Gameplay.Environment;

namespace Attrition.Editor
{
    /// <summary>
    /// Tool tạo sẵn các component World Map cho scene gameplay đang mở:
    ///   - "WorldMapSystem" : FogTracker + WorldMapController (MonoBehaviour thường).
    ///   - "PendingTravelSpawner" : NetworkObject + PendingTravelSpawner (NetworkBehaviour, Fusion spawn).
    /// Idempotent: chạy lại không tạo trùng.
    /// Menu: Tools/Attrition/Setup World Map (current scene)
    /// </summary>
    public static class WorldMapSceneSetupEditor
    {
        [MenuItem("Tools/Attrition/Setup World Map (current scene)")]
        public static void Setup()
        {
            // 1) WorldMapSystem (Fog + UI) — MonoBehaviour, không cần NetworkObject.
            var sys = Object.FindFirstObjectByType<WorldMapController>();
            if (sys == null)
            {
                var go = new GameObject("WorldMapSystem");
                Undo.RegisterCreatedObjectUndo(go, "Create WorldMapSystem");
                go.AddComponent<FogTracker>();
                go.AddComponent<WorldMapController>();
                Debug.Log("[WorldMapSetup] Đã tạo 'WorldMapSystem' (FogTracker + WorldMapController).");
            }
            else
            {
                if (sys.GetComponent<FogTracker>() == null) sys.gameObject.AddComponent<FogTracker>();
                Debug.Log("[WorldMapSetup] WorldMapController đã có — bỏ qua.");
            }

            // 2) PendingTravelSpawner — NetworkBehaviour → cần NetworkObject để Fusion spawn.
            var pts = Object.FindFirstObjectByType<PendingTravelSpawner>();
            if (pts == null)
            {
                var go = new GameObject("PendingTravelSpawner");
                Undo.RegisterCreatedObjectUndo(go, "Create PendingTravelSpawner");
                go.AddComponent<Fusion.NetworkObject>();
                go.AddComponent<PendingTravelSpawner>();
                Debug.Log("[WorldMapSetup] Đã tạo 'PendingTravelSpawner' (NetworkObject + PendingTravelSpawner). " +
                          "LƯU Ý: object có NetworkObject đặt sẵn trong scene phải được Fusion quản lý — " +
                          "đảm bảo scene này là scene gameplay do host load qua Fusion.");
            }
            else
            {
                Debug.Log("[WorldMapSetup] PendingTravelSpawner đã có — bỏ qua.");
            }

            EditorUtility.SetDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()[0]);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("[WorldMapSetup] Xong. Nhớ Save scene (Ctrl+S).");
        }
    }
}
