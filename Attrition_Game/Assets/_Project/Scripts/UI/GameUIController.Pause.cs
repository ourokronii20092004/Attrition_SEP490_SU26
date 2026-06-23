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
            BindButton("pause-resume", () => ShowOverlay(Overlay.None));
            BindButton("pause-settings", () => ShowOverlay(Overlay.Settings));
            BindButton("pause-quit", OnPauseQuit);
        }

        private void OnPauseQuit()
        {
            Time.timeScale = 1f; // bỏ pause trước khi rời scene

            // BẮT BUỘC LƯU TRƯỚC KHI THOÁT ĐỂ GIỮ LẠI VỊ TRÍ, HP, VÀ ĐIỂM
            var saver = Attrition.Gameplay.Persistence.GameSaveService.EnsureExists();
            saver.Save(Attrition.Gameplay.Persistence.GameSaveService.SaveEvent.Quit);

            var runner = _controller != null ? _controller.Runner : null;
            if (runner != null) runner.Shutdown();
            SceneManager.LoadScene("Main_Menu_UI");
        }
    }
}
