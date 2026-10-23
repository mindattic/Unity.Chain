using MindAttic.Chain.Core;
using NUnit.Framework;

namespace MindAttic.Chain.Tests
{
    internal sealed class FakeTarget : IGrappleTarget
    {
        public bool CanBeGrappled { get; set; } = true;
        public int GrappledCount;
        public int ThrowCount;
        public Vec2 HeldAt;
        public Vec2 ThrowVelocity;

        public void OnGrappled() => GrappledCount++;
        public void HoldAt(Vec2 position) => HeldAt = position;

        public void Throw(Vec2 velocity)
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
            grapple.SetHeld(true, Vec2.Zero);

            Assert.AreEqual(GrappleState.Armed, grapple.State);
            Assert.IsTrue(grapple.IsArmed);
        }

        [Test]
        public void Armed_CatchesOneTarget()
        {
            var grapple = new Grapple();
            var first = new FakeTarget();
            var second = new FakeTarget();
            grapple.SetHeld(true, Vec2.Zero);

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
            grapple.SetHeld(true, Vec2.Zero);

            Assert.IsFalse(grapple.TryCatch(new FakeTarget { CanBeGrappled = false }));
            Assert.AreEqual(GrappleState.Armed, grapple.State);
        }

        [Test]
        public void Release_ThrowsTheHeldTargetWithTheMaceVelocity()
        {
            var grapple = new Grapple();
            var target = new FakeTarget();
            grapple.SetHeld(true, Vec2.Zero);
            grapple.TryCatch(target);

            var thrown = grapple.SetHeld(false, new Vec2(3f, -1f));

            Assert.AreSame(target, thrown);
            Assert.AreEqual(1, target.ThrowCount);
            Assert.AreEqual(new Vec2(3f, -1f), target.ThrowVelocity);
            Assert.AreEqual(GrappleState.Idle, grapple.State);
            Assert.IsNull(grapple.Target);
        }

        [Test]
        public void Release_ScalesTheThrowByTheMultiplier()
        {
            var grapple = new Grapple(throwMultiplier: 2f);
            var target = new FakeTarget();
            grapple.SetHeld(true, Vec2.Zero);
            grapple.TryCatch(target);
            grapple.SetHeld(false, new Vec2(1f, 2f));

            Assert.AreEqual(new Vec2(2f, 4f), target.ThrowVelocity);
        }

        [Test]
        public void Release_WithNothingCaught_JustDisarms()
        {
            var grapple = new Grapple();
            grapple.SetHeld(true, Vec2.Zero);

            Assert.IsNull(grapple.SetHeld(false, Vec2.One));
            Assert.AreEqual(GrappleState.Idle, grapple.State);
        }

        [Test]
        public void StillHeld_DoesNotThrow()
        {
            var grapple = new Grapple();
            var target = new FakeTarget();
            grapple.SetHeld(true, Vec2.Zero);
            grapple.TryCatch(target);
            grapple.SetHeld(true, Vec2.One);

            Assert.AreEqual(0, target.ThrowCount);
            Assert.AreEqual(GrappleState.Holding, grapple.State);
        }

        [Test]
        public void Carry_MovesTheHeldTargetToTheMace()
        {
            var grapple = new Grapple();
            var target = new FakeTarget();
            grapple.SetHeld(true, Vec2.Zero);
            grapple.TryCatch(target);
            grapple.Carry(new Vec2(4f, 5f));

            Assert.AreEqual(new Vec2(4f, 5f), target.HeldAt);
        }

        [Test]
        public void Drop_LetsGoWithoutThrowing_AndStaysArmed()
        {
            var grapple = new Grapple();
            var target = new FakeTarget();
            grapple.SetHeld(true, Vec2.Zero);
            grapple.TryCatch(target);
            grapple.Drop();

            Assert.AreEqual(GrappleState.Armed, grapple.State);
            Assert.IsNull(grapple.Target);
            Assert.IsNull(grapple.SetHeld(false, Vec2.One));
            Assert.AreEqual(0, target.ThrowCount);
        }
    }

    public class SweepTests
    {
        [Test]
        public void StartingInside_TouchesAtZero()
        {
            Assert.IsTrue(Sweep.CircleCast(Vec2.Zero, new Vec2(5f, 0f), 0.1f, new Vec2(0.05f, 0f), 0.1f, out var t));
            Assert.AreEqual(0f, t);
        }

        [Test]
        public void PassingThrough_TouchesWhereTheCirclesFirstMeet()
        {
            Assert.IsTrue(Sweep.CircleCast(Vec2.Zero, new Vec2(10f, 0f), 0.5f, new Vec2(5f, 0f), 0.5f, out var t));
            Assert.AreEqual(0.4f, t, 1e-5f);
        }

        [Test]
        public void PassingWide_OrStoppingShort_Misses()
        {
            Assert.IsFalse(Sweep.CircleCast(Vec2.Zero, new Vec2(10f, 0f), 0.1f, new Vec2(5f, 1f), 0.1f, out _));
            Assert.IsFalse(Sweep.CircleCast(Vec2.Zero, new Vec2(2f, 0f), 0.1f, new Vec2(5f, 0f), 0.1f, out _));
        }
    }
}
