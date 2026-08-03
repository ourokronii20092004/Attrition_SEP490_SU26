using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

namespace Attrition.Editor
{
    /// <summary>
    /// Tạo BIỂN CHỈ ĐƯỜNG trong scene: mũi tên + chữ world-space để player biết đi hướng nào.
    /// Dùng TextMeshPro như HazardWarningSignSetupEditor — KHÔNG cần script runtime, KHÔNG NetworkObject,
    /// nên biển chỉ là đồ hoạ tĩnh, mỗi máy tự thấy giống nhau và không tốn băng thông.
    ///
    /// Cách dùng: chọn chỗ muốn đặt trong Scene view (hoặc chọn sẵn 1 object làm mốc) → chạy menu →
    /// biển hiện ra ngay tâm view. Sau đó sửa TRỰC TIẾP trong Inspector:
    ///   • Đổi chữ: component TextMeshPro → ô Text.
    ///   • Đổi hướng mũi tên: sửa luôn trong chữ (◀ ▶ ▲ ▼).
    ///   • Đổi cỡ/màu: fontSize / color của TextMeshPro.
    ///   • Kéo biển tới đúng khúc rẽ như mọi GameObject khác.
    ///
    /// Menu: Tools/Attrition/World/Create Direction Sign
    /// </summary>
    public static class DirectionSignSetupEditor
    {
        private const string RootName = "DirectionSigns";
        private const string SignPrefix = "DirectionSign_";

        [MenuItem("Tools/Attrition/World/Create Direction Sign (Right)")]
        public static void CreateRight() => Create("▶", "Đi tiếp");

        [MenuItem("Tools/Attrition/World/Create Direction Sign (Left)")]
        public static void CreateLeft() => Create("◀", "Quay lại");

        [MenuItem("Tools/Attrition/World/Create Direction Sign (Up)")]
        public static void CreateUp() => Create("▲", "Lên trên");

        [MenuItem("Tools/Attrition/World/Create Direction Sign (Down)")]
        public static void CreateDown() => Create("▼", "Xuống dưới");

        private static void Create(string arrow, string label)
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                EditorUtility.DisplayDialog("Direction Sign", "Mở một scene gameplay trước đã.", "OK");
                return;
            }

            // Gom mọi biển vào một root cho Hierarchy gọn + dễ tìm lại khi cần sửa hàng loạt.
            var root = GameObject.Find(RootName);
            if (root == null)
            {
                root = new GameObject(RootName);
                Undo.RegisterCreatedObjectUndo(root, "Create DirectionSigns root");
            }

            // Đặt ngay tâm Scene view đang nhìn → designer thấy biển liền, không phải mò trong Hierarchy.
            var sv = SceneView.lastActiveSceneView;
            Vector3 position = sv != null ? new Vector3(sv.pivot.x, sv.pivot.y, 0f) : Vector3.zero;

            int index = root.transform.childCount;
            var go = new GameObject($"{SignPrefix}{index:00}");
            Undo.RegisterCreatedObjectUndo(go, "Create Direction Sign");
            go.transform.SetParent(root.transform, true);
            go.transform.position = position;

            var text = go.AddComponent<TextMeshPro>();
            // Mũi tên bên nào thì chữ nằm bên đó cho mắt đọc theo đúng chiều đi.
            text.text = arrow == "◀" ? $"{arrow} {label}" : $"{label} {arrow}";
            text.fontSize = 4f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.95f, 0.88f, 0.62f, 1f);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            // Trên gameplay + nền, dưới cảnh báo hazard (50) để bẫy vẫn nổi hơn chỉ đường.
            text.sortingOrder = 45;

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
            Debug.Log($"[DirectionSign] Đã đặt '{text.text}' tại {position}. " +
                      "Sửa chữ/cỡ/màu trong Inspector (component TextMeshPro), kéo tới đúng khúc rẽ, rồi SAVE scene.");
        }
    }
}
