using Fusion;
using UnityEngine;
using Attrition.Data;
using Attrition.Gameplay.Player;
using Attrition.Gameplay.Player.Inventory;

namespace Attrition.Gameplay.NPC
{
    /// <summary>
    /// NPC controller gắn vào NPC prefab (scene-placed NetworkObject).
    /// - Quản lý trạng thái quest [Networked] (CHUNG cho cả hai player).
    /// - Cung cấp RPC để UI gọi (accept, claim reward).
    /// - Phát thưởng cho TẤT CẢ player khi hoàn thành quest.
    ///
    /// Prefab cần: NetworkObject + Collider2D (Is Trigger) cho vùng tương tác.
    /// Prompt "[F] Talk" do DialogueUI (UI Toolkit) render — PlayerController phát hiện
    /// NPC gần qua trigger và expose IsNearNPC/CurrentNPC, không cần world-canvas trên NPC.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class NetworkNPC : NetworkBehaviour
    {
        //  DATA (gán trong Inspector)

        [Header("──── NPC IDENTITY ────")]
        [Tooltip("Tên NPC hiển thị trong hội thoại.")]
        [SerializeField] private string npcName = "NPC";
        [Tooltip("Bật nếu sprite gốc của NPC quay mặt sang phải và cần lật sang trái.")]
        [SerializeField] private bool defaultFacingLeft;

        private Transform _nameLabel;
        private float _nameLabelScaleSign;

        [Header("──── QUEST (tùy chọn) ────")]
        [Tooltip("Nhiệm vụ ĐẦU TIÊN NPC giao. Bỏ trống = NPC chỉ nói chuyện thường.")]
        [SerializeField] private QuestSO quest;

        [Tooltip("Các nhiệm vụ TIẾP THEO, giao LẦN LƯỢT sau khi nhiệm vụ trước đã nhận thưởng. " +
                 "VD Summer Fairy map 2: quest = elite(burn), extraQuests[0] = boss(regen). " +
                 "Bỏ trống = NPC chỉ có 1 nhiệm vụ.")]
        [SerializeField] private QuestSO[] extraQuests;
        [Tooltip("Tự kích hoạt quest khi NPC spawn. Dùng cho objective cuối Map 5, không cần fairy giao quest.")]
        [SerializeField] private bool autoStartQuest;
        [Tooltip("Đủ objective thì chuyển thẳng Rewarded, không hiện Claim Reward và không phát thưởng.")]
        [SerializeField] private bool autoFinishWithoutReward;

        /// <summary>Đang ở nhiệm vụ thứ mấy trong chuỗi (0 = `quest`, 1.. = `extraQuests[i-1]`).</summary>
        [Networked] public byte QuestChainIndex { get; set; }

        [Header("──── IDLE DIALOGUE (không có quest) ────")]
        [Tooltip("Hội thoại mặc định khi NPC không giao quest hoặc quest đã xong.")]
        [SerializeField] private DialogueSO idleDialogue;

        [Header("──── NPC NHẬN NỘP (turn-in) ────")]
        [Tooltip("NPC mà mình NHẬN NỘP QUEST HỘ. Dùng cho bố cục 'Summer Fairy giao quest — Autumn Fairy " +
                 "nhận nộp': kéo Summer Fairy vào đây trên Autumn Fairy. Player nhận quest ở Summer, làm " +
                 "xong thì tới Autumn lấy thưởng. Bỏ trống = NPC tự nhận nộp quest của chính mình (mặc định).")]
        [SerializeField] private NetworkNPC claimForNpc;

        [Tooltip("Key mục tiêu Custom sẽ được BÁO HOÀN THÀNH khi player nói chuyện xong với NPC này. " +
                 "Dùng cho nhiệm vụ ĐƯA TIN: NPC A giao quest (objectiveType = Custom, targetId = key), " +
                 "NPC B ở nơi khác đặt key này để 'nhận tin'. Bỏ trống = NPC không nhận nộp gì.")]
        [SerializeField] private string turnInObjectiveKey = "";

        [Tooltip("Thoại khi player MANG TIN tới (quest đang Active). Bỏ trống = dùng idleDialogue.")]
        [SerializeField] private DialogueSO turnInDialogue;

        [Tooltip("Thoại sau khi ĐÃ nhận tin xong (lần nói chuyện tiếp theo). Bỏ trống = dùng idleDialogue.")]
        [SerializeField] private DialogueSO turnInDoneDialogue;

        /// <summary>Đã báo nộp rồi chưa (tránh cộng tiến độ nhiều lần khi nói chuyện lại).</summary>
        [Networked] private NetworkBool TurnInReported { get; set; }

        //  NETWORKED QUEST STATE (shared cả hai player)

        /// <summary>0=NotStarted, 1=Active, 2=Completed, 3=Rewarded</summary>
        [Networked] public byte QuestState { get; set; }

        /// <summary>Tiến độ hiện tại (VD: số quái đã giết).</summary>
        [Networked] public int QuestProgress { get; set; }
        [Networked] private int QuestTargetMask { get; set; }

        //  PUBLIC ACCESSORS (DialogueUI đọc)

        public string NpcName => npcName;
        public DialogueSO IdleDialogue => idleDialogue;

        /// <summary>Tổng số nhiệm vụ trong chuỗi của NPC này.</summary>
        private int ChainLength => (quest != null ? 1 : 0) + (extraQuests != null ? extraQuests.Length : 0);

        /// <summary>
        /// Nhiệm vụ ĐANG hoạt động trong chuỗi (theo `QuestChainIndex`). Null khi đã xong hết chuỗi.
        ///
        /// VÌ SAO CÓ CHUỖI: mỗi map cần 2 nhiệm vụ (giết elite + giết boss) nhưng NPC chỉ có 1 ô quest.
        /// Thay vì đặt 2 NPC giao quest cho mỗi map (Summer Fairy chỉ có 1), NPC giao LẦN LƯỢT: xong
        /// nhiệm vụ elite + nhận thưởng → tự chuyển sang nhiệm vụ boss.
        /// </summary>
        public QuestSO Quest
        {
            get
            {
                int i = QuestChainIndex;
                if (i == 0) return quest;
                int extraIdx = i - 1;
                if (extraQuests == null || extraIdx >= extraQuests.Length) return null;
                return extraQuests[extraIdx];
            }
        }

        /// <summary>Có quest và quest đang active?</summary>
        public bool HasActiveQuest => Quest != null && QuestState == 1;

        /// <summary>Số lượng mục tiêu cần hoàn thành.</summary>
        public int RequiredAmount => Quest != null ? Quest.requiredAmount : 0;

        /// <summary>NPC này là điểm NỘP của một nhiệm vụ đưa tin?</summary>
        public bool IsTurnInPoint => !string.IsNullOrEmpty(turnInObjectiveKey);

        /// <summary>
        /// NPC giữ QUEST mà mình đang làm việc với: bình thường là chính mình, nhưng nếu `claimForNpc` được
        /// gán thì là NPC đó (bố cục "Summer giao — Autumn nhận nộp").
        ///
        /// Mọi thứ liên quan quest (thoại theo trạng thái, nhận thưởng, tiến độ) đều đi qua đây, nên chỉ
        /// cần gán 1 ô Inspector là tách được vai trò giao/nhận mà không nhân đôi state quest.
        /// </summary>
        public NetworkNPC QuestOwner => claimForNpc != null ? claimForNpc : this;

        /// <summary>NPC này có nhận nộp hộ người khác không? (vẫn có thể TỰ giao quest riêng của mình)</summary>
        public bool IsClaimProxy => claimForNpc != null;

        /// <summary>
        /// NPC mà player phải QUAY LẠI để NỘP quest của NPC này — nghịch đảo của `claimForNpc`.
        ///
        /// VÌ SAO CẦN: chuỗi thiết kế là "Spring giao → nộp cho Summer; Summer giao quest boss → nộp cho
        /// Autumn". Mọi chỗ nhắc người chơi (toast, tracker HUD, bảng quest Tab) trước đây in `NpcName` của
        /// NPC GIỮ quest, tức là bảo quay lại Spring — trong khi Spring không nhận nộp. Người chơi đi lộn
        /// NPC rồi tưởng quest lỗi. Đây đúng là lỗi "NPC để trả nhiệm vụ không đúng".
        ///
        /// Không có ai nhận hộ → trả chính mình (NPC tự nhận nộp quest của nó, mặc định).
        /// </summary>
        public NetworkNPC TurnInNpc
        {
            get
            {
                // Cache: quan hệ này do `claimForNpc` (field Inspector) quyết định nên KHÔNG đổi lúc chạy,
                // mà getter bị gọi từ tracker HUD (làm mới mỗi 0.5s) → không cache là quét cả scene liên tục.
                if (_turnInNpc != null) return _turnInNpc;

                // Include inactive: NPC nhận nộp có thể đang tắt (vd fairy sau cửa ending Map 5). Bỏ qua nó
                // thì cache dính luôn giá trị "tự nhận nộp" sai và không bao giờ tự sửa.
                foreach (var npc in FindObjectsByType<NetworkNPC>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (npc != null && npc != this && npc.claimForNpc == this) { _turnInNpc = npc; return npc; }

                _turnInNpc = this;    // không ai nhận hộ → tự nhận nộp
                return this;
            }
        }
        private NetworkNPC _turnInNpc;

        /// <summary>Tên NPC cần quay lại để nộp quest (xem <see cref="TurnInNpc"/>).</summary>
        public string TurnInNpcName
        {
            get { var n = TurnInNpc; return n != null ? n.NpcName : npcName; }
        }

        /// <summary>
        /// Đang có việc NỘP HỘ cần xử lý: NPC mình nhận hộ đang có quest DỞ (Active) hoặc XONG MỤC TIÊU
        /// (Completed, chờ trao thưởng).
        ///
        /// VÌ SAO CẦN: chuỗi 3 chặng (Spring giao elite → Summer nhận elite + giao boss → Autumn nhận boss)
        /// nghĩa là Summer VỪA nhận nộp VỪA giao quest. Nếu cứ hễ `IsClaimProxy` là chặn giao quest thì
        /// Summer không bao giờ giao được nhiệm vụ boss. Ở đây chỉ ƯU TIÊN việc nộp hộ khi nó đang dở;
        /// xong rồi (state 3) thì Summer quay về giao quest của chính mình.
        /// </summary>
        public bool HasClaimPending
        {
            get
            {
                if (!IsClaimProxy) return false;
                byte s = QuestOwner.QuestState;
                return QuestOwner.Quest != null && (s == 1 || s == 2);
            }
        }

        /// <summary>
        /// Quest mà UI nên hiển thị khi nói chuyện với NPC này.
        ///
        /// DialogueUI đọc `Quest`/`QuestState` để quyết định hiện nút Accept hay "Claim Reward". NPC nhận
        /// nộp hộ (Autumn) không giữ quest nên đọc trực tiếp sẽ luôn null/0 → không bao giờ hiện nút nhận
        /// thưởng. Vì thế khi đang có việc nộp hộ thì trả quest của NPC kia; hết việc thì trả quest của mình.
        /// </summary>
        public QuestSO DisplayQuest => HasClaimPending ? QuestOwner.Quest : Quest;

        /// <summary>Trạng thái quest mà UI nên dùng (xem <see cref="DisplayQuest"/>).</summary>
        public byte DisplayQuestState => HasClaimPending ? QuestOwner.QuestState : QuestState;

        /// <summary>
        /// Có được mời nhận quest RIÊNG của mình không? Đang dở việc nộp hộ thì chưa — xử lý xong chặng
        /// trước đã, tránh nhồi 2 nhiệm vụ trong một lần nói chuyện.
        /// </summary>
        public bool CanOfferQuest => !HasClaimPending && Quest != null;

        /// <summary>
        /// Đang có player mang tin tới NPC này? (tồn tại NPC khác đang giữ quest Custom Active với đúng key)
        /// Dùng để chọn thoại + để biết có nên báo nộp khi kết thúc hội thoại.
        /// </summary>
        public bool HasIncomingDelivery
        {
            get
            {
                if (!IsTurnInPoint) return false;
                foreach (var npc in FindObjectsByType<NetworkNPC>(FindObjectsSortMode.None))
                {
                    if (npc == null || npc == this) continue;

                    // Đọc qua `Quest` (nhiệm vụ ĐANG hoạt động trong chuỗi), KHÔNG phải field `quest` thô —
                    // NPC có chuỗi nhiều nhiệm vụ thì field thô luôn trả nhiệm vụ ĐẦU, dò sai mục tiêu.
                    var q = npc.Quest;
                    if (q == null) continue;
                    if (npc.QuestState != 1) continue;                                  // chưa nhận / đã xong
                    if (q.objectiveType != QuestObjectiveType.Custom) continue;
                    if (q.targetId != turnInObjectiveKey) continue;
                    return true;
                }
                return false;
            }
        }

        /// <summary>Lấy DialogueSO phù hợp với trạng thái quest hiện tại.</summary>
        public DialogueSO GetCurrentDialogue()
        {
            // ── ĐANG CÓ VIỆC NỘP HỘ (Summer nhận elite của Spring / Autumn nhận boss của Summer) ──
            // Đọc trạng thái quest của NPC GIAO, không phải của mình. Dùng `HasClaimPending` chứ không phải
            // `IsClaimProxy`: xong việc nộp hộ rồi thì NPC quay về giao quest RIÊNG của nó (Summer giao boss).
            // Đọc qua `owner.Quest` (nhiệm vụ đang hoạt động trong chuỗi), KHÔNG phải field `quest` thô.
            if (HasClaimPending)
            {
                var ownerQuest = QuestOwner.Quest;
                switch (QuestOwner.QuestState)
                {
                    case 1: return ownerQuest.dialogueInProgress ?? idleDialogue;
                    case 2: return ownerQuest.dialogueCompleted ?? idleDialogue;
                    default: return idleDialogue;
                }
            }

            // NPC NHẬN TIN (nhiệm vụ đưa tin): không tự giao quest, thoại phụ thuộc việc player có đang
            // mang tin hay không. Xét TRƯỚC quest riêng để 1 NPC vừa giao quest của mình vừa nhận tin của
            // NPC khác vẫn đúng.
            if (IsTurnInPoint && Quest == null)
            {
                if (TurnInReported) return turnInDoneDialogue ?? idleDialogue;
                if (HasIncomingDelivery) return turnInDialogue ?? idleDialogue;
                return idleDialogue;
            }

            var active = Quest;
            if (active == null) return idleDialogue;

            switch (QuestState)
            {
                case 0: return active.dialogueNotStarted ?? idleDialogue;
                case 1: return active.dialogueInProgress ?? idleDialogue;
                case 2: return active.dialogueCompleted ?? idleDialogue;
                case 3: return active.dialogueFinished ?? idleDialogue;
                default: return idleDialogue;
            }
        }

        /// <summary>
        /// Player NÓI CHUYỆN XONG với NPC này → nếu đây là điểm nộp và đang có người mang tin thì báo
        /// hoàn thành mục tiêu. DialogueUI gọi khi đóng hội thoại.
        ///
        /// Vì sao báo lúc KẾT THÚC hội thoại, không phải lúc mở: yêu cầu là "nhận được khi nói chuyện xong
        /// với NPC cuối map" — báo sớm thì quest hoàn thành trước khi player đọc hết lời thoại.
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RpcReportTurnIn()
        {
            if (!HasStateAuthority || !IsTurnInPoint) return;
            if (TurnInReported) return;              // đã báo rồi → không cộng tiến độ lần nữa
            if (!HasIncomingDelivery) return;        // player chưa nhận quest đưa tin

            TurnInReported = true;
            NotifyCustomObjective(turnInObjectiveKey);
        }


        public override void Spawned()
        {
            if (defaultFacingLeft)
            {
                var scale = transform.localScale;
                transform.localScale = new Vector3(-Mathf.Abs(scale.x), scale.y, scale.z);
            }

            // Nhãn tên nổi trên đầu NPC (world-space, mọi máy thấy giống nhau). Vàng kim cho NPC.
            string display = string.IsNullOrEmpty(npcName) ? "NPC" : npcName;
            var label = Attrition.Gameplay.WorldNameLabel.Attach(
                transform, display, new Vector3(0f, 0.95f, 0f), new Color(0.91f, 0.78f, 0.41f), 3f);
            _nameLabel = label != null ? label.transform : null;
            UpdateNameLabelFacing();

            // Host khôi phục tiến trình quest đã lưu (solo local). Mỗi NPC tự đọc questId của mình
            // → không lệ thuộc thứ tự spawn. Online: server là nguồn, không nạp từ slot.
            RestoreSavedProgress();
            if (HasStateAuthority && autoStartQuest && Quest != null && QuestState == 0)
            {
                QuestState = 1;
                CreditAlreadyDefeatedBosses();   // boss có thể đã hạ trước khi quest tự bật
            }
        }

        private void LateUpdate()
        {
            UpdateNameLabelFacing();
        }

        private void UpdateNameLabelFacing()
        {
            if (_nameLabel == null) return;

            float sign = transform.localScale.x < 0f ? -1f : 1f;
            if (sign == _nameLabelScaleSign) return;

            _nameLabelScaleSign = sign;
            var scale = _nameLabel.localScale;
            _nameLabel.localScale = new Vector3(Mathf.Abs(scale.x) * sign, scale.y, scale.z);
        }

        /// <summary>
        /// Host nạp lại state/progress của quest NPC này khi spawn. Mỗi NPC tự đọc questId của mình
        /// → không lệ thuộc thứ tự spawn. Solo: từ save slot. Online: từ holder CoopQuestsJson host đã
        /// fetch về (host-authoritative). No-op nếu chưa có tiến trình.
        /// </summary>
        private void RestoreSavedProgress()
        {
            if (!HasStateAuthority || ChainLength == 0) return;

            Attrition.Persistence.QuestProgressEntry[] entries;
            if (Attrition.Persistence.GameLaunch.IsOnline)
            {
                entries = ParseQuestsJson(Attrition.Persistence.GameLaunch.CoopQuestsJson);
            }
            else
            {
                var data = Attrition.Persistence.SaveManager.LoadSlot(Attrition.Persistence.GameLaunch.SelectedSlot);
                entries = data?.quests;
            }
            if (entries == null) return;

            // Duyệt CẢ CHUỖI: save lưu theo questId nên phải tìm xem đang dở ở nhiệm vụ thứ mấy.
            // Nhiệm vụ đã Rewarded (state 3) → nhảy sang nhiệm vụ kế tiếp trong chuỗi.
            for (int i = 0; i < ChainLength; i++)
            {
                var q = QuestAt(i);
                if (q == null || string.IsNullOrEmpty(q.questId)) continue;

                var found = FindEntry(entries, q.questId);
                if (found == null) break;      // chưa từng nhận nhiệm vụ này → dừng ở đây

                QuestChainIndex = (byte)i;
                QuestState = found.state;
                QuestProgress = DecodeProgress(Quest, found.progress);
                QuestTargetMask = DecodeTargetMask(Quest, found.progress, found.targetMask);

                if (found.state != 3) return;  // đang dở → giữ nguyên vị trí chuỗi

                // Đã nhận thưởng xong: sang nhiệm vụ kế (nếu còn).
                if (i + 1 < ChainLength)
                {
                    QuestChainIndex = (byte)(i + 1);
                    QuestState = 0;
                    QuestProgress = 0;
                QuestTargetMask = 0;
                }
            }
        }

        private static bool IsMultiTarget(QuestSO q) =>
            q != null && q.requiredTargetIds != null && q.requiredTargetIds.Length > 0;

        // Keep backend compatibility: world-state only stores one progress integer.
        // Low byte = visible progress, remaining bits = completed target mask.
        private static int EncodeProgress(QuestSO q, int progress, int targetMask) =>
            IsMultiTarget(q) ? (progress & 0xff) | (targetMask << 8) : progress;

        private static int DecodeProgress(QuestSO q, int saved) =>
            IsMultiTarget(q) ? saved & 0xff : saved;

        private static int DecodeTargetMask(QuestSO q, int saved, int explicitMask) =>
            !IsMultiTarget(q) ? 0 : explicitMask != 0 ? explicitMask : saved >> 8;

        private static Attrition.Persistence.QuestProgressEntry FindEntry(
            Attrition.Persistence.QuestProgressEntry[] entries, string questId)
        {
            foreach (var e in entries)
                if (e != null && e.questId == questId) return e;
            return null;
        }

        /// <summary>Nhiệm vụ thứ i trong chuỗi (0 = `quest`, 1.. = `extraQuests`). Null nếu ngoài phạm vi.</summary>
        private QuestSO QuestAt(int i)
        {
            if (i == 0) return quest;
            int extraIdx = i - 1;
            if (extraQuests == null || extraIdx < 0 || extraIdx >= extraQuests.Length) return null;
            return extraQuests[extraIdx];
        }

        private static Attrition.Persistence.QuestProgressEntry[] ParseQuestsJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var parsed = JsonUtility.FromJson<Attrition.Persistence.QuestProgressList>(json);
                return parsed?.quests;
            }
            catch { return null; }
        }


        /// <summary>Player nhấn Accept trong UI → nhận quest.</summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RpcAcceptQuest()
        {
            if (Quest == null || QuestState != 0) return;
            QuestState = 1; // Active
            QuestProgress = 0;
            QuestTargetMask = 0;
            CreditAlreadyDefeatedBosses();
        }

        /// <summary>
        /// Vừa nhận quest → cộng NGAY tiến độ cho boss ĐÃ HẠ TRƯỚC KHI nhận.
        ///
        /// VÌ SAO CẦN: <see cref="NotifyEnemyKilled"/> chỉ cộng cho quest đang Active (state 1), mà boss đã
        /// hạ thì KHÔNG BAO GIỜ hồi sinh (BossDefeatState + BossGateController despawn xác lúc Spawned).
        /// Nên nếu player giết boss TRƯỚC khi nhận quest boss thì cú giết đó mất trắng: quest kẹt vĩnh viễn
        /// ở 0/1, không còn cách nào hoàn thành → không có Claim Reward, không popup, không skill/item.
        /// Đây đúng là lỗi "đã đánh chết boss rồi mà nhiệm vụ báo chưa tiêu diệt được".
        ///
        /// Rất dễ gặp vì chuỗi 3 chặng: Summer chỉ giao được quest boss SAU khi quest elite của Spring đã
        /// nộp xong, nhưng phòng boss thì mở sẵn — player nào đánh boss trước khi đi hết chuỗi fairy đều dính.
        ///
        /// Chỉ dựa vào BossDefeatState (chỉ ghi BOSS), nên quái thường/elite vẫn phải giết sau khi nhận quest.
        /// </summary>
        private void CreditAlreadyDefeatedBosses()
        {
            var q = Quest;
            if (q == null || q.objectiveType != QuestObjectiveType.Kill) return;

            // Solo: đảm bảo danh sách boss đã hạ được nạp từ save (thứ tự Spawned không đảm bảo).
            Attrition.Gameplay.Environment.BossDefeatState.EnsureLoadedForSolo();

            if (q.requiredTargetIds != null && q.requiredTargetIds.Length > 0)
            {
                int n = Mathf.Min(q.requiredTargetIds.Length, 31);   // mask chỉ có 31 bit dùng được
                for (int i = 0; i < n; i++)
                {
                    if (!Attrition.Gameplay.Environment.BossDefeatState.IsDefeated(q.requiredTargetIds[i])) continue;
                    int bit = 1 << i;
                    if ((QuestTargetMask & bit) != 0) continue;
                    QuestTargetMask |= bit;
                    QuestProgress++;
                }
            }
            else if (Attrition.Gameplay.Environment.BossDefeatState.IsDefeated(q.targetId))
            {
                // Mục tiêu đơn: boss là duy nhất toàn game nên hạ rồi = đủ số lượng.
                QuestProgress = q.requiredAmount;
            }

            if (QuestProgress >= q.requiredAmount)
            {
                QuestState = autoFinishWithoutReward ? (byte)3 : (byte)2;
                if (QuestState == 2) RpcNotifyObjectiveComplete(q.title);
            }
        }

        /// <summary>Player nhấn Decline → không nhận quest, đóng dialogue.</summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RpcDeclineQuest()
        {
            // Không làm gì — quest vẫn NotStarted, player có thể quay lại sau.
        }

        /// <summary>
        /// Player quay lại NPC khi quest đã hoàn thành → nhận thưởng.
        ///
        /// Đang có việc NỘP HỘ (Summer nhận elite của Spring / Autumn nhận boss của Summer) thì chuyển tiếp
        /// sang NPC giữ quest — state/thưởng nằm ở đó, không nhân đôi sang mình. Dùng `HasClaimPending` chứ
        /// không phải `IsClaimProxy`: xong việc nộp hộ rồi thì NPC tự nhận quest RIÊNG của nó.
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RpcClaimReward()
        {
            if (HasClaimPending)
            {
                QuestOwner.ClaimRewardLocal();
                return;
            }
            ClaimRewardLocal();
        }

        /// <summary>Phần thực thi của việc nhận thưởng (host). Tách ra để proxy gọi trực tiếp, không qua RPC lần 2.</summary>
        private void ClaimRewardLocal()
        {
            if (!HasStateAuthority) return;
            if (Quest == null || QuestState != 2) return;

            QuestState = 3; // Rewarded
            DistributeRewards();

            // CHUỖI QUEST: nhận thưởng xong thì mở nhiệm vụ KẾ TIẾP (nếu còn). Nhờ vậy Summer Fairy giao
            // được cả 2 nhiệm vụ của map (elite → boss) mà không cần 2 NPC.
            // Phải chạy SAU DistributeRewards vì hàm đó đọc `Quest` (nhiệm vụ vừa xong) để trao thưởng.
            if (QuestChainIndex + 1 < ChainLength)
            {
                QuestChainIndex = (byte)(QuestChainIndex + 1);
                QuestState = 0;      // nhiệm vụ mới: chờ player nhận
                QuestProgress = 0;
                QuestTargetMask = 0;
            }
        }

        //  QUEST PROGRESS — Host xử lý

        /// <summary>
        /// Static: gọi khi quái chết (EnemyController.DieFinal).
        /// Host duyệt tất cả NPC, cộng tiến độ nếu enemyId khớp.
        /// </summary>
        public static void NotifyEnemyKilled(string enemyId)
        {
            if (string.IsNullOrEmpty(enemyId)) return;
            var npcs = FindObjectsByType<NetworkNPC>(FindObjectsSortMode.None);
            foreach (var npc in npcs)
            {
                // Chưa Spawned → [Networked] chưa đọc được (HasStateAuthority cũng ném).
                if (npc == null || npc.Object == null || !npc.Object.IsValid) continue;
                if (!npc.HasStateAuthority) continue;
                var active = npc.Quest;
                if (active == null || npc.QuestState != 1) continue;
                if (active.objectiveType != QuestObjectiveType.Kill) continue;

                if (active.requiredTargetIds != null && active.requiredTargetIds.Length > 0)
                {
                    int targetIndex = System.Array.IndexOf(active.requiredTargetIds, enemyId);
                    if (targetIndex < 0 || targetIndex >= 31) continue;
                    int bit = 1 << targetIndex;
                    if ((npc.QuestTargetMask & bit) != 0) continue;
                    npc.QuestTargetMask |= bit;
                    npc.QuestProgress++;
                }
                else
                {
                    if (active.targetId != enemyId) continue;
                    npc.QuestProgress++;
                }

                if (npc.QuestProgress >= active.requiredAmount)
                {
                    npc.QuestState = npc.autoFinishWithoutReward ? (byte)3 : (byte)2;
                    if (npc.QuestState == 2) npc.RpcNotifyObjectiveComplete(active.title);
                }
            }
        }

        /// <summary>
        /// Static: gọi bởi PuzzleController/Switch khi mục tiêu tùy chỉnh hoàn thành.
        /// VD: NetworkNPC.NotifyCustomObjective("puzzle_gate_1");
        /// </summary>
        public static void NotifyCustomObjective(string objectiveKey)
        {
            if (string.IsNullOrEmpty(objectiveKey)) return;
            var npcs = FindObjectsByType<NetworkNPC>(FindObjectsSortMode.None);
            foreach (var npc in npcs)
            {
                if (!npc.HasStateAuthority) continue;
                var active = npc.Quest;
                if (active == null || npc.QuestState != 1) continue;
                if (active.objectiveType != QuestObjectiveType.Custom) continue;
                if (active.targetId != objectiveKey) continue;

                npc.QuestProgress++;
                if (npc.QuestProgress >= active.requiredAmount)
                {
                    npc.QuestState = npc.autoFinishWithoutReward ? (byte)3 : (byte)2;
                    if (npc.QuestState == 2) npc.RpcNotifyObjectiveComplete(active.title);
                }
            }
        }

        /// <summary>
        /// Host → MỌI máy: quest vừa đủ mục tiêu, chờ nộp. Toast là UI CỤC BỘ của từng người chơi nên phải
        /// đi bằng RPC; nếu để mỗi peer tự suy ra từ `QuestState` thì client bỏ lỡ đúng khoảnh khắc chuyển
        /// trạng thái (nó chỉ thấy giá trị mới, không thấy lúc đổi).
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcNotifyObjectiveComplete(string questTitle)
        {
            // Tên NPC NHẬN NỘP (không phải NPC giao) — xem TurnInNpc.
            Attrition.Controllers.QuestNotifyEvents.RaiseObjectiveComplete(questTitle, TurnInNpcName);
        }

        //  SAVE / LOAD — Host gom & khôi phục tiến trình quest

        /// <summary>
        /// Host gom trạng thái quest của MỌI NPC trong scene để lưu vào save slot.
        /// Chỉ lưu NPC có quest và đã có tiến triển (state>0) để file gọn.
        /// </summary>
        public static Attrition.Persistence.QuestProgressEntry[] CaptureAll()
        {
            var npcs = FindObjectsByType<NetworkNPC>(FindObjectsSortMode.None);
            var list = new System.Collections.Generic.List<Attrition.Persistence.QuestProgressEntry>();
            foreach (var npc in npcs)
            {
                if (npc == null || npc.ChainLength == 0) continue;

                // Lưu CẢ CHUỖI: nhiệm vụ đã xong trước đó (index < QuestChainIndex) phải được ghi state 3,
                // nếu không lần load sau `RestoreSavedProgress` sẽ dừng ở nhiệm vụ đầu và player bị giao lại
                // nhiệm vụ đã hoàn thành.
                for (int i = 0; i <= npc.QuestChainIndex && i < npc.ChainLength; i++)
                {
                    var q = npc.QuestAt(i);
                    if (q == null || string.IsNullOrEmpty(q.questId)) continue;

                    bool isCurrent = i == npc.QuestChainIndex;
                    byte state = isCurrent ? npc.QuestState : (byte)3;   // nhiệm vụ trước = đã nhận thưởng
                    if (isCurrent && state == 0) continue;               // chưa nhận → khỏi lưu

                    list.Add(new Attrition.Persistence.QuestProgressEntry
                    {
                        questId = q.questId,
                        state = state,
                        progress = isCurrent
                            ? EncodeProgress(q, npc.QuestProgress, npc.QuestTargetMask)
                            : q.requiredAmount,
                        targetMask = isCurrent ? npc.QuestTargetMask : 0
                    });
                }
            }
            return list.ToArray();
        }

        /// <summary>Host gom quest mọi NPC → JSON (gửi lên server coop). Null nếu không có gì để lưu.</summary>
        public static string CaptureAllJson()
        {
            var arr = CaptureAll();
            if (arr == null || arr.Length == 0) return null;
            return JsonUtility.ToJson(new Attrition.Persistence.QuestProgressList { quests = arr });
        }

        /// <summary>
        /// Host (coop) áp tiến trình quest từ JSON server vào các NPC khớp questId. Gọi một lần khi
        /// vào room — NPC nào không khớp giữ nguyên NotStarted. No-op nếu JSON rỗng.
        /// </summary>
        public static void ApplyAllJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            Attrition.Persistence.QuestProgressList parsed;
            try { parsed = JsonUtility.FromJson<Attrition.Persistence.QuestProgressList>(json); }
            catch { return; }
            if (parsed?.quests == null || parsed.quests.Length == 0) return;

            var npcs = FindObjectsByType<NetworkNPC>(FindObjectsSortMode.None);
            foreach (var npc in npcs)
            {
                if (npc == null || !npc.HasStateAuthority || npc.ChainLength == 0) continue;

                // Duyệt CẢ CHUỖI giống RestoreSavedProgress (solo): nhiệm vụ đã Rewarded → nhảy sang cái kế,
                // nếu không client vào room sẽ bị giao lại nhiệm vụ đã hoàn thành.
                for (int i = 0; i < npc.ChainLength; i++)
                {
                    var q = npc.QuestAt(i);
                    if (q == null || string.IsNullOrEmpty(q.questId)) continue;

                    var found = FindEntry(parsed.quests, q.questId);
                    if (found == null) break;      // chưa từng nhận → dừng ở đây

                    npc.QuestChainIndex = (byte)i;
                    npc.QuestState = found.state;
                    npc.QuestProgress = found.progress;

                    if (found.state != 3) break;   // đang dở → giữ nguyên vị trí chuỗi

                    if (i + 1 < npc.ChainLength)
                    {
                        npc.QuestChainIndex = (byte)(i + 1);
                        npc.QuestState = 0;
                        npc.QuestProgress = 0;
                    }
                }
            }
        }

        //  REWARD DISTRIBUTION — Host

        private void DistributeRewards()
        {
            if (!HasStateAuthority || Quest == null) return;
            var db = ItemDatabaseSO.Instance;

            // EXP → cộng cho TẤT CẢ player (giống cơ chế coop khi quái chết)
            if (Quest.expReward > 0)
            {
                var progressions = FindObjectsByType<PlayerProgression>(FindObjectsSortMode.None);
                foreach (var p in progressions)
                    if (p != null) p.GainExp(Quest.expReward);
            }

            // Items → thêm thẳng vào Inventory CẢ HAI player
            if (Quest.itemRewards != null && db != null)
            {
                var inventories = FindObjectsByType<PlayerInventory>(FindObjectsSortMode.None);
                foreach (var reward in Quest.itemRewards)
                {
                    if (string.IsNullOrEmpty(reward.itemId) || reward.amount <= 0) continue;
                    int idx = db.GetIndex(reward.itemId);
                    if (idx < 0) continue;
                    if (IsCoopOnlyReward(db, idx)) continue;   // solo: bỏ qua món chỉ có ở coop
                    foreach (var inv in inventories)
                        if (inv != null) inv.AddItemOrDrop(idx, reward.amount);
                }
            }

            // Fire event → DialogueUI hiện popup "Congratulations!" trên tất cả client.
            // TRUYỀN chỉ số chuỗi: `ClaimRewardLocal` nhảy sang nhiệm vụ KẾ TIẾP ngay sau hàm này, còn RPC
            // tới client MUỘN HƠN → nếu client đọc `Quest` nó sẽ lấy nhiệm vụ mới và hiện popup SAI món.
            RpcNotifyRewards(QuestChainIndex);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcNotifyRewards(byte chainIndex)
        {
            // Đọc theo chỉ số host gửi kèm, KHÔNG dùng `Quest` (xem ghi chú ở DistributeRewards).
            var claimed = QuestAt(chainIndex);
            if (claimed == null) return;

            // Fire local events để DialogueUI hiện popup
            if (claimed.expReward > 0)
                RewardEvents.NotifyExp(claimed.expReward);

            var db = ItemDatabaseSO.Instance;
            if (claimed.itemRewards != null && db != null)
            {
                foreach (var reward in claimed.itemRewards)
                {
                    if (string.IsNullOrEmpty(reward.itemId) || reward.amount <= 0) continue;
                    int idx = db.GetIndex(reward.itemId);
                    // Solo: KHÔNG hiện popup cho món coop-only, nếu không player thấy "nhận được X" mà túi
                    // trống (DistributeRewards đã bỏ qua món đó).
                    if (idx >= 0 && IsCoopOnlyReward(db, idx)) continue;
                    RewardEvents.NotifyItem(reward.itemId, reward.amount);
                }
            }

            RewardEvents.NotifyBatchComplete();
        }

        /// <summary>
        /// Món này là accessory CHỈ CÓ Ở COOP và ta đang chơi SOLO?
        ///
        /// Theo yêu cầu: nhiệm vụ vẫn hiện ở CẢ HAI chế độ (để tiến trình/lore không lệch), nhưng phần
        /// thưởng của một số accessory (shield/slow/burn/lifesteal/acc_potion) chỉ trao khi chơi coop.
        /// Gate ở đây — nơi duy nhất trao đồ — thay vì nhân đôi điều kiện ra từng quest asset.
        /// </summary>
        private static bool IsCoopOnlyReward(ItemDatabaseSO db, int itemIndex)
        {
            if (Attrition.Persistence.GameLaunch.Mode == Attrition.Persistence.LaunchMode.Coop) return false;
            return db.GetItem(itemIndex) is AccessorySO acc && acc.coopOnlyReward;
        }
    }
}
