using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Attrition.Gameplay.World;
using Attrition.Gameplay.Player;
using Attrition.Gameplay.Environment;
using Attrition.Persistence;

namespace Attrition.UI
{
    /// <summary>
    /// Fast Travel / Checkpoint (Bonfire) System.
    /// Manages Rest, Level Up, Flask Allocation, and Fast Travel.
    /// </summary>
    public partial class GameUIController
    {
        private MapDataSO _ftMap;
        private MapDataSO.CheckpointMarker? _ftSelected;
        
        // Provisional Level Up
        private int _provUnspent;
        private int[] _provStats = new int[6];
        
        // Provisional Flasks
        private int _provHpFlask;
        private int _provManaFlask;
        private int _totalFlasks;

        private void SetupFastTravelControls()
        {
            // Main menu
            BindButton("ft-btn-rest", RestHere);
            BindButton("ft-btn-levelup", OpenLevelUpMenu);
            BindButton("ft-btn-flasks", OpenFlasksMenu);
            BindButton("ft-btn-travel", OpenTravelMenu);
            BindButton("ft-btn-leave", () => ShowOverlay(Overlay.None));
            
            // Level Up Menu
            BindButton("alloc-MaxHP", () => ProvAllocate(0));
            BindButton("alloc-MaxMana", () => ProvAllocate(1));
            BindButton("alloc-AD", () => ProvAllocate(2));
            BindButton("alloc-AP", () => ProvAllocate(3));
            BindButton("alloc-DEF", () => ProvAllocate(4));
            BindButton("alloc-RES", () => ProvAllocate(5));
            BindButton("ft-levelup-apply", ApplyLevelUp);
            BindButton("ft-levelup-back", ShowBonfireMain);

            // Flasks Menu
            BindButton("flask-hp-sub", () => ChangeFlasks(-1, 1));
            BindButton("flask-hp-add", () => ChangeFlasks(1, -1));
            BindButton("flask-mana-sub", () => ChangeFlasks(1, -1));
            BindButton("flask-mana-add", () => ChangeFlasks(-1, 1));
            BindButton("ft-flasks-apply", ApplyFlasks);
            BindButton("ft-flasks-back", ShowBonfireMain);
            
            // Travel Menu
            BindButton("ft-map-prev", () => ChangeFastTravelMap(-1));
            BindButton("ft-map-next", () => ChangeFastTravelMap(1));
            BindButton("ft-go", TeleportToSelected);
            BindButton("ft-travel-back", ShowBonfireMain);
        }
        
        // Called when opening Overlay.FastTravel from PlayerController
        public void OnOpenCheckpointUI()
        {
            ShowBonfireMain();
        }

        private void HideAllBonfireMenus()
        {
            _root.Q<VisualElement>("ft-main-menu")?.AddToClassList("hidden");
            _root.Q<VisualElement>("ft-levelup-menu")?.AddToClassList("hidden");
            _root.Q<VisualElement>("ft-flasks-menu")?.AddToClassList("hidden");
            _root.Q<VisualElement>("ft-travel-menu")?.AddToClassList("hidden");
        }

        private void ShowBonfireMain()
        {
            HideAllBonfireMenus();
            _root.Q<VisualElement>("ft-main-menu")?.RemoveFromClassList("hidden");
        }

        // --- LEVEL UP ---
        private void OpenLevelUpMenu()
        {
            HideAllBonfireMenus();
            _root.Q<VisualElement>("ft-levelup-menu")?.RemoveFromClassList("hidden");
            
            for(int i=0; i<6; i++) _provStats[i] = 0;
            if (_stats != null) _provUnspent = _stats.UnspentPoints;
            RefreshLevelUpUI();
        }

        private void RefreshLevelUpUI()
        {
            SetText("ft-points", $"UNSPENT POINTS: {_provUnspent}");
            if (_stats == null) return;
            
            SetText("lbl-MaxHP", _provStats[0] > 0 ? $"{_stats.MaxHP} + {_provStats[0]}" : $"{_stats.MaxHP}");
            SetText("lbl-MaxMana", _provStats[1] > 0 ? $"{_stats.MaxMana} + {_provStats[1]}" : $"{_stats.MaxMana}");
            SetText("lbl-AD", _provStats[2] > 0 ? $"{_stats.AD} + {_provStats[2]}" : $"{_stats.AD}");
            SetText("lbl-AP", _provStats[3] > 0 ? $"{_stats.AP} + {_provStats[3]}" : $"{_stats.AP}");
            SetText("lbl-DEF", _provStats[4] > 0 ? $"{_stats.DEF} + {_provStats[4]}" : $"{_stats.DEF}");
            SetText("lbl-RES", _provStats[5] > 0 ? $"{_stats.RES} + {_provStats[5]}" : $"{_stats.RES}");
        }

        private void ProvAllocate(int statIndex)
        {
            if (_provUnspent > 0)
            {
                _provUnspent--;
                _provStats[statIndex]++;
                RefreshLevelUpUI();
            }
        }

        private void ApplyLevelUp()
        {
            if (_stats == null) return;
            for (int i = 0; i < 6; i++)
            {
                for (int j = 0; j < _provStats[i]; j++)
                {
                    _stats.RpcRequestAllocate(i);
                }
            }
            ShowBonfireMain();
        }

        // --- FLASKS ---
        private void OpenFlasksMenu()
        {
            HideAllBonfireMenus();
            _root.Q<VisualElement>("ft-flasks-menu")?.RemoveFromClassList("hidden");
            
            if (_controller != null)
            {
                var potionSys = _controller.GetComponent<PotionSystem>();
                if (potionSys != null)
                {
                    _provHpFlask = potionSys.MaxHealthCharges;
                    _provManaFlask = potionSys.MaxManaCharges;
                    _totalFlasks = _provHpFlask + _provManaFlask;
                }
            }
            RefreshFlasksUI();
        }

        private void ChangeFlasks(int hpDelta, int manaDelta)
        {
            if (_provHpFlask + hpDelta >= 0 && _provManaFlask + manaDelta >= 0)
            {
                _provHpFlask += hpDelta;
                _provManaFlask += manaDelta;
                RefreshFlasksUI();
            }
        }

        private void RefreshFlasksUI()
        {
            SetText("ft-flasks-total", $"TOTAL FLASKS: {_totalFlasks}");
            SetText("flask-hp-val", _provHpFlask.ToString());
            SetText("flask-mana-val", _provManaFlask.ToString());
        }

        private void ApplyFlasks()
        {
            if (_controller != null)
            {
                var potionSys = _controller.GetComponent<PotionSystem>();
                if (potionSys != null)
                {
                    potionSys.RpcReallocateFlasks(_provHpFlask, _provManaFlask);
                }
            }
            ShowBonfireMain();
        }

        // --- FAST TRAVEL & REST ---
        private void RestHere()
        {
            if (_controller == null) return;
            // Đóng bảng checkpoint TRƯỚC khi request. Trên HOST, RequestRestAtCheckpoint chạy RPC
            // StateAuthority ĐỒNG BỘ ngay (DoRest → RpcRestTeleportLoading → ShowLoading = mở overlay
            // Loading). Nếu gọi ShowOverlay(None) SAU thì nó đè tắt loading vừa mở → host không thấy
            // loading (client thì RPC tới trễ nên không bị). Đóng trước rồi request → loading giữ nguyên.
            ShowOverlay(Overlay.None);
            _controller.RequestRestAtCheckpoint();
        }

        private void OpenTravelMenu()
        {
            HideAllBonfireMenus();
            _root.Q<VisualElement>("ft-travel-menu")?.RemoveFromClassList("hidden");
            _ftMap = MapRegistrySO.Load()?.GetByScene(GameLaunch.GameplayScene);
            if (_ftMap == null) _ftMap = AvailableFastTravelMaps().FirstOrDefault();
            RefreshFastTravelList();
        }

        private List<MapDataSO> AvailableFastTravelMaps()
        {
            var result = new List<MapDataSO>();
            var registry = MapRegistrySO.Load();
            if (registry == null) return result;

            foreach (var map in registry.maps)
            {
                if (map == null) continue;
                bool hasBeacon = map.checkpoints.Exists(cp => WorldMapState.IsCheckpointDiscovered(cp.checkpointId));
                if (hasBeacon) result.Add(map);
            }
            return result;
        }

        private void ChangeFastTravelMap(int direction)
        {
            var maps = AvailableFastTravelMaps();
            if (maps.Count == 0) return;
            int index = Mathf.Max(0, maps.IndexOf(_ftMap));
            _ftMap = maps[(index + direction + maps.Count) % maps.Count];
            RefreshFastTravelList();
        }

        private void RefreshFastTravelList()
        {
            var list = _root?.Q<ScrollView>("ft-list");
            if (list == null) return;
            list.Clear();
            _ftSelected = null;

            var maps = AvailableFastTravelMaps();
            if (_ftMap == null || !maps.Contains(_ftMap)) _ftMap = maps.FirstOrDefault();

            int discovered = 0;
            int total = _ftMap != null ? _ftMap.checkpoints.Count : 0;
            if (_ftMap != null)
            {
                foreach (var marker in _ftMap.checkpoints)
                {
                    if (!WorldMapState.IsCheckpointDiscovered(marker.checkpointId)) continue;
                    discovered++;
                    var captured = marker;
                    var row = new Button { text = captured.checkpointId };
                    row.AddToClassList("ft-row");
                    row.clicked += () => SelectFtRow(captured, row);
                    list.Add(row);
                }
            }

            SetText("ft-count", $"{discovered} / {total} DISCOVERED");
            SetText("ft-preview-name", discovered > 0 ? "SELECT A BEACON" : "NO BEACONS YET");
            SetText("ft-preview-region", "");
            _root.Q<Button>("ft-go")?.SetEnabled(false);
            _root.Q<Button>("ft-map-prev")?.SetEnabled(maps.Count > 1);
            _root.Q<Button>("ft-map-next")?.SetEnabled(maps.Count > 1);
        }

        private void SelectFtRow(MapDataSO.CheckpointMarker marker, Button row)
        {
            _ftSelected = marker;
            var list = _root.Q<ScrollView>("ft-list");
            if (list != null)
                foreach (var b in list.Children())
                    b.RemoveFromClassList("selected");
            row.AddToClassList("selected");

            SetText("ft-preview-name", marker.checkpointId.ToUpper());
            SetText("ft-preview-region", _ftMap != null
                ? $"MAP: {(string.IsNullOrEmpty(_ftMap.displayName) ? _ftMap.sceneName : _ftMap.displayName)}"
                : "");
            _root.Q<Button>("ft-go")?.SetEnabled(true);
        }

        private void TeleportToSelected()
        {
            if (_ftSelected == null || _ftMap == null || _controller == null) return;
            var marker = _ftSelected.Value;
            ShowOverlay(Overlay.None);

            if (_ftMap.sceneName == GameLaunch.GameplayScene)
            {
                Vector3 target = marker.worldPos;
                foreach (var checkpoint in FindObjectsByType<Checkpoint>(FindObjectsSortMode.None))
                    if (checkpoint != null && checkpoint.DisplayName == marker.checkpointId)
                    {
                        target = checkpoint.RespawnPosition;
                        break;
                    }
                _controller.RpcRequestFastTravelToCheckpoint(target, marker.checkpointId);
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(_ftMap.sceneName))
            {
                Debug.LogWarning($"[FastTravel] Scene '{_ftMap.sceneName}' chưa có trong Build Settings.");
                return;
            }

            WorldMapState.PendingTravelScene = _ftMap.sceneName;
            WorldMapState.PendingTravelCheckpointId = marker.checkpointId;
            var launcher = Attrition.Networking.NetworkLauncher.Instance;
            if (launcher != null) launcher.BeginGameplay(_ftMap.sceneName);
            else Debug.LogWarning("[FastTravel] Không tìm thấy NetworkLauncher.");
        }

        /// <summary>
        /// Host bắn event này về MỌI máy khi rest / chuyển room / fast-travel.
        /// CHỈ nháy màn ĐEN rồi sáng lại — KHÔNG hiện màn hình loading (yêu cầu: chuyển room và rest chỉ
        /// cần nền đen). Fast-travel CROSS-MAP vẫn dùng loading riêng ở NetworkLauncher (load scene thật).
        /// </summary>
        private void OnCoopTravelLoading(string label)
        {
            Attrition.Gameplay.Environment.SceneFader.FlashBlack();
        }
    }
}
