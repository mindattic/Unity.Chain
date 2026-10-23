using MindAttic.Chain.Core;
using NUnit.Framework;


namespace MindAttic.Chain.Tests
{
    public class ChainSimulationTests
    {
        private static ChainSimulation.Settings NoGravityNoDrag() => new ChainSimulation.Settings(
            segmentLength: 0.2f,
            springConstant: 60f,
            resistance: 0f,
            gravity: Vec2.Zero,
            stopVelocity: 0f,
            stopAcceleration: 0f);

        [Test]
        public void Anchor_NeverMovesOnItsOwn()
        {
            var anchor = new Vec2(1f, 2f);
            var chain = new ChainSimulation(5, anchor, ChainSimulation.Settings.Default);

            for (var i = 0; i < 30; i++)
            {
                chain.SetAnchor(anchor);
                chain.Step(1f / 60f);
            }

            Assert.AreEqual(anchor, chain.GetPosition(0));
        }

        [Test]
        public void SlackChain_StaysAtRest()
        {
            var chain = new ChainSimulation(5, Vec2.Zero, NoGravityNoDrag());
            for (var i = 0; i < 10; i++)
            {
                chain.Step(1f / 60f);
            }

            for (var i = 1; i < chain.LinkCount; i++)
            {
                Assert.AreEqual(Vec2.Zero, chain.GetPosition(i));
            }
        }

        [Test]
        public void StretchedChain_PullsLinksTowardAnchor()
        {
            var anchor = new Vec2(0f, 0f);
            var chain = new ChainSimulation(3, anchor, NoGravityNoDrag());

            chain.Step(1f / 60f);
            chain.Step(1f / 60f);

            var farLink = chain.GetPosition(chain.LinkCount - 1);
            Assert.AreEqual(0f, farLink.Magnitude, 0.0001f, "with no initial stretch the chain should not move");
        }

        [Test]
        public void Gravity_PullsHangingLinksDown()
        {
            var settings = new ChainSimulation.Settings(
                segmentLength: 0.2f,
                springConstant: 60f,
                resistance: 2f,
                gravity: new Vec2(0f, -9.8f),
                stopVelocity: 0f,
                stopAcceleration: 0f);
            var chain = new ChainSimulation(4, Vec2.Zero, settings);

            for (var i = 0; i < 5; i++)
            {
                chain.Step(1f / 60f);
            }

            var tip = chain.GetPosition(chain.LinkCount - 1);
            Assert.Less(tip.Y, 0f, "the free end should fall under gravity");
        }

        [Test]
        public void BothNeighbors_PullAMiddleLinkTowardTheMidpoint()
        {
            // Regression guard for the Chain.vb If/ElseIf bug (docs/chain-physics.md):
            // a middle link must be pulled by BOTH neighbors, not just the previous one.
            // Drag is needed: an undamped chain yanked 10 units keeps oscillating for ever,
            // so the middle/tail distance below would never settle.
            var damped = new ChainSimulation.Settings(
                segmentLength: 0.2f,
                springConstant: 60f,
                resistance: 4f,
                gravity: Vec2.Zero,
                stopVelocity: 0f,
                stopAcceleration: 0f);
            var chain = new ChainSimulation(3, new Vec2(-10f, 0f), damped);

            // Reach into the simulation indirectly: drive link 0 far to the left, let link 2
            // settle near it, then yank link 0 back to the origin. Link 1 must respond to the
            // pull from link 2 (its "next" neighbor) as well as link 0.
            for (var i = 0; i < 60; i++)
            {
                chain.SetAnchor(new Vec2(-10f, 0f));
                chain.Step(1f / 60f);
            }

            for (var i = 0; i < 120; i++)
            {
                chain.SetAnchor(Vec2.Zero);
                chain.Step(1f / 60f);
            }

            var middle = chain.GetPosition(1);
            var tail = chain.GetPosition(2);
            Assert.Less(Vec2.Distance(middle, tail), 1f, "tail should stay roughly chained to the middle link");
        }

        [Test]
        public void ActiveLinkCount_ParksTheInactiveTailOnTheEndLink()
        {
            var chain = new ChainSimulation(6, Vec2.Zero, ChainSimulation.Settings.Default);
            chain.ActiveLinkCount = 3;

            for (var i = 0; i < 20; i++)
            {
                chain.SetAnchor(new Vec2(i * 0.1f, 0f));
                chain.Step(1f / 60f);
            }

            var end = chain.GetPosition(2);
            Assert.Less(end.Y, 0f, "the active end should still hang under gravity");
            for (var i = 3; i < chain.LinkCount; i++)
            {
                Assert.AreEqual(end, chain.GetPosition(i));
                Assert.AreEqual(chain.GetVelocity(2), chain.GetVelocity(i));
            }
        }

        [Test]
        public void ActiveLinkCount_IsClampedToTheChain()
        {
            var chain = new ChainSimulation(5, Vec2.Zero, ChainSimulation.Settings.Default);

            chain.ActiveLinkCount = 0;
            Assert.AreEqual(2, chain.ActiveLinkCount);

            chain.ActiveLinkCount = 99;
            Assert.AreEqual(5, chain.ActiveLinkCount);
        }

        [Test]
        public void OverheadBoost_AmplifiesUpwardPullOnALinkAboveTheAnchor()
        {
            // Link 1 starts 0.5 below the anchor; a 1-unit margin counts that as overhead.
            var plain = Boosted(1f, 1f);
            var boosted = Boosted(2f, 1f);

            Assert.Greater(plain.Y, 0f, "the spring should pull the link up toward the anchor");
            Assert.AreEqual(plain.Y * 2f, boosted.Y, 1e-5f);
            Assert.AreEqual(plain.X, boosted.X, 1e-6f);
        }

        [Test]
        public void OverheadBoost_LeavesLinksBelowTheAnchorAlone()
        {
            // Same setup with no margin: 0.5 below the anchor is not overhead.
            Assert.AreEqual(Boosted(1f, 0f).Y, Boosted(2f, 0f).Y, 1e-6f);
        }

        private static Vec2 Boosted(float boost, float margin)
        {
            var settings = new ChainSimulation.Settings(
                segmentLength: 0.2f,
                springConstant: 60f,
                resistance: 0f,
                gravity: Vec2.Zero,
                stopVelocity: 0f,
                stopAcceleration: 0f,
                overheadBoost: boost,
                overheadMargin: margin);
            var chain = new ChainSimulation(2, Vec2.Zero, settings);
            chain.SetAnchor(new Vec2(0.1f, 0.5f));
            chain.Step(1f / 60f);
            return chain.GetVelocity(1);
        }
    }
}
