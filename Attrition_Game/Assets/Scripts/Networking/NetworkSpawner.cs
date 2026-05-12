using Fusion;
using Fusion.Addons.Physics;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class EnemySpawnConfig
{
    public Transform spawnPoint;
    public int spawnCount = 1;
}

public class NetworkSpawner : MonoBehaviour, INetworkRunnerCallbacks
{
    public NetworkPrefabRef playerPrefab;
    public Transform[] spawnPoints;

    [Header("Enemies")]
    public NetworkPrefabRef axeDemonPrefab;
    public EnemySpawnConfig[] enemySpawnConfigs;

    [Header("UI References")]
    public GameObject lobbyPanel;

    private NetworkRunner _runner;
    private bool _hasSpawnedEnemies = false;

    private void Awake()
    {
        // RunnerSimulatePhysics2D phải nằm trên cùng GameObject với NetworkRunner và tồn tại khi StartGame khởi tạo,
        // thì Fusion mới tự gán Runner (AddGlobal tay yêu cầu instance.Runner != null và sẽ Assert).
        var sim = GetComponent<RunnerSimulatePhysics2D>();
        if (sim == null)
            sim = gameObject.AddComponent<RunnerSimulatePhysics2D>();
        sim.ClientPhysicsSimulation = ClientPhysicsSimulation.SimulateForward;

        // Frame pacing: giảm judder/stutter và xé hình phía client (Fusion tick vẫn lấy từ NetworkProjectConfig).
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
        Debug.Log("=== BẮT ĐẦU ĐẺ QUÁI ===");

        if (enemySpawnConfigs == null || enemySpawnConfigs.Length == 0)
        {
            Debug.LogWarning("Mảng cấu hình rỗng!");
            return;
        }

        foreach (var config in enemySpawnConfigs)
        {
            if (config.spawnPoint == null)
            {
                Debug.LogWarning("Có một điểm spawn bị null, bỏ qua!");
                continue;
            }

            Debug.Log($"Đang đẻ {config.spawnCount} quái tại {config.spawnPoint.name}");

            for (int i = 0; i < config.spawnCount; i++)
            {
                float randomXOffset = UnityEngine.Random.Range(-0.5f, 0.5f);

                // ÉP CỨNG TRỤC Z VỀ 0 ĐỂ CAMERA 2D NHÌN THẤY ĐƯỢC
                Vector3 spawnPos = config.spawnPoint.position + new Vector3(randomXOffset, 0, 0);
                spawnPos.z = 0;

                NetworkObject enemyObj = _runner.Spawn(axeDemonPrefab, spawnPos, Quaternion.identity, null);

                if (enemyObj != null)
                    Debug.Log("-> Đẻ thành công 1 Axe_Demon!");
                else
                    Debug.LogError("-> Fusion từ chối đẻ con quái này (Kiểm tra lại Prefab)!");
            }
        }
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (runner.IsServer)
        {
            // --- 1. ĐẺ PLAYER ---
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

            NetworkObject playerObj = runner.Spawn(playerPrefab, spawnPos, Quaternion.identity, player);
            runner.SetPlayerObject(player, playerObj);

            // --- 2. ĐẺ QUÁI VẬT (Chỉ đẻ 1 lần duy nhất khi Host vừa vào game) ---
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