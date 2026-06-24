using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Attrition.Editor
{
    /// <summary>
    /// Dọn các transition "mồ côi" trong AnimatorController — sub-asset AnimatorStateTransition /
    /// AnimatorTransition còn nằm trong file .controller nhưng KHÔNG được state/state-machine nào
    /// tham chiếu. Rác này (thường do tool sửa controller gán lại mảng transitions mà không destroy
    /// sub-asset cũ) khiến cửa sổ Animator ném "UnityEditor.Graphs.Edge.WakeUp NullReferenceException".
    ///
    /// Cách dùng:
    ///  - Menu Tools/Attrition/Clean Animator Orphan Transitions (Selected): chọn 1 .controller trong
    ///    Project rồi chạy — dọn đúng controller đó.
    ///  - Menu Tools/Attrition/Clean SeveredFang Animator Orphans: dọn thẳng controller SeveredFang.
    ///
    /// An toàn: chỉ destroy sub-asset KHÔNG được tham chiếu; không đụng state/transition đang dùng.
    /// </summary>
    public static class AnimatorOrphanCleanerEditor
    {
        private const string SeveredFangControllerPath =
            "Assets/_Project/Animations/SeveredFang/SeveredFangIdle001-Sheet_0 1.controller";

        [MenuItem("Tools/Attrition/Clean SeveredFang Animator Orphans")]
        public static void CleanSeveredFang()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(SeveredFangControllerPath);
            if (controller == null)
            {
                Debug.LogError($"[Attrition] Không tìm thấy controller: {SeveredFangControllerPath}");
                return;
            }
            CleanController(controller);
        }

        [MenuItem("Tools/Attrition/Clean Animator Orphan Transitions (Selected)")]
        public static void CleanSelected()
        {
            var controller = Selection.activeObject as AnimatorController;
            if (controller == null)
            {
                Debug.LogError("[Attrition] Hãy chọn 1 AnimatorController trong cửa sổ Project trước khi chạy.");
                return;
            }
            CleanController(controller);
        }

        private static void CleanController(AnimatorController controller)
        {
            string path = AssetDatabase.GetAssetPath(controller);

            // 1. Gom mọi transition ĐANG được tham chiếu (hợp lệ).
            var referenced = new HashSet<Object>();
            foreach (var layer in controller.layers)
                CollectStateMachine(layer.stateMachine, referenced);

            // 2. Quét toàn bộ sub-asset trong file; destroy transition KHÔNG nằm trong tập hợp lệ.
            var all = AssetDatabase.LoadAllAssetRepresentationsAtPath(path);
            // LoadAllAssetRepresentations bỏ qua main asset; thêm cả LoadAllAssetsAtPath cho chắc.
            var everything = AssetDatabase.LoadAllAssetsAtPath(path);

            int removed = 0;
            removed += DestroyOrphans(all, referenced);
            removed += DestroyOrphans(everything, referenced);

            if (removed > 0)
            {
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }

            Debug.Log($"[Attrition] Đã dọn {removed} transition mồ côi trong '{controller.name}'. " +
                      (removed > 0 ? "Mở lại cửa sổ Animator để kiểm tra." : "Không có rác — controller đã sạch."));
        }

        private static int DestroyOrphans(Object[] assets, HashSet<Object> referenced)
        {
            int removed = 0;
            foreach (var obj in assets)
            {
                if (obj == null) continue;
                bool isTransition = obj is AnimatorStateTransition || obj is AnimatorTransition;
                if (!isTransition) continue;
                if (referenced.Contains(obj)) continue;

                Object.DestroyImmediate(obj, true); // xóa sub-asset khỏi file .controller
                removed++;
            }
            return removed;
        }

        private static void CollectStateMachine(AnimatorStateMachine sm, HashSet<Object> referenced)
        {
            if (sm == null) return;

            foreach (var t in sm.anyStateTransitions) if (t != null) referenced.Add(t);
            foreach (var t in sm.entryTransitions) if (t != null) referenced.Add(t);

            foreach (var cs in sm.states)
            {
                if (cs.state == null) continue;
                foreach (var t in cs.state.transitions) if (t != null) referenced.Add(t);
            }

            foreach (var csm in sm.stateMachines)
            {
                if (csm.stateMachine == null) continue;
                // Transition từ state machine con (state-machine transitions).
                foreach (var t in sm.GetStateMachineTransitions(csm.stateMachine))
                    if (t != null) referenced.Add(t);
                CollectStateMachine(csm.stateMachine, referenced); // đệ quy state machine lồng nhau
            }
        }
    }
}
