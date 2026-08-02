using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Attrition.Gameplay.Environment
{
    /// <summary>
    /// Bảng GIỚI THIỆU THÀNH VIÊN nhóm — hiện ở GÓC PHẢI-DƯỚI màn hình kiểu Hollow Knight (tên khu vực),
    /// trồi lên + mờ dần rồi tự ẩn. Dùng để chạy sau bảng hướng dẫn Map 1 (credits nhóm SEP490).
    ///
    /// Tự dựng Canvas runtime (giống TutorialPanel/SceneFader) nên không đụng GameUI.uxml. Singleton nhẹ.
    /// TutorialPrompt gọi Show() khi bảng hướng dẫn vừa đóng. Chỉ hiện MỘT LẦN mỗi lượt chơi (guard theo
    /// creditsId; reset khi mở game mới = process mới).
    /// </summary>
    public class TeamCreditsPanel : MonoBehaviour
    {
        private static TeamCreditsPanel _instance;
        private static readonly System.Collections.Generic.HashSet<string> _shown = new System.Collections.Generic.HashSet<string>();

        private CanvasGroup _group;
        private TextMeshProUGUI _titleLabel;
        private TextMeshProUGUI _bodyLabel;
        private Coroutine _routine;

        /// <summary>Hiện từng thành viên riêng: fade in 1.5s, giữ 2.5s, fade out 1.5s.</summary>
        public static void Show(string creditsId, string title, string[] members)
        {
            if (!string.IsNullOrEmpty(creditsId))
            {
                if (_shown.Contains(creditsId)) return;
                _shown.Add(creditsId);
            }
            EnsureInstance();
            _instance.ShowInternal(title, members);
        }

        private static void EnsureInstance()
        {
            if (_instance != null) return;

            var canvasObj = new GameObject("TeamCreditsCanvas");
            DontDestroyOnLoad(canvasObj);
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 480; // dưới tutorial (500) để tutorial luôn nổi hơn khi trùng thời điểm
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            _instance = canvasObj.AddComponent<TeamCreditsPanel>();
            _instance.BuildUI(canvasObj.transform);
        }

        private void BuildUI(Transform parent)
        {
            var panelGo = new GameObject("Panel");
            panelGo.transform.SetParent(parent, false);
            var rt = panelGo.AddComponent<RectTransform>();
            // Neo GÓC PHẢI-DƯỚI (kiểu tên khu vực Hollow Knight).
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-60f, 80f);
            rt.sizeDelta = new Vector2(420f, 220f);

            _group = panelGo.AddComponent<CanvasGroup>();
            _group.alpha = 0f;

            // KHÔNG nền đục — chữ nổi trực tiếp trên cảnh (giống tên khu vực HK). Căn phải.
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(panelGo.transform, false);
            var titleRt = titleGo.AddComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f); titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(1f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -8f);
            titleRt.sizeDelta = new Vector2(0f, 40f);
            _titleLabel = titleGo.AddComponent<TextMeshProUGUI>();
            _titleLabel.alignment = TextAlignmentOptions.Right;
            _titleLabel.fontSize = 26; _titleLabel.fontStyle = FontStyles.SmallCaps;
            _titleLabel.characterSpacing = 5f;
            _titleLabel.color = new Color(0.95f, 0.92f, 0.78f);
            _titleLabel.raycastTarget = false;

            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(panelGo.transform, false);
            var bodyRt = bodyGo.AddComponent<RectTransform>();
            bodyRt.anchorMin = new Vector2(0f, 0f); bodyRt.anchorMax = new Vector2(1f, 1f);
            bodyRt.offsetMin = new Vector2(0f, 0f); bodyRt.offsetMax = new Vector2(0f, -48f);
            _bodyLabel = bodyGo.AddComponent<TextMeshProUGUI>();
            _bodyLabel.alignment = TextAlignmentOptions.TopRight;
            _bodyLabel.fontSize = 20; _bodyLabel.color = new Color(0.88f, 0.88f, 0.92f);
            _bodyLabel.raycastTarget = false;

            panelGo.SetActive(false);
        }

        private void ShowInternal(string title, string[] members)
        {
            if (_titleLabel == null) return;
            _titleLabel.text = string.IsNullOrEmpty(title) ? "TEAM" : title;
            _group.transform.parent.gameObject.SetActive(true);
            _group.gameObject.SetActive(true);

            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(ShowRoutine(members ?? new string[0]));
        }

        private IEnumerator ShowRoutine(string[] members)
        {
            var panel = _group.gameObject;
            panel.SetActive(true);
            _group.alpha = 0f;

            foreach (var member in members)
            {
                if (string.IsNullOrWhiteSpace(member)) continue;

                _bodyLabel.text = member;
                yield return Fade(0f, 1f, FadeSeconds);
                yield return new WaitForSecondsRealtime(HoldSeconds);
                yield return Fade(1f, 0f, FadeSeconds);
            }

            panel.SetActive(false);
            _routine = null;
        }

        private const float FadeSeconds = 1.5f;
        private const float HoldSeconds = 2.5f;

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
