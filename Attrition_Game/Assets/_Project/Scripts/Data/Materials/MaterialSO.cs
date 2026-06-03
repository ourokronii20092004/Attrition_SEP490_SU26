using UnityEngine;

namespace Attrition.Data
{
    /// <summary>
    /// STATIC data — nguyên liệu hoặc chìa khóa khu vực.
    /// Stack tối đa 99 (BR-41). Key Item = không drop/bán/hủy được (BR-45).
    /// Tạo asset: Create → Attrition → Material.
    /// </summary>
    [CreateAssetMenu(menuName = "Attrition/Material", fileName = "Material")]
    public class MaterialSO : ItemSO
    {
        // maxStack mặc định = 99 trong Reset(), isKeyItem toggle trong Inspector.
        // Không có stat modifiers — chỉ là vật phẩm thu thập / quest.

        public override ItemCategory Category => ItemCategory.Material;

        private void Reset()
        {
            maxStack = 99;
        }
    }
}
