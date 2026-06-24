using UnityEditor;
using UnityEngine;
using Attrition.Gameplay.Enemy.SeveredFang;
using Attrition.Gameplay.Environment;

namespace Attrition.Editor
{
    /// <summary>
    /// Tool tạo vùng kích hoạt Boss (BossEncounterTrigger) — thứ còn THIẾU khiến boss chỉ đứng
    /// idle (không ai gọi StartIntroSequence). Tự tìm SeveredFangAI trong scene và gán vào ô 'boss'.
    /// Menu: Tools/Attrition/Create Boss Encounter Trigger
    /// Sau khi chạy: đặt vùng này ở GIỮA/đầu phòng boss (chỗ player chắc chắn đi qua sau khi vào),
    /// chỉnh kích thước BoxCollider cho phủ lối đi. Đảm bảo boss có waitForTrigger=true + introDialogue.
    /// </summary>
    public static class BossEncounterTriggerSetupEditor
    {
        [MenuItem("Tools/Attrition/Create Boss Encounter Trigger")]
        public static void CreateTrigger()
        {
            var go = new GameObject("BossEncounterTrigger");
            Undo.RegisterCreatedObjectUndo(go, "Create Boss Encounter Trigger");

            var sv = SceneView.lastActiveSceneView;
            if (sv != null) go.transform.position = sv.pivot;

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(4f, 5f); // phủ lối đi; chỉnh lại cho khớp phòng

            var trigger = go.AddComponent<BossEncounterTrigger>();

            // Tự tìm boss SeveredFang trong scene và gán.
            var boss = Object.FindFirstObjectByType<SeveredFangAI>(FindObjectsInactive.Include);
            if (boss != null)
            {
                var so = new SerializedObject(trigger);
                var prop = so.FindProperty("boss");
                if (prop != null) { prop.objectReferenceValue = boss; so.ApplyModifiedPropertiesWithoutUndo(); }

                // Cảnh báo nếu boss chưa bật waitForTrigger (sẽ không chờ → tự đánh ngay).
                var bso = new SerializedObject(boss);
                var wft = bso.FindProperty("waitForTrigger");
                var dlg = bso.FindProperty("introDialogue");
                if (wft != null && !wft.boolValue)
                    Debug.LogWarning("[Attrition] Boss đang waitForTrigger=FALSE → boss sẽ tự đánh, KHÔNG chờ trigger. " +
                                     "Bật waitForTrigger=true trên SeveredFangAI nếu muốn intro qua trigger.");
                if (dlg != null && dlg.objectReferenceValue == null)
                    Debug.LogWarning("[Attrition] Boss chưa gán 'introDialogue' (DialogueSO) → sẽ vào đánh luôn, không có thoại.");
            }
            else
            {
                Debug.LogWarning("[Attrition] Không tìm thấy SeveredFangAI trong scene. Hãy đặt boss vào scene trước, " +
                                 "rồi kéo tay boss vào ô 'boss' của BossEncounterTrigger.");
            }

            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
            Debug.Log("[Attrition] Đã tạo BossEncounterTrigger" + (boss != null ? " và gán boss tự động." : ".") +
                      " Đặt vùng ở giữa/đầu phòng boss (sau lối vào), chỉnh BoxCollider phủ lối đi, SAVE scene.");
        }
    }
}
