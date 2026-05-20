using Fusion;
using Fusion.Addons.Physics;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
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

public class NetworkSpawner : MonoBehaviour, INetworkRunnerCallbacks
{
    public NetworkPrefabRef playerPrefab;
    public NetworkPrefabRef player1Prefab;
    public Transform[] spawnPoints;

    [Header("Enemies")]
    [Tooltip("Khi spawn config không gán biome hoặc pool biom rỗng.")]
    [FormerlySerializedAs("axeDemonPrefab")]
    public NetworkPrefabRef fallbackEnemyPrefab;
    public EnemySpawnConfig[] enemySpawnConfigs;

    [Header("UI References")]
    public GameObject lobbyPanel;

    private NetworkRunner _runner;
    private bool _hasSpawnedEnemies;

    private void Awake()
    {
        var sim = GetComponent<RunnerSimulatePhysics2D>();
        if (sim == null)
            sim = gameObject.AddComponent<RunnerSimulatePhysics2D>();
        sim.ClientPhysicsSimulation = ClientPhysicsSimulation.SimulateForward;

        QualitySettings.vSyncCount = 1;
#if UNITY_ANDROID || UNITY_IOS
        Application.targetFrameRate = 60;
#else
        var rr = Screen.currentResolution.refreshRateRatio;
        var hz = rr.denominator != 0 ? rr.numerator / (double)rr.denominator : 60.0;
        Application.targetFrameRate = Mathf.Clamp(Mathf.RoundToInt((float)hz), 60, 360);
#endif
    }

    async void StartGame(GameMode mode)
    {
        _runner = gameObject.AddComponent<NetworkRunner>();
        _runner.ProvideInput = true;
        _runner.AddCallbacks(this);
        _runner.AddCallbacks(GetComponent<NetworkInputHandler>());

        await _runner.StartGame(new StartGameArgs()
        {
            GameMode = mode,
            SessionName = "TestRoom",
            Scene = SceneRef.FromIndex(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex),
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
        });
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
        NetworkObject prefabNo = config.biome != null ? config.biome.PickRandomPrefab() : null;

        if (prefabNo != null)
            return _runner.Spawn(prefabNo, spawnPos, Quaternion.identity, null);

        if (fallbackEnemyPrefab.IsValid)
            return _runner.Spawn(fallbackEnemyPrefab, spawnPos, Quaternion.identity, null);

        return null;
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (runner.IsServer)
        {
            bool isHostPlayer = player == runner.LocalPlayer;
            NetworkPrefabRef prefabToSpawn = isHostPlayer ? playerPrefab : player1Prefab;

            Vector3 spawnPos;
            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                spawnPos = spawnPoints[player.RawEncoded % spawnPoints.Length].position;
            }
            else
            {
                float randomX = UnityEngine.Random.Range(-2f, 2f);
                spawnPos = new Vector3(randomX, 48f, 0);
            }

            NetworkObject playerObj = runner.Spawn(prefabToSpawn, spawnPos, Quaternion.identity, player);
            runner.SetPlayerObject(player, playerObj);

            if (player == runner.LocalPlayer && !_hasSpawnedEnemies)
            {
                SpawnAllEnemies();
                _hasSpawnedEnemies = true;
            }
        }
    }

    public void OnClickHost()
    {
        if (lobbyPanel != null) lobbyPanel.SetActive(false);
        StartGame(GameMode.Host);
    }

    public void OnClickClient()
    {
        if (lobbyPanel != null) lobbyPanel.SetActive(false);
        StartGame(GameMode.Client);
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (runner.IsServer)
        {
            if (runner.TryGetPlayerObject(player, out NetworkObject playerObj))
            {
                runner.Despawn(playerObj);
                runner.SetPlayerObject(player, null);
                Debug.Log($"[SERVER] Người chơi {player.PlayerId} đã thoát.");
            }
        }
    }

    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] payload) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
}
