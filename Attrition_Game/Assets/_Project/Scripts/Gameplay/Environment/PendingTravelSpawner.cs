using System.Collections;
using Fusion;
using UnityEngine;

namespace Attrition.Gameplay.Environment
{
    /// <summary>
    /// Xử lý FAST-TRAVEL CROSS-MAP phía Gameplay: sau khi scene mới load do travel từ World Map,
    /// host đặt MỌI player tại checkpoint đích (lấy worldPos từ MapData). Đặt 1 cái trong mỗi
    /// scene gameplay (NetworkObject). Đặt ở Gameplay assembly để tránh vòng lặp asmdef với Networking.
    /// </summary>
    public class PendingTravelSpawner : NetworkBehaviour
    {
        public override void Spawned()
        {
            if (!HasStateAuthority) return;

            string scene = Attrition.Persistence.GameLaunch.GameplayScene;
            if (string.IsNullOrEmpty(WorldMapState.PendingTravelScene) || WorldMapState.PendingTravelScene != scene)
                return;

            StartCoroutine(PlaceAfterPlayersReady());
        }

        private IEnumerator PlaceAfterPlayersReady()
        {
            string scene = Attrition.Persistence.GameLaunch.GameplayScene;
            string wantId = WorldMapState.PendingTravelCheckpointId;

            // Tìm vị trí checkpoint đích: ưu tiên Checkpoint thật trong scene; fallback MapData marker.
            Vector3 target;
            if (!TryGetCheckpointPos(wantId, out target))
            {
                var reg = MapRegistrySO.Load();
                var map = reg != null ? reg.GetByScene(scene) : null;
                bool found = false;
                if (map != null)
                    foreach (var m in map.checkpoints)
                        if (m.checkpointId == wantId) { target = m.worldPos; found = true; break; }
                if (!found) { Clear(); yield break; }
            }

            // Chờ tới khi có ít nhất 1 player spawned (mọi peer), rồi teleport tất cả.
            float timeout = 5f;
            while (timeout > 0f)
            {
                var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
                if (players.Length > 0)
                {
                    foreach (var p in players) if (p != null) p.TeleportTo(target);
                    break;
                }
                timeout -= Time.deltaTime;
                yield return null;
            }

            // Cập nhật MostRecentlyActivated → respawn / Game Over hồi sinh đúng checkpoint mới.
            if (!string.IsNullOrEmpty(wantId))
            {
                foreach (var cp in FindObjectsByType<Attrition.Gameplay.World.Checkpoint>(FindObjectsSortMode.None))
                {
                    if (cp != null && cp.DisplayName == wantId)
                    {
                        Attrition.Gameplay.World.Checkpoint.MostRecentlyActivated = cp;
                        break;
                    }
                }

                // LƯU checkpoint đích: solo → local JSON, coop → server.
                var saver = Attrition.Gameplay.Persistence.GameSaveService.EnsureExists();
                saver.Save(Attrition.Gameplay.Persistence.GameSaveService.SaveEvent.Rest,
                           wantId, target);
            }

            Clear();
        }

        private bool TryGetCheckpointPos(string id, out Vector3 pos)
        {
            pos = Vector3.zero;
            if (string.IsNullOrEmpty(id)) return false;
            foreach (var cp in FindObjectsByType<Attrition.Gameplay.World.Checkpoint>(FindObjectsSortMode.None))
            {
                if (cp != null && cp.DisplayName == id) { pos = cp.transform.position; return true; }
            }
            return false;
        }

        private void Clear()
        {
            WorldMapState.PendingTravelScene = null;
            WorldMapState.PendingTravelCheckpointId = null;
        }
    }
}
