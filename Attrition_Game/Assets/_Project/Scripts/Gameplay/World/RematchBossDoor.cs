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

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || entryDoor == null) return;

            // ponytail: trạng thái chỉ sống trong phiên Map 5; thêm rematchId riêng vào save nếu cần
            // duy trì rematch qua việc thoát game/load lại scene mà không dùng chung enemyId boss gốc.
            if (boss == null || boss.Object == null || !boss.Object.IsValid || boss.IsDead)
            {
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
