using UnityEngine;
using UnityEditor;
using Attrition.Data;

namespace Attrition.Editor.Tools
{
    public static class BossDialogueGeneratorEditor
    {
        private const string Folder = "Assets/_Project/Data/Dialogue/Bosses";

        [MenuItem("Tools/Attrition/NPC/Generate Boss Dialogues")]
        public static void GenerateDialogue()
        {
            EnsureFolder();

            Save("Dialogue_SeveredFang_Intro", Lines("Severed Fang",
                "Another trespasser steps into my domain.",
                "Your fear gives you away. Draw your weapon."));
            Save("Dialogue_SeveredFang_Death", Lines("Severed Fang",
                "So this blade has finally found its end...",
                "Take the flame. The road ahead will show you no mercy."));

            Save("Dialogue_Druid_Intro", Lines("Druid",
                "The grove no longer welcomes your kind.",
                "The wind itself will cast you out."));
            Save("Dialogue_Druid_Death", Lines("Druid",
                "The wind... has chosen another bearer.",
                "Go. Free what remains of this forest."));

            Save("Dialogue_Elf_Intro", Lines("Fallen Elf",
                "Turn back. Nothing living leaves this valley unchanged.",
                "If you advance, the storm will judge you."));
            Save("Dialogue_Elf_Death", Lines("Fallen Elf",
                "At last... the thunder is quiet.",
                "Carry its strength beyond this ruined valley."));

            Save("Dialogue_DemonKin_Intro", Lines("Demon Kin",
                "You crossed the dark wood only to kneel before me.",
                "Come. Let the earth become your grave."));
            Save("Dialogue_DemonKin_Death", Lines("Demon Kin",
                "The abyss does not forgive defeat...",
                "The castle awaits you. It will finish what I could not."));

            Save("Dialogue_ArchDemon_Intro", Lines("Nameless Tyrant",
                "Every road you survived ends at my throne.",
                "There is nowhere left to run."));
            Save("Dialogue_ArchDemon_Death", Lines("Nameless Tyrant",
                "Impossible... the throne cannot fall...",
                "Go, then. See what remains when the last tyrant is gone."));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Boss Dialogue] Generated/updated 10 English dialogue assets in {Folder}.");
        }

        private static DialogueLine[] Lines(string speaker, params string[] text)
        {
            var lines = new DialogueLine[text.Length];
            for (int i = 0; i < text.Length; i++)
                lines[i] = new DialogueLine { speakerName = speaker, text = text[i] };
            return lines;
        }

        private static void Save(string name, DialogueLine[] lines)
        {
            string path = $"{Folder}/{name}.asset";
            var dialogue = AssetDatabase.LoadAssetAtPath<DialogueSO>(path);
            if (dialogue == null)
            {
                dialogue = ScriptableObject.CreateInstance<DialogueSO>();
                AssetDatabase.CreateAsset(dialogue, path);
            }
            dialogue.lines = lines;
            EditorUtility.SetDirty(dialogue);
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Data"))
                AssetDatabase.CreateFolder("Assets/_Project", "Data");
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Data/Dialogue"))
                AssetDatabase.CreateFolder("Assets/_Project/Data", "Dialogue");
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/_Project/Data/Dialogue", "Bosses");
        }
    }
}
