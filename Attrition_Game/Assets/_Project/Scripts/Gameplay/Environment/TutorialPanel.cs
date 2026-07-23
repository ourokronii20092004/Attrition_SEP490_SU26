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
