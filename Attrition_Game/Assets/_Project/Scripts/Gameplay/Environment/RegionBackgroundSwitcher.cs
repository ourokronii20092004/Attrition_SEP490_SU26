using System.Collections;
using UnityEngine;

namespace Attrition.Gameplay.Environment
{
    /// <summary>
    /// Đổi BACKGROUND theo khu vực kiểu Afterimage (vd: dưới lòng đất → lên mặt đất trong CÙNG 1 room).
    /// Hiệu ứng: background hiện tại MỜ DẦN → ĐEN → đổi sang bộ sprite mới → MỜ DẦN HIỆN RA.
    ///
    /// Cách dùng: gắn lên cùng GameObject "ParallaxBackground" (cha của các BG_Layer). Mỗi layer là 1
    /// SpriteRenderer (+ ParallaxLayer). BackgroundZone gọi SwitchTo(spriteSet) khi player đi vào vùng.
    ///
    /// Local/visual thuần (không networked) — mỗi máy tự đổi background của mình.
    /// </summary>
    public class RegionBackgroundSwitcher : MonoBehaviour
    {
        [Tooltip("Các lớp background (SpriteRenderer) sẽ được fade + đổi sprite. Theo thứ tự xa→gần.")]
        [SerializeField] private SpriteRenderer[] layers;
        [Tooltip("Thời gian mờ dần ra đen (giây).")]
        [SerializeField] private float fadeOutTime = 0.7f;
        [Tooltip("Thời gian hiện dần từ đen (giây).")]
        [SerializeField] private float fadeInTime = 0.7f;

        private Coroutine _running;
        private int _currentSetId = -1; // tránh đổi lại cùng 1 bộ

        private void Awake()
        {
            if (layers == null || layers.Length == 0)
                layers = GetComponentsInChildren<SpriteRenderer>();
        }

        /// <summary>
        /// Đổi sang bộ sprite mới (mỗi phần tử ứng 1 layer theo thứ tự). setId để chống đổi trùng.
        /// Nếu newSprites ngắn hơn số layer → các layer thừa giữ sprite cũ (chỉ fade).
        /// </summary>
        public void SwitchTo(Sprite[] newSprites, int setId)
        {
            if (setId == _currentSetId) return;
            _currentSetId = setId;
            if (_running != null) StopCoroutine(_running);
            _running = StartCoroutine(SwitchRoutine(newSprites));
        }

        private IEnumerator SwitchRoutine(Sprite[] newSprites)
        {
            // 1. Mờ dần ra đen (giảm alpha + tối màu về đen để mượt như Afterimage).
            yield return Fade(1f, 0f, fadeOutTime);

            // 2. Đổi sprite cho từng layer (lúc màn đang đen).
            if (newSprites != null && layers != null)
            {
                for (int i = 0; i < layers.Length && i < newSprites.Length; i++)
                {
                    if (layers[i] == null || newSprites[i] == null) continue;
                    layers[i].sprite = newSprites[i];
                    // Cập nhật lại kích thước tiled nếu đang dùng Tiled draw mode.
                    if (layers[i].drawMode != SpriteDrawMode.Simple)
                        layers[i].size = new Vector2(newSprites[i].bounds.size.x * 3f, newSprites[i].bounds.size.y);
                }
            }

            // 3. Hiện dần ra.
            yield return Fade(0f, 1f, fadeInTime);
            _running = null;
        }

        private IEnumerator Fade(float from, float to, float dur)
        {
            if (layers == null || layers.Length == 0 || dur <= 0.01f) yield break;
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                float a = Mathf.Lerp(from, to, k);
                foreach (var sr in layers)
                {
                    if (sr == null) continue;
                    var c = sr.color; c.a = a; sr.color = c;
                }
                yield return null;
            }
            foreach (var sr in layers)
            {
                if (sr == null) continue;
                var c = sr.color; c.a = to; sr.color = c;
            }
        }
    }
}
