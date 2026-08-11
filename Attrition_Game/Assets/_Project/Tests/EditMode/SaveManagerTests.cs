using Attrition.Persistence;
using NUnit.Framework;

namespace Attrition.Tests.EditMode
{
    /// <summary>
    /// SaveManager.IsSlotCompatible — save Solo không mở được ở Coop và ngược lại; slot trống hoặc
    /// save cũ (chưa gắn originMode) luôn tương thích. Khớp function MCP "Manage Character Profiles".
    ///
    /// Dùng slot index 7 (cuối) và PHỤC HỒI dữ liệu gốc trong TearDown để không làm mất save thật.
    /// </summary>
    public class SaveManagerTests
    {
        private const int Slot = 7;
        private SaveSlotData _original;

        [SetUp]
        public void SetUp() => _original = SaveManager.LoadSlot(Slot);

        [TearDown]
        public void TearDown()
        {
            if (_original != null) SaveManager.SaveSlot(Slot, _original);
            else SaveManager.ClearSlot(Slot);
        }

        private void SaveWithOrigin(string originMode) =>
            SaveManager.SaveSlot(Slot, new SaveSlotData { characterName = "Test", level = 1, originMode = originMode });

        [Test]
        public void EmptySlot_IsCompatibleWithBothModes()
        {
            SaveManager.ClearSlot(Slot);

            Assert.IsTrue(SaveManager.IsSlotCompatible(Slot, LaunchMode.Solo));
            Assert.IsTrue(SaveManager.IsSlotCompatible(Slot, LaunchMode.Coop));
        }

        [Test]
        public void LegacySave_WithoutOriginMode_IsCompatibleWithBothModes()
        {
            SaveWithOrigin(null);

            Assert.IsTrue(SaveManager.IsSlotCompatible(Slot, LaunchMode.Solo));
            Assert.IsTrue(SaveManager.IsSlotCompatible(Slot, LaunchMode.Coop));
        }

        [Test]
        public void SoloSave_IsRejectedInCoopMode()
        {
            SaveWithOrigin(LaunchMode.Solo.ToString());

            Assert.IsTrue(SaveManager.IsSlotCompatible(Slot, LaunchMode.Solo));
            Assert.IsFalse(SaveManager.IsSlotCompatible(Slot, LaunchMode.Coop));
        }

        [Test]
        public void CoopSave_IsRejectedInSoloMode()
        {
            SaveWithOrigin(LaunchMode.Coop.ToString());

            Assert.IsTrue(SaveManager.IsSlotCompatible(Slot, LaunchMode.Coop));
            Assert.IsFalse(SaveManager.IsSlotCompatible(Slot, LaunchMode.Solo));
        }

        [Test]
        public void SaveLoadRoundTrip_PreservesData()
        {
            var data = new SaveSlotData { characterName = "Kael", level = 42, location = "Ember Citadel", originMode = "Solo" };

            SaveManager.SaveSlot(Slot, data);
            var loaded = SaveManager.LoadSlot(Slot);

            Assert.IsNotNull(loaded);
            Assert.AreEqual("Kael", loaded.characterName);
            Assert.AreEqual(42, loaded.level);
            Assert.AreEqual("Ember Citadel", loaded.location);
            Assert.AreEqual("Solo", loaded.originMode);
        }

        [Test]
        public void ClearSlot_RemovesTheSave()
        {
            SaveWithOrigin(LaunchMode.Solo.ToString());
            SaveManager.ClearSlot(Slot);

            Assert.IsNull(SaveManager.LoadSlot(Slot));
        }
    }
}
