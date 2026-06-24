using System.Collections;
using UnityEngine;

namespace Attrition.Gameplay.Environment
{
    /// <summary>
    /// Đổi qua lại giữa NHIỀU ParallaxBackground object riêng biệt (kiểu Afterimage), GIỮ NGUYÊN
    /// số layer của mỗi cái. Mỗi "background" là 1 GameObject (vd ParallaxBackground 3 lớp dưới đất,
    /// ParallaxBackground_Surface 4 lớp mặt đất). Khi đổi: FADE TẮT cái đang hiện → bật cái mới →
    /// FADE HIỆN cái mới. KHÔNG đổi sprite, KHÔNG đụng số layer.
    ///
    /// BackgroundZone gọi ShowBackground(index) khi player vào vùng. Local/visual thuần.
    /// </summary>
    public class ParallaxBackgroundCrossfade : MonoBehaviour
    {
        [Tooltip("Các ParallaxBackground object (mỗi cái giữ nguyên layer riêng). Index 0,1,2... khớp regionId của BackgroundZone.")]
        [SerializeField] private GameObject[] backgrounds;
        [Tooltip("Index background hiển thị lúc bắt đầu.")]
        [SerializeField] private int startIndex = 0;
        [Tooltip("Thời gian fade tắt cái cũ (giây).")]
        [SerializeField] private float fadeOutTime = 0.6f;
        [Tooltip("Thời gian fade hiện cái mới (giây).")]
        [SerializeField] private float fadeInTime = 0.6f;

        private int _current = -1;
        private Coroutine _running;

        private void Start()
        {
            // Bật đúng background khởi đầu, tắt các cái còn lại.
            for (int i = 0; i < backgrounds.Length; i++)
            {
                if (backgrounds[i] == null) continue;
                bool on = (i == startIndex);
                backgrounds[i].SetActive(on);
                SetAlpha(backgrounds[i], on ? 1f : 0f);
            }
            _current = startIndex;
        }

        /// <summary>Đổi sang background theo index (khớp regionId). Bỏ qua nếu đang hiện sẵn.</summary>
        public void ShowBackground(int index)
        {
            if (index == _current) return;
            if (index < 0 || index >= backgrounds.Length || backgrounds[index] == null) return;
            if (_running != null) StopCoroutine(_running);
            _running = StartCoroutine(CrossfadeRoutine(index));
        }

        private IEnumerator CrossfadeRoutine(int index)
        {
            // 1. Fade tắt cái đang hiện.
            if (_current >= 0 && _current < backgrounds.Length && backgrounds[_current] != null)
            {
                yield return FadeObject(backgrounds[_current], 1f, 0f, fadeOutTime);
                backgrounds[_current].SetActive(false);
            }

            // 2. Bật + fade hiện cái mới.
            var next = backgrounds[index];
            next.SetActive(true);
            SetAlpha(next, 0f);
            yield return FadeObject(next, 0f, 1f, fadeInTime);

            _current = index;
            _running = null;
        }

        private IEnumerator FadeObject(GameObject go, float from, float to, float dur)
        {
            var rends = go.GetComponentsInChildren<SpriteRenderer>();
            if (rends.Length == 0 || dur <= 0.01f) { SetAlpha(go, to); yield break; }

            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float a = Mathf.Lerp(from, to, Mathf.Clamp01(t / dur));
                foreach (var sr in rends) { if (sr == null) continue; var c = sr.color; c.a = a; sr.color = c; }
                yield return null;
            }
            SetAlpha(go, to);
        }

        private void SetAlpha(GameObject go, float a)
        {
            foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>())
            {
                if (sr == null) continue;
                var c = sr.color; c.a = a; sr.color = c;
            }
        }
    }
}
