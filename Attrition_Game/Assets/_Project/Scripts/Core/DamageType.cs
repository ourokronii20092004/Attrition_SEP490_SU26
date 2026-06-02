namespace Attrition.Core
{
    /// <summary>Phân loại nguồn sát thương để DamageCalculator chọn công thức.</summary>
    public enum DamageType
    {
        Physical, // realDamage = Max(1, ad - def)
        Magic,    // realDamage = Max(1, ap - res)
        True,     // bỏ qua phòng thủ (DoT/Hazard có thể dùng)
    }
}
