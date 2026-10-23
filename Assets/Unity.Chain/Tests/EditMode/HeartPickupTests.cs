using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace MindAttic.Chain.Tests
{
    public class HeartFloatTests
    {
        private static readonly Rect Area = new Rect(-8f, -4.5f, 16f, 9f);

        [Test]
        public void Step_FallsByFloatSpeedOfHeightPerReferenceFrame()
        {
            var heart = new HeartFloat();
            var position = new Vector2(0f, 0f);
            heart.Step(ref position, Area, 1f / 60f);

            Assert.AreEqual(-0.002f * 9f, position.y, 1e-5f);
        }

        [Test]
        public void Step_SwaysLeftAndRight()
        {
            var heart = new HeartFloat();
            var position = Vector2.zero;
            var sawRight = false;
            var sawLeft = false;

            for (var i = 0; i < 3000; i++)
            {
                position.y = 0f; // keep it from wrapping
                heart.Step(ref position, Area, 1f / 60f);
                sawRight |= heart.SwayVelocity > 0f;
                sawLeft |= heart.SwayVelocity < 0f;
            }

            Assert.IsTrue(sawRight);
            Assert.IsTrue(sawLeft);
        }

        [Test]
        public void Step_WrapsToTopWhenItLeavesTheBottom()
        {
            var heart = new HeartFloat();
            var position = new Vector2(1f, Area.yMin + 0.001f);

            Assert.IsTrue(heart.Step(ref position, Area, 1f / 60f));
            Assert.AreEqual(Area.yMax, position.y, 1e-5f);
        }
    }

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

        private HeartPickup SpawnHeart(Vector2 position)
        {
            var go = Track(new GameObject("Heart"));
            go.transform.position = position;
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
            SpawnHeart(new Vector2(0.1f, 0f));

            Assert.AreEqual(10, _session.CheckPickups());
            Assert.AreEqual(25, _session.State.Hp);
        }

        [Test]
        public void Touch_CapsAtMaxHp()
        {
            _session.State.TryHurt(1f);
            SpawnHeart(new Vector2(0.1f, 0f));

            Assert.AreEqual(1, _session.CheckPickups());
            Assert.AreEqual(30, _session.State.Hp);
        }

        [Test]
        public void FarHeart_DoesNotHeal()
        {
            _session.State.TryHurt(1f);
            SpawnHeart(new Vector2(5f, 5f));

            Assert.AreEqual(0, _session.CheckPickups());
            Assert.AreEqual(29, _session.State.Hp);
        }

        [Test]
        public void CollectedHeart_GoesBackToTheTop()
        {
            var heart = SpawnHeart(new Vector2(0.1f, 0f));
            _session.CheckPickups();

            Assert.AreEqual(heart.Area.yMax, heart.transform.position.y, 1e-5f);
        }
    }
}
