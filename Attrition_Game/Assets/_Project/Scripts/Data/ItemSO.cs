using UnityEngine;

namespace Attrition.Data
{
    /// <summary>Phân loại vật phẩm — dùng để xác định vật phẩm thuộc nhóm slot nào trong inventory.</summary>
    public enum ItemCategory { Equipment, Accessory, Skill, Material }

    /// <summary>
    /// Lớp cha trừu tượng cho MỌI vật phẩm trong game (Equipment, Accessory, Skill, Material).
    /// Chứa thông tin chung: itemId, displayName, icon, stacking, key-item flag.
    /// Các SO con kế thừa và thêm dữ liệu riêng (stat modifiers, element, v.v.).
    /// </summary>
    public abstract class ItemSO : ScriptableObject
    {
        [Header("---- ITEM BASE ----")]
        [Tooltip("ID dạng chuỗi, duy nhất toàn game. Dùng cho save/load và backend. Ví dụ: 'iron_helm'.")]
        public string itemId = "";
        public string displayName = "New Item";
        [TextArea] public string description;
        public Sprite icon;

        [Header("---- STACKING ----")]
        [Tooltip("Số lượng tối đa cộng dồn trong 1 ô. Equipment/Skill = 1 (BR-42), Material = 99 (BR-41).")]
        public int maxStack = 1;

        [Tooltip("Key Item = không thể drop/bán/hủy (BR-45).")]
        public bool isKeyItem = false;

        /// <summary>Loại vật phẩm — con class override để trả về giá trị cố định.</summary>
        public abstract ItemCategory Category { get; }
    }
}
