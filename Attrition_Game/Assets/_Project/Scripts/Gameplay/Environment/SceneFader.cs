using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace Attrition.Gameplay.Environment
{
    public class SceneFader : MonoBehaviour
    {
        private static SceneFader _instance;
        private Image _fadeImage;
        
        public static bool IsTransitioning { get; private set; } = false;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private static void CreateInstance()
        {
            if (_instance != null) return;

            var canvasObj = new GameObject("SceneFaderCanvas");
            DontDestroyOnLoad(canvasObj);
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999; // Lớp trên cùng
            
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

            var imgObj = new GameObject("FadeImage");
            imgObj.transform.SetParent(canvasObj.transform, false);
            
            var img = imgObj.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0); // Trong suốt ban đầu
            img.raycastTarget = true; // Chặn các thao tác click chuột trong lúc transition
            
            var rect = img.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;

            _instance = canvasObj.AddComponent<SceneFader>();
            _instance._fadeImage = img;
        }

        public static IEnumerator FadeOut(float duration)
        {
            CreateInstance();
            IsTransitioning = true;
            
            if (_instance._fadeImage == null) yield break;

            float time = 0;
            Color color = _instance._fadeImage.color;
            while (time < duration)
            {
                time += Time.deltaTime;
                color.a = Mathf.Clamp01(time / duration);
                _instance._fadeImage.color = color;
                yield return null;
            }
            color.a = 1f;
            _instance._fadeImage.color = color;
        }

        public static IEnumerator FadeIn(float duration)
        {
            CreateInstance();
            
            if (_instance._fadeImage == null) 
            {
                IsTransitioning = false;
                yield break;
            }

            float time = 0;
            Color color = _instance._fadeImage.color;
            while (time < duration)
            {
                time += Time.deltaTime;
                color.a = 1f - Mathf.Clamp01(time / duration);
                _instance._fadeImage.color = color;
                yield return null;
            }
            color.a = 0f;
            _instance._fadeImage.color = color;
            
            IsTransitioning = false;
        }
    }
}
