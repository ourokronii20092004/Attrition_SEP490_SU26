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
            bool pauseOnTop = o == Overlay.Pause || o == Overlay.Settings;
            if (_doc != null)
                _doc.sortingOrder = pauseOnTop ? 2000f : _defaultSortingOrder;
            Attrition.Gameplay.Environment.TutorialPanel.SetSuppressed(pauseOnTop);

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
            // Fusion physics bỏ qua Time.timeScale → SetSoloFreeze set cả timeScale lẫn GamePause.
            // COOP: SetSoloFreeze tự no-op (online — dừng sẽ phá đồng bộ).
            // Chỉ khai báo lý do CỦA MÌNH — hội thoại NPC giữ freeze riêng, đóng overlay không gỡ của nó.
            Attrition.Persistence.GamePause.SetSoloFreeze(
                Attrition.Persistence.GamePause.Freeze.Overlay,
                o != Overlay.None && o != Overlay.Loading);

            if (o == Overlay.Inventory)
            {
                RefreshCharacterPanel();
                // Log nhiệm vụ: dựng lại MỖI LẦN mở Tab. Tiến độ quest đổi trong lúc bảng đóng (giết quái)
                // nên nếu chỉ dựng lúc bấm vào tab thì player mở Tab lại vẫn thấy số cũ.
                if (_questTabActive) RefreshQuestLog();
            }
            if (o == Overlay.FastTravel)
            {
                ShowBonfireMain();
                RefreshFastTravelList();
            }
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

        /// <summary>
        /// PlayerProgression của `_stats` hiện tại. Cache vì `GetComponent` mỗi frame cũng là chi phí
        /// thừa đúng loại mà `_hudSig` đang tránh. `_progOwner` để phát hiện đổi player (respawn/rebind)
        /// → tự resolve lại thay vì giữ tham chiếu cũ.
        /// </summary>
        private Attrition.Gameplay.Player.PlayerProgression _prog;
        private Attrition.Gameplay.Player.PlayerStats _progOwner;

        private void UpdateHud()
        {
            if (_stats == null || _stats.Object == null || !_stats.Object.IsValid) return;

            if (_progOwner != _stats)
            {
                _progOwner = _stats;
                _prog = _stats.GetComponent<Attrition.Gameplay.Player.PlayerProgression>();
            }

            int sta = Mathf.FloorToInt(_stats.CurrentStamina);
            int exp = _prog != null ? _prog.CurrentExp : 0;
            int expMax = _prog != null ? _prog.ExpToNext : 0;
            int hpFlask = _potions != null ? _potions.HealthCharges : 0;
            int manaFlask = _potions != null ? _potions.ManaCharges : 0;

            // Chữ ký gộp mọi con số HUD vẽ ở dưới. Chỉ đổi UI khi có số thật sự đổi.
            int sig = _stats.CurrentHP;
            unchecked
            {
                sig = sig * 397 + _stats.MaxHP;
                sig = sig * 397 + _stats.CurrentMana;
                sig = sig * 397 + _stats.MaxMana;
                sig = sig * 397 + sta;
                sig = sig * 397 + _stats.MaxStamina;
                sig = sig * 397 + _stats.Level;
                sig = sig * 397 + exp;
                sig = sig * 397 + expMax;
                sig = sig * 397 + hpFlask;
                sig = sig * 397 + manaFlask;
            }

            if (sig != _hudSig)
            {
                _hudSig = sig;

                // HP trên HUD là ORB TRÒN (khung Gandalf) → dâng theo CHIỀU CAO, không bóp ngang.
                // Đổi height của TRACK (cửa sổ cắt), KHÔNG phải của fill: fill giữ nguyên kích thước ảnh
                // nên hình cầu không bị co méo, chỉ bị CẮT bớt phần trên → mép nước cong đúng viền orb.
                // KHÔNG chạm `hud-hp-fill` ở đây — set width % lên nó sẽ bóp méo ảnh orb.
                // Tab (#inv-hp-fill) vẫn là thanh ngang nên vẫn dùng SetFill thường.
                SetFillVertical("hud-hp-track", _stats.CurrentHP, _stats.MaxHP);
                SetText("hud-hp-label", $"{_stats.CurrentHP}/{_stats.MaxHP}");

                SetFill("hud-mana-fill", _stats.CurrentMana, _stats.MaxMana);
                SetText("hud-mana-label", $"{_stats.CurrentMana}/{_stats.MaxMana}");

                SetFill("hud-stamina-fill", sta, _stats.MaxStamina);
                SetText("hud-stamina-label", $"{sta}/{_stats.MaxStamina}");

                SetText("hud-level", $"LV. {_stats.Level}");

                // EXP trên HUD (yêu cầu user: hiện chung cụm với mana/stamina cho dễ theo dõi).
                // _prog có thể null nếu prefab chưa gắn → chỉ vẽ khi có.
                if (_prog != null) SetFill("hud-exp-fill", exp, expMax);

                if (_potions != null)
                {
                    SetText("hud-hp-flask-count", hpFlask.ToString());
                    SetText("hud-mana-flask-count", manaFlask.ToString());
                    SetSlotEmpty("hud-hp-flask", hpFlask <= 0);
                    SetSlotEmpty("hud-mana-flask", manaFlask <= 0);
                }
            }

            // Ngoài cổng _hudSig: những cái này KHÔNG phụ thuộc con số stat nên gate sẽ làm chúng đứng.
            if (_potions != null) ApplyFlaskIcons();   // tự no-op sau lần đầu
            UpdateHudSkillIcon();                      // đổi theo skill đang trang bị
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
                    ? "A TEAMMATE IS REVIVING YOU..."
                    : "DOWNED — WAIT FOR A TEAMMATE");
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
