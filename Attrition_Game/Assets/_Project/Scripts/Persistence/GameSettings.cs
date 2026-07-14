using System;
using System.Collections.Generic;
using UnityEngine;

namespace Attrition.Persistence
{
    /// <summary>
    /// Lưu/đọc toàn bộ thiết lập người chơi ở LOCAL (PlayerPrefs).
    /// Gồm: âm lượng, toggle gameplay/graphics, đồ hoạ, và keybinding (đổi phím).
    /// Gameplay đọc phím qua GameSettings.GetKey(action); UI Settings ghi qua Set/Save.
    ///
    /// Không phụ thuộc Fusion — chạy được cả ở MainMenu lẫn trong trận, solo lẫn coop.
    /// </summary>
    public static class GameSettings
    {
        // ─── Hành động có thể đổi phím (khớp Game Mechanics trong concept) ───
        public enum InputAction
        {
            Jump, Dash, LightAttack, Skill, HealthFlask, ManaFlask, Block, Map, Inventory, Interact, Revive
        }

        public static readonly Dictionary<InputAction, KeyCode> DefaultKeys = new()
        {
            { InputAction.Jump,        KeyCode.Space },
            { InputAction.Dash,        KeyCode.LeftShift },
            { InputAction.LightAttack, KeyCode.J },
            { InputAction.Skill,       KeyCode.K },
            { InputAction.HealthFlask, KeyCode.Q },
            { InputAction.ManaFlask,   KeyCode.E },
            { InputAction.Block,       KeyCode.L },
            { InputAction.Map,         KeyCode.M },
            { InputAction.Inventory,   KeyCode.Tab },
            { InputAction.Interact,    KeyCode.F },
            { InputAction.Revive,      KeyCode.R },
        };

        private static readonly Dictionary<InputAction, KeyCode> _keys = new();
        private static bool _loaded;

        // ─── Audio (0..1) ───
        public static float MasterVolume { get; private set; } = 0.8f;
        public static float MusicVolume  { get; private set; } = 0.65f;
        public static float SfxVolume    { get; private set; } = 1f;
        public static float AmbientVolume { get; private set; } = 0.7f;
        public static float VoiceVolume  { get; private set; } = 0.9f;

        // ─── Gameplay/Graphics toggles ───
        public static bool ShowDamageNumbers { get; private set; } = true;
        public static bool CameraShake       { get; private set; } = true;
        public static bool VSync             { get; private set; } = true;
        public static bool PostProcessing    { get; private set; } = true;

        public static event Action OnChanged;

        private const string P = "settings.";

        public static void EnsureLoaded()
        {
            if (_loaded) return;
            Load();
        }

        public static KeyCode GetKey(InputAction action)
        {
            EnsureLoaded();
            return _keys.TryGetValue(action, out var k) ? k : DefaultKeys[action];
        }

        public static void SetKey(InputAction action, KeyCode key)
        {
            EnsureLoaded();
            _keys[action] = key;
        }

        public static void SetAudio(float master, float music, float sfx, float ambient, float voice)
        {
            MasterVolume = Mathf.Clamp01(master);
            MusicVolume = Mathf.Clamp01(music);
            SfxVolume = Mathf.Clamp01(sfx);
            AmbientVolume = Mathf.Clamp01(ambient);
            VoiceVolume = Mathf.Clamp01(voice);
        }

        public static void SetToggles(bool showDmg, bool camShake, bool vsync, bool postFx)
        {
            ShowDamageNumbers = showDmg;
            CameraShake = camShake;
            VSync = vsync;
            PostProcessing = postFx;
        }

        public static void Load()
        {
            MasterVolume  = PlayerPrefs.GetFloat(P + "vol.master", 0.8f);
            MusicVolume   = PlayerPrefs.GetFloat(P + "vol.music", 0.65f);
            SfxVolume     = PlayerPrefs.GetFloat(P + "vol.sfx", 1f);
            AmbientVolume = PlayerPrefs.GetFloat(P + "vol.ambient", 0.7f);
            VoiceVolume   = PlayerPrefs.GetFloat(P + "vol.voice", 0.9f);

            ShowDamageNumbers = PlayerPrefs.GetInt(P + "showDmg", 1) == 1;
            CameraShake       = PlayerPrefs.GetInt(P + "camShake", 1) == 1;
            VSync             = PlayerPrefs.GetInt(P + "vsync", 1) == 1;
            PostProcessing    = PlayerPrefs.GetInt(P + "postFx", 1) == 1;

            _keys.Clear();
            foreach (var kv in DefaultKeys)
            {
                int stored = PlayerPrefs.GetInt(P + "key." + kv.Key, (int)kv.Value);
                _keys[kv.Key] = (KeyCode)stored;
            }
            _loaded = true;
        }

        public static void Save()
        {
            PlayerPrefs.SetFloat(P + "vol.master", MasterVolume);
            PlayerPrefs.SetFloat(P + "vol.music", MusicVolume);
            PlayerPrefs.SetFloat(P + "vol.sfx", SfxVolume);
            PlayerPrefs.SetFloat(P + "vol.ambient", AmbientVolume);
            PlayerPrefs.SetFloat(P + "vol.voice", VoiceVolume);

            PlayerPrefs.SetInt(P + "showDmg", ShowDamageNumbers ? 1 : 0);
            PlayerPrefs.SetInt(P + "camShake", CameraShake ? 1 : 0);
            PlayerPrefs.SetInt(P + "vsync", VSync ? 1 : 0);
            PlayerPrefs.SetInt(P + "postFx", PostProcessing ? 1 : 0);

            foreach (var kv in _keys)
                PlayerPrefs.SetInt(P + "key." + kv.Key, (int)kv.Value);

            PlayerPrefs.Save();
            OnChanged?.Invoke();
        }

        public static void ResetToDefault()
        {
            SetAudio(0.8f, 0.65f, 1f, 0.7f, 0.9f);
            SetToggles(true, true, true, true);
            _keys.Clear();
            foreach (var kv in DefaultKeys) _keys[kv.Key] = kv.Value;
            _loaded = true;
        }

        /// <summary>Áp ngay vào engine (gọi sau Load/Save): âm lượng tổng + VSync.</summary>
        public static void ApplyToEngine()
        {
            EnsureLoaded();
            AudioListener.volume = MasterVolume;
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            // Đẩy hệ số âm lượng SFX xuống GameSfx (Systems). Chiều Persistence→Systems là hợp lệ
            // (Persistence đã tham chiếu Systems); KHÔNG để GameSfx đọc ngược lên đây (circular dep).
            Attrition.Systems.GameSfx.SfxVolume = SfxVolume;
            Attrition.Systems.GameBgm.MusicVolume = MusicVolume;
            Attrition.Systems.GameBgm.MasterVolume = MasterVolume;
        }
    }
}
