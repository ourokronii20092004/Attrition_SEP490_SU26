namespace Attrition.API.Models;

public class CharacterInventorySlot
{
    public Guid CharacterId { get; set; }
    public Character Character { get; set; } = null!;
    
    public int? ItemId { get; set; }
    public Item? Item { get; set; }

    public int SlotIndex { get; set; }
    public short Quantity { get; set; } = 1;
    public bool IsEquipped { get; set; } = false;
}