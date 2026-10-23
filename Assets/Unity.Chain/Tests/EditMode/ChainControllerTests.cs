using MindAttic.Chain;
using NUnit.Framework;
using UnityEngine;

namespace MindAttic.Chain.Tests
{
    public class ChainControllerTests
    {
        private GameObject _root;
        private Transform _socket;
        private ChainController _controller;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Chain");
            _socket = new GameObject("HandSocket").transform;
            _socket.SetParent(_root.transform, false);
            _socket.position = new Vector3(1f, 2f, 0f);

            _controller = _root.AddComponent<ChainController>();
            _controller.HandSocket = _socket;
            _controller.Rebuild();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void Rebuild_CollapsesEveryLinkOntoTheHandSocket()
        {
            for (var i = 0; i < _controller.LinkCount; i++)
            {
                Assert.AreEqual((Vector2)_socket.position, _controller.GetLinkPosition(i));
            }
        }

        [Test]
        public void Simulate_PinsLinkZeroToTheHandSocketAfterItMoves()
        {
            _socket.position = new Vector3(4f, -3f, 0f);
            _controller.Simulate(1f / 50f);

            Assert.AreEqual(new Vector2(4f, -3f), _controller.GetLinkPosition(0));
        }

        [Test]
        public void Simulate_DraggedAnchorPullsTheChainAfterIt()
        {
            var start = _controller.GetLinkPosition(1);
            _socket.position = new Vector3(6f, 2f, 0f);

            for (var i = 0; i < 20; i++)
            {
                _controller.Simulate(1f / 50f);
            }

            Assert.Greater(_controller.GetLinkPosition(1).x, start.x);
        }

        [Test]
        public void Simulate_WithoutASocket_AnchorsToOwnTransform()
        {
            _controller.HandSocket = null;
            _root.transform.position = new Vector3(-2f, 5f, 0f);
            _controller.Simulate(1f / 50f);

            Assert.AreEqual(new Vector2(-2f, 5f), _controller.GetLinkPosition(0));
        }

        [Test]
        public void RenderChain_WritesOneLinePointPerLink()
        {
            _socket.position = new Vector3(3f, 0f, 0f);
            _controller.Simulate(1f / 50f);
            _controller.RenderChain(1f);

            var line = _root.GetComponent<LineRenderer>();
            Assert.AreEqual(_controller.LinkCount, line.positionCount);
            Assert.IsTrue(line.useWorldSpace);
            for (var i = 0; i < _controller.LinkCount; i++)
            {
                Assert.AreEqual((Vector3)_controller.GetLinkPosition(i), line.GetPosition(i));
            }
        }

        [Test]
        public void RenderChain_InterpolatesBetweenPhysicsSteps()
        {
            _socket.position = new Vector3(5f, 2f, 0f);
            _controller.Simulate(1f / 50f);
            _controller.RenderChain(0.5f);

            var line = _root.GetComponent<LineRenderer>();
            var expected = Vector3.Lerp(new Vector3(1f, 2f, 0f), new Vector3(5f, 2f, 0f), 0.5f);
            Assert.AreEqual(expected, line.GetPosition(0));
        }

        [Test]
        public void SetLinkCount_RebuildsTheChainAndTheLine()
        {
            _controller.SetLinkCount(8);

            Assert.AreEqual(8, _controller.Simulation.LinkCount);
            Assert.AreEqual(8, _root.GetComponent<LineRenderer>().positionCount);
        }
    }
}
