using Fusion;
using UnityEngine;
using Attrition.Controllers;

namespace Attrition.Gameplay.World
{
    /// <summary>Cửa lock-in cho một boss rematch; không đọc BossDefeatState vì rematch dùng chung enemyId với boss gốc.</summary>
    public class RematchBossDoor : NetworkBehaviour
    {
        [SerializeField] private EnemyController boss;
        [SerializeField] private MonoBehaviour bossAI;
        [SerializeField] private Door entryDoor;

        private Attrition.Core.IBossEncounter Encounter => bossAI as Attrition.Core.IBossEncounter;

        // Khóa "đã hạ" RIÊNG cho bản rematch — rematch dùng chung enemyId với boss gốc, nên nếu ghi vào
        // BossDefeatState bằng enemyId sẽ làm boss gốc (Map 2/3/4) coi như đã hạ. Prefix phân biệt.
        private const string KeyPrefix = "rematch:";
        private string _defeatKey;

        public override void Spawned()
        {
            if (!HasStateAuthority) return;

            // Cache enemyId lúc spawn: sau khi boss despawn thì tham chiếu null, không lấy được nữa.
            var es = boss != null ? boss.GetComponent<Attrition.Gameplay.Enemy.EnemyStats>() : null;
            string enemyId = es != null ? es.EnemyId : null;
            _defeatKey = string.IsNullOrEmpty(enemyId) ? null : KeyPrefix + enemyId;
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || entryDoor == null) return;

            // Đã hạ (lần này hoặc lần trước) → GIỮ boss chết: despawn mỗi tick (idempotent) và mở cửa.
            // Vì sao phải despawn mỗi tick: boss là prefab scene-placed, `EnemyController` tự despawn khi
            // chết, nhưng Fusion có thể respawn lại object scene-placed khi thế giới nạp lại (rest) —
            // chính là bug "rest xong 3 boss phụ sống lại". Despawn lặp lại giữ chúng biến mất.
            if (_defeatKey != null && Attrition.Gameplay.Environment.BossDefeatState.IsDefeated(_defeatKey))
            {
                if (boss != null && boss.Object != null && boss.Object.IsValid && !boss.IsDead)
                    boss.ForceDespawnNow();
                entryDoor.Open();
                return;
            }

            if (boss == null || boss.Object == null || !boss.Object.IsValid || boss.IsDead)
            {
                // Boss vừa chết (despawn xong) → đánh dấu đã hạ để giữ chết từ giờ.
                if (_defeatKey != null)
                    Attrition.Gameplay.Environment.BossDefeatState.MarkDefeated(_defeatKey);
                entryDoor.Open();
                return;
            }

            if (Encounter == null || Encounter.IsWaitingForTrigger)
                entryDoor.Open();
            else
                entryDoor.Close();
        }
    }
}
