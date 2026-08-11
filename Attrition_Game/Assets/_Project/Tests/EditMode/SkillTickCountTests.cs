using Attrition.Data;
using Attrition.Persistence;
using NUnit.Framework;
using UnityEngine;

namespace Attrition.Tests.EditMode
{
    /// <summary>
    /// ComputeTickCount — số lần skill gây damage trong active window.
    /// tickInterval = 0 nghĩa là "1 hit"; ngược lại số tick = floor(activeDuration / tickInterval) + 1,
    /// và luôn >= 1 để skill không bao giờ trở thành vô hại.
    /// Chỉ ComputeTickCount được test: SkillRuntimeConfig.From gọi SkillConfigProvider.Instance (singleton scene).
    /// </summary>
    public class SkillTickCountTests
    {
        private static SkillRuntimeConfig Config(float castTime, float start, float end, float tickInterval) =>
            new SkillRuntimeConfig
            {
                castTime = castTime,
                activeStartFrac = start,
                activeEndFrac = end,
                tickInterval = tickInterval
            };

        [Test]
        public void ZeroTickInterval_MeansSingleHit()
        {
            Assert.AreEqual(1, Config(1f, 0f, 1f, tickInterval: 0f).ComputeTickCount());
        }

        [Test]
        public void NegativeTickInterval_MeansSingleHit()
        {
            Assert.AreEqual(1, Config(1f, 0f, 1f, tickInterval: -0.5f).ComputeTickCount());
        }

        [Test]
        public void MultiHit_CountsTicksAcrossActiveWindow()
        {
            // activeDuration = (1 - 0) * 2 = 2s, tick mỗi 0.5s ⇒ floor(4) + 1 = 5 hit.
            Assert.AreEqual(5, Config(2f, 0f, 1f, tickInterval: 0.5f).ComputeTickCount());
        }

        [Test]
        public void PartialActiveWindow_OnlyCountsTheActivePortion()
        {
            // activeDuration = (0.75 - 0.25) * 4 = 2s, tick 1s ⇒ floor(2) + 1 = 3 hit.
            Assert.AreEqual(3, Config(4f, 0.25f, 0.75f, tickInterval: 1f).ComputeTickCount());
        }

        [Test]
        public void RemainderIsDiscarded_NotRoundedUp()
        {
            // activeDuration = 1s, tick 0.3s ⇒ floor(3.33) + 1 = 4 hit.
            Assert.AreEqual(4, Config(1f, 0f, 1f, tickInterval: 0.3f).ComputeTickCount());
        }

        [Test]
        public void TickLongerThanWindow_StillDealsOneHit()
        {
            Assert.AreEqual(1, Config(1f, 0f, 0.1f, tickInterval: 5f).ComputeTickCount());
        }

        [Test]
        public void InvertedActiveWindow_ClampsDurationToZero()
        {
            // end < start là cấu hình sai; Mathf.Max(0, ...) giữ kết quả ở 1 thay vì âm.
            Assert.AreEqual(1, Config(2f, 0.8f, 0.2f, tickInterval: 0.1f).ComputeTickCount());
        }

        [Test]
        public void ZeroCastTime_StillDealsOneHit()
        {
            Assert.AreEqual(1, Config(0f, 0f, 1f, tickInterval: 0.1f).ComputeTickCount());
        }

        [Test]
        public void SkillScriptableObject_UsesTheSameFormulaAsRuntimeConfig()
        {
            var so = ScriptableObject.CreateInstance<SkillSO>();
            try
            {
                so.castTime = 2f;
                so.activeStartFrac = 0f;
                so.activeEndFrac = 1f;
                so.tickInterval = 0.5f;

                Assert.AreEqual(5, so.ComputeTickCount());
                Assert.AreEqual(Config(2f, 0f, 1f, 0.5f).ComputeTickCount(), so.ComputeTickCount());
            }
            finally
            {
                Object.DestroyImmediate(so);
            }
        }

        [Test]
        public void SkillScriptableObject_DefaultTickInterval_IsSingleHit()
        {
            var so = ScriptableObject.CreateInstance<SkillSO>();
            try
            {
                // Mặc định tickInterval = 0 ⇒ skill 1 hit.
                Assert.AreEqual(1, so.ComputeTickCount());
            }
            finally
            {
                Object.DestroyImmediate(so);
            }
        }
    }
}
