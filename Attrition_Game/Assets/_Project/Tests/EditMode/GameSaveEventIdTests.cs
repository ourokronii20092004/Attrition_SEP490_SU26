using Attrition.Gameplay.Persistence;
using NUnit.Framework;

namespace Attrition.Tests.EditMode
{
    /// <summary>
    /// GameSaveService.ParseCheckpointEventId / ParseQuestEventId / IsBossEventId — bóc prefix
    /// world-state để khôi phục/đồng bộ tiến trình (report RFFCS: nạp lại trạng thái đã lưu
    /// theo đúng định danh của từng loại).
    /// </summary>
    public class GameSaveEventIdTests
    {
        [Test]
        public void ParseCheckpointEventId_StripsCpPrefix()
        {
            Assert.AreEqual("cp_main_gate", GameSaveService.ParseCheckpointEventId("cp:cp_main_gate"));
        }

        [Test]
        public void ParseCheckpointEventId_NonCheckpoint_ReturnsNull()
        {
            Assert.IsNull(GameSaveService.ParseCheckpointEventId("q:quest_1"));
            Assert.IsNull(GameSaveService.ParseCheckpointEventId("severed_fang"));
            Assert.IsNull(GameSaveService.ParseCheckpointEventId(""));
            Assert.IsNull(GameSaveService.ParseCheckpointEventId(null));
        }

        [Test]
        public void ParseQuestEventId_StripsQPrefix()
        {
            Assert.AreEqual("quest_1", GameSaveService.ParseQuestEventId("q:quest_1"));
        }

        [Test]
        public void ParseQuestEventId_NonQuest_ReturnsNull()
        {
            Assert.IsNull(GameSaveService.ParseQuestEventId("cp:cp_main_gate"));
            Assert.IsNull(GameSaveService.ParseQuestEventId("severed_fang"));
            Assert.IsNull(GameSaveService.ParseQuestEventId(null));
        }

        [Test]
        public void IsBossEventId_PrefixedRows_AreNotBosses()
        {
            // quest + checkpoint đều có prefix → không phải boss.
            Assert.IsFalse(GameSaveService.IsBossEventId("q:quest_1"));
            Assert.IsFalse(GameSaveService.IsBossEventId("cp:cp_main_gate"));
        }

        [Test]
        public void IsBossEventId_Unprefixed_IsBoss()
        {
            // Boss = eventId KHÔNG prefix (vd "severed_fang").
            Assert.IsTrue(GameSaveService.IsBossEventId("severed_fang"));
            Assert.IsTrue(GameSaveService.IsBossEventId("arch_demon"));
        }

        [Test]
        public void IsBossEventId_NullOrEmpty_ReturnsFalse()
        {
            Assert.IsFalse(GameSaveService.IsBossEventId(null));
            Assert.IsFalse(GameSaveService.IsBossEventId(""));
        }

        [Test]
        public void RoundTrip_CheckpointEventId_CyclesCleanly()
        {
            // CheckpointEventId là private — kiểm tra qua cặp parse/IsBoss để đảm bảo prefix không đụng nhau.
            string cp = "cp:cp_cave_exit";
            Assert.AreEqual("cp_cave_exit", GameSaveService.ParseCheckpointEventId(cp));
            Assert.IsFalse(GameSaveService.IsBossEventId(cp));
        }
    }
}
