using NUnit.Framework;
using UnityEngine;

namespace MindAttic.Chain.Tests
{
    public class EnemySpawnerTests
    {
        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                Object.DestroyImmediate(_go);
            }
        }

        private EnemySpawner NewSpawner()
        {
            _go = new GameObject("Spawner");
            return _go.AddComponent<EnemySpawner>();
        }

        [Test]
        public void DefaultCount_IsFourteen()
        {
            Assert.AreEqual(14, NewSpawner().Count);
        }

        [Test]
        public void RandomPositions_AreInsideArea()
        {
            var area = new Rect(-8f, -4.5f, 16f, 9f);
            var points = EnemySpawner.RandomPositions(area, 200, new System.Random(1));

            Assert.AreEqual(200, points.Length);
            foreach (var p in points)
            {
                Assert.IsTrue(area.Contains(p));
            }
        }

        [Test]
        public void Spawn_CreatesCount_AndRespawnReplacesThem()
        {
            var spawner = NewSpawner();
            spawner.Spawn();
            Assert.AreEqual(14, spawner.Alive);
            Assert.AreEqual(14, _go.GetComponentsInChildren<Enemy>().Length);

            spawner.Spawn();
            Assert.AreEqual(14, spawner.Alive);
            Assert.AreEqual(14, _go.GetComponentsInChildren<Enemy>().Length);
        }

        [Test]
        public void Spawn_PicksFrames_AndMassInRange()
        {
            var spawner = NewSpawner();
            var tex = new Texture2D(2, 2);
            spawner.Frames = new[] { Sprite.Create(tex, new Rect(0, 0, 2, 2), Vector2.zero) };
            spawner.Spawn();

            foreach (var enemy in _go.GetComponentsInChildren<Enemy>())
            {
                Assert.IsNotNull(enemy.GetComponent<SpriteRenderer>().sprite);
                Assert.IsTrue(enemy.Brain.Mass >= 1f && enemy.Brain.Mass <= 2f);
            }

            Object.DestroyImmediate(tex);
        }

        [Test]
        public void SessionRestart_RespawnsEnemies()
        {
            var spawner = NewSpawner();
            var session = new GameObject("Session").AddComponent<GameSession>();
            try
            {
                spawner.Session = session;
                spawner.Spawn();
                foreach (var enemy in _go.GetComponentsInChildren<Enemy>())
                {
                    enemy.TakeDamage(1000f);
                }

                session.Restart();

                Assert.AreEqual(14, spawner.Alive);
            }
            finally
            {
                Object.DestroyImmediate(session.gameObject);
            }
        }
    }
}
