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
        private VisualElement _hud, _invScreen, _ftScreen, _goScreen, _loading;

        // bound player components
        private PlayerStats _stats;
        private PotionSystem _potions;
        private PlayerInventory _inventory;
        private PlayerProgression _progression;
        private PlayerController _controller;

        private ItemDatabaseSO _db;

        private enum Overlay { None, Inventory, FastTravel, GameOver, Loading }
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

            _db = ItemDatabaseSO.Instance;
            _runStartTime = Time.time;

            BuildInventoryGrid();
            SetupInventoryControls();
            SetupFastTravelControls();
            SetupGameOverControls();

            ShowOverlay(Overlay.None);
        }

        private void OnDisable()
        {
            if (_stats != null) _stats.OnStatsChanged -= RefreshCharacterPanel;
            if (_inventory != null) _inventory.OnInventoryChanged -= RefreshInventory;
        }

        private void Update()
        {
            if (_stats == null) TryBindLocalPlayer();
            if (_stats != null) UpdateHud();

            CheckGameOver();

            // Tab = mở/đóng Character/Inventory (không khi đang Game Over/Loading)
            if (Input.GetKeyDown(KeyCode.Tab) && _overlay != Overlay.GameOver && _overlay != Overlay.Loading)
                ToggleOverlay(Overlay.Inventory);

            // M = Fast Travel (chỉ khi đang ở checkpoint đã kích hoạt — xem SetupFastTravelControls)
            if (Input.GetKeyDown(KeyCode.M) && _overlay != Overlay.GameOver && _overlay != Overlay.Loading)
                ToggleOverlay(Overlay.FastTravel);

            if (Input.GetKeyDown(KeyCode.Escape) && (_overlay == Overlay.Inventory || _overlay == Overlay.FastTravel))
                ShowOverlay(Overlay.None);
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
                break;
            }
        }
    }
}
