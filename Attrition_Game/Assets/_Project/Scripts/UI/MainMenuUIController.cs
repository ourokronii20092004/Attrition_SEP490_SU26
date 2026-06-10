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
        private string[] _characterIds = new string[SaveManager.SlotCount];

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
            BuildSaveSlots();
            SetupSaveSelection();
            SetupLogin();
            SetupHostJoin();
            SetupCoopLobby();
            SetupSettings();
            SetupGlobalProfile();

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
                // Offline / solo: chỉ hiện slot thuộc ĐÚNG chế độ đang chọn (BR — solo/coop tách biệt).
                var wantMode = _isHost ? LaunchMode.Coop : LaunchMode.Solo;
                var localSaves = SaveManager.LoadAllSlots();
                for (int i = 0; i < SaveManager.SlotCount; i++)
                {
                    _characterIds[i] = null;
                    var s = localSaves[i];
                    // Save cũ chưa gắn originMode → coi như tương thích; khác chế độ → ẩn (hiện slot trống).
                    bool sameMode = s == null || string.IsNullOrEmpty(s.originMode) || s.originMode == wantMode.ToString();
                    _saveSlots[i] = sameMode ? s : null;
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
                    var wantMode = _isHost ? LaunchMode.Coop : LaunchMode.Solo;
                    if (!SaveManager.IsSlotCompatible(_selectedSaveSlot, wantMode))
                    {
                        var origin = _isHost ? "Solo" : "Co-op";
                        var target = _isHost ? "Co-op" : "Solo";
                        ShowSlotWarning($"This save belongs to {origin} mode and cannot be played in {target}. Pick another slot or create a new character.");
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

            var mode = _isHost ? LaunchMode.Coop : LaunchMode.Solo;
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

            CloseNameEntry();

            if (mode == LaunchMode.Solo)
            {
                // Tạo file save local rồi vào game qua màn loading.
                SaveManager.SaveSlot(slot, newSave);
                _saveSlots[slot] = newSave;
                GameLaunch.Mode = LaunchMode.Solo;
                GameLaunch.SelectedSlot = slot;
                GameLaunch.CharacterName = name;
                StartCoroutine(LoadGameplaySceneAsync(GameLaunch.GameplayScene));
            }
            else
            {
                // Coop host: ghi local làm cache + đánh dấu để tạo nhân vật server, rồi vào lobby.
                SaveManager.SaveSlot(slot, newSave);
                _saveSlots[slot] = newSave;
                GameLaunch.CharacterName = name;
                StartFusionNetwork(GameMode.Host, _currentRoomCode);
                UpdateLobbyRoomCode(_currentRoomCode);
                SetupLobbyHostView();
                ShowScreen("coop-lobby");
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
                    // COOP có mạng: runner đã chạy (Host). Đánh dấu Coop để bootstrap KHÔNG tự start Single.
                    GameLaunch.Mode = LaunchMode.Coop;
                    GameLaunch.SelectedSlot = _selectedSaveSlot;
                    Debug.Log("[MainMenu] Host bắt đầu CO-OP → load scene gameplay cho cả phòng.");

                    // Host điều khiển load scene; Fusion NetworkSceneManager sync client theo.
                    var runner = FindObjectOfType<NetworkRunner>();
                    if (runner != null && runner.IsServer)
                    {
                        int idx = SceneUtility.GetBuildIndexByScenePath($"Assets/_Project/Scenes/{GameLaunch.GameplayScene}.unity");
                        if (idx >= 0) runner.LoadScene(SceneRef.FromIndex(idx));
                        else Debug.LogError($"[MainMenu] Scene '{GameLaunch.GameplayScene}' chưa có trong Build Settings.");
                    }
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
