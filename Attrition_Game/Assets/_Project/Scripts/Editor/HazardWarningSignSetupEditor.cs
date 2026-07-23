using UnityEditor;
using UnityEngine;

namespace Attrition.Editor
{
    /// <summary>
    /// Tool tạo BIỂN CẢNH BÁO HAZARD — vật trang trí đặt cạnh bẫy (gai/dung nham...) để người chơi chú ý.
    /// Menu: Tools/Attrition/Create Hazard Warning Sign
    /// Chỉ là sprite tĩnh (không cần script/NetworkObject): logic chạm bẫy → quay về điểm đứng đã nằm sẵn
    /// ở PlayerController.HazardHit + component Hazard. Sau khi tạo: đặt cạnh bẫy, gán sprite biển báo.
    /// </summary>
    public static class HazardWarningSignSetupEditor
    {
        [MenuItem("Tools/Attrition/Create Hazard Warning Sign")]
        public static void CreateSign()
        {
            var go = new GameObject("HazardWarningSign");
            Undo.RegisterCreatedObjectUndo(go, "Create Hazard Warning Sign");

            var sv = SceneView.lastActiveSceneView;
            if (sv != null) go.transform.position = sv.pivot;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.color = new Color(1f, 0.85f, 0.1f); // vàng cảnh báo (placeholder cho tới khi gán sprite)
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(1f, 1f);
            sr.sortingOrder = 3;

            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
            Debug.Log("[Attrition] Đã tạo Hazard Warning Sign (placeholder vàng). Đặt cạnh bẫy, gán sprite biển báo. " +
                      "Không cần script/bake — chỉ là vật trang trí báo hiệu.");
        }
    }
}
