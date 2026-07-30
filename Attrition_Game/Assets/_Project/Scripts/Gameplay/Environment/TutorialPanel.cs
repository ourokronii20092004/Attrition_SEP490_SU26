using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Attrition.Gameplay.Environment
{
    /// <summary>
    /// Panel hướng dẫn hiện ở DƯỚI màn hình (kiểu Hollow Knight). Tự dựng Canvas runtime (giống
    /// SceneFader) nên không phụ thuộc GameUI.uxml. Singleton nhẹ; TutorialPrompt gọi Show().
    /// Đóng khi người chơi bấm phím / click, hoặc tự ẩn sau autoHideSeconds nếu > 0.
    /// </summary>
    public class TutorialPanel : MonoBehaviour
    {
        private static TutorialPanel _instance;

        private CanvasGroup _group;
        private TextMeshProUGUI _titleLabel;
        private TextMeshProUGUI _bodyLabel;
        private Coroutine _routine;

        public static void Show(string title, TutorialPrompt.Line[] lines, float autoHideSeconds,
                                System.Action onClosed = null)
        {
            EnsureInstance();
            _instance.ShowInternal(title, lines, autoHideSeconds, onClosed);
        }

        /// <summary>
        /// Hiện LẦN LƯỢT từng dòng: mỗi bước 1 phím (WASD → Space → J → ...), bấm phím bất kỳ để sang
        /// bước sau. Hết bước thì đóng và gọi onClosed. Có chỉ báo tiến độ (● ○ ○) để người chơi biết
        /// còn mấy bước.
        /// </summary>
        public static void ShowSteps(string title, TutorialPrompt.Line[] lines, float autoHideSeconds,
                                     System.Action onClosed = null)
        {
            EnsureInstance();
            _instance.ShowStepsInternal(title, lines, autoHideSeconds, onClosed);
        }

        private static void EnsureInstance()
        {
            if (_instance != null) return;

            var canvasObj = new GameObject("TutorialCanvas");
            DontDestroyOnLoad(canvasObj);
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            _instance = canvasObj.AddComponent<TutorialPanel>();
            _instance.BuildUI(canvasObj.transform);
        }

        private void BuildUI(Transform parent)
        {
            var panelGo = new GameObject("Panel");
            panelGo.transform.SetParent(parent, false);
            var rt = panelGo.AddComponent<RectTransform>();
            // Neo ở giữa-dưới màn hình (kiểu Hollow Knight).
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 90f);
            rt.sizeDelta = new Vector2(760f, 240f);

            var bg = panelGo.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.04f, 0.07f, 0.88f);
            bg.raycastTarget = false;
            // Khung Ornate Retro UI (9-slice). Không nạp được sprite → giữ nguyên màu phẳng ở trên.
            UiTheme.ApplyPanel(bg);

            _group = panelGo.AddComponent<CanvasGroup>();
            _group.alpha = 0f;

            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(panelGo.transform, false);
            var titleRt = titleGo.AddComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f); titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -16f);
            titleRt.sizeDelta = new Vector2(-40f, 44f);
            _titleLabel = titleGo.AddComponent<TextMeshProUGUI>();
            _titleLabel.alignment = TextAlignmentOptions.Center;
            _titleLabel.fontSize = 30; _titleLabel.fontStyle = FontStyles.SmallCaps;
            _titleLabel.characterSpacing = 6f;
            _titleLabel.color = new Color(0.95f, 0.92f, 0.78f);
            _titleLabel.raycastTarget = false;

            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(panelGo.transform, false);
            var bodyRt = bodyGo.AddComponent<RectTransform>();
            bodyRt.anchorMin = new Vector2(0f, 0f); bodyRt.anchorMax = new Vector2(1f, 1f);
            bodyRt.offsetMin = new Vector2(40f, 24f); bodyRt.offsetMax = new Vector2(-40f, -64f);
            _bodyLabel = bodyGo.AddComponent<TextMeshProUGUI>();
            _bodyLabel.alignment = TextAlignmentOptions.Center;
            _bodyLabel.fontSize = 24; _bodyLabel.color = Color.white;
            _bodyLabel.raycastTarget = false;

            panelGo.SetActive(false);
        }

        private void ShowInternal(string title, TutorialPrompt.Line[] lines, float autoHideSeconds,
                                  System.Action onClosed)
        {
            if (_titleLabel == null) return;
            _titleLabel.text = title;

            var sb = new System.Text.StringBuilder();
            if (lines != null)
                foreach (var l in lines)
                {
                    if (sb.Length > 0) sb.Append('\n');
                    // Phím tô vàng nhạt để nổi bật khỏi mô tả.
                    sb.Append("<color=#FFE08A><b>").Append(l.key).Append("</b></color>   ").Append(l.description);
                }
            _bodyLabel.text = sb.ToString();

            _group.transform.parent.gameObject.SetActive(true); // Panel
            _group.gameObject.SetActive(true);

            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(ShowRoutine(autoHideSeconds, onClosed));
        }

        private void ShowStepsInternal(string title, TutorialPrompt.Line[] lines, float autoHideSeconds,
                                       System.Action onClosed)
        {
            if (_titleLabel == null) return;
            if (lines == null || lines.Length == 0) { onClosed?.Invoke(); return; }

            _titleLabel.text = title;
            _group.transform.parent.gameObject.SetActive(true);
            _group.gameObject.SetActive(true);

            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(StepsRoutine(lines, autoHideSeconds, onClosed));
        }

        private IEnumerator StepsRoutine(TutorialPrompt.Line[] lines, float autoHideSeconds,
                                         System.Action onClosed)
        {
            var panel = _group.gameObject;
            panel.SetActive(true);

            yield return Fade(0f, 1f, 0.25f);

            for (int i = 0; i < lines.Length; i++)
            {
                _bodyLabel.text = BuildStepText(lines, i);

                // Phím CỦA BƯỚC NÀY. Rỗng (không nhận ra phím nào) → chấp nhận phím bất kỳ, để dòng dạng
                // "Di chuyển" không có phím thật vẫn qua được.
                var expected = ParseKeys(lines[i].key);

                // Chờ người chơi bấm ĐÚNG phím đang hướng dẫn mới sang bước kế.
                float shownAt = Time.unscaledTime;
                while (true)
                {
                    bool timedOut = autoHideSeconds > 0f && Time.unscaledTime - shownAt >= autoHideSeconds;

                    // Trễ 0.4s để phím vừa bấm ở bước trước không lật luôn bước này.
                    bool ready = Time.unscaledTime - shownAt > 0.4f;
                    bool advanced = ready && (expected.Count > 0
                        ? AnyKeyDown(expected)
                        : (Input.anyKeyDown || Input.GetMouseButtonDown(0)));

                    if (timedOut || advanced) break;
                    yield return null;
                }

                // Nhịp nghỉ ngắn giữa các bước: hiện LẦN LƯỢT, CHẬM RÃI (yêu cầu user) chứ không nháy
                // sang bước sau ngay khi vừa nhả phím.
                yield return new WaitForSecondsRealtime(StepGapSeconds);
            }

            yield return Fade(1f, 0f, 0.25f);
            panel.SetActive(false);
            _routine = null;
            onClosed?.Invoke();
        }

        /// <summary>Nghỉ giữa 2 bước hướng dẫn (giây thực) — để các bước hiện chậm rãi, không nháy liên tiếp.</summary>
        private const float StepGapSeconds = 0.45f;

        /// <summary>
        /// Đọc chuỗi phím trong Inspector thành danh sách KeyCode. Hỗ trợ nhiều phím cách nhau bởi
        /// '/', ',', '+' hoặc khoảng trắng — vd "A / D", "W,A,S,D", "Space".
        ///
        /// Trả về danh sách RỖNG nếu không nhận ra phím nào (vd key = "Chuột" hay để trống); lúc đó
        /// StepsRoutine chấp nhận phím bất kỳ, tránh việc player bị kẹt không sang bước được.
        /// </summary>
        private static System.Collections.Generic.List<KeyCode> ParseKeys(string raw)
        {
            var result = new System.Collections.Generic.List<KeyCode>();
            if (string.IsNullOrWhiteSpace(raw)) return result;

            var tokens = raw.Split(new[] { '/', ',', '+', ' ', '\t' },
                                   System.StringSplitOptions.RemoveEmptyEntries);
            foreach (var tk in tokens)
            {
                var t = tk.Trim();
                if (t.Length == 0) continue;
                if (TryMapKey(t, out var kc) && !result.Contains(kc)) result.Add(kc);
            }
            return result;
        }

        /// <summary>
        /// Ánh xạ 1 token thành KeyCode. Xử lý riêng các tên hay dùng trong bảng hướng dẫn (Space, Shift,
        /// mũi tên...) rồi mới thử Enum.Parse cho chữ/số đơn.
        /// </summary>
        private static bool TryMapKey(string token, out KeyCode key)
        {
            switch (token.ToLowerInvariant())
            {
                case "space": case "spacebar": key = KeyCode.Space; return true;
                case "shift": case "lshift": key = KeyCode.LeftShift; return true;
                case "ctrl": case "control": key = KeyCode.LeftControl; return true;
                case "alt": key = KeyCode.LeftAlt; return true;
                case "esc": case "escape": key = KeyCode.Escape; return true;
                case "tab": key = KeyCode.Tab; return true;
                case "enter": case "return": key = KeyCode.Return; return true;
                case "left": case "←": key = KeyCode.LeftArrow; return true;
                case "right": case "→": key = KeyCode.RightArrow; return true;
                case "up": case "↑": key = KeyCode.UpArrow; return true;
                case "down": case "↓": key = KeyCode.DownArrow; return true;
            }

            // Chữ/số đơn (A, D, J, 1...) — KeyCode có tên trùng nên Enum.TryParse xử lý được.
            if (System.Enum.TryParse(token, true, out KeyCode parsed)) { key = parsed; return true; }

            key = KeyCode.None;
            return false;
        }

        /// <summary>Có phím nào trong danh sách vừa được bấm?</summary>
        private static bool AnyKeyDown(System.Collections.Generic.List<KeyCode> keys)
        {
            for (int i = 0; i < keys.Count; i++)
                if (Input.GetKeyDown(keys[i])) return true;
            return false;
        }

        /// <summary>Dòng của bước hiện tại + chỉ báo tiến độ (● đã/đang, ○ còn lại).</summary>
        private static string BuildStepText(TutorialPrompt.Line[] lines, int index)
        {
            var l = lines[index];
            var sb = new System.Text.StringBuilder();
            sb.Append("<size=34><color=#FFE08A><b>").Append(l.key).Append("</b></color></size>\n\n");
            sb.Append(l.description);

            // Chỉ báo tiến độ — chỉ hiện khi có nhiều hơn 1 bước.
            if (lines.Length > 1)
            {
                sb.Append("\n\n<size=20><color=#8A8A99>");
                for (int i = 0; i < lines.Length; i++)
                    sb.Append(i <= index ? "● " : "○ ");
                sb.Append("</color></size>");
            }
            return sb.ToString();
        }

        private IEnumerator ShowRoutine(float autoHideSeconds, System.Action onClosed)
        {
            var panel = _group.gameObject;
            panel.SetActive(true);

            // Fade in (realtime — solo pause đặt timeScale=0).
            yield return Fade(0f, 1f, 0.25f);

            // Chờ input đóng (bỏ qua vài frame đầu để không đóng ngay bởi chính phím vừa nhấn).
            float shownAt = Time.unscaledTime;
            while (true)
            {
                bool timedOut = autoHideSeconds > 0f && Time.unscaledTime - shownAt >= autoHideSeconds;
                bool dismissed = Time.unscaledTime - shownAt > 0.4f
                                 && (Input.anyKeyDown || Input.GetMouseButtonDown(0));
                if (timedOut || dismissed) break;
                yield return null;
            }

            yield return Fade(1f, 0f, 0.25f);
            panel.SetActive(false);
            _routine = null;
            onClosed?.Invoke();
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                _group.alpha = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }
            _group.alpha = to;
        }
    }
}
