using Fusion;
using Fusion.Addons.Physics;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Attrition.Networking
{
    /// <summary>
    /// Chủ sở hữu DUY NHẤT của NetworkRunner — object BỀN (DontDestroyOnLoad), đặt sẵn trong scene Menu.
    /// Sống xuyên suốt: Menu (lobby) → scene gameplay, để runner không chết khi đổi scene.
    ///
    /// Vòng đời coop:
    ///   1. Host/Client bấm vào lobby → StartCoopLobby() → runner kết nối ngay ở scene Menu.
    ///   2. Mỗi peer join → spawn 1 LobbyPlayer (object nhẹ: tên/level/ready) để 2 bên thấy nhau.
    ///   3. Cả phòng Ready → host BeginGameplay() → runner.LoadScene(gameplay).
    ///   4. Scene gameplay load xong → host despawn LobbyPlayer + spawn nhân vật gameplay thật + quái.
    ///
    /// Solo: GameBootstrap gọi StartSinglePlayer() trong scene gameplay (Phase=Gameplay luôn).
    /// Spawn quái + spawnPoints vẫn ở NetworkSpawner (thuộc riêng scene gameplay).
    /// </summary>
    [RequireComponent(typeof(NetworkInputHandler))]
    public class NetworkLauncher : MonoBehaviour, INetworkRunnerCallbacks
    {
        public static NetworkLauncher Instance { get; private set; }

        [Tooltip("Prefab LobbyPlayer (NetworkObject nhẹ: tên/level/ready). PHẢI đăng ký trong Fusion NetworkProjectConfig.")]
        public NetworkPrefabRef lobbyPlayerPrefab;

        private NetworkRunner _runner;
        public NetworkRunner Runner => _runner;

        private enum Phase { Idle, Lobby, Gameplay }
        private Phase _phase = Phase.Idle;
        private bool _gameplaySpawned;
        private bool _starting; // chặn StartCoopLobby re-entry (tránh mở connection trùng UserId → host tự đá mình)
        private NetworkSceneManagerDefault _sceneManager; // reuse 1 instance, tránh AddComponent rác mỗi lần start

        /// <summary>True khi đang ở phòng chờ coop (lobby networked, chưa vào game).</summary>
        public bool InLobby => _phase == Phase.Lobby;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            QualitySettings.vSyncCount = 1;
#if UNITY_ANDROID || UNITY_IOS
            Application.targetFrameRate = 60;
#else
            var rr = Screen.currentResolution.refreshRateRatio;
            var hz = rr.denominator != 0 ? rr.numerator / (double)rr.denominator : 60.0;
            Application.targetFrameRate = Mathf.Clamp(Mathf.RoundToInt((float)hz), 60, 360);
#endif
        }

        /// <summary>Tạo runner nếu chưa có (gắn lên chính object bền này) + cấu hình physics 2D.</summary>
        private void EnsureRunner()
        {
            if (_runner != null) return;
            _runner = gameObject.AddComponent<NetworkRunner>();
            _runner.ProvideInput = true;

            var sim = GetComponent<RunnerSimulatePhysics2D>();
            if (sim == null) sim = gameObject.AddComponent<RunnerSimulatePhysics2D>();
            sim.ClientPhysicsSimulation = ClientPhysicsSimulation.SimulateForward;

            _runner.AddCallbacks(this);
            _runner.AddCallbacks(GetComponent<NetworkInputHandler>());
        }

        /// <summary>Scene manager dùng chung (tạo 1 lần). Tránh AddComponent mới mỗi lần StartGame.</summary>
        private NetworkSceneManagerDefault SceneManager_
        {
            get
            {
                if (_sceneManager == null) _sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();
                return _sceneManager;
            }
        }

        // ─────────────────────────── COOP LOBBY ───────────────────────────

        /// <summary>
        /// Host (GameMode.Host) hoặc Client (GameMode.Client) kết nối phòng chờ NGAY ở scene Menu.
        /// sessionName = room code. onResult(ok, err): join thất bại (sai mã) → UI báo lỗi, thử lại.
        /// </summary>
        public async void StartCoopLobby(GameMode mode, string sessionName, Action<bool, string> onResult = null)
        {
            // Chặn re-entry: bấm continue 2 lần / callback gọi lại sẽ mở connection Fusion MỚI cùng
            // UserId → Photon coi là "host reconnect" và ĐÁ host trước ra → host bị văng về host-join.
            // Mỗi lần gọi cũng AddComponent<NetworkSceneManagerDefault> rác. Đang start/đã ở lobby → bỏ qua.
            if (_starting || _phase == Phase.Lobby)
            {
                onResult?.Invoke(true, null);
                return;
            }
            _starting = true;

            EnsureRunner();
            _phase = Phase.Lobby;
            _gameplaySpawned = false;

            // Photon UserId DUY NHẤT theo tài khoản. 2 clone ParrelSync chạy chung máy → chung
            // PlayerPrefs → Fusion sinh cùng 1 UserId ngẫu nhiên → Photon coi peer thứ 2 là "host
            // reconnect" và ĐÁ host ra. Gán theo OwnerId (login) để mỗi tài khoản 1 UserId riêng.
            string userId = Attrition.Persistence.GameLaunch.OwnerId;
            if (string.IsNullOrEmpty(userId)) userId = System.Guid.NewGuid().ToString();

            // Phòng chờ KHÔNG truyền args.Scene. Nếu truyền, Fusion (NetworkSceneManagerDefault) SỞ HỮU
            // scene Menu → vài giây sau reload nó (host bị văng về main-menu) và khi LeaveSession shutdown
            // runner thì Fusion UNLOAD scene Menu → mất camera ("Display 1 no camera rendering" lúc Back).
            // Luồng client giờ đã validate qua API + connect 1 lần (không còn early-connect race), nên
            // StartGame vẫn hoàn tất bình thường mà không cần Scene. Host đổi scene khi bấm Start
            // (BeginGameplay → runner.LoadScene); client tự follow qua Fusion.
            var args = new StartGameArgs
            {
                GameMode = mode,
                SessionName = sessionName,
                PlayerCount = 2,
                SceneManager = SceneManager_,
                AuthValues = new Fusion.Photon.Realtime.AuthenticationValues(userId)
            };

            var result = await _runner.StartGame(args);

            if (!result.Ok)
            {
                Debug.LogWarning($"[NetworkLauncher] Lobby join thất bại: {result.ShutdownReason}");
                ShutdownInternal();
                _phase = Phase.Idle;
            }
            _starting = false;
            onResult?.Invoke(result.Ok, result.Ok ? null : result.ShutdownReason.ToString());
        }

        /// <summary>Host bấm Start: chuyển cả phòng sang scene gameplay (client tự follow qua Fusion).</summary>
        public void BeginGameplay(string sceneName)
        {
            if (_runner == null || !_runner.IsServer) return;
            _phase = Phase.Gameplay;
            _gameplaySpawned = false;

            int idx = SceneUtility.GetBuildIndexByScenePath($"Assets/_Project/Scenes/{sceneName}.unity");
            if (idx >= 0) _runner.LoadScene(SceneRef.FromIndex(idx));
            else Debug.LogError($"[NetworkLauncher] Scene '{sceneName}' chưa có trong Build Settings.");
        }

        /// <summary>Rời phòng chờ (nút Back): tắt runner để có thể tạo/join lại từ đầu.</summary>
        public void LeaveSession()
        {
            ShutdownInternal();
            _phase = Phase.Idle;
            _starting = false;
        }

        // ─────────────────────────── SOLO ───────────────────────────

        /// <summary>
        /// Solo cục bộ (GameMode.Single) — gọi bởi GameBootstrap trong scene gameplay.
        /// Không relay/login. Phase=Gameplay → spawn player + quái khi scene sẵn sàng.
        /// </summary>
        public async void StartSinglePlayer()
        {
            EnsureRunner();
            _phase = Phase.Gameplay;
            _gameplaySpawned = false;

            // Scene gameplay thường ĐÃ nằm trong Build Settings (index hợp lệ), nhưng nếu bấm Play
            // thẳng scene chưa add vào Build thì buildIndex = -1 → SceneRef.FromIndex(-1) ném exception.
            // Chỉ truyền Scene khi index hợp lệ; ngược lại giữ scene hiện tại (vẫn spawn được player/quái).
            var args = new StartGameArgs
            {
                GameMode = GameMode.Single,
                SessionName = "SoloLocal",
                SceneManager = SceneManager_
            };
            int sceneIdx = SceneManager.GetActiveScene().buildIndex;
            if (sceneIdx >= 0) args.Scene = SceneRef.FromIndex(sceneIdx);

            await _runner.StartGame(args);
        }

        private void ShutdownInternal()
        {
            if (_runner != null)
            {
                // destroyGameObject:false — runner là component TRÊN chính NetworkLauncher (object bền).
                // Shutdown() mặc định huỷ luôn GameObject chứa runner → NetworkLauncher biến mất →
                // lần host/join sau "NetworkLauncher not found". Giữ GO sống để tái dùng.
                _runner.Shutdown(destroyGameObject: false);
                _runner = null;
            }
        }

        // ─────────────────────────── CALLBACKS ───────────────────────────

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer) return;

            if (_phase == Phase.Lobby)
            {
                // Phòng chờ: spawn object nhẹ LobbyPlayer cho peer vừa vào.
                if (lobbyPlayerPrefab.IsValid)
                {
                    var obj = runner.Spawn(lobbyPlayerPrefab, Vector3.zero, Quaternion.identity, player);
                    runner.SetPlayerObject(player, obj);
                }
                return;
            }

            // Gameplay đã chạy (reconnect / join muộn): spawn 1 nhân vật gameplay cho peer này.
            if (_phase == Phase.Gameplay && _gameplaySpawned)
            {
                var spawner = FindFirstObjectByType<NetworkSpawner>();
                if (spawner != null) spawner.ServerSpawnPlayer(runner, player);

                if (runner.GameMode != GameMode.Single && runner.ActivePlayers.Count() >= 2)
                    Attrition.Persistence.CoopSession.EndWaiting();
            }
        }

        public void OnSceneLoadDone(NetworkRunner runner)
        {
            if (!runner.IsServer) return;

            var spawner = FindFirstObjectByType<NetworkSpawner>();
            if (spawner == null) return; // scene Menu (lobby) không có NetworkSpawner → bỏ qua.

            // Vào scene gameplay: dọn LobbyPlayer, spawn nhân vật thật cho mọi peer + quái (một lần).
            _phase = Phase.Gameplay;
            if (_gameplaySpawned) return;
            _gameplaySpawned = true;

            foreach (var lp in FindObjectsByType<LobbyPlayer>(FindObjectsSortMode.None))
                if (lp != null && lp.Object != null && lp.Object.IsValid) runner.Despawn(lp.Object);

            foreach (var player in runner.ActivePlayers)
                spawner.ServerSpawnPlayer(runner, player);

            spawner.ServerSpawnEnemies(runner);
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer) return;

            if (runner.TryGetPlayerObject(player, out NetworkObject obj))
            {
                runner.Despawn(obj);
                runner.SetPlayerObject(player, null);
            }

            // Coop trong game: client rời → CHỜ họ quay lại (không drop về solo).
            if (_phase == Phase.Gameplay && runner.GameMode != GameMode.Single && runner.ActivePlayers.Count() <= 1)
                Attrition.Persistence.CoopSession.BeginWaiting("WAITING FOR PLAYER TO RECONNECT...");
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
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    }
}
