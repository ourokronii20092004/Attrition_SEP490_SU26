using System;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Attrition.Persistence
{
    [Serializable]
    public class SaveSlotData
    {
        public string characterName;
        public int level;
        public string location;
        public string playtime;
        public int deaths;
        public int avatarColorIndex; // 0=purple, 1=blue, etc.
    }

    public static class SaveManager
    {
        private static string SaveDirectory => Path.Combine(Application.persistentDataPath, "saves");

        public static void EnsureDirectoryExists()
        {
            if (!Directory.Exists(SaveDirectory))
            {
                Directory.CreateDirectory(SaveDirectory);
            }
        }

        public static SaveSlotData LoadSlot(int slotIndex)
        {
            EnsureDirectoryExists();
            string filePath = Path.Combine(SaveDirectory, $"slot_{slotIndex}.json");
            
            if (File.Exists(filePath))
            {
                try
                {
                    string json = File.ReadAllText(filePath);
                    return JsonConvert.DeserializeObject<SaveSlotData>(json);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error loading save slot {slotIndex}: {e.Message}");
                    return null;
                }
            }
            return null;
        }

        public static SaveSlotData[] LoadAllSlots()
        {
            SaveSlotData[] slots = new SaveSlotData[3];
            for (int i = 0; i < 3; i++)
            {
                slots[i] = LoadSlot(i);
            }
            return slots;
        }

        public static void SaveSlot(int slotIndex, SaveSlotData data)
        {
            EnsureDirectoryExists();
            string filePath = Path.Combine(SaveDirectory, $"slot_{slotIndex}.json");
            
            try
            {
                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(filePath, json);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error saving slot {slotIndex}: {e.Message}");
            }
        }

        public static void DeleteSlot(int slotIndex)
        {
            string filePath = Path.Combine(SaveDirectory, $"slot_{slotIndex}.json");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }

        // --- Mock generator cho testing ---
        public static void CreateMockDataIfNeeded()
        {
            EnsureDirectoryExists();
            if (!File.Exists(Path.Combine(SaveDirectory, "slot_0.json")))
            {
                SaveSlot(0, new SaveSlotData { characterName = "Kael the Undying", level = 42, location = "Ember Citadel", playtime = "24:17", deaths = 188, avatarColorIndex = 0 });
            }
            if (!File.Exists(Path.Combine(SaveDirectory, "slot_1.json")))
            {
                SaveSlot(1, new SaveSlotData { characterName = "Saria Ashborne", level = 18, location = "Fungal Caverns", playtime = "08:44", deaths = 63, avatarColorIndex = 1 });
            }
        }
    }
}
