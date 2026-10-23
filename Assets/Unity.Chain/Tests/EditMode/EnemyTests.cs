using System.Collections.Generic;
using MindAttic.Chain;
using MindAttic.Chain.Core;
using NUnit.Framework;
using UnityEngine;

namespace MindAttic.Chain.Tests
{
    public class MaceHitDetectorTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private ChainController _chain;
        private MaceHitDetector _detector;

        [SetUp]
        public void SetUp()
        {
            var root = Track(new GameObject("Chain"));
            _chain = root.AddComponent<ChainController>();
            _chain.Rebuild();
            _detector = root.AddComponent<MaceHitDetector>();
            _detector.Chain = _chain;
            _detector.MaceIndex = 6;
            _detector.HitRadius = 0.25f;
            _detector.DamagePerSpeed = 0.05f;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _objects)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }

            _objects.Clear();
        }

        private GameObject Track(GameObject go)
        {
            _objects.Add(go);
            return go;
        }

        private Enemy SpawnEnemy(Vector2 position, float hp = 100f, float radius = 0.15f)
        {
            var go = Track(new GameObject("Enemy"));
            go.transform.position = position;
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            var circle = go.AddComponent<CircleCollider2D>();
            circle.radius = radius;
            var enemy = go.AddComponent<Enemy>();
            enemy.StartingHp = hp;
            enemy.FixedMass = 1f;
            Physics2D.SyncTransforms();
            return enemy;
        }

        private void SwingChain(int steps = 10)
        {
            for (var i = 0; i < steps; i++)
            {
                _chain.transform.position = new Vector3(i * 0.3f, 0f, 0f);
                _chain.Simulate(1f / 50f);
            }
        }

        [Test]
        public void EnemyAtTheMace_IsHitWithTheMaceVelocity()
        {
            SwingChain();
            var enemy = SpawnEnemy(_detector.MacePosition);
            Enemy hitEnemy = null;
            var hitDamage = 0f;
            _detector.Hit += (e, d) =>
            {
                hitEnemy = e;
                hitDamage = d;
            };

            var velocity = _detector.MaceVelocity;
            Assert.AreNotEqual(Vector2.zero, velocity);
            Assert.AreEqual(1, _detector.Step());

            Assert.AreSame(enemy, hitEnemy);
            Assert.AreEqual(velocity.magnitude * 0.05f, hitDamage, 1e-4f);
            Assert.AreEqual(EnemyState.Hurt, enemy.State);
            Assert.AreEqual(velocity, enemy.Velocity);
            Assert.Less(enemy.Hp, 100f);
        }

        [Test]
        public void EnemyAwayFromTheMace_IsNotHit()
        {
            SwingChain();
            var enemy = SpawnEnemy(_detector.MacePosition + new Vector2(5f, 5f));

            Assert.AreEqual(0, _detector.Step());
            Assert.AreEqual(EnemyState.Seek, enemy.State);
        }

        [Test]
        public void FastSwing_SweepsThroughAnEnemyBetweenSteps()
        {
            _detector.HitRadius = 0.05f;
            SwingChain();
            _detector.Step(); // records the mace's position
            var from = _detector.MacePosition;

            // Keep dragging without a detector step, so the mace travels well past an enemy
            // sitting halfway along its path: a per-step overlap test at either end misses it.
            for (var i = 10; i < 20; i++)
            {
                _chain.transform.position = new Vector3(i * 0.3f, 0f, 0f);
                _chain.Simulate(1f / 50f);
            }

            var to = _detector.MacePosition;
            Assert.Greater((to - from).magnitude, 0.3f);

            var enemy = SpawnEnemy((from + to) * 0.5f, radius: 0.05f);
            Assert.IsFalse(Physics2D.OverlapCircle(to, _detector.HitRadius), "the end position alone should miss");

            Assert.AreEqual(1, _detector.Step());
            Assert.AreEqual(EnemyState.Hurt, enemy.State);
        }

        [Test]
        public void HurtEnemy_IsNotHitTwice()
        {
            SwingChain();
            var enemy = SpawnEnemy(_detector.MacePosition);

            Assert.AreEqual(1, _detector.Step());
            var hp = enemy.Hp;
            Assert.AreEqual(0, _detector.Step());
            Assert.AreEqual(hp, enemy.Hp);
        }

        [Test]
        public void LethalHit_KillsAndRemovesTheEnemy()
        {
            SwingChain();
            var enemy = SpawnEnemy(_detector.MacePosition, hp: 0.0001f);
            var died = 0;
            enemy.Died += _ => died++;

            _detector.Step();

            Assert.AreEqual(1, died);
            Assert.IsFalse(enemy.IsAlive);
            Assert.IsFalse(enemy.gameObject.activeSelf);
        }

        [Test]
        public void ArmedGrapple_CatchesInsteadOfHitting_AndTheHeldEnemyShares_LaterDamage()
        {
            var grapple = _chain.gameObject.AddComponent<GrappleController>();
            grapple.Chain = _chain;
            grapple.UseMouseButton = false;
            grapple.MaceIndex = 6;
            grapple.CatchRadius = 0f;
            _detector.Grapple = grapple;

            SwingChain();
            grapple.SetButtonHeld(true);
            grapple.Step();
            Assert.AreEqual(GrappleState.Armed, grapple.State);

            var caught = SpawnEnemy(_detector.MacePosition);
            Assert.AreEqual(0, _detector.Step());
            Assert.AreSame(caught, grapple.Target);
            Assert.AreEqual(EnemyState.Grabbed, caught.State);
            Assert.AreEqual(100f, caught.Hp);

            var victim = SpawnEnemy(_detector.MacePosition + new Vector2(0.1f, 0f));
            Assert.AreEqual(1, _detector.Step());
            Assert.AreEqual(EnemyState.Hurt, victim.State);
            Assert.AreEqual(100f - victim.Hp, 100f - caught.Hp, 1e-4f);

            // Release: the held enemy is thrown with the mace velocity and knocks what it touches.
            grapple.SetButtonHeld(false);
            grapple.Step();
            Assert.AreEqual(EnemyState.Hurt, caught.State);
            Assert.IsTrue(caught.Brain.IsThrown);
        }

        [Test]
        public void ThrownEnemy_KnocksEnemiesItTouches()
        {
            var thrown = SpawnEnemy(Vector2.zero);
            var bystander = SpawnEnemy(new Vector2(0.2f, 0f));
            bystander.FixedMass = 2f;

            thrown.OnGrappled();
            thrown.Throw(new Vector2(4f, 0f));
            thrown.Step(1f / 50f);
            Physics2D.SyncTransforms();
            thrown.Step(1f / 50f);

            Assert.AreEqual(EnemyState.Hurt, bystander.State);
            Assert.Greater(bystander.Velocity.x, 0f);
            Assert.AreEqual(100f, bystander.Hp);
        }

        [Test]
        public void OverlappingEnemies_SeparateOverTime()
        {
            var a = SpawnEnemy(new Vector2(0f, 0f));
            var b = SpawnEnemy(new Vector2(0.1f, 0f));

            for (var i = 0; i < 50; i++)
            {
                a.Step(1f / 50f);
                b.Step(1f / 50f);
                Physics2D.SyncTransforms();
            }

            Assert.Less(a.Position.x, 0f);
            Assert.Greater(b.Position.x, 0.1f);
        }

        [Test]
        public void GrabbedEnemy_IsNotPushedAndNotCounted()
        {
            var free = SpawnEnemy(new Vector2(0f, 0f));
            var held = SpawnEnemy(new Vector2(0.1f, 0f));
            held.OnGrappled();

            free.Step(1f / 50f);

            Assert.AreEqual(0f, free.Position.x, 1e-6f);
            Assert.AreEqual(0.1f, held.Position.x, 1e-6f);
        }

        [Test]
        public void Enemy_SeeksItsTarget()
        {
            var target = Track(new GameObject("Angel"));
            target.transform.position = new Vector3(3f, 0f, 0f);
            var enemy = SpawnEnemy(Vector2.zero);
            enemy.Target = target.transform;

            enemy.Step(1f / 50f);

            Assert.Greater(enemy.Position.x, 0f);
            Assert.AreEqual(0f, enemy.Position.y, 1e-5f);
        }
    }
}
