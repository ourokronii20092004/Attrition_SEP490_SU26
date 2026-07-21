using System;
using System.Collections.Generic;
using UnityEngine;

namespace Attrition.Persistence
{
    public static class GameSettings
    {
        public enum InputAction
        {
            Jump, Dash, LightAttack, Skill, HealthFlask, ManaFlask, Map, Inventory, Interact, Revive
        }

        public static readonly Dictionary<InputAction, KeyCode> DefaultKeys = new()
        {
            { InputAction.Jump, KeyCode.Space },
            { InputAction.Dash, KeyCode.LeftShift },
            { InputAction.LightAttack, KeyCode.J },
            { InputAction.Skill, KeyCode.K },
            { InputAction.HealthFlask, KeyCode.Q },
            { InputAction.ManaFlask, KeyCode.E },
            { InputAction.Map, KeyCode.M },
            { InputAction.Inventory, KeyCode.Tab },
            { InputAction.Interact, KeyCode.F },
            { InputAction.Revive, KeyCode.R },
        };

        private static readonly Dictionary<InputAction, KeyCode> _keys = new();
        private static bool _loaded;

        public static float MasterVolume { get; private set; } = 0.8f;
        public static float MusicVolume { get; private set; } = 0.65f;
        public static float SfxVolume { get; private set; } = 1f;

        public static bool ShowDamageNumbers { get; private set; } = true;
        public static bool ShowOtherPlayers { get; private set; } = true;
        public static bool ShowPlayerNameplates { get; private set; } = true;
        public static bool VSync { get; private set; } = true;

        public static int ResolutionWidth { get; private set; } = 1920;
        public static int ResolutionHeight { get; private set; } = 1080;
        public static FullScreenMode DisplayMode { get; private set; } = FullScreenMode.FullScreenWindow;
        public static int FrameLimit { get; private set; } = 144;
        public static int ShadowQualityIndex { get; private set; } = 3;

        public static event Action OnChanged;

        private const string P = "settings.";

        public static void EnsureLoaded()
        {
            if (!_loaded) Load();
        }

        public static KeyCode GetKey(InputAction action)
        {
            EnsureLoaded();
            return _keys.TryGetValue(action, out var key) ? key : DefaultKeys[action];
        }

        public static void SetKey(InputAction action, KeyCode key)
        {
            EnsureLoaded();
            _keys[action] = key;
        }

        public static void SetAudio(float master, float music, float sfx)
        {
            MasterVolume = Mathf.Clamp01(master);
            MusicVolume = Mathf.Clamp01(music);
            SfxVolume = Mathf.Clamp01(sfx);
        }

        public static void SetGameplay(bool showDamageNumbers, bool showOtherPlayers, bool showPlayerNameplates)
        {
            ShowDamageNumbers = showDamageNumbers;
            ShowOtherPlayers = showOtherPlayers;
            ShowPlayerNameplates = showPlayerNameplates;
        }

        public static void SetGraphics(int width, int height, FullScreenMode mode, bool vsync, int frameLimit, int shadowQualityIndex)
        {
            ResolutionWidth = width > 0 ? width : 1920;
            ResolutionHeight = height > 0 ? height : 1080;
            DisplayMode = Enum.IsDefined(typeof(FullScreenMode), mode) ? mode : FullScreenMode.FullScreenWindow;
            VSync = vsync;
            FrameLimit = frameLimit is 30 or 60 or 120 or 144 or -1 ? frameLimit : 144;
            ShadowQualityIndex = Mathf.Clamp(shadowQualityIndex, 0, 3);
        }

        public static void Load()
        {
            MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(P + "vol.master", 0.8f));
            MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(P + "vol.music", 0.65f));
            SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(P + "vol.sfx", 1f));

            ShowDamageNumbers = PlayerPrefs.GetInt(P + "showDmg", 1) == 1;
            ShowOtherPlayers = PlayerPrefs.GetInt(P + "showPlayers", 1) == 1;
            ShowPlayerNameplates = PlayerPrefs.GetInt(P + "showNameplates", 1) == 1;

            var modeValue = PlayerPrefs.GetInt(P + "fullscreenMode", (int)FullScreenMode.FullScreenWindow);
            var mode = Enum.IsDefined(typeof(FullScreenMode), modeValue)
                ? (FullScreenMode)modeValue
                : FullScreenMode.FullScreenWindow;
            SetGraphics(
                PlayerPrefs.GetInt(P + "resolutionWidth", 1920),
                PlayerPrefs.GetInt(P + "resolutionHeight", 1080),
                mode,
                PlayerPrefs.GetInt(P + "vsync", 1) == 1,
                PlayerPrefs.GetInt(P + "frameLimit", 144),
                PlayerPrefs.GetInt(P + "shadowQuality", 3));

            _keys.Clear();
            foreach (var pair in DefaultKeys)
                _keys[pair.Key] = (KeyCode)PlayerPrefs.GetInt(P + "key." + pair.Key, (int)pair.Value);
            _loaded = true;
        }

        public static void Save()
        {
            PlayerPrefs.SetFloat(P + "vol.master", MasterVolume);
            PlayerPrefs.SetFloat(P + "vol.music", MusicVolume);
            PlayerPrefs.SetFloat(P + "vol.sfx", SfxVolume);
            PlayerPrefs.SetInt(P + "showDmg", ShowDamageNumbers ? 1 : 0);
            PlayerPrefs.SetInt(P + "showPlayers", ShowOtherPlayers ? 1 : 0);
            PlayerPrefs.SetInt(P + "showNameplates", ShowPlayerNameplates ? 1 : 0);
            PlayerPrefs.SetInt(P + "vsync", VSync ? 1 : 0);
            PlayerPrefs.SetInt(P + "resolutionWidth", ResolutionWidth);
            PlayerPrefs.SetInt(P + "resolutionHeight", ResolutionHeight);
            PlayerPrefs.SetInt(P + "fullscreenMode", (int)DisplayMode);
            PlayerPrefs.SetInt(P + "frameLimit", FrameLimit);
            PlayerPrefs.SetInt(P + "shadowQuality", ShadowQualityIndex);

            foreach (var pair in _keys)
                PlayerPrefs.SetInt(P + "key." + pair.Key, (int)pair.Value);

            PlayerPrefs.Save();
            OnChanged?.Invoke();
        }

        public static void ResetToDefault()
        {
            SetAudio(0.8f, 0.65f, 1f);
            SetGameplay(true, true, true);
            SetGraphics(1920, 1080, FullScreenMode.FullScreenWindow, true, 144, 3);
            _keys.Clear();
            foreach (var pair in DefaultKeys) _keys[pair.Key] = pair.Value;
            _loaded = true;
        }

        public static void ApplyToEngine()
        {
            EnsureLoaded();
            AudioListener.volume = MasterVolume;
            Attrition.Systems.GameSfx.SfxVolume = SfxVolume;
            Attrition.Systems.GameBgm.MusicVolume = MusicVolume;
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            Application.targetFrameRate = FrameLimit;
            QualitySettings.shadows = ShadowQualityIndex == 0 ? ShadowQuality.HardOnly : ShadowQuality.All;
            QualitySettings.shadowResolution = (ShadowResolution)ShadowQualityIndex;
            Screen.SetResolution(ResolutionWidth, ResolutionHeight, DisplayMode);
        }
    }
}
