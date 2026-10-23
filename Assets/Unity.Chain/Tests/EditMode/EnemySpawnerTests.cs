using MindAttic.Chain.Core;
using NUnit.Framework;
using UnityEngine;

namespace MindAttic.Chain.Tests
{
    /// <summary>The spawner makes, kills and removes enemy views as the world reports its enemies.</summary>
    public class EnemySpawnerTests
    {
        private TestRig _rig;
        private EnemySpawner _spawner;

        [SetUp]
        public void SetUp()
        {
            _rig = new TestRig();
            _spawner = _rig.Track(new GameObject("Spawner")).AddComponent<EnemySpawner>();
            _spawner.Session = _rig.Session;
        }

        [TearDown]
        public void TearDown() => _rig.Dispose();

        private Enemy[] Views() => _spawner.GetComponentsInChildren<Enemy>();

        [Test]
        public void Reset_SpawnsFourteen_AndAnotherResetReplacesThem()
        {
            _rig.Session.Restart();
            Assert.AreEqual(14, _rig.Session.World.Enemies.Count);
            Assert.AreEqual(14, _spawner.Alive);
            var before = Views();
            Assert.AreEqual(14, before.Length);

            _rig.Session.Restart();
            var after = Views();
            Assert.AreEqual(14, after.Length);
            foreach (var enemy in after)
            {
                CollectionAssert.DoesNotContain(before, enemy);
                Assert.IsTrue(enemy.IsAlive);
                Assert.IsTrue(enemy.Entity.Brain.Mass >= 1f && enemy.Entity.Brain.Mass <= 2f);
            }
        }

        [Test]
        public void Views_PickFrames_OrFallBackToADisc()
        {
            _rig.Session.Restart();
            foreach (var enemy in Views())
            {
                Assert.IsNotNull(enemy.GetComponent<SpriteRenderer>().sprite);
            }

            var tex = new Texture2D(2, 2);
            var frame = Sprite.Create(tex, new Rect(0, 0, 2, 2), Vector2.zero);
            _spawner.Frames = new[] { frame };
            _rig.Session.Restart();
            foreach (var enemy in Views())
            {
                Assert.AreSame(frame, enemy.GetComponent<SpriteRenderer>().sprite);
            }

            Object.DestroyImmediate(tex);
        }

        [Test]
        public void KilledEnemy_PlaysItsDeath()
        {
            _rig.Swing();
            var entity = _rig.SpawnEnemy(_rig.Player.MacePosition.ToUnity(), hp: 0.0001f);
            _rig.Step(0f); // hands the spawn event over without moving anything
            var view = _spawner.ViewOf(entity.Id);
            Assert.IsNotNull(view);

            _rig.Step();

            Assert.IsFalse(entity.InWorld);
            Assert.IsFalse(view.gameObject.activeSelf);
            Assert.IsNull(_spawner.ViewOf(entity.Id));
        }

        [Test]
        public void Spawner_ShowsEnemiesAlreadyInTheWorld_WhenItIsWired()
        {
            var late = _rig.Track(new GameObject("Late")).AddComponent<EnemySpawner>();
            _rig.SpawnEnemy(new Vector2(2f, 2f));
            late.Session = _rig.Session;

            Assert.AreEqual(1, late.GetComponentsInChildren<Enemy>().Length);
        }

        [Test]
        public void UnwiredSpawner_ShowsNothing()
        {
            var loose = _rig.Track(new GameObject("Loose")).AddComponent<EnemySpawner>();
            _rig.Session.Restart();

            Assert.AreEqual(0, loose.GetComponentsInChildren<Enemy>().Length);
        }
    }
}
