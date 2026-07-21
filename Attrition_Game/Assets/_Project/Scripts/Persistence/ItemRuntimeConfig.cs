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
    }
}
