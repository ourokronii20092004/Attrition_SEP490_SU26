using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Attrition.Persistence;
using Fusion;

namespace Attrition.UI
{
    /// <summary>
    /// Controller cho scene Lobby RIÊNG (Lobby.unity). Host StartGame với args.Scene = Lobby nên
    /// Fusion sở hữu scene này → spawn LobbyPlayer chạy đúng (khác scene Menu không spawn được).
    /// Vai trò host/client derive từ runner.IsServer. Port phần lobby từ MainMenuUIController:
    /// roster polling (host↔client thấy nhau), nút Ready (client), Start (host), Back (về Menu).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class LobbyUIController : MonoBehaviour
    {
        private const string MenuSceneName = "Main_Menu_UI";

        private UIDocument _uiDocument;
        private VisualElement _root;

        private bool _isHost;
        private bool _isCoopReady;
        private bool _clientPresent;
        private bool _clientReady;

        private Coroutine _rosterCoroutine;

        // Avatar đã tải (cache theo URL) → không tải lại mỗi 0.5s poll.
        private readonly Dictionary<string, Texture2D> _avatarCache = new Dictionary<string, Texture2D>();
        private readonly HashSet<string> _avatarLoading = new HashSet<string>();

        private void OnEnable()
        {
            _uiDocument = GetComponent<UIDocument>();
            _root = _uiDocument.rootVisualElement;
            if (_root == null) return;

            var runner = Attrition.Networking.NetworkLauncher.Instance != null
                ? Attrition.Networking.NetworkLauncher.Instance.Runner : null;
            _isHost = runner != null && runner.IsServer;

            UpdateRoomLabels();
            SetupButtons();
            SetupRoleView();

            _rosterCoroutine = StartCoroutine(PollLobbyRoster());
        }

        private void OnDisable()
        {
            if (_rosterCoroutine != null) { StopCoroutine(_rosterCoroutine); _rosterCoroutine = null; }
        }

        private void UpdateRoomLabels()
        {
            string room = string.IsNullOrEmpty(GameLaunch.RoomName) ? "ROOM" : GameLaunch.RoomName;
            SetText("coop-room-name", room);
            var roomId = _root.Q<Label>("coop-room-id");
            if (roomId != null) roomId.text = $"● ROOM ID: {GameLaunch.RoomCode}";
        }

        private void SetupRoleView()
        {
            var startBtn = _root.Q<Button>("btn-coop-start");
            var readyBtn = _root.Q<Button>("btn-coop-ready");

            if (_isHost)
            {
                if (startBtn != null) { startBtn.style.display = DisplayStyle.Flex; startBtn.AddToClassList("coop-start-disabled"); }
                if (readyBtn != null) readyBtn.style.display = DisplayStyle.None; // host luôn ready
                _isCoopReady = true;
                _clientPresent = false;
                _clientReady = false;

                FillLocalPlayerCard("coop-host-name", "coop-host-level");
                SetText("coop-client-name", "Waiting for player...");
                SetText("coop-client-level", "");
                var clientCard = _root.Q<VisualElement>("coop-card-client");
                if (clientCard != null) clientCard.style.opacity = 0.5f;
            }
            else
            {
                if (startBtn != null) startBtn.style.display = DisplayStyle.None; // client không start
                if (readyBtn != null) readyBtn.style.display = DisplayStyle.Flex;
                _isCoopReady = false;
                if (readyBtn != null) UpdateReadyState(readyBtn);

                FillLocalPlayerCard("coop-client-name", "coop-client-level");
            }
        }
        private void SetupButtons()
        {
            var readyBtn = _root.Q<Button>("btn-coop-ready");
            if (readyBtn != null)
            {
                readyBtn.RegisterCallback<ClickEvent>(evt =>
                {
                    _isCoopReady = !_isCoopReady;
                    UpdateReadyState(readyBtn);
                    var local = FindLocalLobbyStats();
                    if (local != null) local.RpcSetReady(_isCoopReady);
                });
            }

            var backBtn = _root.Q<Button>("btn-coop-back");
            if (backBtn != null)
            {
                backBtn.RegisterCallback<ClickEvent>(evt =>
                {
                    var launcher = Attrition.Networking.NetworkLauncher.Instance;
                    if (launcher != null) launcher.LeaveSession();
                    SceneManager.LoadScene(MenuSceneName);
                });
            }

            var startBtn = _root.Q<Button>("btn-coop-start");
            if (startBtn != null)
            {
                startBtn.RegisterCallback<ClickEvent>(evt =>
                {
                    if (!_clientPresent || !_clientReady) return; // chờ client sẵn sàng
                    GameLaunch.Mode = LaunchMode.Coop;
                    var launcher = Attrition.Networking.NetworkLauncher.Instance;
                    if (launcher != null) launcher.BeginGameplay(GameLaunch.GameplayScene);
                });
            }
        }

        private void UpdateReadyState(Button readyBtn)
        {
            var startBtn = _root.Q<Button>("btn-coop-start");
            if (_isCoopReady)
            {
                readyBtn.text = "● READY";
                readyBtn.RemoveFromClassList("not-ready");
                readyBtn.AddToClassList("ready");
                if (startBtn != null) startBtn.RemoveFromClassList("coop-start-disabled");
            }
            else
            {
                readyBtn.text = "● NOT READY";
                readyBtn.RemoveFromClassList("ready");
                readyBtn.AddToClassList("not-ready");
                if (startBtn != null) startBtn.AddToClassList("coop-start-disabled");
            }
        }

        private void FillLocalPlayerCard(string nameElement, string levelElement)
        {
            // Tên/level từ GameLaunch (character server đã chọn), KHÔNG đọc save slot LOCAL: ParrelSync
            // 2 clone chung thư mục save → cùng slot → cùng tên. Coop character nằm trên server.
            string charName = string.IsNullOrEmpty(GameLaunch.CharacterName) ? "Wanderer" : GameLaunch.CharacterName;
            int level = Mathf.Max(1, GameLaunch.CharacterLevel);
            SetText(nameElement, charName);
            SetText(levelElement, $"LEVEL {level}");

            var ping = _root.Q<Label>("coop-ping");
            if (ping != null) ping.style.display = DisplayStyle.None;
        }

        private Attrition.Networking.LobbyPlayer FindLocalLobbyStats()
        {
            foreach (var lp in FindObjectsByType<Attrition.Networking.LobbyPlayer>(FindObjectsSortMode.None))
            {
                if (lp == null || lp.Object == null) continue;
                if (lp.Object.HasInputAuthority) return lp;
            }
            return null;
        }

        private IEnumerator PollLobbyRoster()
        {
            var wait = new WaitForSeconds(0.5f);
            while (true)
            {
                var launcher = Attrition.Networking.NetworkLauncher.Instance;
                var runner = launcher != null ? launcher.Runner : null;

                if (runner != null)
                {
                    Attrition.Networking.LobbyPlayer hostLp = null;
                    Attrition.Networking.LobbyPlayer clientLp = null;

                    var allLp = FindObjectsByType<Attrition.Networking.LobbyPlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    foreach (var lp in allLp)
                    {
                        if (lp == null) continue;
                        if (lp.gameObject.scene.name == null) continue; // prefab chưa spawn
                        if (lp.IsHostPlayer) hostLp = lp;
                        else clientLp = lp;
                    }

                    if (hostLp != null)
                    {
                        string n = hostLp.DisplayName.Value;
                        SetText("coop-host-name", string.IsNullOrEmpty(n) ? "Wanderer" : n);
                        SetText("coop-host-level", $"LEVEL {Mathf.Max(1, hostLp.Level)}");
                        LoadAvatar("coop-card-host", "coop-avatar", hostLp.AvatarUrl.Value);
                        string room = hostLp.RoomName.Value;
                        if (!string.IsNullOrEmpty(room)) SetText("coop-room-name", room);
                    }

                    var clientCard = _root.Q<VisualElement>("coop-card-client");
                    if (clientLp != null)
                    {
                        string n = clientLp.DisplayName.Value;
                        bool ready = clientLp.IsReady;
                        SetText("coop-client-name", string.IsNullOrEmpty(n) ? "Wanderer" : n);
                        SetText("coop-client-level", $"LEVEL {Mathf.Max(1, clientLp.Level)}  •  {(ready ? "READY" : "NOT READY")}");
                        LoadAvatar("coop-card-client", "coop-avatar", clientLp.AvatarUrl.Value);
                        if (clientCard != null) clientCard.style.opacity = 1f;
                        _clientPresent = true;
                        _clientReady = ready;
                    }
                    else
                    {
                        SetText("coop-client-name", "Waiting for player...");
                        SetText("coop-client-level", "");
                        if (clientCard != null) clientCard.style.opacity = 0.5f;
                        _clientPresent = false;
                        _clientReady = false;
                    }

                    if (_isHost)
                    {
                        var startBtn = _root.Q<Button>("btn-coop-start");
                        if (startBtn != null)
                        {
                            bool canStart = _clientPresent && _clientReady;
                            startBtn.SetEnabled(canStart);
                            if (canStart) startBtn.RemoveFromClassList("coop-start-disabled");
                            else startBtn.AddToClassList("coop-start-disabled");
                        }
                    }
                }

                yield return wait;
            }
        }

        /// <summary>
        /// Tải avatar từ web vào element `avatarClass` trong card `cardName` (host/client).
        /// Đường dẫn tương đối (/api/account/media/...) được nối với baseUrl; URL tuyệt đối (Google)
        /// dùng nguyên. Bỏ qua nếu chưa có avatar hoặc đang tải. Load 1 lần rồi cache.
        /// </summary>
        private void LoadAvatar(string cardName, string avatarClass, string avatarUrl)
        {
            if (string.IsNullOrEmpty(avatarUrl)) return;

            var card = _root.Q<VisualElement>(cardName);
            if (card == null) return;

            var avatarEl = card.Q<VisualElement>(className: avatarClass);
            if (avatarEl == null) return;

            if (_avatarCache.TryGetValue(avatarUrl, out var cached))
            {
                ApplyAvatar(avatarEl, cached);
                return;
            }
            if (_avatarLoading.Contains(avatarUrl)) return;

            _avatarLoading.Add(avatarUrl);
            StartCoroutine(LoadAvatarRoutine(avatarUrl, avatarEl));
        }

        private IEnumerator LoadAvatarRoutine(string avatarUrl, VisualElement avatarEl)
        {
            // Nối vào WebUrl (gốc web, KHÔNG có /api) chứ KHÔNG phải BaseUrl: avatarUrl từ server là
            // /api/account/media/... (path tương đối ĐÃ có /api) → nối BaseUrl (vốn đã chứa /api) sinh
            // https://host/api/api/... → 404. Google avatar là URL tuyệt đối nên dùng nguyên.
            string full = avatarUrl.StartsWith("http") ? avatarUrl
                : (APIManager.Instance != null
                    ? APIManager.Instance.WebUrl + "/" + avatarUrl.TrimStart('/')
                    : avatarUrl);

            using (var req = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(full))
            {
                yield return req.SendWebRequest();
                _avatarLoading.Remove(avatarUrl);

                if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    var tex = ((UnityEngine.Networking.DownloadHandlerTexture)req.downloadHandler).texture;
                    if (tex != null)
                    {
                        _avatarCache[avatarUrl] = tex;
                        ApplyAvatar(avatarEl, tex);
                    }
                }
                else
                {
                    Debug.LogWarning($"[LobbyAvatar] Tải '{full}' lỗi: {req.error}");
                }
            }
        }

        private void ApplyAvatar(VisualElement el, Texture2D tex)
        {
            // Dùng ảnh làm background, cover cho vừa khung tròn 120px.
            el.style.backgroundImage = new StyleBackground(new Background(tex));
            el.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(BackgroundSizeType.Cover));
        }

        private void SetText(string elementName, string value)
        {
            var lbl = _root.Q<Label>(elementName);
            if (lbl != null) lbl.text = value;
        }
    }
}
