using System.Collections.Generic;
using Attrition.Core;
using Attrition.Data;
using Attrition.Systems;
using NUnit.Framework;
using UnityEngine;

namespace Attrition.Tests.EditMode
{
    /// <summary>
    /// StatSheet — chỉ số cuối = base (SO) + điểm tự cộng × perPoint + modifier trang bị/accessory,
    /// với web override thắng modifiers trong SO.
    /// </summary>
    public class StatSheetTests
    {
        private CharacterBaseStatsSO _baseStats;
        private LevelingConfigSO _leveling;
        private readonly List<ScriptableObject> _created = new();

        [SetUp]
        public void SetUp()
        {
            _baseStats = Make<CharacterBaseStatsSO>();
            _baseStats.baseHP = 100; _baseStats.baseMana = 100; _baseStats.baseStamina = 100;
            _baseStats.baseAD = 10; _baseStats.baseAP = 10; _baseStats.baseDEF = 10; _baseStats.baseRES = 10;

            _leveling = Make<LevelingConfigSO>();
            _leveling.maxLevel = 21; _leveling.statPointsPerLevel = 5;
            _leveling.hpPerPoint = 20; _leveling.manaPerPoint = 10; _leveling.staminaPerPoint = 5;
            _leveling.adPerPoint = 2; _leveling.apPerPoint = 2; _leveling.defPerPoint = 1; _leveling.resPerPoint = 1;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var so in _created) Object.DestroyImmediate(so);
            _created.Clear();
        }

        private T Make<T>() where T : ScriptableObject
        {
            var so = ScriptableObject.CreateInstance<T>();
            _created.Add(so);
            return so;
        }

        private StatSheet NewSheet() => new StatSheet(_baseStats, _leveling);

        private EquipmentSO Equipment(string itemId, params StatModifier[] mods)
        {
            var e = Make<EquipmentSO>();
            e.itemId = itemId;
            e.modifiers = mods;
            return e;
        }

        private static StatModifier Mod(StatType stat, int amount) => new StatModifier { stat = stat, amount = amount };

        [Test]
        public void FreshSheet_ReturnsBaseStatsAndLevelOne()
        {
            var sheet = NewSheet();

            Assert.AreEqual(1, sheet.Level);
            Assert.AreEqual(100, sheet.MaxHP);
            Assert.AreEqual(10, sheet.AD);
            Assert.AreEqual(0, sheet.UnspentPoints);   // level 1 chưa có điểm nào
        }

        [Test]
        public void UnspentPoints_GrowsWithLevel()
        {
            var sheet = NewSheet();
            sheet.SetLevel(5);

            // (5 - 1) * 5 điểm mỗi cấp.
            Assert.AreEqual(20, sheet.UnspentPoints);
        }

        [Test]
        public void SetLevel_ClampsToConfiguredRange()
        {
            var sheet = NewSheet();

            sheet.SetLevel(999);
            Assert.AreEqual(21, sheet.Level);

            sheet.SetLevel(-5);
            Assert.AreEqual(1, sheet.Level);
        }

        [Test]
        public void AllocatePoint_AppliesPerPointGrowth()
        {
            var sheet = NewSheet();
            sheet.SetLevel(2);

            Assert.IsTrue(sheet.AllocatePoint(StatType.MaxHP));

            Assert.AreEqual(120, sheet.MaxHP);       // 100 + 1 * 20
            Assert.AreEqual(4, sheet.UnspentPoints); // 5 - 1
        }

        [Test]
        public void AllocatePoint_FailsWhenNoPointsRemain()
        {
            var sheet = NewSheet();   // level 1 ⇒ 0 điểm

            Assert.IsFalse(sheet.AllocatePoint(StatType.AD));
            Assert.AreEqual(10, sheet.AD);
        }

        [Test]
        public void AllocatePoint_StopsExactlyWhenPointsAreExhausted()
        {
            var sheet = NewSheet();
            sheet.SetLevel(2);   // đúng 5 điểm

            for (int i = 0; i < 5; i++) Assert.IsTrue(sheet.AllocatePoint(StatType.DEF));

            Assert.AreEqual(0, sheet.UnspentPoints);
            Assert.IsFalse(sheet.AllocatePoint(StatType.DEF));
            Assert.AreEqual(15, sheet.DEF);   // 10 + 5 * 1
        }

        [Test]
        public void LoadAllocated_RestoresPointsFromSave()
        {
            var sheet = NewSheet();
            sheet.SetLevel(10);

            sheet.LoadAllocated(new Dictionary<StatType, int> { { StatType.AD, 3 }, { StatType.MaxHP, 2 } });

            Assert.AreEqual(16, sheet.AD);        // 10 + 3 * 2
            Assert.AreEqual(140, sheet.MaxHP);    // 100 + 2 * 20
            Assert.AreEqual(40, sheet.UnspentPoints); // (10-1)*5 - 5
        }

        [Test]
        public void LoadAllocated_WithNull_ClearsPreviousAllocation()
        {
            var sheet = NewSheet();
            sheet.SetLevel(5);
            sheet.AllocatePoint(StatType.AD);

            sheet.LoadAllocated(null);

            Assert.AreEqual(10, sheet.AD);
            Assert.AreEqual(20, sheet.UnspentPoints);
        }

        [Test]
        public void RebuildGear_SumsModifiersAcrossEquipment()
        {
            var sheet = NewSheet();
            var helm = Equipment("iron_helm", Mod(StatType.DEF, 5));
            var boots = Equipment("iron_boots", Mod(StatType.DEF, 3), Mod(StatType.AD, 4));

            sheet.RebuildGear(new[] { helm, boots }, null);

            Assert.AreEqual(18, sheet.DEF);   // 10 + 5 + 3
            Assert.AreEqual(14, sheet.AD);    // 10 + 4
        }

        [Test]
        public void RebuildGear_IgnoresNullEntries()
        {
            var sheet = NewSheet();

            sheet.RebuildGear(new EquipmentSO[] { null, Equipment("iron_helm", Mod(StatType.DEF, 5)) }, null);

            Assert.AreEqual(15, sheet.DEF);
        }

        [Test]
        public void RebuildGear_ReplacesPreviousGearInsteadOfAccumulating()
        {
            var sheet = NewSheet();
            var helm = Equipment("iron_helm", Mod(StatType.DEF, 5));

            sheet.RebuildGear(new[] { helm }, null);
            sheet.RebuildGear(new[] { helm }, null);

            Assert.AreEqual(15, sheet.DEF);   // không phải 20
        }

        [Test]
        public void RebuildGear_AppliesDamageAccessoryButSkipsAbilityGrant()
        {
            var sheet = NewSheet();

            var damage = Make<AccessorySO>();
            damage.itemId = "acc_burn";
            damage.kind = AccessoryKind.DamageEffect;
            damage.modifiers = new[] { Mod(StatType.AP, 7) };

            var ability = Make<AccessorySO>();
            ability.itemId = "acc_double_jump";
            ability.kind = AccessoryKind.AbilityGrant;
            ability.modifiers = new[] { Mod(StatType.AP, 100) };

            sheet.RebuildGear(null, new[] { damage, ability });

            Assert.AreEqual(17, sheet.AP);   // chỉ DamageEffect được cộng
        }

        [Test]
        public void RebuildGear_WebOverrideReplacesScriptableObjectModifiers()
        {
            var sheet = NewSheet();
            var helm = Equipment("iron_helm", Mod(StatType.DEF, 5));
            var overrides = new Dictionary<string, StatModifier[]>
            {
                { "iron_helm", new[] { Mod(StatType.DEF, 50) } }
            };

            sheet.RebuildGear(new[] { helm }, null, overrides);

            Assert.AreEqual(60, sheet.DEF);   // 10 + 50, giá trị SO bị bỏ qua
        }

        [Test]
        public void RebuildGear_OverrideForOtherItem_LeavesModifiersUntouched()
        {
            var sheet = NewSheet();
            var helm = Equipment("iron_helm", Mod(StatType.DEF, 5));
            var overrides = new Dictionary<string, StatModifier[]>
            {
                { "steel_helm", new[] { Mod(StatType.DEF, 999) } }
            };

            sheet.RebuildGear(new[] { helm }, null, overrides);

            Assert.AreEqual(15, sheet.DEF);
        }

        [Test]
        public void Get_NeverReturnsNegativeValue()
        {
            var sheet = NewSheet();

            sheet.RebuildGear(new[] { Equipment("cursed_ring", Mod(StatType.DEF, -500)) }, null);

            Assert.AreEqual(0, sheet.DEF);
        }

        [Test]
        public void Get_CombinesBaseAllocatedAndGear()
        {
            var sheet = NewSheet();
            sheet.SetLevel(3);
            sheet.AllocatePoint(StatType.AD);          // +2
            sheet.RebuildGear(new[] { Equipment("blade", Mod(StatType.AD, 6)) }, null);

            Assert.AreEqual(18, sheet.AD);             // 10 + 2 + 6
        }

        [Test]
        public void MoveSpeed_HasNoPerPointGrowth()
        {
            var sheet = NewSheet();
            sheet.SetLevel(5);
            sheet.LoadAllocated(new Dictionary<StatType, int> { { StatType.MoveSpeed, 4 } });

            // MoveSpeed không có base trong SO và không có perPoint ⇒ chỉ đến từ gear.
            Assert.AreEqual(0, sheet.Get(StatType.MoveSpeed));
        }
    }
}
