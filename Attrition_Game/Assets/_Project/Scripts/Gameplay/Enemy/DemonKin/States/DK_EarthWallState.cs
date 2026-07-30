using UnityEngine;

namespace Attrition.Gameplay.Enemy.DemonKin.States
{
    /// <summary>
    /// SKILL 4: Earth Wall — boss triệu `wallCount` (4-5) bức tường TRỒI LÊN từ dưới đất, lần lượt tiến về
    /// phía player. Tường CHẶN ĐƯỢC skill/đạn của player.
    ///
    /// CHẶN ĐẠN — không cần code riêng: đạn của player (`EnemyProjectile` dùng chung cho cả SkillProjectile)
    /// dò va chạm bằng CircleCast theo `hitLayer`, và hitLayer đó vốn đã gồm `Ground`. Chỉ cần collider của
    /// tường nằm ở layer Ground là đạn tự nổ khi đụng — tool setup lo phần layer. Chi tiết trong
    /// <see cref="EnemyBlockingWall"/>.
    ///
    /// Tường trồi LẦN LƯỢT ra xa (`wallInterval`) chứ không dựng cùng lúc: player thấy được hàng tường đang
    /// tiến tới và có nhịp để nhảy qua, đồng thời tạo cảm giác mặt đất nứt lan.
    ///
    /// Kẹp trong biên phòng để không dựng tường xuyên qua tường phòng.
    /// </summary>
    public class DK_EarthWallState : DemonKinBossState
    {
        private float _elapsed;
        private int _spawned;
        private float _nextSpawnTime;
        private float _dirX;
        private float _minX, _maxX;

        public override void Enter(DemonKinBossAI ai)
        {
            ai.CurrentState = EnemyState.Attacking;
            _elapsed = 0f;
            _spawned = 0;
            _nextSpawnTime = ai.wallChargeTime;

            ai.DetectPlayer();
            ai.FaceTowardsPlayer();
            ai.StopMovement();

            _dirX = ai.DirToPlayerX();
            ai.NetFacingDir = _dirX;
            ai.AttackLockedFacingDir = _dirX;

            BossRoomBounds.GetHorizontal(ai.transform.position, ai.roomFallbackHalfWidth,
                                         out _minX, out _maxX);

            ai.PlayAnim("Attack");
        }

        public override void Update(DemonKinBossAI ai)
        {
            _elapsed += ai.Runner.DeltaTime;
            ai.StopMovement();

            // Chỉ host chạy tới đây: EnemyController.FixedUpdateNetwork return sớm nếu !HasStateAuthority,
            // nên toàn bộ state machine boss là host-only. Không cần nhánh riêng cho client.
            if (_spawned < ai.wallCount && _elapsed >= _nextSpawnTime)
            {
                float x = ai.transform.position.x
                          + _dirX * (ai.wallFirstOffset + ai.wallSpacing * _spawned);

                // Ra ngoài biên phòng thì dừng luôn chuỗi — dựng tường trong tường là vô nghĩa.
                if (x < _minX || x > _maxX)
                {
                    _spawned = ai.wallCount;
                    _nextSpawnTime = _elapsed;
                }
                else
                {
                    ai.SpawnAoE(ai.EarthWallPrefab, new Vector2(x, ai.transform.position.y), ai.wallDamage);
                    _spawned++;
                    _nextSpawnTime = _elapsed + ai.wallInterval;
                }
            }

            if (_spawned >= ai.wallCount && _elapsed >= _nextSpawnTime + 0.4f)
                ai.ChangeState(DemonKinBossAI.RecoveryState);
        }

        public override void Exit(DemonKinBossAI ai) => ai.StopMovement();
    }
}
