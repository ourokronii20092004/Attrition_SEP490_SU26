using Fusion;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

[System.Serializable]
public class EnemySpawnConfig
{
    [Tooltip("Vị trí spawn (Transform trong scene).")]
    public Transform spawnPoint;
    [Min(1)] public int spawnCount = 1;

    [Header("Chọn 1 trong 2 (ưu tiên Override Prefab)")]
    [Tooltip("Gán quái CỤ THỂ vào đây → spawn đúng con này, bỏ qua biome.")]
    public Fusion.NetworkPrefabRef overridePrefab;
    [Tooltip("Random trong pool biom. Chỉ dùng khi overridePrefab KHÔNG được gán.")]
    public EnemyBiomeDefinition biome;
}

/// <summary>
/// Spawn nhân vật gameplay + quái cho RIÊNG scene gameplay (spawnPoints/enemySpawnConfigs là Transform
/// của scene này). KHÔNG còn sở hữu NetworkRunner — runner do NetworkLauncher (object bền) quản lý.
/// NetworkLauncher gọi ServerSpawnPlayer / ServerSpawnEnemies khi scene gameplay load xong (host-side).
/// Checkpoint gọi RespawnConfiguredEnemies / DespawnObject khi Rest.
/// </summary>
public class NetworkSpawner : MonoBehaviour
{
    public NetworkPrefabRef playerPrefab;
    public NetworkPrefabRef player1Prefab;
    public Transform[] spawnPoints;

    [Header("Enemies")]
    [Tooltip("Khi spawn config không gán biome hoặc pool biom rỗng.")]
    [FormerlySerializedAs("axeDemonPrefab")]
    public NetworkPrefabRef fallbackEnemyPrefab;
    public EnemySpawnConfig[] enemySpawnConfigs;

    private NetworkRunner _runner;
    private bool _hasSpawnedEnemies;

    /// <summary>Runner đang dùng (lấy từ NetworkLauncher). Null nếu chưa khởi tạo.</summary>
    private NetworkRunner Runner
        => _runner != null ? _runner
         : (_runner = Attrition.Networking.NetworkLauncher.Instance != null
              ? Attrition.Networking.NetworkLauncher.Instance.Runner : null);

    /// <summary>Host spawn 1 nhân vật gameplay cho 1 peer. Gọi bởi NetworkLauncher.</summary>
    public void ServerSpawnPlayer(NetworkRunner runner, PlayerRef player)
    {
        if (runner == null || !runner.IsServer) return;
        _runner = runner;

        // Idempotent: peer đã có nhân vật rồi thì bỏ qua. Chặn spawn trùng khi cả OnSceneLoadDone
        // lẫn OnPlayerJoined cùng gọi (Solo: local player join fire SAU khi scene load xong → 2 nhân vật).
        if (runner.TryGetPlayerObject(player, out var existing) && existing != null) return;

        bool isHostPlayer = player == runner.LocalPlayer;
        NetworkPrefabRef prefabToSpawn = isHostPlayer ? playerPrefab : player1Prefab;

        Vector3 spawnPos;
        if (spawnPoints != null && spawnPoints.Length > 0)
            spawnPos = spawnPoints[player.RawEncoded % spawnPoints.Length].position;
        else
            spawnPos = new Vector3(UnityEngine.Random.Range(-2f, 2f), 48f, 0);

        // [TỐI ƯU LOAD] Solo: spawn THẲNG tại checkpoint đã lưu — NHƯNG chỉ khi checkpoint đó
        // thuộc ĐÚNG scene hiện tại (logic Metroidvania kiểu Hollow Knight/Afterimage: bench gắn
        // với room của nó; sang map mới thì vào ở điểm xuất phát của map đó, không nhảy về bench cũ).
        // Coop (IsOnline) KHÔNG đọc save local — luôn dùng spawnPoints của scene để 2 máy nhất quán.
        // (Fast-travel CROSS-MAP do PendingTravelSpawner phía Gameplay xử lý SAU khi load scene.)
        if (!Attrition.Persistence.GameLaunch.IsOnline)
        {
            var data = Attrition.Persistence.SaveManager.LoadSlot(Attrition.Persistence.GameLaunch.SelectedSlot);
            string activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (data != null
                && !string.IsNullOrEmpty(data.checkpointId)
                && data.checkpointScene == activeScene)
            {
                spawnPos = new Vector3(data.checkpointX, data.checkpointY, data.checkpointZ);
            }
        }

        NetworkObject playerObj = runner.Spawn(prefabToSpawn, spawnPos, Quaternion.identity, player);
        runner.SetPlayerObject(player, playerObj);
    }

    /// <summary>Host spawn toàn bộ quái theo config (một lần khi vào scene). Gọi bởi NetworkLauncher.</summary>
    public void ServerSpawnEnemies(NetworkRunner runner)
    {
        if (runner == null || !runner.IsServer) return;
        _runner = runner;
        if (_hasSpawnedEnemies) return;
        _hasSpawnedEnemies = true;
        // Host prefetch override chỉ số quái (web sửa) TRƯỚC khi spawn, rồi mới spawn để EnemyStats
        // đọc được cache. Lỗi mạng → PrefetchAll tự fallback dùng default SO, vẫn spawn bình thường.
        StartCoroutine(PrefetchThenSpawn());
    }

    private System.Collections.IEnumerator PrefetchThenSpawn()
    {
        // Solo/offline: KHÔNG fetch override từ web → cache rỗng → EnemyStats dùng default SO.
        // Chỉ online (coop, đã login) mới đắp override admin sửa trên web lên SO.
        if (Attrition.Persistence.GameLaunch.IsOnline)
        {
            var provider = Attrition.Persistence.EnemyStatProvider.Ensure();
            yield return provider.PrefetchAll();
        }
        else if (Attrition.Persistence.EnemyStatProvider.Instance != null)
        {
            // Singleton sống sót từ phiên coop trước → xóa override để solo dùng default SO.
            Attrition.Persistence.EnemyStatProvider.Instance.ClearOverrides();
        }
        SpawnAllEnemies();
    }

    private void SpawnAllEnemies()
    {
        if (enemySpawnConfigs == null || enemySpawnConfigs.Length == 0)
        {
            Debug.LogWarning("[NetworkSpawner] enemySpawnConfigs rỗng.");
            return;
        }

        foreach (var config in enemySpawnConfigs)
        {
            if (config.spawnPoint == null)
            {
                Debug.LogWarning("[NetworkSpawner] spawnPoint null — bỏ qua.");
                continue;
            }

            for (int i = 0; i < config.spawnCount; i++)
            {
                float randomXOffset = UnityEngine.Random.Range(-0.5f, 0.5f);
                Vector3 spawnPos = config.spawnPoint.position + new Vector3(randomXOffset, 0f, 0f);
                spawnPos.z = 0f;

                NetworkObject spawned = TrySpawnOneEnemy(config, spawnPos);
                if (spawned != null)
                    Debug.Log($"[NetworkSpawner] Spawn quái OK: {spawned.name} tại {spawnPos}");
                else
                    Debug.LogError("[NetworkSpawner] Spawn thất bại (prefab / Fusion PrefabTable).");
            }
        }
    }

    private NetworkObject TrySpawnOneEnemy(EnemySpawnConfig config, Vector3 spawnPos)
    {
        var runner = Runner;
        if (runner == null) return null;

        // 1. Ưu tiên: quái cụ thể (overridePrefab) → spawn đúng con này, bỏ qua biome.
        if (config.overridePrefab.IsValid)
            return runner.Spawn(config.overridePrefab, spawnPos, Quaternion.identity, null);

        // 2. Random từ biome pool.
        NetworkObject prefabNo = config.biome != null ? config.biome.PickRandomPrefab() : null;
        if (prefabNo != null)
            return runner.Spawn(prefabNo, spawnPos, Quaternion.identity, null);

        // 3. Fallback cuối cùng.
        if (fallbackEnemyPrefab.IsValid)
            return runner.Spawn(fallbackEnemyPrefab, spawnPos, Quaternion.identity, null);

        return null;
    }

    /// <summary>
    /// Spawn lại toàn bộ quái theo config (public để Checkpoint gọi khi Rest).
    /// Lưu ý: chỉ spawn những gì khai báo trong enemySpawnConfigs — BOSS nên đặt riêng
    /// trong scene (không cho vào config) để Rest không hồi sinh boss.
    /// </summary>
    public void RespawnConfiguredEnemies()
    {
        var runner = Runner;
        if (runner == null || !runner.IsServer) return;
        SpawnAllEnemies();
    }

    /// <summary>Despawn 1 NetworkObject (Checkpoint quyết định con nào, tránh phụ thuộc type Gameplay → không tạo vòng lặp assembly).</summary>
    public void DespawnObject(NetworkObject obj)
    {
        var runner = Runner;
        if (runner == null || !runner.IsServer) return;
        if (obj != null && obj.IsValid) runner.Despawn(obj);
    }
}
