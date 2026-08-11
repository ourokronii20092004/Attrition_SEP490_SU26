using Attrition.Data;
using Attrition.Persistence;
using NUnit.Framework;
using UnityEngine;

namespace Attrition.Tests.EditMode
{
    /// <summary>
    /// ItemRuntimeConfig — cầu nối web → game cho config item. Khi chưa có web override (Instance null,
    /// offline/solo) thì mọi field phải fallback về default trong SO; MaxStack luôn >= 1.
    /// Khớp function AIM "Apply Item Modifiers" trong report.
    /// </summary>
    public class ItemRuntimeConfigTests
    {
        private MaterialSO _item;

        [SetUp]
        public void SetUp()
        {
            _item = ScriptableObject.CreateInstance<MaterialSO>();
            _item.itemId = "iron_ingot";
            _item.displayName = "Iron Ingot";
            _item.description = "Default desc";
            _item.maxStack = 99;
            _item.isKeyItem = false;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_item);

        [Test]
        public void NoOverride_UsesScriptableObjectDefaults()
        {
            Assert.AreEqual("Iron Ingot", ItemRuntimeConfig.Name(_item));
            Assert.AreEqual("Default desc", ItemRuntimeConfig.Description(_item));
            Assert.AreEqual(99, ItemRuntimeConfig.MaxStack(_item));
            Assert.IsFalse(ItemRuntimeConfig.IsKeyItem(_item));
        }

        [Test]
        public void MaxStack_WithZeroOrNegative_ScriptableObjectIsClampedToOne()
        {
            _item.maxStack = 0;
            Assert.AreEqual(1, ItemRuntimeConfig.MaxStack(_item));

            _item.maxStack = -5;
            Assert.AreEqual(1, ItemRuntimeConfig.MaxStack(_item));
        }
    }
}
