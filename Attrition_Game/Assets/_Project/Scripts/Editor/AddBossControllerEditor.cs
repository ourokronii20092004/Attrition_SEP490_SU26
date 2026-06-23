using UnityEditor;
using UnityEngine;
using Attrition.Controllers;

public class AddBossControllerEditor
{
    [MenuItem("Tools/Fix SeveredFang Boss HUD")]
    public static void AddBossController()
    {
        string[] guids = AssetDatabase.FindAssets("SeveredFang t:Prefab");
        if (guids.Length == 0)
        {
            Debug.LogError("Could not find SeveredFang prefab.");
            return;
        }

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

        if (prefab.GetComponent<BossController>() == null)
        {
            BossController bc = prefab.AddComponent<BossController>();
            SerializedObject so = new SerializedObject(bc);
            so.FindProperty("bossDisplayName").stringValue = "SeveredFang";
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(prefab);
            AssetDatabase.SaveAssets();
            Debug.Log("Successfully added BossController to SeveredFang prefab! The Boss HP HUD will now show up.");
        }
        else
        {
            Debug.Log("SeveredFang already has a BossController.");
        }
    }
}
