using Fusion;

namespace Attrition.Gameplay.Player.Inventory
{
    /// <summary>
    /// Dữ liệu 1 ô trong túi đồ. Truyền qua mạng bằng Fusion NetworkArray.
    /// ItemIndex = vị trí trong ItemDatabaseSO (-1 = ô trống).
    /// Amount = số lượng (1 cho Equipment/Skill, 1-99 cho Material theo BR-41/BR-42).
    /// </summary>
    public struct InventorySlot : INetworkStruct
    {
        /// <summary>Index trong ItemDatabaseSO. -1 = ô trống.</summary>
        public int ItemIndex;

        /// <summary>Số lượng hiện tại. 0 khi trống.</summary>
        public int Amount;

        public bool IsEmpty => ItemIndex < 0 || Amount <= 0;

        public static InventorySlot Empty => new InventorySlot { ItemIndex = -1, Amount = 0 };

        public override string ToString() => IsEmpty ? "[Empty]" : $"[Item:{ItemIndex} x{Amount}]";
    }
}
