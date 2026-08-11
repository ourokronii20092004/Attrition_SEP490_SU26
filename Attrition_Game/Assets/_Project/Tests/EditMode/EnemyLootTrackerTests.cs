using Attrition.Controllers;
using Attrition.Persistence;
using NUnit.Framework;
using UnityEngine;

namespace Attrition.Tests.EditMode
{
    /// <summary>
    /// EnemyLootTracker — elite/boss chỉ rơi đồ ĐÚNG MỘT LẦN cho mỗi vị trí spawn.
    /// Khoá gồm scene|enemyId@x,y nên cùng enemyId ở scene khác / vị trí khác vẫn tính là chưa rơi.
    /// Khớp function GELD "Generate Enemy Loot Drops" trong report.
    /// </summary>
    public class EnemyLootTrackerTests
    {
        private const string SceneA = "Map 1";
        private const string SceneB = "Map 2";
        private readonly Vector3 _pos = new(10f, 20f, 0f);

        private LaunchMode _origMode;
        private string _origOwnerId;
        private int _origSlot;
        private string _origScene;

        [SetUp]
        public void SetUp()
        {
            _origMode = GameLaunch.Mode;
            _origOwnerId = GameLaunch.OwnerId;
            _origSlot = GameLaunch.SelectedSlot;
            _origScene = GameLaunch.GameplayScene;

            // Solo + slot 0 + scene Map 1 — không đụng mạng, không đọc save thật (LoadSlot(0) trả null nếu chưa có).
            GameLaunch.Mode = LaunchMode.Solo;
            GameLaunch.OwnerId = "";
            GameLaunch.SelectedSlot = 0;
            GameLaunch.GameplayScene = SceneA;

            EnemyLootTracker.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            EnemyLootTracker.Clear();

            GameLaunch.Mode = _origMode;
            GameLaunch.OwnerId = _origOwnerId;
            GameLaunch.SelectedSlot = _origSlot;
            GameLaunch.GameplayScene = _origScene;
        }

        [Test]
        public void MarkLooted_ThenAlreadyLooted_ReturnsTrue()
        {
            EnemyLootTracker.MarkLooted("elite_demon", _pos);

            Assert.IsTrue(EnemyLootTracker.AlreadyLooted("elite_demon", _pos));
        }

        [Test]
        public void MarkLooted_ReturnsTrueOnlyOnFirstTime()
        {
            Assert.IsTrue(EnemyLootTracker.MarkLooted("elite_demon", _pos));
            Assert.IsFalse(EnemyLootTracker.MarkLooted("elite_demon", _pos));
        }

        [Test]
        public void FreshEnemy_IsNotLooted()
        {
            Assert.IsFalse(EnemyLootTracker.AlreadyLooted("elite_demon", _pos));
        }

        [Test]
        public void SameEnemy_AtDifferentPosition_IsNotLooted()
        {
            EnemyLootTracker.MarkLooted("elite_demon", _pos);

            Assert.IsFalse(EnemyLootTracker.AlreadyLooted("elite_demon", new Vector3(11f, 20f, 0f)));
        }

        [Test]
        public void SameEnemy_SamePosition_DifferentScene_IsNotLooted()
        {
            EnemyLootTracker.MarkLooted("elite_demon", _pos);
            GameLaunch.GameplayScene = SceneB;

            Assert.IsFalse(EnemyLootTracker.AlreadyLooted("elite_demon", _pos));
        }

        [Test]
        public void DifferentEnemy_SamePosition_IsNotLooted()
        {
            EnemyLootTracker.MarkLooted("elite_demon", _pos);

            Assert.IsFalse(EnemyLootTracker.AlreadyLooted("elite_boss", _pos));
        }

        [Test]
        public void Clear_ResetsAllTrackedState()
        {
            EnemyLootTracker.MarkLooted("elite_demon", _pos);
            EnemyLootTracker.Clear();

            Assert.IsFalse(EnemyLootTracker.AlreadyLooted("elite_demon", _pos));
        }

        [Test]
        public void LoadFrom_ThenWriteTo_RoundTripsTheKeys()
        {
            var from = new SaveSlotData { lootedElites = new System.Collections.Generic.List<string> { "Map 1|elite_demon@10,20" } };
            var to = new SaveSlotData();

            EnemyLootTracker.LoadFrom(from);
            EnemyLootTracker.WriteTo(to);

            Assert.IsNotNull(to.lootedElites);
            Assert.Contains("Map 1|elite_demon@10,20", to.lootedElites);
        }

        [Test]
        public void LoadFrom_NullData_ClearsTrackedState()
        {
            EnemyLootTracker.MarkLooted("elite_demon", _pos);

            EnemyLootTracker.LoadFrom(null);

            Assert.IsFalse(EnemyLootTracker.AlreadyLooted("elite_demon", _pos));
        }
    }
}
