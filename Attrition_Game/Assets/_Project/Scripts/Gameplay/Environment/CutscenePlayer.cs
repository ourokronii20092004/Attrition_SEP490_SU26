using System.Collections;
using Fusion;
using UnityEngine;
using Attrition.Data;
using Attrition.Persistence;

namespace Attrition.Gameplay.Environment
{
    /// <summary>
    /// Chiếu một cutscene (lia camera + chữ giữa màn + hội thoại) khi player đi vào vùng trigger.
    ///
    /// CÁCH DÙNG (Inspector):
    ///   1. Tạo GameObject trong scene gameplay, thêm BoxCollider2D (Is Trigger tự bật) + NetworkObject.
    ///   2. Gắn script này, kéo CutsceneSO vào ô 'Cutscene'.
    ///   3. Tạo vài GameObject rỗng làm mốc camera, kéo vào 'Focus Points' theo đúng thứ tự;
    ///      mỗi beat trong CutsceneSO trỏ tới mốc bằng focusPointIndex.
    ///
    /// LUỒNG MẠNG: host phát hiện trigger (StateAuthority) → RPC cho MỌI máy tự chiếu cảnh cục bộ.
    /// Không dùng GamePause: coop cấm dừng mô phỏng Fusion. Cảnh chỉ khoá input + lia camera nên
    /// quái vẫn chạy — vì vậy hãy đặt vùng trigger ở nơi an toàn (đầu map, trước cửa boss).
    ///
    /// Mỗi máy chiếu ĐỘC LẬP: ai đọc thoại xong trước thì được đi trước. Cố tình không đồng bộ nhịp
    /// đọc — chờ nhau sẽ khiến người đọc nhanh bị treo màn hình vì bạn mình chưa bấm.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class CutscenePlayer : NetworkBehaviour
    {
        [Header("---- KỊCH BẢN ----")]
        [Tooltip("CutsceneSO chứa các nhịp của cảnh. Bỏ trống = không làm gì.")]
        [SerializeField] private CutsceneSO cutscene;

        [Tooltip("Các mốc camera. Beat trỏ tới đây bằng focusPointIndex (0 = mốc đầu tiên).")]
        [SerializeField] private Transform[] focusPoints;

        [Header("---- FADE ----")]
        [Tooltip("Fade đen ra/vào ở đầu và cuối cảnh cho đỡ giật camera.")]
        [SerializeField] private bool fadeAtEdges = true;
        [SerializeField] private float fadeDuration = 0.45f;

        /// <summary>Host đã kích hoạt cảnh này chưa (chống trigger lặp khi player đi qua lại).</summary>
        [Networked] public NetworkBool HasTriggered { get; set; }

        // Đang chiếu trên MÁY NÀY (mỗi peer một cờ riêng, không networked).
        private Coroutine _running;

        public override void Spawned()
        {
            GetComponent<Collider2D>().isTrigger = true;

            // Nạp danh sách cutscene đã xem từ save (chỉ chạy thật 1 lần mỗi lượt chơi).
            // ponytail: playOnce chỉ bền ở SOLO (file save). COOP nhớ trong phiên — vào lại phòng sẽ
            // xem lại cảnh. Nâng cấp: thêm cutsceneId vào bảng world_state (đã có sẵn khoá
            // SessionId + EventId cho đúng mục đích này) rồi nạp trong PlayerInventory.EnsureSessionLoaded.
            CutsceneState.EnsureLoadedFromSave();

            // Đã xem rồi + playOnce → khoá luôn, khỏi phải chờ tới lúc va trigger.
            if (HasStateAuthority && cutscene != null && cutscene.playOnce
                && CutsceneState.HasSeen(cutscene.cutsceneId))
                HasTriggered = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // Chỉ host quyết định — client va trigger cục bộ không được tự chiếu (sẽ lệch nhau).
            if (!HasStateAuthority || HasTriggered || cutscene == null) return;
            if (!other.CompareTag("Player")) return;

            var pc = other.GetComponentInParent<PlayerController>();
            if (pc == null || pc.isDeadNetworked) return;

            HasTriggered = true;
            RpcPlay();
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcPlay()
        {
            PlayLocal();
        }

        /// <summary>Chiếu cảnh trên máy này. Gọi được trực tiếp để test bằng nút trong Inspector.</summary>
        public void PlayLocal()
        {
            if (cutscene == null || cutscene.beats == null || cutscene.beats.Length == 0) return;
            if (_running != null) return;                 // đang chiếu → bỏ qua
            if (CutsceneState.IsPlaying) return;          // cảnh khác đang chiếu → không chồng lên
            _running = StartCoroutine(PlayRoutine());
        }

        private IEnumerator PlayRoutine()
        {
            CutsceneState.IsPlaying = true;
            DialogueState.IsActive = true;   // khoá di chuyển player (PlayerController đã đọc cờ này)

            // Đánh dấu ngay từ đầu (không đợi xem hết): bỏ qua giữa cảnh hoặc chết ngay sau đó vẫn
            // tính là đã xem — người chơi không phải xem lại cảnh mình vừa chủ động skip.
            if (cutscene.playOnce) CutsceneState.MarkSeen(cutscene.cutsceneId);

            if (fadeAtEdges) yield return SceneFader.FadeOut(fadeDuration);

            CameraFocusController.Begin();

            if (fadeAtEdges) yield return SceneFader.FadeIn(fadeDuration);

            bool skipped = false;
            foreach (var beat in cutscene.beats)
            {
                if (beat == null) continue;

                // 1. Lia camera tới mốc (nếu beat có chỉ định) và chờ camera tới gần.
                var point = ResolveFocusPoint(beat.focusPointIndex);
                if (point != null)
                {
                    CameraFocusController.Point(point.position);

                    float t = 0f;
                    while (t < CameraArriveTimeout && CameraFocusController.DistanceToPoint() > CameraArriveDistance)
                    {
                        if (WantsSkip()) { skipped = true; break; }
                        t += Time.deltaTime;
                        yield return null;
                    }
                }
                if (skipped) break;

                // 2. Chữ giữa màn hình (không chặn — banner tự fade).
                if (!string.IsNullOrEmpty(beat.bannerText)) AreaNameBanner.Show(beat.bannerText);

                // 3. Giữ camera cho người chơi kịp nhìn.
                float hold = 0f;
                while (hold < beat.holdSeconds)
                {
                    if (WantsSkip()) { skipped = true; break; }
                    hold += Time.deltaTime;
                    yield return null;
                }
                if (skipped) break;

                // 4. Hội thoại — chờ người chơi đọc hết mới sang beat sau.
                if (beat.dialogue != null && beat.dialogue.lines != null && beat.dialogue.lines.Length > 0)
                {
                    bool done = false;
                    DialogueEvents.OnOpenCustomDialogue?.Invoke(beat.dialogue, () => done = true);

                    // Thoại có mở thật không? Không mở (thiếu DialogueUI trong scene, hoặc đang có
                    // thoại khác mở nên OpenCustomDialogue return sớm) → callback KHÔNG BAO GIỜ chạy.
                    // Nếu cứ chờ, người chơi bị khoá input vĩnh viễn. Chờ 1 nhịp rồi kiểm tra cờ.
                    yield return null;
                    if (!done && !DialogueState.IsActive)
                    {
                        Debug.LogWarning($"[Cutscene] '{cutscene.cutsceneId}': không mở được thoại "
                                         + $"'{beat.dialogue.name}' (thiếu DialogueUI trong scene?) — bỏ qua nhịp thoại.");
                    }
                    else
                    {
                        // DialogueUI tự set DialogueState.IsActive; nó cũng TẮT cờ khi đóng thoại — mà
                        // cutscene vẫn cần khoá input tới hết cảnh, nên bật lại sau mỗi lần thoại xong.
                        while (!done)
                        {
                            if (WantsSkip())
                            {
                                // Đóng thoại đang mở; ForceCloseDialogue vẫn gọi callback nên done = true.
                                DialogueEvents.OnForceCloseDialogue?.Invoke();
                                skipped = true;
                                break;
                            }
                            yield return null;
                        }
                    }
                    DialogueState.IsActive = true;
                }
                if (skipped) break;
            }

            if (fadeAtEdges) yield return SceneFader.FadeOut(fadeDuration);

            CameraFocusController.End();

            if (fadeAtEdges) yield return SceneFader.FadeIn(fadeDuration);

            DialogueState.IsActive = false;
            CutsceneState.IsPlaying = false;
            _running = null;
        }

        private const float CameraArriveDistance = 0.6f;
        private const float CameraArriveTimeout = 3f;   // camera bị chặn (confiner/bounds) → vẫn đi tiếp

        /// <summary>ESC để bỏ qua, nếu CutsceneSO cho phép.</summary>
        private bool WantsSkip() => cutscene.skippable && Input.GetKeyDown(KeyCode.Escape);

        private Transform ResolveFocusPoint(int index)
        {
            if (index < 0 || focusPoints == null || index >= focusPoints.Length) return null;
            return focusPoints[index];
        }

        /// <summary>
        /// An toàn khi đổi scene / despawn giữa cảnh: nếu không trả cờ, player bị khoá input vĩnh viễn
        /// ở map mới (DialogueState.IsActive kẹt = true).
        /// </summary>
        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (_running == null) return;
            StopCoroutine(_running);
            _running = null;
            CameraFocusController.End();
            DialogueState.IsActive = false;
            CutsceneState.IsPlaying = false;
        }

        private void OnDrawGizmos()
        {
            // Vùng trigger
            if (GetComponent<Collider2D>() is BoxCollider2D box)
            {
                Gizmos.color = new Color(0.5f, 0.7f, 1f, 0.12f);
                Vector3 c = transform.position + (Vector3)box.offset;
                Vector3 s = new Vector3(box.size.x * transform.lossyScale.x, box.size.y * transform.lossyScale.y, 1f);
                Gizmos.DrawCube(c, s);
                Gizmos.color = new Color(0.5f, 0.7f, 1f, 0.9f);
                Gizmos.DrawWireCube(c, s);
            }

            // Đường lia camera qua các mốc — xem trước đường đi ngay trong Editor.
            if (focusPoints == null) return;
            Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.9f);
            for (int i = 0; i < focusPoints.Length; i++)
            {
                if (focusPoints[i] == null) continue;
                Gizmos.DrawWireSphere(focusPoints[i].position, 0.4f);
                if (i > 0 && focusPoints[i - 1] != null)
                    Gizmos.DrawLine(focusPoints[i - 1].position, focusPoints[i].position);
            }
        }
    }
}
