using UnityEngine;
using UnityEditor;
using Attrition.Data;

namespace Attrition.Editor.Tools
{
    public class BossDialogueGeneratorEditor
    {
        [MenuItem("Tools/Generate Boss Dialogue (SeveredFang)")]
        public static void GenerateDialogue()
        {
            string folderPath = "Assets/_Project/Data/Dialogue/Bosses";
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Data"))
                AssetDatabase.CreateFolder("Assets/_Project", "Data");
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Data/Dialogue"))
                AssetDatabase.CreateFolder("Assets/_Project/Data", "Dialogue");
            if (!AssetDatabase.IsValidFolder(folderPath))
                AssetDatabase.CreateFolder("Assets/_Project/Data/Dialogue", "Bosses");

            var so = ScriptableObject.CreateInstance<DialogueSO>();
            so.lines = new DialogueLine[]
            {
                new DialogueLine { speakerName = "Severed Fang", text = "Lại có kẻ lạ mặt dám bước chân vào lãnh địa của ta sao?" },
                new DialogueLine { speakerName = "Severed Fang", text = "Hơi thở của kẻ yếu đuối... Thật nực cười!" },
                new DialogueLine { speakerName = "Severed Fang", text = "Hãy để thanh kiếm này kết thúc sự đau khổ của ngươi. Bỏ mạng tại đây đi, kẻ ngoại đạo!" }
            };

            string path = $"{folderPath}/Dialogue_SeveredFang_Intro.asset";
            AssetDatabase.CreateAsset(so, path);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Boss Dialogue] Đã tạo thành công thoại mẫu tại: {path}");
            Selection.activeObject = so;
        }
    }
}
