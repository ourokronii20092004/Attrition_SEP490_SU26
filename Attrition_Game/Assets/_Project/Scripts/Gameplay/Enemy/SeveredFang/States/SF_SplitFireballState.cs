using Fusion;
using UnityEngine;

namespace Attrition.Gameplay.Enemy.SeveredFang.States
{
    /// <summary>
    /// SKILL 5: Split Fireball (fireball nâng cao) — Boss bắn 1 cầu lửa "carrier" bay THẲNG về phía
    /// player; sau splitTravelTime giây, carrier bị huỷ và TÁCH thành splitFireballCount cầu lửa con
    /// toả theo splitSpreadAngle, mỗi cầu bay tiếp theo hướng đã toả.
    ///
    /// Tái dùng Attack animation (không cần anim mới). Host điều khiển; đạn là NetworkObject nên mọi
    /// máy thấy. Hướng toả tính quanh hướng bay của carrier (về phía player lúc tách).
    /// </summary>
    public class SF_SplitFireballState : SeveredFangState
    {
        private float _elapsed;
        private bool _carrierSpawned;
        private bool _splitDone;
        private NetworkObject _carrier;
        private Vector2 _carrierDir;
        private Vector2 _spawnPos;

        public override void Enter(SeveredFangAI ai)
        {
            ai.CurrentState = EnemyState.Attacking;
            _elapsed = 0f;
            _carrierSpawned = false;
            _splitDone = false;
            _carrier = null;

            ai.DetectPlayer();
            ai.FaceTowardsPlayer();
            ai.AttackLockedFacingDir = ai.NetFacingDir;
            ai.StopMovement();
            ai.PlayAttackAnim();
        }

        public override void Update(SeveredFangAI ai)
        {
            _elapsed += ai.Runner.DeltaTime;
            ai.StopMovement();

            // Phase 1: chốt hướng + bắn carrier ngay đầu.
            if (!_carrierSpawned)
            {
                _carrierSpawned = true;
                _spawnPos = (Vector2)ai.transform.position + new Vector2(ai.AttackLockedFacingDir * 0.8f, 0.3f);
                _carrierDir = ai.PlayerTarget != null
                    ? ((Vector2)ai.PlayerTarget.position - _spawnPos).normalized
                    : new Vector2(ai.AttackLockedFacingDir, 0f);
                if (_carrierDir.sqrMagnitude < 0.0001f) _carrierDir = new Vector2(ai.AttackLockedFacingDir, 0f);

                _carrier = ai.SpawnFireBoltTracked(_spawnPos, _carrierDir, ai.splitCarrierDamage, ai.splitCarrierSpeed);
                return;
            }

            // Phase 2: sau splitTravelTime → huỷ carrier tại vị trí hiện tại của nó rồi tách chùm con.
            if (!_splitDone && _elapsed >= ai.splitTravelTime)
            {
                _splitDone = true;

                // Điểm tách = vị trí carrier còn sống, nếu không thì ước lượng theo hướng * tốc độ * thời gian.
                Vector2 splitPos;
                if (_carrier != null && _carrier.IsValid)
                {
                    splitPos = _carrier.transform.position;
                    ai.DespawnObject(_carrier);
                }
                else
                {
                    float sp = ai.splitCarrierSpeed > 0f ? ai.splitCarrierSpeed : 12f;
                    splitPos = _spawnPos + _carrierDir * sp * ai.splitTravelTime;
                }

                SpawnSplitChildren(ai, splitPos);
            }

            // Phase 3: xong → recovery.
            if (_splitDone && _elapsed >= ai.splitTravelTime + 0.3f)
            {
                ai.ChangeState(SeveredFangAI.RecoveryState);
            }
        }

        /// <summary>Toả splitFireballCount cầu lửa con quanh hướng carrier (về phía player lúc tách).</summary>
        private void SpawnSplitChildren(SeveredFangAI ai, Vector2 splitPos)
        {
            int count = Mathf.Max(1, ai.splitFireballCount);

            // Hướng tâm: ưu tiên nhắm lại player tại thời điểm tách (player đã di chuyển).
            Vector2 center = ai.PlayerTarget != null
                ? ((Vector2)ai.PlayerTarget.position - splitPos).normalized
                : _carrierDir;
            if (center.sqrMagnitude < 0.0001f) center = _carrierDir;

            float baseAng = Mathf.Atan2(center.y, center.x) * Mathf.Rad2Deg;
            float step = count > 1 ? ai.splitSpreadAngle / (count - 1) : 0f;
            float start = baseAng - (count > 1 ? ai.splitSpreadAngle * 0.5f : 0f);

            for (int i = 0; i < count; i++)
            {
                float a = (start + step * i) * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a)).normalized;
                ai.SpawnFireBolt(splitPos, dir, ai.splitFireballDamage, ai.splitFireballSpeed);
            }
        }

        public override void Exit(SeveredFangAI ai)
        {
            ai.StopMovement();
        }
    }
}
