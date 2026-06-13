namespace Attrition.Data
{
    /// <summary>
    /// Event bus tĩnh — Gameplay fire khi player nhận thưởng (quest NPC, Elite, Boss),
    /// DialogueUI lắng nghe để hiện popup "Congratulations!".
    /// Đặt ở Data assembly để cả Gameplay lẫn UI đều truy cập được (tránh vòng tham chiếu).
    /// </summary>
    public static class RewardEvents
    {
        /// <summary>Được fire khi 1 item được thêm vào inventory (itemId, amount). Local-only.</summary>
        public static event System.Action<string, int> OnItemReceived;

        /// <summary>Được fire khi player nhận EXP thưởng (amount). Local-only.</summary>
        public static event System.Action<int> OnExpReceived;

        /// <summary>Gọi khi tất cả item + EXP đã phát xong — UI kết thúc popup.</summary>
        public static event System.Action OnRewardBatchComplete;

        public static void NotifyItem(string itemId, int amount)
            => OnItemReceived?.Invoke(itemId, amount);

        public static void NotifyExp(int amount)
            => OnExpReceived?.Invoke(amount);

        public static void NotifyBatchComplete()
            => OnRewardBatchComplete?.Invoke();
    }
}
