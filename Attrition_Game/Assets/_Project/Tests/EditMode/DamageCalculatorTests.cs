using Attrition.Core;
using NUnit.Framework;

namespace Attrition.Tests.EditMode
{
    /// <summary>
    /// DamageCalculator.Compute — Physical trừ DEF, Magic trừ RES, True bỏ qua cả hai,
    /// và mọi nhánh đều có sàn MinDamage = 1 (người chơi thấy "chưa đủ chỉ số", không phải 0 damage).
    /// </summary>
    public class DamageCalculatorTests
    {
        [Test]
        public void Physical_SubtractsDefenseOnly()
        {
            // RES = 50 phải bị bỏ qua: đòn vật lý chỉ ăn DEF.
            Assert.AreEqual(70, DamageCalculator.Compute(DamageType.Physical, 100, targetDef: 30, targetRes: 50));
        }

        [Test]
        public void Magic_SubtractsResistanceOnly()
        {
            Assert.AreEqual(60, DamageCalculator.Compute(DamageType.Magic, 100, targetDef: 30, targetRes: 40));
        }

        [Test]
        public void True_IgnoresBothDefenses()
        {
            Assert.AreEqual(100, DamageCalculator.Compute(DamageType.True, 100, targetDef: 999, targetRes: 999));
        }

        [TestCase(DamageType.Physical)]
        [TestCase(DamageType.Magic)]
        [TestCase(DamageType.True)]
        public void EveryType_ClampsToMinDamage(DamageType type)
        {
            // Phòng thủ cao hơn sát thương (hoặc raw <= 0) vẫn phải trả về đúng sàn, không âm và không 0.
            Assert.AreEqual(DamageCalculator.MinDamage,
                DamageCalculator.Compute(type, rawAmount: -5, targetDef: 500, targetRes: 500));
        }

        [Test]
        public void Physical_ExactlyLethalDefense_StillDealsMinDamage()
        {
            // raw == def là biên: 100 - 100 = 0 → phải nâng lên 1.
            Assert.AreEqual(1, DamageCalculator.Compute(DamageType.Physical, 100, targetDef: 100, targetRes: 0));
        }

        [Test]
        public void ZeroDefenses_ReturnRawAmount()
        {
            Assert.AreEqual(25, DamageCalculator.Compute(DamageType.Physical, 25, 0, 0));
            Assert.AreEqual(25, DamageCalculator.Compute(DamageType.Magic, 25, 0, 0));
        }
    }
}
