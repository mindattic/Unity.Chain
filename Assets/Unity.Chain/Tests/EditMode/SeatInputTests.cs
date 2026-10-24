using System.Collections.Generic;
using System.Linq;
using MindAttic.Chain.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MindAttic.Chain.Tests
{
    /// <summary>
    /// UNI-T34: the game's one InputActionAsset, built in code, and a seat turning a frame of it into that seat's
    /// commands. The stick and dead-zone rule itself is tested in Tests/Core.Tests (SteerInputTests).
    /// </summary>
    public class SeatInputTests
    {
        private TestRig _rig;
        private SeatInput _input;
        private readonly List<GameEvent> _events = new List<GameEvent>();

        [SetUp]
        public void SetUp()
        {
            _rig = new TestRig();
            _input = _rig.Angel.GetComponent<SeatInput>();
            _rig.Session.EventRaised += _events.Add;
        }

        [TearDown]
        public void TearDown()
        {
            _rig.Dispose();
            _events.Clear();
        }

        private static string[] Paths(InputAction action) => action.bindings.Select(b => b.path).ToArray();

        // --- The asset ---------------------------------------------------------------------------

        [Test]
        public void Asset_HasThePlayerAndDebugMaps()
        {
            using (var controls = new ChainControls())
            {
                Assert.IsNotNull(controls.Asset.FindActionMap(ChainControls.PlayerMapName));
                Assert.IsNotNull(controls.Asset.FindActionMap(ChainControls.DebugMapName));
                CollectionAssert.AreEquivalent(
                    new[] { "Steer", "SteerStick", "Grapple", "ChainShorter", "ChainLonger", "ToggleFollow", "Pause", "Reset", "ToggleJoystick" },
                    controls.Player.actions.Select(a => a.name).ToArray());
                CollectionAssert.AreEquivalent(new[] { "ToggleLighting" }, controls.Debug.actions.Select(a => a.name).ToArray());
            }
        }

        [Test]
        public void OriginalKeys_KeepTheirBindings()
        {
            using (var c = new ChainControls())
            {
                CollectionAssert.Contains(Paths(c.Steer), "<Mouse>/delta");
                CollectionAssert.Contains(Paths(c.Grapple), "<Mouse>/leftButton");
                CollectionAssert.Contains(Paths(c.ChainShorter), "<Keyboard>/leftBracket", "Input.vb keyChainMinus");
                CollectionAssert.Contains(Paths(c.ChainLonger), "<Keyboard>/rightBracket", "Input.vb keyChainPlus");
                CollectionAssert.Contains(Paths(c.ToggleFollow), "<Keyboard>/f");
                CollectionAssert.Contains(Paths(c.Pause), "<Keyboard>/pause");
                CollectionAssert.Contains(Paths(c.Reset), "<Keyboard>/r");
                CollectionAssert.Contains(Paths(c.ToggleJoystick), "<Keyboard>/j");
                CollectionAssert.Contains(Paths(c.ToggleLighting), "<Keyboard>/l");
            }
        }

        [Test]
        public void MinusAndEquals_StayFreeForGameSpeed()
        {
            using (var c = new ChainControls())
            {
                var all = c.Asset.SelectMany(a => a.bindings).Select(b => b.path).ToArray();
                CollectionAssert.DoesNotContain(all, "<Keyboard>/minus", "Input.vb: gameSpeed (UNI-T52)");
                CollectionAssert.DoesNotContain(all, "<Keyboard>/equals");
            }
        }

        [Test]
        public void Gamepad_DrivesThePlayerActions()
        {
            using (var c = new ChainControls())
            {
                CollectionAssert.Contains(Paths(c.SteerStick), "<Gamepad>/leftStick");
                foreach (var action in new[] { c.Grapple, c.ChainShorter, c.ChainLonger, c.ToggleFollow, c.Pause, c.Reset })
                {
                    Assert.That(Paths(action), Has.Some.StartsWith("<Gamepad>/"), action.name);
                }
            }
        }

        // --- A frame as commands ------------------------------------------------------------------

        [Test]
        public void AngelRequiresASeatInput()
        {
            Assert.IsNotNull(_input);
        }

        [Test]
        public void Stick_SteersLikeTheMouse()
        {
            var start = _rig.Player.Flight.Position;

            _input.Dispatch(new SeatInput.Frame { Stick = new Vector2(1f, 0f) }, 0.5f);

            var moved = _rig.Player.Flight.Position - start;
            var counts = _rig.Session.Settings.Steering.StickSpeed * 0.5f;
            Assert.AreEqual(counts * _rig.Player.Setup.Flight.Sensitivity, moved.X, 1e-4f);
        }

        [Test]
        public void J_StopsTheStickButNotTheMouse()
        {
            _input.Dispatch(new SeatInput.Frame { ToggleJoystick = true }, 0f);
            Assert.IsFalse(_input.JoystickEnabled);
            var start = _rig.Player.Flight.Position;

            _input.Dispatch(new SeatInput.Frame { Stick = new Vector2(1f, 0f) }, 0.5f);
            Assert.AreEqual(start, _rig.Player.Flight.Position, "off, the stick steers nothing");

            _input.Dispatch(new SeatInput.Frame { MouseDelta = new Vector2(100f, 0f) }, 0f);
            Assert.AreNotEqual(start, _rig.Player.Flight.Position);
        }

        [Test]
        public void Grapple_ArmsWhileHeld()
        {
            _input.Dispatch(new SeatInput.Frame { GrappleHeld = true }, 0f);
            _rig.Step();
            Assert.IsTrue(_rig.Grapple.IsArmed);

            _input.Dispatch(new SeatInput.Frame { GrappleHeld = false }, 0f);
            _rig.Step();
            Assert.IsFalse(_rig.Grapple.IsArmed);
        }

        [Test]
        public void Brackets_ChangeTheChainLength()
        {
            var mace = _rig.Player.MaceIndex;

            _input.Dispatch(new SeatInput.Frame { ChainShorter = true }, 0f);
            Assert.AreEqual(mace - 1, _rig.Player.MaceIndex);

            _input.Dispatch(new SeatInput.Frame { ChainLonger = true }, 0f);
            Assert.AreEqual(mace, _rig.Player.MaceIndex);
        }

        [Test]
        public void F_TogglesFollow()
        {
            _input.Dispatch(new SeatInput.Frame { ToggleFollow = true }, 0f);

            Assert.IsFalse(_rig.Player.IsFollowing);
            Assert.That(_events.OfType<FollowChanged>().Single().Player, Is.EqualTo(_rig.Id));
        }

        [Test]
        public void Pause_PausesAndR_Resets()
        {
            _input.Dispatch(new SeatInput.Frame { Pause = true }, 0f);
            Assert.IsTrue(_rig.Session.State.IsPaused);

            _input.Dispatch(new SeatInput.Frame { Reset = true }, 0f);
            Assert.That(_events.OfType<WorldReset>().Count(), Is.EqualTo(1));
        }
    }
}
