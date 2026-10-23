using MindAttic.Chain.Core;
using NUnit.Framework;
using UnityEngine;

namespace MindAttic.Chain.Tests
{
    /// <summary>The chain view: it draws the world's chain for its player and hands its tuning to the world. The spring rule is tested in Tests/Core.Tests.</summary>
    public class ChainControllerTests
    {
        private TestRig _rig;
        private ChainController _chain;
        private LineRenderer _line;

        [SetUp]
        public void SetUp()
        {
            _rig = new TestRig(mace: false, grapple: false);
            _chain = _rig.Chain;
            _line = _chain.GetComponent<LineRenderer>();
        }

        [TearDown]
        public void TearDown() => _rig.Dispose();

        [Test]
        public void Seating_BindsTheChainToItsPlayersWorldChain()
        {
            Assert.AreSame(_rig.Session, _chain.Session);
            Assert.AreEqual(_rig.Id, _chain.Player);
            Assert.AreSame(_rig.Player.Chain, _chain.Simulation);
            for (var i = 0; i < _chain.LinkCount; i++)
            {
                Assert.AreEqual(_rig.Angel.HandSocket, _chain.GetLinkPosition(i), "every link starts on the hand");
            }
        }

        [Test]
        public void LinkZero_FollowsTheAngelsHand()
        {
            _rig.MoveAngelTo(new Vector2(4f, -3f));
            _rig.Step();

            Assert.AreEqual(_rig.Angel.HandSocket, _chain.GetLinkPosition(0));
        }

        [Test]
        public void RenderChain_DrawsTheLiveLinks()
        {
            _rig.MoveAngelTo(new Vector2(3f, 0f));
            _rig.Step();
            _chain.RenderChain(1f);

            Assert.AreEqual(_chain.ActiveLinkCount, _line.positionCount);
            Assert.AreEqual(7, _line.positionCount, "the default mace is link 6");
            Assert.IsTrue(_line.useWorldSpace);
            for (var i = 0; i < _line.positionCount; i++)
            {
                Assert.AreEqual((Vector3)_chain.GetLinkPosition(i), _line.GetPosition(i));
            }
        }

        [Test]
        public void RenderChain_InterpolatesBetweenPhysicsSteps()
        {
            var before = _chain.GetLinkPosition(0);
            _rig.MoveAngelTo(new Vector2(4f, 0f));
            _rig.Step();
            _chain.RenderChain(0.5f);

            var expected = Vector2.Lerp(before, _chain.GetLinkPosition(0), 0.5f);
            Assert.AreEqual((Vector3)expected, _line.GetPosition(0));
        }

        [Test]
        public void SetLinkCount_RebuildsTheWorldsChain()
        {
            _chain.SetLinkCount(8);
            _chain.RenderChain(1f);

            Assert.AreEqual(8, _chain.Simulation.LinkCount);
            Assert.AreEqual(8, _rig.Player.Chain.LinkCount);
        }

        [Test]
        public void Tuning_GoesToTheWorld()
        {
            _chain.OverheadBoost = 3f;
            _chain.OverheadMargin = 0.4f;
            _chain.WallBounce = 0.25f;

            Assert.AreEqual(3f, _chain.CurrentSettings.OverheadBoost);
            Assert.AreEqual(3f, _chain.Simulation.Config.OverheadBoost);
            Assert.AreEqual(0.4f, _chain.Simulation.Config.OverheadMargin);
            Assert.AreEqual(0.25f, _chain.Simulation.WallBounce);
        }

        [Test]
        public void UnseatedChain_DrawsNothing()
        {
            var loose = _rig.Track(new GameObject("Loose")).AddComponent<ChainController>();

            Assert.IsNull(loose.Simulation);
            Assert.DoesNotThrow(() => loose.RenderChain(1f));
            Assert.AreEqual(25, loose.ActiveLinkCount);
        }
    }
}
