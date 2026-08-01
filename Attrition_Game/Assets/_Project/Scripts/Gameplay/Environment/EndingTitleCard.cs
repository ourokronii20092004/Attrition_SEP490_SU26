using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Attrition.Gameplay.Environment
{
    /// <summary>Title card kết game, render trên màn đen của SceneFader.</summary>
    public static class EndingTitleCard
    {
        public static IEnumerator Show(string text, float fadeIn = 1.5f, float hold = 2f, float fadeOut = 1.5f)
        {
            var root = new GameObject("EndingTitleCard");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            root.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

            var group = root.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = true;

            var label = new GameObject("Title").AddComponent<TextMeshProUGUI>();
            label.transform.SetParent(root.transform, false);
            label.text = text;
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 76f;
            label.fontStyle = FontStyles.SmallCaps;
            label.characterSpacing = 12f;
            label.color = new Color(0.95f, 0.92f, 0.8f);
            label.raycastTarget = false;
            var rect = label.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;

            yield return Fade(group, 0f, 1f, fadeIn);
            yield return new WaitForSecondsRealtime(hold);
            yield return Fade(group, 1f, 0f, fadeOut);
            Object.Destroy(root);
        }

        private static IEnumerator Fade(CanvasGroup group, float from, float to, float duration)
        {
            if (duration <= 0f) { group.alpha = to; yield break; }
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
                yield return null;
            }
            group.alpha = to;
        }
    }
}
