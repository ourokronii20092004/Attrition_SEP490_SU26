using Attrition.Persistence;
using NUnit.Framework;
using UnityEngine;

namespace Attrition.Tests.EditMode
{
    /// <summary>
    /// GameSettings — cài đặt âm lượng / gameplay / đồ hoạ / phím tắt (report RCAA).
    /// Kiểm tra clamp biên (volume 0..1), fallback resolution khi giá trị vô lý,
    /// danh sách frame rate hợp lệ, và rebind phím roundtrip.
    /// </summary>
    public class GameSettingsTests
    {
        [SetUp]
        public void SetUp() => GameSettings.ResetToDefault();

        [TearDown]
        public void TearDown() => GameSettings.ResetToDefault();

        [Test]
        public void SetAudio_ClampsVolumeToUnitRange()
        {
            // 1.5 → 1, -1 → 0, 0.5 giữ nguyên.
            GameSettings.SetAudio(1.5f, -1f, 0.5f);

            Assert.AreEqual(1f, GameSettings.MasterVolume);
            Assert.AreEqual(0f, GameSettings.MusicVolume);
            Assert.AreEqual(0.5f, GameSettings.SfxVolume);
        }

        [Test]
        public void SetGraphics_InvalidResolution_FallsBackToDefaults()
        {
            // width/height <= 0 → 1920x1080.
            GameSettings.SetGraphics(0, -100, FullScreenMode.FullScreenWindow, true, 144, 3);

            Assert.AreEqual(1920, GameSettings.ResolutionWidth);
            Assert.AreEqual(1080, GameSettings.ResolutionHeight);
        }

        [Test]
        public void SetGraphics_InvalidFullScreenMode_FallsBackToFullScreenWindow()
        {
            // (FullScreenMode)999 không phải enum hợp lệ → fallback FullScreenWindow.
            GameSettings.SetGraphics(1280, 720, (FullScreenMode)999, true, 144, 3);

            Assert.AreEqual(FullScreenMode.FullScreenWindow, GameSettings.DisplayMode);
        }

        [Test]
        public void SetGraphics_FrameLimit_OnlyAcceptsWhitelistedValues()
        {
            GameSettings.SetGraphics(1920, 1080, FullScreenMode.FullScreenWindow, true, frameLimit: 60, 3);
            Assert.AreEqual(60, GameSettings.FrameLimit);

            // 300 không nằm trong {30, 60, 120, 144, -1} → fallback 144.
            GameSettings.SetGraphics(1920, 1080, FullScreenMode.FullScreenWindow, true, frameLimit: 300, 3);
            Assert.AreEqual(144, GameSettings.FrameLimit);

            // -1 (unlimited) là hợp lệ.
            GameSettings.SetGraphics(1920, 1080, FullScreenMode.FullScreenWindow, true, frameLimit: -1, 3);
            Assert.AreEqual(-1, GameSettings.FrameLimit);
        }

        [Test]
        public void SetGraphics_ShadowQuality_ClampsToZeroToThree()
        {
            GameSettings.SetGraphics(1920, 1080, FullScreenMode.FullScreenWindow, true, 144, shadowQualityIndex: 99);
            Assert.AreEqual(3, GameSettings.ShadowQualityIndex);

            GameSettings.SetGraphics(1920, 1080, FullScreenMode.FullScreenWindow, true, 144, shadowQualityIndex: -5);
            Assert.AreEqual(0, GameSettings.ShadowQualityIndex);
        }

        [Test]
        public void SetKey_And_GetKey_Roundtrip()
        {
            GameSettings.SetKey(GameSettings.InputAction.Jump, KeyCode.Z);
            Assert.AreEqual(KeyCode.Z, GameSettings.GetKey(GameSettings.InputAction.Jump));
        }

        [Test]
        public void GetKey_UnboundAction_ReturnsDefaultKey()
        {
            // Chưa set bao giờ → trả về phím mặc định trong DefaultKeys.
            Assert.AreEqual(KeyCode.J, GameSettings.GetKey(GameSettings.InputAction.LightAttack));
            Assert.AreEqual(KeyCode.M, GameSettings.GetKey(GameSettings.InputAction.Map));
        }

        [Test]
        public void SetGameplay_StoresToggles()
        {
            GameSettings.SetGameplay(showDamageNumbers: false, showOtherPlayers: true, showPlayerNameplates: false);

            Assert.IsFalse(GameSettings.ShowDamageNumbers);
            Assert.IsTrue(GameSettings.ShowOtherPlayers);
            Assert.IsFalse(GameSettings.ShowPlayerNameplates);
        }

        [Test]
        public void ResetToDefault_RestoresAllDefaults()
        {
            GameSettings.SetAudio(0.1f, 0.2f, 0.3f);
            GameSettings.SetGraphics(800, 600, FullScreenMode.Windowed, false, 30, 0);
            GameSettings.SetKey(GameSettings.InputAction.Jump, KeyCode.X);

            GameSettings.ResetToDefault();

            Assert.AreEqual(0.8f, GameSettings.MasterVolume);
            Assert.AreEqual(1920, GameSettings.ResolutionWidth);
            Assert.AreEqual(FullScreenMode.FullScreenWindow, GameSettings.DisplayMode);
            Assert.AreEqual(KeyCode.Space, GameSettings.GetKey(GameSettings.InputAction.Jump));
        }
    }
}
