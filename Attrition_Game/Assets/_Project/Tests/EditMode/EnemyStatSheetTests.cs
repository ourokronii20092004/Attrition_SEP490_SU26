using Attrition.Data;
using Attrition.Systems;
using NUnit.Framework;
using UnityEngine;

namespace Attrition.Tests.EditMode
{
    /// <summary>
    /// EnemyStatSheet.Build — chỉ số quái = SO mặc định ⊕ override từ web ⊕ coop scaling.
    /// BR-20: coop nhân MaxHP đúng 50%. BR-22: coop nhân Poise đúng 50%.
    /// </summary>
    public class EnemyStatSheetTests
    {
        private EnemyStatsSO _so;

        [SetUp]
        public void SetUp()
        {
            // ScriptableObject dựng trong bộ nhớ — EditMode test không cần asset hay scene.
            _so = ScriptableObject.CreateInstance<EnemyStatsSO>();
            _so.maxHP = 100; _so.ad = 20; _so.ap = 10; _so.def = 5; _so.res = 3; _so.poise = 40;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_so);

        [Test]
        public void NoOverride_Solo_UsesScriptableObjectDefaults()
        {
            var sheet = EnemyStatSheet.Build(_so, null, isCoop: false);
            Assert.AreEqual(100, sheet.MaxHP);
            Assert.AreEqual(20, sheet.AD);
            Assert.AreEqual(10, sheet.AP);
            Assert.AreEqual(5, sheet.DEF);
            Assert.AreEqual(3, sheet.RES);
            Assert.AreEqual(40, sheet.Poise);
        }

        [Test]
        public void Override_WinsOverScriptableObject()
        {
            var ovr = new EnemyStatOverride { maxHP = 250, ad = 44, def = 9 };
            var sheet = EnemyStatSheet.Build(_so, ovr, isCoop: false);

            Assert.AreEqual(250, sheet.MaxHP);
            Assert.AreEqual(44, sheet.AD);
            Assert.AreEqual(9, sheet.DEF);
            // Field không override phải giữ giá trị SO, không bị reset về 0.
            Assert.AreEqual(10, sheet.AP);
            Assert.AreEqual(3, sheet.RES);
            Assert.AreEqual(40, sheet.Poise);
        }

        [Test]
        public void Coop_ScalesHpAndPoiseByFiftyPercent()
        {
            var sheet = EnemyStatSheet.Build(_so, null, isCoop: true);

            Assert.AreEqual(150, sheet.MaxHP);   // BR-20
            Assert.AreEqual(60, sheet.Poise);    // BR-22
        }

        [Test]
        public void Coop_DoesNotScaleOffensiveOrDefensiveStats()
        {
            var sheet = EnemyStatSheet.Build(_so, null, isCoop: true);

            Assert.AreEqual(20, sheet.AD);
            Assert.AreEqual(10, sheet.AP);
            Assert.AreEqual(5, sheet.DEF);
            Assert.AreEqual(3, sheet.RES);
        }

        [Test]
        public void Coop_ScalesTheOverriddenValueNotTheDefault()
        {
            var sheet = EnemyStatSheet.Build(_so, new EnemyStatOverride { maxHP = 200, poise = 10 }, isCoop: true);

            Assert.AreEqual(300, sheet.MaxHP);
            Assert.AreEqual(15, sheet.Poise);
        }

        [Test]
        public void Coop_RoundsHalfValuesToNearestInt()
        {
            // 101 * 1.5 = 151.5 → RoundToInt dùng banker's rounding của Unity ⇒ 152.
            var sheet = EnemyStatSheet.Build(_so, new EnemyStatOverride { maxHP = 101 }, isCoop: true);
            Assert.AreEqual(Mathf.RoundToInt(101 * 1.5f), sheet.MaxHP);
        }

        [Test]
        public void MaxHp_NeverDropsBelowOne()
        {
            var sheet = EnemyStatSheet.Build(_so, new EnemyStatOverride { maxHP = 0 }, isCoop: false);
            Assert.AreEqual(1, sheet.MaxHP);
        }

        [Test]
        public void NegativeOverrides_ClampToZeroExceptHp()
        {
            var ovr = new EnemyStatOverride { maxHP = -50, ad = -1, ap = -1, def = -1, res = -1, poise = -1 };
            var sheet = EnemyStatSheet.Build(_so, ovr, isCoop: false);

            Assert.AreEqual(1, sheet.MaxHP);
            Assert.AreEqual(0, sheet.AD);
            Assert.AreEqual(0, sheet.AP);
            Assert.AreEqual(0, sheet.DEF);
            Assert.AreEqual(0, sheet.RES);
            Assert.AreEqual(0, sheet.Poise);
        }
    }
}
