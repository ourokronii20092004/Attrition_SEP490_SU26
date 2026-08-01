using Fusion;
using UnityEngine;

namespace Attrition.Gameplay.Enemy.ArchDemon.States
{
    /// <summary>
    /// SKILL 3: Water Blast — tung `blastCount` (3) lốc xoáy nước LẦN LƯỢT chạy về phía rìa phòng đối diện
    /// (có khoảng cách nhất định giữa các lốc), rồi mỗi lốc QUAY LẠI phía boss. Player trúng bị CHẬM 30%.
    ///
    /// AI tự lái từng lốc (không để prefab tự bay) vì lốc phải đi RA rồi VỀ — `EnemyProjectile` chỉ bay một
    /// chiều. Vì vậy dùng `SpawnAoETracked` để giữ NetworkObject rồi ghi `transform.position` ở host mỗi
    /// tick; NetworkTransform sync xuống client nên client thấy đúng đường đi mà không cần RPC.
    ///
    /// LÀM CHẬM do AI áp (`SlowPlayersInRadius`) chứ không phải AoE của prefab: `EnemyAoEDamage` chỉ gây
    /// damage MỘT LẦN lúc spawn, còn lốc phải làm chậm bất cứ ai nó CHẠM DỌC ĐƯỜNG đi và về.
    ///
    /// Về tới boss → spawn `waterBlastEndPrefab` (hiệu ứng tan) và despawn lốc.
    /// </summary>
    public class AD_WaterBlastState : ArchDemonBossState
    {
        /// <summary>Một lốc đang bay: object + mốc thời gian + đã quay đầu chưa.</summary>
        private struct Blast
        {
            public NetworkObject Obj;
            public float StartX;      // x lúc spawn (điểm boss) — đích khi quay về
            public float TargetX;     // x rìa phòng — đích khi đi ra
            public float Y;
            public bool Returning;
            public bool Done;
        }

        private readonly Blast[] _blasts = new Blast[8];
        private int _spawned;
        private float _elapsed;
        private float _nextSpawnTime;
        private float _dirX;
        private float _minX, _maxX;

        public override void Enter(ArchDemonBossAI ai)
        {
            ai.CurrentState = EnemyState.Attacking;
            _elapsed = 0f;
            _spawned = 0;
            _nextSpawnTime = Mathf.Max(ai.blastChargeTime, ArchDemonBossAI.AttackAnimCutTime);
            for (int i = 0; i < _blasts.Length; i++) _blasts[i] = default;

            ai.DetectPlayer();
            ai.FaceTowardsPlayer();
            ai.StopMovement();

            _dirX = ai.DirToPlayerX();
            ai.NetFacingDir = _dirX;
            ai.AttackLockedFacingDir = _dirX;

            BossRoomBounds.GetHorizontal(ai.transform.position, ai.roomFallbackHalfWidth,
                                         out _minX, out _maxX);

            ai.PlayAttackAnimNoOrb();   // cắt clip trước frame 8 — skill nước không kèm cầu bóng tối
        }

        public override void Update(ArchDemonBossAI ai)
        {
            _elapsed += ai.Runner.DeltaTime;
            ai.StopMovement();

            int total = Mathf.Min(ai.blastCount, _blasts.Length);

            // ── Spawn lần lượt từng lốc ──
            if (_spawned < total && _elapsed >= _nextSpawnTime)
            {
                if (_spawned == 0) ai.PlayAnim("Idle");
                SpawnBlast(ai, _spawned, total);
                _spawned++;
                _nextSpawnTime = _elapsed + ai.blastInterval;
            }

            // ── Lái các lốc đang sống ──
            bool anyAlive = false;
            for (int i = 0; i < total; i++)
            {
                if (_blasts[i].Done) continue;
                if (_blasts[i].Obj == null || !_blasts[i].Obj.IsValid) { _blasts[i].Done = true; continue; }
                anyAlive = true;
                DriveBlast(ai, i);
            }

            // Xong khi đã spawn hết và không còn lốc nào bay.
            if (_spawned >= total && !anyAlive)
                ai.ChangeState(ArchDemonBossAI.RecoveryState);
        }

        /// <summary>Spawn lốc thứ i, lệch dọc để 3 lốc có khoảng cách nhìn thấy được.</summary>
        private void SpawnBlast(ArchDemonBossAI ai, int i, int total)
        {
            // Cả 3 lốc cùng một phương ngang, nâng 1 tile trên chân player.
            float y = (ai.PlayerTarget != null ? ai.PlayerTarget.position.y : ai.transform.position.y) + 1f;
            float spawnX = ai.transform.position.x + _dirX * 1.2f;
            float targetX = _dirX > 0 ? _maxX - 0.5f : _minX + 0.5f;
            Vector2 pos = new Vector2(spawnX, y);
            float speed = Mathf.Max(0.1f, ai.blastMoveSpeed);
            float roundTripLifetime = 2f * Mathf.Abs(targetX - spawnX) / speed + 2f;

            var obj = ai.SpawnAoETracked(ai.WaterBlastPrefab, pos, ai.blastDamage, roundTripLifetime);
            if (obj == null) return;

            _blasts[i] = new Blast
            {
                Obj = obj,
                StartX = ai.transform.position.x,
                TargetX = targetX,
                Y = y,
                Returning = false,
                Done = false
            };
        }

        /// <summary>Di chuyển 1 lốc: ra rìa → quay đầu → về boss → tan.</summary>
        private void DriveBlast(ArchDemonBossAI ai, int i)
        {
            var b = _blasts[i];
            var tr = b.Obj.transform;
            float step = ai.blastMoveSpeed * ai.Runner.DeltaTime;

            float goalX = b.Returning ? b.StartX : b.TargetX;
            float newX = Mathf.MoveTowards(tr.position.x, goalX, step);
            tr.position = new Vector3(newX, b.Y, tr.position.z);

            // Làm chậm bất cứ ai lốc chạm phải trên đường (cả lượt đi và lượt về).
            ai.SlowPlayersInRadius(new Vector2(newX, b.Y), ai.blastTouchRadius,
                                   ai.blastSlowFactor, ai.blastSlowDuration);

            if (Mathf.Abs(newX - goalX) > 0.05f) { _blasts[i] = b; return; }

            if (!b.Returning)
            {
                // Tới rìa → quay đầu về boss.
                b.Returning = true;
                _blasts[i] = b;
                return;
            }

            // Về tới boss → hiệu ứng tan + despawn.
            ai.SpawnAoE(ai.WaterBlastEndPrefab, new Vector2(newX, b.Y), 0);
            if (b.Obj != null && b.Obj.IsValid) ai.Runner.Despawn(b.Obj);
            b.Obj = null;
            b.Done = true;
            _blasts[i] = b;
        }

        public override void Exit(ArchDemonBossAI ai)
        {
            // State bị cắt giữa lúc lốc còn bay (boss chết/knockback) → dọn để không còn lốc treo lơ lửng
            // gây damage/slow vĩnh viễn.
            for (int i = 0; i < _blasts.Length; i++)
            {
                if (_blasts[i].Obj != null && _blasts[i].Obj.IsValid && ai.HasStateAuthority)
                    ai.Runner.Despawn(_blasts[i].Obj);
                _blasts[i] = default;
            }
            ai.StopMovement();
        }
    }
}
