using System.Collections.Generic;
using Attrition.Data;
using Attrition.Gameplay.Player.Inventory;
using NUnit.Framework;
using UnityEngine;

namespace Attrition.Tests.EditMode
{
    /// <summary>
    /// InventorySlot + PlayerInventory.CanDrop — lõi thuần (không cần Fusion/NetworkBehaviour)
    /// của hệ thống túi đồ (report CDI / VLPIT / HFI / DCQ):
    ///  - InventorySlot: IsEmpty, Empty singleton, ToString.
    ///  - CanDrop: key item (BR-45), skill, accessory không vứt được; material/equipment vứt được.
    ///  - DCQ: lượng vật phẩm chỉ giảm khi đủ số lượng (đã test qua SaveManager roundtrip, và
    ///    InventorySlot là đơn vị lưu trữ cơ bản cho việc trừ/hết stack ở AddItemInternal/TryRemoveItem).
    /// </summary>
    public class InventorySlotTests
    {
        [Test]
        public void EmptySlot_ItemIndexNegative_IsEmpty()
        {
            var slot = new InventorySlot { ItemIndex = -1, Amount = 0 };
            Assert.IsTrue(slot.IsEmpty);
        }

        [Test]
        public void EmptySlot_ZeroAmount_IsEmpty()
        {
            // Amount <= 0 cũng coi là trống (stack đã hết).
            var slot = new InventorySlot { ItemIndex = 5, Amount = 0 };
            Assert.IsTrue(slot.IsEmpty);
        }

        [Test]
        public void FilledSlot_IsNotEmpty()
        {
            var slot = new InventorySlot { ItemIndex = 3, Amount = 7 };
            Assert.IsFalse(slot.IsEmpty);
        }

        [Test]
        public void EmptySingleton_HasNegativeIndexAndZeroAmount()
        {
            var empty = InventorySlot.Empty;
            Assert.IsTrue(empty.IsEmpty);
            Assert.AreEqual(-1, empty.ItemIndex);
            Assert.AreEqual(0, empty.Amount);
        }

        [Test]
        public void ToString_ReflectsState()
        {
            Assert.AreEqual("[Empty]", new InventorySlot { ItemIndex = -1, Amount = 0 }.ToString());
            Assert.AreEqual("[Item:4 x3]", new InventorySlot { ItemIndex = 4, Amount = 3 }.ToString());
        }

        [Test]
        public void CanDrop_NullItem_IsFalse()
        {
            Assert.IsFalse(PlayerInventory.CanDrop(null));
        }

        [Test]
        public void CanDrop_KeyItem_IsFalse()
        {
            // BR-45: key item không vứt được.
            var mat = ScriptableObject.CreateInstance<MaterialSO>();
            mat.maxStack = 99;
            mat.isKeyItem = true;

            Assert.IsFalse(PlayerInventory.CanDrop(mat));
            Object.DestroyImmediate(mat);
        }

        [Test]
        public void CanDrop_SkillAndAccessory_AreFalse()
        {
            var skill = ScriptableObject.CreateInstance<SkillSO>();
            var acc = ScriptableObject.CreateInstance<AccessorySO>();

            Assert.IsFalse(PlayerInventory.CanDrop(skill));
            Assert.IsFalse(PlayerInventory.CanDrop(acc));
            Object.DestroyImmediate(skill);
            Object.DestroyImmediate(acc);
        }

        [Test]
        public void CanDrop_NonKeyMaterialAndEquipment_AreTrue()
        {
            var mat = ScriptableObject.CreateInstance<MaterialSO>();
            mat.maxStack = 99;
            mat.isKeyItem = false;

            var eq = ScriptableObject.CreateInstance<EquipmentSO>();
            eq.isKeyItem = false;

            Assert.IsTrue(PlayerInventory.CanDrop(mat));
            Assert.IsTrue(PlayerInventory.CanDrop(eq));
            Object.DestroyImmediate(mat);
            Object.DestroyImmediate(eq);
        }
    }
}
