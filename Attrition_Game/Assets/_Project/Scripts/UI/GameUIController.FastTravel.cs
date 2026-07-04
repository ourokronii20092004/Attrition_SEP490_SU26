using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Attrition.Gameplay.World;
using Attrition.Gameplay.Player;

namespace Attrition.UI
{
    /// <summary>
    /// Fast Travel / Checkpoint (Bonfire) System.
    /// Manages Rest, Level Up, Flask Allocation, and Fast Travel.
    /// </summary>
    public partial class GameUIController
    {
        private Checkpoint _ftSelected;
        
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
            _controller.RequestRestAtCheckpoint();
            ShowOverlay(Overlay.None);
            // KHÔNG chạy loading local ở đây: host bắn CoopFeedbackEvents.OnTravelLoading về CẢ HAI máy
            // (kể cả máy này) khi rest thành công → OnCoopTravelLoading lo thanh load đồng bộ. Chạy ở
            // đây nữa sẽ double trên máy bấm, và rest có thể bị từ chối (còn quái) mà vẫn hiện loading.
        }

        private void OpenTravelMenu()
        {
            HideAllBonfireMenus();
            _root.Q<VisualElement>("ft-travel-menu")?.RemoveFromClassList("hidden");
            RefreshFastTravelList();
        }

        private void RefreshFastTravelList()
        {
            var list = _root?.Q<ScrollView>("ft-list");
            if (list == null) return;
            list.Clear();
            _ftSelected = null;

            var all = FindObjectsByType<Checkpoint>(FindObjectsSortMode.None);
            int discovered = 0;

            foreach (var cp in all)
            {
                if (!cp.HasBeenActivated) continue;
                discovered++;
                var c = cp;

                var row = new Button { text = c.DisplayName };
                row.AddToClassList("ft-row");
                row.clicked += () => SelectFtRow(c, row);
                list.Add(row);
            }

            SetText("ft-count", $"{discovered} / {all.Length} DISCOVERED");
            SetText("ft-preview-name", discovered > 0 ? "SELECT A BEACON" : "NO BEACONS YET");
            SetText("ft-preview-region", "");
            _root.Q<Button>("ft-go")?.SetEnabled(false);
        }

        private void SelectFtRow(Checkpoint cp, Button row)
        {
            _ftSelected = cp;
            var list = _root.Q<ScrollView>("ft-list");
            if (list != null)
                foreach (var b in list.Children())
                    b.RemoveFromClassList("selected");
            row.AddToClassList("selected");

            SetText("ft-preview-name", cp.DisplayName.ToUpper());
            SetText("ft-preview-region", cp.Region);
            _root.Q<Button>("ft-go")?.SetEnabled(true);
        }

        private void TeleportToSelected()
        {
            if (_ftSelected == null || _controller == null) return;
            _controller.RpcRequestFastTravelToCheckpoint(_ftSelected.RespawnPosition, _ftSelected.DisplayName);
            ShowOverlay(Overlay.None);
            // Loading do host bắn về cả 2 máy (OnCoopTravelLoading), không chạy local ở đây.
        }

        /// <summary>Host bắn event này về MỌI máy khi rest/fast-travel thành công → thanh load đồng bộ.</summary>
        private void OnCoopTravelLoading(string label)
        {
            StartCoroutine(FastTravelLoadingRoutine(label));
        }

        private System.Collections.IEnumerator FastTravelLoadingRoutine(string dest)
        {
            ShowLoading(dest, "Travelling...");
            float t = 0f;
            const float dur = 1.2f;
            while (t < dur)
            {
                t += Time.deltaTime;
                SetLoadingProgress(t / dur);
                yield return null;
            }
            HideLoading();
        }
    }
}
