using NUnit.Framework;
using UnityEngine;

namespace Attrition.Tests.EditMode
{
    /// <summary>
    /// SceneEntryRegistry — sổ đăng ký static sống xuyên scene. Hai hành vi quan trọng:
    /// z luôn được chuẩn hoá về 0 (player 2D, z ≠ 0 làm nhân vật nằm sau tilemap), và
    /// Unregister chỉ xoá khi đúng chủ sở hữu (object scene mới OnEnable trước OnDisable của scene cũ).
    /// </summary>
    public class SceneEntryRegistryTests
    {
        // Registry là static toàn cục nên state phải được dọn trước và sau mỗi test.
        [SetUp]
        public void SetUp() => ClearRegistry();

        [TearDown]
        public void TearDown() => ClearRegistry();

        private static void ClearRegistry()
        {
            foreach (var id in new[] { "gate_a", "gate_b", "boss_exit" })
                SceneEntryRegistry.Unregister(id);
            SceneEntryRegistry.ClearPending();
        }

        [Test]
        public void Register_NormalizesZToZero()
        {
            SceneEntryRegistry.Register("gate_a", new Vector3(12f, 4f, 3.59f));
            SceneEntryRegistry.PendingEntryId = "gate_a";

            Assert.IsTrue(SceneEntryRegistry.TryGetPendingPosition(out var pos));
            Assert.AreEqual(12f, pos.x);
            Assert.AreEqual(4f, pos.y);
            Assert.AreEqual(0f, pos.z);
        }

        [Test]
        public void Register_WithEmptyId_IsIgnored()
        {
            SceneEntryRegistry.Register("", new Vector3(1f, 1f, 0f));
            SceneEntryRegistry.PendingEntryId = "";

            Assert.IsFalse(SceneEntryRegistry.TryGetPendingPosition(out _));
        }

        [Test]
        public void TryGetPendingPosition_WithoutPendingId_ReturnsFalse()
        {
            SceneEntryRegistry.Register("gate_a", Vector3.one);

            Assert.IsFalse(SceneEntryRegistry.TryGetPendingPosition(out var pos));
            Assert.AreEqual(Vector3.zero, pos);
        }

        [Test]
        public void TryGetPendingPosition_WithUnknownPendingId_ReturnsFalse()
        {
            SceneEntryRegistry.Register("gate_a", Vector3.one);
            SceneEntryRegistry.PendingEntryId = "gate_missing";

            Assert.IsFalse(SceneEntryRegistry.TryGetPendingPosition(out _));
        }

        [Test]
        public void Register_SameId_OverwritesPreviousPosition()
        {
            SceneEntryRegistry.Register("gate_a", new Vector3(1f, 1f, 0f));
            SceneEntryRegistry.Register("gate_a", new Vector3(9f, 8f, 0f));
            SceneEntryRegistry.PendingEntryId = "gate_a";

            SceneEntryRegistry.TryGetPendingPosition(out var pos);
            Assert.AreEqual(new Vector3(9f, 8f, 0f), pos);
        }

        [Test]
        public void Unregister_ByMatchingOwner_RemovesEntry()
        {
            var owner = new object();
            SceneEntryRegistry.Register("gate_a", Vector3.one, owner);

            SceneEntryRegistry.Unregister("gate_a", owner);
            SceneEntryRegistry.PendingEntryId = "gate_a";

            Assert.IsFalse(SceneEntryRegistry.TryGetPendingPosition(out _));
        }

        [Test]
        public void Unregister_ByDifferentOwner_KeepsEntryOfNewScene()
        {
            var oldSceneObject = new object();
            var newSceneObject = new object();

            // Scene mới đăng ký lại cùng id, rồi object của scene cũ mới OnDisable.
            SceneEntryRegistry.Register("gate_a", new Vector3(5f, 6f, 0f), newSceneObject);
            SceneEntryRegistry.Unregister("gate_a", oldSceneObject);

            SceneEntryRegistry.PendingEntryId = "gate_a";
            Assert.IsTrue(SceneEntryRegistry.TryGetPendingPosition(out var pos));
            Assert.AreEqual(new Vector3(5f, 6f, 0f), pos);
        }

        [Test]
        public void Unregister_WithoutOwner_RemovesEntryUnconditionally()
        {
            SceneEntryRegistry.Register("gate_a", Vector3.one, new object());

            SceneEntryRegistry.Unregister("gate_a");

            SceneEntryRegistry.PendingEntryId = "gate_a";
            Assert.IsFalse(SceneEntryRegistry.TryGetPendingPosition(out _));
        }

        [Test]
        public void ClearPending_StopsTheEntryFromLeakingIntoNextSceneLoad()
        {
            SceneEntryRegistry.Register("gate_a", Vector3.one);
            SceneEntryRegistry.PendingEntryId = "gate_a";

            SceneEntryRegistry.ClearPending();

            Assert.IsNull(SceneEntryRegistry.PendingEntryId);
            Assert.IsFalse(SceneEntryRegistry.TryGetPendingPosition(out _));
        }

        [Test]
        public void MultipleEntries_AreTrackedIndependently()
        {
            SceneEntryRegistry.Register("gate_a", new Vector3(1f, 2f, 7f));
            SceneEntryRegistry.Register("gate_b", new Vector3(3f, 4f, 7f));

            SceneEntryRegistry.PendingEntryId = "gate_b";
            SceneEntryRegistry.TryGetPendingPosition(out var b);
            Assert.AreEqual(new Vector3(3f, 4f, 0f), b);

            SceneEntryRegistry.PendingEntryId = "gate_a";
            SceneEntryRegistry.TryGetPendingPosition(out var a);
            Assert.AreEqual(new Vector3(1f, 2f, 0f), a);
        }
    }
}
