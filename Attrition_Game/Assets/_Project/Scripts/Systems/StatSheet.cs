using System.Collections.Generic;
using UnityEngine;
using Attrition.Core;
using Attrition.Data;

namespace Attrition.Systems
{
    /// <summary>
    /// Lớp runtime gộp chỉ số cuối của 1 nhân vật từ 4 nguồn:
    ///   1. base    — CharacterBaseStatsSO (STATIC, level 1)
    ///   2. allocated — điểm người chơi tự cộng (DYNAMIC, Option 2, lưu DB)
    ///   3. equipment — modifiers từ 4 món đang mặc
    ///   4. accessory — modifiers từ damage-accessory đang trang bị
    /// Đây là nguồn chỉ số DUY NHẤT mà gameplay đọc — không hard-code ở Controller.
    /// </summary>
    public struct LevelingConfig
    {
        public int maxLevel;
        public int statPointsPerLevel;
        public int hpPerPoint;
        public int manaPerPoint;
        public int staminaPerPoint;
        public int adPerPoint;
        public int apPerPoint;
        public int defPerPoint;
        public int resPerPoint;
    }

    public class StatSheet
    {
        private readonly CharacterBaseStatsSO _baseStats;
        private readonly LevelingConfig _leveling;
        private int _level = 1;

        // điểm tự cộng (Option 2): stat -> số điểm đã đầu tư
        private readonly Dictionary<StatType, int> _allocated = new();
        // tổng modifier phẳng từ trang bị + accessory
        private readonly Dictionary<StatType, int> _gearFlat = new();

        public StatSheet(CharacterBaseStatsSO baseStats, LevelingConfig leveling)
        {
            _baseStats = baseStats;
            _leveling = leveling;
        }

        public int Level => _level;
        public CharacterBaseStatsSO Config => _baseStats;

        /// <summary>Số điểm chưa tiêu = điểm tích lũy tới level - điểm đã cộng.</summary>
        public int UnspentPoints
        {
            get
            {
                int spent = 0;
                foreach (var kv in _allocated) spent += kv.Value;
                int clamped = Mathf.Clamp(_level, 1, _leveling.maxLevel);
                int totalPoints = (clamped - 1) * _leveling.statPointsPerLevel;
                return totalPoints - spent;
            }
        }

        public void SetLevel(int level) => _level = Mathf.Clamp(level, 1, _leveling.maxLevel);

        /// <summary>Cộng 1 điểm tự phân bổ vào stat (Option 2). Trả false nếu hết điểm.</summary>
        public bool AllocatePoint(StatType stat)
        {
            if (UnspentPoints <= 0) return false;
            _allocated.TryGetValue(stat, out int cur);
            _allocated[stat] = cur + 1;
            return true;
        }

        /// <summary>Nạp trực tiếp bản đồ điểm đã cộng (khi load từ DB).</summary>
        public void LoadAllocated(IReadOnlyDictionary<StatType, int> allocated)
        {
            _allocated.Clear();
            if (allocated == null) return;
            foreach (var kv in allocated) _allocated[kv.Key] = kv.Value;
        }

        public IReadOnlyDictionary<StatType, int> Allocated => _allocated;

        // ─── Trang bị / accessory: rebuild toàn bộ gear flat mỗi lần đổi đồ ───
        public void RebuildGear(IEnumerable<EquipmentSO> equipped, IEnumerable<AccessorySO> damageAccessories)
        {
            _gearFlat.Clear();
            if (equipped != null)
                foreach (var e in equipped)
                    if (e != null) AddModifiers(e.modifiers);

            if (damageAccessories != null)
                foreach (var a in damageAccessories)
                    if (a != null && a.kind == AccessoryKind.DamageEffect) AddModifiers(a.modifiers);
        }

        private void AddModifiers(StatModifier[] mods)
        {
            if (mods == null) return;
            foreach (var m in mods)
            {
                _gearFlat.TryGetValue(m.stat, out int cur);
                _gearFlat[m.stat] = cur + m.amount;
            }
        }

        /// <summary>Chỉ số cuối cùng = base + allocated*perPoint + gear.</summary>
        public int Get(StatType stat)
        {
            int value = _baseStats.GetBase(stat);

            _allocated.TryGetValue(stat, out int points);
            value += points * PerPoint(stat);

            _gearFlat.TryGetValue(stat, out int gear);
            value += gear;

            return Mathf.Max(0, value);
        }

        private int PerPoint(StatType stat)
        {
            switch (stat)
            {
                case StatType.MaxHP: return _leveling.hpPerPoint;
                case StatType.MaxMana: return _leveling.manaPerPoint;
                case StatType.MaxStamina: return _leveling.staminaPerPoint;
                case StatType.AD: return _leveling.adPerPoint;
                case StatType.AP: return _leveling.apPerPoint;
                case StatType.DEF: return _leveling.defPerPoint;
                case StatType.RES: return _leveling.resPerPoint;
                default: return 0;
            }
        }

        // Tiện ích đọc nhanh
        public int MaxHP => Get(StatType.MaxHP);
        public int MaxMana => Get(StatType.MaxMana);
        public int MaxStamina => Get(StatType.MaxStamina);
        public int AD => Get(StatType.AD);
        public int AP => Get(StatType.AP);
        public int DEF => Get(StatType.DEF);
        public int RES => Get(StatType.RES);
    }
}
