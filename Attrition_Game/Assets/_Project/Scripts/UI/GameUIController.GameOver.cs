using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Attrition.Gameplay.Player;

namespace Attrition.UI
{
    /// <summary>
    /// Game Over + Loading cho GameUIController.
    /// BR-26 (solo: HP=0 → over) / BR-27 (coop: cả 2 chết → over).
    /// Resume → respawn checkpoint gần nhất; Quit → shutdown runner về menu (BR-31).
    /// </summary>
    public partial class GameUIController
    {
        private bool _gameOverShown;

        private void SetupGameOverControls()
        {
            BindButton("go-resume", OnResumeClicked);
            BindButton("go-quit", OnQuitClicked);
        }

        /// <summary>Gọi mỗi frame trong Update — phát hiện toàn bộ player chết.</summary>
        private void CheckGameOver()
        {
            if (_gameOverShown || _overlay == Overlay.Loading) return;
            // Đang rời trận (Quit/back menu) → runner tắt, không xét game over nữa.
            if (_controller == null || _controller.Object == null || !_controller.Object.Runner.IsRunning) return;

            var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);

            // CHỈ xét player có NetworkObject hợp lệ. Khi back menu, player bị despawn (Object invalid)
            // → nếu không lọc, vòng lặp "continue" qua hết invalid rồi rơi xuống ShowGameOver NHẦM
            // (tưởng cả 2 chết). Phải có ÍT NHẤT 1 player valid VÀ tất cả valid đều chết mới là over.
            int validCount = 0;
            bool allDead = true;
            foreach (var p in players)
            {
                if (p.Object == null || !p.Object.IsValid) continue;
                validCount++;
                if (!p.IsDead) { allDead = false; break; }
            }

            if (validCount > 0 && allDead) ShowGameOver(); // BR-27: cả 2 (mọi player sống) đều chết
        }

        private void ShowGameOver()
        {
            _gameOverShown = true;
            float survived = Time.time - _runStartTime;
            SetText("go-time", $"{Mathf.FloorToInt(survived / 60f)}:{Mathf.FloorToInt(survived % 60f):00}");
            // _stats có thể trỏ tới object đã despawn / chưa Spawned() (vd sau respawn) → đọc Level
            // sẽ ném InvalidOperationException. Chỉ đọc khi NetworkObject còn hợp lệ.
            if (_stats != null && _stats.Object != null && _stats.Object.IsValid)
                SetText("go-level", _stats.Level.ToString());

            // Chỉ đếm player có NetworkObject hợp lệ (đã Spawned, chưa despawn) — đọc IsDead trên
            // object chưa/đã hết Spawned sẽ ném InvalidOperationException.
            int alive = FindObjectsByType<PlayerController>(FindObjectsSortMode.None)
                .Count(p => p.Object != null && p.Object.IsValid && !p.IsDead);
            SetText("go-subtitle", alive == 0 ? "Both flames have been extinguished." : "The flame has been extinguished.");
            ShowOverlay(Overlay.GameOver);
        }

        private void OnResumeClicked()
        {
            if (_controller != null) _controller.RpcRequestRespawnAll();
            _gameOverShown = false;
            ShowOverlay(Overlay.None);
        }

        private void OnQuitClicked()
        {
            var runner = _controller != null ? _controller.Runner : null;
            if (runner != null) runner.Shutdown();   // host shutdown = kết thúc session (BR-31)
            SceneManager.LoadScene("Main_Menu_UI");
        }

        // ─────────────────────────── LOADING ───────────────────────────

        /// <summary>API công khai: hiện màn loading khi vào trận/đổi khu.</summary>
        public void ShowLoading(string area, string status = "Preparing world...")
        {
            SetText("loading-area", string.IsNullOrEmpty(area) ? "LOADING" : area.ToUpper());
            SetText("loading-status", status);
            SetVisible(_root.Q<Label>("loading-countdown"), false);
            SetLoadingProgress(0f);
            ShowOverlay(Overlay.Loading);
        }

        public void SetLoadingProgress(float pct01)
        {
            var fill = _root?.Q<VisualElement>("loading-fill");
            if (fill != null) fill.style.width = Length.Percent(Mathf.Clamp01(pct01) * 100f);
        }

        public void HideLoading()
        {
            if (_overlay == Overlay.Loading) ShowOverlay(Overlay.None);
        }

        /// <summary>Reconnect coop (BR-32): hiện loading + đếm ngược giây còn lại.</summary>
        public void ShowReconnect(string playerName, int secondsLeft)
        {
            SetText("loading-area", "CONNECTION LOST");
            SetText("loading-status", $"{playerName} lost connection. Session paused.");
            var cd = _root.Q<Label>("loading-countdown");
            if (cd != null)
            {
                cd.text = $"{secondsLeft / 60}:{secondsLeft % 60:00} to reconnect";
                SetVisible(cd, true);
            }
            ShowOverlay(Overlay.Loading);
        }
    }
}
