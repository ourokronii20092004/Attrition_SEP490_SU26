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
            if (skipIfRunnerExists && FindFirstObjectByType<NetworkRunner>() != null)
                return; // coop đã start runner từ trước → bỏ qua

            if (GameLaunch.Mode == LaunchMode.Solo)
            {
                var spawner = GetComponent<NetworkSpawner>();
                spawner.StartSinglePlayer();
            }
            // Coop: runner do lobby tạo trước khi đổi scene, không làm gì ở đây.
        }
    }
}
