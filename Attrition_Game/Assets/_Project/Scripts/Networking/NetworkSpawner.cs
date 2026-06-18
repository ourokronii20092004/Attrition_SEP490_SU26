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
    [Tooltip("Random trong pool biom; null thì dùng fallbackEnemyPrefab.")]
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
        var provider = Attrition.Persistence.EnemyStatProvider.Ensure();
        yield return provider.PrefetchAll();
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

        NetworkObject prefabNo = config.biome != null ? config.biome.PickRandomPrefab() : null;
        if (prefabNo != null)
            return runner.Spawn(prefabNo, spawnPos, Quaternion.identity, null);

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
