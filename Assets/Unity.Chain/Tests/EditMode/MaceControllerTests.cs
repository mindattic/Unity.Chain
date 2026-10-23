using MindAttic.Chain;
using NUnit.Framework;
using UnityEngine;

namespace MindAttic.Chain.Tests
{
    public class MaceControllerTests
    {
        private GameObject _root;
        private Transform _socket;
        private ChainController _chain;
        private MaceController _mace;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Chain");
            _socket = new GameObject("HandSocket").transform;
            _socket.SetParent(_root.transform, false);
            _socket.position = new Vector3(1f, 2f, 0f);

            _chain = _root.AddComponent<ChainController>();
            _chain.HandSocket = _socket;
            _chain.Rebuild();

            // Awake does not run for AddComponent in edit mode, so push the mace explicitly.
            _mace = _root.AddComponent<MaceController>();
            _mace.ApplyToChain();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void Default_MaceIsLinkSixAndEndsTheChain()
        {
            Assert.AreEqual(6, _mace.MaceIndex);
            Assert.AreEqual(7, _chain.ActiveLinkCount);

            _chain.RenderChain(1f);
            Assert.AreEqual(7, _root.GetComponent<LineRenderer>().positionCount);
        }

        [Test]
        public void ShortenAndLengthen_MoveTheMaceOneLinkAndResizeTheChain()
        {
            Assert.IsTrue(_mace.Shorten());
            Assert.AreEqual(5, _mace.MaceIndex);
            Assert.AreEqual(6, _chain.ActiveLinkCount);

            Assert.IsTrue(_mace.Lengthen());
            Assert.IsTrue(_mace.Lengthen());
            Assert.AreEqual(7, _mace.MaceIndex);
            Assert.AreEqual(8, _chain.ActiveLinkCount);
        }

        [Test]
        public void Shorten_StopsAtOneLink()
        {
            _mace.MaceIndex = 1;

            Assert.IsFalse(_mace.Shorten());
            Assert.AreEqual(1, _mace.MaceIndex);
            Assert.AreEqual(2, _chain.ActiveLinkCount);
        }

        [Test]
        public void Lengthen_StopsAtTwelveLinks()
        {
            _mace.MaceIndex = 12;

            Assert.IsFalse(_mace.Lengthen());
            Assert.AreEqual(12, _mace.MaceIndex);
        }

        [Test]
        public void MaxIndex_IsCappedByAShortChain()
        {
            _chain.SetLinkCount(5);

            Assert.AreEqual(4, _mace.MaxIndex);
            Assert.AreEqual(4, _mace.MaceIndex);
        }

        [Test]
        public void MaceMass_FollowsTheMaceWhenItMoves()
        {
            _mace.MaceMass = 3f;
            Assert.AreEqual(3f, _chain.Simulation.GetMass(6));
            Assert.AreEqual(1f, _chain.Simulation.GetMass(5));

            _mace.Shorten();
            Assert.AreEqual(3f, _chain.Simulation.GetMass(5));
            Assert.AreEqual(1f, _chain.Simulation.GetMass(6));
        }

        [Test]
        public void ShortenedChain_StillHangsAndSwingsFromTheHand()
        {
            _mace.MaceIndex = 3;
            for (var i = 0; i < 50; i++)
            {
                _chain.Simulate(1f / 50f);
            }

            Assert.Less(_mace.MacePosition.y, _socket.position.y, "the mace should hang below the hand");
            Assert.AreEqual(_mace.MacePosition, _chain.GetLinkPosition(_chain.LinkCount - 1),
                "the hidden tail rides on the mace");
            Assert.AreEqual(_chain.Simulation.GetVelocity(3), _mace.MaceVelocity);
        }

        [Test]
        public void RenderMace_PlacesAndSizesTheVisualOnTheDrawnMaceLink()
        {
            var visual = new GameObject("Mace").transform;
            visual.SetParent(_root.transform, false);
            _mace.MaceVisual = visual;
            _mace.SpriteOffset = new Vector2(0.1f, -0.2f);
            _mace.MaceScale = 3.5f;
            _root.GetComponent<LineRenderer>().widthMultiplier = 0.1f;

            _socket.position = new Vector3(4f, 2f, 0f);
            _chain.Simulate(1f / 50f);
            _chain.RenderChain(1f);
            _mace.RenderMace();

            var expected = _chain.GetRenderedLinkPosition(6) + new Vector3(0.1f, -0.2f, 0f);
            Assert.AreEqual(expected, visual.position);
            Assert.AreEqual(0.35f, visual.localScale.x, 1e-5f);
        }

        [Test]
        public void Grapple_FollowsTheMaceWhenTheChainIsShortened()
        {
            var grapple = _root.AddComponent<GrappleController>();
            grapple.Chain = _chain;
            grapple.MaceIndex = 10;

            Assert.AreEqual(6, grapple.MaceIndex, "the chain's MaceController wins over the grapple's own index");

            _mace.Shorten();
            Assert.AreEqual(5, grapple.MaceIndex);
        }

        [Test]
        public void RenderMace_DoesNotMoveTheSimulatedLink()
        {
            var visual = new GameObject("Mace").transform;
            visual.SetParent(_root.transform, false);
            _mace.MaceVisual = visual;
            _mace.SpriteOffset = new Vector2(0.5f, 0.5f);

            var before = _mace.MacePosition;
            _chain.RenderChain(1f);
            _mace.RenderMace();
            _mace.RenderMace();

            Assert.AreEqual(before, _mace.MacePosition, "the sprite offset is render-only");
        }
    }
}
