using UnityEngine;
using UnityEngine.UIElements;
using Attrition.Data;

namespace Attrition.UI
{
    /// <summary>HUD + điều phối overlay cho GameUIController.</summary>
    public partial class GameUIController
    {
        private void ShowOverlay(Overlay o)
        {
            _overlay = o;
            SetVisible(_invScreen, o == Overlay.Inventory);
            SetVisible(_ftScreen, o == Overlay.FastTravel);
            SetVisible(_goScreen, o == Overlay.GameOver);
            SetVisible(_loading, o == Overlay.Loading);
            SetVisible(_pauseScreen, o == Overlay.Pause);
            SetVisible(_settingsScreen, o == Overlay.Settings);
            // HUD ẩn khi có overlay che toàn màn (trừ khi None)
            SetVisible(_hud, o == Overlay.None);

            bool wantCursor = o != Overlay.None;
            UnityEngine.Cursor.visible = wantCursor;
            UnityEngine.Cursor.lockState = wantCursor ? CursorLockMode.None : CursorLockMode.Locked;

            // SOLO: dừng game khi mở bất kỳ overlay nào (ESC/map/inventory/checkpoint).
            // Fusion physics bỏ qua Time.timeScale → phải dùng GamePause cho các sim tự đóng băng.
            // COOP: KHÔNG bao giờ dừng (online — dừng sẽ phá đồng bộ).
            bool solo = Attrition.Persistence.GameLaunch.Mode == Attrition.Persistence.LaunchMode.Solo;
            bool pause = solo && o != Overlay.None && o != Overlay.Loading;
            Time.timeScale = pause ? 0f : 1f;               // dừng Animator + Update non-network
            Attrition.Persistence.GamePause.IsPaused = pause; // dừng các sim Fusion

            if (o == Overlay.Inventory) RefreshCharacterPanel();
            if (o == Overlay.FastTravel) RefreshFastTravelList();
        }

        private void ToggleOverlay(Overlay o) => ShowOverlay(_overlay == o ? Overlay.None : o);

        private static void SetVisible(VisualElement e, bool v)
        {
            if (e == null) return;
            if (v) e.RemoveFromClassList("hidden");
            else if (!e.ClassListContains("hidden")) e.AddToClassList("hidden");
        }

        // ─────────────────────────── HUD ───────────────────────────

        private void UpdateHud()
        {
            SetFill("hud-hp-fill", _stats.CurrentHP, _stats.MaxHP);
            SetText("hud-hp-label", $"{_stats.CurrentHP}/{_stats.MaxHP}");

            SetFill("hud-mana-fill", _stats.CurrentMana, _stats.MaxMana);
            SetText("hud-mana-label", $"{_stats.CurrentMana}/{_stats.MaxMana}");

            int sta = Mathf.FloorToInt(_stats.CurrentStamina);
            SetFill("hud-stamina-fill", sta, _stats.MaxStamina);
            SetText("hud-stamina-label", $"{sta}/{_stats.MaxStamina}");

            SetText("hud-level", $"LV. {_stats.Level}");

            if (_potions != null)
            {
                SetText("hud-hp-flask-count", _potions.HealthCharges.ToString());
                SetText("hud-mana-flask-count", _potions.ManaCharges.ToString());
                SetSlotEmpty("hud-hp-flask", _potions.HealthCharges <= 0);
                SetSlotEmpty("hud-mana-flask", _potions.ManaCharges <= 0);
                ApplyFlaskIcons();
            }

            UpdateHudSkillIcon();
            UpdateRestPrompt();
        }

        private bool _flaskIconsApplied;

        /// <summary>Gán icon bình HP/Mana lên HUD (1 lần, từ Sprite trong Inspector).</summary>
        private void ApplyFlaskIcons()
        {
            if (_flaskIconsApplied) return;
            var hpIcon = _root.Q<VisualElement>("hud-hp-flask-icon");
            var manaIcon = _root.Q<VisualElement>("hud-mana-flask-icon");
            if (hpIcon != null && healthFlaskIcon != null) hpIcon.style.backgroundImage = new StyleBackground(healthFlaskIcon);
            if (manaIcon != null && manaFlaskIcon != null) manaIcon.style.backgroundImage = new StyleBackground(manaFlaskIcon);
            _flaskIconsApplied = true;
        }

        private void UpdateHudSkillIcon()
        {
            var icon = _root.Q<VisualElement>("hud-skill-icon");
            var slot = _root.Q<VisualElement>("hud-skill");
            if (icon == null || _inventory == null || _db == null) return;

            var equipped = _inventory.EquippedSkill;
            if (!equipped.IsEmpty && _db.GetItem(equipped.ItemIndex) is SkillSO sk && sk.icon != null)
            {
                icon.style.backgroundImage = new StyleBackground(sk.icon);
                SetSlotEmpty("hud-skill", false);
            }
            else
            {
                icon.style.backgroundImage = StyleKeyword.None;
                SetSlotEmpty("hud-skill", true);
            }
        }

        /// <summary>Hiện gợi ý [R] REST khi đứng trong checkpoint (đọc qua PlayerController).</summary>
        private void UpdateRestPrompt()
        {
            var prompt = _root.Q<VisualElement>("hud-prompt");
            if (prompt == null || _controller == null) return;
            bool show = _controller.IsAtCheckpoint;
            SetVisible(prompt, show);
        }

        // ── Boss bar API: gọi từ boss EnemyController khi aggro/đổi máu/chết ──
        public void ShowBossBar(string bossName, int maxHp)
        {
            SetVisible(_root.Q<VisualElement>("hud-boss"), true);
            SetText("hud-boss-name", bossName.ToUpper());
            SetFill("hud-boss-fill", maxHp, maxHp);
        }

        public void UpdateBossBar(int currentHp, int maxHp) => SetFill("hud-boss-fill", currentHp, maxHp);
        public void HideBossBar() => SetVisible(_root.Q<VisualElement>("hud-boss"), false);

        // ─────────────────────────── helpers ───────────────────────────

        private void SetFill(string name, float cur, float max)
        {
            var e = _root.Q<VisualElement>(name);
            if (e == null) return;
            float pct = max > 0 ? Mathf.Clamp01(cur / max) : 0f;
            e.style.width = Length.Percent(pct * 100f);
        }

        private void SetText(string name, string text)
        {
            var l = _root.Q<Label>(name);
            if (l != null) l.text = text;
        }

        private void SetSlotEmpty(string name, bool empty)
        {
            var e = _root.Q<VisualElement>(name);
            if (e == null) return;
            if (empty) { if (!e.ClassListContains("hud-slot-empty")) e.AddToClassList("hud-slot-empty"); }
            else e.RemoveFromClassList("hud-slot-empty");
        }
    }
}
