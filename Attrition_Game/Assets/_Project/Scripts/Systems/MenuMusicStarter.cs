using UnityEngine;

namespace Attrition.Systems
{
    public class MenuMusicStarter : MonoBehaviour
    {
        [Header("Menu Music Configuration")]
        [SerializeField] private AudioClip menuBgmClip;
        [SerializeField][Range(0f, 1f)] private float volume = 0.8f;
        [SerializeField] private float _fadeDuration = 0.8f;
        void Start()
        {
            if (menuBgmClip != null)
            {
                GameBgm.Instance.Play(menuBgmClip, volume, _fadeDuration);
            }
        }
    }
}
