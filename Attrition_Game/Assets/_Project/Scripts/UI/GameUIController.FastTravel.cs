using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Attrition.Gameplay.World;
using Attrition.Gameplay.Player;

namespace Attrition.UI
{
    /// <summary>
    /// Fast Travel cho GameUIController. Chỉ liệt kê checkpoint đã kích hoạt (BR-37).
    /// Chọn beacon → preview; TELEPORT → RPC host dịch chuyển cả 2 player (giữ chung khung camera coop).
    /// </summary>
    public partial class GameUIController
    {
        private Checkpoint _ftSelected;

        private void SetupFastTravelControls()
        {
            BindButton("ft-close", () => ShowOverlay(Overlay.None));
            BindButton("ft-go", TeleportToSelected);
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
            string dest = _ftSelected.DisplayName;
            _controller.RpcRequestFastTravel(_ftSelected.RespawnPosition);
            StartCoroutine(FastTravelLoadingRoutine(dest));
        }

        /// <summary>Hiện UI Loading trong lúc dịch chuyển rồi tự ẩn (cảm giác chuyển khu).</summary>
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
