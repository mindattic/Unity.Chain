using System.Collections.Generic;
using MindAttic.Chain.Core;
using NUnit.Framework;
using UnityEngine;

namespace MindAttic.Chain.Tests
{
    /// <summary>The grapple button as commands; the rule itself is tested in Tests/Core.Tests (GrappleTests, GameWorldTests).</summary>
    public class GrappleControllerTests
    {
        private TestRig _rig;
        private readonly List<GameEvent> _events = new List<GameEvent>();

        [SetUp]
        public void SetUp()
        {
            _rig = new TestRig();
            _rig.Session.EventRaised += _events.Add;
        }

        [TearDown]
        public void TearDown()
        {
            _rig.Dispose();
            _events.Clear();
        }

        [Test]
        public void ButtonHeld_ArmsOnTheNextStep()
        {
            _rig.Grapple.SetButtonHeld(true);
            Assert.AreEqual(GrappleState.Idle, _rig.Grapple.State, "the button is a command; the step applies it");

            _rig.Step();
            Assert.AreEqual(GrappleState.Armed, _rig.Grapple.State);
            Assert.IsTrue(_rig.Grapple.IsArmed);
        }

        [Test]
        public void Release_ThrowsTheCaughtEnemyWithTheMaceVelocity()
        {
            _rig.Grapple.ThrowMultiplier = 1.5f;
            _rig.Grapple.SetButtonHeld(true);
            _rig.Step();
            var enemy = _rig.SpawnEnemy(_rig.Player.MacePosition.ToUnity());
            _rig.Step();
            Assert.AreSame(enemy, _rig.Grapple.HeldEnemy);

            _rig.Swing();
            Assert.AreEqual(_rig.Player.MacePosition, enemy.Position, "carried on the mace");

            _rig.Grapple.SetButtonHeld(false);
            _rig.Step();

            var thrown = _events.FindLast(e => e is Thrown) as Thrown;
            Assert.IsNotNull(thrown);
            Assert.AreEqual(_rig.Id, thrown.Player);
            Assert.AreEqual(enemy.Id, thrown.Enemy);
            Assert.AreEqual(_rig.Player.MaceVelocity * 1.5f, thrown.Velocity);
            Assert.AreEqual(GrappleState.Idle, _rig.Grapple.State);
            Assert.IsNull(_rig.Grapple.HeldEnemy);
        }

        [Test]
        public void QuickTapBetweenSteps_StillArms()
        {
            _rig.Grapple.SetButtonHeld(true);
            _rig.Grapple.SetButtonHeld(false);
            _rig.Step();

            Assert.AreEqual(GrappleState.Armed, _rig.Grapple.State);
            _rig.Step();
            Assert.AreEqual(GrappleState.Idle, _rig.Grapple.State);
        }

        [Test]
        public void CatchTuning_GoesToTheWorld()
        {
            _rig.Grapple.CatchRadius = 0.7f;
            _rig.Grapple.ThrowMultiplier = 2f;

            Assert.AreEqual(0.7f, _rig.Player.Setup.CatchRadius);
            Assert.AreEqual(2f, _rig.Player.Grapple.ThrowMultiplier);
        }

        [Test]
        public void UnseatedGrapple_IsIdle_AndSendsNothing()
        {
            var go = _rig.Track(new GameObject("Loose"));
            go.AddComponent<ChainController>();
            var loose = go.AddComponent<GrappleController>();

            Assert.DoesNotThrow(() => loose.SetButtonHeld(true));
            Assert.AreEqual(GrappleState.Idle, loose.State);
            Assert.IsNull(loose.HeldEnemy);
        }
    }
}
