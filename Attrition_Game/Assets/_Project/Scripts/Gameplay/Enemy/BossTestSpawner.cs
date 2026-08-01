using Fusion;
using UnityEngine;

namespace Attrition.Gameplay.Enemy
{
    /// <summary>
    /// RIG TEST BOSS (chỉ dùng trong scene Enemy_Axe_Demon) — bấm số để gọi boss ra ngay tại chỗ, không phải
    /// đi hết map đánh từng con.
    ///
    ///   2 = Druid (map 2)   3 = Elf (map 3)   4 = DemonKin (map 4)   5 = ArchDemon (map 5)
    ///   0 = despawn boss đang test (gọi con khác cũng tự despawn con cũ)
    ///
    /// Boss spawn ra là đã VÀO TRẬN luôn (StartIntroSequence) vì prefab boss để waitForTrigger = 1, mà scene
    /// test không có BossEncounterTrigger — nếu không gọi thì boss đứng bất động.
    ///
    /// Chỉ HOST spawn: Runner.Spawn là host-authoritative, client bấm phím sẽ không có tác dụng (đúng ý —
    /// tránh 2 máy spawn 2 con boss trùng nhau).
    /// </summary>
    public class BossTestSpawner : MonoBehaviour
    {
        [Tooltip("Nơi boss xuất hiện. Trống thì dùng chính transform của object này.")]
        public Transform spawnPoint;
        [Tooltip("Điểm cứu player nếu rơi khỏi arena test (player 1/2).")]
        public Transform[] safePlayerPoints;
        [Tooltip("Player thấp hơn Y này sẽ được đưa về điểm test tương ứng.")]
        public float rescueBelowY = 45f;

        [Header("Boss prefab (thứ tự map 2 → 5)")]
        public NetworkObject druidPrefab;
        public NetworkObject elfPrefab;
        public NetworkObject demonKinPrefab;
        public NetworkObject archDemonPrefab;

        private NetworkObject _current;

        private NetworkRunner Runner
            => Attrition.Networking.NetworkLauncher.Instance != null
             ? Attrition.Networking.NetworkLauncher.Instance.Runner : null;

        private void Update()
        {
            RescueFallenPlayers();

            if (Input.GetKeyDown(KeyCode.Alpha2)) Spawn(druidPrefab, "Druid");
            else if (Input.GetKeyDown(KeyCode.Alpha3)) Spawn(elfPrefab, "Elf");
            else if (Input.GetKeyDown(KeyCode.Alpha4)) Spawn(demonKinPrefab, "DemonKin");
            else if (Input.GetKeyDown(KeyCode.Alpha5)) Spawn(archDemonPrefab, "ArchDemon");
            else if (Input.GetKeyDown(KeyCode.Alpha0)) DespawnCurrent();
        }

        private void RescueFallenPlayers()
        {
            var runner = Runner;
            if (runner == null || !runner.IsServer || safePlayerPoints == null || safePlayerPoints.Length == 0) return;

            foreach (var player in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            {
                if (player == null || player.transform.position.y >= rescueBelowY) continue;
                int i = player.Object != null ? Mathf.Abs(player.Object.InputAuthority.RawEncoded) % safePlayerPoints.Length : 0;
                var point = safePlayerPoints[i];
                if (point != null) player.TeleportTo(point.position);
            }
        }

        private void Spawn(NetworkObject prefab, string label)
        {
            var runner = Runner;
            if (runner == null || !runner.IsServer) return;   // client không spawn (xem doc class)
            if (prefab == null)
            {
                Debug.LogWarning($"[BossTest] Chưa gán prefab {label} trên BossTestSpawner.");
                return;
            }

            DespawnCurrent();

            Vector3 pos = spawnPoint != null ? spawnPoint.position : transform.position;
            _current = runner.Spawn(prefab, pos, Quaternion.identity, null);

            // Scene test không có trigger phòng boss → phải tự đẩy boss vào trận, nếu không nó đứng chờ mãi.
            var encounter = _current != null ? _current.GetComponent<Attrition.Core.IBossEncounter>() : null;
            encounter?.StartIntroSequence();

            Debug.Log($"[BossTest] Spawn {label}. Bấm 0 để despawn, 2/3/4/5 để đổi boss.");
        }

        private void DespawnCurrent()
        {
            var runner = Runner;
            if (runner == null || !runner.IsServer) return;
            if (_current != null && _current.IsValid) runner.Despawn(_current);
            _current = null;
        }
    }
}
