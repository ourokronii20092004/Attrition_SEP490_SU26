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
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (_instance == this)
                UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        /// <summary>
        /// Scene mới load xong → LUÔN mở màn (fade-in) và nhả cờ IsTransitioning.
        /// RoomTransitionZone fade-đen rồi LoadScene nhưng không ai fade-in ở scene mới; canvas này
        /// DontDestroyOnLoad nên màn đen ở lại vĩnh viễn → map mới "không hiện camera". Ngoài ra
        /// IsTransitioning kẹt true còn khoá input player (PlayerController) và chặn popup tên khu vực.
        /// </summary>
        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene,
                                  UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (_fadeImage == null) { IsTransitioning = false; return; }
            StartCoroutine(FadeInAfterSceneLoad());
        }

        private IEnumerator FadeInAfterSceneLoad()
        {
            // Chờ 1 frame cho camera/player của scene mới khởi tạo xong rồi mới mở màn.
            yield return null;
            yield return FadeIn(0.5f);
            IsTransitioning = false;   // chắc chắn nhả cờ dù fade bị ngắt
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

        /// <summary>
        /// Màn ĐEN NGAY rồi sáng dần — dùng cho REST / FAST-TRAVEL giữa các checkpoint.
        /// Lúc event tới thì host ĐÃ teleport xong, nên nếu fade-out mới thì người chơi thấy cảnh cũ rồi
        /// mới tối → cảm giác "chớp màn hình". Đen ngay rồi fade-in cho ra đúng cảm giác chuyển room
        /// (tối → hiện ra chỗ mới). Gọi được từ RPC / script không phải MonoBehaviour trong scene.
        /// </summary>
        public static void FlashBlack(float hold = 0.2f, float fadeIn = 0.5f)
        {
            CreateInstance();
            // Đang có fade khác chạy (vd RoomTransitionTrigger tự fade quanh lúc teleport) → KHÔNG chồng
            // thêm coroutine, tránh hai fade giành nhau alpha làm màn nháy loang lổ.
            if (IsTransitioning) return;
            _instance.StartCoroutine(_instance.FlashRoutine(hold, fadeIn));
        }

        private IEnumerator FlashRoutine(float hold, float fadeIn)
        {
            IsTransitioning = true;
            SetAlphaImmediate(1f);                       // đen tức thì — không có pha tối dần gây "chớp"
            yield return new WaitForSeconds(hold);
            yield return FadeIn(fadeIn);
        }

        /// <summary>Đặt alpha màn đen ngay lập tức (không animate).</summary>
        private static void SetAlphaImmediate(float a)
        {
            CreateInstance();
            if (_instance._fadeImage == null) return;
            var c = _instance._fadeImage.color;
            c.a = Mathf.Clamp01(a);
            _instance._fadeImage.color = c;
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
