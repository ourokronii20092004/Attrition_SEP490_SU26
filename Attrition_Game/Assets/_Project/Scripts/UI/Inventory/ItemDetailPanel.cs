using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Attrition.Data;
using Attrition.Gameplay.Player.Inventory;

namespace Attrition.UI.Inventory
{
    /// <summary>
    /// Panel chi tiết item — hiện bên phải khi hover item trong grid.
    /// Hiển thị: tên, mô tả, loại, stat modifiers.
    /// </summary>
    public class ItemDetailPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI categoryText;
        [SerializeField] private TextMeshProUGUI statsText;
        [SerializeField] private Image itemIcon;

        public void Show(InventorySlot slot)
        {
            if (panel == null) return;

            var db = ItemDatabaseSO.Instance;
            if (db == null || slot.IsEmpty)
            {
                Hide();
                return;
            }

            var item = db.GetItem(slot.ItemIndex);
            if (item == null)
            {
                Hide();
                return;
            }

            panel.SetActive(true);

            if (nameText != null) nameText.text = Attrition.Persistence.ItemRuntimeConfig.Name(item);
            if (descriptionText != null) descriptionText.text = Attrition.Persistence.ItemRuntimeConfig.Description(item);
            if (categoryText != null) categoryText.text = item.Category.ToString();
            if (itemIcon != null) { itemIcon.sprite = item.icon; itemIcon.enabled = item.icon != null; }

            // Stat modifiers
            if (statsText != null)
            {
                var sb = new System.Text.StringBuilder();

                if (item is EquipmentSO eq && eq.modifiers != null)
                {
                    sb.AppendLine($"Slot: {eq.slot}");
                    foreach (var mod in eq.modifiers)
                        sb.AppendLine($"  {mod.stat}: +{mod.amount}");
                }
                else if (item is AccessorySO acc)
                {
                    sb.AppendLine($"Kind: {acc.kind}");
                    if (acc.kind == AccessoryKind.AbilityGrant)
                        sb.AppendLine($"Grants: {acc.grantedAbility}");
                    if (acc.modifiers != null)
                        foreach (var mod in acc.modifiers)
                            sb.AppendLine($"  {mod.stat}: +{mod.amount}");
                }
                else if (item is SkillSO skill)
                {
                    var runtime = Attrition.Persistence.SkillRuntimeConfig.From(skill);
                    sb.AppendLine($"Element: {runtime.element}");
                    sb.AppendLine($"Mana: {runtime.manaCost}");
                    sb.AppendLine($"Damage: {runtime.baseDamage}");
                    sb.AppendLine($"Cast: {runtime.castTime:F1}s");
                    sb.AppendLine($"CD: {runtime.cooldown:F1}s");
                }
                else if (item is MaterialSO)
                {
                    sb.AppendLine($"Stack: {slot.Amount}/{Attrition.Persistence.ItemRuntimeConfig.MaxStack(item)}");
                    if (Attrition.Persistence.ItemRuntimeConfig.IsKeyItem(item)) sb.AppendLine("<color=yellow>Key Item</color>");
                }

                statsText.text = sb.ToString();
            }
        }

        public void Hide()
        {
            if (panel != null) panel.SetActive(false);
        }
    }
}
