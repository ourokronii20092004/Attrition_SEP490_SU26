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

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Tạo runner nếu chưa có (gắn lên chính object bền này) + cấu hình physics 2D.</summary>
        private void EnsureRunner()
        {
            // Runner cũ có thể vẫn còn trên GO ở trạng thái shutdown (Fusion không tự hủy component).
            // Dọn hết runner rác trước khi tạo mới để tránh AddComponent tạo ra 2 runner trùng.
            if (_runner == null)
            {
                var stale = GetComponents<NetworkRunner>();
                foreach (var r in stale) DestroyImmediate(r);
            }
            else return; // _runner vẫn còn sống → dùng lại.

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

#if UNITY_EDITOR
            // Tạm thời để test ParrelSync 2 acc không bị văng: 
            // Nếu là clone project thì nối thêm một chuỗi random vào UserId để Photon coi là 2 người khác nhau.
            if (Application.dataPath.Contains("clone", StringComparison.OrdinalIgnoreCase))
            {
                userId += "_clone_" + System.Guid.NewGuid().ToString().Substring(0, 4);
            }
#endif

            // Lobby là scene RIÊNG (Lobby.unity) — KHÔNG phải scene Menu. Host truyền args.Scene =
            // Lobby để runner SỞ HỮU scene đó; Fusion 2 chỉ spawn NetworkObject vào scene runner sở
            // hữu (không có scene → Spawn trả null). Vì Lobby tách khỏi Menu nên load nó không reload
            // Menu → host không bị văng. Client để TRỐNG Scene, tự follow scene của host qua Fusion.
            // Host bấm Start → BeginGameplay → runner.LoadScene(gameplay).
            var args = new StartGameArgs
            {
                GameMode = mode,
                SessionName = sessionName,
                PlayerCount = 2,
                SceneManager = SceneManager_,
                AuthValues = new Fusion.Photon.Realtime.AuthenticationValues(userId)
            };
            if (mode == GameMode.Host)
            {
                int lobbyIdx = SceneUtility.GetBuildIndexByScenePath("Assets/_Project/Scenes/Lobby.unity");
                if (lobbyIdx >= 0) args.Scene = SceneRef.FromIndex(lobbyIdx);
                else Debug.LogError("[NetworkLauncher] Scene 'Lobby' chưa có trong Build Settings — host sẽ không spawn được LobbyPlayer.");
            }

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
            // Đổi room/session → xoá cache đồ-theo-session để lần vào sau fetch lại đúng session mới.
            Attrition.Persistence.GameLaunch.ClearSessionInventoryCache();
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
                // destroyGameObject:false — giữ GO sống; chỉ tắt mạng.
                // Sau Shutdown, component NetworkRunner vẫn nằm trên GO nhưng ở trạng thái "đã tắt".
                // Phải Destroy component cũ để EnsureRunner tạo mới sạch sẽ, tránh trùng lặp.
                _runner.Shutdown(destroyGameObject: false);
                Destroy(_runner);
                _runner = null;
            }

            // Scene manager cũ gắn liền runner cũ — reset để EnsureRunner property tạo lại.
            if (_sceneManager != null)
            {
                Destroy(_sceneManager);
                _sceneManager = null;
            }
        }

        // ─────────────────────────── CALLBACKS ───────────────────────────

        private bool _spawnAttempted;

        private void Update()
        {
            if (_runner == null || !_runner.IsRunning || !_runner.IsServer) return;

            if (_phase == Phase.Lobby)
            {
                if (!lobbyPlayerPrefab.IsValid && !_spawnAttempted)
                {
                    Debug.LogError("[NetworkLauncher] LỖI: lobbyPlayerPrefab.IsValid đang là FALSE! Ô Lobby Player Prefab ở Inspector chưa được gán đúng hoặc chưa được Rebuild Prefab Table!");
                    _spawnAttempted = true;
                    return;
                }

                foreach (var player in _runner.ActivePlayers)
                    TrySpawnLobbyPlayer(player);
            }
        }

        /// <summary>
        /// Host spawn 1 LobbyPlayer cho 1 peer (idempotent). Gọi từ OnPlayerJoined (đúng phase
        /// simulation của Fusion) và retry trong Update phòng khi callback chạy trước lúc runner sẵn sàng.
        /// </summary>
        private void TrySpawnLobbyPlayer(PlayerRef player)
        {
            if (_runner == null || !_runner.IsServer || _phase != Phase.Lobby) return;
            if (_runner.TryGetPlayerObject(player, out var existingObj) && existingObj != null) return;

            try
            {
                var newObj = _runner.Spawn(lobbyPlayerPrefab, Vector3.zero, Quaternion.identity, player);
                if (newObj != null)
                {
                    _runner.SetPlayerObject(player, newObj);
                }
                // null = sai simulation phase (gọi từ Update) → KHÔNG log lỗi: callback OnPlayerJoined
                // hoặc Update frame sau sẽ retry thành công. Chỉ là timing bình thường của Fusion 2.
            }
            catch (System.Exception ex)
            {
                if (!_spawnAttempted)
                {
                    Debug.LogError($"[NetworkLauncher] Exception khi spawn LobbyPlayer: {ex.Message}");
                    _spawnAttempted = true;
                }
            }
        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer) return;

            // Phòng chờ: peer (kể cả host) join → spawn LobbyPlayer NGAY trong callback (đúng
            // simulation phase của Fusion 2). Spawn trong Update có thể trả null do sai phase.
            if (_phase == Phase.Lobby)
            {
                TrySpawnLobbyPlayer(player);
                return;
            }

            // Gameplay đã chạy (reconnect / join muộn): spawn 1 nhân vật gameplay cho peer này.
            // Client reconnect KHÔNG về lobby (client join với Scene trống → tự follow scene gameplay
            // của host qua Fusion). Spawn xong, PlayerInventory.LoadOnlineInventory (chạy khi client gửi
            // lại identity) tự teleport nhân vật về CHECKPOINT đã lưu trong session (đọc
            // SessionRestPosByChar cache còn trên host) — không cần xử lý checkpoint ở đây, tránh vi phạm
            // ranh giới assembly (Networking không ref Gameplay).
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

        // ─── Mất kết nối / host tắt phòng → client KHÔNG đứng kẹt trong scene gameplay ───

        /// <summary>
        /// Host shutdown phòng (BeginGameplay xong host Quit, hoặc host crash) → client nhận shutdown.
        /// Đưa client về main menu thay vì kẹt lại trong scene gameplay với lỗi.
        /// Chỉ xử lý phía CLIENT (host tự điều hướng bằng nút Quit của mình).
        /// </summary>
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            // Runner đã shutdown KHÔNG được tái dùng (Fusion: "NetworkRunner should not be reused").
            // Clear _runner để EnsureRunner tạo runner MỚI lần vào phòng sau (component cũ trên GO sẽ
            // được EnsureRunner DestroyImmediate khi _runner == null). Không clear → vào lại phòng báo
            // "should not be reused". Chạy cho CẢ host lẫn client (cả 2 đều Shutdown rồi về menu).
            _runner = null;

            bool wasClient = runner != null && !runner.IsServer;
            CleanupAndReturnToMenuIfClient(wasClient, $"shutdown ({shutdownReason})");
        }

        /// <summary>Client mất kết nối tới host (host crash / mạng đứt) → về menu.</summary>
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            CleanupAndReturnToMenuIfClient(true, $"disconnected ({reason})");
        }

        private bool _returningToMenu;

        private void CleanupAndReturnToMenuIfClient(bool isClient, string why)
        {
            // Chỉ client mới auto-về-menu. Tránh gọi 2 lần (shutdown + disconnect cùng bắn).
            if (!isClient || _returningToMenu) return;
            // Mất host ở BẤT KỲ phiên coop nào — phòng chờ (Lobby) HOẶC trong game (Gameplay). Idle =
            // chưa vào phiên (đang ở menu) → để UI báo lỗi tại chỗ, không điều hướng.
            if (_phase != Phase.Gameplay && _phase != Phase.Lobby) return;

            _returningToMenu = true;
            Debug.LogWarning($"[NetworkLauncher] Client mất host ({why}) → về Main Menu.");

            Attrition.Persistence.CoopSession.Reset();
            Attrition.Persistence.GameLaunch.ClearSessionInventoryCache();
            Attrition.Persistence.GamePause.IsPaused = false;
            _phase = Phase.Idle;
            _gameplaySpawned = false;
            _starting = false;
            Attrition.Persistence.CoopSession.HostLeftMessage =
                "The host left the game. You've been returned to the menu.";

            // Runner đã/đang tắt → chỉ cần load lại scene menu trên luồng chính.
            UnityEngine.SceneManagement.SceneManager.LoadScene("Main_Menu_UI");
            _returningToMenu = false;
        }

        public void OnConnectedToServer(NetworkRunner runner) { }
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
