using UnityEngine;
using UnityEngine.UIElements;
using Attrition.Persistence;

namespace Attrition.UI
{
    /// <summary>
    /// Màn Settings trong trận (mở từ menu ESC). Đọc/ghi GameSettings (PlayerPrefs, local).
    /// Gồm âm lượng (5 slider) + toggle gameplay/graphics. Apply ngay vào engine.
    /// Đổi phím để ở MainMenu (cần layout rộng) — ở đây tập trung audio/toggle.
    /// </summary>
    public partial class GameUIController
    {
        private void SetupSettingsControls()
        {
            GameSettings.EnsureLoaded();
            LoadSettingsIntoUI();

            BindSlider("set-vol-master", "set-vol-master-val");
            BindSlider("set-vol-music", "set-vol-music-val");
            BindSlider("set-vol-sfx", "set-vol-sfx-val");
            BindSlider("set-vol-ambient", "set-vol-ambient-val");
            BindSlider("set-vol-voice", "set-vol-voice-val");

            BindButton("set-back", SaveAndCloseSettings);
            BindButton("set-reset", () =>
            {
                GameSettings.ResetToDefault();
                LoadSettingsIntoUI();
            });
        }

        private void LoadSettingsIntoUI()
        {
            SetSlider("set-vol-master", GameSettings.MasterVolume, "set-vol-master-val");
            SetSlider("set-vol-music", GameSettings.MusicVolume, "set-vol-music-val");
            SetSlider("set-vol-sfx", GameSettings.SfxVolume, "set-vol-sfx-val");
            SetSlider("set-vol-ambient", GameSettings.AmbientVolume, "set-vol-ambient-val");
            SetSlider("set-vol-voice", GameSettings.VoiceVolume, "set-vol-voice-val");

            SetToggle("set-dmg", GameSettings.ShowDamageNumbers);
            SetToggle("set-shake", GameSettings.CameraShake);
            SetToggle("set-vsync", GameSettings.VSync);
            SetToggle("set-postfx", GameSettings.PostProcessing);
        }

        private void SaveAndCloseSettings()
        {
            float Get(string n) => _root.Q<Slider>(n)?.value ?? 0f;
            bool Tg(string n) => _root.Q<Toggle>(n)?.value ?? false;

            GameSettings.SetAudio(Get("set-vol-master"), Get("set-vol-music"),
                Get("set-vol-sfx"), Get("set-vol-ambient"), Get("set-vol-voice"));
            GameSettings.SetToggles(Tg("set-dmg"), Tg("set-shake"), Tg("set-vsync"), Tg("set-postfx"));
            GameSettings.Save();
            GameSettings.ApplyToEngine();

            ShowOverlay(Overlay.Pause); // quay lại menu tạm dừng
        }

        // ─── helpers ───
        private void BindSlider(string sliderName, string labelName)
        {
            var s = _root.Q<Slider>(sliderName);
            if (s == null) return;
            s.RegisterValueChangedCallback(e =>
            {
                var l = _root.Q<Label>(labelName);
                if (l != null) l.text = Mathf.RoundToInt(e.newValue * 100f) + "%";
            });
        }

        private void SetSlider(string sliderName, float value01, string labelName)
        {
            var s = _root.Q<Slider>(sliderName);
            if (s != null) { s.lowValue = 0f; s.highValue = 1f; s.value = value01; }
            var l = _root.Q<Label>(labelName);
            if (l != null) l.text = Mathf.RoundToInt(value01 * 100f) + "%";
        }

        private void SetToggle(string name, bool on)
        {
            var t = _root.Q<Toggle>(name);
            if (t != null) t.value = on;
        }
    }
}
