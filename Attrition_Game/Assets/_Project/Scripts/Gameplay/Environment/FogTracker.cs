using UnityEngine;
using UnityEngine.SceneManagement;

namespace Attrition.Gameplay.Environment
{
    /// <summary>
    /// Theo dõi vị trí player LOCAL trong scene gameplay → xua sương mù (fog) các ô đã đi qua.
    /// Ghi vào WorldMapState (sống xuyên scene + lưu vào save). Đặt 1 cái trong mỗi scene gameplay,
    /// HOẶC tự tồn tại và tìm MapData theo scene hiện tại.
    ///
    /// Mỗi khoảng thời gian ngắn: lấy ô (cx,cy) của player + 8 ô lân cận → đánh dấu visited.
    /// Local thuần (mỗi máy tự theo dõi player của mình).
    /// </summary>
    public class FogTracker : MonoBehaviour
    {
        [Tooltip("MapData của scene này. Bỏ trống = tự tìm theo tên scene trong MapRegistry.")]
        [SerializeField] private MapDataSO mapData;
        [Tooltip("Bán kính xua sương quanh player (số ô lân cận mỗi chiều).")]
        [SerializeField] private int revealRadius = 1;
        [Tooltip("Tần suất cập nhật (giây) — không cần mỗi frame.")]
        [SerializeField] private float updateInterval = 0.25f;

        private Transform _player;
        private float _timer;
        private string _scene;

        private void Start()
        {
            _scene = SceneManager.GetActiveScene().name;

            // Nạp fog + checkpoint đã khám phá từ save (solo). Đảm bảo fog đúng ngay từ đầu kể cả khi
            // scene không có checkpoint nào (Checkpoint.RestoreActivatedFromSave có thể không chạy).
            // New game = save trống → WorldMapState rỗng → mọi room phủ sương. Coop: hệ online lo riêng.
            if (!Attrition.Persistence.GameLaunch.IsOnline)
            {
                var data = Attrition.Persistence.SaveManager.LoadSlot(Attrition.Persistence.GameLaunch.SelectedSlot);
                WorldMapState.LoadFrom(data); // data null → clear sạch (new game)
            }

            if (mapData == null)
            {
                var reg = MapRegistrySO.Load();
                if (reg != null) mapData = reg.GetByScene(_scene);
            }
            if (mapData == null)
                Debug.LogWarning($"[FogTracker] Không có MapData cho scene '{_scene}' — fog sẽ không ghi nhận. Hãy bake map + thêm vào MapRegistry.");
        }

        private void Update()
        {
            if (mapData == null) return;

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = updateInterval;

            if (_player == null) TryFindLocalPlayer();
            if (_player == null) return;

            var cell = mapData.WorldToCell(_player.position);
            var grid = mapData.FogGridSize();
            for (int dx = -revealRadius; dx <= revealRadius; dx++)
                for (int dy = -revealRadius; dy <= revealRadius; dy++)
                {
                    int cx = cell.x + dx, cy = cell.y + dy;
                    if (cx < 0 || cy < 0 || cx >= grid.x || cy >= grid.y) continue;
                    WorldMapState.MarkFogVisited(_scene, cx, cy);
                }
        }

        private void TryFindLocalPlayer()
        {
            foreach (var pc in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
                if (pc != null && pc.HasInputAuthority) { _player = pc.transform; break; }
        }
    }
}
