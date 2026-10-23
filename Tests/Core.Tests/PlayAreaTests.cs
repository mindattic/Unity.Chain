using MindAttic.Chain.Core;
using NUnit.Framework;

namespace MindAttic.Chain.Tests
{
    public class PlayAreaTests
    {
        private static readonly Rect2 Level = new Rect2(-8f, -4.5f, 16f, 9f);

        [Test]
        public void Inset_ShrinksEverySide()
        {
            var inset = PlayArea.Inset(Level, new Vec2(1f, 0.5f));
            Assert.AreEqual(new Rect2(-7f, -4f, 14f, 8f), inset);
        }

        [Test]
        public void Inset_LargerThanHalf_CollapsesOntoCenter()
        {
            var inset = PlayArea.Inset(new Rect2(2f, 2f, 2f, 2f), new Vec2(5f, 5f));
            Assert.AreEqual(new Vec2(3f, 3f), inset.Center);
            Assert.AreEqual(Vec2.Zero, inset.Size);
        }

        [Test]
        public void Bounce_PastAWall_PutsPointOnEdge_AndReflectsScaledVelocity()
        {
            var position = new Vec2(9f, 0f);
            var velocity = new Vec2(4f, 1f);

            Assert.IsTrue(PlayArea.Bounce(Level, ref position, ref velocity, 0.5f));
            Assert.AreEqual(new Vec2(8f, 0f), position);
            Assert.AreEqual(new Vec2(-2f, 1f), velocity, "only the axis that hit is reflected");
        }

        [Test]
        public void Bounce_VelocityAlreadyHeadingBackIn_IsLeftAlone()
        {
            var position = new Vec2(0f, -5f);
            var velocity = new Vec2(0f, 3f);

            PlayArea.Bounce(Level, ref position, ref velocity, 0.5f);
            Assert.AreEqual(new Vec2(0f, -4.5f), position);
            Assert.AreEqual(new Vec2(0f, 3f), velocity);
        }

        [Test]
        public void Bounce_Inside_DoesNothing()
        {
            var position = new Vec2(1f, 1f);
            var velocity = new Vec2(5f, 5f);
            Assert.IsFalse(PlayArea.Bounce(Level, ref position, ref velocity, 0.5f));
            Assert.AreEqual(new Vec2(1f, 1f), position);
            Assert.AreEqual(new Vec2(5f, 5f), velocity);
        }

        [Test]
        public void ChainSimulation_WithBounds_KeepsEveryLinkInside()
        {
            var bounds = new Rect2(-2f, -1f, 4f, 2f);
            var sim = new ChainSimulation(12, Vec2.Zero, ChainSimulation.Settings.Default)
            {
                Bounds = bounds,
                WallBounce = 0.5f,
            };

            // Whip the anchor back and forth near a wall so the tail is flung hard into it.
            for (var step = 0; step < 300; step++)
            {
                sim.SetAnchor(new Vec2(step % 20 < 10 ? 1.9f : -1.9f, 0.9f));
                sim.Step(1f / 50f);
                for (var i = 1; i < sim.LinkCount; i++)
                {
                    var p = sim.GetPosition(i);
                    Assert.IsTrue(p.X >= bounds.XMin && p.X <= bounds.XMax && p.Y >= bounds.YMin && p.Y <= bounds.YMax,
                        $"link {i} escaped to {p} at step {step}");
                }
            }
        }

        [Test]
        public void ChainSimulation_WithoutBounds_IsUnconstrained()
        {
            var sim = new ChainSimulation(6, Vec2.Zero, ChainSimulation.Settings.Default);
            for (var step = 0; step < 200; step++)
            {
                sim.SetAnchor(Vec2.Zero);
                sim.Step(1f / 50f);
            }

            Assert.Less(sim.GetPosition(5).Y, -1f, "gravity hangs the chain well below a 1-unit floor");
        }
    }
}
