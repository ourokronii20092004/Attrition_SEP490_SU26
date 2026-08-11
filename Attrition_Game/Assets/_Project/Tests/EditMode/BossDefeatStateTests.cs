using System.Collections.Generic;
using Attrition.Gameplay.Environment;
using Attrition.Persistence;
using NUnit.Framework;

namespace Attrition.Tests.EditMode
{
    /// <summary>
    /// BossDefeatState — boss đã hạ (report BD): MarkDefeated idempotent, LoadFrom/WriteTo
    /// roundtrip qua SaveSlotData.defeatedBosses, và hợp nhất coop theo sessionId
    /// (cùng phòng → union, phòng khác → thay thế).
    /// </summary>
    public class BossDefeatStateTests
    {
        [SetUp]
        public void SetUp() => BossDefeatState.Clear();

        [TearDown]
        public void TearDown() => BossDefeatState.Clear();

        [Test]
        public void MarkDefeated_OnlyOnce()
        {
            Assert.IsTrue(BossDefeatState.MarkDefeated("severed_fang"));
            Assert.IsFalse(BossDefeatState.MarkDefeated("severed_fang"));
            Assert.IsTrue(BossDefeatState.IsDefeated("severed_fang"));
        }

        [Test]
        public void MarkDefeated_NullOrEmpty_Ignored()
        {
            Assert.IsFalse(BossDefeatState.MarkDefeated(null));
            Assert.IsFalse(BossDefeatState.MarkDefeated(""));
            Assert.IsFalse(BossDefeatState.IsDefeated(null));
        }

        [Test]
        public void WriteTo_RoundtripsThroughSlotData()
        {
            BossDefeatState.MarkDefeated("arch_demon");
            BossDefeatState.MarkDefeated("demon_kin");

            var data = new SaveSlotData();
            BossDefeatState.WriteTo(data);
            BossDefeatState.Clear();
            BossDefeatState.LoadFrom(data);

            Assert.IsTrue(BossDefeatState.IsDefeated("arch_demon"));
            Assert.IsTrue(BossDefeatState.IsDefeated("demon_kin"));
            Assert.IsFalse(BossDefeatState.IsDefeated("elf"));
        }

        [Test]
        public void LoadFrom_Null_ClearsEverything()
        {
            BossDefeatState.MarkDefeated("arch_demon");
            BossDefeatState.LoadFrom(null);

            Assert.IsFalse(BossDefeatState.IsDefeated("arch_demon"));
        }

        [Test]
        public void LoadFromIds_SameSession_Merges()
        {
            BossDefeatState.LoadFromIds(new[] { "arch_demon" }, "P1");
            // Fetch lại CÙNG phòng: boss vừa hạ mà server chưa kịp lưu → phải giữ (union), không xoá.
            BossDefeatState.LoadFromIds(new[] { "elf" }, "P1");

            Assert.IsTrue(BossDefeatState.IsDefeated("arch_demon"));
            Assert.IsTrue(BossDefeatState.IsDefeated("elf"));
        }

        [Test]
        public void LoadFromIds_DifferentSession_Replaces()
        {
            BossDefeatState.LoadFromIds(new[] { "arch_demon" }, "P1");
            // Sang phòng KHÁC → thay thế sạch, không để boss phòng trước lẫn sang.
            BossDefeatState.LoadFromIds(new[] { "elf" }, "P2");

            Assert.IsFalse(BossDefeatState.IsDefeated("arch_demon"));
            Assert.IsTrue(BossDefeatState.IsDefeated("elf"));
        }

        [Test]
        public void LoadFromIds_NullList_KeepsCurrentState()
        {
            BossDefeatState.LoadFromIds(new[] { "arch_demon" }, "P1");
            BossDefeatState.LoadFromIds(null, "P1"); // không có dữ liệu → giữ nguyên

            Assert.IsTrue(BossDefeatState.IsDefeated("arch_demon"));
        }
    }
}
