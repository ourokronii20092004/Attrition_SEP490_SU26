using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;
using System.IO;

public class EnemyAnimatorOverrideBuilder : EditorWindow
{
    private AnimatorController baseController;
    private string prefix = "MonsterName";
    private string targetFolder = "Assets/Animations/Enemies";

    [MenuItem("Tools/Enemy Animator Override Builder")]
    public static void ShowWindow()
    {
        GetWindow<EnemyAnimatorOverrideBuilder>("Override Builder");
    }

    private void OnGUI()
    {
        GUILayout.Label("Animator Override Builder", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.HelpBox("Công cụ này giúp tự động tạo Animator Override Controller từ một Base Controller và tự động điền các Animation Clips nếu tên của chúng khớp với (Prefix_StateName).", MessageType.Info);
        
        EditorGUILayout.Space();

        baseController = (AnimatorController)EditorGUILayout.ObjectField("Base Controller", baseController, typeof(AnimatorController), false);
        prefix = EditorGUILayout.TextField("Monster Prefix (VD: Goblin)", prefix);
        
        GUILayout.BeginHorizontal();
        targetFolder = EditorGUILayout.TextField("Folder lưu & tìm Clip", targetFolder);
        if (GUILayout.Button("Browse", GUILayout.Width(60)))
        {
            string path = EditorUtility.OpenFolderPanel("Chọn thư mục", "Assets", "");
            if (!string.IsNullOrEmpty(path))
            {
                if (path.StartsWith(Application.dataPath))
                {
                    targetFolder = "Assets" + path.Substring(Application.dataPath.Length);
                }
            }
        }
        GUILayout.EndHorizontal();

        EditorGUILayout.Space();

        if (GUILayout.Button("Create Override Controller", GUILayout.Height(30)))
        {
            CreateOverrideController();
        }
    }

    private void CreateOverrideController()
    {
        if (baseController == null)
        {
            EditorUtility.DisplayDialog("Lỗi", "Vui lòng chọn Base Controller!", "OK");
            return;
        }

        if (string.IsNullOrEmpty(targetFolder) || !AssetDatabase.IsValidFolder(targetFolder))
        {
            EditorUtility.DisplayDialog("Lỗi", "Thư mục không hợp lệ hoặc không tồn tại!", "OK");
            return;
        }

        // Tạo Override Controller
        AnimatorOverrideController overrideController = new AnimatorOverrideController();
        overrideController.runtimeAnimatorController = baseController;

        // Lấy danh sách Animation Clips trong base
        var clipOverrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        overrideController.GetOverrides(clipOverrides);

        // Tìm tất cả các clip trong thư mục đích có chứa Prefix
        string[] guids = AssetDatabase.FindAssets($"t:AnimationClip {prefix}", new[] { targetFolder });
        Dictionary<string, AnimationClip> availableClips = new Dictionary<string, AnimationClip>();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip != null)
            {
                availableClips[clip.name.ToLower()] = clip;
            }
        }

        // Tự động gán clip dựa theo tên State trong Base
        for (int i = 0; i < clipOverrides.Count; i++)
        {
            AnimationClip originalClip = clipOverrides[i].Key;
            if (originalClip != null)
            {
                // Dùng tên clip cũ trong base hoặc state name để so sánh
                // Thường đặt tên clip base là Base_Idle, Base_Walk... ta sẽ thay Base bằng Prefix
                string stateName = originalClip.name;
                string searchName = $"{prefix}_{stateName}".ToLower();
                string searchName2 = $"{prefix}{stateName}".ToLower(); // Không có gạch dưới

                // Tìm trong available clips
                AnimationClip replacementClip = null;
                foreach (var kvp in availableClips)
                {
                    if (kvp.Key.Contains(searchName) || kvp.Key.Contains(stateName.ToLower())) // Fallback
                    {
                        // Ưu tiên chính xác prefix
                        if (kvp.Key.Contains(prefix.ToLower()))
                        {
                            replacementClip = kvp.Value;
                            break;
                        }
                    }
                }

                if (replacementClip != null)
                {
                    clipOverrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(originalClip, replacementClip);
                }
            }
        }

        overrideController.ApplyOverrides(clipOverrides);

        string savePath = $"{targetFolder}/{prefix}_AnimatorOverride.overrideController";
        savePath = AssetDatabase.GenerateUniqueAssetPath(savePath);

        AssetDatabase.CreateAsset(overrideController, savePath);
        AssetDatabase.SaveAssets();

        Selection.activeObject = overrideController;
        EditorGUIUtility.PingObject(overrideController);

        Debug.Log($"[AnimatorOverrideBuilder] Đã tạo thành công Override Controller tại {savePath}");
    }
}
