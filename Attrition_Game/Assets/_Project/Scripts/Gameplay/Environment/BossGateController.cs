using System.Collections;
using Fusion;
using UnityEngine;
using Attrition.Controllers;
using Attrition.Gameplay.World;

namespace Attrition.Gameplay.Environment
{
    /// <summary>
    /// Cổng Boss + chuỗi sự kiện KHI BOSS CHẾT, theo đúng luồng:
    ///   Boss hết máu → DIALOGUE hiện → đọc hết thoại → ANIMATION DEATH chạy →
    ///   boss PHAI MỜ DẦN rồi biến mất → MỞ entryDoor + exitDoor + bật vùng chuyển scene.
    ///
    /// Boss KHÔNG tự despawn (HoldDespawn=true); controller này điều khiển toàn bộ thời điểm.
    /// Host cầm trịch; thoại + anim + fade chạy trên mọi máy qua RPC.
    /// </summary>
    public class BossGateController : NetworkBehaviour
    {
        [Header("---- BOSS ----")]
        [SerializeField] private EnemyController boss;
        [Tooltip("AI boss (SeveredFang / Druid / Elf / DemonKin / ArchDemon) — phải implement IBossEncounter. " +
                 "Để biết đã VÀO TRẬN chưa. Bỏ trống = bỏ qua khoá lối vào.")]
        [SerializeField] private MonoBehaviour bossAI;

        /// <summary>bossAI dạng interface — null nếu bỏ trống hoặc gán sai loại component.</summary>
        private Attrition.Core.IBossEncounter BossEncounter => bossAI as Attrition.Core.IBossEncounter;

        [Header("---- LỐI VÀO (khoá khi đang đánh) ----")]
        [SerializeField] private Door entryDoor;

        [Header("---- LỐI RA (mở sau khi boss biến mất) ----")]
        [SerializeField] private Door exitDoor;
        [SerializeField] private RoomTransitionZone exitZone;

        [Header("---- CHUỖI CHẾT ----")]
        [Tooltip("Thời gian animation Death chạy trước khi boss phai mờ (giây).")]
        [SerializeField] private float deathAnimTime = 2f;
        [Tooltip("Thoại boss nói trước animation chết. Bỏ trống = bỏ qua thoại.")]
        [SerializeField] private Attrition.Data.DialogueSO deathDialogue;
        [Tooltip("Thời gian phai mờ (fade out) trước khi boss biến mất (giây).")]
        [SerializeField] private float fadeOutTime = 1f;

        /// <summary>Boss mà cổng này quản (để bên ngoài biết boss đã được gate xử lý, tránh reset 2 lần).</summary>
        public EnemyController Boss => boss;

        [Networked] public NetworkBool DeathStarted { get; set; }
        [Networked] public NetworkBool BossDefeated { get; set; }
        [Networked] public NetworkBool EntrySealed { get; set; }

        /// <summary>Id boss để nhớ "đã hạ" qua các lần load scene / lần chơi. Lấy từ EnemyStats.EnemyId.</summary>
        private string BossId
        {
            get
            {
                if (boss == null) return null;
                var st = boss.GetComponent<Attrition.Gameplay.Enemy.EnemyStats>();
                return st != null ? st.EnemyId : null;
            }
        }

        // Cache id lúc Spawned: sau khi boss despawn thì `boss == null` nên BossId trả null, không tra
        // BossDefeatState được nữa. Cũng tránh GetComponent mỗi tick.
        private string _cachedBossId;

        public override void Spawned()
        {
            if (!HasStateAuthority) return;
            if (boss != null) boss.HoldDespawn = true;

            _cachedBossId = BossId;

            // Nạp lazy (thứ tự Spawned vs FogTracker.Start không đảm bảo).
            BossDefeatState.EnsureLoadedForSolo();

            // CHỈ đặt cờ ở đây, KHÔNG mở cửa/bật zone. Lý do: `Door.Spawned()` và
            // `RoomTransitionZone.Spawned()` đều GHI ĐÈ trạng thái về mặc định Inspector
            // (startOpen=false / startActive=false), mà thứ tự Spawned giữa các NetworkObject KHÔNG
            // đảm bảo → mở ở đây có thể bị chúng đóng lại ngay sau đó (bug: về Map 1 rồi không sang
            // lại Map 2 được vì exitZone bị khoá). Việc mở dồn xuống FixedUpdateNetwork — chạy SAU
            // khi mọi Spawned() đã xong.
            if (!BossDefeatState.IsDefeated(BossId)) return;

            BossDefeated = true;
            DeathStarted = true;      // chặn FixedUpdateNetwork chạy lại chuỗi chết
            EntrySealed = false;
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            // COOP: danh sách boss đã hạ tới TỪ SERVER (GET /sessions/{id}) nên có thể về SAU Spawned()
            // — lúc đó `IsDefeated` còn false và boss sẽ sống lại nguyên máu. Nên kiểm lại mỗi tick cho
            // tới khi khớp, cùng tinh thần "tự chữa" như khối mở cửa bên dưới.
            // Chỉ chạy chiều false→true: không bao giờ hồi sinh boss vừa bị hạ trong phiên này.
            if (!BossDefeated && !DeathStarted && BossDefeatState.IsDefeated(_cachedBossId))
            {
                BossDefeated = true;
                DeathStarted = true;
                EntrySealed = false;
            }

            // Boss đã hạ từ lần chơi/lần load trước → dọn xác + MỞ LẠI đường đi.
            // Chạy ở FUN (sau mọi Spawned) nên không bị Door/Zone ghi đè mặc định.
            // ĐẶT TRƯỚC guard `boss == null`: sau khi despawn thì boss = null, nếu return sớm ở trên
            // thì cửa/zone sẽ KHÔNG BAO GIỜ được mở (bug: về Map 1 rồi không sang lại Map 2 được).
            if (BossDefeated)
            {
                // Gọi MỖI TICK, không dùng cờ "đã áp 1 lần": Open()/Activate() đều idempotent (return
                // sớm nếu đã mở) nên rẻ, mà lại TỰ CHỮA nếu Spawned() của Door/Zone chạy sau tick này
                // và ghi đè về mặc định đóng. ForceDespawnNow cũng đã guard Object.IsValid.
                if (boss != null) boss.ForceDespawnNow();
                if (entryDoor != null) entryDoor.Open();
                if (exitDoor != null) exitDoor.Open();
                if (exitZone != null) exitZone.Activate();
                return;
            }

            if (boss == null) return;

            if (!DeathStarted && boss.IsDead)
            {
                DeathStarted = true;
                boss.HoldDespawn = true;       // không cho boss tự despawn — controller lo
                RpcRunDeathSequence();          // chạy chuỗi trên MỌI máy
                return;
            }

            if (DeathStarted) return;

            // Dùng EncounterStarted ([Networked]) chứ KHÔNG dùng IsWaitingForTrigger: cái sau là bool
            // thường trên AI, chỉ đúng ở host. Cả hai đều chạy ở nhánh host này nên kết quả như nhau,
            // nhưng EncounterStarted là nguồn sự thật đã đồng bộ — trận đã bắt đầu thì cửa phải đóng,
            // kể cả khi boss được đánh thức bằng đường khác (bị đánh trước) chứ không qua trigger.
            if (!EntrySealed && entryDoor != null && BossEncounter != null && BossEncounter.EncounterStarted)
            {
                EntrySealed = true;
                entryDoor.Close();
            }
        }

        /// <summary>
        /// Cả team player chết mà boss còn sống → MỞ LẠI lối vào + reset boss về chờ trigger, để player
        /// hồi sinh ở checkpoint có thể quay lại đánh. Không mở lại thì phòng boss bị khoá vĩnh viễn.
        /// Bỏ qua nếu boss đã bị hạ (lúc đó FinishDefeat đã mở cửa). Chỉ host.
        /// </summary>
        public void ResetEncounterAfterWipe()
        {
            if (!HasStateAuthority) return;
            if (DeathStarted || BossDefeated) return;   // boss đã hạ → giữ nguyên kết quả

            EntrySealed = false;
            if (entryDoor != null) entryDoor.Open();

            // Boss: hồi đầy máu + reset AI về chờ trigger (ẩn thanh máu tới khi player kích hoạt lại).
            if (boss != null)
            {
                boss.ResetForEncounterRetry();
                var bc = boss.GetComponent<Attrition.Controllers.BossController>();
                if (bc != null) bc.ResetPhases();
            }
            BossEncounter?.ResetEncounter();

            // Trigger vào phòng đã "dùng" 1 lần → cho phép kích hoạt lại.
            foreach (var trig in FindObjectsByType<BossEncounterTrigger>(FindObjectsSortMode.None))
                if (trig != null && trig.boss == bossAI) trig.ResetTrigger();
        }

        /// <summary>Chạy trên mọi máy: thoại → death anim → fade. Host mở cửa + despawn.</summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcRunDeathSequence()
        {
            StartCoroutine(DeathSequence());
        }

        private IEnumerator DeathSequence()
        {
            // PHA 1: Dialogue — animation chỉ chạy sau khi player đọc xong.
            if (deathDialogue != null && Attrition.Data.DialogueEvents.OnOpenCustomDialogue != null)
            {
                bool done = false;
                Attrition.Data.DialogueEvents.OnOpenCustomDialogue.Invoke(deathDialogue, () => done = true);
                while (!done) yield return null;
            }

            // PHA 2: Animation Death.
            ForcePlayDeathAnim();
            yield return new WaitForSeconds(deathAnimTime);

            // PHA 3: Phai mờ dần.
            yield return FadeOutBoss();

            // PHA 4 (chỉ HOST): boss biến mất + mở cửa + bật vùng chuyển.
            // ponytail: host chưa đợi ACK thoại từ mọi client; thêm RPC ACK nếu cần đồng bộ tuyệt đối.
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

            // GHI NHỚ boss đã hạ (bền qua load scene + out/vào game). Không có bước này thì quay lại
            // map cũ hoặc vào lại game boss sẽ spawn nguyên máu dù đã đánh chết.
            string id = BossId;
            if (BossDefeatState.MarkDefeated(id))
            {
                var saver = Attrition.Gameplay.Persistence.GameSaveService.EnsureExists();
                if (Attrition.Persistence.GameLaunch.IsOnline)
                {
                    // COOP: ghi NGAY 1 row lên server. Trước đây nhánh này không làm gì ("chỉ giữ trong
                    // phiên host") nên boss vừa hạ chỉ nằm trong RAM tới lần rest kế tiếp — host thoát
                    // trước đó là boss sống lại nguyên máu. Không dùng bulk save: nó ghi cả HP/vị trí
                    // đang dở giữa trận của mọi player.
                    saver.SaveBossDefeatOnline(id);
                }
                else
                {
                    // Solo: ghi ngay vào save slot.
                    saver.SaveWorldState();
                }
            }

            if (boss != null) boss.ForceDespawnNow();
            if (entryDoor != null) entryDoor.Open();
            if (exitDoor != null) exitDoor.Open();
            if (exitZone != null) exitZone.Activate();
        }
    }
}
