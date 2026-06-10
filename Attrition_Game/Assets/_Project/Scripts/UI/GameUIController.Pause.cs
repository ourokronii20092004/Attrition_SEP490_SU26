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
            var runner = _controller != null ? _controller.Runner : null;
            if (runner != null) runner.Shutdown();
            SceneManager.LoadScene("Main_Menu_UI");
        }
    }
}
