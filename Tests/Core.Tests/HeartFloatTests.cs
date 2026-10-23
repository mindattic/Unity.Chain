using MindAttic.Chain.Core;
using NUnit.Framework;

namespace MindAttic.Chain.Tests
{
    /// <summary>Item.vb's heart, figure for figure (per-frame values; the fixed step is 60 fps, D9).</summary>
    public class HeartFloatTests
    {
        [Test]
        public void Starts_AtTheOriginalsPlace()
        {
            var heart = new HeartFloat();

            Assert.AreEqual(0f, heart.X);
            Assert.AreEqual(0.4f, heart.Y);
            Assert.AreEqual(0.002f, heart.FloatSpeed);
            Assert.AreEqual(HeartSway.Left, heart.Sway);
            Assert.IsFalse(heart.PickedUp);
            Assert.AreEqual(10, heart.HealAmount);
        }

        [Test]
        public void Frame_FallsByFallRateNotFloatSpeed()
        {
            var heart = new HeartFloat();
            heart.Frame();

            Assert.AreEqual(0.4f - 0.001f, heart.Y, 1e-7f);
        }

        [Test]
        public void SixtyFrames_FallPointZeroSixUnits()
        {
            var heart = new HeartFloat();

            Assert.AreEqual(60, heart.Advance(1f + 1e-4f));
            Assert.AreEqual(0.4f - 0.06f, heart.Y, 1e-4f);
        }

        [Test]
        public void Advance_RunsOnlyWholeFramesAndKeepsTheRemainder()
        {
            var heart = new HeartFloat();

            Assert.AreEqual(0, heart.Advance(HeartFloat.FrameSeconds * 0.6f));
            Assert.AreEqual(1, heart.Advance(HeartFloat.FrameSeconds * 0.6f));
        }

        [Test]
        public void Frame_WrapsFromBelowMinusHalfToPlusHalf()
        {
            var heart = new HeartFloat();
            var wrapped = false;
            for (var i = 0; i < 1000 && !wrapped; i++)
            {
                heart.Frame();
                wrapped = heart.Y > 0.45f;
            }

            Assert.IsTrue(wrapped);
            Assert.AreEqual(0.5f, heart.Y);
        }

        [Test]
        public void FirstFrame_RunsTheSixSequentialIfsAsTheOriginalDoes()
        {
            // floatSPD 0.002 is already >= floatMaxSPD 0.001, so frame one goes left 0.002, flips right,
            // goes right 0.00199 and flips back left in the same frame.
            var heart = new HeartFloat();
            heart.Frame();

            Assert.AreEqual(-0.00001f, heart.X, 1e-7f);
            Assert.AreEqual(0.00198f, heart.FloatSpeed, 1e-7f);
            Assert.AreEqual(HeartSway.Left, heart.Sway);
            Assert.IsFalse(heart.TurnAround);
        }

        [Test]
        public void Sway_GoesBothWaysOnceTheSpeedIsBelowTheMax()
        {
            var heart = new HeartFloat();
            heart.Reset(); // floatSPD 0.001, the Reset figure
            var sawLeft = false;
            var sawRight = false;
            for (var i = 0; i < 2000; i++)
            {
                var before = heart.X;
                heart.Frame();
                sawLeft |= heart.X < before;
                sawRight |= heart.X > before;
            }

            Assert.IsTrue(sawLeft);
            Assert.IsTrue(sawRight);
        }

        [Test]
        public void Reset_UsesTheResetFloatSpeedAndTheStartPlace()
        {
            var heart = new HeartFloat();
            heart.Advance(2f);
            heart.Reset();

            Assert.AreEqual(0f, heart.X);
            Assert.AreEqual(0.4f, heart.Y);
            Assert.AreEqual(0.001f, heart.FloatSpeed);
            Assert.AreEqual(HeartSway.Left, heart.Sway);
            Assert.IsFalse(heart.PickedUp);
        }

        [Test]
        public void Rect_IsTheOriginalsOffsetsFromXY()
        {
            var heart = new HeartFloat();

            Assert.AreEqual(0f, heart.Rect.Left, 1e-7f);
            Assert.AreEqual(0.045f, heart.Rect.Right, 1e-6f);
            Assert.AreEqual(0.4f + 0.025f + 0.005f, heart.Rect.Top, 1e-6f);
            Assert.AreEqual(0.4f - 0.02f, heart.Rect.Bottom, 1e-6f);
        }

        [Test]
        public void TryCollect_Touching_ConsumesItOnce()
        {
            var heart = new HeartFloat();

            Assert.IsFalse(heart.TryCollect(0f, 0f)); // the angel's start is below the heart
            Assert.IsTrue(heart.TryCollect(0f, 0.27f));
            Assert.IsTrue(heart.PickedUp);
            Assert.IsFalse(heart.TryCollect(0f, 0.27f));
        }

        [Test]
        public void RectIntersect_TestsOnlyTheFirstRectanglesEdges()
        {
            // rectIntersect asks whether A's top or bottom lies inside B's span: an angel rectangle
            // taller than the heart that straddles it entirely (both edges outside) does not count.
            var heart = new HeartFloat();

            Assert.IsFalse(heart.TryCollect(0f, 0.33f));
            Assert.IsTrue(heart.TryCollect(0f, 0.27f));
        }

        [Test]
        public void Reset_RebuildsTheRectSoTheOldSpotCannotCollectAgain()
        {
            var heart = new HeartFloat();
            heart.Advance(5f);
            var spotY = heart.Y - 0.13f;
            Assert.IsTrue(heart.TryCollect(heart.X, spotY));

            heart.Reset();

            Assert.IsFalse(heart.TryCollect(heart.X, spotY));
        }

        [Test]
        public void PickedUpHeart_NeitherMovesNorWrapsUntilReset()
        {
            var heart = new HeartFloat();
            heart.TryCollect(0f, 0.27f);
            var y = heart.Y;
            heart.Advance(5f);

            Assert.AreEqual(y, heart.Y);

            heart.Reset();
            heart.Advance(0.1f);
            Assert.Less(heart.Y, 0.4f);
        }
    }
}
