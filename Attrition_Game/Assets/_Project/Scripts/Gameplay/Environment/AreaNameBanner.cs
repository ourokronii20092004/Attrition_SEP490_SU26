using System.Collections;
using UnityEngine;
using TMPro;

namespace Attrition.Gameplay.Environment
{
    /// <summary>
    /// Banner TÊN KHU VỰC giữa màn hình (kiểu Hollow Knight / Afterimage): hiện chữ, giữ một lúc,
    /// mờ dần rồi biến mất. Font hơi pixel (letter-spacing nhẹ, không làm khó đọc).
    ///
    /// Tự dựng runtime một Canvas overlay + TMP text (DontDestroyOnLoad-free, sống trong scene gameplay).
    /// Gọi qua API tĩnh: AreaNameBanner.Show("Tên khu"). AreaNameZone gọi khi player vào khu mới.
    /// Chỉ là hiệu ứng hình local — mỗi máy tự hiện banner của mình.
    /// </summary>
    public class AreaNameBanner : MonoBehaviour
    {
        private static AreaNameBanner _instance;

        private CanvasGroup _group;
        private TextMeshProUGUI _label;
        private Coroutine _running;

        [Header("Timing")]
        [SerializeField] private float fadeInTime = 0.6f;
        [SerializeField] private float holdTime = 1.6f;
        [SerializeField] private float fadeOutTime = 1.2f;

        public static void Show(string areaName)
        {
            if (string.IsNullOrEmpty(areaName)) return;
            EnsureInstance();
            _instance.Play(areaName);
        }

        private static void EnsureInstance()
        {
            if (_instance != null) return;
            var go = new GameObject("AreaNameBanner");
            _instance = go.AddComponent<AreaNameBanner>();
            _instance.Build();
        }

        private void Build()
        {
            // Canvas overlay riêng (sortingOrder cao để nổi trên gameplay, dưới menu nếu cần).
            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>().uiScaleMode =
                UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;

            _group = canvasGo.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;

            var textGo = new GameObject("AreaLabel");
            textGo.transform.SetParent(canvasGo.transform, false);
            _label = textGo.AddComponent<TextMeshProUGUI>();
            _label.alignment = TextAlignmentOptions.Center;
            _label.fontSize = 64;
            _label.fontStyle = FontStyles.SmallCaps;     // hơi "pixel/cổ" nhưng vẫn dễ đọc
            _label.characterSpacing = 8f;                // giãn chữ nhẹ cho cảm giác trang trọng
            _label.color = new Color(0.95f, 0.92f, 0.8f, 1f);
            _label.textWrappingMode = TextWrappingModes.NoWrap;
            _label.raycastTarget = false;

            // Canh giữa màn hình, hơi cao hơn tâm một chút (kiểu HK).
            var rt = _label.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 120f);
            rt.sizeDelta = new Vector2(1200f, 160f);
        }

        private void Play(string areaName)
        {
            _label.text = areaName;
            if (_running != null) StopCoroutine(_running);
            _running = StartCoroutine(Sequence());
        }

        private IEnumerator Sequence()
        {
            yield return Fade(_group.alpha, 1f, fadeInTime);
            yield return new WaitForSecondsRealtime(holdTime);
            yield return Fade(1f, 0f, fadeOutTime);
            _running = null;
        }

        private IEnumerator Fade(float from, float to, float dur)
        {
            if (dur <= 0.01f) { _group.alpha = to; yield break; }
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime; // chạy cả khi game pause
                _group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / dur));
                yield return null;
            }
            _group.alpha = to;
        }
    }
}
