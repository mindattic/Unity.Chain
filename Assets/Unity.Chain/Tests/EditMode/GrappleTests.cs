using MindAttic.Chain;
using NUnit.Framework;
using UnityEngine;

namespace MindAttic.Chain.Tests
{
    internal sealed class FakeTarget : IGrappleTarget
    {
        public bool CanBeGrappled { get; set; } = true;
        public int GrappledCount;
        public int ThrowCount;
        public Vector2 HeldAt;
        public Vector2 ThrowVelocity;

        public void OnGrappled() => GrappledCount++;
        public void HoldAt(Vector2 position) => HeldAt = position;

        public void Throw(Vector2 velocity)
        {
            ThrowCount++;
            ThrowVelocity = velocity;
        }
    }

    public class GrappleTests
    {
        [Test]
        public void StartsIdle_AndCannotCatch()
        {
            var grapple = new Grapple();
            var target = new FakeTarget();

            Assert.AreEqual(GrappleState.Idle, grapple.State);
            Assert.IsFalse(grapple.TryCatch(target));
            Assert.AreEqual(0, target.GrappledCount);
        }

        [Test]
        public void HoldingTheButton_Arms()
        {
            var grapple = new Grapple();
            grapple.SetHeld(true, Vector2.zero);

            Assert.AreEqual(GrappleState.Armed, grapple.State);
            Assert.IsTrue(grapple.IsArmed);
        }

        [Test]
        public void Armed_CatchesOneTarget()
        {
            var grapple = new Grapple();
            var first = new FakeTarget();
            var second = new FakeTarget();
            grapple.SetHeld(true, Vector2.zero);

            Assert.IsTrue(grapple.TryCatch(first));
            Assert.IsFalse(grapple.TryCatch(second));
            Assert.AreEqual(GrappleState.Holding, grapple.State);
            Assert.AreSame(first, grapple.Target);
            Assert.AreEqual(1, first.GrappledCount);
            Assert.AreEqual(0, second.GrappledCount);
        }

        [Test]
        public void Armed_IgnoresTargetsThatCannotBeGrappled()
        {
            var grapple = new Grapple();
            grapple.SetHeld(true, Vector2.zero);

            Assert.IsFalse(grapple.TryCatch(new FakeTarget { CanBeGrappled = false }));
            Assert.AreEqual(GrappleState.Armed, grapple.State);
        }

        [Test]
        public void Release_ThrowsTheHeldTargetWithTheMaceVelocity()
        {
            var grapple = new Grapple();
            var target = new FakeTarget();
            grapple.SetHeld(true, Vector2.zero);
            grapple.TryCatch(target);

            var thrown = grapple.SetHeld(false, new Vector2(3f, -1f));

            Assert.AreSame(target, thrown);
            Assert.AreEqual(1, target.ThrowCount);
            Assert.AreEqual(new Vector2(3f, -1f), target.ThrowVelocity);
            Assert.AreEqual(GrappleState.Idle, grapple.State);
            Assert.IsNull(grapple.Target);
        }

        [Test]
        public void Release_ScalesTheThrowByTheMultiplier()
        {
            var grapple = new Grapple(throwMultiplier: 2f);
            var target = new FakeTarget();
            grapple.SetHeld(true, Vector2.zero);
            grapple.TryCatch(target);
            grapple.SetHeld(false, new Vector2(1f, 2f));

            Assert.AreEqual(new Vector2(2f, 4f), target.ThrowVelocity);
        }

        [Test]
        public void Release_WithNothingCaught_JustDisarms()
        {
            var grapple = new Grapple();
            grapple.SetHeld(true, Vector2.zero);

            Assert.IsNull(grapple.SetHeld(false, Vector2.one));
            Assert.AreEqual(GrappleState.Idle, grapple.State);
        }

        [Test]
        public void StillHeld_DoesNotThrow()
        {
            var grapple = new Grapple();
            var target = new FakeTarget();
            grapple.SetHeld(true, Vector2.zero);
            grapple.TryCatch(target);
            grapple.SetHeld(true, Vector2.one);

            Assert.AreEqual(0, target.ThrowCount);
            Assert.AreEqual(GrappleState.Holding, grapple.State);
        }

        [Test]
        public void Carry_MovesTheHeldTargetToTheMace()
        {
            var grapple = new Grapple();
            var target = new FakeTarget();
            grapple.SetHeld(true, Vector2.zero);
            grapple.TryCatch(target);
            grapple.Carry(new Vector2(4f, 5f));

            Assert.AreEqual(new Vector2(4f, 5f), target.HeldAt);
        }

        [Test]
        public void Drop_LetsGoWithoutThrowing_AndStaysArmed()
        {
            var grapple = new Grapple();
            var target = new FakeTarget();
            grapple.SetHeld(true, Vector2.zero);
            grapple.TryCatch(target);
            grapple.Drop();

            Assert.AreEqual(GrappleState.Armed, grapple.State);
            Assert.IsNull(grapple.Target);
            Assert.IsNull(grapple.SetHeld(false, Vector2.one));
            Assert.AreEqual(0, target.ThrowCount);
        }
    }

    public class GrappleControllerTests
    {
        private GameObject _root;
        private ChainController _chain;
        private GrappleController _grapple;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Chain");
            _chain = _root.AddComponent<ChainController>();
            _chain.Rebuild();
            _grapple = _root.AddComponent<GrappleController>();
            _grapple.Chain = _chain;
            _grapple.UseMouseButton = false;
            _grapple.MaceIndex = 6;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        private void SwingChain()
        {
            // Drag the anchor sideways so the mace link picks up velocity.
            for (var i = 0; i < 10; i++)
            {
                _root.transform.position = new Vector3(i * 0.3f, 0f, 0f);
                _chain.Simulate(1f / 50f);
            }
        }

        [Test]
        public void ChainSimulation_ReportsLinkVelocity()
        {
            SwingChain();

            Assert.AreNotEqual(Vector2.zero, _chain.GetLinkVelocity(_grapple.MaceIndex));
            Assert.AreEqual(Vector2.zero, _chain.GetLinkVelocity(0));
        }

        [Test]
        public void ButtonHeld_ArmsOnStep()
        {
            _grapple.SetButtonHeld(true);
            _grapple.Step();

            Assert.AreEqual(GrappleState.Armed, _grapple.State);
        }

        [Test]
        public void Release_ThrowsTheCaughtTargetWithTheMaceLinkVelocity()
        {
            var target = new FakeTarget();
            _grapple.ThrowMultiplier = 1.5f;
            _grapple.SetButtonHeld(true);
            _grapple.Step();
            Assert.IsTrue(_grapple.TryCatch(target));

            SwingChain();
            _grapple.Step();
            Assert.AreEqual(_grapple.MacePosition, target.HeldAt);

            var expected = _chain.GetLinkVelocity(_grapple.MaceIndex) * 1.5f;
            IGrappleTarget thrownTarget = null;
            var thrownVelocity = Vector2.zero;
            _grapple.Thrown += (t, v) =>
            {
                thrownTarget = t;
                thrownVelocity = v;
            };

            _grapple.SetButtonHeld(false);
            _grapple.Step();

            Assert.AreEqual(1, target.ThrowCount);
            Assert.AreEqual(expected, target.ThrowVelocity);
            Assert.AreSame(target, thrownTarget);
            Assert.AreEqual(expected, thrownVelocity);
            Assert.AreEqual(GrappleState.Idle, _grapple.State);
        }

        [Test]
        public void QuickTapBetweenSteps_StillArms()
        {
            _grapple.SetButtonHeld(true);
            _grapple.SetButtonHeld(false);
            _grapple.Step();

            // Armed for that one step, then released on the next.
            Assert.AreEqual(GrappleState.Armed, _grapple.State);
            _grapple.Step();
            Assert.AreEqual(GrappleState.Idle, _grapple.State);
        }

        [Test]
        public void MaceIndex_IsClampedToTheChain()
        {
            _chain.SetLinkCount(4);
            _grapple.MaceIndex = 10;

            Assert.AreEqual(3, _grapple.MaceIndex);
        }

        [Test]
        public void DestroyedTarget_IsDroppedWithoutThrowing()
        {
            var enemy = new GameObject("Enemy");
            enemy.AddComponent<Rigidbody2D>();
            var target = enemy.AddComponent<GrappleTarget>();

            _grapple.SetButtonHeld(true);
            _grapple.Step();
            Assert.IsTrue(_grapple.TryCatch(target));

            Object.DestroyImmediate(enemy);
            _grapple.Step();
            Assert.AreEqual(GrappleState.Armed, _grapple.State);

            _grapple.SetButtonHeld(false);
            Assert.DoesNotThrow(() => _grapple.Step());
            Assert.AreEqual(GrappleState.Idle, _grapple.State);
        }

        [Test]
        public void GrappleTarget_GoesKinematicWhileHeld_AndRestoresOnThrow()
        {
            var enemy = new GameObject("Enemy");
            try
            {
                var body = enemy.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Dynamic;
                var target = enemy.AddComponent<GrappleTarget>();

                _grapple.SetButtonHeld(true);
                _grapple.Step();
                Assert.IsTrue(_grapple.TryCatch(target));
                Assert.IsTrue(target.IsHeld);
                Assert.AreEqual(RigidbodyType2D.Kinematic, body.bodyType);
                Assert.AreEqual(_grapple.MacePosition, (Vector2)enemy.transform.position);

                _grapple.SetButtonHeld(false);
                _grapple.Step();
                Assert.IsFalse(target.IsHeld);
                Assert.AreEqual(RigidbodyType2D.Dynamic, body.bodyType);
                Assert.AreEqual(_grapple.MaceVelocity, target.LastThrowVelocity);
            }
            finally
            {
                Object.DestroyImmediate(enemy);
            }
        }
    }
}
