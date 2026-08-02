#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Attrition.Gameplay.Environment;

namespace Attrition.Editor
{
    public static class SceneAreaIntroSetupEditor
    {
        private static readonly (string path, string area)[] Maps =
        {
            ("Assets/_Project/Scenes/Forest - Map 2.unity", "Forest"),
            ("Assets/_Project/Scenes/Elf Valley -Map 3.unity", "Elf Valley"),
            ("Assets/_Project/Scenes/Dark Forest - Map 4.unity", "Dark Forest"),
            ("Assets/_Project/Scenes/Castle - Map 5.unity", "Castle"),
        };

        [MenuItem("Tools/Attrition/World/Setup Area Intro For Maps 2-5")]
        public static void SetupAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            foreach (var item in Maps)
            {
                var scene = EditorSceneManager.OpenScene(item.path, OpenSceneMode.Single);
                var intro = Object.FindFirstObjectByType<SceneAreaIntro>();
                if (intro == null)
                {
                    var go = new GameObject("SceneAreaIntro");
                    intro = go.AddComponent<SceneAreaIntro>();
                }

                var so = new SerializedObject(intro);
                so.FindProperty("areaName").stringValue = item.area;
                so.FindProperty("delay").floatValue = 1.3f;
                so.ApplyModifiedPropertiesWithoutUndo();

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            Debug.Log("[AreaIntro] Đã thêm tên khu cho Map 2-5 và SAVE: Forest, Elf Valley, Dark Forest, Castle.");
        }
    }
}
#endif
