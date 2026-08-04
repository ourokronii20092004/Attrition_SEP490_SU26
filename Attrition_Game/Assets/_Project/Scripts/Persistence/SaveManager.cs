using System;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Attrition.Persistence
{
    /// <summary>Tiến trình 1 quest đã lưu — khớp lại NPC qua questId khi load.</summary>
    [Serializable]
    public class QuestProgressEntry
    {
        public string questId;
        public byte state;     // 0=NotStarted, 1=Active, 2=Completed, 3=Rewarded
        public int progress;   // số mục tiêu đã hoàn thành
        public int targetMask; // multi-target kill quest: bit các enemyId đã hạ
    }

    /// <summary>Bọc mảng quest để (de)serialize JSON ổn định (Unity/Newtonsoft) cho lưu server coop.</summary>
    [Serializable]
    public class QuestProgressList
    {
        public QuestProgressEntry[] quests;
    }

    [Serializable]
    public class SaveSlotData
    {
        public string characterName;
        public int level;
        public string location;
        public string playtime;
        public int deaths;
        public int avatarColorIndex; // 0=purple, 1=blue, etc.

        public int currentExp;
        public int currentHP;
        public int currentMana;
        public string checkpointId;       // checkpoint cuối đã rest
        public string checkpointScene;    // scene chứa checkpoint cuối (Metroidvania: chỉ spawn tại đây nếu đúng scene)
        public float checkpointX;
        public float checkpointY;
        public float checkpointZ;
        public int playtimeSeconds;       // nguồn thật; `playtime` chỉ để hiển thị
        public int potionMaxFlasks;       // số bình máu tối đa
        public int potionMaxManaFlasks;   // số bình mana tối đa
        public int healthCharges;         // số lượng bình máu hiện có
        public int manaCharges;           // số lượng bình mana hiện có
        public int[] allocatedPoints;     // 7 chỉ số tự cộng (Option 2)
        public long lastSavedUnix;        // mốc lưu gần nhất
        public string originMode;         // "Solo" | "Coop" — chặn dùng chéo chế độ
        public QuestProgressEntry[] quests; // tiến trình quest NPC (khớp lại qua questId)

        public List<string> discoveredCheckpoints = new List<string>(); // id (DisplayName) các checkpoint ĐÃ REST
        public List<string> fogVisited = new List<string>();            // "scene:cellX:cellY" các ô fog đã xua
        public List<string> defeatedBosses = new List<string>();        // bossId các boss ĐÃ HẠ (không hồi sinh lại)
        public List<string> brokenObjects = new List<string>();          // "scene@x,y" vật phá được ĐÃ VỠ (không spawn lại)
        public List<string> lootedElites = new List<string>();            // "scene|enemyId@x,y" elite/boss ĐÃ rơi đồ (không rơi lần hai)

        public string ToDisplayPlaytime()
        {
            int h = playtimeSeconds / 3600;
            int m = (playtimeSeconds % 3600) / 60;
            return $"{h:00}:{m:00}";
        }
    }

    public static class SaveManager
    {
        /// <summary>Số save slot tối đa. Đổi 1 chỗ này là toàn bộ UI + load/save theo.</summary>
        public const int SlotCount = 8;

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
            SaveSlotData[] slots = new SaveSlotData[SlotCount];
            for (int i = 0; i < SlotCount; i++)
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

        /// <summary>
        /// File inventory của 1 save slot SOLO. PlayerInventory ghi riêng ra đây (không nhét vào
        /// slot_N.json), nên xoá nhân vật phải xoá cả file này — nếu không, tạo lại ở CÙNG slot sẽ
        /// nạp nguyên túi đồ + trang bị của nhân vật cũ.
        /// </summary>
        public static string SoloInventoryPath(int slotIndex)
            => Path.Combine(Application.persistentDataPath, $"inventory_solo_{slotIndex}.json");

        /// <summary>Xoá đúng dữ liệu tại một slot, không thay đổi chỉ số các slot khác.</summary>
        public static void ClearSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount) return;
            TryDelete(Path.Combine(SaveDirectory, $"slot_{slotIndex}.json"));
            TryDelete(SoloInventoryPath(slotIndex));
        }

        /// <summary>Xoá slot solo và dồn toàn bộ slot phía sau lên, giữ inventory đi cùng nhân vật.</summary>
        public static void DeleteSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount) return;

            ClearSlot(slotIndex);

            int destination = slotIndex;
            for (int source = slotIndex + 1; source < SlotCount; source++)
            {
                string sourceSave = Path.Combine(SaveDirectory, $"slot_{source}.json");
                if (!File.Exists(sourceSave)) continue;

                MoveReplacing(sourceSave, Path.Combine(SaveDirectory, $"slot_{destination}.json"));
                MoveReplacing(SoloInventoryPath(source), SoloInventoryPath(destination));
                destination++;
            }

            for (int i = destination; i < SlotCount; i++) ClearSlot(i);
        }

        private static void MoveReplacing(string source, string destination)
        {
            try
            {
                if (File.Exists(destination)) File.Delete(destination);
                if (File.Exists(source)) File.Move(source, destination);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error moving '{source}' to '{destination}': {e.Message}");
            }
        }

        private static void TryDelete(string filePath)
        {
            try
            {
                if (File.Exists(filePath)) File.Delete(filePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error deleting '{filePath}': {e.Message}");
            }
        }

        /// <summary>
        /// Save slot có dùng được ở chế độ đang chọn không? Save Solo KHÔNG mở ở Coop và ngược lại.
        /// Slot trống (chưa có data) hoặc chưa gắn originMode (save cũ) → cho phép, coi như tương thích.
        /// </summary>
        public static bool IsSlotCompatible(int slotIndex, LaunchMode mode)
        {
            var data = LoadSlot(slotIndex);
            if (data == null || string.IsNullOrEmpty(data.originMode)) return true;
            return data.originMode == mode.ToString();
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
