using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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
        private VisualElement _coopLobbyScreen;
        private VisualElement _settingsScreen;

        // ===== State =====
        private string _currentScreen = "main-menu";
        private string _previousScreen = "main-menu"; 
        private int _selectedSaveSlot = 0;
        private bool _isCoopReady = false;
        private bool _isLoggedIn = false;
        private string _currentUserId = null;
        private string _currentRoomCode = null;
        private bool _isHost = false;
        private bool _isOnlineMode = false;

        private SaveSlotData[] _saveSlots;
        private string[] _characterIds = new string[3];

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

            if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();

            _mainMenuScreen = _root.Q<VisualElement>("main-menu-screen");
            _saveSelectionScreen = _root.Q<VisualElement>("save-selection-screen");
            _loginScreen = _root.Q<VisualElement>("login-screen");
            _hostJoinScreen = _root.Q<VisualElement>("host-join-screen");
            _coopLobbyScreen = _root.Q<VisualElement>("coop-lobby-screen");
            _settingsScreen = _root.Q<VisualElement>("settings-screen");

            SetupMainMenu();
            SetupSaveSelection();
            SetupLogin();
            SetupHostJoin();
            SetupCoopLobby();
            SetupSettings();

            SaveManager.CreateMockDataIfNeeded();
            LoadSavesFromDisk();

            ShowScreen("main-menu");
        }

        private void ShowScreen(string screenName)
        {
            _previousScreen = _currentScreen;
            _currentScreen = screenName;

            SetScreenVisible(_mainMenuScreen, screenName == "main-menu");
            SetScreenVisible(_saveSelectionScreen, screenName == "save-selection");
            SetScreenVisible(_loginScreen, screenName == "login");
            SetScreenVisible(_hostJoinScreen, screenName == "host-join");
            SetScreenVisible(_coopLobbyScreen, screenName == "coop-lobby");
            SetScreenVisible(_settingsScreen, screenName == "settings");
        }

        private void SetScreenVisible(VisualElement screen, bool visible)
        {
            if (screen != null)
                screen.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
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
                            ShowScreen("save-selection");
                            break;
                        case "btn-coop-mode":
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
            if (_saveSlots == null) _saveSlots = new SaveSlotData[3];
            
            if (_isOnlineMode)
            {
                if (APIManager.Instance != null && !string.IsNullOrEmpty(APIManager.Instance.AccessToken))
                {
                    StartCoroutine(APIManager.Instance.GetCharacters((characters) => {
                        for (int i = 0; i < 3; i++)
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
                    for (int i = 0; i < 3; i++)
                    {
                        _characterIds[i] = null;
                        _saveSlots[i] = null;
                        RenderSaveSlot(i, null);
                    }
                }
            }
            else
            {
                // Offline mode
                var localSaves = SaveManager.LoadAllSlots();
                for (int i = 0; i < 3; i++)
                {
                    _characterIds[i] = null;
                    _saveSlots[i] = localSaves[i];
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

        private void SetupSaveSelection()
        {
            for (int i = 0; i < 3; i++)
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
                    Debug.Log("Create character...");
                });
            }

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

                    if (_isHost)
                    {
                        // Connect fusion as Host
                        StartFusionNetwork(GameMode.Host, _currentRoomCode);
                        UpdateLobbyRoomCode(_currentRoomCode);
                        SetupLobbyHostView();
                        ShowScreen("coop-lobby");
                    }
                    else
                    {
                        // Solo play
                        Debug.Log("Starting Solo game...");
                        // SceneManager.LoadScene("GameScene");
                    }
                });
            }

            SelectSaveSlot(0);
        }

        private void SelectSaveSlot(int index)
        {
            _selectedSaveSlot = index;

            for (int i = 0; i < 3; i++)
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
                    // Auto generate 4 char room code
                    _currentRoomCode = GenerateRoomCode();
                    
                    UpdateSaveTitle("SELECT SAVE DATA (HOST)");
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
                    _currentRoomCode = code;

                    // Client joins directly to lobby and waits
                    StartFusionNetwork(GameMode.Client, _currentRoomCode);
                    UpdateLobbyRoomCode(_currentRoomCode);
                    SetupLobbyClientView();
                    ShowScreen("coop-lobby");
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

            var logoutBtn = _root.Q<Button>("btn-logout");
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
                    ShowScreen("login");
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

        private void StartFusionNetwork(GameMode mode, string code)
        {
            NetworkSpawner spawner = FindObjectOfType<NetworkSpawner>();
            if (spawner != null)
            {
                spawner.StartCoopSession(mode, code);
            }
            else
            {
                Debug.LogError("NetworkSpawner not found in scene! Cannot start Fusion.");
            }
        }

        // =================================================================
        // CO-OP LOBBY
        // =================================================================
        private void SetupCoopLobby()
        {
            _isCoopReady = false;

            var readyBtn = _root.Q<Button>("btn-coop-ready");
            if (readyBtn != null)
            {
                readyBtn.RegisterCallback<PointerEnterEvent>(evt => PlayHoverSound());
                readyBtn.RegisterCallback<ClickEvent>(evt =>
                {
                    PlayClickSound();
                    _isCoopReady = !_isCoopReady;
                    UpdateReadyState(readyBtn);
                });
            }

            var backBtn = _root.Q<Button>("btn-coop-back");
            if (backBtn != null)
            {
                backBtn.RegisterCallback<PointerEnterEvent>(evt => PlayHoverSound());
                backBtn.RegisterCallback<ClickEvent>(evt =>
                {
                    PlayClickSound();
                    // NetworkRunner.Shutdown() needed if going back
                    NetworkRunner runner = FindObjectOfType<NetworkRunner>();
                    if (runner != null) runner.Shutdown();

                    ShowScreen("host-join");
                });
            }

            var startBtn = _root.Q<Button>("btn-coop-start");
            if (startBtn != null)
            {
                startBtn.RegisterCallback<PointerEnterEvent>(evt => PlayHoverSound());
                startBtn.RegisterCallback<ClickEvent>(evt =>
                {
                    if (!_isCoopReady) return;
                    PlayClickSound();
                    Debug.Log("Starting Co-op Game...");
                });
            }
        }

        private void SetupLobbyHostView()
        {
            var startBtn = _root.Q<Button>("btn-coop-start");
            if (startBtn != null) startBtn.style.display = DisplayStyle.Flex; // Host can start
            
            var readyBtn = _root.Q<Button>("btn-coop-ready");
            if (readyBtn != null) readyBtn.style.display = DisplayStyle.None; // Host is always ready
            
            // Auto ready for host
            _isCoopReady = true;
            if (startBtn != null) startBtn.RemoveFromClassList("coop-start-disabled");

            var clientCard = _root.Q<VisualElement>("coop-card-client");
            if (clientCard != null) clientCard.style.opacity = 0.5f; // Wait for client
        }

        private void SetupLobbyClientView()
        {
            var startBtn = _root.Q<Button>("btn-coop-start");
            if (startBtn != null) startBtn.style.display = DisplayStyle.None; // Client cannot start
            
            var readyBtn = _root.Q<Button>("btn-coop-ready");
            if (readyBtn != null) readyBtn.style.display = DisplayStyle.Flex; 

            _isCoopReady = false;
            UpdateReadyState(readyBtn);
        }

        private void UpdateLobbyRoomCode(string code)
        {
            var roomId = _root.Q<Label>("coop-room-id");
            if (roomId != null) roomId.text = $"● ROOM ID: {code}";
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
            var keybindBtns = _root.Query<Button>(className: "keybind-btn").ToList();
            foreach (var btn in keybindBtns)
            {
                btn.RegisterCallback<PointerEnterEvent>(evt => PlayHoverSound());
                btn.RegisterCallback<ClickEvent>(evt =>
                {
                    PlayClickSound();
                    btn.text = "PRESS KEY…";
                    btn.AddToClassList("rebinding");
                    btn.RegisterCallback<KeyDownEvent>(keyEvt =>
                    {
                        btn.text = keyEvt.keyCode.ToString().ToUpper();
                        btn.RemoveFromClassList("rebinding");
                        keyEvt.StopPropagation();
                    });
                });
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
            SetSliderValue("slider-master", 80, "label-master-val");
            SetSliderValue("slider-music", 65, "label-music-val");
            SetSliderValue("slider-sfx", 100, "label-sfx-val");
            SetSliderValue("slider-ambient", 70, "label-ambient-val");
            SetSliderValue("slider-voice", 90, "label-voice-val");
            SetToggleValue("toggle-autolock", true);
            SetToggleValue("toggle-dmg-numbers", true);
            SetToggleValue("toggle-cam-shake", false);
            SetToggleValue("toggle-vsync", true);
            SetToggleValue("toggle-post-processing", true);
            SetDropdownIndex("dropdown-difficulty", 2);
            SetDropdownIndex("dropdown-resolution", 1);
            SetDropdownIndex("dropdown-fullscreen", 1);
            SetDropdownIndex("dropdown-framelimit", 3);
            SetDropdownIndex("dropdown-shadows", 3);
        }

        private void ApplySettings()
        {
            var masterSlider = _root.Q<Slider>("slider-master");
            if (masterSlider != null) AudioListener.volume = masterSlider.value / 100f;

            var vsyncToggle = _root.Q<Toggle>("toggle-vsync");
            if (vsyncToggle != null) QualitySettings.vSyncCount = vsyncToggle.value ? 1 : 0;

            var fullscreenDropdown = _root.Q<DropdownField>("dropdown-fullscreen");
            if (fullscreenDropdown != null)
            {
                switch (fullscreenDropdown.index)
                {
                    case 0: Screen.fullScreenMode = FullScreenMode.ExclusiveFullScreen; break;
                    case 1: Screen.fullScreenMode = FullScreenMode.FullScreenWindow; break;
                    case 2: Screen.fullScreenMode = FullScreenMode.Windowed; break;
                }
            }

            var resDropdown = _root.Q<DropdownField>("dropdown-resolution");
            if (resDropdown != null)
            {
                int[][] resolutions = { new[]{1280,720}, new[]{1920,1080}, new[]{2560,1440}, new[]{3840,2160} };
                if (resDropdown.index >= 0 && resDropdown.index < resolutions.Length)
                {
                    var res = resolutions[resDropdown.index];
                    Screen.SetResolution(res[0], res[1], Screen.fullScreenMode);
                }
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
