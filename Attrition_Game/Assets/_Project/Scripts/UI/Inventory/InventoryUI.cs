using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Attrition.Data;
using Attrition.Gameplay.Player;
using Attrition.Gameplay.Player.Inventory;

namespace Attrition.UI.Inventory
{
    /// <summary>
    /// Panel chính Inventory. Gắn trên Canvas, mở/đóng bằng Tab.
    /// 3 tab: Item (Equipment+Material) | Accessory | Skill.
    /// Hiển thị grid ô + equip slots + player stats + detail panel bên phải.
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        [Header("---- PANEL ----")]
        [SerializeField] private GameObject inventoryPanel;

        [Header("---- TABS ----")]
        [SerializeField] private Button tabItem;
        [SerializeField] private Button tabAccessory;
        [SerializeField] private Button tabSkill;

        [Header("---- GRIDS ----")]
        [SerializeField] private Transform gridItemContent;
        [SerializeField] private Transform gridAccessoryContent;
        [SerializeField] private Transform gridSkillContent;
        [SerializeField] private GameObject[] tabPanels; // 0=Item, 1=Accessory, 2=Skill

        [Header("---- PREFAB ----")]
        [SerializeField] private GameObject slotPrefab; // InventorySlotUI prefab

        [Header("---- EQUIP SLOTS ----")]
        [SerializeField] private EquipSlotUI equipHead;
        [SerializeField] private EquipSlotUI equipChest;
        [SerializeField] private EquipSlotUI equipLegs;
        [SerializeField] private EquipSlotUI equipBoots;
        [SerializeField] private EquipSlotUI equipSkill;
        [SerializeField] private EquipSlotUI equipAccessory;

        [Header("---- DETAIL PANEL ----")]
        [SerializeField] private ItemDetailPanel detailPanel;

        [Header("---- STATS DISPLAY ----")]
        [SerializeField] private TextMeshProUGUI statsText;

        private PlayerInventory _inventory;
        private PlayerStats _stats;
        private bool _isOpen;
        private int _currentTab;

        private InventorySlotUI[] _itemSlots;
        private InventorySlotUI[] _accessorySlots;

        private void Start()
        {
            if (inventoryPanel != null) inventoryPanel.SetActive(false);

            if (tabItem != null) tabItem.onClick.AddListener(() => SwitchTab(0));
            if (tabAccessory != null) tabAccessory.onClick.AddListener(() => SwitchTab(1));
            if (tabSkill != null) tabSkill.onClick.AddListener(() => SwitchTab(2));
        }

        private void Update()
        {
            if (_inventory == null)
            {
                var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
                foreach (var p in players)
                {
                    if (p.Object != null && p.Object.HasInputAuthority)
                    {
                        var inv = p.GetComponent<Attrition.Gameplay.Player.Inventory.PlayerInventory>();
                        var stats = p.GetComponent<Attrition.Gameplay.Player.PlayerStats>();
                        BindToPlayer(inv, stats);
                        break;
                    }
                }
            }

            // Mở/đóng bằng Tab (local only, không qua network)
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                ToggleInventory();
            }
        }

        /// <summary>Gọi khi local player spawn — bind vào PlayerInventory.</summary>
        public void BindToPlayer(PlayerInventory inventory, PlayerStats stats)
        {
            _inventory = inventory;
            _stats = stats;

            if (_inventory != null)
                _inventory.OnInventoryChanged += RefreshAll;

            InitSlots();
            RefreshAll();
        }

        public void ToggleInventory()
        {
            _isOpen = !_isOpen;
            if (inventoryPanel != null) inventoryPanel.SetActive(_isOpen);
            if (_isOpen) RefreshAll();

            // Hiện/ẩn cursor
            Cursor.visible = _isOpen;
            Cursor.lockState = _isOpen ? CursorLockMode.None : CursorLockMode.Locked;
        }

        public bool IsOpen => _isOpen;


        private void SwitchTab(int tab)
        {
            _currentTab = tab;
            for (int i = 0; i < tabPanels.Length; i++)
            {
                if (tabPanels[i] != null) tabPanels[i].SetActive(i == tab);
            }
        }


        private void InitSlots()
        {
            if (slotPrefab == null) return;

            // Item tab: 40 equipment + 14 material = 54 slots trong 1 grid
            if (gridItemContent != null)
            {
                int total = 40 + 14;
                _itemSlots = new InventorySlotUI[total];
                for (int i = 0; i < total; i++)
                {
                    var go = Instantiate(slotPrefab, gridItemContent);
                    var slotUI = go.GetComponent<InventorySlotUI>();
                    _itemSlots[i] = slotUI;
                    if (slotUI != null)
                    {
                        bool isMaterial = i >= 40;
                        int realIndex = isMaterial ? i - 40 : i;
                        var cat = isMaterial ? ItemCategory.Material : ItemCategory.Equipment;
                        slotUI.Setup(this, _inventory, cat, realIndex);
                    }
                }
            }

            // Accessory tab
            if (gridAccessoryContent != null)
            {
                _accessorySlots = new InventorySlotUI[10];
                for (int i = 0; i < 10; i++)
                {
                    var go = Instantiate(slotPrefab, gridAccessoryContent);
                    var slotUI = go.GetComponent<InventorySlotUI>();
                    _accessorySlots[i] = slotUI;
                    if (slotUI != null)
                        slotUI.Setup(this, _inventory, ItemCategory.Accessory, i);
                }
            }

            // Skill tab: reuse equipment slots that contain SkillSO
            // Skills share EquipmentSlots array — UI filters by SO type

            // Equip slots
            if (equipHead != null) equipHead.Setup(this, _inventory, EquipmentSlot.Head);
            if (equipChest != null) equipChest.Setup(this, _inventory, EquipmentSlot.Chest);
            if (equipLegs != null) equipLegs.Setup(this, _inventory, EquipmentSlot.Legs);
            if (equipBoots != null) equipBoots.Setup(this, _inventory, EquipmentSlot.Boots);
            if (equipSkill != null) equipSkill.SetupSkill(this, _inventory);
            if (equipAccessory != null) equipAccessory.SetupAccessory(this, _inventory);
        }


        public void RefreshAll()
        {
            if (_inventory == null) return;

            var db = ItemDatabaseSO.Instance;
            if (db == null) return;

            // Item slots (40 equipment + 14 material)
            if (_itemSlots != null)
            {
                for (int i = 0; i < _itemSlots.Length; i++)
                {
                    if (_itemSlots[i] == null) continue;
                    InventorySlot slot;
                    if (i < 40)
                        slot = _inventory.EquipmentSlots.Get(i);
                    else
                        slot = _inventory.MaterialSlots.Get(i - 40);

                    _itemSlots[i].Refresh(slot, db);
                }
            }

            // Accessory slots
            if (_accessorySlots != null)
            {
                for (int i = 0; i < _accessorySlots.Length; i++)
                {
                    if (_accessorySlots[i] == null) continue;
                    var slot = _inventory.AccessorySlots.Get(i);
                    _accessorySlots[i].Refresh(slot, db);
                }
            }

            // Equip slots
            if (equipHead != null) equipHead.Refresh(_inventory.EquippedHead, db);
            if (equipChest != null) equipChest.Refresh(_inventory.EquippedChest, db);
            if (equipLegs != null) equipLegs.Refresh(_inventory.EquippedLegs, db);
            if (equipBoots != null) equipBoots.Refresh(_inventory.EquippedBoots, db);
            if (equipSkill != null) equipSkill.Refresh(_inventory.EquippedSkill, db);
            if (equipAccessory != null) equipAccessory.Refresh(_inventory.EquippedAccessory, db);

            // Stats text
            RefreshStats();
        }

        private void RefreshStats()
        {
            if (_stats == null || statsText == null) return;
            statsText.text = $"HP: {_stats.CurrentHP}/{_stats.MaxHP}\n" +
                             $"MP: {_stats.CurrentMana}/{_stats.MaxMana}\n" +
                             $"STA: {_stats.CurrentStamina:F0}/{_stats.MaxStamina}\n" +
                             $"AD: {_stats.AD}  AP: {_stats.AP}\n" +
                             $"DEF: {_stats.DEF}  RES: {_stats.RES}\n" +
                             $"Lv: {_stats.Level}";
        }


        public void ShowDetail(InventorySlot slot)
        {
            if (detailPanel != null) detailPanel.Show(slot);
        }

        public void HideDetail()
        {
            if (detailPanel != null) detailPanel.Hide();
        }

        private void OnDestroy()
        {
            if (_inventory != null)
                _inventory.OnInventoryChanged -= RefreshAll;
        }
    }
}
