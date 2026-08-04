#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Attrition.Gameplay.Environment;

namespace Attrition.Editor
{
    /// <summary>
    /// Đặt vùng ZOOM OUT phòng boss cho Map 2–5 giống Map 1 (user báo Map 1 có, các map khác thiếu).
    ///
    /// Cách tìm phòng boss: lấy CameraBoundsZone NHỎ NHẤT bao quanh boss (cùng cách
    /// <see cref="CameraZoomZone"/> tự dò lúc runtime, và cùng cách RemainingMapGameplaySetupEditor
    /// tìm biên phòng) → tạo zone trùng bound đó.
    ///
    /// Tham số copy từ zone Map 1 đang chạy tốt: zoomedSize 8, maxZoomHardCap 10, lerpSpeed 4.
    /// aspect lấy theo Game view hiện tại, KHÔNG hardcode: clamp theo chiều ngang dùng số này, đặt sai
    /// thì zoom bị hụt hoặc lộ ra ngoài map.
    ///
    /// Idempotent: scene đã có CameraZoomZone trong phòng boss thì chỉ cập nhật lại tham số, không tạo trùng.
    /// Menu: Tools/Attrition/World/Setup Boss Room Zoom (Map 2-5)
    /// </summary>
    public static class BossRoomZoomAllMapsSetupEditor
    {
        private const string SceneDir = "Assets/_Project/Scenes/";

        // Map 1 đã có zone làm sẵn nên KHÔNG đụng vào (tránh ghi đè tinh chỉnh tay của designer).
        private static readonly string[] Scenes =
        {
            "Forest - Map 2",
            "Elf Valley -Map 3",
            "Dark Forest - Map 4",
            "Castle - Map 5",
        };

        [MenuItem("Tools/Attrition/World/Setup Boss Room Zoom (Map 2-5)")]
        public static void SetupAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            string original = SceneManager.GetActiveScene().path;
            int done = 0;

            try
            {
                foreach (var name in Scenes)
                    if (SetupScene(name)) done++;
            }
            finally
            {
                if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original, OpenSceneMode.Single);
            }

            Debug.Log($"[BossZoom] Xong {done}/{Scenes.Length} map. Vào phòng boss để kiểm tra: camera phải " +
                      "kéo xa ra và KHÔNG lộ ngoài mép phòng.");
        }

        private static bool SetupScene(string sceneName)
        {
            string path = SceneDir + sceneName + ".unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
            {
                Debug.LogError($"[BossZoom] Không thấy scene '{path}'.");
                return false;
            }

            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            // Boss = boss mà BossGateController quản (đáng tin hơn dò theo tên prefab: Map 5 có cả boss
            // đánh lại dùng chung prefab, chỉ boss của gate mới là boss CHÍNH của map).
            var gate = Object.FindFirstObjectByType<BossGateController>(FindObjectsInactive.Include);
            if (gate == null || gate.Boss == null)
            {
                Debug.LogError($"[BossZoom] {sceneName}: không thấy BossGateController có boss. " +
                               "Chạy 'Setup Boss Rooms Map 2-5' trước.");
                return false;
            }

            Vector3 bossPos = gate.Boss.transform.position;
            var roomCol = FindSmallestBoundsContaining(bossPos);
            if (roomCol == null)
            {
                Debug.LogError($"[BossZoom] {sceneName}: boss không nằm trong CameraBoundsZone nào → " +
                               "không biết biên phòng để clamp zoom. Vẽ CameraBoundsZone cho phòng boss trước.");
                return false;
            }

            var zone = FindZoneInside(roomCol);
            if (zone == null)
            {
                var go = new GameObject("BossCameraZoomZone");
                Undo.RegisterCreatedObjectUndo(go, "Create Boss Camera Zoom Zone");
                go.transform.position = roomCol.bounds.center;
                var col = go.AddComponent<BoxCollider2D>();
                col.isTrigger = true;
                col.size = new Vector2(roomCol.bounds.size.x, roomCol.bounds.size.y);
                zone = go.AddComponent<CameraZoomZone>();
            }
            else
            {
                // Đã có: chỉ chỉnh lại cho khít bound (không tạo thêm).
                zone.transform.position = roomCol.bounds.center;
                var col = zone.GetComponent<BoxCollider2D>();
                if (col != null)
                {
                    col.isTrigger = true;
                    col.size = new Vector2(roomCol.bounds.size.x, roomCol.bounds.size.y);
                }
            }

            var so = new SerializedObject(zone);
            SetFloat(so, "zoomedSize", 8f);
            SetFloat(so, "maxZoomHardCap", 10f);
            SetFloat(so, "defaultSize", 0f);      // 0 = tự lấy cỡ camera lúc vào vùng làm mốc trả về
            SetFloat(so, "lerpSpeed", 4f);
            SetFloat(so, "aspect", GetGameAspect());
            var rb = so.FindProperty("roomBounds");
            if (rb != null) rb.objectReferenceValue = roomCol;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[BossZoom] {sceneName}: zone khớp bound '{roomCol.name}' " +
                      $"(size {roomCol.bounds.size.x:F1}x{roomCol.bounds.size.y:F1}), zoomedSize=8, cap=10.");
            return true;
        }

        /// <summary>CameraBoundsZone nhỏ nhất chứa điểm này = phòng sát nhất (khớp cách CameraZoomZone tự dò).</summary>
        private static Collider2D FindSmallestBoundsContaining(Vector3 point)
        {
            Collider2D best = null;
            float bestArea = float.MaxValue;
            foreach (var bz in Object.FindObjectsByType<CameraBoundsZone>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var col = bz.GetComponent<Collider2D>();
                if (col == null || !col.bounds.Contains(point)) continue;
                float area = col.bounds.size.x * col.bounds.size.y;
                if (area < bestArea) { bestArea = area; best = col; }
            }
            return best;
        }

        private static CameraZoomZone FindZoneInside(Collider2D room)
        {
            foreach (var z in Object.FindObjectsByType<CameraZoomZone>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (z != null && room.bounds.Contains(z.transform.position)) return z;
            return null;
        }

        private static float GetGameAspect()
        {
            if (Screen.height > 0) return (float)Screen.width / Screen.height;
            return 16f / 9f;
        }

        private static void SetFloat(SerializedObject so, string field, float value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.floatValue = value;
        }
    }
}
#endif
