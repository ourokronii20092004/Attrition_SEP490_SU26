namespace Attrition.Core
{
    /// <summary>
    /// Mọi chỉ số trong game. Dùng chung cho player, enemy, equipment, accessory.
    /// Thêm stat mới chỉ cần thêm 1 entry ở đây + map trong StatSheet.
    /// </summary>
    public enum StatType
    {
        MaxHP,
        MaxMana,
        MaxStamina,
        AD,   // Attack Damage (vật lý)
        AP,   // Ability Power (phép)
        DEF,  // Giảm sát thương vật lý
        RES,  // Giảm sát thương phép
        MoveSpeed,
        AttackSpeed,
    }
}
