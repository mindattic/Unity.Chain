using MindAttic.Chain.Core;
using NUnit.Framework;


namespace MindAttic.Chain.Tests
{
    public class AngelFlightTests
    {
        private static AngelFlight.Settings Flat(float hoverAmplitude = 0f) => new AngelFlight.Settings(
            sensitivity: 0.01f,
            bounds: new Rect2(-5f, -3f, 10f, 6f),
            hoverAmplitude: hoverAmplitude,
            hoverPeriod: 2f,
            frameDuration: 0.1f,
            frameCount: 4,
            handOffset: new Vec2(0.25f, -0.1f));

        [Test]
        public void MouseDelta_MovesByDeltaTimesSensitivity()
        {
            var angel = new AngelFlight(Vec2.Zero, Flat());

            angel.Step(new Vec2(100f, -50f), 1f / 60f);

            Assert.AreEqual(1f, angel.Position.X, 1e-5f);
            Assert.AreEqual(-0.5f, angel.Position.Y, 1e-5f, "Input System deltas are Y-up; no inversion");
        }

        [Test]
        public void StillMouse_DoesNotMove()
        {
            var angel = new AngelFlight(new Vec2(1f, 1f), Flat());

            for (var i = 0; i < 30; i++)
            {
                angel.Step(Vec2.Zero, 1f / 60f);
            }

            Assert.AreEqual(new Vec2(1f, 1f), angel.Position);
        }

        [Test]
        public void Facing_FollowsHorizontalMouseDirection_AndHoldsWhenStill()
        {
            var angel = new AngelFlight(Vec2.Zero, Flat());
            Assert.AreEqual(Facing.Right, angel.Facing, "starts facing right, like Angel.vb");

            angel.Step(new Vec2(-5f, 0f), 0.016f);
            Assert.AreEqual(Facing.Left, angel.Facing);

            angel.Step(new Vec2(0f, 10f), 0.016f);
            Assert.AreEqual(Facing.Left, angel.Facing, "pure vertical motion keeps the last facing");

            angel.Step(new Vec2(3f, 0f), 0.016f);
            Assert.AreEqual(Facing.Right, angel.Facing);
        }

        [Test]
        public void Position_IsClampedToBounds()
        {
            var angel = new AngelFlight(Vec2.Zero, Flat());

            angel.Step(new Vec2(100000f, 100000f), 0.016f);
            Assert.AreEqual(new Vec2(5f, 3f), angel.Position);

            angel.Step(new Vec2(-100000f, -100000f), 0.016f);
            Assert.AreEqual(new Vec2(-5f, -3f), angel.Position);
        }

        [Test]
        public void StartPosition_OutsideBounds_IsClamped()
        {
            var angel = new AngelFlight(new Vec2(50f, -50f), Flat());
            Assert.AreEqual(new Vec2(5f, -3f), angel.Position);
        }

        [Test]
        public void HoverBob_StaysWithinAmplitude_AndOnlyOffsetsRenderPosition()
        {
            const float amplitude = 0.1f;
            var angel = new AngelFlight(Vec2.Zero, Flat(amplitude));
            var sawUp = false;
            var sawDown = false;

            for (var i = 0; i < 240; i++)
            {
                angel.Step(Vec2.Zero, 1f / 60f);
                Assert.LessOrEqual(Calc.Abs(angel.HoverOffset), amplitude + 1e-5f);
                Assert.AreEqual(Vec2.Zero, angel.Position, "bob must not drift the flight position");
                Assert.AreEqual(angel.HoverOffset, angel.RenderPosition.Y, 1e-6f);
                sawUp |= angel.HoverOffset > amplitude * 0.9f;
                sawDown |= angel.HoverOffset < -amplitude * 0.9f;
            }

            Assert.IsTrue(sawUp && sawDown, "bob should reach both peaks over a full cycle");
        }

        [Test]
        public void HoverBob_IsAngelVbsTriangle_OneHoverRateAFrame()
        {
            // Angel.vb at the shipped figures: hoverMax 0.01, hoverRate 0.001 a frame, so a 40-frame cycle.
            var settings = new AngelFlight.Settings(1f, new Rect2(-10f, -10f, 20f, 20f), 0.01f, 40f / 60f, 0.1f, 4, Vec2.Zero);
            var angel = new AngelFlight(Vec2.Zero, settings);
            var expected = new[] { 0.001f, 0.002f, 0.003f, 0.004f, 0.005f, 0.006f, 0.007f, 0.008f, 0.009f, 0.010f,
                0.009f, 0.008f, 0.007f, 0.006f, 0.005f, 0.004f, 0.003f, 0.002f, 0.001f, 0f,
                -0.001f, -0.002f, -0.003f, -0.004f, -0.005f, -0.006f, -0.007f, -0.008f, -0.009f, -0.010f,
                -0.009f, -0.008f, -0.007f, -0.006f, -0.005f, -0.004f, -0.003f, -0.002f, -0.001f, 0f, 0.001f };

            for (var frame = 0; frame < expected.Length; frame++)
            {
                angel.Step(Vec2.Zero, 1f / 60f);
                Assert.AreEqual(expected[frame], angel.HoverOffset, 1e-5f, $"frame {frame + 1}");
            }
        }

        [Test]
        public void HoverBob_DoesNotPushRenderPositionPastClampedFlightPosition()
        {
            var angel = new AngelFlight(Vec2.Zero, Flat(0.1f));
            angel.Step(new Vec2(0f, 100000f), 0.5f);

            Assert.AreEqual(3f, angel.Position.Y, 1e-6f, "the clamp applies to the flight position");
        }

        [Test]
        public void Animation_AdvancesOneFramePerFrameDuration_AndLoops()
        {
            var angel = new AngelFlight(Vec2.Zero, Flat());
            Assert.AreEqual(0, angel.Frame);

            angel.Step(Vec2.Zero, 0.05f);
            Assert.AreEqual(0, angel.Frame, "half a frame duration is not enough to advance");

            angel.Step(Vec2.Zero, 0.06f);
            Assert.AreEqual(1, angel.Frame);

            angel.Step(Vec2.Zero, 0.3f);
            Assert.AreEqual(0, angel.Frame, "4 frames loop back to 0");
        }

        [Test]
        public void HandSocket_MirrorsWithFacing()
        {
            var angel = new AngelFlight(Vec2.Zero, Flat());

            angel.Step(new Vec2(1f, 0f), 0f);
            var right = angel.HandSocket - angel.RenderPosition;

            angel.Step(new Vec2(-1f, 0f), 0f);
            var left = angel.HandSocket - angel.RenderPosition;

            Assert.AreEqual(0.25f, right.X, 1e-6f);
            Assert.AreEqual(-0.25f, left.X, 1e-6f);
            Assert.AreEqual(right.Y, left.Y, 1e-6f);
        }
    }
}
