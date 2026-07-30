using Fusion;
using UnityEngine;

namespace Attrition.Gameplay.Player
{
    /// <summary>
    /// Hiệu ứng hình GẮN VÀO NGƯỜI PLAYER: hồi máu (Heal Effect) và lên cấp (Level Up).
    /// Hiện ở GIỮA người, chạy hết chuỗi frame rồi BIẾN MẤT NGAY (không fade, không để lại rác).
    ///
    /// COOP: `PlayerStats.RestoreHP` và `PlayerProgression.OnLevelUp` đều là host-only, nên nếu spawn hình
    /// trực tiếp trong đó thì CHỈ HOST thấy. Vì vậy phải đi qua RPC broadcast — mỗi máy tự dựng hình cục bộ,
    /// không tốn NetworkObject nào (hiệu ứng thuần hình, không ảnh hưởng gameplay).
    ///
    /// Hình là CON của player nên tự đi theo người trong lúc chạy animation; `NetworkTransform` chỉ ghi đè
    /// transform GỐC nên child không bị đụng.
    ///
    /// Gắn lên player prefab (cùng cấp PlayerStats). Frame do editor tool nạp — xem PlayerVfxSetupEditor.
    /// </summary>
    public class PlayerVfx : NetworkBehaviour
    {
        [Header("---- HỒI MÁU ----")]
        [Tooltip("Các frame hiệu ứng hồi máu, theo thứ tự (art: Heal Effect Sprite Sheet). Trống = tắt.")]
        [SerializeField] private Sprite[] healFrames;
        [Tooltip("Thời gian chạy hết hiệu ứng hồi máu (giây).")]
        [SerializeField] private float healDuration = 0.7f;
        [Tooltip("Phóng to hiệu ứng hồi máu.")]
        [SerializeField] private float healScale = 1.2f;

        [Header("---- LÊN CẤP ----")]
        [Tooltip("Các frame hiệu ứng lên cấp, theo thứ tự (art: Level Up). Trống = tắt.")]
        [SerializeField] private Sprite[] levelUpFrames;
        [Tooltip("Thời gian chạy hết hiệu ứng lên cấp (giây).")]
        [SerializeField] private float levelUpDuration = 0.9f;
        [Tooltip("Phóng to hiệu ứng lên cấp.")]
        [SerializeField] private float levelUpScale = 1.6f;

        [Header("---- CHUNG ----")]
        [Tooltip("Vị trí hiệu ứng so với gốc player. (0, 0) = ngay GIỮA người (gốc nằm giữa collider).")]
        [SerializeField] private Vector2 offset = Vector2.zero;
        [Tooltip("Sorting layer vẽ hiệu ứng. Phải nằm SAU layer của sprite player để hiện phía trước.")]
        [SerializeField] private string sortingLayer = "Player";
        [Tooltip("Sorting order trong layer trên. Cao hơn player để không bị người che.")]
        [SerializeField] private int sortingOrder = 20;

        /// <summary>
        /// Giãn cách tối thiểu giữa 2 lần hiện hiệu ứng hồi máu (giây).
        ///
        /// VÌ SAO CẦN: `RestoreHP` được gọi từ NHIỀU nguồn, trong đó accessory Lifesteal gọi MỖI LẦN đánh
        /// trúng quái. Không chặn thì đánh liên tục sẽ spawn hàng chục hiệu ứng chồng nhau, chớp loá màn hình.
        /// </summary>
        [SerializeField] private float healMinInterval = 0.4f;

        private float _nextHealVfxTime;

        // ─────────────────────── API cho host gọi ───────────────────────

        /// <summary>HOST gọi khi player vừa được hồi máu → phát hiệu ứng trên MỌI máy.</summary>
        public void PlayHeal()
        {
            if (!HasStateAuthority) return;
            if (healFrames == null || healFrames.Length == 0) return;

            // Chặn spam ở HOST (một chỗ) thay vì để từng máy tự lọc — đỡ tốn băng thông RPC.
            if (Time.time < _nextHealVfxTime) return;
            _nextHealVfxTime = Time.time + Mathf.Max(0.05f, healMinInterval);

            RPC_PlayVfx(false);
        }

        /// <summary>HOST gọi khi player vừa lên cấp → phát hiệu ứng trên MỌI máy.</summary>
        public void PlayLevelUp()
        {
            if (!HasStateAuthority) return;
            if (levelUpFrames == null || levelUpFrames.Length == 0) return;
            RPC_PlayVfx(true);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_PlayVfx(NetworkBool isLevelUp)
        {
            var frames = isLevelUp ? levelUpFrames : healFrames;
            if (frames == null || frames.Length == 0) return;

            float life = isLevelUp ? levelUpDuration : healDuration;
            float scale = isLevelUp ? levelUpScale : healScale;
            Spawn(frames, life, scale);
        }

        // ─────────────────────── Dựng hình (cục bộ mỗi máy) ───────────────────────

        private void Spawn(Sprite[] frames, float life, float scale)
        {
            var go = new GameObject(gameObject.name + "_Vfx");

            // Làm CON của player để hình đi theo người trong lúc chạy animation.
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(offset.x, offset.y, 0f);
            go.transform.localScale = new Vector3(scale, scale, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = frames[0];

            int layerId = SortingLayer.NameToID(sortingLayer);
            if (!string.IsNullOrEmpty(sortingLayer) && SortingLayer.IsValid(layerId))
            {
                sr.sortingLayerID = layerId;
                sr.sortingOrder = sortingOrder;
            }

            go.AddComponent<OneShotSpriteAnim>().Init(frames, life);
        }
    }

    /// <summary>
    /// Chạy một chuỗi frame ĐÚNG MỘT LẦN rồi tự huỷ. Không fade — biến mất ngay khi hết hiệu ứng
    /// (đúng yêu cầu user).
    ///
    /// Dùng SpriteRenderer + đổi frame bằng tay thay vì Animator: chỉ một chuỗi thẳng, không nhánh, nên
    /// Animator sẽ kéo theo controller + state machine cho việc mà 10 dòng này làm xong.
    /// </summary>
    public class OneShotSpriteAnim : MonoBehaviour
    {
        private SpriteRenderer _sr;
        private Sprite[] _frames;
        private float _life;
        private float _t;

        public void Init(Sprite[] frames, float life)
        {
            _sr = GetComponent<SpriteRenderer>();
            _frames = frames;
            _life = Mathf.Max(0.05f, life);
        }

        private void Update()
        {
            if (_sr == null || _frames == null || _frames.Length == 0) { Destroy(gameObject); return; }

            _t += Time.deltaTime;
            float k = Mathf.Clamp01(_t / _life);

            // Kẹp ở frame cuối để không tràn mảng khi k == 1.
            int i = Mathf.Min(_frames.Length - 1, (int)(k * _frames.Length));
            _sr.sprite = _frames[i];

            if (_t >= _life) Destroy(gameObject);
        }
    }
}
