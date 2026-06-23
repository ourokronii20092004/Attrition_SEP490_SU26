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
            var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            if (players.Length == 0) return;

            foreach (var p in players)
            {
                if (p.Object == null || !p.Object.IsValid) continue;
                if (!p.IsDead) return; // còn ai sống → chưa over (BR-27)
            }

            ShowGameOver();
        }

        private void ShowGameOver()
        {
            _gameOverShown = true;
            float survived = Time.time - _runStartTime;
            SetText("go-time", $"{Mathf.FloorToInt(survived / 60f)}:{Mathf.FloorToInt(survived % 60f):00}");
            if (_stats != null) SetText("go-level", _stats.Level.ToString());

            int alive = FindObjectsByType<PlayerController>(FindObjectsSortMode.None).Count(p => !p.IsDead);
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
