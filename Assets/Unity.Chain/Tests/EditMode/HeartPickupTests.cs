using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace MindAttic.Chain.Tests
{
    public class HeartPickupTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private GameSession _session;
        private AngelController _angel;

        [SetUp]
        public void SetUp()
        {
            HeartPickup.AfterReset = false;
            _angel = Track(new GameObject("Angel")).AddComponent<AngelController>();
            _session = Track(new GameObject("Session")).AddComponent<GameSession>();
            _session.Angel = _angel;
            _angel.transform.position = new Vector3(0f, 0.27f, 0f); // its rectangle touches the heart's start
            _session.Join(_angel); // seated where it stands
        }

        [TearDown]
        public void TearDown()
        {
            HeartPickup.AfterReset = false;
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

        private HeartPickup SpawnHeart()
        {
            var go = Track(new GameObject("Heart"));
            go.AddComponent<SpriteRenderer>();
            return go.AddComponent<HeartPickup>();
        }

        [Test]
        public void Touch_HealsTenHp()
        {
            _session.State.TryHurt(1f);
            _session.State.Config.HurtDelay = 0f;
            for (var i = 0; i < 14; i++)
            {
                _session.State.TryHurt(2f + i);
            }

            Assert.AreEqual(15, _session.State.Hp);
            SpawnHeart();

            Assert.AreEqual(10, _session.CheckPickups());
            Assert.AreEqual(25, _session.State.Hp);
        }

        [Test]
        public void Touch_CapsAtMaxHp()
        {
            _session.State.TryHurt(1f);
            SpawnHeart();

            Assert.AreEqual(1, _session.CheckPickups());
            Assert.AreEqual(30, _session.State.Hp);
        }

        [Test]
        public void FarAngel_DoesNotHeal()
        {
            _session.State.TryHurt(1f);
            _angel.SendInput(new Vector2(500f, 500f), 0f); // the world flies it away
            SpawnHeart();

            Assert.AreEqual(0, _session.CheckPickups());
            Assert.AreEqual(29, _session.State.Hp);
        }

        [Test]
        public void CollectedHeart_IsGoneAndHealsOnlyOnce()
        {
            _session.State.TryHurt(1f);
            var heart = SpawnHeart();

            Assert.AreEqual(1, _session.CheckPickups());
            Assert.IsTrue(heart.IsCollected);
            Assert.IsFalse(heart.Renderer.enabled);
            Assert.AreEqual(0, _session.CheckPickups());
        }

        [Test]
        public void Heart_ShowsTheRulesPosition()
        {
            var heart = SpawnHeart();
            heart.Step(1f + 1e-4f);

            Assert.AreEqual(heart.Rules.X, heart.transform.position.x, 1e-6f);
            Assert.AreEqual(0.4f - 0.06f, heart.transform.position.y, 1e-4f);
        }

        [Test]
        public void SessionRestart_BringsTheHeartBackAtTheResetFigures()
        {
            var heart = SpawnHeart();
            _session.CheckPickups();
            Assert.IsTrue(heart.IsCollected);

            _session.Restart();

            Assert.IsFalse(heart.IsCollected);
            Assert.IsTrue(heart.Renderer.enabled);
            Assert.AreEqual(0.4f, heart.transform.position.y, 1e-6f);
            Assert.AreEqual(0.001f, heart.Rules.FloatSpeed);
        }
    }
}
