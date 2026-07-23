using UnityEngine;
using System.Collections;

namespace Attrition.Systems
{
    /// <summary>
    /// Bộ quản lý nhạc nền (BGM) Singleton bền vững (DontDestroyOnLoad).
    /// Hỗ trợ chuyển đổi bài hát mượt mà (Fade Out/Fade In) giữa các Scene.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class GameBgm : MonoBehaviour
    {
        private static GameBgm _instance;
        public static GameBgm Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<GameBgm>();
                    if (_instance == null)
                    {
                        // Thử load từ Resources nếu có prefab
                        var prefab = Resources.Load<GameObject>("GameBgm");
                        if (prefab != null)
                        {
                            var go = Instantiate(prefab);
                            go.name = "[GameBgm]";
                            _instance = go.GetComponent<GameBgm>();
                        }
                    }
                    if (_instance == null)
                    {
                        var go = new GameObject("[GameBgm]");
                        _instance = go.AddComponent<GameBgm>();
                    }
                }
                return _instance;
            }
        }

        private AudioSource _source;
        private float _baseVolume = 1f;

        public static float MusicVolume = 1f;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            _source = GetComponent<AudioSource>();
            if (_source == null) _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = true;
            _source.spatialBlend = 0f; // Nhạc nền luôn là 2D
        }

        private void Update()
        {
            // Cập nhật âm lượng thời gian thực khi người chơi kéo thanh Slider
            if (_source != null && _source.isPlaying)
            {
                _source.volume = _baseVolume * MusicVolume;
            }
        }

        /// <summary>
        /// Phát một bài nhạc nền mới. Nếu đang có bài khác phát, sẽ tự động chuyển mượt (Crossfade).
        /// </summary>
        /// <param name="clip">File nhạc cần phát</param>
        /// <param name="volume">Âm lượng riêng của bài nhạc đó (0..1)</param>
        /// <param name="fadeDuration">Thời gian chuyển nhạc (giây)</param>
        public void Play(AudioClip clip, float volume = 1f, float fadeDuration = 0.8f)
        {
            if (_source == null) return;
            if (clip == null)
            {
                Stop(fadeDuration);
                return;
            }

            // Nếu bài nhạc yêu cầu đang phát rồi thì giữ nguyên, không phát lại từ đầu
            if (_source.clip == clip && _source.isPlaying)
            {
                _baseVolume = volume;
                return;
            }

            StopAllCoroutines();
            StartCoroutine(TransitionToClip(clip, volume, fadeDuration));
        }

        /// <summary>
        /// Tắt nhạc nền từ từ (Fade Out).
        /// </summary>
        public void Stop(float fadeDuration = 0.8f)
        {
            if (_source == null || !_source.isPlaying) return;
            StopAllCoroutines();
            StartCoroutine(TransitionToClip(null, 0f, fadeDuration));
        }

        private IEnumerator TransitionToClip(AudioClip newClip, float targetVolume, float duration)
        {
            // 1. Fade Out bài cũ
            if (_source.isPlaying && duration > 0f)
            {
                float startVol = _baseVolume;
                for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
                {
                    _baseVolume = Mathf.Lerp(startVol, 0f, t / duration);
                    yield return null;
                }
            }

            _source.Stop();
            _source.clip = newClip;
            _baseVolume = targetVolume;

            if (newClip == null) yield break;

            _source.Play();

            // 2. Fade In bài mới
            if (duration > 0f)
            {
                _baseVolume = 0f;
                for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
                {
                    _baseVolume = Mathf.Lerp(0f, targetVolume, t / duration);
                    yield return null;
                }
            }
            _baseVolume = targetVolume;
        }
    }
}