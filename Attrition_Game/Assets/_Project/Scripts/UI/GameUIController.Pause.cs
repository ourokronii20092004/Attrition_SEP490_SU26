using UnityEngine;
using UnityEngine.SceneManagement;
using Attrition.Gameplay.Player;

namespace Attrition.UI
{
    /// <summary>
    /// Menu tạm dừng (ESC) cho GameUIController: Resume / Settings / Quit to Menu.
    /// Solo: ShowOverlay tự đặt Time.timeScale=0 (dừng game). Coop: không dừng.
    /// Quit = shutdown runner (host kết thúc session, BR-31) rồi về MainMenu.
    /// </summary>
    public partial class GameUIController
    {
        private void SetupPauseControls()
        {
            BindButton("pause-resume", ResumeFromPause);
            BindButton("pause-settings", () => ShowOverlay(Overlay.Settings));
            BindButton("pause-quit", OnPauseQuit);
        }

        private void ResumeFromPause()
        {
            var target = _overlayBeforePause;
            _overlayBeforePause = Overlay.None;
            ShowOverlay(target == Overlay.Pause || target == Overlay.Settings ? Overlay.None : target);
        }

        private void OnPauseQuit()
        {
            Time.timeScale = 1f; // bỏ pause trước khi rời scene
            StartCoroutine(SaveThenQuit());
        }

        /// <summary>
        /// LƯU rồi CHỜ XONG mới shutdown runner + rời scene.
        ///
        /// VÌ SAO PHẢI CHỜ: bản cũ gọi `Save()` rồi `Shutdown()` + `LoadScene` ngay trong cùng frame.
        /// Nhưng nhánh coop của Save (`SaveAllOnline`) là coroutine và nó `yield return
        /// RefreshAccessToken(...)` TRƯỚC khi quét `FindObjectsByType&lt;PlayerController&gt;()`. Tới lúc
        /// quét thì `Shutdown()` đã despawn hết player → payload KHÔNG có ai (không nhân vật nào được
        /// ghi), hoặc đọc `stats.CurrentHP` trên NetworkObject đã invalid → ném exception giữa
        /// coroutine và save chết im lặng. Đây là mốc "không lưu là mất" mà lại không chờ.
        /// `SaveAndWait` có timeout riêng nên mạng treo cũng không kẹt người chơi trong menu Pause.
        /// </summary>
        private System.Collections.IEnumerator SaveThenQuit()
        {
            // Giữ tham chiếu runner TRƯỚC khi lưu: sau khi lưu xong, `_controller` có thể đã null
            // (rebind/despawn) và ta sẽ không còn đường nào lấy runner để shutdown cho sạch.
            var runner = _controller != null ? _controller.Runner : null;

            var saver = Attrition.Gameplay.Persistence.GameSaveService.EnsureExists();
            yield return saver.SaveAndWait(Attrition.Gameplay.Persistence.GameSaveService.SaveEvent.Quit);

            // destroyGameObject:false — runner AddComponent lên chính GO của NetworkLauncher (bền,
            // DontDestroyOnLoad). Shutdown() mặc định destroyGameObject=true sẽ HỦY luôn NetworkLauncher
            // → vào lại phòng báo "NetworkLauncher not found". Giữ GO để lần sau tái dùng.
            if (runner != null) runner.Shutdown(destroyGameObject: false);
            SceneManager.LoadScene("Main_Menu_UI");
        }
    }
}
