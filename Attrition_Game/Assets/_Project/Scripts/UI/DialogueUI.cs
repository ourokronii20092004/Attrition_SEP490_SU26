using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Attrition.Data;
using Attrition.Gameplay.NPC;

namespace Attrition.UI
{
    /// <summary>
    /// Controller cho toàn bộ UI hội thoại NPC + Reward popup + Quest tracker HUD.
    /// Gắn lên 1 GameObject riêng có UIDocument (TÁCH khỏi GameUIController).
    ///
    /// Chức năng:
    /// 1. Dialogue panel: typewriter, nút Accept/Decline/Continue, auto-advance bằng F/Space.
    /// 2. Reward popup: "Congratulations!" + ảnh item + EXP, hiệu ứng pop-in từng item.
    /// 3. Quest tracker: bên phải HUD, hiện quest đang active với progress bar.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class DialogueUI : MonoBehaviour
    {
        public static DialogueUI Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance._isDialogueOpen;


        [Header("──── TYPEWRITER ────")]
        [Tooltip("Số ký tự hiện mỗi giây (typewriter speed).")]
        [SerializeField] private float charsPerSecond = 40f;

        [Header("──── REWARD POPUP ────")]
        [Tooltip("Delay giữa các item pop-in (giây).")]
        [SerializeField] private float rewardItemDelay = 0.25f;

        [Header("──── QUEST TRACKER ────")]
        [Tooltip("Tần suất refresh quest tracker (giây).")]
        [SerializeField] private float trackerRefreshInterval = 0.5f;


        private VisualElement _root;

        private VisualElement _dialogueOverlay, _dialoguePanel, _questInfo, _dialogueButtons;
        private Label _speakerName, _dialogueText, _questTitle, _questDesc, _keyHint;
        private Button _btnAccept, _btnDecline, _btnContinue;

        private VisualElement _rewardOverlay, _rewardPanel, _rewardItems;
        private Label _rewardTitle, _rewardExp;
        private Button _btnRewardClose;

        private VisualElement _questTracker, _trackerList;

        // Interact prompt ("[F] Talk")
        private VisualElement _interactPrompt;
        private Label _interactKey, _interactLabel;
        private bool _promptShown;


        private bool _isDialogueOpen;
        private NetworkNPC _currentNPC;
        private PlayerController _protectedPlayer;
        private DialogueSO _currentDialogue;
        private int _currentLineIndex;
        private bool _isTyping;
        private string _fullText;
        private int _charCount;
        private float _typeTimer;
        private System.Action _onDialogueComplete;

        // Reward popup
        private readonly List<RewardEntry> _pendingRewards = new();
        private int _pendingExp;
        private bool _isRewardShowing;

        // Quest tracker
        private float _trackerTimer;

        private struct RewardEntry
        {
            public string itemId;
            public int amount;
        }


        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            var doc = GetComponent<UIDocument>();
            _root = doc.rootVisualElement;
            if (_root == null) return;

            // Dialogue elements
            _dialogueOverlay = _root.Q<VisualElement>("dialogue-overlay");
            _dialoguePanel = _root.Q<VisualElement>("dialogue-panel");
            _speakerName = _root.Q<Label>("speaker-name");
            _dialogueText = _root.Q<Label>("dialogue-text");
            _questInfo = _root.Q<VisualElement>("quest-info");
            _questTitle = _root.Q<Label>("quest-title");
            _questDesc = _root.Q<Label>("quest-desc");
            _dialogueButtons = _root.Q<VisualElement>("dialogue-buttons");
            _btnAccept = _root.Q<Button>("btn-accept");
            _btnDecline = _root.Q<Button>("btn-decline");
            _btnContinue = _root.Q<Button>("btn-continue");
            _keyHint = _root.Q<Label>("key-hint");

            // Reward elements
            _rewardOverlay = _root.Q<VisualElement>("reward-overlay");
            _rewardPanel = _root.Q<VisualElement>("reward-panel");
            _rewardItems = _root.Q<VisualElement>("reward-items");
            _rewardTitle = _root.Q<Label>("reward-title");
            _rewardExp = _root.Q<Label>("reward-exp");
            _btnRewardClose = _root.Q<Button>("btn-reward-close");

            // Quest tracker elements
            _questTracker = _root.Q<VisualElement>("quest-tracker");
            _trackerList = _root.Q<VisualElement>("tracker-list");

            // Interact prompt elements
            _interactPrompt = _root.Q<VisualElement>("interact-prompt");
            _interactKey = _root.Q<Label>("interact-key");
            _interactLabel = _root.Q<Label>("interact-label");

            _btnAccept.clicked += OnAcceptClicked;
            _btnDecline.clicked += OnDeclineClicked;
            _btnContinue.clicked += AdvanceLine;
            _btnRewardClose.clicked += CloseRewardPopup;

            RewardEvents.OnItemReceived += OnItemReceived;
            RewardEvents.OnExpReceived += OnExpReceived;
            RewardEvents.OnRewardBatchComplete += OnRewardBatchComplete;

            Attrition.Data.DialogueEvents.OnOpenCustomDialogue += OpenCustomDialogue;
        }

        private void OnDisable()
        {
            Attrition.Data.DialogueEvents.OnOpenCustomDialogue -= OpenCustomDialogue;

            if (_btnAccept != null) _btnAccept.clicked -= OnAcceptClicked;
            if (_btnDecline != null) _btnDecline.clicked -= OnDeclineClicked;
            if (_btnContinue != null) _btnContinue.clicked -= AdvanceLine;
            if (_btnRewardClose != null) _btnRewardClose.clicked -= CloseRewardPopup;

            RewardEvents.OnItemReceived -= OnItemReceived;
            RewardEvents.OnExpReceived -= OnExpReceived;
            RewardEvents.OnRewardBatchComplete -= OnRewardBatchComplete;

            // Thoát/đổi scene lúc đang mở thoại: hạ CẢ hai cờ rồi tính lại → không để kẹt
            // DialogueState.IsActive (khóa di chuyển) hay solo-freeze (game đứng hình) sang phiên sau.
            _isDialogueOpen = false;
            _isRewardShowing = false;
            RefreshBlockingState();
            SetLocalQuestProtection(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        //  UPDATE — F key + typewriter + tracker

        private void Update()
        {
            KeyCode interactKey = Attrition.Persistence.GameSettings.GetKey(
                Attrition.Persistence.GameSettings.InputAction.Interact);

            if (!_isDialogueOpen && !_isRewardShowing && Input.GetKeyDown(interactKey))
            {
                TryOpenFromNearbyNPC();
                // Mở xong return ngay: tránh cùng frame rơi vào khối advance bên dưới
                // (bug cũ: dòng đầu bị bỏ qua typewriter mỗi lần mở).
                return;
            }

            // Đang ở bảng mời nhận quest? Phải tính TRƯỚC khi phân nhánh: OnAcceptClicked/OnDeclineClicked
            // đều gọi CloseDialogue() làm nút bị ẩn lại, đọc sau sẽ luôn ra false.
            bool choosing = _isDialogueOpen && !_isTyping
                            && _btnAccept != null && !_btnAccept.ClassListContains("hidden");

            // N = từ chối quest. Chỉ nhận khi bảng mời đang hiện, nên không đụng luồng thoại thường.
            if (choosing && Input.GetKeyDown(KeyCode.N))
            {
                OnDeclineClicked();
                return;
            }

            if (_isDialogueOpen
                && (Input.GetKeyDown(interactKey) || Input.GetKeyDown(KeyCode.Space)))
            {
                if (_isTyping)
                    CompleteTyping();
                else if (choosing)
                    OnAcceptClicked();   // ở bảng mời: F = Accept thay vì lật sang dòng kế
                else
                    AdvanceLine();
            }

            UpdateInteractPrompt(interactKey);

            if (_isTyping)
            {
                _typeTimer += Time.unscaledDeltaTime;
                int targetChars = Mathf.FloorToInt(_typeTimer * charsPerSecond);
                if (targetChars > _charCount)
                {
                    _charCount = Mathf.Min(targetChars, _fullText.Length);
                    _dialogueText.text = _fullText.Substring(0, _charCount);
                    if (_charCount >= _fullText.Length)
                        CompleteTyping();
                }
            }

            _trackerTimer += Time.unscaledDeltaTime;
            if (_trackerTimer >= trackerRefreshInterval)
            {
                _trackerTimer = 0f;
                RefreshQuestTracker();
            }
        }

        //  DIALOGUE — Open / Advance / Close

        /// <summary>Tìm NPC gần (qua PlayerController.CurrentNPC) và mở hội thoại.</summary>
        private void TryOpenFromNearbyNPC()
        {
            var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            PlayerController local = null;
            foreach (var p in players)
            {
                if (p.Object != null && p.Object.HasInputAuthority)
                { local = p; break; }
            }
            if (local == null || !local.IsNearNPC) return;

            var npc = local.CurrentNPC;
            if (npc == null) return;

            OpenDialogue(npc);
        }

        /// <summary>
        /// Hiện/ẩn prompt "[F] Talk" theo NPC gần local player. Ẩn khi đang thoại/reward.
        /// Badge phím + tên NPC cập nhật động (theo keybind người chơi đã đổi).
        /// </summary>
        private void UpdateInteractPrompt(KeyCode interactKey)
        {
            if (_interactPrompt == null) return;

            NetworkNPC nearby = null;
            if (!_isDialogueOpen && !_isRewardShowing)
            {
                var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
                foreach (var p in players)
                {
                    if (p.Object != null && p.Object.HasInputAuthority)
                    {
                        if (p.IsNearNPC) nearby = p.CurrentNPC;
                        break;
                    }
                }
            }

            bool shouldShow = nearby != null;
            if (shouldShow)
            {
                if (_interactKey != null) _interactKey.text = FormatKey(interactKey);
                if (_interactLabel != null)
                {
                    string n = nearby.NpcName;
                    _interactLabel.text = string.IsNullOrEmpty(n) ? "Talk" : $"Talk to {n}";
                }
            }

            if (shouldShow == _promptShown) return; // không spam toggle class mỗi frame
            _promptShown = shouldShow;

            if (shouldShow)
            {
                _interactPrompt.RemoveFromClassList("hidden");
                _interactPrompt.schedule.Execute(() => _interactPrompt.AddToClassList("visible")).ExecuteLater(10);
            }
            else
            {
                _interactPrompt.RemoveFromClassList("visible");
                _interactPrompt.schedule.Execute(() =>
                {
                    if (!_promptShown) _interactPrompt.AddToClassList("hidden");
                }).ExecuteLater(250);
            }
        }

        /// <summary>Rút gọn KeyCode thành nhãn badge dễ đọc (Space→SPACE, LeftShift→SHIFT…).</summary>
        private static string FormatKey(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.Space: return "SPACE";
                case KeyCode.LeftShift: case KeyCode.RightShift: return "SHIFT";
                case KeyCode.LeftControl: case KeyCode.RightControl: return "CTRL";
                case KeyCode.Tab: return "TAB";
                case KeyCode.Return: return "ENTER";
                default: return key.ToString().ToUpperInvariant();
            }
        }

        /// <summary>Phím Interact hiện tại. Đọc từ GameSettings vì player rebind được — KHÔNG ghi cứng "F".</summary>
        private static KeyCode InteractKey => Attrition.Persistence.GameSettings.GetKey(
            Attrition.Persistence.GameSettings.InputAction.Interact);

        /// <summary>Đổi dòng gợi ý phím dưới khung thoại. UXML ghi cứng "[F] or [Space]" nên phải ghi lại
        /// theo ngữ cảnh, nếu không bảng mời quest sẽ hiện nhầm hướng dẫn.</summary>
        private void SetKeyHint(string text)
        {
            if (_keyHint != null) _keyHint.text = text;
        }

        private void ResetKeyHint() =>
            SetKeyHint($"[{FormatKey(InteractKey)}] or [SPACE] to continue");

        /// <summary>Mở hội thoại với NPC cụ thể.</summary>
        public void OpenDialogue(NetworkNPC npc)
        {
            if (_isDialogueOpen || npc == null) return;

            _currentNPC = npc;
            _currentDialogue = npc.GetCurrentDialogue();
            if (_currentDialogue == null || _currentDialogue.lines == null || _currentDialogue.lines.Length == 0)
                return;
            if (_currentNPC.DisplayQuest != null) SetLocalQuestProtection(true);

            _currentLineIndex = -1;
            _isDialogueOpen = true;
            _onDialogueComplete = null;
            RefreshBlockingState();

            _dialogueOverlay.RemoveFromClassList("hidden");
            // Delay 1 frame để CSS transition chạy từ trạng thái ẩn → hiện
            _dialoguePanel.schedule.Execute(() => _dialoguePanel.AddToClassList("visible")).ExecuteLater(20);

            // Quest info
            UpdateQuestInfoVisibility();

            AdvanceLine();
        }

        /// <summary>Mở hội thoại tuỳ chỉnh (dành cho Boss hoặc Event không có NPC).</summary>
        public void OpenCustomDialogue(DialogueSO dialogue, System.Action onComplete = null)
        {
            if (_isDialogueOpen || dialogue == null || dialogue.lines == null || dialogue.lines.Length == 0) return;

            _currentNPC = null;
            _currentDialogue = dialogue;
            _currentLineIndex = -1;
            _isDialogueOpen = true;
            _onDialogueComplete = onComplete;

            RefreshBlockingState();

            _dialogueOverlay.RemoveFromClassList("hidden");
            _dialoguePanel.schedule.Execute(() => _dialoguePanel.AddToClassList("visible")).ExecuteLater(20);

            UpdateQuestInfoVisibility();
            AdvanceLine();
        }

        /// <summary>Chuyển sang dòng tiếp theo. Kết thúc nếu hết dòng.</summary>
        private void AdvanceLine()
        {
            if (!_isDialogueOpen || _currentDialogue == null) return;

            _currentLineIndex++;

            if (_currentLineIndex >= _currentDialogue.lines.Length)
            {
                // Hết dòng — kiểm tra trạng thái quest để show nút phù hợp hoặc đóng
                OnDialogueFinished();
                return;
            }

            var line = _currentDialogue.lines[_currentLineIndex];

            // Cập nhật speaker name
            if (!string.IsNullOrEmpty(line.speakerName))
                _speakerName.text = line.speakerName;

            // Bắt đầu typewriter
            _fullText = line.text ?? "";
            _charCount = 0;
            _typeTimer = 0f;
            _isTyping = true;
            _dialogueText.text = "";

            // Ẩn nút trong khi đọc
            HideAllButtons();
        }

        /// <summary>Hoàn thành typewriter ngay lập tức.</summary>
        private void CompleteTyping()
        {
            _isTyping = false;
            _charCount = _fullText.Length;
            _dialogueText.text = _fullText;

            // Hiện nút phù hợp
            bool isLastLine = _currentLineIndex >= _currentDialogue.lines.Length - 1;

            // DisplayQuest/DisplayQuestState: NPC "nhận nộp hộ" (Autumn Fairy) không giữ quest của mình mà
            // trỏ tới NPC giao (Summer Fairy) — đọc trực tiếp Quest/QuestState của nó sẽ luôn null/0 nên
            // nút "Claim Reward" không bao giờ hiện.
            if (isLastLine && _currentNPC != null && _currentNPC.DisplayQuest != null)
            {
                byte state = _currentNPC.DisplayQuestState;
                if (state == 0 && _currentNPC.CanOfferQuest) // NotStarted → show Accept/Decline
                {
                    _btnAccept.RemoveFromClassList("hidden");
                    _btnDecline.RemoveFromClassList("hidden");
                    _btnContinue.AddToClassList("hidden");
                    SetKeyHint($"[{FormatKey(InteractKey)}] Accept   ·   [N] Decline");
                    return;
                }
                if (state == 2) // Completed → show claim (Continue sẽ claim)
                {
                    _btnContinue.text = "Claim Reward ★";
                    _btnContinue.RemoveFromClassList("hidden");
                    _btnAccept.AddToClassList("hidden");
                    _btnDecline.AddToClassList("hidden");
                    SetKeyHint($"[{FormatKey(InteractKey)}] Claim reward");
                    return;
                }
            }

            if (isLastLine)
            {
                _btnContinue.text = "Close";
                _btnContinue.RemoveFromClassList("hidden");
            }
            else
            {
                _btnContinue.text = "Continue ▶";
                _btnContinue.RemoveFromClassList("hidden");
            }

            _btnAccept.AddToClassList("hidden");
            _btnDecline.AddToClassList("hidden");
            ResetKeyHint();
        }

        /// <summary>Hết dòng hội thoại — xử lý theo trạng thái quest.</summary>
        private void OnDialogueFinished()
        {
            if (_onDialogueComplete != null)
            {
                var callback = _onDialogueComplete;
                _onDialogueComplete = null;
                CloseDialogue();
                callback.Invoke();
                return;
            }

            // DisplayQuest*: hỗ trợ NPC nhận nộp hộ (xem ghi chú ở phần hiện nút).
            if (_currentNPC != null && _currentNPC.DisplayQuest != null)
            {
                byte state = _currentNPC.DisplayQuestState;
                if (state == 2) // Completed → claim
                {
                    _currentNPC.RpcClaimReward();
                    CloseDialogue();
                    return;
                }
            }
            CloseDialogue();
        }

        /// <summary>Đóng hội thoại.</summary>
        public void CloseDialogue()
        {
            // NPC NHẬN NỘP (nhiệm vụ đưa tin): báo hoàn thành mục tiêu khi player đã NÓI CHUYỆN XONG.
            // Báo ở đây (không phải lúc mở thoại) để đúng ý "nhận được khi nói chuyện xong với NPC cuối map".
            // RpcReportTurnIn tự no-op nếu NPC này không phải điểm nộp / chưa ai mang tin / đã báo rồi.
            if (_currentNPC != null && _currentNPC.IsTurnInPoint) _currentNPC.RpcReportTurnIn();

            _isDialogueOpen = false;
            _isTyping = false;
            SetLocalQuestProtection(false);
            // Tính lại từ cờ: nếu popup thưởng vừa mở (host claim chạy đồng bộ) thì chuột VẪN tự do.
            RefreshBlockingState();

            _dialoguePanel.RemoveFromClassList("visible");

            // Delay ẩn overlay sau khi animation kết thúc
            _dialogueOverlay.schedule.Execute(() =>
            {
                if (!_isDialogueOpen) _dialogueOverlay.AddToClassList("hidden");
            }).ExecuteLater(500);

            _currentNPC = null;
            _currentDialogue = null;
        }


        private void OnAcceptClicked()
        {
            if (_currentNPC == null) return;
            _currentNPC.RpcAcceptQuest();
            CloseDialogue();
            // Quest tracker sẽ tự refresh và hiện quest mới
        }

        private void OnDeclineClicked()
        {
            if (_currentNPC == null) return;
            _currentNPC.RpcDeclineQuest();
            CloseDialogue();
        }


        private void HideAllButtons()
        {
            _btnAccept.AddToClassList("hidden");
            _btnDecline.AddToClassList("hidden");
            _btnContinue.AddToClassList("hidden");
        }

        private void SetLocalQuestProtection(bool active)
        {
            if (!active)
            {
                if (_protectedPlayer != null) _protectedPlayer.SetQuestDialogueProtection(false);
                _protectedPlayer = null;
                return;
            }

            // Chỉ bảo vệ player local đã mở NPC. Player coop còn lại không bị khoá hay miễn damage.
            foreach (var player in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            {
                if (player.Object == null || !player.Object.HasInputAuthority) continue;
                _protectedPlayer = player;
                _protectedPlayer.SetQuestDialogueProtection(true);
                return;
            }
        }

        /// <summary>
        /// Áp trạng thái "đang có UI hội thoại/thưởng chặn gameplay" từ HAI cờ `_isDialogueOpen` và
        /// `_isRewardShowing` — chuột, khóa di chuyển, và dừng game (solo).
        ///
        /// VÌ SAO TÍNH LẠI TỪ CỜ, KHÔNG SET TRỰC TIẾP: trên HOST (và solo — solo là host), `RpcClaimReward`
        /// chạy ĐỒNG BỘ ngay trong lời gọi → popup thưởng mở TRƯỚC khi `CloseDialogue` chạy xong, nên
        /// `CloseDialogue` khóa lại chuột đè lên popup vừa mở → bảng thưởng treo, không có chuột bấm OK.
        /// Client không lỗi vì RPC tới muộn hơn (đúng thứ tự) — vì thế bug chỉ thấy ở solo/host.
        /// Tính lại từ cờ thì thứ tự gọi không còn quan trọng.
        /// </summary>
        private void RefreshBlockingState()
        {
            bool blocking = _isDialogueOpen || _isRewardShowing;

            UnityEngine.Cursor.visible = blocking;
            UnityEngine.Cursor.lockState = blocking ? CursorLockMode.None : CursorLockMode.Locked;

            // Khóa di chuyển player local (PlayerController đọc cờ này). Popup thưởng cũng là modal —
            // chuột đang tự do, không nên cho chạy nhảy phía sau.
            Attrition.Persistence.DialogueState.IsActive = blocking;

            // SOLO: dừng hẳn game khi đang nói chuyện NPC → quái không đánh mất HP lúc đọc thoại.
            // COOP: SetSoloFreeze tự no-op (online — dừng sẽ phá đồng bộ), giữ nguyên hành vi cũ.
            Attrition.Persistence.GamePause.SetSoloFreeze(
                Attrition.Persistence.GamePause.Freeze.Dialogue, blocking);
        }

        private void UpdateQuestInfoVisibility()
        {
            // DisplayQuest*: NPC nhận nộp hộ (Autumn) trỏ tới quest của NPC giao (Summer) — xem NetworkNPC.
            if (_currentNPC == null || _currentNPC.DisplayQuest == null)
            {
                _questInfo.AddToClassList("hidden");
                _questInfo.RemoveFromClassList("visible");
                return;
            }

            var q = _currentNPC.DisplayQuest;
            byte state = _currentNPC.DisplayQuestState;

            // Proxy KHÔNG chào mời quest (việc của Summer) → state 0 thì không hiện bảng "Quest: ...".
            if (state == 0 && !_currentNPC.CanOfferQuest)
            {
                _questInfo.AddToClassList("hidden");
                _questInfo.RemoveFromClassList("visible");
                return;
            }

            if (state == 0) // Offer quest → show info
            {
                _questTitle.text = $"Quest: {q.title}";
                _questDesc.text = q.description ?? "";
                _questInfo.RemoveFromClassList("hidden");
                _questInfo.schedule.Execute(() => _questInfo.AddToClassList("visible")).ExecuteLater(100);
            }
            else if (state == 2) // Completed → show completion summary
            {
                _questTitle.text = $"✓ {q.title} — Complete!";
                _questDesc.text = "Return your findings to claim your reward.";
                _questInfo.RemoveFromClassList("hidden");
                _questInfo.schedule.Execute(() => _questInfo.AddToClassList("visible")).ExecuteLater(100);
            }
            else
            {
                _questInfo.AddToClassList("hidden");
                _questInfo.RemoveFromClassList("visible");
            }
        }


        private void OnItemReceived(string itemId, int amount)
        {
            _pendingRewards.Add(new RewardEntry { itemId = itemId, amount = amount });
        }

        private void OnExpReceived(int amount)
        {
            _pendingExp += amount;
        }

        private void OnRewardBatchComplete()
        {
            if (_pendingRewards.Count == 0 && _pendingExp == 0) return;
            ShowRewardPopup();
        }

        private void ShowRewardPopup()
        {
            _isRewardShowing = true;
            RefreshBlockingState();

            _rewardItems.Clear();
            _rewardExp.AddToClassList("hidden");
            _rewardExp.RemoveFromClassList("visible");

            _rewardOverlay.RemoveFromClassList("hidden");
            _rewardOverlay.schedule.Execute(() => _rewardOverlay.AddToClassList("visible")).ExecuteLater(20);
            _rewardPanel.schedule.Execute(() => _rewardPanel.AddToClassList("visible")).ExecuteLater(50);

            // Pop-in items one by one
            var db = ItemDatabaseSO.Instance;
            for (int i = 0; i < _pendingRewards.Count; i++)
            {
                var reward = _pendingRewards[i];
                var row = CreateRewardItemRow(reward.itemId, reward.amount, db);
                _rewardItems.Add(row);

                int delay = 300 + (int)(i * rewardItemDelay * 1000);
                row.schedule.Execute(() => row.AddToClassList("visible")).ExecuteLater(delay);
            }

            // EXP (pop-in after items)
            if (_pendingExp > 0)
            {
                _rewardExp.text = $"+{_pendingExp} EXP";
                _rewardExp.RemoveFromClassList("hidden");
                int expDelay = 300 + (int)(_pendingRewards.Count * rewardItemDelay * 1000) + 200;
                _rewardExp.schedule.Execute(() => _rewardExp.AddToClassList("visible")).ExecuteLater(expDelay);
            }

            _pendingRewards.Clear();
            _pendingExp = 0;
        }

        private VisualElement CreateRewardItemRow(string itemId, int amount, ItemDatabaseSO db)
        {
            var row = new VisualElement();
            row.AddToClassList("reward-item-row");

            // Icon
            var icon = new VisualElement();
            icon.AddToClassList("reward-item-icon");
            if (db != null)
            {
                var itemSO = db.GetItemByStringId(itemId);
                if (itemSO != null && itemSO.icon != null)
                    icon.style.backgroundImage = new StyleBackground(itemSO.icon);
            }
            row.Add(icon);

            // Name
            var nameLabel = new Label();
            nameLabel.AddToClassList("reward-item-name");
            string displayName = itemId;
            if (db != null)
            {
                var itemSO = db.GetItemByStringId(itemId);
                if (itemSO != null) displayName = Attrition.Persistence.ItemRuntimeConfig.Name(itemSO);
            }
            nameLabel.text = displayName;
            row.Add(nameLabel);

            // Amount
            if (amount > 1)
            {
                var amountLabel = new Label();
                amountLabel.AddToClassList("reward-item-amount");
                amountLabel.text = $"×{amount}";
                row.Add(amountLabel);
            }

            return row;
        }

        private void CloseRewardPopup()
        {
            _isRewardShowing = false;
            RefreshBlockingState();

            _rewardPanel.RemoveFromClassList("visible");
            _rewardOverlay.RemoveFromClassList("visible");

            _rewardOverlay.schedule.Execute(() =>
            {
                if (!_isRewardShowing) _rewardOverlay.AddToClassList("hidden");
            }).ExecuteLater(400);
        }

        //  QUEST TRACKER HUD (right side)

        private void RefreshQuestTracker()
        {
            if (_trackerList == null || _questTracker == null) return;

            var npcs = FindObjectsByType<NetworkNPC>(FindObjectsSortMode.None);
            bool hasActive = false;

            _trackerList.Clear();

            foreach (var npc in npcs)
            {
                if (npc.Quest == null) continue;
                byte state = npc.QuestState;
                if (state != 1 && state != 2) continue; // only Active or Completed

                hasActive = true;
                var entry = CreateTrackerEntry(npc);
                _trackerList.Add(entry);
            }

            if (hasActive)
            {
                _questTracker.RemoveFromClassList("hidden");
                // Delay để transition chạy
                if (!_questTracker.ClassListContains("visible"))
                    _questTracker.schedule.Execute(() => _questTracker.AddToClassList("visible")).ExecuteLater(50);
            }
            else
            {
                _questTracker.RemoveFromClassList("visible");
                _questTracker.schedule.Execute(() =>
                {
                    if (!_questTracker.ClassListContains("visible"))
                        _questTracker.AddToClassList("hidden");
                }).ExecuteLater(600);
            }
        }

        private VisualElement CreateTrackerEntry(NetworkNPC npc)
        {
            var q = npc.Quest;
            bool isComplete = npc.QuestState == 2;

            var entry = new VisualElement();
            entry.AddToClassList("tracker-entry");
            if (isComplete) entry.AddToClassList("tracker-entry-complete");

            // Title
            var title = new Label();
            title.AddToClassList("tracker-entry-title");
            title.text = isComplete ? $"✓ {q.title}" : q.title;
            entry.Add(title);

            // Progress text. Xong mục tiêu → nhắc quay lại ĐÚNG NPC nào (player hay quên bước nộp).
            // Dùng lại nguyên văn của bảng quest Tab (GameUIController.QuestLog) để hai chỗ không lệch chữ.
            var progress = new Label();
            progress.AddToClassList("tracker-entry-progress");
            string objText = q.objectiveType == QuestObjectiveType.Kill ? "defeated" : "completed";
            progress.text = isComplete
                ? $"Complete — return to {npc.NpcName} to claim your reward"
                : $"{npc.QuestProgress}/{q.requiredAmount} {objText}";
            entry.Add(progress);

            // Progress bar
            var barBg = new VisualElement();
            barBg.AddToClassList("tracker-progress-bar-bg");
            var barFill = new VisualElement();
            barFill.AddToClassList("tracker-progress-bar-fill");
            float pct = q.requiredAmount > 0 ? Mathf.Clamp01((float)npc.QuestProgress / q.requiredAmount) : 0;
            barFill.style.width = new StyleLength(new Length(pct * 100f, LengthUnit.Percent));
            barBg.Add(barFill);
            entry.Add(barBg);

            return entry;
        }
    }
}
