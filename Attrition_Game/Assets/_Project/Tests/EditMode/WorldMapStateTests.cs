using System.Collections.Generic;
using Attrition.Gameplay.Environment;
using Attrition.Persistence;
using NUnit.Framework;
using UnityEngine;

namespace Attrition.Tests.EditMode
{
    /// <summary>
    /// WorldMapState — bản đồ tổng (report MAD / PMRP / FOW):
    ///  - MAD: đánh dấu vùng đã khám phá đúng 1 lần (trả true mới khám phá).
    ///  - FOW: fog ô chuẩn hoá theo key scene:cell, clamp ngoài bounds, MarkFogVisited idempotent.
    ///  - PMRP: LoadFrom/WriteTo roundtrip qua SaveSlotData (fog + discoveredCheckpoints).
    ///  - Hợp nhất coop theo sessionId: cùng phòng → union, phòng khác → thay thế.
    ///  - MapDataSO.WorldToCell / FogGridSize / IsMapDiscovered.
    /// </summary>
    public class WorldMapStateTests
    {
        [SetUp]
        public void SetUp() => WorldMapState.Clear();

        [TearDown]
        public void TearDown() => WorldMapState.Clear();

        private static SaveSlotData MakeSlot()
        {
            return new SaveSlotData
            {
                fogVisited = new List<string> { "map1:0:0", "map1:1:2" },
                discoveredCheckpoints = new List<string> { "cp_a", "cp_b" }
            };
        }

        [Test]
        public void MarkCheckpointDiscovered_OnlyOnce()
        {
            Assert.IsTrue(WorldMapState.MarkCheckpointDiscovered("cp_a"));   // lần đầu = mới
            Assert.IsFalse(WorldMapState.MarkCheckpointDiscovered("cp_a"));  // lần 2 = đã có
            Assert.IsTrue(WorldMapState.IsCheckpointDiscovered("cp_a"));
        }

        [Test]
        public void MarkCheckpointDiscovered_NullOrEmpty_Ignored()
        {
            Assert.IsFalse(WorldMapState.MarkCheckpointDiscovered(null));
            Assert.IsFalse(WorldMapState.MarkCheckpointDiscovered(""));
            Assert.IsFalse(WorldMapState.IsCheckpointDiscovered(""));
        }

        [Test]
        public void MarkFogVisited_IsIdempotent()
        {
            Assert.IsTrue(WorldMapState.MarkFogVisited("map1", 0, 0));
            Assert.IsFalse(WorldMapState.MarkFogVisited("map1", 0, 0));
            Assert.IsTrue(WorldMapState.IsFogVisited("map1", 0, 0));
        }

        [Test]
        public void FogKey_IsolatedBySceneAndCell()
        {
            WorldMapState.MarkFogVisited("map1", 0, 0);
            Assert.IsFalse(WorldMapState.IsFogVisited("map2", 0, 0));  // scene khác
            Assert.IsFalse(WorldMapState.IsFogVisited("map1", 0, 1));  // ô khác
        }

        [Test]
        public void LoadFrom_RestoresFogAndCheckpoints()
        {
            WorldMapState.LoadFrom(MakeSlot());

            Assert.IsTrue(WorldMapState.IsFogVisited("map1", 0, 0));
            Assert.IsTrue(WorldMapState.IsCheckpointDiscovered("cp_b"));
            Assert.IsFalse(WorldMapState.IsCheckpointDiscovered("cp_zzz"));
        }

        [Test]
        public void WriteTo_RoundtripsThroughSlotData()
        {
            WorldMapState.MarkFogVisited("map1", 3, 4);
            WorldMapState.MarkCheckpointDiscovered("cp_c");

            var data = new SaveSlotData();
            WorldMapState.WriteTo(data);
            WorldMapState.Clear();                      // xoá bộ nhớ rồi nạp lại từ slot
            WorldMapState.LoadFrom(data);

            Assert.IsTrue(WorldMapState.IsFogVisited("map1", 3, 4));
            Assert.IsTrue(WorldMapState.IsCheckpointDiscovered("cp_c"));
        }

        [Test]
        public void LoadFrom_Null_ClearsEverything()
        {
            WorldMapState.MarkFogVisited("map1", 0, 0);
            WorldMapState.MarkCheckpointDiscovered("cp_a");

            WorldMapState.LoadFrom(null);

            Assert.IsFalse(WorldMapState.IsFogVisited("map1", 0, 0));
            Assert.IsFalse(WorldMapState.IsCheckpointDiscovered("cp_a"));
        }

        [Test]
        public void LoadFromCoop_SameSession_Merges()
        {
            // Lần đầu: phòng P1.
            WorldMapState.LoadFromCoop(new[] { "m:0:0" }, new[] { "cp_a" }, "P1");
            // Fetch lại CÙNG phòng: boss/fog vừa mở mà server chưa kịp lưu → phải hợp nhất, không xoá.
            WorldMapState.LoadFromCoop(new[] { "m:1:1" }, new[] { "cp_b" }, "P1");

            Assert.IsTrue(WorldMapState.IsFogVisited("m", 0, 0));
            Assert.IsTrue(WorldMapState.IsFogVisited("m", 1, 1));
            Assert.IsTrue(WorldMapState.IsCheckpointDiscovered("cp_a"));
            Assert.IsTrue(WorldMapState.IsCheckpointDiscovered("cp_b"));
        }

        [Test]
        public void LoadFromCoop_DifferentSession_Replaces()
        {
            WorldMapState.LoadFromCoop(new[] { "m:0:0" }, new[] { "cp_a" }, "P1");
            // Sang phòng KHÁC → thay thế sạch, không để boss/fog phòng trước lẫn sang.
            WorldMapState.LoadFromCoop(new[] { "m:9:9" }, new[] { "cp_z" }, "P2");

            Assert.IsFalse(WorldMapState.IsFogVisited("m", 0, 0));
            Assert.IsFalse(WorldMapState.IsCheckpointDiscovered("cp_a"));
            Assert.IsTrue(WorldMapState.IsFogVisited("m", 9, 9));
            Assert.IsTrue(WorldMapState.IsCheckpointDiscovered("cp_z"));
        }

        [Test]
        public void IsMapDiscovered_AnyFogOrCheckpointInScene_Counts()
        {
            var map = ScriptableObject.CreateInstance<MapDataSO>();
            map.sceneName = "map1";

            WorldMapState.MarkFogVisited("map1", 2, 2);
            Assert.IsTrue(WorldMapState.IsMapDiscovered(map));
            Object.DestroyImmediate(map);
        }

        [Test]
        public void IsMapDiscovered_NullMap_ReturnsFalse()
        {
            Assert.IsFalse(WorldMapState.IsMapDiscovered(null));
        }

        [Test]
        public void MapData_WorldToCell_ClampsOutsideBounds()
        {
            var map = ScriptableObject.CreateInstance<MapDataSO>();
            map.fogCellSize = 2.5f;
            map.worldBounds = new Bounds(new Vector3(0f, 0f, 0f), new Vector3(10f, 10f, 0f)); // -5..5

            // Trong bounds: (-5, -5) là ô (0,0).
            Assert.AreEqual(new Vector2Int(0, 0), map.WorldToCell(new Vector2(-5f, -5f)));
            // Ngoài bounds phải clamp về biên, không âm.
            Assert.AreEqual(new Vector2Int(0, 0), map.WorldToCell(new Vector2(-999f, -999f)));
            Assert.AreEqual(new Vector2Int(3, 3), map.WorldToCell(new Vector2(999f, 999f)));

            Object.DestroyImmediate(map);
        }

        [Test]
        public void MapData_FogGridSize_ComputesFromBoundsAndCellSize()
        {
            var map = ScriptableObject.CreateInstance<MapDataSO>();
            map.fogCellSize = 2.5f;
            map.worldBounds = new Bounds(new Vector3(0f, 0f, 0f), new Vector3(10f, 10f, 0f));

            // size 10 / cell 2.5 = 4 ô mỗi chiều.
            Assert.AreEqual(new Vector2Int(4, 4), map.FogGridSize());

            Object.DestroyImmediate(map);
        }
    }
}
