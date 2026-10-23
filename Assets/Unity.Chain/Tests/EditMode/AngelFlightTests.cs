using MindAttic.Chain;
using NUnit.Framework;
using UnityEngine;

namespace MindAttic.Chain.Tests
{
    public class AngelFlightTests
    {
        private static AngelFlight.Settings Flat(float hoverAmplitude = 0f) => new AngelFlight.Settings(
            sensitivity: 0.01f,
            bounds: new Rect(-5f, -3f, 10f, 6f),
            hoverAmplitude: hoverAmplitude,
            hoverPeriod: 2f,
            frameDuration: 0.1f,
            frameCount: 4,
            handOffset: new Vector2(0.25f, -0.1f));

        [Test]
        public void MouseDelta_MovesByDeltaTimesSensitivity()
        {
            var angel = new AngelFlight(Vector2.zero, Flat());

            angel.Step(new Vector2(100f, -50f), 1f / 60f);

            Assert.AreEqual(1f, angel.Position.x, 1e-5f);
            Assert.AreEqual(-0.5f, angel.Position.y, 1e-5f, "Input System deltas are Y-up; no inversion");
        }

        [Test]
        public void StillMouse_DoesNotMove()
        {
            var angel = new AngelFlight(new Vector2(1f, 1f), Flat());

            for (var i = 0; i < 30; i++)
            {
                angel.Step(Vector2.zero, 1f / 60f);
            }

            Assert.AreEqual(new Vector2(1f, 1f), angel.Position);
        }

        [Test]
        public void Facing_FollowsHorizontalMouseDirection_AndHoldsWhenStill()
        {
            var angel = new AngelFlight(Vector2.zero, Flat());
            Assert.AreEqual(Facing.Right, angel.Facing, "starts facing right, like Angel.vb");

            angel.Step(new Vector2(-5f, 0f), 0.016f);
            Assert.AreEqual(Facing.Left, angel.Facing);

            angel.Step(new Vector2(0f, 10f), 0.016f);
            Assert.AreEqual(Facing.Left, angel.Facing, "pure vertical motion keeps the last facing");

            angel.Step(new Vector2(3f, 0f), 0.016f);
            Assert.AreEqual(Facing.Right, angel.Facing);
        }

        [Test]
        public void Position_IsClampedToBounds()
        {
            var angel = new AngelFlight(Vector2.zero, Flat());

            angel.Step(new Vector2(100000f, 100000f), 0.016f);
            Assert.AreEqual(new Vector2(5f, 3f), angel.Position);

            angel.Step(new Vector2(-100000f, -100000f), 0.016f);
            Assert.AreEqual(new Vector2(-5f, -3f), angel.Position);
        }

        [Test]
        public void StartPosition_OutsideBounds_IsClamped()
        {
            var angel = new AngelFlight(new Vector2(50f, -50f), Flat());
            Assert.AreEqual(new Vector2(5f, -3f), angel.Position);
        }

        [Test]
        public void HoverBob_StaysWithinAmplitude_AndOnlyOffsetsRenderPosition()
        {
            const float amplitude = 0.1f;
            var angel = new AngelFlight(Vector2.zero, Flat(amplitude));
            var sawUp = false;
            var sawDown = false;

            for (var i = 0; i < 240; i++)
            {
                angel.Step(Vector2.zero, 1f / 60f);
                Assert.LessOrEqual(Mathf.Abs(angel.HoverOffset), amplitude + 1e-5f);
                Assert.AreEqual(Vector2.zero, angel.Position, "bob must not drift the flight position");
                Assert.AreEqual(angel.HoverOffset, angel.RenderPosition.y, 1e-6f);
                sawUp |= angel.HoverOffset > amplitude * 0.9f;
                sawDown |= angel.HoverOffset < -amplitude * 0.9f;
            }

            Assert.IsTrue(sawUp && sawDown, "bob should reach both peaks over a full cycle");
        }

        [Test]
        public void HoverBob_DoesNotPushRenderPositionPastClampedFlightPosition()
        {
            var angel = new AngelFlight(Vector2.zero, Flat(0.1f));
            angel.Step(new Vector2(0f, 100000f), 0.5f);

            Assert.AreEqual(3f, angel.Position.y, 1e-6f, "the clamp applies to the flight position");
        }

        [Test]
        public void Animation_AdvancesOneFramePerFrameDuration_AndLoops()
        {
            var angel = new AngelFlight(Vector2.zero, Flat());
            Assert.AreEqual(0, angel.Frame);

            angel.Step(Vector2.zero, 0.05f);
            Assert.AreEqual(0, angel.Frame, "half a frame duration is not enough to advance");

            angel.Step(Vector2.zero, 0.06f);
            Assert.AreEqual(1, angel.Frame);

            angel.Step(Vector2.zero, 0.3f);
            Assert.AreEqual(0, angel.Frame, "4 frames loop back to 0");
        }

        [Test]
        public void HandSocket_MirrorsWithFacing()
        {
            var angel = new AngelFlight(Vector2.zero, Flat());

            angel.Step(new Vector2(1f, 0f), 0f);
            var right = angel.HandSocket - angel.RenderPosition;

            angel.Step(new Vector2(-1f, 0f), 0f);
            var left = angel.HandSocket - angel.RenderPosition;

            Assert.AreEqual(0.25f, right.x, 1e-6f);
            Assert.AreEqual(-0.25f, left.x, 1e-6f);
            Assert.AreEqual(right.y, left.y, 1e-6f);
        }
    }
}
