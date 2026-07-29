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
        private bool _clientWaitShown; // coop CLIENT: đang chờ host quyết định sau khi cả 2 chết

        /// <summary>
        /// Chờ bao lâu sau khi player chết mới hiện menu Game Over — để animation chết chạy xong
        /// (yêu cầu user). Trước đây menu bật NGAY frame `IsDead` bật nên che mất anim chết.
        /// 2.2s: anim Player_Death + xác rơi xuống đất + lặng một nhịp cho đỡ hụt.
        /// </summary>
        private const float DeathAnimDelaySeconds = 2.2f;

        /// <summary>Thời điểm phát hiện TẤT CẢ player chết (-1 = chưa). Dùng để đếm delay ở trên.</summary>
        private float _allDeadSince = -1f;

        private void SetupGameOverControls()
        {
            BindButton("go-resume", OnResumeClicked);
            BindButton("go-quit", OnQuitClicked);
        }

        /// <summary>Gọi mỗi frame trong Update — phát hiện toàn bộ player chết.</summary>
        private void CheckGameOver()
        {
            if (_overlay == Overlay.Loading) return;
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

            bool coop = Attrition.Persistence.GameLaunch.Mode == Attrition.Persistence.LaunchMode.Coop;
            bool isHost = _controller.Object.Runner.IsServer;

            if (validCount > 0 && allDead)
            {
                // Bắt đầu đếm từ lúc phát hiện chết hết, KHÔNG hiện menu ngay (che mất anim chết).
                if (_allDeadSince < 0f) _allDeadSince = Time.unscaledTime;

                // Chưa đủ thời gian cho animation chết → chờ tiếp.
                if (Time.unscaledTime - _allDeadSince < DeathAnimDelaySeconds) return;

                // COOP + CLIENT: KHÔNG tự quyết. Host mới chọn (checkpoint / main menu). Client chờ.
                if (coop && !isHost)
                {
                    if (!_clientWaitShown) ShowClientWaitForHost();
                }
                else if (!_gameOverShown) ShowGameOver(); // solo hoặc host: hiện panel có 2 lựa chọn
            }
            else
            {
                // Còn người sống → reset đồng hồ đếm để lần chết sau lại chờ đủ anim.
                _allDeadSince = -1f;

                // Reset luôn cờ đã-hiện-menu. `OnResumeClicked` có reset, nhưng player còn sống lại
                // được bằng đường khác (đồng đội revive, host respawn qua RPC) — không reset ở đây thì
                // lần chết SAU menu sẽ không bao giờ hiện lại.
                _gameOverShown = false;

                // Không còn all-dead nữa = host đã chọn respawn checkpoint → cả 2 sống lại. Client đang
                // chờ thì tự ẩn màn chờ, trả lại HUD.
                if (_clientWaitShown)
                {
                    _clientWaitShown = false;
                    ShowOverlay(Overlay.None);
                }
            }
        }

        private void ShowGameOver()
        {
            _gameOverShown = true;
            // Host/solo: hiện 2 nút chọn (Resume = respawn checkpoint gần nhất, Quit = về Main Menu).
            SetVisible(_root.Q<VisualElement>("go-resume"), true);
            SetVisible(_root.Q<VisualElement>("go-quit"), true);
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

        /// <summary>COOP CLIENT: cả 2 chết → chờ HOST chọn (dùng chung màn Game Over nhưng ẩn 2 nút,
        /// đổi phụ đề thành "Waiting for host..."). Tự ẩn khi host respawn (xem CheckGameOver).</summary>
        private void ShowClientWaitForHost()
        {
            _clientWaitShown = true;
            SetVisible(_root.Q<VisualElement>("go-resume"), false);
            SetVisible(_root.Q<VisualElement>("go-quit"), false);
            float survived = Time.time - _runStartTime;
            SetText("go-time", $"{Mathf.FloorToInt(survived / 60f)}:{Mathf.FloorToInt(survived % 60f):00}");
            SetText("go-subtitle", "Both flames have been extinguished. Waiting for host...");
            ShowOverlay(Overlay.GameOver);
        }

        private void OnResumeClicked()
        {
            // Đóng panel Game Over TRƯỚC khi gọi RPC. Trên host, RpcRequestRespawnAll chạy ĐỒNG BỘ →
            // RpcTravelLoading mở overlay Loading ngay; nếu ShowOverlay(None) chạy SAU sẽ đè tắt loading
            // (giống race đã sửa ở RestHere). Đóng trước rồi request → loading giữ nguyên, che lúc camera
            // snap về checkpoint.
            _gameOverShown = false;
            ShowOverlay(Overlay.None);
            if (_controller != null) _controller.RpcRequestRespawnAll();
        }

        private void OnQuitClicked()
        {
            var runner = _controller != null ? _controller.Runner : null;
            // destroyGameObject:false — tránh hủy NetworkLauncher (runner nằm trên GO bền của nó) →
            // giữ để vào lại phòng được. Xem chi tiết trong GameUIController.Pause.OnPauseQuit.
            if (runner != null) runner.Shutdown(destroyGameObject: false);   // host shutdown = kết thúc session (BR-31)
            SceneManager.LoadScene("Main_Menu_UI");
        }


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
