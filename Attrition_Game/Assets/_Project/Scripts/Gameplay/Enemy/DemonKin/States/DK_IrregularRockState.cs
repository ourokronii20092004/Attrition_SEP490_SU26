using UnityEngine;

namespace Attrition.Gameplay.Enemy.DemonKin.States
{
    /// <summary>
    /// SKILL 2: Irregular Rock — boss tạo các mảnh đất BỌC quanh mục tiêu (6 frame đầu). Nếu player KHÔNG
    /// ra khỏi vùng trước khi đất gộp lại thì bị KHỐNG CHẾ (root). Sau khi khép, đất PHÁT NỔ gây sát thương
    /// (4 frame cuối) rồi THẢ player ra. Không dính khống chế thì vụ nổ vẫn gây damage như bình thường.
    ///
    /// BA MỐC THỜI GIAN:
    ///  1. Enter               → spawn hình đất bọc tại chỗ player (chốt vị trí, KHÔNG đuổi theo).
    ///  2. +rockEncloseTime    → đất khép: quét bán kính, ai còn trong vùng thì bị root.
    ///  3. +rockExplodeDelay   → nổ gây damage + nhả root sớm.
    ///
    /// Chốt vị trí ở Enter là điểm mấu chốt: nếu vùng bọc bám theo player mỗi tick thì không thể thoát,
    /// skill thành đòn chắc chắn trúng. Chốt trước cho player một cửa sổ thật để chạy ra.
    ///
    /// Sát thương do `EnemyAoEDamage` trên prefab lo (đặt damageDelay ≈ rockEncloseTime + rockExplodeDelay
    /// để hình nổ khớp lúc gây damage). Root do AI áp vì đó là logic gameplay, không phải hiệu ứng hình.
    /// </summary>
    public class DK_IrregularRockState : DemonKinBossState
    {
        private enum Phase { Enclosing, Rooted, Done }

        private Phase _phase;
        private float _elapsed;
        private Vector2 _center;
        private bool _caught;

        public override void Enter(DemonKinBossAI ai)
        {
            ai.CurrentState = EnemyState.Attacking;
            _phase = Phase.Enclosing;
            _elapsed = 0f;
            _caught = false;

            ai.DetectPlayer();
            ai.FaceTowardsPlayer();
            ai.StopMovement();

            // Chốt tâm vùng bọc = chỗ player ĐANG đứng lúc này.
            _center = ai.PlayerTarget != null
                ? (Vector2)ai.PlayerTarget.position
                : (Vector2)ai.transform.position + new Vector2(ai.DirToPlayerX() * 3f, 0f);

            ai.PlayAnim("Attack");

            // Hình đất bọc + vụ nổ đều nằm trên prefab này (EnemyAoEDamage tự nổ sau damageDelay).
            if (ai.HasStateAuthority)
                ai.SpawnAoE(ai.IrregularRockPrefab, _center, ai.rockDamage);
        }

        public override void Update(DemonKinBossAI ai)
        {
            _elapsed += ai.Runner.DeltaTime;
            ai.StopMovement();

            switch (_phase)
            {
                case Phase.Enclosing:
                    if (_elapsed < ai.rockEncloseTime) return;
                    // Đất khép lại → ai còn trong vùng thì bị giam.
                    if (ai.HasStateAuthority)
                        _caught = ai.RootPlayersInRadius(_center, ai.rockRadius, ai.rockRootDuration);
                    _phase = Phase.Rooted;
                    _elapsed = 0f;
                    return;

                case Phase.Rooted:
                    if (_elapsed < ai.rockExplodeDelay) return;
                    // Nổ xong → THẢ ra ngay, không đợi hết rockRootDuration (đúng yêu cầu "nổ rồi thả").
                    if (ai.HasStateAuthority && _caught)
                        ai.ClearRootPlayersInRadius(_center, ai.rockRadius);
                    _phase = Phase.Done;
                    ai.ChangeState(DemonKinBossAI.RecoveryState);
                    return;
            }
        }

        public override void Exit(DemonKinBossAI ai)
        {
            // An toàn: state bị cắt giữa lúc player đang bị giam (boss chết/knockback) → phải nhả, nếu không
            // player đứng cứng tới khi timer tự hết.
            if (ai.HasStateAuthority && _caught)
                ai.ClearRootPlayersInRadius(_center, ai.rockRadius);
            ai.StopMovement();
        }
    }
}
