using MindAttic.Chain.Core;
using NUnit.Framework;

namespace MindAttic.Chain.Tests
{
    public class CameraFramingTests
    {
        [Test]
        public void FitOrthographicSize_ShowsWholeLevel_OnWideAndTallScreens()
        {
            var level = new Vec2(16f, 9f);

            Assert.AreEqual(4.5f, CameraFraming.FitOrthographicSize(level, 16f / 9f), 1e-4f);
            // 4:3: width is the limit, 16 / 2 / (4/3) = 6.
            Assert.AreEqual(6f, CameraFraming.FitOrthographicSize(level, 4f / 3f), 1e-4f);
            // 21:9: height is the limit.
            Assert.AreEqual(4.5f, CameraFraming.FitOrthographicSize(level, 21f / 9f), 1e-4f);
            Assert.AreEqual(5.5f, CameraFraming.FitOrthographicSize(level, 16f / 9f, padding: 1f), 1e-4f);
        }

        [Test]
        public void FillOrthographicSize_StaysInsideLevel()
        {
            var level = new Vec2(16f, 9f);
            var size = CameraFraming.FillOrthographicSize(level, 4f / 3f);
            var half = CameraFraming.HalfExtents(size, 4f / 3f);

            Assert.AreEqual(4.5f, size, 1e-4f);
            Assert.LessOrEqual(half.X * 2f, level.X + 1e-4f);
            Assert.LessOrEqual(half.Y * 2f, level.Y + 1e-4f);
        }

        [Test]
        public void ClampCenter_KeepsViewInsideLevel()
        {
            var level = new Rect2(-20f, -10f, 40f, 20f);
            var half = new Vec2(8f, 4.5f);

            Assert.AreEqual(new Vec2(12f, 5.5f), CameraFraming.ClampCenter(new Vec2(50f, 50f), half, level));
            Assert.AreEqual(new Vec2(-12f, -5.5f), CameraFraming.ClampCenter(new Vec2(-50f, -50f), half, level));
            Assert.AreEqual(new Vec2(3f, 1f), CameraFraming.ClampCenter(new Vec2(3f, 1f), half, level));
        }

        [Test]
        public void ClampCenter_ViewBiggerThanLevel_CentersOnLevel()
        {
            var level = new Rect2(0f, 0f, 10f, 40f);
            var center = CameraFraming.ClampCenter(new Vec2(100f, 100f), new Vec2(8f, 4.5f), level);
            Assert.AreEqual(5f, center.X, 1e-5f, "view wider than level: centered on X");
            Assert.AreEqual(35.5f, center.Y, 1e-5f, "still clamped on Y");
        }

        [Test]
        public void DeadZone_IgnoresSmallMoves_AndDragsOnBigOnes()
        {
            var zone = new Vec2(1f, 0.5f);
            Assert.AreEqual(Vec2.Zero, CameraFraming.DeadZone(Vec2.Zero, new Vec2(0.9f, -0.4f), zone));
            Assert.AreEqual(new Vec2(2f, -1.5f), CameraFraming.DeadZone(Vec2.Zero, new Vec2(3f, -2f), zone));
        }

        [Test]
        public void Smooth_IsFrameRateIndependent()
        {
            var goal = new Vec2(10f, 0f);
            var oneStep = CameraFraming.Smooth(Vec2.Zero, goal, 5f, 0.1f);
            var tenSteps = Vec2.Zero;
            for (var i = 0; i < 10; i++)
            {
                tenSteps = CameraFraming.Smooth(tenSteps, goal, 5f, 0.01f);
            }

            Assert.AreEqual(oneStep.X, tenSteps.X, 1e-4f);
            Assert.AreEqual(goal, CameraFraming.Smooth(Vec2.Zero, goal, 0f, 0.1f), "sharpness 0 snaps");
        }
    }
}
