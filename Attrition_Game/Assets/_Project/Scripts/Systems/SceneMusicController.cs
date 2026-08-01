using UnityEngine;

namespace Attrition.Systems
{
    /// <summary>Nhạc map + nhạc phòng boss. Clip để trống là hợp lệ để designer gán sau.</summary>
    public class SceneMusicController : MonoBehaviour
    {
        private static SceneMusicController _active;

        [Header("Scene Music Configuration")]
        [SerializeField] private AudioClip sceneBgmClip;
        [SerializeField] private AudioClip bossBgmClip;
        [SerializeField][Range(0f, 1f)] private float volume = 0.7f;
        [SerializeField] private float fadeDuration = 0.8f;

        private void Awake() => _active = this;

        private void Start()
        {
            // Null = map chưa được gán nhạc: dừng bài còn sót từ scene trước vì GameBgm là DontDestroyOnLoad.
            if (sceneBgmClip != null) GameBgm.Instance.Play(sceneBgmClip, volume, fadeDuration);
            else GameBgm.Instance.Stop(fadeDuration);
        }

        private void OnDestroy()
        {
            if (_active == this) _active = null;
        }

        /// <summary>Gameplay gọi khi encounter bắt đầu. Null boss clip = giữ nhạc map.</summary>
        public static void NotifyBossStarted(AudioClip overrideClip = null)
        {
            if (_active == null) return;
            var clip = overrideClip != null ? overrideClip : _active.bossBgmClip;
            if (clip != null) GameBgm.Instance.Play(clip, _active.volume, _active.fadeDuration);
        }

        /// <summary>Boss chết/reset/despawn: trả nhạc map, hoặc tắt nếu scene chưa có clip.</summary>
        public static void NotifyBossEnded()
        {
            if (_active == null) return;
            if (_active.sceneBgmClip != null)
                GameBgm.Instance.Play(_active.sceneBgmClip, _active.volume, _active.fadeDuration);
            else
                GameBgm.Instance.Stop(_active.fadeDuration);
        }
    }
}
