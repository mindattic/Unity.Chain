using System.Collections.Generic;
using MindAttic.Chain.Core;
using NUnit.Framework;
using UnityEngine;

namespace MindAttic.Chain.Tests
{
    /// <summary>The chain-length keys as commands, the mace's tuning, and its visual.</summary>
    public class MaceControllerTests
    {
        private TestRig _rig;
        private MaceController _mace;
        private readonly List<GameEvent> _events = new List<GameEvent>();

        [SetUp]
        public void SetUp()
        {
            _rig = new TestRig();
            _mace = _rig.Mace;
            _rig.Session.EventRaised += _events.Add;
        }

        [TearDown]
        public void TearDown()
        {
            _rig.Dispose();
            _events.Clear();
        }

        [Test]
        public void Default_MaceIsLinkSixAndEndsTheChain()
        {
            Assert.AreEqual(6, _mace.MaceIndex);
            Assert.AreEqual(7, _rig.Chain.ActiveLinkCount);
            _rig.Chain.RenderChain(1f);
            Assert.AreEqual(7, _rig.Chain.GetComponent<LineRenderer>().positionCount);
        }

        [Test]
        public void ShortenAndLengthen_SendCommands_ThatMoveTheMaceAndResizeTheChain()
        {
            Assert.IsTrue(_mace.Shorten());
            Assert.AreEqual(5, _mace.MaceIndex);
            Assert.AreEqual(6, _rig.Chain.ActiveLinkCount);
            var changed = _events.Find(e => e is ChainLengthChanged) as ChainLengthChanged;
            Assert.IsNotNull(changed);
            Assert.AreEqual(_rig.Id, changed.Player);

            Assert.IsTrue(_mace.Lengthen());
            Assert.IsTrue(_mace.Lengthen());
            Assert.AreEqual(7, _mace.MaceIndex);
            Assert.AreEqual(8, _rig.Chain.ActiveLinkCount);
        }

        [Test]
        public void ChainLength_StopsAtOneAndTwelveLinks()
        {
            while (_mace.Shorten()) { }
            Assert.AreEqual(1, _mace.MaceIndex);
            Assert.AreEqual(2, _rig.Chain.ActiveLinkCount);

            while (_mace.Lengthen()) { }
            Assert.AreEqual(12, _mace.MaceIndex);
        }

        [Test]
        public void MaxIndex_IsCappedByAShortChain()
        {
            _rig.Chain.SetLinkCount(5);

            Assert.AreEqual(4, _mace.MaxIndex);
            Assert.AreEqual(4, _mace.MaceIndex);
        }

        [Test]
        public void MaceTuning_GoesToTheWorld()
        {
            _mace.MaceMass = 3f;
            _mace.HitRadius = 0.4f;
            _mace.DamagePerSpeed = 0.2f;

            Assert.AreEqual(3f, _rig.Player.Chain.GetMass(6));
            Assert.AreEqual(1f, _rig.Player.Chain.GetMass(5));
            Assert.AreEqual(0.4f, _rig.Player.Setup.HitRadius);
            Assert.AreEqual(0.2f, _rig.Player.Setup.DamagePerSpeed);

            _mace.Shorten();
            Assert.AreEqual(3f, _rig.Player.Chain.GetMass(5));
        }

        [Test]
        public void RenderMace_PlacesAndSizesTheVisualOnTheDrawnMaceLink_WithoutMovingIt()
        {
            var visual = _rig.Track(new GameObject("Mace")).transform;
            _mace.MaceVisual = visual;
            _mace.SpriteOffset = new Vector2(0.1f, -0.2f);
            _mace.MaceScale = 3.5f;
            _rig.Chain.GetComponent<LineRenderer>().widthMultiplier = 0.1f;
            _rig.MoveAngelTo(new Vector2(4f, 2f));
            _rig.Step();
            _rig.Chain.RenderChain(1f);
            var before = _mace.MacePosition;

            _mace.RenderMace();
            _mace.RenderMace();

            var expected = _rig.Chain.GetRenderedLinkPosition(6) + new Vector3(0.1f, -0.2f, 0f);
            Assert.AreEqual(expected, visual.position);
            Assert.AreEqual(0.35f, visual.localScale.x, 1e-5f);
            Assert.AreEqual(before, _mace.MacePosition, "the sprite offset is render-only");
        }

        [Test]
        public void NotFollowing_TheMaceIsDrawnAsALink_WithoutItsOffset_UntilFollowIsBackOn()
        {
            var visual = _rig.Track(new GameObject("Mace")).transform;
            _mace.MaceVisual = visual;
            _mace.SpriteOffset = new Vector2(0.1f, -0.2f);
            _rig.Chain.GetComponent<LineRenderer>().widthMultiplier = 0.1f;

            Assert.IsFalse(_rig.Chain.ToggleFollow());
            Assert.IsFalse(_rig.Player.IsFollowing);
            var changed = _events.Find(e => e is FollowChanged) as FollowChanged;
            Assert.IsNotNull(changed);
            Assert.AreEqual(_rig.Id, changed.Player);
            Assert.IsFalse(changed.IsFollowing);

            _rig.Step();
            _rig.Chain.RenderChain(1f);
            _mace.RenderMace();
            Assert.AreEqual(_rig.Chain.GetRenderedLinkPosition(6), visual.position);
            Assert.AreEqual(0.1f, visual.localScale.x, 1e-5f);

            Assert.IsTrue(_rig.Chain.ToggleFollow());
            _mace.RenderMace();
            Assert.AreEqual(0.35f, visual.localScale.x, 1e-5f);
        }

        [Test]
        public void IsFollowing_SurvivesAChainRebuildAndAReset()
        {
            _rig.Chain.ToggleFollow();
            _rig.Chain.SetLinkCount(10);
            Assert.IsFalse(_rig.Chain.Simulation.IsFollowing);

            _rig.Session.Restart();
            Assert.IsFalse(_rig.Chain.Simulation.IsFollowing, "Root.vb's Reset leaves followMouse alone");
        }

        [Test]
        public void TheVisual_WearsThePlayersMaceSkin_SpikeBallThenMagnet()
        {
            var ball = MakeSprite("ball");
            var magnet = MakeSprite("magnet");
            var visual = _rig.Track(new GameObject("Mace"));
            var renderer = visual.AddComponent<SpriteRenderer>();
            _mace.MaceVisual = visual.transform;
            _mace.Skins = new[] { new MaceController.SkinArt(MaceMode.SpikeBall, ball), new MaceController.SkinArt(MaceMode.Magnet, magnet) };
            _rig.Chain.GetComponent<LineRenderer>().widthMultiplier = 0.1f;
            _mace.MaceScale = 3.5f;
            _rig.Step();
            _rig.Chain.RenderChain(1f);

            _mace.RenderMace();
            Assert.AreEqual(MaceMode.SpikeBall, _mace.Mode, "chain.json's maceSkins.mode");
            Assert.AreSame(ball, renderer.sprite);

            Assert.IsTrue(_mace.ToggleMode());
            Assert.AreEqual(MaceMode.Magnet, _rig.Player.MaceMode);
            Assert.IsTrue(_events.Exists(e => e is MaceModeChanged changed && changed.Player == _rig.Id && changed.Mode == MaceMode.Magnet));
            _mace.RenderMace();
            Assert.AreSame(magnet, renderer.sprite);
            Assert.AreEqual(0.35f, visual.transform.localScale.x, 1e-5f, "the magnet is drawn at the mace's 3.5x, as the ball is");
            Assert.AreEqual(_rig.Chain.GetRenderedLinkPosition(6), visual.transform.position);

            Assert.IsTrue(_mace.ToggleMode());
            _mace.RenderMace();
            Assert.AreSame(ball, renderer.sprite);
        }

        private Sprite MakeSprite(string name)
        {
            var texture = _rig.Track(new Texture2D(32, 32));
            var sprite = _rig.Track(Sprite.Create(texture, new Rect(0f, 0f, 32f, 32f), new Vector2(0.5f, 0.5f), 32f));
            sprite.name = name;
            return sprite;
        }

        [Test]
        public void UnseatedMace_SendsNothing()
        {
            var go = _rig.Track(new GameObject("Loose"));
            go.AddComponent<ChainController>();
            var loose = go.AddComponent<MaceController>();

            Assert.IsFalse(loose.Shorten());
            Assert.IsFalse(loose.ToggleMode());
            Assert.AreEqual(6, loose.MaceIndex);
        }
    }
}
