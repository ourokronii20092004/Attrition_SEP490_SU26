using UnityEngine;

namespace Attrition.Systems
{
    public class SceneMusicController : MonoBehaviour
    {
        [Header("Scene Music Configuration")]
        [SerializeField] private AudioClip sceneBgmClip;
        [SerializeField][Range(0f, 1f)] private float volume = 0.7f;
        [SerializeField] private float fadeDuration = 0.8f;

        void Start()
        {
            if (sceneBgmClip != null)
            {
                GameBgm.Instance.Play(sceneBgmClip, volume, fadeDuration);
        }
        }
    }
}
