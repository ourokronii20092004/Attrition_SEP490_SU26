using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Attrition.Core;
using Attrition.Data;
using Attrition.Gameplay.Player;
using Attrition.Gameplay.Player.Inventory;

namespace Attrition.UI
{
    /// <summary>
    /// Bộ điều khiển TOÀN BỘ UI trong trận bằng UIToolkit (1 UIDocument, 1 GameUI.uxml).
    /// Gồm 5 màn: HUD, Character/Inventory (Tab), Fast Travel, Game Over, Loading.
    ///
    /// Bind dữ liệu THẬT: PlayerStats (HP/Mana/Stamina/AD/AP/DEF/RES/Level),
    /// PotionSystem (số bình), PlayerInventory (grid + equip), PlayerProgression (EXP).
    /// Tự tìm local player (HasInputAuthority).
    ///
    /// Overlay (Inventory/FastTravel/GameOver/Loading) chỉ hiện 1 lúc; HUD ẩn khi có overlay.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public partial class GameUIController : MonoBehaviour
    {
        [Header("---- HUD ICONS (gán trong Inspector) ----")]
        [Tooltip("Icon bình máu hiện ở HUD (Art/UI_Elements/16x16/hp potion).")]
        [SerializeField] private Sprite healthFlaskIcon;
        [Tooltip("Icon bình mana hiện ở HUD (Art/UI_Elements/16x16/mana potion).")]
        [SerializeField] private Sprite manaFlaskIcon;

        private UIDocument _doc;
        private VisualElement _root;

        // screens
        private VisualElement _hud, _invScreen, _ftScreen, _goScreen, _loading, _pauseScreen, _settingsScreen, _waitingScreen;

        // bound player components
        private PlayerStats _stats;
        private PotionSystem _potions;
        private PlayerInventory _inventory;
        private PlayerProgression _progression;
        private PlayerController _controller;
        private CoopReviveSystem _revive;

        /// <summary>
        /// True khi đã tìm thấy local player VÀ tất cả component đã Spawned (Object.IsValid).
        /// Trước cờ này bật, KHÔNG được đọc bất kỳ [Networked] property nào (sẽ ném
        /// InvalidOperationException). Pattern chuyên nghiệp: UI chờ event Spawned thay
        /// vì try-catch / null-check mỗi frame.
        /// </summary>
        private bool _isBound;

        private ItemDatabaseSO _db;

        private enum Overlay { None, Inventory, FastTravel, GameOver, Loading, Pause, Settings }
        private Overlay _overlay = Overlay.None;

        private float _runStartTime;

        private void OnEnable()
        {
            _doc = GetComponent<UIDocument>();
            _root = _doc.rootVisualElement;
            if (_root == null) return;

            _hud = _root.Q<VisualElement>("hud");
            _invScreen = _root.Q<VisualElement>("inv-screen");
            _ftScreen = _root.Q<VisualElement>("ft-screen");
            _goScreen = _root.Q<VisualElement>("go-screen");
            _loading = _root.Q<VisualElement>("loading");
            _pauseScreen = _root.Q<VisualElement>("pause-screen");
            _settingsScreen = _root.Q<VisualElement>("settings-screen");
            _waitingScreen = _root.Q<VisualElement>("waiting-screen");

            _db = ItemDatabaseSO.Instance;
            _runStartTime = Time.time;

            BuildInventoryGrid();
            SetupInventoryControls();
            SetupFastTravelControls();
            SetupGameOverControls();
            SetupPauseControls();
            SetupSettingsControls();

            // Hiện màn loading tới khi player của máy này spawn xong (mỗi máy tự chờ).
            ShowOverlay(Overlay.Loading);

            // Boss bar qua event bus (Gameplay không ref UI).
            Attrition.Controllers.BossEvents.OnBossSpawned += ShowBossBar;
            Attrition.Controllers.BossEvents.OnBossHpChanged += UpdateBossBar;
            Attrition.Controllers.BossEvents.OnBossDespawned += HideBossBar;

            HookSaveToast();
            Attrition.Controllers.CoopFeedbackEvents.OnTravelLoading += OnCoopTravelLoading;
        }

        private void OnDisable()
        {
            Time.timeScale = 1f; // tránh để MainMenu bị đứng hình nếu quit lúc đang pause (solo)
            Attrition.Persistence.GamePause.IsPaused = false;
            Attrition.Persistence.CoopSession.Reset();
            if (_stats != null)
            {
                _stats.OnStatsChanged -= RefreshCharacterPanel;
                _stats.OnStatsChanged -= RefreshAllocPoints;
            }
            if (_inventory != null) _inventory.OnInventoryChanged -= RefreshInventory;
            _isBound = false;

            Attrition.Controllers.BossEvents.OnBossSpawned -= ShowBossBar;
            Attrition.Controllers.BossEvents.OnBossHpChanged -= UpdateBossBar;
            Attrition.Controllers.BossEvents.OnBossDespawned -= HideBossBar;

            UnhookSaveToast();
            Attrition.Controllers.CoopFeedbackEvents.OnTravelLoading -= OnCoopTravelLoading;
        }

        private void Update()
        {
            // Bound player có thể despawn (respawn / đổi scene) → object cũ invalid. Huỷ bind để
            // TryBindLocalPlayer gắn lại vào player mới (tránh đọc Networked trên object đã chết).
            if (_isBound && (_controller == null || _controller.Object == null || !_controller.Object.IsValid))
            {
                // Gỡ handler khỏi object cũ trước khi rebind (tránh double-subscribe / leak).
                if (_stats != null)
                {
                    _stats.OnStatsChanged -= RefreshCharacterPanel;
                    _stats.OnStatsChanged -= RefreshAllocPoints;
                }
                if (_inventory != null) _inventory.OnInventoryChanged -= RefreshInventory;
                _isBound = false;
            }

            // Không polling thô: chỉ tìm player cho đến khi bind thành công, sau đó tin vào sự kiện.
            if (!_isBound) TryBindLocalPlayer();
            if (_isBound) UpdateHud();

            // FPS chạy mỗi frame (kể cả chưa bind player) để đo cả lúc loading — dùng chẩn giật.
            UpdateFps();

            UpdateLoadingWarmup();
            UpdateWaitingOverlay();

            CheckGameOver();

            // Tab = mở/đóng Character/Inventory (không khi đang Game Over/Loading)
            if (Input.GetKeyDown(KeyCode.Tab) && _overlay != Overlay.GameOver && _overlay != Overlay.Loading)
                ToggleOverlay(Overlay.Inventory);

            // F (Interact) = mở UI checkpoint (Rest + Fast Travel) khi đang đứng trong vùng checkpoint.
            // NPC ưu tiên hơn checkpoint — DialogueUI tự xử lý NPC khi player đứng gần.
            if (Input.GetKeyDown(Attrition.Persistence.GameSettings.GetKey(Attrition.Persistence.GameSettings.InputAction.Interact))
                && _overlay != Overlay.GameOver && _overlay != Overlay.Loading
                && !Attrition.Persistence.DialogueState.IsActive)
            {
                if (_overlay == Overlay.FastTravel) ShowOverlay(Overlay.None);
                else if (_controller != null && _controller.IsNearNPC) { /* NPC ưu tiên — DialogueUI xử lý */ }
                else if (_controller != null && _controller.IsAtCheckpoint)
                {
                    // Mở bảng checkpoint = kích hoạt beacon + LƯU tiến trình (save không cần out-of-combat).
                    _controller.ActivateAndSaveCheckpoint();
                    ShowOverlay(Overlay.FastTravel);
                }
            }

            // ESC = menu tạm dừng, hoặc lùi/đóng overlay đang mở.
            if (Input.GetKeyDown(KeyCode.Escape) && _overlay != Overlay.GameOver && _overlay != Overlay.Loading)
            {
                if (_overlay == Overlay.Settings) ShowOverlay(Overlay.Pause);          // Settings → lùi về Pause
                else if (_overlay == Overlay.Inventory || _overlay == Overlay.FastTravel) ShowOverlay(Overlay.None);
                else ToggleOverlay(Overlay.Pause);                                      // None ↔ Pause
            }
        }

        private bool _waitingShown;

        private void UpdateWaitingOverlay()
        {
            // Chỉ hiện overlay Waiting khi KHÔNG có overlay nào đang mở. Nếu host mở Pause (ESC) để
            // Quit, ẩn Waiting đi — nếu không Waiting render đè lên Pause, host bấm ESC mà không thao
            // tác được nút nào. Waiting tự hiện lại khi host đóng Pause (vẫn còn chờ client).
            bool waiting = Attrition.Persistence.CoopSession.WaitingForPlayer && _overlay == Overlay.None;
            if (waiting == _waitingShown) return;
            _waitingShown = waiting;

            if (_waitingScreen != null)
            {
                if (waiting) _waitingScreen.RemoveFromClassList("hidden");
                else _waitingScreen.AddToClassList("hidden");
            }

            var title = _root?.Q<Label>("waiting-title");
            if (title != null) title.text = Attrition.Persistence.CoopSession.WaitingMessage;

            UnityEngine.Cursor.visible = waiting || _overlay != Overlay.None;
        }

        private void TryBindLocalPlayer()
        {
            var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            foreach (var p in players)
            {
                // Chờ cho Object đã được Fusion xử lý hoàn tất (HasInputAuthority + IsValid).
                // Nếu chưa IsValid thì Networked properties sẽ ném exception → bỏ qua frame này.
                if (p.Object == null || !p.Object.HasInputAuthority || !p.Object.IsValid) continue;

                // Kiểm tra mọi component quan trọng cũng đã sẵn sàng.
                var stats = p.GetComponent<PlayerStats>();
                var inv = p.GetComponent<PlayerInventory>();
                if (stats == null || stats.Object == null || !stats.Object.IsValid) continue;
                if (inv != null && (inv.Object == null || !inv.Object.IsValid)) continue;

                _controller = p;
                _stats = stats;
                _potions = p.GetComponent<PotionSystem>();
                _inventory = inv;
                _progression = p.GetComponent<PlayerProgression>();
                _revive = p.GetComponent<CoopReviveSystem>();

                _stats.OnStatsChanged += RefreshCharacterPanel;
                _stats.OnStatsChanged += RefreshAllocPoints;
                if (_inventory != null) _inventory.OnInventoryChanged += RefreshInventory;

                _isBound = true;

                // An toàn gọi lần đầu vì đã xác nhận IsValid.
                RefreshCharacterPanel();
                RefreshInventory();

                // Player bind xong NHƯNG tài nguyên khác (enemy networked, vật thể scene) có thể chưa
                // sync/khởi tạo xong trên client → ẩn loading ngay sẽ thấy lag/giật + quái pop-in.
                // Bắt đầu warmup: ẩn loading sau khi qua giai đoạn này (xem UpdateLoadingWarmup).
                _bindTime = Time.unscaledTime;
                break;
            }
        }

        // Loading warmup: giữ màn loading thêm sau khi bind để client kịp sync tài nguyên + frame ổn
        // định, tránh lag/pop-in lúc vừa vào. Solo cũng qua warmup nhưng rất nhanh (đã load tại chỗ).
        private float _bindTime = -1f;
        private bool _loadingHidden;
        [Tooltip("Số giây giữ màn loading sau khi player bind, để client sync xong tài nguyên.")]
        [SerializeField] private float loadingWarmupSeconds = 1.5f;

        private void UpdateLoadingWarmup()
        {
            if (_loadingHidden || !_isBound || _bindTime < 0f) return;
            if (_overlay != Overlay.Loading) { _loadingHidden = true; return; }

            // Chờ đủ warmup VÀ (coop) quái đã spawn trên máy này — dấu hiệu scene gameplay đã sync.
            bool warmedUp = Time.unscaledTime - _bindTime >= loadingWarmupSeconds;
            bool enemiesReady = FindObjectsByType<Attrition.Controllers.EnemyController>(FindObjectsSortMode.None).Length > 0
                                || Attrition.Persistence.GameLaunch.Mode == Attrition.Persistence.LaunchMode.Solo;

            if (warmedUp && enemiesReady)
            {
                ShowOverlay(Overlay.None);
                _loadingHidden = true;
            }
        }
    }
}
