using UnityEngine;
using UnityEngine.UIElements;
using Attrition.Persistence;

namespace Attrition.UI
{
    public partial class GameUIController
    {
        private void SetupSettingsControls()
        {
            GameSettings.EnsureLoaded();
            LoadSettingsIntoUI();

            BindSlider("set-vol-master", "set-vol-master-val");
            BindSlider("set-vol-music", "set-vol-music-val");
            BindSlider("set-vol-sfx", "set-vol-sfx-val");

            BindButton("set-back", SaveAndCloseSettings);
            BindButton("set-reset", () =>
            {
                GameSettings.ResetToDefault();
                GameSettings.Save();
                GameSettings.ApplyToEngine();
                LoadSettingsIntoUI();
                ShowToast("Settings reset to defaults.", new Color(0.18f, 0.65f, 0.32f), 2.5f);
            });
        }

        private void LoadSettingsIntoUI()
        {
            SetSlider("set-vol-master", GameSettings.MasterVolume, "set-vol-master-val");
            SetSlider("set-vol-music", GameSettings.MusicVolume, "set-vol-music-val");
            SetSlider("set-vol-sfx", GameSettings.SfxVolume, "set-vol-sfx-val");
            SetToggle("set-dmg", GameSettings.ShowDamageNumbers);
            SetToggle("set-other-players", GameSettings.ShowOtherPlayers);
            SetToggle("set-player-nameplates", GameSettings.ShowPlayerNameplates);
            SetToggle("set-vsync", GameSettings.VSync);
        }

        private void SaveAndCloseSettings()
        {
            float Get(string name) => _root.Q<Slider>(name)?.value ?? 0f;
            bool GetToggle(string name) => _root.Q<Toggle>(name)?.value ?? false;

            GameSettings.SetAudio(Get("set-vol-master"), Get("set-vol-music"), Get("set-vol-sfx"));
            GameSettings.SetGameplay(
                GetToggle("set-dmg"),
                GetToggle("set-other-players"),
                GetToggle("set-player-nameplates"));
            GameSettings.SetGraphics(
                GameSettings.ResolutionWidth,
                GameSettings.ResolutionHeight,
                GameSettings.DisplayMode,
                GetToggle("set-vsync"),
                GameSettings.FrameLimit,
                GameSettings.ShadowQualityIndex);
            GameSettings.Save();
            GameSettings.ApplyToEngine();
            ShowToast("Settings updated.", new Color(0.18f, 0.65f, 0.32f), 2.5f);
            ShowOverlay(Overlay.Pause);
        }

        private void BindSlider(string sliderName, string labelName)
        {
            var slider = _root.Q<Slider>(sliderName);
            if (slider == null) return;
            slider.RegisterValueChangedCallback(e =>
            {
                var label = _root.Q<Label>(labelName);
                if (label != null) label.text = Mathf.RoundToInt(e.newValue * 100f) + "%";
            });
        }

        private void SetSlider(string sliderName, float value, string labelName)
        {
            var slider = _root.Q<Slider>(sliderName);
            if (slider != null)
            {
                slider.lowValue = 0f;
                slider.highValue = 1f;
                slider.value = value;
            }
            var label = _root.Q<Label>(labelName);
            if (label != null) label.text = Mathf.RoundToInt(value * 100f) + "%";
        }

        private void SetToggle(string name, bool value)
        {
            var toggle = _root.Q<Toggle>(name);
            if (toggle != null) toggle.value = value;
        }
    }
}
