using Attrition.Data;

namespace Attrition.Persistence
{
    public static class ItemRuntimeConfig
    {
        private static ItemConfigOverride Get(ItemSO item) =>
            item != null && ItemConfigProvider.Instance != null && ItemConfigProvider.Instance.IsReady
                ? ItemConfigProvider.Instance.GetOverride(item.itemId) : null;

        public static string Name(ItemSO item) => Get(item)?.name ?? item.displayName;
        public static string Description(ItemSO item) => Get(item)?.description ?? item.description;
        public static int MaxStack(ItemSO item) => System.Math.Max(1, Get(item)?.maxStack ?? item.maxStack);
        public static bool IsKeyItem(ItemSO item) => Get(item)?.isKeyItem ?? item.isKeyItem;

        /// <summary>
        /// Danh sách cộng chỉ số THẬT SỰ đang có hiệu lực của item: ưu tiên override admin sửa trên web,
        /// không có thì dùng mặc định trong SO.
        ///
        /// VÌ SAO CẦN: panel thông tin item (GameUIController.Inventory.Refresh.AppendMods) trước đây đọc
        /// THẲNG `eq.modifiers` từ SO tĩnh, còn chỉ số cộng vào người chơi lại đi qua
        /// `PlayerStats.BuildItemModifierOverrides` — cái này ĐỌC override từ ItemConfigProvider. Hai
        /// đường khác nhau nên tăng chỉ số trên web thì máu/sát thương cộng đúng số mới, mà panel vẫn
        /// hiện số CŨ trong SO. Dùng hàm này cho hiển thị để hai bên luôn khớp.
        ///
        /// Trả mảng rỗng (không null) khi item không có dòng cộng nào, để caller khỏi kiểm null.
        /// </summary>
        public static StatModifier[] Modifiers(ItemSO item, StatModifier[] fallback)
        {
            var ov = Get(item);
            if (ov?.modifiers == null || ov.modifiers.Count == 0)
                return fallback ?? System.Array.Empty<StatModifier>();

            var list = new System.Collections.Generic.List<StatModifier>(ov.modifiers.Count);
            foreach (var (stat, amount) in ov.modifiers)
                if (System.Enum.TryParse<Attrition.Core.StatType>(stat, out var st))
                    list.Add(new StatModifier { stat = st, amount = amount });

            // Override có nhưng KHÔNG parse được stat nào (web gửi tên lạ) → thà hiện số mặc định của SO
            // còn hơn hiện panel trống.
            return list.Count > 0 ? list.ToArray() : (fallback ?? System.Array.Empty<StatModifier>());
        }
    }
}
