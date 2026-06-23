using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Attrition.Persistence;
using Fusion;
using System.Linq;

namespace Attrition.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class MainMenuUIController : MonoBehaviour
    {
        [Header("Audio")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _hoverSound;
        [SerializeField] private AudioClip _clickSound;

        private UIDocument _uiDocument;
        private VisualElement _root;

        // ===== Screens =====
        private VisualElement _mainMenuScreen;
        private VisualElement _saveSelectionScreen;
        private VisualElement _loginScreen;
        private VisualElement _hostJoinScreen;
        private VisualElement _settingsScreen;
        private VisualElement _sessionSelectionScreen;

        // ===== State =====
        private string _currentScreen = "main-menu";
        private string _previousScreen = "main-menu";
        private int _selectedSaveSlot = 0;
        private bool _isLoggedIn = false;
        private string _currentUserId = null;
        private string _currentRoomCode = null;
        private string _currentRoomName = null; // tên phòng host đặt (hiển thị trong lobby)
        private bool _isHost = false;
        private bool _isOnlineMode = false;

        private SaveSlotData[] _saveSlots;
        private string[] _characterIds = new string[SaveManager.SlotCount];

        // ===== Session Selection =====
        private System.Collections.Generic.List<APIManager.SessionSummaryDto> _cachedSessions;
        private int _selectedSessionIndex = -1; // -1 = "New Journey"
        private string _sessionIdToDelete;

        // ===== Particles =====
        private List<Button> _menuButtons = new List<Button>();
        private Dictionary<Button, Coroutine> _particleCoroutines = new Dictionary<Button, Coroutine>();

        // ===== Settings =====
        private string _activeSettingsTab = "gameplay";
        private readonly string[] _settingsTabNames = { "gameplay", "graphics", "audio", "controls" };

        private void OnEnable()
        {
            _uiDocument = GetComponent<UIDocument>();
            _root = _uiDocument.rootVisualElement;
            if (_root == null) return;

            if (LocalAuthServer.Instance != null)
                LocalAuthServer.Instance.OnTokenReceived.AddListener(HandleGoogleTokenReceived);

            if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();

            _mainMenuScreen = _root.Q<VisualElement>("main-menu-screen");
            _saveSelectionScreen = _root.Q<VisualElement>("save-selection-screen");
            _loginScreen = _root.Q<VisualElement>("login-screen");
            _hostJoinScreen = _root.Q<VisualElement>("host-join-screen");
            _settingsScreen = _root.Q<VisualElement>("settings-screen");
            _sessionSelectionScreen = _root.Q<VisualElement>("session-selection-screen");

            SetupMainMenu();
            BuildSaveSlots();
            SetupSaveSelection();
            SetupLogin();
            SetupHostJoin();
            SetupSettings();
            SetupGlobalProfile();
            SetupSessionSelection();

            // Khôi phục trạng thái login nếu APIManager (DontDestroyOnLoad) vẫn còn token hợp lệ
            if (APIManager.Instance != null && !string.IsNullOrEmpty(APIManager.Instance.AccessToken))
            {
                _isLoggedIn = true;
                _currentUserId = PlayerPrefs.GetString("SavedUserId", null);
            }

            LoadSavesFromDisk();

            ShowScreen("main-menu");
        }

        private void OnDisable()
        {
            if (LocalAuthServer.Instance != null)
                LocalAuthServer.Instance.OnTokenReceived.RemoveListener(HandleGoogleTokenReceived);
        }

        private void ShowScreen(string screenName)
        {
            _previousScreen = _currentScreen;
            _currentScreen = screenName;

            SetScreenVisible(_mainMenuScreen, screenName == "main-menu");
            SetScreenVisible(_saveSelectionScreen, screenName == "save-selection");
            SetScreenVisible(_loginScreen, screenName == "login");
            SetScreenVisible(_hostJoinScreen, screenName == "host-join");
            SetScreenVisible(_settingsScreen, screenName == "settings");
            SetScreenVisible(_sessionSelectionScreen, screenName == "session-selection");

            UpdateGlobalProfileVisibility();
        }

        private void SetScreenVisible(VisualElement screen, bool visible)
        {
            if (screen != null)
                screen.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void SetupGlobalProfile()
        {
            var logoutBtn = _root.Q<Button>("btn-global-logout");
            if (logoutBtn != null)
            {
                logoutBtn.RegisterCallback<PointerEnterEvent>(evt => PlayHoverSound());
                logoutBtn.RegisterCallback<ClickEvent>(evt =>
                {
                    PlayClickSound();
                    PlayerPrefs.DeleteKey("SavedUserId");
                    PlayerPrefs.Save();
                    _isLoggedIn = false;
                    _currentUserId = null;
                    UpdateGlobalProfileVisibility();
                    ShowScreen("main-menu");
                });
            }

            var loginBtn = _root.Q<Button>("btn-global-login");
            if (loginBtn != null)
            {
                loginBtn.RegisterCallback<PointerEnterEvent>(evt => PlayHoverSound());
                loginBtn.RegisterCallback<ClickEvent>(evt =>
                {
                    PlayClickSound();
                    ShowScreen("login");
                });
            }
        }

        private void UpdateGlobalProfileVisibility()
        {
            var profileContainer = _root.Q<VisualElement>("global-profile-container");
            var lbl = _root.Q<Label>("lbl-logged-in-user");
            var loginBtn = _root.Q<Button>("btn-global-login");
            var logoutBtn = _root.Q<Button>("btn-global-logout");

            if (profileContainer == null) return;

            // Don't show login/logout button ON the login screen itself
            if (_currentScreen == "login")
            {
                profileContainer.style.display = DisplayStyle.None;
                return;
            }

            profileContainer.style.display = DisplayStyle.Flex;

            if (_isLoggedIn)
            {
                if (lbl != null) lbl.text = "Logged in";
                if (loginBtn != null) loginBtn.style.display = DisplayStyle.None;
                if (logoutBtn != null) logoutBtn.style.display = DisplayStyle.Flex;
            }
            else
            {
                if (lbl != null) lbl.text = "Not Logged in";
                if (loginBtn != null) loginBtn.style.display = DisplayStyle.Flex;
                if (logoutBtn != null) logoutBtn.style.display = DisplayStyle.None;
            }
        }

        // =================================================================
        // MAIN MENU
        // =================================================================
        private void SetupMainMenu()
        {
            var buttons = _mainMenuScreen?.Query<Button>(className: "menu-button").ToList();
            if (buttons == null) return;

            foreach (var btn in buttons)
            {
                _menuButtons.Add(btn);

                var particleContainer = btn.Q<VisualElement>(className: "particle-container");
                if (particleContainer != null)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        var particle = new VisualElement();
                        particle.AddToClassList("particle");
                        particleContainer.Add(particle);
                    }
                }

                btn.RegisterCallback<PointerEnterEvent>(evt => OnButtonHover(btn));
                btn.RegisterCallback<PointerLeaveEvent>(evt => OnButtonLeave(btn));

                string capturedName = btn.name;
                btn.RegisterCallback<ClickEvent>(evt =>
                {
                    PlayClickSound();
                    switch (capturedName)
                    {
                        case "btn-solo-mode":
                            _isHost = false; // Solo mode isn't networking
                            _isOnlineMode = false;
                            UpdateSaveTitle("SELECT SAVE DATA");
                            LoadSavesFromDisk(); // reload slot LOCAL (tránh giữ list nhân vật server từ coop)
                            ShowScreen("save-selection");
                            break;
                        case "btn-coop-mode":
                            _isOnlineMode = true; // coop = nhân vật server (table character), không phải save local
                            if (_isLoggedIn)
                                ShowScreen("host-join");
                            else
                                ShowScreen("login");
                            break;
                        case "btn-settings":
                            ShowScreen("settings");
                            break;
                        case "btn-quit":
#if UNITY_EDITOR
                            UnityEditor.EditorApplication.isPlaying = false;
#else
                            Application.Quit();
#endif
                            break;
                    }
                });
            }
        }

        // =================================================================
        // SAVE SELECTION (REAL DATA)
        // =================================================================
        private void LoadSavesFromDisk()
        {
            if (_saveSlots == null) _saveSlots = new SaveSlotData[SaveManager.SlotCount];
            
            if (_isOnlineMode)
            {
                if (APIManager.Instance != null && !string.IsNullOrEmpty(APIManager.Instance.AccessToken))
                {
                    StartCoroutine(APIManager.Instance.GetCharacters((characters) => {
                        for (int i = 0; i < SaveManager.SlotCount; i++)
                        {
                            if (characters != null && i < characters.Count)
                            {
                                var c = characters[i];
                                _characterIds[i] = c.id;
                                _saveSlots[i] = new SaveSlotData { 
                                    characterName = c.name, 
                                    level = c.latestSnapshot?.level ?? 1, 
                                    location = "Server Save", 
                                    playtime = c.latestSnapshot?.playtimeSeconds.ToString() ?? "0", 
                                    deaths = 0 
                                };
                            }
                            else
                            {
                                _characterIds[i] = null;
                                _saveSlots[i] = null;
                            }
                            RenderSaveSlot(i, _saveSlots[i]);
                        }
                    }));
                }
                else
                {
                    for (int i = 0; i < SaveManager.SlotCount; i++)
                    {
                        _characterIds[i] = null;
                        _saveSlots[i] = null;
                        RenderSaveSlot(i, null);
                    }
                }
            }
            else
            {
                // SOLO: chỉ dùng file JSON local. Coop luôn đi nhánh _isOnlineMode (character server),
                // nên nhánh này luôn lọc theo Solo — KHÔNG phụ thuộc _isHost (có thể kẹt true từ
                // phiên coop trước). Save coop cũ lỡ nằm trong file local cũng bị ẩn khỏi solo.
                var localSaves = SaveManager.LoadAllSlots();
                for (int i = 0; i < SaveManager.SlotCount; i++)
                {
                    _characterIds[i] = null;
                    var s = localSaves[i];
                    // originMode == "Coop" → ẩn khỏi solo. Trống / save cũ chưa gắn mode → coi là solo.
                    bool isSolo = s == null || string.IsNullOrEmpty(s.originMode)
                                  || s.originMode == LaunchMode.Solo.ToString();
                    _saveSlots[i] = isSolo ? s : null;
                    RenderSaveSlot(i, _saveSlots[i]);
                }
            }
        }
        private void RenderSaveSlot(int index, SaveSlotData data)
        {
            var slotBtn = _root.Q<Button>($"save-slot-{index}");
            if (slotBtn == null) return;

            if (data != null)
            {
                slotBtn.RemoveFromClassList("empty");
                slotBtn.AddToClassList("filled");
                
                var innerEmpty = slotBtn.Q<VisualElement>(className: "save-slot-inner-empty");
                var innerFilled = slotBtn.Q<VisualElement>(className: "save-slot-inner");
                if (innerEmpty != null) innerEmpty.style.display = DisplayStyle.None;
                if (innerFilled != null) innerFilled.style.display = DisplayStyle.Flex;

                var nameLabel = slotBtn.Q<Label>(className: "save-name");
                var lvlLabel = slotBtn.Q<Label>(className: "save-level");
                var locLabel = slotBtn.Q<Label>(className: "save-location");
                var timeLabel = slotBtn.Q<Label>(className: "save-playtime");
                var deathLabel = slotBtn.Q<Label>(className: "save-deaths");

                if (nameLabel != null) nameLabel.text = data.characterName;
                if (lvlLabel != null) lvlLabel.text = $"LV. {data.level}";
                if (locLabel != null) locLabel.text = $"📍 {data.location}";
                if (timeLabel != null) timeLabel.text = $"⏱ {data.playtime}";
                if (deathLabel != null) deathLabel.text = $"{data.deaths} deaths";
            }
            else
            {
                slotBtn.RemoveFromClassList("filled");
                slotBtn.AddToClassList("empty");

                var innerEmpty = slotBtn.Q<VisualElement>(className: "save-slot-inner-empty");
                var innerFilled = slotBtn.Q<VisualElement>(className: "save-slot-inner");
                if (innerEmpty != null) innerEmpty.style.display = DisplayStyle.Flex;
                if (innerFilled != null) innerFilled.style.display = DisplayStyle.None;
            }
        }

        /// <summary>
        /// Dựng động SaveManager.SlotCount thẻ save trong ScrollView "save-slots-container".
        /// Mỗi thẻ giữ đúng tên/class như UXML cũ (save-slot-{i}, btn-delete-slot-{i}, các class)
        /// để RenderSaveSlot và SetupSaveSelection query không đổi.
        /// </summary>
        private void BuildSaveSlots()
        {
            var container = _root?.Q<ScrollView>("save-slots-container");
            if (container == null) return;
            container.Clear();

            string[] avatarColors = { "purple", "blue", "green", "red", "gold", "cyan" };

            for (int i = 0; i < SaveManager.SlotCount; i++)
            {
                var slotBtn = new Button { name = $"save-slot-{i}" };
                slotBtn.AddToClassList("save-slot");
                slotBtn.AddToClassList("empty");

                var indicator = new VisualElement();
                indicator.AddToClassList("save-slot-active-indicator");
                slotBtn.Add(indicator);

                // --- inner FILLED ---
                var inner = new VisualElement();
                inner.AddToClassList("save-slot-inner");

                var avatar = new VisualElement();
                avatar.AddToClassList("save-avatar");
                var circle = new VisualElement();
                circle.AddToClassList("save-avatar-circle");
                circle.AddToClassList(avatarColors[i % avatarColors.Length]);
                avatar.Add(circle);
                inner.Add(avatar);

                var info = new VisualElement();
                info.AddToClassList("save-info");
                var infoTop = new VisualElement();
                infoTop.AddToClassList("save-info-top");
                infoTop.Add(MakeLabel("", "save-name"));
                infoTop.Add(MakeLabel("", "save-level"));
                info.Add(infoTop);
                info.Add(MakeLabel("", "save-location"));
                var statsRow = new VisualElement();
                statsRow.AddToClassList("save-stats-row");
                statsRow.Add(MakeLabel("", "save-playtime"));
                statsRow.Add(MakeLabel("", "save-deaths"));
                info.Add(statsRow);
                inner.Add(info);

                var right = new VisualElement();
                right.AddToClassList("save-slot-right");
                right.Add(MakeLabel($"SLOT {i + 1}", "save-slot-number"));
                var del = new Button { name = $"btn-delete-slot-{i}", text = "🗑" };
                del.AddToClassList("save-delete-btn");
                right.Add(del);
                inner.Add(right);
                slotBtn.Add(inner);

                // --- inner EMPTY ---
                var innerEmpty = new VisualElement();
                innerEmpty.AddToClassList("save-slot-inner-empty");
                var emptyCircle = new VisualElement();
                emptyCircle.AddToClassList("save-empty-circle");
                emptyCircle.Add(MakeLabel("+", "save-empty-plus"));
                innerEmpty.Add(emptyCircle);
                innerEmpty.Add(MakeLabel($"EMPTY SLOT {i + 1}", "save-empty-text"));
                slotBtn.Add(innerEmpty);

                container.Add(slotBtn);
            }
        }

        private static Label MakeLabel(string text, string className)
        {
            var l = new Label(text);
            l.AddToClassList(className);
            return l;
        }

        private void SetupSaveSelection()
        {
            for (int i = 0; i < SaveManager.SlotCount; i++)
            {
                int slotIndex = i;
                var slotBtn = _root.Q<Button>($"save-slot-{i}");
                if (slotBtn == null) continue;

                slotBtn.RegisterCallback<PointerEnterEvent>(evt => PlayHoverSound());
                slotBtn.RegisterCallback<ClickEvent>(evt =>
                {
                    PlayClickSound();
                    SelectSaveSlot(slotIndex);
                });

                var deleteBtn = _root.Q<Button>($"btn-delete-slot-{i}");
                if (deleteBtn != null)
                {
                    deleteBtn.RegisterCallback<PointerEnterEvent>(evt => PlayHoverSound());
                    deleteBtn.RegisterCallback<ClickEvent>(evt =>
                    {
                        PlayClickSound();
                        evt.StopPropagation();

                        if (_isOnlineMode && !string.IsNullOrEmpty(_characterIds[slotIndex]) || (!_isOnlineMode && _saveSlots[slotIndex] != null))
                        {
                            var overlay = _root.Q<VisualElement>("delete-confirm-overlay");
                            if (overlay != null)
                            {
                                overlay.style.display = DisplayStyle.Flex;
                                
                                var confirmBtn = overlay.Q<Button>("btn-delete-confirm");
                                var cancelBtn = overlay.Q<Button>("btn-delete-cancel");

                                // Unregister previous callbacks to avoid multiple calls
                                confirmBtn?.UnregisterCallback<ClickEvent>(ConfirmDeleteCallback);
                                cancelBtn?.UnregisterCallback<ClickEvent>(CancelDeleteCallback);

                                void ConfirmDeleteCallback(ClickEvent e)
                                {
                                    PlayClickSound();
                                    overlay.style.display = DisplayStyle.None;

                                    if (_isOnlineMode)
                                    {
                                        StartCoroutine(APIManager.Instance.DeleteCharacter(_characterIds[slotIndex], (success) => {
                                            if (success) LoadSavesFromDisk();
                                        }));
                                    }
                                    else
                                    {
                                        SaveManager.DeleteSlot(slotIndex);
                                        LoadSavesFromDisk();
                                    }
                                }

                                void CancelDeleteCallback(ClickEvent e)
                                {
                                    PlayClickSound();
                                    overlay.style.display = DisplayStyle.None;
                                }

                                confirmBtn?.RegisterCallback<ClickEvent>(ConfirmDeleteCallback);
                                cancelBtn?.RegisterCallback<ClickEvent>(CancelDeleteCallback);
                            }
                        }
                    });
                }
            }

            var backBtn = _root.Q<Button>("btn-save-back");
            if (backBtn != null)
            {
                backBtn.RegisterCallback<PointerEnterEvent>(evt => PlayHoverSound());
                backBtn.RegisterCallback<ClickEvent>(evt =>
                {
                    PlayClickSound();
                    if (_previousScreen == "host-join")
                        ShowScreen("host-join");
                    else
                        ShowScreen("main-menu");
                });
            }

            var createBtn = _root.Q<Button>("btn-save-create");
            if (createBtn != null)
            {
                createBtn.RegisterCallback<PointerEnterEvent>(evt => PlayHoverSound());
                createBtn.RegisterCallback<ClickEvent>(evt =>
                {
                    PlayClickSound();
                    OpenNameEntry();
                });
            }

            SetupNameEntry();

            var continueBtn = _root.Q<Button>("btn-save-continue");
            if (continueBtn != null)
            {
                continueBtn.RegisterCallback<PointerEnterEvent>(evt => PlayHoverSound());
                continueBtn.RegisterCallback<ClickEvent>(evt =>
                {
                    PlayClickSound();
                    if (_saveSlots[_selectedSaveSlot] == null)
                    {
                        Debug.LogWarning("Selected slot is empty!");
                        return;
                    }

                    // Chặn dùng chéo chế độ: save Solo không mở ở Coop và ngược lại.
                    // _isOnlineMode = coop (host hoặc client join); ngược lại = solo.
                    var wantMode = _isOnlineMode ? LaunchMode.Coop : LaunchMode.Solo;
                    if (!SaveManager.IsSlotCompatible(_selectedSaveSlot, wantMode))
                    {
                        var origin = _isOnlineMode ? "Solo" : "Co-op";
                        var target = _isOnlineMode ? "Co-op" : "Solo";
                        ShowSlotWarning($"This save belongs to {origin} mode and cannot be played in {target}. Pick another slot or create a new character.");
                        return;
                    }

                    if (_isOnlineMode && _isHost)
                    {
                        // HOST: chọn nhân vật xong → mở màn chọn session (phòng cũ / tạo mới).
                        SetCoopLaunchContext();
                        LoadSessionsFromServer();
                        ShowScreen("session-selection");
                    }
                    else if (_isOnlineMode)
                    {
                        // CLIENT JOIN: chọn nhân vật xong mới connect Fusion ĐÚNG 1 LẦN với identity đã
                        // sẵn sàng (SetCoopLaunchContext set tên/char/owner TRƯỚC khi connect). Connect xong
                        // Fusion sync scene Lobby của host → client tự load scene Lobby (không ShowScreen).
                        SetCoopLaunchContext();
                        StartFusionNetwork(GameMode.Client, _currentRoomCode, (ok, err) =>
                        {
                            if (!ok)
                            {
                                var launcher = Attrition.Networking.NetworkLauncher.Instance;
                                if (launcher != null) launcher.LeaveSession();
                                ShowSlotWarning($"Could not join room '{_currentRoomCode}': {err}");
                                ShowScreen("host-join");
                            }
                        });
                    }
                    else
                    {
                        // SOLO cục bộ: lưu ý định + load thẳng scene gameplay (không cần login/mạng).
                        GameLaunch.Mode = LaunchMode.Solo;
                        GameLaunch.SelectedSlot = _selectedSaveSlot;
                        Debug.Log($"[MainMenu] Bắt đầu SOLO, slot {_selectedSaveSlot} → scene {GameLaunch.GameplayScene}");
                        StartCoroutine(LoadGameplaySceneAsync(GameLaunch.GameplayScene));
                    }
                });
            }

            SelectSaveSlot(0);
        }

        private static readonly string[] LoadingTips =
        {
            "Hold Space longer to jump higher.",
            "Shadow dash (Shift) grants 1 second of invincibility.",
            "Resting at a checkpoint refills HP, mana and flasks.",
            "Elite enemies can't be stunned - dodge, then counterattack.",
            "Co-op: stand near a downed ally and hold the revive key for 3 seconds.",
            "Each level grants 5 free stat points - build your own way."
        };

        /// <summary>Load scene gameplay (Solo) bất đồng bộ kèm màn loading + progress bar.</summary>
        private IEnumerator LoadGameplaySceneAsync(string sceneName)
        {
            var loading = _root?.Q<VisualElement>("menu-loading");
            var fill = _root?.Q<VisualElement>("menu-loading-fill");
            var tip = _root?.Q<Label>("menu-loading-tip");

            if (loading != null) loading.style.display = DisplayStyle.Flex;
            if (tip != null) tip.text = LoadingTips[Random.Range(0, LoadingTips.Length)];

            var op = SceneManager.LoadSceneAsync(sceneName);
            if (op == null)
            {
                Debug.LogError($"[MainMenu] Lỗi không thể load scene '{sceneName}'. Hãy kiểm tra File -> Build Settings xem đã tick chọn scene chưa.");
                if (loading != null) loading.style.display = DisplayStyle.None;
                yield break;
            }

            op.allowSceneActivation = false;

            // 0..0.9 = load thật; giữ ở 0.9 tới khi sẵn sàng kích hoạt.
            while (op.progress < 0.9f)
            {
                if (fill != null) fill.style.width = Length.Percent(Mathf.Clamp01(op.progress / 0.9f) * 100f);
                yield return null;
            }

            if (fill != null) fill.style.width = Length.Percent(100f);
            yield return new WaitForSeconds(0.4f); // cho người chơi kịp đọc tip
            op.allowSceneActivation = true;
        }

        // =================================================================
        // NAME ENTRY (tạo nhân vật mới — BR-02/03/04)
        // =================================================================
        private void SetupNameEntry()
        {
            var confirm = _root.Q<Button>("btn-name-confirm");
            if (confirm != null)
            {
                confirm.RegisterCallback<PointerEnterEvent>(evt => PlayHoverSound());
                confirm.RegisterCallback<ClickEvent>(evt => { PlayClickSound(); OnConfirmName(); });
            }

            var cancel = _root.Q<Button>("btn-name-cancel");
            if (cancel != null)
            {
                cancel.RegisterCallback<PointerEnterEvent>(evt => PlayHoverSound());
                cancel.RegisterCallback<ClickEvent>(evt => { PlayClickSound(); CloseNameEntry(); });
            }
        }

        private void OpenNameEntry()
        {
            var overlay = _root.Q<VisualElement>("name-entry-overlay");
            var input = _root.Q<TextField>("name-entry-input");
            var error = _root.Q<Label>("name-entry-error");
            if (input != null) input.value = "";
            if (error != null) error.text = "";
            if (overlay != null) overlay.style.display = DisplayStyle.Flex;
            if (input != null) input.Focus();
        }

        private void CloseNameEntry()
        {
            var overlay = _root.Q<VisualElement>("name-entry-overlay");
            if (overlay != null) overlay.style.display = DisplayStyle.None;
        }

        /// <summary>BR-03 (chỉ chữ-số), BR-04 (3–16). Trả null nếu hợp lệ, hoặc thông báo lỗi.</summary>
        private static string ValidateNameFormat(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Please enter a name.";
            if (name.Length < 3 || name.Length > 16) return "Name must be 3-16 characters long.";
            foreach (char c in name)
                if (!char.IsLetterOrDigit(c)) return "Only letters and numbers are allowed (no special characters).";
            return null;
        }

        /// <summary>BR-02: tên unique. Solo check các slot local; coop sẽ check thêm trên server.</summary>
        private bool IsNameTakenLocally(string name)
        {
            if (_saveSlots == null) return false;
            foreach (var s in _saveSlots)
                if (s != null && string.Equals(s.characterName, name, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private void OnConfirmName()
        {
            var input = _root.Q<TextField>("name-entry-input");
            var error = _root.Q<Label>("name-entry-error");
            string name = input?.value?.Trim() ?? "";

            string fmtErr = ValidateNameFormat(name);
            if (fmtErr != null) { if (error != null) error.text = fmtErr; return; }

            if (IsNameTakenLocally(name))
            {
                if (error != null) error.text = "This name is already used in another slot.";
                return;
            }

            // First empty slot in the CURRENT mode to hold the new character.
            int slot = FindFirstEmptySlot();
            if (slot < 0) { if (error != null) error.text = "No empty slots - please delete a character first."; return; }
            _selectedSaveSlot = slot;

            // _isOnlineMode = coop (host hoặc client join); ngược lại = solo.
            var mode = _isOnlineMode ? LaunchMode.Coop : LaunchMode.Solo;
            var newSave = new SaveSlotData
            {
                characterName = name,
                level = 1,
                location = "The Ashen Threshold",
                playtimeSeconds = 0,
                playtime = "00:00",
                deaths = 0,
                currentHP = 0, currentMana = 0,
                potionMaxFlasks = 3,
                allocatedPoints = new int[7],
                originMode = mode.ToString(),
                lastSavedUnix = 0
            };

            SaveManager.SaveSlot(slot, newSave);
            _saveSlots[slot] = newSave;
            CloseNameEntry();

            if (mode == LaunchMode.Solo)
            {
                // Tạo file save local rồi vào game qua màn loading.
                GameLaunch.Mode = LaunchMode.Solo;
                GameLaunch.SelectedSlot = slot;
                GameLaunch.CharacterName = name;
                StartCoroutine(LoadGameplaySceneAsync(GameLaunch.GameplayScene));
            }
            else
            {
                if (APIManager.Instance != null)
                {
                    var req = new APIManager.SnapshotIngestRequest
                    {
                        ownerId = _currentUserId ?? "",
                        characterId = null,
                        name = name,
                        archetype = "Wanderer",
                        level = 1,
                        hp = 100, maxHp = 100, gold = 0, isAlive = true,
                        roomCode = _currentRoomCode ?? "",
                        eventType = "rest",
                        playtimeSeconds = 0,
                        inventoryJson = "{}", equipmentJson = "{}", questsJson = "{}"
                    };
                    
                    StartCoroutine(APIManager.Instance.PostSnapshot(req, success =>
                    {
                        if (success) LoadSavesFromDisk(); // Refresh danh sách để lấy ID
                        ProceedOnlineFlow();
                    }));
                }
                else
                {
                    ProceedOnlineFlow();
                }
            }

            void ProceedOnlineFlow()
            {
                if (_isHost)
                {
                    // HOST: nhân vật mới tạo xong → mở màn chọn session.
                    SetCoopLaunchContext();
                    LoadSessionsFromServer();
                    ShowScreen("session-selection");
                }
                else
                {
                    // CLIENT JOIN: phải ĐÚNG room code. Connect xong, Fusion sync scene Lobby của host →
                    // client tự load scene Lobby. KHÔNG ShowScreen("coop-lobby") nữa (lobby là scene riêng).
                    SetCoopLaunchContext();
                    StartFusionNetwork(GameMode.Client, _currentRoomCode, (ok, err) =>
                    {
                        if (!ok)
                        {
                            var launcher = Attrition.Networking.NetworkLauncher.Instance;
                            if (launcher != null) launcher.LeaveSession();
                            ShowSlotWarning($"Could not join room '{_currentRoomCode}': {err}");
                            ShowScreen("host-join");
                        }
                    });
                }
            }
        }

        /// <summary>Slot trống đầu tiên (theo dữ liệu đã lọc của chế độ hiện tại).</summary>
        private int FindFirstEmptySlot()
        {
            if (_saveSlots == null) return 0;
            for (int i = 0; i < _saveSlots.Length; i++)
                if (_saveSlots[i] == null) return i;
            return -1;
        }

        private VisualElement _slotWarningToast;

        /// <summary>Hiện cảnh báo nổi (toast) khi chọn slot sai chế độ. Tự dựng runtime, tự ẩn sau 4s.</summary>
        private void ShowSlotWarning(string message)
        {
            if (_root == null) return;

            if (_slotWarningToast == null)
            {
                _slotWarningToast = new VisualElement();
                _slotWarningToast.style.position = Position.Absolute;
                _slotWarningToast.style.bottom = 40;
                _slotWarningToast.style.left = Length.Percent(50);
                _slotWarningToast.style.translate = new StyleTranslate(new Translate(Length.Percent(-50), 0));
                _slotWarningToast.style.paddingLeft = 22;
                _slotWarningToast.style.paddingRight = 22;
                _slotWarningToast.style.paddingTop = 12;
                _slotWarningToast.style.paddingBottom = 12;
                _slotWarningToast.style.backgroundColor = new Color(0.12f, 0.04f, 0.04f, 0.96f);
                _slotWarningToast.style.borderTopLeftRadius = 6;
                _slotWarningToast.style.borderTopRightRadius = 6;
                _slotWarningToast.style.borderBottomLeftRadius = 6;
                _slotWarningToast.style.borderBottomRightRadius = 6;
                SetBorder(_slotWarningToast, new Color(0.78f, 0.25f, 0.25f, 0.9f), 1);
                _slotWarningToast.style.maxWidth = 560;

                var label = new Label { name = "slot-warning-label" };
                label.style.color = new Color(0.96f, 0.82f, 0.78f);
                label.style.fontSize = 13;
                label.style.whiteSpace = WhiteSpace.Normal;
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                _slotWarningToast.Add(label);
                _root.Add(_slotWarningToast);
            }

            _slotWarningToast.Q<Label>("slot-warning-label").text = message;
            _slotWarningToast.style.display = DisplayStyle.Flex;
            _slotWarningToast.BringToFront();

            _slotWarningToast.schedule.Execute(() =>
            {
                if (_slotWarningToast != null) _slotWarningToast.style.display = DisplayStyle.None;
            }).StartingIn(4000);
        }

        private static void SetBorder(VisualElement ve, Color c, float w)
        {
            ve.style.borderTopColor = c; ve.style.borderBottomColor = c;
            ve.style.borderLeftColor = c; ve.style.borderRightColor = c;
            ve.style.borderTopWidth = w; ve.style.borderBottomWidth = w;
            ve.style.borderLeftWidth = w; ve.style.borderRightWidth = w;
        }

        private void SelectSaveSlot(int index)
        {
            _selectedSaveSlot = index;

            for (int i = 0; i < SaveManager.SlotCount; i++)
            {
                var slot = _root.Q<Button>($"save-slot-{i}");
                if (slot == null) continue;

                if (i == index)
                {
                    slot.AddToClassList("selected");
                    var indicator = slot.Q<VisualElement>(className: "save-slot-active-indicator");
                    if (indicator != null) indicator.style.opacity = 1f;
                }
                else
                {
                    slot.RemoveFromClassList("selected");
                    var indicator = slot.Q<VisualElement>(className: "save-slot-active-indicator");
                    if (indicator != null) indicator.style.opacity = 0f;
                }
            }
        }

        private void UpdateSaveTitle(string titleText)
        {
            var title = _root.Q<Label>("save-title");
            if (title != null) title.text = titleText;
        }

        // =================================================================
        // LOGIN SCREEN (REAL API)
        // =================================================================
        private void SetupLogin()
        {
            var loginError = _root.Q<VisualElement>("login-error");
            var emailInput = _root.Q<TextField>("input-email");
            var passwordInput = _root.Q<TextField>("input-password");
            var errorText = _root.Q<Label>("login-error-text");

            var loginBtn = _root.Q<Button>("btn-login-submit");
            if (loginBtn != null)
            {
                loginBtn.RegisterCallback<PointerEnterEvent>(evt => PlayHoverSound());
                loginBtn.RegisterCallback<ClickEvent>(evt =>
                {
                    PlayClickSound();

                    string email = emailInput?.value ?? "";
                    string password = passwordInput?.value ?? "";

                    if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
                    {
                        if (loginError != null) loginError.style.display = DisplayStyle.Flex;
                        if (errorText != null) errorText.text = "⚠ Please enter both email and password.";
                        return;
                    }

                    if (loginError != null) loginError.style.display = DisplayStyle.Flex;
                    if (errorText != null)
                    {
                        errorText.text = "Logging in...";
                        errorText.style.color = new StyleColor(Color.white);
                    }

                    if (APIManager.Instance == null)
                    {
                        Debug.Log("APIManager not found in scene. Creating one automatically.");
                        var apiObj = new GameObject("APIManager");
                        apiObj.AddComponent<APIManager>();
                    }

                    StartCoroutine(APIManager.Instance.Login(email, password, (userId) => {
                        if (!string.IsNullOrEmpty(userId))
                        {
                            PlayerPrefs.SetString("SavedUserId", userId);
                            PlayerPrefs.Save();
                            _isLoggedIn = true;
                            _currentUserId = userId;
                            UpdateGlobalProfileVisibility();
                            
                            if (loginError != null) loginError.style.display = DisplayStyle.None;
                            ShowScreen("host-join");
                        }
                        else
                        {
                            if (errorText != null) 
                            {
                                errorText.style.color = new StyleColor(new Color(0.86f, 0.39f, 0.39f));
                                errorText.text = "⚠ Incorrect username or password.";
                            }
                        }
                    }));
                });
            }

            var backBtn = _root.Q<Button>("btn-login-back");
            if (backBtn != null)
            {
                backBtn.RegisterCallback<PointerEnterEvent>(evt => PlayHoverSound());
                backBtn.RegisterCallback<ClickEvent>(evt =>
                {
                    PlayClickSound();
                    if (loginError != null) loginError.style.display = DisplayStyle.None;
                    ShowScreen("main-menu");
                });
            }

            var googleBtn = _root.Q<Button>("btn-login-google");
            if (googleBtn != null)
            {
                googleBtn.RegisterCallback<PointerEnterEvent>(evt => PlayHoverSound());
                googleBtn.RegisterCallback<ClickEvent>(evt =>
                {
                    PlayClickSound();
                    
                    if (loginError != null) loginError.style.display = DisplayStyle.Flex;
                    if (errorText != null)
                    {
                        errorText.text = "Waiting for browser login...";
                        errorText.style.color = new StyleColor(Color.white);
                    }

                    if (LocalAuthServer.Instance != null)
                        LocalAuthServer.Instance.StartListening();

                    Application.OpenURL("http://localhost:3000/login?client=unity");
                });
            }
        }

        private void HandleGoogleTokenReceived(string token)
        {
            var loginError = _root.Q<VisualElement>("login-error");
            var errorText = _root.Q<Label>("login-error-text");

            if (loginError != null) loginError.style.display = DisplayStyle.Flex;
            if (errorText != null)
            {
                errorText.text = "Authenticating Google Token...";
                errorText.style.color = new StyleColor(Color.white);
            }

            if (APIManager.Instance == null)
            {
                var apiObj = new GameObject("APIManager");
                apiObj.AddComponent<APIManager>();
            }

            StartCoroutine(APIManager.Instance.LoginWithToken(token, (userId) => {
                if (!string.IsNullOrEmpty(userId))
                {
                    PlayerPrefs.SetString("SavedUserId", userId);
                    PlayerPrefs.Save();
                    _isLoggedIn = true;
                    _currentUserId = userId;
                    UpdateGlobalProfileVisibility();
                    
                    if (loginError != null) loginError.style.display = DisplayStyle.None;
                    ShowScreen("host-join");
                }
                else
                {
                    if (errorText != null) 
                    {
                        errorText.style.color = new StyleColor(new Color(0.86f, 0.39f, 0.39f));
                        errorText.text = "⚠ Google Login failed. Please try again.";
                    }
                }
            }));
        }

        // =================================================================
        // HOST OR JOIN SCREEN
        // =================================================================
        private void SetupHostJoin()
        {
            var hostBtn = _root.Q<Button>("btn-host-game");
            if (hostBtn != null)
            {
                hostBtn.RegisterCallback<PointerEnterEvent>(evt => PlayHoverSound());
                hostBtn.RegisterCallback<ClickEvent>(evt =>
                {
                    PlayClickSound();
                    _isHost = true;
                    _isOnlineMode = true;
                    // Auto generate 4 char room code
                    _currentRoomCode = GenerateRoomCode();
                    _currentRoomName = $"Room {_currentRoomCode}";

                    UpdateSaveTitle("SELECT CHARACTER (HOST)");
                    LoadSavesFromDisk(); // nạp nhân vật server (table character) cho host
                    ShowScreen("save-selection");
                });
            }

            var roomCodeInput = _root.Q<TextField>("input-room-code");
            var joinBtn = _root.Q<Button>("btn-join-game");
            
            if (joinBtn != null)
            {
                joinBtn.RegisterCallback<PointerEnterEvent>(evt => PlayHoverSound());
                joinBtn.RegisterCallback<ClickEvent>(evt =>
                {
                    PlayClickSound();
                    string code = roomCodeInput?.value;
                    if (string.IsNullOrEmpty(code)) return;

                    _isHost = false;
                    _isOnlineMode = true;
                    _currentRoomCode = code.Trim().ToUpperInvariant();

                    // Validate room code bằng API (KHÔNG connect Fusion sớm). Phòng tồn tại → sang màn
                    // chọn nhân vật; connect Fusion ĐÚNG 1 LẦN ở bước Continue với identity đã sẵn sàng.
                    // Sai mã → báo lỗi, ở lại màn nhập code. Tránh luồng connect-2-lần + đẩy-lại-identity.
                    if (APIManager.Instance == null)
                    {
                        ShowSlotWarning("Service not ready. Please try again.");
                        return;
                    }
                    joinBtn.SetEnabled(false);
                    StartCoroutine(APIManager.Instance.GetSessionByCode(_currentRoomCode, session =>
                    {
                        joinBtn.SetEnabled(true);
                        if (session != null)
                        {
                            _currentRoomName = session.name;
                            UpdateSaveTitle("SELECT CHARACTER (JOIN)");
                            LoadSavesFromDisk();
                            ShowScreen("save-selection");
                        }
                        else
                        {
                            ShowSlotWarning($"Room '{_currentRoomCode}' not found. Check the code and try again.");
                        }
                    }));
                });
            }

            var backBtn = _root.Q<Button>("btn-host-join-back");
            if (backBtn != null)
            {
                backBtn.RegisterCallback<PointerEnterEvent>(evt => PlayHoverSound());
                backBtn.RegisterCallback<ClickEvent>(evt =>
                {
                    PlayClickSound();
                    ShowScreen("main-menu");
                });
            }
        }

        private string GenerateRoomCode()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            string code = "";
            for (int i = 0; i < 4; i++) code += chars[Random.Range(0, chars.Length)];
            return code;
        }

        private void StartFusionNetwork(GameMode mode, string code, System.Action<bool, string> onResult = null)
        {
            var launcher = Attrition.Networking.NetworkLauncher.Instance;

            // Fallback: Instance có thể bị null nếu Fusion Shutdown hủy GO bất ngờ.
            // Tìm bằng type để khôi phục; nếu không tìm thấy mới báo lỗi.
            if (launcher == null)
                launcher = FindFirstObjectByType<Attrition.Networking.NetworkLauncher>();

            if (launcher != null)
            {
                launcher.StartCoopLobby(mode, code, (ok, err) => 
                {
                    if (ok) 
                    {
                        // Ẩn Main Menu UI và Camera để nhường chỗ cho Lobby/Game UI
                        if (_root != null) _root.style.display = DisplayStyle.None;
                        var cam = Camera.main;
                        if (cam != null && cam.gameObject.scene.name == "Main_Menu_UI") 
                            cam.gameObject.SetActive(false);
                    }
                    onResult?.Invoke(ok, err);
                });
            }
            else
            {
                Debug.LogError("NetworkLauncher not found! Phải đặt sẵn object NetworkLauncher trong scene Menu.");
                onResult?.Invoke(false, "NetworkLauncher not found in Menu scene");
            }
        }

        /// <summary>
        /// Gán bối cảnh ONLINE coop vào GameLaunch từ slot đang chọn: room code, tên nhân vật,
        /// characterId (server) và ownerId (đăng nhập). Lưu/khôi phục online dựa vào các giá trị này.
        /// </summary>
        private void SetCoopLaunchContext()
        {
            GameLaunch.Mode = LaunchMode.Coop;
            GameLaunch.SelectedSlot = _selectedSaveSlot;
            GameLaunch.RoomCode = _currentRoomCode;
            GameLaunch.RoomName = _currentRoomName ?? "";
            GameLaunch.OwnerId = _currentUserId ?? "";
            GameLaunch.CharacterId = _characterIds != null && _selectedSaveSlot < _characterIds.Length
                ? (_characterIds[_selectedSaveSlot] ?? "") : "";

            // Tên player = USERNAME tài khoản (Postgres, vd "PlayerOne"), KHÔNG phải tên save slot.
            // Save slot chỉ là tên file save/load. Fallback: tên nhân vật slot, rồi "Wanderer".
            string playerName = APIManager.Instance != null ? APIManager.Instance.Username : null;
            if (string.IsNullOrEmpty(playerName))
            {
                var slot = _saveSlots != null && _selectedSaveSlot < _saveSlots.Length ? _saveSlots[_selectedSaveSlot] : null;
                if (slot != null && !string.IsNullOrEmpty(slot.characterName)) playerName = slot.characterName;
            }
            if (!string.IsNullOrEmpty(playerName)) GameLaunch.CharacterName = playerName;
        }

        /// <summary>
        /// HOST: tạo (hoặc reopen) room bền trên server theo ĐÚNG room code Fusion đang dùng, rồi lưu
        /// SessionId vào GameLaunch làm khóa cho save/load per-room (character_session, world_state).
        /// Backend tôn trọng room code host gửi nên server code == join code. Lỗi mạng → KHÔNG chặn
        /// vào lobby (vẫn chơi được; chỉ là tiến trình per-room chưa lưu được tới khi có session).
        /// Chỉ host gọi: client không tự lưu (host-authoritative), nên client không cần SessionId.
        /// </summary>
        private void CreateHostSessionOnServer()
        {
            if (APIManager.Instance == null || string.IsNullOrEmpty(GameLaunch.OwnerId)) return;

            var req = new APIManager.CreateSessionRequest
            {
                ownerId = GameLaunch.OwnerId,
                name = string.IsNullOrEmpty(_currentRoomName) ? $"Room {_currentRoomCode}" : _currentRoomName,
                roomCode = _currentRoomCode,
                currentScene = GameLaunch.GameplayScene
            };

            StartCoroutine(APIManager.Instance.CreateOrReopenSession(req, session =>
            {
                if (session != null)
                {
                    GameLaunch.SessionId = session.id;
                    Debug.Log($"[Session] Host room sẵn sàng: id={session.id} code={session.roomCode}");
                }
                else
                {
                    GameLaunch.SessionId = "";
                    Debug.LogWarning("[Session] Không tạo được room trên server (vẫn vào lobby, tiến trình per-room chưa lưu).");
                }
            }));
        }

        // =================================================================
        // SESSION SELECTION (HOST ONLY)
        // =================================================================

        private void SetupSessionSelection()
        {
            var backBtn = _root.Q<Button>("btn-session-back");
            if (backBtn != null)
            {
                backBtn.RegisterCallback<PointerEnterEvent>(evt => PlayHoverSound());
                backBtn.RegisterCallback<ClickEvent>(evt =>
                {
                    PlayClickSound();
                    ShowScreen("save-selection");
                });
            }

            var newBtn = _root.Q<Button>("btn-session-new");
            if (newBtn != null)
            {
                newBtn.RegisterCallback<PointerEnterEvent>(evt => PlayHoverSound());
                newBtn.RegisterCallback<ClickEvent>(evt =>
                {
                    PlayClickSound();
                    _selectedSessionIndex = -1;
                    HighlightSelectedSession();
                });
            }

            var continueBtn = _root.Q<Button>("btn-session-continue");
            if (continueBtn != null)
            {
                continueBtn.RegisterCallback<PointerEnterEvent>(evt => PlayHoverSound());
                continueBtn.RegisterCallback<ClickEvent>(evt =>
                {
                    PlayClickSound();
                    OnSessionContinue();
                });
            }

            // Reset overlay buttons
            var resetCancel = _root.Q<Button>("btn-reset-cancel");
            if (resetCancel != null)
            {
                resetCancel.RegisterCallback<PointerEnterEvent>(evt => PlayHoverSound());
                resetCancel.RegisterCallback<ClickEvent>(evt =>
                {
                    PlayClickSound();
                    var overlay = _root.Q<VisualElement>("session-reset-overlay");
                    if (overlay != null) overlay.style.display = DisplayStyle.None;
                });
            }

            var resetConfirm = _root.Q<Button>("btn-reset-confirm");
            if (resetConfirm != null)
            {
                resetConfirm.RegisterCallback<PointerEnterEvent>(evt => PlayHoverSound());
                resetConfirm.RegisterCallback<ClickEvent>(evt =>
                {
                    PlayClickSound();
                    var overlay = _root.Q<VisualElement>("session-reset-overlay");
                    if (overlay != null) overlay.style.display = DisplayStyle.None;
                    if (!string.IsNullOrEmpty(_sessionIdToDelete) && APIManager.Instance != null)
                    {
                        StartCoroutine(APIManager.Instance.DeleteSession(_sessionIdToDelete, success =>
                        {
                            if (success)
                                Debug.Log($"[Session] Delete thành công: {_sessionIdToDelete}");
                            else
                                ShowSlotWarning("Failed to delete session.");
                            LoadSessionsFromServer(); // refresh list
                        }));
                    }
                });
            }
        }

        private void LoadSessionsFromServer()
        {
            if (APIManager.Instance == null || string.IsNullOrEmpty(APIManager.Instance.AccessToken))
            {
                _cachedSessions = null;
                BuildSessionCards();
                return;
            }

            StartCoroutine(APIManager.Instance.GetMySessions(sessions =>
            {
                _cachedSessions = sessions;
                _selectedSessionIndex = _cachedSessions != null && _cachedSessions.Count > 0 ? 0 : -1;
                BuildSessionCards();
            }));
        }

        private void BuildSessionCards()
        {
            var container = _root?.Q<ScrollView>("session-list-container");
            if (container == null) return;
            container.Clear();

            if (_cachedSessions == null || _cachedSessions.Count == 0)
            {
                // Empty state
                var emptyState = new VisualElement();
                emptyState.AddToClassList("session-empty-state");
                var icon = new Label("⚔");
                icon.AddToClassList("session-empty-icon");
                emptyState.Add(icon);
                var text = new Label("NO JOURNEYS YET\nCreate a new journey to begin your adventure.");
                text.AddToClassList("session-empty-text");
                emptyState.Add(text);
                container.Add(emptyState);
                _selectedSessionIndex = -1;
                return;
            }

            for (int i = 0; i < _cachedSessions.Count; i++)
            {
                int idx = i;
                var session = _cachedSessions[i];

                var card = new Button { name = $"session-card-{i}" };
                card.AddToClassList("session-card");

                // Selection indicator
                var indicator = new VisualElement();
                indicator.AddToClassList("session-card-indicator");
                card.Add(indicator);

                var inner = new VisualElement();
                inner.AddToClassList("session-card-inner");

                // Room code badge
                var codeBadge = new VisualElement();
                codeBadge.AddToClassList("session-code-badge");
                var codeText = new Label(session.roomCode ?? "???");
                codeText.AddToClassList("session-code-text");
                codeBadge.Add(codeText);
                inner.Add(codeBadge);

                // Info block
                var info = new VisualElement();
                info.AddToClassList("session-info");

                var infoTop = new VisualElement();
                infoTop.AddToClassList("session-info-top");
                var nameLabel = new Label(session.name ?? "Unnamed Room");
                nameLabel.AddToClassList("session-room-name");
                infoTop.Add(nameLabel);
                var playerCount = new Label($"{session.characterCount}/2");
                playerCount.AddToClassList("session-player-count");
                infoTop.Add(playerCount);
                info.Add(infoTop);

                var detailRow = new VisualElement();
                detailRow.AddToClassList("session-detail-row");

                if (!string.IsNullOrEmpty(session.currentScene))
                {
                    var sceneLabel = new Label($"📍 {session.currentScene}");
                    sceneLabel.AddToClassList("session-scene");
                    detailRow.Add(sceneLabel);
                }

                var playtimeLabel = new Label($"⏱ {FormatPlaytime(session.playTimeSeconds)}");
                playtimeLabel.AddToClassList("session-playtime");
                detailRow.Add(playtimeLabel);

                if (!string.IsNullOrEmpty(session.lastPlayedAt))
                {
                    var lastPlayed = new Label($"Last: {FormatLastPlayed(session.lastPlayedAt)}");
                    lastPlayed.AddToClassList("session-last-played");
                    detailRow.Add(lastPlayed);
                }

                info.Add(detailRow);
                inner.Add(info);

                // Right side: delete button
                var right = new VisualElement();
                right.AddToClassList("session-card-right");
                var resetBtn = new Button { name = $"btn-session-reset-{i}", text = "✖" };
                resetBtn.AddToClassList("session-reset-btn");
                resetBtn.RegisterCallback<PointerEnterEvent>(evt => PlayHoverSound());
                resetBtn.RegisterCallback<ClickEvent>(evt =>
                {
                    PlayClickSound();
                    evt.StopPropagation();
                    _sessionIdToDelete = session.id;
                    var overlay = _root.Q<VisualElement>("session-reset-overlay");
                    if (overlay != null) overlay.style.display = DisplayStyle.Flex;
                });
                right.Add(resetBtn);
                inner.Add(right);

                card.Add(inner);

                card.RegisterCallback<PointerEnterEvent>(evt => PlayHoverSound());
                card.RegisterCallback<ClickEvent>(evt =>
                {
                    PlayClickSound();
                    _selectedSessionIndex = idx;
                    HighlightSelectedSession();
                });

                container.Add(card);
            }

            HighlightSelectedSession();
        }

        private void HighlightSelectedSession()
        {
            if (_cachedSessions == null) return;
            for (int i = 0; i < _cachedSessions.Count; i++)
            {
                var card = _root.Q<Button>($"session-card-{i}");
                if (card == null) continue;
                if (i == _selectedSessionIndex) card.AddToClassList("selected");
                else card.RemoveFromClassList("selected");
            }
        }

        /// <summary>
        /// Host bấm Continue: dùng session đã chọn (reopen) hoặc tạo mới, rồi StartGame Fusion.
        /// </summary>
        private void OnSessionContinue()
        {
            if (_selectedSessionIndex >= 0 && _cachedSessions != null && _selectedSessionIndex < _cachedSessions.Count)
            {
                // Reopen session cũ: dùng room code của session đó.
                var session = _cachedSessions[_selectedSessionIndex];
                _currentRoomCode = session.roomCode;
                _currentRoomName = session.name;
                GameLaunch.RoomCode = _currentRoomCode;
                GameLaunch.RoomName = _currentRoomName ?? "";

                CreateHostSessionOnServer();
                StartFusionNetwork(GameMode.Host, _currentRoomCode, (ok, err) =>
                {
                    if (!ok)
                    {
                        ShowSlotWarning($"Could not open room: {err}");
                        ShowScreen("session-selection");
                    }
                });
            }
            else
            {
                // Tạo mới: sinh room code mới.
                _currentRoomCode = GenerateRoomCode();
                _currentRoomName = $"Room {_currentRoomCode}";
                GameLaunch.RoomCode = _currentRoomCode;
                GameLaunch.RoomName = _currentRoomName;

                CreateHostSessionOnServer();
                StartFusionNetwork(GameMode.Host, _currentRoomCode, (ok, err) =>
                {
                    if (!ok)
                    {
                        ShowSlotWarning($"Could not create room: {err}");
                        ShowScreen("session-selection");
                    }
                });
            }
        }

        private static string FormatPlaytime(int seconds)
        {
            if (seconds <= 0) return "00:00";
            int h = seconds / 3600;
            int m = (seconds % 3600) / 60;
            return h > 0 ? $"{h}h {m:D2}m" : $"{m:D2}:{seconds % 60:D2}";
        }

        private static string FormatLastPlayed(string isoDate)
        {
            if (System.DateTime.TryParse(isoDate, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
            {
                var diff = System.DateTime.UtcNow - dt;
                if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
                if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
                if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
                return dt.ToString("MMM dd");
            }
            return "";
        }

        // =================================================================
        // SETTINGS (UNCHANGED)
        // =================================================================
        private void SetupSettings()
        {
            foreach (var tabName in _settingsTabNames)
            {
                string capturedTab = tabName;
                var tabBtn = _root.Q<Button>($"tab-{tabName}");
                if (tabBtn == null) continue;

                tabBtn.RegisterCallback<PointerEnterEvent>(evt => PlayHoverSound());
                tabBtn.RegisterCallback<ClickEvent>(evt =>
                {
                    PlayClickSound();
                    SwitchSettingsTab(capturedTab);
                });
            }

            var backBtn = _root.Q<Button>("btn-settings-back");
            if (backBtn != null)
            {
                backBtn.RegisterCallback<PointerEnterEvent>(evt => PlayHoverSound());
                backBtn.RegisterCallback<ClickEvent>(evt =>
                {
                    PlayClickSound();
                    ShowScreen("main-menu");
                });
            }

            var resetBtn = _root.Q<Button>("btn-settings-reset");
            if (resetBtn != null)
            {
                resetBtn.RegisterCallback<PointerEnterEvent>(evt => PlayHoverSound());
                resetBtn.RegisterCallback<ClickEvent>(evt =>
                {
                    PlayClickSound();
                    ResetSettingsToDefault();
                });
            }

            var applyBtn = _root.Q<Button>("btn-settings-apply");
            if (applyBtn != null)
            {
                applyBtn.RegisterCallback<PointerEnterEvent>(evt => PlayHoverSound());
                applyBtn.RegisterCallback<ClickEvent>(evt =>
                {
                    PlayClickSound();
                    ApplySettings();
                });
            }

            SetupSliderLabel("slider-master", "label-master-val");
            SetupSliderLabel("slider-music", "label-music-val");
            SetupSliderLabel("slider-sfx", "label-sfx-val");
            SetupSliderLabel("slider-ambient", "label-ambient-val");
            SetupSliderLabel("slider-voice", "label-voice-val");

            SetupKeybindButtons();
            LoadSettingsIntoUI();
            GameSettings.ApplyToEngine();
        }

        private void SetupSliderLabel(string sliderName, string labelName)
        {
            var slider = _root.Q<Slider>(sliderName);
            var label = _root.Q<Label>(labelName);
            if (slider == null || label == null) return;
            slider.RegisterValueChangedCallback(evt => { label.text = $"{Mathf.RoundToInt(evt.newValue)}%"; });
        }

        private void SetupKeybindButtons()
        {
            foreach (GameSettings.InputAction action in System.Enum.GetValues(typeof(GameSettings.InputAction)))
            {
                var btn = _root.Q<Button>($"key-{action}");
                if (btn == null) continue;
                var captured = action;
                btn.text = PrettyKey(GameSettings.GetKey(captured));
                btn.RegisterCallback<PointerEnterEvent>(evt => PlayHoverSound());
                btn.RegisterCallback<ClickEvent>(evt => BeginRebind(btn, captured));
            }
        }

        private void BeginRebind(Button btn, GameSettings.InputAction action)
        {
            PlayClickSound();
            btn.text = "PRESS KEY…";
            btn.AddToClassList("rebinding");
            btn.focusable = true;
            btn.Focus();
            EventCallback<KeyDownEvent> handler = null;
            handler = keyEvt =>
            {
                if (keyEvt.keyCode == KeyCode.None) return;
                GameSettings.SetKey(action, keyEvt.keyCode);
                GameSettings.Save();
                btn.text = PrettyKey(keyEvt.keyCode);
                btn.RemoveFromClassList("rebinding");
                btn.UnregisterCallback(handler);
                keyEvt.StopPropagation();
            };
            btn.RegisterCallback(handler);
        }

        private static string PrettyKey(KeyCode k)
        {
            switch (k)
            {
                case KeyCode.Space: return "SPACE";
                case KeyCode.LeftShift: case KeyCode.RightShift: return "SHIFT";
                case KeyCode.Tab: return "TAB";
                default: return k.ToString().ToUpper();
            }
        }

        private void SwitchSettingsTab(string tabName)
        {
            _activeSettingsTab = tabName;
            foreach (var name in _settingsTabNames)
            {
                var tabBtn = _root.Q<Button>($"tab-{name}");
                if (tabBtn != null)
                {
                    if (name == tabName) tabBtn.AddToClassList("active");
                    else tabBtn.RemoveFromClassList("active");
                }
                var content = _root.Q<VisualElement>($"settings-tab-{name}");
                if (content != null)
                    content.style.display = (name == tabName) ? DisplayStyle.Flex : DisplayStyle.None;
            }
            var headerTitle = _root.Q<Label>("settings-content-title");
            if (headerTitle != null) headerTitle.text = tabName.ToUpper();
        }

        private void ResetSettingsToDefault()
        {
            GameSettings.ResetToDefault();
            GameSettings.Save();
            LoadSettingsIntoUI();
            SetDropdownIndex("dropdown-resolution", 1);
            SetDropdownIndex("dropdown-fullscreen", 1);
            SetDropdownIndex("dropdown-framelimit", 3);
            SetDropdownIndex("dropdown-shadows", 3);
            GameSettings.ApplyToEngine();
            ApplyGraphicsFromUI();
            SetupKeybindButtons();
        }

        /// <summary>Đổ giá trị đã lưu (PlayerPrefs) lên các control trong UI.</summary>
        private void LoadSettingsIntoUI()
        {
            GameSettings.EnsureLoaded();
            SetSliderValue("slider-master", GameSettings.MasterVolume * 100f, "label-master-val");
            SetSliderValue("slider-music", GameSettings.MusicVolume * 100f, "label-music-val");
            SetSliderValue("slider-sfx", GameSettings.SfxVolume * 100f, "label-sfx-val");
            SetSliderValue("slider-ambient", GameSettings.AmbientVolume * 100f, "label-ambient-val");
            SetSliderValue("slider-voice", GameSettings.VoiceVolume * 100f, "label-voice-val");
            SetToggleValue("toggle-dmg-numbers", GameSettings.ShowDamageNumbers);
            SetToggleValue("toggle-cam-shake", GameSettings.CameraShake);
            SetToggleValue("toggle-vsync", GameSettings.VSync);
            SetToggleValue("toggle-post-processing", GameSettings.PostProcessing);
        }

        private void ApplySettings()
        {
            float V(string n) { var s = _root.Q<Slider>(n); return s != null ? s.value / 100f : 0f; }
            bool T(string n) { var t = _root.Q<Toggle>(n); return t != null && t.value; }

            GameSettings.SetAudio(V("slider-master"), V("slider-music"), V("slider-sfx"), V("slider-ambient"), V("slider-voice"));
            GameSettings.SetToggles(T("toggle-dmg-numbers"), T("toggle-cam-shake"), T("toggle-vsync"), T("toggle-post-processing"));
            GameSettings.Save();
            GameSettings.ApplyToEngine();
            ApplyGraphicsFromUI();
        }

        /// <summary>Áp thiết lập đồ hoạ không lưu trong GameSettings: độ phân giải, fps, shadow, fullscreen.</summary>
        private void ApplyGraphicsFromUI()
        {
            var fs = _root.Q<DropdownField>("dropdown-fullscreen");
            if (fs != null)
                Screen.fullScreenMode = fs.index switch
                {
                    0 => FullScreenMode.ExclusiveFullScreen,
                    2 => FullScreenMode.Windowed,
                    _ => FullScreenMode.FullScreenWindow,
                };

            var resDropdown = _root.Q<DropdownField>("dropdown-resolution");
            if (resDropdown != null && resDropdown.index >= 0)
            {
                int[][] resolutions = { new[]{1280,720}, new[]{1920,1080}, new[]{2560,1440}, new[]{3840,2160} };
                if (resDropdown.index < resolutions.Length)
                {
                    var res = resolutions[resDropdown.index];
                    Screen.SetResolution(res[0], res[1], Screen.fullScreenMode);
                }
            }

            var fps = _root.Q<DropdownField>("dropdown-framelimit");
            if (fps != null)
                Application.targetFrameRate = fps.index switch { 0 => 30, 1 => 60, 2 => 120, 3 => 144, _ => -1 };

            var shadows = _root.Q<DropdownField>("dropdown-shadows");
            if (shadows != null)
            {
                QualitySettings.shadows = shadows.index <= 0 ? ShadowQuality.Disable : ShadowQuality.All;
                QualitySettings.shadowResolution = (ShadowResolution)Mathf.Clamp(shadows.index, 0, 3);
            }
        }

        private void SetSliderValue(string sliderName, float value, string labelName)
        {
            var slider = _root.Q<Slider>(sliderName);
            if (slider != null) slider.value = value;
            var label = _root.Q<Label>(labelName);
            if (label != null) label.text = $"{Mathf.RoundToInt(value)}%";
        }

        private void SetToggleValue(string toggleName, bool value)
        {
            var toggle = _root.Q<Toggle>(toggleName);
            if (toggle != null) toggle.value = value;
        }

        private void SetDropdownIndex(string dropdownName, int index)
        {
            var dropdown = _root.Q<DropdownField>(dropdownName);
            if (dropdown != null) dropdown.index = index;
        }

        private void PlayClickSound() { if (_audioSource != null && _clickSound != null) _audioSource.PlayOneShot(_clickSound); }
        private void PlayHoverSound() { if (_audioSource != null && _hoverSound != null) _audioSource.PlayOneShot(_hoverSound); }

        private void OnButtonHover(Button btn)
        {
            PlayHoverSound();
            if (_particleCoroutines.ContainsKey(btn)) StopCoroutine(_particleCoroutines[btn]);
            _particleCoroutines[btn] = StartCoroutine(AnimateParticles(btn));
        }

        private void OnButtonLeave(Button btn)
        {
            if (_particleCoroutines.ContainsKey(btn))
            {
                StopCoroutine(_particleCoroutines[btn]);
                _particleCoroutines.Remove(btn);
            }
            var container = btn.Q<VisualElement>(className: "particle-container");
            if (container != null)
            {
                foreach (var child in container.Children())
                {
                    child.style.opacity = 0f;
                    child.style.translate = new StyleTranslate(new Translate(0, 0, 0));
                }
            }
        }

        private IEnumerator AnimateParticles(Button btn)
        {
            var container = btn.Q<VisualElement>(className: "particle-container");
            if (container == null || container.childCount < 3) yield break;

            var particles = new List<VisualElement>();
            foreach (var child in container.Children()) particles.Add(child);

            float duration = 1.0f;
            float[] startTimes = { Time.time, Time.time + 0.15f, Time.time + 0.3f };

            while (true)
            {
                float currentTime = Time.time;
                for (int i = 0; i < 3; i++)
                {
                    if (currentTime >= startTimes[i])
                    {
                        float t = (currentTime - startTimes[i]) % duration / duration;
                        float opacity = t < 0.3f ? Mathf.Lerp(0f, 1f, t / 0.3f) : Mathf.Lerp(1f, 0f, (t - 0.3f) / 0.7f);
                        float x = Mathf.Lerp(0, 10f + i * 8f, t);
                        float y = Mathf.Lerp(0, -15f - i * 5f, t);
                        float wobble = Mathf.Sin(t * Mathf.PI * 4f) * 2f;

                        particles[i].style.opacity = opacity;
                        particles[i].style.translate = new StyleTranslate(
                            new Translate(new Length(x, LengthUnit.Pixel), new Length(y + wobble, LengthUnit.Pixel), 0));
                    }
                }
                yield return null;
            }
        }
    }
}
