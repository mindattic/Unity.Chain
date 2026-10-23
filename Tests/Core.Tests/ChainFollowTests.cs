using MindAttic.Chain.Core;
using NUnit.Framework;


namespace MindAttic.Chain.Tests
{
    /// <summary>Form1.vb's F key (Chain.vb's <c>followMouse</c>), UNI-T47.</summary>
    public class ChainFollowTests
    {
        private static ChainSimulation.Settings NoGravityNoDrag() => new ChainSimulation.Settings(
            segmentLength: 0.2f,
            springConstant: 60f,
            resistance: 0f,
            gravity: Vec2.Zero,
            stopVelocity: 0f,
            stopAcceleration: 0f);

        [Test]
        public void Chain_StartsFollowing()
        {
            var chain = new ChainSimulation(5, Vec2.Zero, ChainSimulation.Settings.Default);
            Assert.IsTrue(chain.IsFollowing);
        }

        [Test]
        public void Toggle_FlipsTheFlag_AndReportsThePlayerAndNewState()
        {
            var chain = new ChainSimulation(5, Vec2.Zero, ChainSimulation.Settings.Default);

            var off = ChainFollow.Apply(chain, new ToggleFollowCommand(3));
            Assert.IsFalse(chain.IsFollowing);
            Assert.AreEqual(3, off.PlayerId);
            Assert.IsFalse(off.IsFollowing);

            var on = ChainFollow.Apply(chain, new ToggleFollowCommand(3));
            Assert.IsTrue(chain.IsFollowing);
            Assert.IsTrue(on.IsFollowing);
        }

        [Test]
        public void NotFollowing_NoLinkFeelsItsSpring()
        {
            // Without gravity or drag, a stretched chain that is not following must not move at all.
            var chain = new ChainSimulation(4, Vec2.Zero, NoGravityNoDrag());
            chain.SetAnchor(new Vec2(5f, 0f));
            chain.IsFollowing = false;

            for (var i = 0; i < 30; i++)
            {
                chain.Step(1f / 60f);
            }

            for (var i = 1; i < chain.LinkCount; i++)
            {
                Assert.AreEqual(Vec2.Zero, chain.GetPosition(i), $"link {i}");
                Assert.AreEqual(Vec2.Zero, chain.GetVelocity(i), $"link {i}");
            }
        }

        [Test]
        public void NotFollowing_TheChainFallsUnderGravity_FollowingItHangsFromTheHand()
        {
            var settings = ChainSimulation.Settings.Default;
            var slack = new ChainSimulation(7, Vec2.Zero, settings) { IsFollowing = false };
            var held = new ChainSimulation(7, Vec2.Zero, settings);

            for (var i = 0; i < 300; i++)
            {
                slack.SetAnchor(Vec2.Zero);
                held.SetAnchor(Vec2.Zero);
                slack.Step(1f / 60f);
                held.Step(1f / 60f);
            }

            // Held, the springs hang the mace about 4.6 below the hand; five seconds of free fall
            // against drag 4 (terminal speed 9.8 / 4) take it well past that.
            Assert.Less(slack.GetPosition(6).Y, -8f);
            Assert.Greater(held.GetPosition(6).Y, -5f);
            Assert.Greater(slack.GetVelocity(6).Magnitude, 1f);
        }

        [Test]
        public void TurningFollowBackOn_RestoresTheSpring()
        {
            var chain = new ChainSimulation(7, Vec2.Zero, ChainSimulation.Settings.Default) { IsFollowing = false };
            for (var i = 0; i < 300; i++)
            {
                chain.Step(1f / 60f);
            }

            var fallen = chain.GetPosition(6).Y;
            ChainFollow.Apply(chain, new ToggleFollowCommand(0));
            for (var i = 0; i < 600; i++)
            {
                chain.SetAnchor(Vec2.Zero);
                chain.Step(1f / 60f);
            }

            Assert.Greater(chain.GetPosition(6).Y, fallen);
            Assert.Greater(chain.GetPosition(6).Y, -5f);
        }

        [Test]
        public void Mace_IsThreeAndAHalfLinksWhileFollowing_OneLinkAndNoHitsOtherwise()
        {
            Assert.AreEqual(3.5f, ChainFollow.MaceScaleFor(true));
            Assert.AreEqual(1f, ChainFollow.MaceScaleFor(false));
            Assert.AreEqual(2f, ChainFollow.MaceScaleFor(true, 2f));
            Assert.AreEqual(1f, ChainFollow.MaceScaleFor(false, 2f));
            Assert.IsTrue(ChainFollow.MaceHits(true));
            Assert.IsFalse(ChainFollow.MaceHits(false));
        }
    }
}
