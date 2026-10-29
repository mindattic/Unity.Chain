using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace MindAttic.Chain.Tests
{
    /// <summary>The heart view over the world's heart: the world moves, checks, heals and resets it.</summary>
    public class HeartPickupTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private GameSession _session;
        private AngelController _angel;

        [SetUp]
        public void SetUp()
        {
            _angel = Track(new GameObject("Angel")).AddComponent<AngelController>();
            _session = Track(new GameObject("Session")).AddComponent<GameSession>();
            _session.Angel = _angel;
            // Its rectangle's bottom edge on the heart's start (Angel.vb: bottom = Y + 0.03, scaled).
            var heart = SettingsStore.Current.Heart;
            _angel.transform.position = new Vector3(heart.StartX, heart.StartY - heart.AngelRectBottom, 0f);
            _session.Join(_angel); // seated where it stands
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

        private HeartPickup SpawnHeart()
        {
            var go = Track(new GameObject("Heart"));
            go.AddComponent<SpriteRenderer>();
            var heart = go.AddComponent<HeartPickup>();
            _session.AddHeart(heart); // what its Awake does in play
            return heart;
        }

        private void HurtTo(int hp)
        {
            _session.State.Config.HurtDelay = 0f;
            for (var i = 0; _session.State.Hp > hp; i++)
            {
                _session.State.TryHurt(1f + i);
            }
        }

        [Test]
        public void AddHeart_PutsItInTheWorld()
        {
            var heart = SpawnHeart();

            Assert.IsTrue(heart.IsInWorld);
            Assert.AreSame(_session.World.Hearts[0], heart.Rules);
        }

        [Test]
        public void Touch_HealsTenHp()
        {
            HurtTo(15);
            SpawnHeart();

            Assert.AreEqual(10, _session.CheckPickups());
            Assert.AreEqual(25, _session.State.Hp);
        }

        [Test]
        public void Touch_CapsAtMaxHp()
        {
            HurtTo(29);
            SpawnHeart();

            Assert.AreEqual(1, _session.CheckPickups());
            Assert.AreEqual(30, _session.State.Hp);
        }

        [Test]
        public void FarAngel_DoesNotHeal()
        {
            HurtTo(29);
            _angel.SendInput(new Vector2(-5000f, -5000f), 0f); // the world flies it away
            SpawnHeart();

            Assert.AreEqual(0, _session.CheckPickups());
            Assert.AreEqual(29, _session.State.Hp);
        }

        [Test]
        public void CollectedHeart_IsGoneAndHealsOnlyOnce()
        {
            HurtTo(29);
            var heart = SpawnHeart();

            Assert.AreEqual(1, _session.CheckPickups());
            heart.Refresh();
            Assert.IsTrue(heart.IsCollected);
            Assert.IsFalse(heart.Renderer.enabled);
            Assert.AreEqual(0, _session.CheckPickups());
        }

        [Test]
        public void Heart_ShowsTheWorldsPosition()
        {
            _angel.SendInput(new Vector2(-5000f, -5000f), 0f);
            var heart = SpawnHeart();
            for (var i = 0; i < 50; i++)
            {
                _session.Step(0.02f);
            }

            heart.Refresh();
            Assert.Less(heart.Rules.Y, heart.Rules.Config.StartY, "the world moved it");
            Assert.AreEqual(heart.Rules.X, heart.transform.position.x, 1e-6f);
            Assert.AreEqual(heart.Rules.Y, heart.transform.position.y, 1e-6f);
        }

        [Test]
        public void SessionRestart_BringsTheHeartBackAtTheResetFigures()
        {
            var heart = SpawnHeart();
            _session.CheckPickups();
            Assert.IsTrue(heart.IsCollected);

            _session.Restart();
            heart.Refresh();

            Assert.IsFalse(heart.IsCollected);
            Assert.IsTrue(heart.Renderer.enabled);
            Assert.AreEqual(heart.Rules.Config.StartY, heart.transform.position.y, 1e-6f);
            Assert.AreEqual(heart.Rules.Config.ResetFloatSpeed, heart.Rules.FloatSpeed);
        }
    }
}
