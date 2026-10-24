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
                    new[]
                    {
                        "Steer", "SteerStick", "Grapple", "ChainShorter", "ChainLonger", "ToggleFollow", "Pause", "Quit", "Reset", "ToggleJoystick",
                        "ResistanceUp", "ResistanceDown", "GravityDown", "GravityUp", "SpringUp", "SpringDown",
                        "GameSpeedDown", "GameSpeedUp",
                    },
                    controls.Player.actions.Select(a => a.name).ToArray());
                CollectionAssert.AreEquivalent(
                    new[] { "ToggleLighting", "ShakeScreen", "ToggleFps", "ToggleMenu", "ToggleWireframe", "ToggleTextures", "ToggleCulling", "ConfigureVideo", "ToggleZones" },
                    controls.Debug.actions.Select(a => a.name).ToArray());
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
                CollectionAssert.Contains(Paths(c.ResistanceUp), "<Keyboard>/insert", "Form1.vb changeResistance(0.001)");
                CollectionAssert.Contains(Paths(c.ResistanceDown), "<Keyboard>/delete", "Form1.vb changeResistance(-0.001)");
                CollectionAssert.Contains(Paths(c.GravityDown), "<Keyboard>/home", "Form1.vb changeGravity(-0.00001)");
                CollectionAssert.Contains(Paths(c.GravityUp), "<Keyboard>/end", "Form1.vb changeGravity(0.00001)");
                CollectionAssert.Contains(Paths(c.SpringUp), "<Keyboard>/pageUp", "Form1.vb changeSpring(0.001)");
                CollectionAssert.Contains(Paths(c.SpringDown), "<Keyboard>/pageDown", "Form1.vb changeSpring(-0.001)");
            }
        }

        [Test]
        public void MinusAndEquals_AreTheGameSpeedOnly()
        {
            using (var c = new ChainControls())
            {
                CollectionAssert.AreEqual(new[] { "<Keyboard>/minus" }, Paths(c.GameSpeedDown), "Input.vb: gameSpeed -= 0.01");
                CollectionAssert.AreEqual(new[] { "<Keyboard>/equals" }, Paths(c.GameSpeedUp), "Input.vb: gameSpeed += 0.01");
                var others = c.Asset.Where(a => a != c.GameSpeedDown && a != c.GameSpeedUp)
                    .SelectMany(a => a.bindings).Select(b => b.path).ToArray();
                CollectionAssert.DoesNotContain(others, "<Keyboard>/minus", "no longer bound to chain length (UNI-T34)");
                CollectionAssert.DoesNotContain(others, "<Keyboard>/equals");
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
        public void TuningKeys_MoveTheReadoutsFigures()
        {
            var mapping = _rig.Session.Settings.Mapping;
            ChainReadout Readout() => ChainReadout.From(_rig.Player.Chain.Config, mapping);

            _input.Dispatch(new SeatInput.Frame { ResistanceUp = true, GravityDown = true, SpringUp = true }, 0f);
            Assert.AreEqual("Res 0.251\nGrav -0.00251\nSpring 0.101", HudText.Readout(Readout()));
            Assert.That(_events.OfType<ChainTuned>().Count(), Is.EqualTo(3));

            _input.Dispatch(new SeatInput.Frame { ResistanceDown = true, GravityUp = true, SpringDown = true }, 0f);
            _input.Dispatch(new SeatInput.Frame { ResistanceDown = true, GravityUp = true, SpringDown = true }, 0f);
            Assert.AreEqual("Res 0.249\nGrav -0.00249\nSpring 0.099", HudText.Readout(Readout()));
        }

        [Test]
        public void MinusAndEquals_HeldMoveTheGameSpeed_EvenPaused()
        {
            var world = _rig.Session.World;

            _input.Dispatch(new SeatInput.Frame { GameSpeedUp = true }, 1f / 60f);
            Assert.AreEqual("1.01", HudText.GameSpeed(world.GameSpeed), "0.01 a frame held, shown with two decimals");
            Assert.That(_events.OfType<GameSpeedChanged>().Single().Player, Is.EqualTo(_rig.Id));

            _rig.Session.Submit(new Pause(_rig.Id, true));
            _input.Dispatch(new SeatInput.Frame { GameSpeedDown = true }, 0f, 3f / 60f);
            Assert.AreEqual("0.98", HudText.GameSpeed(world.GameSpeed), "paused, the real frame time counts");

            _input.Dispatch(new SeatInput.Frame { GameSpeedDown = true, GameSpeedUp = true }, 0f, 1f);
            Assert.AreEqual("0.98", HudText.GameSpeed(world.GameSpeed), "both held cancel out, as in Input.vb");
        }

        [Test]
        public void PauseKeyOrP_Pause_AndEscapeQuits()
        {
            using (var c = new ChainControls())
            {
                CollectionAssert.AreEquivalent(
                    new[] { "<Keyboard>/pause", "<Keyboard>/p", "<Gamepad>/start" }, Paths(c.Pause),
                    "Form1.vb's Pause key, P as the extra (BIBLE V8); Escape no longer pauses");
                CollectionAssert.AreEqual(new[] { "<Keyboard>/escape" }, Paths(c.Quit), "Form1.vb's Escape quits");
            }
        }

        [Test]
        public void Escape_QuitsThroughACommand_EvenPaused()
        {
            var quitting = 0;
            _rig.Session.Quitting += () => quitting++;
            _input.Dispatch(new SeatInput.Frame { Pause = true }, 0f);

            _input.Dispatch(new SeatInput.Frame { Quit = true }, 0f);

            var quit = _events.OfType<QuitRequested>().Single();
            Assert.AreEqual(_rig.Id, quit.Player);
            Assert.AreEqual(1, quitting);
            Assert.IsTrue(_rig.Session.State.IsPaused, "quitting does not resume");
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
