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


        // Chữ ký các con số HUD ở frame trước. HUD chỉ đổi khi số đổi, nên chạy lại toàn bộ set-text +
        // nội suy chuỗi ($"{a}/{b}") mỗi frame ở 144fps là rác GC thuần vô ích — đây là loại chi phí gây
        // giật kiểu "FPS cao nhưng vẫn hitch" vì GC dồn cục.
        private int _hudSig = int.MinValue;

        private void UpdateHud()
        {
            if (_stats == null || _stats.Object == null || !_stats.Object.IsValid) return;

            // HP trên HUD là ORB TRÒN (khung Gandalf) → dâng theo CHIỀU CAO, không bóp ngang.
            // Đổi height của TRACK (cửa sổ cắt), KHÔNG phải của fill: fill giữ nguyên kích thước ảnh
            // nên hình cầu không bị co méo, chỉ bị CẮT bớt phần trên → mép nước cong đúng theo viền orb.
            // Tab (#inv-hp-fill) vẫn là thanh ngang nên vẫn dùng SetFill thường.
            SetFillVertical("hud-hp-track", _stats.CurrentHP, _stats.MaxHP);
            SetText("hud-hp-label", $"{_stats.CurrentHP}/{_stats.MaxHP}");

                SetFill("hud-hp-fill", _stats.CurrentHP, _stats.MaxHP);
                SetText("hud-hp-label", $"{_stats.CurrentHP}/{_stats.MaxHP}");

                SetFill("hud-mana-fill", _stats.CurrentMana, _stats.MaxMana);
                SetText("hud-mana-label", $"{_stats.CurrentMana}/{_stats.MaxMana}");

                SetFill("hud-stamina-fill", sta, _stats.MaxStamina);
                SetText("hud-stamina-label", $"{sta}/{_stats.MaxStamina}");

            // EXP trên HUD (yêu cầu user: hiện chung cụm với mana/stamina cho dễ theo dõi).
            // _progression có thể null nếu prefab chưa gắn → chỉ vẽ khi có.
            var prog = _stats.GetComponent<Attrition.Gameplay.Player.PlayerProgression>();
            if (prog != null) SetFill("hud-exp-fill", prog.CurrentExp, prog.ExpToNext);

            if (_potions != null)
            {
                SetText("hud-hp-flask-count", _potions.HealthCharges.ToString());
                SetText("hud-mana-flask-count", _potions.ManaCharges.ToString());
                SetSlotEmpty("hud-hp-flask", _potions.HealthCharges <= 0);
                SetSlotEmpty("hud-mana-flask", _potions.ManaCharges <= 0);
                ApplyFlaskIcons();
            }

            UpdateRestPrompt();
            UpdateRevivePrompt();
            UpdatePing();
        }

        private float _nextPingTime;

        /// <summary>
        /// Ping coop (góc trên phải): RTT tới peer, cập nhật mỗi giây. Chỉ hiện khi chơi coop —
        /// solo không có mạng nên ẩn hẳn. Chấm đổi màu: xanh (mượt) / vàng (chấp nhận) / đỏ (giật).
        /// </summary>
        private void UpdatePing()
        {
            var ping = _root.Q<VisualElement>("hud-ping");
            if (ping == null) return;

            var runner = Attrition.Networking.NetworkLauncher.Instance?.Runner;
            bool coop = Attrition.Persistence.GameLaunch.Mode == Attrition.Persistence.LaunchMode.Coop
                        && runner != null && runner.IsRunning;
            if (!coop)
            {
                SetVisible(ping, false);
                return;
            }
            SetVisible(ping, true);

            if (Time.unscaledTime < _nextPingTime) return;
            _nextPingTime = Time.unscaledTime + 1f;

            // GetPlayerRtt trả RTT (giây) → ms. Trong hosted mode, CLIENT chỉ có kết nối tới server
            // (host) nên GetPlayerRtt(peer khác) trả 0 — phải đo tới server bằng PlayerRef.None.
            // HOST là server, PlayerRef.None = chính nó (0) nên host đo RTT tới client đang kết nối.
            int ms = -1;
            if (runner.IsServer)
            {
                foreach (var p in runner.ActivePlayers)
                {
                    if (p == runner.LocalPlayer) continue;
                    ms = Mathf.RoundToInt((float)runner.GetPlayerRtt(p) * 1000f);
                    break;
                }
            }
            else
            {
                ms = Mathf.RoundToInt((float)runner.GetPlayerRtt(Fusion.PlayerRef.None) * 1000f);
            }

            var dot = _root.Q<VisualElement>("hud-ping-dot");
            if (ms < 0)
            {
                SetText("hud-ping-label", "-- ms");
                SetPingClass(dot, "ping-bad");
                return;
            }

            SetText("hud-ping-label", $"{ms} ms");
            SetPingClass(dot, ms <= 80 ? "ping-good" : ms <= 160 ? "ping-ok" : "ping-bad");
        }

        private float _fpsAccum;
        private int _fpsFrames;
        private float _nextFpsTime;

        /// <summary>
        /// FPS (góc trên phải, dưới ping): trung bình mỗi 0.5s cho ổn định (không nhảy loạn mỗi frame).
        /// Dùng unscaledDeltaTime để đo đúng cả khi pause. Đổi màu: xanh ≥50, vàng ≥30, đỏ &lt;30.
        /// </summary>
        private void UpdateFps()
        {
            var fps = _root.Q<VisualElement>("hud-fps");
            if (fps == null) return;

            _fpsAccum += Time.unscaledDeltaTime;
            _fpsFrames++;

            if (Time.unscaledTime < _nextFpsTime) return;
            _nextFpsTime = Time.unscaledTime + 0.5f;

            int val = (_fpsAccum > 0f && _fpsFrames > 0)
                ? Mathf.RoundToInt(_fpsFrames / _fpsAccum)
                : 0;
            _fpsAccum = 0f;
            _fpsFrames = 0;

            SetText("hud-fps-label", $"{val} FPS");
            var label = _root.Q<Label>("hud-fps-label");
            if (label != null)
            {
                label.RemoveFromClassList("fps-good");
                label.RemoveFromClassList("fps-ok");
                label.RemoveFromClassList("fps-bad");
                label.AddToClassList(val >= 50 ? "fps-good" : val >= 30 ? "fps-ok" : "fps-bad");
            }
        }

        private void SetPingClass(VisualElement dot, string cls)
        {
            if (dot == null) return;
            dot.RemoveFromClassList("ping-good");
            dot.RemoveFromClassList("ping-ok");
            dot.RemoveFromClassList("ping-bad");
            dot.AddToClassList(cls);
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

        /// <summary>
        /// Hiện gợi ý [R] HỒI SINH khi đứng gần đồng đội đã gục (chỉ coop). Thanh fill phản ánh
        /// tiến trình giữ R. Đọc local qua CoopReviveSystem (hoạt động cả trên client input-authority).
        /// </summary>
        private void UpdateRevivePrompt()
        {
            var prompt = _root.Q<VisualElement>("hud-revive");
            var downed = _root.Q<VisualElement>("hud-downed");
            var allyDown = _root.Q<VisualElement>("hud-ally-down");

            bool coop = Attrition.Persistence.GameLaunch.Mode == Attrition.Persistence.LaunchMode.Coop;
            bool valid = coop && _revive != null && _revive.Object != null && _revive.Object.IsValid
                         && _controller != null && _controller.Object != null && _controller.Object.IsValid;

            if (!valid)
            {
                SetVisible(prompt, false);
                SetVisible(downed, false);
                SetVisible(allyDown, false);
                return;
            }

            bool imDead = _controller.IsDead;

            // ── 1. MÌNH ĐANG GỤC: hiện tiến trình đồng đội đang cứu mình ──
            // IncomingReviveFraction đọc [Networked] của peer khác nên chạy đúng trên MỌI máy,
            // kể cả client đã gục (trước đây HUD người gục trống trơn).
            float incoming = imDead ? _revive.IncomingReviveFraction : -1f;
            SetVisible(downed, imDead);
            if (imDead)
            {
                bool beingSaved = incoming >= 0f;
                SetText("hud-downed-text", beingSaved
                    ? "ĐỒNG ĐỘI ĐANG CỨU..."
                    : "ĐÃ GỤC — CHỜ ĐỒNG ĐỘI CỨU");
                SetFill("hud-downed-fill", beingSaved ? incoming : 0f, 1f);
            }

            // ── 2. MÌNH CÒN SỐNG, đồng đội gục ──
            // prompt [R] chỉ hiện khi TRONG TẦM + còn bình; ngoài tầm thì hiện chỉ báo + khoảng cách
            // để biết chạy tới đâu (yêu cầu user: host phải thấy được đồng đội đã gục).
            bool inRange = !imDead && (_revive.IsReviving || _revive.HasRevivableAllyNearby());
            SetVisible(prompt, inRange);
            if (inRange) SetFill("hud-revive-fill", _revive.ReviveFraction, 1f);

            var ally = imDead ? null : _revive.FindDownedAllyAnywhere();
            bool showAllyDown = ally != null && !inRange;
            SetVisible(allyDown, showAllyDown);
            if (showAllyDown)
            {
                float dist = Vector2.Distance(_controller.transform.position, ally.transform.position);
                SetText("hud-ally-down-dist", $"{Mathf.RoundToInt(dist)} m");
            }
        }

        public void ShowBossBar(string bossName, int maxHp)
        {
            SetVisible(_root.Q<VisualElement>("hud-boss"), true);
            SetText("hud-boss-name", bossName.ToUpper());
            SetFill("hud-boss-fill", maxHp, maxHp);
        }

        public void UpdateBossBar(int currentHp, int maxHp) => SetFill("hud-boss-fill", currentHp, maxHp);
        public void HideBossBar() => SetVisible(_root.Q<VisualElement>("hud-boss"), false);


        private void SetFill(string name, float cur, float max)
        {
            var e = _root.Q<VisualElement>(name);
            if (e == null) return;
            float pct = max > 0 ? Mathf.Clamp01(cur / max) : 0f;
            e.style.width = Length.Percent(pct * 100f);
        }

        /// <summary>
        /// Fill theo CHIỀU CAO (dâng từ đáy lên) — dùng cho ORB TRÒN chứa HP của khung Gandalf.
        /// Orb là hình cầu: bóp theo chiều NGANG sẽ méo hình, phải cho "chất lỏng" dâng dần.
        /// USS đặt hàng orb là flex-direction: column + justify-content: flex-end nên phần tử
        /// tự dính đáy; ở đây chỉ đổi height theo %.
        /// </summary>
        private void SetFillVertical(string name, float cur, float max)
        {
            var e = _root.Q<VisualElement>(name);
            if (e == null) return;
            float pct = max > 0 ? Mathf.Clamp01(cur / max) : 0f;
            e.style.height = Length.Percent(pct * 100f);
            e.style.width = Length.Percent(100f);   // orb luôn phủ đủ chiều ngang
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
