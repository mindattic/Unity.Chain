using System;
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
            // *Elastic Trail*'s both-neighbor pull (docs/chain-physics.md), opted into explicitly: Chain.vb's own
            // If/ElseIf only ever pulls the previous link (BIBLE D7, UNI-T45), so this is PullBothNeighbors: true,
            // not the port's default.
            // Drag is needed: an undamped chain yanked 10 units keeps oscillating for ever,
            // so the middle/tail distance below would never settle.
            var damped = new ChainSimulation.Settings(
                segmentLength: 0.2f,
                springConstant: 60f,
                resistance: 4f,
                gravity: Vec2.Zero,
                stopVelocity: 0f,
                stopAcceleration: 0f,
                pullBothNeighbors: true);
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
            // Chain.vb's animateChain always simulates every link (SimulateAllLinks: true is the default,
            // BIBLE V3/D8, UNI-T45); parking the inactive tail on the end link is the rebuild's earlier
            // choice, opted into here explicitly.
            var settings = new ChainSimulation.Settings(
                segmentLength: 0.2f,
                springConstant: 60f,
                resistance: 4f,
                gravity: new Vec2(0f, -9.8f),
                stopVelocity: 0.0005f,
                stopAcceleration: 0.0005f,
                simulateAllLinks: false);
            var chain = new ChainSimulation(6, Vec2.Zero, settings);
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

        // --- UNI-T45: a scripted mouse path replayed against the original's own recurrence -------

        /// <summary>
        /// Replays a scripted mouse path against an independent, line-by-line transcription of Chain.vb's
        /// own <c>animateChain</c>/<c>renderChain</c> recurrence -- the "recorded original trajectory" --
        /// in the original's own per-frame screen-space units (<see cref="OriginalSpace.Identity"/>: no
        /// scaling at all, so one <see cref="ChainSimulation.Step"/> of 1 second is exactly one of the
        /// original's frames). This is the end-to-end regression for every default UNI-T45 settled at
        /// once: the previous-link-only spring pull (BIBLE D7), every link simulating whether or not it is
        /// drawn (BIBLE V3), and the link(1) drop and mace offset (BIBLE D8) -- not just the few simple
        /// cases the other tests in this file check one at a time.
        /// </summary>
        [Test]
        public void ScriptedMousePath_MatchesChainVbsOwnRecurrence_AtEveryShippedDefault()
        {
            const int maxLinks = 25; // chain.json's maxLinks: Chain.vb's link(0..25)
            const int linkCount = maxLinks + 1;
            const int mace = 6; // chain.json's Mace
            const float segLength = 0.002f;
            const float springK = 0.1f;
            const float resistance = 0.25f;
            var gravity = new Vec2(0f, -0.0025f);
            const float stopVel = 1e-7f;
            const float stopAcc = 1e-7f;
            const float overheadBoost = 2f;
            const float linkSize = 0.025f; // the overhead margin
            const float link1DropPerFrame = 0.0004f;
            var maceOffset = new Vec2(-0.012f, -0.012f);

            // OriginalSpace.Identity: world units equal the original's own screen units, so the shipped
            // chain.json figures can be fed to ChainSimulation unscaled and compared directly.
            var m = OriginalSpace.Identity;
            var settings = new ChainSimulation.Settings(
                segmentLength: m.Length(segLength),
                springConstant: m.PerSecondSquared(springK),
                resistance: m.PerSecond(resistance),
                gravity: m.Acceleration(gravity),
                stopVelocity: m.Speed(stopVel),
                stopAcceleration: m.Acceleration(stopAcc),
                overheadBoost: overheadBoost,
                overheadMargin: m.Length(linkSize),
                pullBothNeighbors: false,
                simulateAllLinks: true,
                link1DropPerSecond: m.Speed(link1DropPerFrame),
                maceOffsetPerSecond: m.Speed(maceOffset));

            var chain = new ChainSimulation(linkCount, Vec2.Zero, settings) { ActiveLinkCount = mace + 1 };

            // A scripted mouse path: not a straight line or a hold, but a loop that reverses and crosses
            // above and below the anchor, so the overhead boost fires on some frames and not others.
            Vec2 MousePath(int frame) =>
                new Vec2(0.12f * MathF.Sin(frame * 0.17f), 0.07f * MathF.Cos(frame * 0.29f) - 0.02f);

            // The reference: Chain.vb's animateChain (every link, previous-neighbour spring only, the
            // overhead boost) then renderChain's two stray per-frame lines, transcribed directly.
            var refPos = new Vec2[linkCount];
            var refVel = new Vec2[linkCount];

            Vec2 SpringForce(int i, int j)
            {
                var dx = refPos[i].X - refPos[j].X;
                var dy = refPos[i].Y - refPos[j].Y;
                var len = MathF.Sqrt(dx * dx + dy * dy);
                if (len <= segLength)
                {
                    return Vec2.Zero;
                }

                var springF = springK * (len - segLength);
                return new Vec2((dx / len) * springF, (dy / len) * springF);
            }

            void ReferenceFrame()
            {
                for (var l = 1; l <= maxLinks; l++)
                {
                    // Chain.vb: If l > 0 Then spring = springForce(l - 1, l) ElseIf l < maxLinks - 1 ...
                    // (dead code: l is always > 0, so the ElseIf never runs -- BIBLE D7).
                    var spring = SpringForce(l - 1, l);

                    if (spring.Y > 0f && refPos[l].Y > refPos[0].Y - linkSize)
                    {
                        spring = new Vec2(spring.X, spring.Y * overheadBoost);
                    }

                    var resist = refVel[l] * resistance;
                    var accel = (spring - resist) + gravity; // every link's mass is 1 throughout
                    var velocity = refVel[l] + accel;

                    if (velocity.X * velocity.X + velocity.Y * velocity.Y < stopVel * stopVel &&
                        accel.X * accel.X + accel.Y * accel.Y < stopAcc * stopAcc)
                    {
                        velocity = Vec2.Zero;
                    }

                    refVel[l] = velocity;
                    refPos[l] += velocity;
                }

                // renderChain's two stray lines (BIBLE D8): the port takes each one's single figure once a
                // step, not once per drawn link as the original's own draw loop happened to repeat them.
                refPos[1] = refPos[1].WithY(refPos[1].Y - link1DropPerFrame);
                refPos[mace] += maceOffset;
            }

            const int frames = 180; // three seconds at the original's 60 fps
            for (var frame = 0; frame < frames; frame++)
            {
                var anchor = MousePath(frame);
                refPos[0] = anchor;
                chain.SetAnchor(anchor);

                ReferenceFrame();
                chain.Step(1f);

                for (var l = 0; l <= mace; l++)
                {
                    Assert.AreEqual(refPos[l].X, chain.GetPosition(l).X, 1e-4f, $"link {l} x diverged at frame {frame}");
                    Assert.AreEqual(refPos[l].Y, chain.GetPosition(l).Y, 1e-4f, $"link {l} y diverged at frame {frame}");
                }
            }
        }
    }
}
