using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Attrition.UI
{
    /// <summary>
    /// Toast thông báo lưu (góc trên màn hình) cho GameUIController. Nghe SaveNotifyEvents do
    /// GameSaveService phát. Toast được tạo runtime (không cần sửa GameUI.uxml): xanh = lưu thành
    /// công, đỏ = lưu thất bại. Tự ẩn sau vài giây.
    /// </summary>
    public partial class GameUIController
    {
        private Label _saveToast;
        private Coroutine _toastRoutine;

        private void HookSaveToast()
        {
            Attrition.Controllers.SaveNotifyEvents.OnSaveOk += ShowSaveOk;
            Attrition.Controllers.SaveNotifyEvents.OnSaveFailed += ShowSaveFailed;
            Attrition.Controllers.SaveNotifyEvents.OnSessionExpired += ShowSessionExpired;
        }

        private void UnhookSaveToast()
        {
            Attrition.Controllers.SaveNotifyEvents.OnSaveOk -= ShowSaveOk;
            Attrition.Controllers.SaveNotifyEvents.OnSaveFailed -= ShowSaveFailed;
            Attrition.Controllers.SaveNotifyEvents.OnSessionExpired -= ShowSessionExpired;
        }

        private void ShowSaveOk(string message) => ShowToast(message, new Color(0.18f, 0.65f, 0.32f), 2.5f);
        private void ShowSaveFailed(string message) => ShowToast(message, new Color(0.78f, 0.20f, 0.20f), 5f);

        /// <summary>Phiên hết hạn: hiện toast đỏ rồi tự đưa người chơi về main menu để login lại.</summary>
        private void ShowSessionExpired(string message)
        {
            ShowToast(message, new Color(0.78f, 0.20f, 0.20f), 3f);
            StartCoroutine(ReturnToMenuAfter(3f));
        }

        private IEnumerator ReturnToMenuAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            Time.timeScale = 1f;
            // Shutdown runner (kết thúc session coop) rồi về menu — đồng nhất với Quit ở Pause/GameOver.
            var runner = _controller != null ? _controller.Runner : null;
            // destroyGameObject:false — giữ NetworkLauncher (runner nằm trên GO bền của nó) để vào lại
            // phòng được. Xem GameUIController.Pause.OnPauseQuit.
            if (runner != null) runner.Shutdown(destroyGameObject: false);
            SceneManager.LoadScene("Main_Menu_UI");
        }

        private void ShowToast(string message, Color bg, float seconds)
        {
            if (_root == null) return;
            EnsureToast();
            _saveToast.text = message;
            _saveToast.style.backgroundColor = bg;
            _saveToast.style.display = DisplayStyle.Flex;

            if (_toastRoutine != null) StopCoroutine(_toastRoutine);
            _toastRoutine = StartCoroutine(HideToastAfter(seconds));
        }

        private IEnumerator HideToastAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds); // realtime: solo pause đặt timeScale=0
            if (_saveToast != null) _saveToast.style.display = DisplayStyle.None;
            _toastRoutine = null;
        }

        private void EnsureToast()
        {
            if (_saveToast != null) return;
            _saveToast = new Label
            {
                name = "save-toast",
                pickingMode = PickingMode.Ignore
            };
            var s = _saveToast.style;
            s.position = Position.Absolute;
            s.top = 24;
            s.right = 24;
            s.paddingLeft = 16; s.paddingRight = 16; s.paddingTop = 10; s.paddingBottom = 10;
            s.color = Color.white;
            // Cỡ chữ set INLINE ở đây nên USS KHÔNG đè được → phải tăng trực tiếp (yêu cầu user:
            // to hơn cho dễ đọc). 24 = bội số của 16 (cỡ vẽ gốc BoldPixels) nên nét nhất.
            s.fontSize = 24;
            s.unityFontStyleAndWeight = FontStyle.Bold;
            s.borderTopLeftRadius = 6; s.borderTopRightRadius = 6;
            s.borderBottomLeftRadius = 6; s.borderBottomRightRadius = 6;
            s.maxWidth = 420;
            s.whiteSpace = WhiteSpace.Normal;
            s.display = DisplayStyle.None;
            _root.Add(_saveToast); // nổi trên cùng, không phụ thuộc overlay nào
        }
    }
}
