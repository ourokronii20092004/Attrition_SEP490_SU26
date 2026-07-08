using UnityEngine;

namespace Attrition.Systems
{
    /// <summary>
    /// SFX gameplay tối giản: tự nạp clip từ Resources/SFX và phát one-shot. Không cần gán Inspector.
    /// Tự tạo instance khi lần đầu được gọi (không cần đặt sẵn trong scene). 2D, mỗi máy tự phát local
    /// — âm thanh KHÔNG networked (mỗi client tự nghe hành động của mình + của peer qua animation event).
    /// </summary>
    public class GameSfx : MonoBehaviour
    {
        private static GameSfx _instance;
        public static GameSfx Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[GameSfx]");
                    _instance = go.AddComponent<GameSfx>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private AudioSource _source;

        // Clip nạp sẵn từ Resources/SFX (tên file không đuôi).
        private AudioClip _swordAttack, _swordCharge, _swordHit, _jump, _land, _step1, _step2, _dash, _potion;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;

            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f; // 2D

            _swordAttack = Resources.Load<AudioClip>("SFX/SwordAttack");
            _swordCharge = Resources.Load<AudioClip>("SFX/SwordCharge");
            _swordHit = Resources.Load<AudioClip>("SFX/SwordHit");
            _jump = Resources.Load<AudioClip>("SFX/Jump");
            _land = Resources.Load<AudioClip>("SFX/Land");
            _step1 = Resources.Load<AudioClip>("SFX/Step1");
            _step2 = Resources.Load<AudioClip>("SFX/Step2");
            _dash = Resources.Load<AudioClip>("SFX/Dash");
            _potion = Resources.Load<AudioClip>("SFX/Potion");
        }

        private void Play(AudioClip clip, float volume)
        {
            if (clip != null && _source != null) _source.PlayOneShot(clip, volume);
        }

        public void PlayAttack() => Play(_swordAttack, 0.7f);
        public void PlayCharge() => Play(_swordCharge, 0.85f);
        public void PlayHit() => Play(_swordHit, 0.8f);
        public void PlayJump() => Play(_jump, 0.5f);
        public void PlayLand() => Play(_land, 0.45f);
        public void PlayDash() => Play(_dash, 0.6f);
        public void PlayPotion() => Play(_potion, 0.7f);

        // Bước chân xen kẽ 2 clip cho tự nhiên.
        private bool _stepToggle;
        public void PlayStep()
        {
            Play(_stepToggle ? _step1 : _step2, 0.35f);
            _stepToggle = !_stepToggle;
        }
    }
}
