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
        }

        private void OnDisable()
        {
            Time.timeScale = 1f; // tránh để MainMenu bị đứng hình nếu quit lúc đang pause (solo)
            Attrition.Persistence.GamePause.IsPaused = false;
            Attrition.Persistence.CoopSession.Reset();
            if (_stats != null) _stats.OnStatsChanged -= RefreshCharacterPanel;
            if (_inventory != null) _inventory.OnInventoryChanged -= RefreshInventory;

            Attrition.Controllers.BossEvents.OnBossSpawned -= ShowBossBar;
            Attrition.Controllers.BossEvents.OnBossHpChanged -= UpdateBossBar;
            Attrition.Controllers.BossEvents.OnBossDespawned -= HideBossBar;
        }

        private void Update()
        {
            if (_stats == null) TryBindLocalPlayer();
            if (_stats != null) UpdateHud();

            UpdateWaitingOverlay();

            CheckGameOver();

            // Tab = mở/đóng Character/Inventory (không khi đang Game Over/Loading)
            if (Input.GetKeyDown(KeyCode.Tab) && _overlay != Overlay.GameOver && _overlay != Overlay.Loading)
                ToggleOverlay(Overlay.Inventory);

            // F (Interact) = mở UI checkpoint (Rest + Fast Travel) khi đang đứng trong vùng checkpoint.
            if (Input.GetKeyDown(Attrition.Persistence.GameSettings.GetKey(Attrition.Persistence.GameSettings.InputAction.Interact))
                && _overlay != Overlay.GameOver && _overlay != Overlay.Loading)
            {
                if (_overlay == Overlay.FastTravel) ShowOverlay(Overlay.None);
                else if (_controller != null && _controller.IsAtCheckpoint) ShowOverlay(Overlay.FastTravel);
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
            bool waiting = Attrition.Persistence.CoopSession.WaitingForPlayer;
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
                if (p.Object == null || !p.Object.HasInputAuthority) continue;
                _controller = p;
                _stats = p.GetComponent<PlayerStats>();
                _potions = p.GetComponent<PotionSystem>();
                _inventory = p.GetComponent<PlayerInventory>();
                _progression = p.GetComponent<PlayerProgression>();

                if (_stats != null) _stats.OnStatsChanged += RefreshCharacterPanel;
                if (_inventory != null) _inventory.OnInventoryChanged += RefreshInventory;

                RefreshCharacterPanel();
                RefreshInventory();

                // Player của máy này đã sẵn sàng → ẩn màn loading.
                if (_overlay == Overlay.Loading) ShowOverlay(Overlay.None);
                break;
            }
        }
    }
}
