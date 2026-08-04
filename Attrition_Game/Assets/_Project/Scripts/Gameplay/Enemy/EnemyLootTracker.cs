using System.Collections.Generic;
using UnityEngine;

namespace Attrition.Controllers
{
    /// <summary>
    /// Theo dõi các Elite/Boss ĐÃ RƠI ĐỒ — mỗi chỗ chỉ thưởng ĐÚNG MỘT LẦN cho mỗi lần chơi.
    /// Quay lại đánh nữa vẫn được EXP (EnemyController.DieFinal cộng EXP vô điều kiện) nhưng KHÔNG rơi
    /// vật phẩm lần hai.
    ///
    /// BỀN qua load scene + out/vào game — cùng mô hình <see cref="Attrition.Gameplay.Environment.BossDefeatState"/>:
    ///   - SOLO: nạp từ save slot + ghi ngược vào slot.
    ///   - COOP: chỉ giữ trong phiên của host (không đụng API/DB).
    ///
    /// VÌ SAO PHẢI BỀN: bản cũ xoá sạch mỗi lần `sceneLoaded`, nên đi sang map khác rồi quay lại là farm
    /// được. Với NightBorne (skill_water + acc_lifesteal + 3 món amethyst) và Undead (4 món diamond) —
    /// đều `normalDropChance: 1` — nghĩa là nhân bản được cả SKILL và ACCESSORY tiến trình chỉ bằng cách
    /// đi ra đi vào map.
    ///
    /// Khoá = "scene|enemyId@x,y" (vị trí spawn làm tròn). Có TÊN SCENE vì hai map khác nhau hoàn toàn có
    /// thể đặt cùng enemyId ở cùng toạ độ làm tròn — thiếu scene thì con ở map sau bị coi là đã rơi đồ.
    /// Chính vì khoá cũ thiếu scene mà bản cũ buộc phải xoá theo scene để tránh nhầm.
    /// </summary>
    public static class EnemyLootTracker
    {
        private static readonly HashSet<string> _looted = new();

        private static string Key(string scene, string enemyId, Vector3 spawnPos)
            => $"{scene}|{enemyId}@{Mathf.RoundToInt(spawnPos.x)},{Mathf.RoundToInt(spawnPos.y)}";

        public static bool AlreadyLooted(string enemyId, Vector3 spawnPos)
        {
            EnsureLoadedForSolo();
            return _looted.Contains(Key(CurrentScene, enemyId, spawnPos));
        }

        /// <summary>Đánh dấu đã rơi đồ. Trả true nếu MỚI (để caller biết có cần lưu).</summary>
        public static bool MarkLooted(string enemyId, Vector3 spawnPos)
        {
            EnsureLoadedForSolo();
            return _looted.Add(Key(CurrentScene, enemyId, spawnPos));
        }

        // Scene gameplay hiện hành. Dùng GameLaunch (đáng tin cả khi Fusion giữ active scene ở menu/map cũ)
        // rồi mới fallback về active scene — cùng cách BossGateController/Checkpoint xác định scene.
        private static string CurrentScene
        {
            get
            {
                string s = Attrition.Persistence.GameLaunch.GameplayScene;
                return !string.IsNullOrEmpty(s)
                    ? s
                    : UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            }
        }

        //  ─── LƯU / NẠP ───

        public static void LoadFrom(Attrition.Persistence.SaveSlotData data)
        {
            _looted.Clear();
            if (data?.lootedElites == null) return;
            foreach (var k in data.lootedElites)
                if (!string.IsNullOrEmpty(k)) _looted.Add(k);
        }

        public static void WriteTo(Attrition.Persistence.SaveSlotData data)
        {
            if (data == null) return;
            data.lootedElites = new List<string>(_looted);
        }

        // Slot đã nạp (-1 = chưa nạp). Nạp LAZY đúng 1 lần cho mỗi slot.
        private static int _loadedSlot = -1;

        /// <summary>
        /// Đảm bảo đã nạp từ save slot hiện tại (solo). Gọi lazy từ AlreadyLooted/MarkLooted vì không có
        /// điểm khởi tạo nào chắc chắn chạy trước khi quái đầu tiên chết. Nạp lại khi đổi slot.
        /// Coop: no-op, chỉ giữ trong phiên host.
        /// </summary>
        public static void EnsureLoadedForSolo()
        {
            if (Attrition.Persistence.GameLaunch.IsOnline) return;

            int slot = Attrition.Persistence.GameLaunch.SelectedSlot;
            if (_loadedSlot == slot) return;
            _loadedSlot = slot;

            LoadFrom(Attrition.Persistence.SaveManager.LoadSlot(slot));
        }

        /// <summary>
        /// Xoá sạch (xoá save / bắt đầu game mới). Reset cả cờ đã-nạp — nếu không, tạo game mới ở CÙNG
        /// slot trong cùng phiên sẽ giữ danh sách cũ (elite coi như đã rơi đồ dù là game mới).
        /// </summary>
        public static void Clear()
        {
            _looted.Clear();
            _loadedSlot = -1;
        }
    }
}
