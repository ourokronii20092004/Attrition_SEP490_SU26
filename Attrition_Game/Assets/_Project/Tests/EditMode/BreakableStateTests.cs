using System.Collections.Generic;
using Attrition.Gameplay.Environment;
using Attrition.Persistence;
using NUnit.Framework;
using UnityEngine;

namespace Attrition.Tests.EditMode
{
    /// <summary>
    /// BreakableState — vật phá được đã vỡ (report DBO): khoá "scene@x,y" (vị trí làm tròn),
    /// MarkBroken idempotent, LoadFrom/WriteTo roundtrip, và load lại đúng khi đổi slot.
    /// </summary>
    public class BreakableStateTests
    {
        [SetUp]
        public void SetUp() => BreakableState.Clear();

        [TearDown]
        public void TearDown() => BreakableState.Clear();

        [Test]
        public void MarkBroken_KeyedBySceneAndRoundedPosition()
        {
            Assert.IsTrue(BreakableState.MarkBroken("map1", new Vector3(1.2f, 3.7f, 0f)));
            Assert.IsFalse(BreakableState.MarkBroken("map1", new Vector3(1.2f, 3.7f, 0f))); // lặp lại = đã có
            Assert.IsTrue(BreakableState.IsBroken("map1", new Vector3(1.0f, 4.0f, 0f)));     // cùng ô, làm tròn khớp
        }

        [Test]
        public void MarkBroken_DifferentSceneOrCell_IsNotBroken()
        {
            BreakableState.MarkBroken("map1", new Vector3(1.2f, 3.7f, 0f));

            Assert.IsFalse(BreakableState.IsBroken("map2", new Vector3(1.2f, 3.7f, 0f)));  // scene khác
            Assert.IsFalse(BreakableState.IsBroken("map1", new Vector3(2.2f, 3.7f, 0f)));  // ô khác
        }

        [Test]
        public void MarkBroken_EmptyScene_Ignored()
        {
            Assert.IsFalse(BreakableState.MarkBroken("", new Vector3(1f, 1f, 0f)));
        }

        [Test]
        public void WriteTo_RoundtripsThroughSlotData()
        {
            BreakableState.MarkBroken("map1", new Vector3(5f, 5f, 0f));

            var data = new SaveSlotData();
            BreakableState.WriteTo(data);
            BreakableState.Clear();
            BreakableState.LoadFrom(data);

            Assert.IsTrue(BreakableState.IsBroken("map1", new Vector3(5f, 5f, 0f)));
        }

        [Test]
        public void LoadFrom_Null_ClearsEverything()
        {
            BreakableState.MarkBroken("map1", new Vector3(1f, 1f, 0f));
            BreakableState.LoadFrom(null);

            Assert.IsFalse(BreakableState.IsBroken("map1", new Vector3(1f, 1f, 0f)));
        }
    }
}
