using System.Collections;
using Fusion;
using UnityEngine;
using Attrition.Controllers;
using Attrition.Gameplay.World;
using Attrition.Gameplay.Enemy.SeveredFang;

namespace Attrition.Gameplay.Environment
{
    /// <summary>
    /// Cổng Boss + chuỗi sự kiện KHI BOSS CHẾT, theo đúng luồng:
    ///   Boss hết máu → ANIMATION DEATH chạy → xong anim → DIALOGUE hiện → đọc hết thoại →
    ///   boss PHAI MỜ DẦN rồi biến mất → MỞ entryDoor + exitDoor + bật vùng chuyển scene.
    ///
    /// Boss KHÔNG tự despawn (HoldDespawn=true); controller này điều khiển toàn bộ thời điểm.
    /// Host cầm trịch; thoại + anim + fade chạy trên mọi máy qua RPC.
    /// </summary>
    public class BossGateController : NetworkBehaviour
    {
        [Header("---- BOSS ----")]
        [SerializeField] private EnemyController boss;
        [Tooltip("SeveredFangAI của boss — để biết đã VÀO TRẬN chưa. Bỏ trống = bỏ qua khoá lối vào.")]
        [SerializeField] private SeveredFangAI bossAI;

        [Header("---- LỐI VÀO (khoá khi đang đánh) ----")]
        [SerializeField] private Door entryDoor;

        [Header("---- LỐI RA (mở sau khi boss biến mất) ----")]
        [SerializeField] private Door exitDoor;
        [SerializeField] private RoomTransitionZone exitZone;

        [Header("---- CHUỖI CHẾT ----")]
        [Tooltip("Thời gian cho animation Death chạy xong trước khi hiện thoại (giây).")]
        [SerializeField] private float deathAnimTime = 2f;
        [Tooltip("Thoại boss nói sau khi gục (player đọc xong boss mới phai mờ). Bỏ trống = bỏ qua thoại.")]
        [SerializeField] private Attrition.Data.DialogueSO deathDialogue;
        [Tooltip("Thời gian phai mờ (fade out) trước khi boss biến mất (giây).")]
        [SerializeField] private float fadeOutTime = 1f;

        [Networked] public NetworkBool DeathStarted { get; set; }
        [Networked] public NetworkBool BossDefeated { get; set; }
        [Networked] public NetworkBool EntrySealed { get; set; }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || boss == null) return;

            if (!DeathStarted && boss.IsDead)
            {
                DeathStarted = true;
                boss.HoldDespawn = true;       // không cho boss tự despawn — controller lo
                RpcRunDeathSequence();          // chạy chuỗi trên MỌI máy
                return;
            }

            if (DeathStarted) return;

            if (!EntrySealed && entryDoor != null && bossAI != null && !bossAI.waitForTrigger)
            {
                EntrySealed = true;
                entryDoor.Close();
            }
        }

        /// <summary>Chạy chuỗi chết trên mọi máy: chờ anim death → thoại → fade. Host kết thúc bằng mở cửa + despawn.</summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcRunDeathSequence()
        {
            StartCoroutine(DeathSequence());
        }

        private IEnumerator DeathSequence()
        {
            // PHA 1: Animation Death (EnemyController.HandleDeathVisuals đã set trigger; ép thêm cho chắc).
            ForcePlayDeathAnim();
            yield return new WaitForSeconds(deathAnimTime);

            // PHA 2: Dialogue — chờ ĐỌC XONG. Solo: chờ callback. Coop: mỗi máy tự chờ thoại của mình.
            if (deathDialogue != null)
            {
                bool done = false;
                Attrition.Data.DialogueEvents.OnOpenCustomDialogue?.Invoke(deathDialogue, () => done = true);
                // Nếu không có UI nghe bus (không mở được) → timeout an toàn 8s.
                float t = 0f;
                while (!done && t < 8f) { t += Time.deltaTime; yield return null; }
            }

            // PHA 3: Phai mờ dần.
            yield return FadeOutBoss();

            // PHA 4 (chỉ HOST): boss biến mất + mở cửa + bật vùng chuyển.
            if (Object.HasStateAuthority)
                FinishDefeat();
        }

        private void ForcePlayDeathAnim()
        {
            if (boss == null) return;
            var anim = boss.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.SetBool("IsDead", true);
                anim.SetTrigger("DieTrigger");
            }
        }

        private IEnumerator FadeOutBoss()
        {
            if (boss == null) yield break;
            var renderers = boss.GetComponentsInChildren<SpriteRenderer>();
            if (renderers == null || renderers.Length == 0) yield break;

            float t = 0f;
            while (t < fadeOutTime)
            {
                t += Time.deltaTime;
                float a = Mathf.Clamp01(1f - t / fadeOutTime);
                foreach (var sr in renderers)
                {
                    if (sr == null) continue;
                    // Bỏ qua renderer UI (HealthBar/NameTag) nếu có — chỉ mờ thân boss.
                    var c = sr.color; c.a = a; sr.color = c;
                }
                yield return null;
            }
        }

        private void FinishDefeat()
        {
            if (BossDefeated) return;
            BossDefeated = true;
            if (boss != null) boss.ForceDespawnNow();
            if (entryDoor != null) entryDoor.Open();
            if (exitDoor != null) exitDoor.Open();
            if (exitZone != null) exitZone.Activate();
        }
    }
}
