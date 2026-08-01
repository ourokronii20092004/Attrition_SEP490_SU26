using System.Collections;
using UnityEngine;

namespace Attrition.Systems
{
    /// <summary>
    /// 1 dòng SFX gameplay có thể chỉnh trong Inspector: kéo clip vào, chỉnh âm lượng, cao độ (pitch),
    /// và độ trễ phát (delay) cho từng loại. Nhiều clip trong 1 entry → phát ngẫu nhiên cho đỡ nhàm.
    /// </summary>
    [System.Serializable]
    public class SfxEntry
    {
        [Tooltip("Kéo 1 hoặc NHIỀU clip vào đây. Nhiều clip → mỗi lần phát chọn ngẫu nhiên 1 cái.")]
        public AudioClip[] clips;

        [Tooltip("Âm lượng riêng của âm này (nhân thêm với SFX Volume trong Settings).")]
        [Range(0f, 1f)] public float volume = 1f;

        [Tooltip("Cao độ / tốc độ phát. 1 = gốc, <1 = trầm/chậm, >1 = cao/nhanh.")]
        [Range(0.1f, 3f)] public float pitch = 1f;

        [Tooltip("Ngẫu nhiên hoá pitch ± giá trị này mỗi lần phát (0 = tắt). Giúp âm lặp đỡ máy móc.")]
        [Range(0f, 0.5f)] public float pitchVariance = 0f;

        [Tooltip("Hoãn phát bao nhiêu giây sau khi được gọi (0 = phát ngay). Dùng để khớp với khung animation.")]
        [Range(0f, 1f)] public float delay = 0f;

        public bool HasClip => clips != null && clips.Length > 0;

        /// <summary>Chọn 1 clip (ngẫu nhiên nếu có nhiều).</summary>
        public AudioClip Pick()
        {
            if (!HasClip) return null;
            return clips.Length == 1 ? clips[0] : clips[Random.Range(0, clips.Length)];
        }
    }

    /// <summary>
    /// SFX gameplay: singleton bền (DontDestroyOnLoad). Đặt sẵn trong scene Menu/Bootstrap và KÉO CLIP
    /// vào các ô ở Inspector. Nếu không có sẵn trong scene, tự nạp prefab "GameSfx" từ Resources; không
    /// có nữa thì tạo object trống (khi đó chưa gán clip nào → im lặng, không lỗi).
    ///
    /// Âm lượng cuối = volume-riêng-của-entry × SFX Volume (GameSettings). MasterVolume áp toàn cục qua
    /// AudioListener nên không nhân lại ở đây. 2D, mỗi máy tự phát local (âm KHÔNG networked).
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
                    // 1) Ưu tiên instance đã đặt sẵn trong scene (có clip gán qua Inspector).
                    _instance = FindFirstObjectByType<GameSfx>();
                    // 2) Không có → thử nạp prefab đã cấu hình từ Resources/GameSfx.
                    if (_instance == null)
                    {
                        var prefab = Resources.Load<GameObject>("GameSfx");
                        if (prefab != null)
                        {
                            var go = Instantiate(prefab);
                            go.name = "[GameSfx]";
                            _instance = go.GetComponent<GameSfx>();
                        }
                    }
                    // 3) Vẫn không có → tạo trống (chưa gán clip → im lặng, tránh NullReference).
                    if (_instance == null)
                    {
                        var go = new GameObject("[GameSfx]");
                        _instance = go.AddComponent<GameSfx>();
                    }
                }
                return _instance;
            }
        }

        [Header("Chiến đấu")]
        [SerializeField] private SfxEntry attack = new();
        [SerializeField] private SfxEntry charge = new();
        [SerializeField] private SfxEntry hit = new();
        [Tooltip("Âm player bị trúng đòn; tách khỏi hit = vũ khí đánh trúng enemy.")]
        [SerializeField] private SfxEntry hurt = new();

        [Header("Di chuyển")]
        [SerializeField] private SfxEntry jump = new();
        [SerializeField] private SfxEntry land = new();
        [SerializeField] private SfxEntry dash = new();
        [Tooltip("Bước chân MẶC ĐỊNH (map 1-4: nền cỏ/đất) — kéo nhiều clip vào để xen kẽ ngẫu nhiên khi chạy.")]
        [SerializeField] private SfxEntry step = new();
        [Tooltip("Bước chân cho các scene liệt kê ở 'Alt Step Scenes' (map 5: nền gạch). Để trống = dùng lại bước chân mặc định.")]
        [SerializeField] private SfxEntry stepAlt = new();
        [Tooltip("Tên scene dùng bước chân thay thế (phải khớp tên file scene). Mặc định: Castle - Map 5.")]
        [SerializeField] private string[] altStepScenes = { "Castle - Map 5" };

        [Header("Vật phẩm")]
        [SerializeField] private SfxEntry potion = new();

        private AudioSource _source;
        private bool _useAltStep;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            transform.SetParent(null, true);
            DontDestroyOnLoad(gameObject);

            _source = GetComponent<AudioSource>();
            if (_source == null) _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f; // 2D

            // GameSfx sống xuyên scene (DontDestroyOnLoad) nên phải tự cập nhật khi đổi map. Dùng
            // sceneLoaded thay vì activeSceneChanged: Fusion có thể load scene kiểu Additive, khi đó
            // active scene không đổi và event kia không bắn.
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded += OnSceneUnloaded;
            RefreshStepSurface();
        }

        private void OnDestroy()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
            => RefreshStepSurface();

        private void OnSceneUnloaded(UnityEngine.SceneManagement.Scene scene) => RefreshStepSurface();

        /// <summary>
        /// Chọn bộ bước chân theo tên scene: có scene nào đang mở nằm trong altStepScenes → dùng
        /// stepAlt (nền gạch), còn lại → step (nền cỏ). Quét TẤT CẢ scene đang mở vì scene UI/gameplay
        /// có thể load Additive — chỉ xem scene vừa load sẽ tắt nhầm bộ gạch khi UI load sau map.
        /// Khớp theo tên scene thay vì component đặt trong scene: map mới chỉ cần thêm 1 dòng ở
        /// Inspector, không phải kéo lại object.
        /// </summary>
        private void RefreshStepSurface()
        {
            _useAltStep = false;
            if (altStepScenes == null || altStepScenes.Length == 0) return;

            int count = UnityEngine.SceneManagement.SceneManager.sceneCount;
            for (int i = 0; i < count; i++)
            {
                string loaded = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).name;
                foreach (var s in altStepScenes)
                    if (!string.IsNullOrEmpty(s) && string.Equals(s, loaded, System.StringComparison.OrdinalIgnoreCase))
                    {
                        _useAltStep = true;
                        return;
                    }
            }
        }

        /// <summary>
        /// Hệ số âm lượng SFX (0..1) = slider "SFX" trong Settings. GameSettings (assembly Persistence)
        /// ĐẨY giá trị này vào qua ApplyToEngine — KHÔNG để GameSfx tự đọc GameSettings, vì Persistence
        /// đã tham chiếu Systems nên chiều ngược lại sẽ tạo circular dependency (Unity không compile).
        /// Mặc định 1 → nếu Settings chưa apply thì phát đúng volume gốc của từng entry.
        /// </summary>
        public static float SfxVolume = 1f;

        private void Play(SfxEntry e)
        {
            if (e == null || !e.HasClip || _source == null) return;

            float vol = e.volume * SfxVolume;
            if (vol <= 0f) return;

            if (e.delay > 0f) StartCoroutine(PlayDelayed(e, vol));
            else Emit(e, vol);
        }

        private IEnumerator PlayDelayed(SfxEntry e, float vol)
        {
            yield return new WaitForSeconds(e.delay);
            Emit(e, vol);
        }

        private void Emit(SfxEntry e, float vol)
        {
            var clip = e.Pick();
            if (clip == null) return;

            // Pitch đặt trên source ngay trước khi phát. Với âm ngắn/ít chồng của game này là đủ chính xác.
            float variance = e.pitchVariance > 0f ? Random.Range(-e.pitchVariance, e.pitchVariance) : 0f;
            _source.pitch = Mathf.Clamp(e.pitch + variance, 0.1f, 3f);
            _source.PlayOneShot(clip, vol);
        }

        public void PlayAttack() => Play(attack);
        public void PlayCharge() => Play(charge);
        public void PlayHit() => Play(hit);
        public void PlayHurt() => Play(hurt);
        public void PlayJump() => Play(jump);
        public void PlayLand() => Play(land);
        public void PlayDash() => Play(dash);
        public void PlayPotion() => Play(potion);
        /// <summary>Bước chân theo bề mặt của scene hiện tại. Chưa gán stepAlt → tự dùng step mặc định.</summary>
        public void PlayStep() => Play(_useAltStep && stepAlt != null && stepAlt.HasClip ? stepAlt : step);
    }
}
