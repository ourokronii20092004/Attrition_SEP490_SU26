using UnityEngine;
using Fusion;
using Attrition.Persistence;
using Attrition.Data;

namespace Attrition.Networking
{
    /// <summary>
    /// Tự khởi động phiên chơi khi scene gameplay load — đọc GameLaunch.Mode.
    ///   Solo → NetworkSpawner.StartSinglePlayer() (cục bộ, GameMode.Single).
    ///   Coop → KHÔNG tự start ở đây (đã do MainMenu/lobby start trước khi load scene).
    ///
    /// Gắn component này lên cùng GameObject với NetworkSpawner trong scene gameplay.
    /// Nhờ vậy bấm Play thẳng scene Enemy_Axe_Demon cũng chơi được Solo ngay (mặc định Solo).
    /// </summary>
    [RequireComponent(typeof(NetworkSpawner))]
    public class GameBootstrap : MonoBehaviour
    {
        [Tooltip("Nếu đã có NetworkRunner đang chạy (từ coop) thì KHÔNG tự start lại.")]
        [SerializeField] private bool skipIfRunnerExists = true;

        [Tooltip("Registry mọi ItemSO. PHẢI gán — nếu null thì DroppedItem/PickupItem/UI inventory không tra được item.")]
        [SerializeField] private ItemDatabaseSO itemDatabase;

        private void Awake()
        {
            // Gán singleton sớm nhất để DroppedItem/PickupItem/UI dùng được.
            if (itemDatabase != null)
            {
                ItemDatabaseSO.Instance = itemDatabase;
                itemDatabase.Initialize();
            }
            else
            {
                Debug.LogError("[GameBootstrap] Chưa gán ItemDatabase! Item rơi/nhặt và inventory UI sẽ không hoạt động.");
            }
        }

        private void Start()
        {
            // Coop: NetworkLauncher (object bền từ Menu) đã giữ runner ĐANG CHẠY + sẽ tự spawn player/
            // quái ở OnSceneLoadDone. Không làm gì ở đây.
            // QUAN TRỌNG: phải check runner ĐANG CHẠY (IsRunning), KHÔNG chỉ tồn tại. Solo back menu gọi
            // runner.Shutdown(destroyGameObject:false) → component NetworkRunner vẫn nằm trên GO bền
            // (đã tắt, chưa Destroy). Nếu chỉ check != null thì lần vào solo THỨ 2, FindFirstObjectByType
            // thấy runner cũ đã tắt → return sớm → KHÔNG StartSinglePlayer → treo màn loading.
            var existingRunner = FindFirstObjectByType<NetworkRunner>();
            if (skipIfRunnerExists && existingRunner != null && existingRunner.IsRunning)
                return;

            if (GameLaunch.Mode == LaunchMode.Solo)
            {
                // Solo: cần NetworkLauncher để giữ runner. Bấm Play THẲNG scene gameplay (test) thì
                // scene Menu không chạy → chưa có launcher → tự tạo một cái tại đây.
                var launcher = NetworkLauncher.Instance;
                if (launcher == null)
                {
                    var go = new GameObject("NetworkLauncher");
                    launcher = go.AddComponent<NetworkLauncher>();
                }
                launcher.StartSinglePlayer();
            }
        }
    }
}
