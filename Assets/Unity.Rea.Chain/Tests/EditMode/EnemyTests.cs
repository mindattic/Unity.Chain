using MindAttic.Chain.Core;
using NUnit.Framework;
using UnityEngine;

namespace MindAttic.Chain.Tests
{
    /// <summary>The enemy view follows the world; the enemy rules (hits, sweep, grab, throw, flocking, seek) are tested in Tests/Core.Tests (GameWorldTests).</summary>
    public class EnemyViewTests
    {
        private TestRig _rig;

        [SetUp]
        public void SetUp() => _rig = new TestRig();

        [TearDown]
        public void TearDown() => _rig.Dispose();

        private Enemy View(EnemyEntity entity)
        {
            var view = _rig.Track(new GameObject("Enemy")).AddComponent<Enemy>();
            view.Bind(entity);
            return view;
        }

        [Test]
        public void View_StandsWhereTheWorldHasTheEnemy()
        {
            var entity = _rig.SpawnEnemy(new Vector2(3f, 1f));
            var view = View(entity);
            Assert.AreEqual(new Vector2(3f, 1f), (Vector2)view.transform.position);

            _rig.Step();
            view.Follow();

            Assert.Less(view.transform.position.x, 3f, "it seeks the angel at the origin");
            Assert.AreEqual(entity.Position.ToUnity(), (Vector2)view.transform.position);
            Assert.AreEqual(entity.Id, view.Id);
            Assert.AreEqual(EnemyState.Seek, view.State);
        }

        [Test]
        public void MaceHit_IsReadByTheView()
        {
            _rig.Swing();
            var entity = _rig.SpawnEnemy(_rig.Player.MacePosition.ToUnity());
            var view = View(entity);
            _rig.Step();

            Assert.AreEqual(EnemyState.Hurt, view.State);
            Assert.Less(view.Hp, 100f);
            Assert.AreEqual(entity.Velocity.ToUnity(), view.Velocity);
        }

        [Test]
        public void PlayDeath_HidesTheView_OutsidePlayMode()
        {
            var view = View(_rig.SpawnEnemy(new Vector2(3f, 1f)));
            view.PlayDeath();

            Assert.IsFalse(view.gameObject.activeSelf);
        }
    }
}
