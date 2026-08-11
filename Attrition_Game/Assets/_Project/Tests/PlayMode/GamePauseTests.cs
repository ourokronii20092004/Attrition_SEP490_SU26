using System.Collections;
using Attrition.Persistence;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Attrition.Tests.PlayMode
{
    /// <summary>
    /// GamePause.SetSoloFreeze — cộng dồn lý do đóng băng: game chỉ đứng hình khi CÒN ÍT NHẤT một lý do.
    /// Đây là PlayMode test vì logic ghi Time.timeScale (không chạy được trong EditMode).
    /// Khớp function TMPM "Toggle Main / Pause Menu" trong report.
    /// </summary>
    public class GamePauseTests
    {
        private LaunchMode _origMode;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _origMode = GameLaunch.Mode;
            GameLaunch.Mode = LaunchMode.Solo;
            GamePause.ResetFreeze();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GamePause.ResetFreeze();
            GameLaunch.Mode = _origMode;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Freezing_OnFirstReason_SetsTimeScaleToZero()
        {
            GamePause.SetSoloFreeze(GamePause.Freeze.Overlay, true);

            Assert.IsTrue(GamePause.IsPaused);
            Assert.AreEqual(0f, Time.timeScale);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Unfreezing_LastReason_RestoresTimeScaleToOne()
        {
            GamePause.SetSoloFreeze(GamePause.Freeze.Overlay, true);
            GamePause.SetSoloFreeze(GamePause.Freeze.Overlay, false);

            Assert.IsFalse(GamePause.IsPaused);
            Assert.AreEqual(1f, Time.timeScale);
            yield return null;
        }

        [UnityTest]
        public IEnumerator NestedReasons_KeepFrozen_UntilAllClosed()
        {
            GamePause.SetSoloFreeze(GamePause.Freeze.Overlay, true);
            GamePause.SetSoloFreeze(GamePause.Freeze.Dialogue, true);

            // Đóng overlay nhưng còn dialogue → vẫn đứng hình.
            GamePause.SetSoloFreeze(GamePause.Freeze.Overlay, false);
            Assert.IsTrue(GamePause.IsPaused);
            Assert.AreEqual(0f, Time.timeScale);

            // Đóng nốt dialogue → hết đứng hình.
            GamePause.SetSoloFreeze(GamePause.Freeze.Dialogue, false);
            Assert.IsFalse(GamePause.IsPaused);
            Assert.AreEqual(1f, Time.timeScale);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ResetFreeze_ClearsAllReasons()
        {
            GamePause.SetSoloFreeze(GamePause.Freeze.Overlay, true);
            GamePause.SetSoloFreeze(GamePause.Freeze.WorldMap, true);

            GamePause.ResetFreeze();

            Assert.IsFalse(GamePause.IsPaused);
            Assert.AreEqual(1f, Time.timeScale);
            yield return null;
        }
    }
}
