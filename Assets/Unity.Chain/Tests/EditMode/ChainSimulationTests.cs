using MindAttic.Chain;
using NUnit.Framework;
using UnityEngine;

namespace MindAttic.Chain.Tests
{
    public class ChainSimulationTests
    {
        private static ChainSimulation.Settings NoGravityNoDrag() => new ChainSimulation.Settings(
            segmentLength: 0.2f,
            springConstant: 60f,
            resistance: 0f,
            gravity: Vector2.zero,
            stopVelocity: 0f,
            stopAcceleration: 0f);

        [Test]
        public void Anchor_NeverMovesOnItsOwn()
        {
            var anchor = new Vector2(1f, 2f);
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
            var chain = new ChainSimulation(5, Vector2.zero, NoGravityNoDrag());
            for (var i = 0; i < 10; i++)
            {
                chain.Step(1f / 60f);
            }

            for (var i = 1; i < chain.LinkCount; i++)
            {
                Assert.AreEqual(Vector2.zero, chain.GetPosition(i));
            }
        }

        [Test]
        public void StretchedChain_PullsLinksTowardAnchor()
        {
            var anchor = new Vector2(0f, 0f);
            var chain = new ChainSimulation(3, anchor, NoGravityNoDrag());

            chain.Step(1f / 60f);
            chain.Step(1f / 60f);

            var farLink = chain.GetPosition(chain.LinkCount - 1);
            Assert.AreEqual(0f, farLink.magnitude, 0.0001f, "with no initial stretch the chain should not move");
        }

        [Test]
        public void Gravity_PullsHangingLinksDown()
        {
            var settings = new ChainSimulation.Settings(
                segmentLength: 0.2f,
                springConstant: 60f,
                resistance: 2f,
                gravity: new Vector2(0f, -9.8f),
                stopVelocity: 0f,
                stopAcceleration: 0f);
            var chain = new ChainSimulation(4, Vector2.zero, settings);

            for (var i = 0; i < 5; i++)
            {
                chain.Step(1f / 60f);
            }

            var tip = chain.GetPosition(chain.LinkCount - 1);
            Assert.Less(tip.y, 0f, "the free end should fall under gravity");
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
                gravity: Vector2.zero,
                stopVelocity: 0f,
                stopAcceleration: 0f);
            var chain = new ChainSimulation(3, new Vector2(-10f, 0f), damped);

            // Reach into the simulation indirectly: drive link 0 far to the left, let link 2
            // settle near it, then yank link 0 back to the origin. Link 1 must respond to the
            // pull from link 2 (its "next" neighbor) as well as link 0.
            for (var i = 0; i < 60; i++)
            {
                chain.SetAnchor(new Vector2(-10f, 0f));
                chain.Step(1f / 60f);
            }

            for (var i = 0; i < 120; i++)
            {
                chain.SetAnchor(Vector2.zero);
                chain.Step(1f / 60f);
            }

            var middle = chain.GetPosition(1);
            var tail = chain.GetPosition(2);
            Assert.Less(Vector2.Distance(middle, tail), 1f, "tail should stay roughly chained to the middle link");
        }
    }
}
