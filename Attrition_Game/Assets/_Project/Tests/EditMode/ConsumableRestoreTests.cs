using Attrition.Data;
using NUnit.Framework;
using UnityEngine;

namespace Attrition.Tests.EditMode
{
    /// <summary>
    /// ConsumableSO.ComputeRestore — lượng hồi bình (report DCQ: dùng bình trừ số lượng, hồi
    /// theo đúng trị số). Tổng = flatRestore + percentOfMax * maxValue, làm tròn cho phần %.
    /// </summary>
    public class ConsumableRestoreTests
    {
        private static ConsumableSO MakeConsumable(int flat, float percent)
        {
            var c = ScriptableObject.CreateInstance<ConsumableSO>();
            c.flatRestore = flat;
            c.percentOfMax = percent;
            return c;
        }

        [Test]
        public void FlatOnly_RestoresFixedAmount()
        {
            var c = MakeConsumable(flat: 50, percent: 0f);
            Assert.AreEqual(50, c.ComputeRestore(maxValue: 100));
            Assert.AreEqual(50, c.ComputeRestore(maxValue: 500));
            Object.DestroyImmediate(c);
        }

        [Test]
        public void PercentOnly_ScalesWithMax()
        {
            var c = MakeConsumable(flat: 0, percent: 0.25f);
            Assert.AreEqual(25, c.ComputeRestore(maxValue: 100));
            Assert.AreEqual(50, c.ComputeRestore(maxValue: 200));
            Object.DestroyImmediate(c);
        }

        [Test]
        public void FlatPlusPercent_SumsBoth()
        {
            var c = MakeConsumable(flat: 30, percent: 0.2f);
            Assert.AreEqual(70, c.ComputeRestore(maxValue: 200)); // 30 + 40
            Object.DestroyImmediate(c);
        }

        [Test]
        public void FractionalPercent_RoundsToNearestInt()
        {
            var c = MakeConsumable(flat: 0, percent: 0.333f);
            Assert.AreEqual(33, c.ComputeRestore(maxValue: 100)); // 33.3 → 33
            Object.DestroyImmediate(c);
        }

        [Test]
        public void ZeroRestore_ReturnsZero()
        {
            var c = MakeConsumable(flat: 0, percent: 0f);
            Assert.AreEqual(0, c.ComputeRestore(maxValue: 100));
            Object.DestroyImmediate(c);
        }
    }
}
