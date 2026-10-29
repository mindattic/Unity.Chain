using System.Collections.Generic;
using System.Linq;
using MindAttic.Chain.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MindAttic.Chain.Tests
{
    /// <summary>
    /// U.CHN-T61: Form1.vb's scripter mode and Script.vb's showScripter on the HUD, running its scripts into the camera and
    /// the light, and holding the game's input back while it is open. The key handling itself is tested in
    /// Tests/Core.Tests (ScripterTests).
    /// </summary>
    public class ScripterConsoleTests
    {
        private TestRig _rig;
        private GameHud _hud;
        private SceneLight _light;
        private CameraRig _camera;
        private ScripterConsole _console;
        private readonly List<GameEvent> _events = new List<GameEvent>();

        [SetUp]
        public void SetUp()
        {
            _rig = new TestRig();
            _rig.Session.EventRaised += _events.Add;
            _hud = _rig.Track(new GameObject("Hud")).AddComponent<GameHud>();
            _hud.Session = _rig.Session;
            _light = _rig.Track(new GameObject("Light")).AddComponent<SceneLight>();
            _light.Session = _rig.Session;
            var cameraGo = _rig.Track(new GameObject("Camera"));
            cameraGo.AddComponent<Camera>().orthographic = true;
            _camera = cameraGo.AddComponent<CameraRig>();
            _camera.Session = _rig.Session;

            _console = _hud.gameObject.AddComponent<ScripterConsole>();
            _console.Session = _rig.Session;
            _console.SceneLight = _light;
            _console.Rig = _camera;
            _console.Hud = _hud;
        }

        [TearDown]
        public void TearDown()
        {
            _rig.Dispose();
            _events.Clear();
        }

        private void Type(params ScripterKey[] keys)
        {
            foreach (var key in keys)
            {
                _console.Press(key);
            }
        }

        [Test]
        public void GraveKey_IsTheDebugMapsToggleScripter()
        {
            using (var controls = new ChainControls())
            {
                CollectionAssert.AreEqual(new[] { "<Keyboard>/backquote" }, controls.ToggleScripter.bindings.Select(b => b.path).ToArray());
                Assert.AreSame(controls.Debug, controls.ToggleScripter.actionMap);
            }

            var map = ScripterConsole.KeyMap.ToDictionary(e => e.key, e => e.scripter);
            Assert.AreEqual(ScripterKey.A, map[Key.A]);
            Assert.AreEqual(ScripterKey.Z, map[Key.Z]);
            Assert.AreEqual(ScripterKey.D0, map[Key.Digit0]);
            Assert.AreEqual(ScripterKey.D9, map[Key.Digit9]);
            Assert.AreEqual(ScripterKey.Oemtilde, map[Key.Backquote]);
            Assert.AreEqual(ScripterKey.Back, map[Key.Backspace]);
            Assert.AreEqual(ScripterKey.Oemplus, map[Key.Equals]);
        }

        [Test]
        public void Open_EchoesTheLine_OnTheHud_InTheScriptColour()
        {
            Assert.IsFalse(_console.LineShown, "nothing drawn until bScripter");
            Assert.AreSame(_hud.Root, _console.LineLabel.parent, "a panel over the HUD's own layers");

            Type(ScripterKey.Oemtilde, ScripterKey.C, ScripterKey.T);
            Assert.IsTrue(_console.IsOpen);
            Assert.IsTrue(_console.LineShown);
            StringAssert.Contains("> CHANGE TIME ", _console.LineText);
            Assert.AreEqual((Color)new Color32(0, 0, 255, 255), _console.LineLabel.style.color.value, "scriptColor = Color.FromArgb(0, 0, 255)");

            Type(ScripterKey.Escape);
            Assert.IsFalse(_console.IsOpen);
            Assert.IsFalse(_console.LineShown);
        }

        [Test]
        public void Line_IsHiddenWhilePaused_AsShowScripterRunsOnlyUnpaused()
        {
            Type(ScripterKey.Oemtilde);
            _rig.Session.Submit(new Pause(_rig.Id, true));
            _console.Refresh(10f);
            Assert.IsFalse(_console.LineShown);
            Assert.IsTrue(_console.IsOpen, "still typing: OnKeyDown runs paused");
            _rig.Session.Submit(new Pause(_rig.Id, false));
            _console.Refresh(10f);
            Assert.IsTrue(_console.LineShown);
        }

        [Test]
        public void Enter_RunsCameraScripts_IntoTheRig_InTheOriginalsScreenUnits()
        {
            Type(ScripterKey.Oemtilde, ScripterKey.C, ScripterKey.C, ScripterKey.X, ScripterKey.Space, ScripterKey.Oemplus, ScripterKey.D1);
            var result = _console.Press(ScripterKey.Enter);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Ran, result.ToString());
            Assert.IsFalse(_console.IsOpen, "Enter closes it");
            Assert.AreEqual("CHANGE CAMERA X +1", _console.Scripter.LastScript);
            var length = _rig.Session.Settings.Mapping.Length(1f);
            Assert.AreEqual(length, _camera.View.Eye.X, 1e-4f, "cam.X + 1 screen");
            Assert.AreEqual(length, _camera.View.Target.X, 1e-4f, "target.X + 1 screen");

            // Insert recalls it; Enter runs it again (a later instant, so the throttle lets it through).
            Type(ScripterKey.Oemtilde, ScripterKey.Insert);
            StringAssert.Contains("CHANGE CAMERA X +1", _console.LineText);
        }

        [Test]
        public void Enter_RunsLightAndScriptColourScripts()
        {
            Assert.IsTrue(_console.Run("CHANGE LIGHT AMBIENT 255,0,0").Ran);
            Assert.AreEqual(new Rgb24(255, 0, 0), _light.State.Ambient);
            Assert.AreEqual(0f, _light.CurrentColour.g, 1e-5f, "the scene is lit by the ambient colour");

            Assert.IsTrue(_console.Run("CHANGE TIME DAYPERIOD NIGHT").Ran);
            Assert.AreEqual(DayPeriod.Night, _light.State.Period);

            _console.Run("CHANGE SCRIPT COLOR RED");
            Type(ScripterKey.Oemtilde);
            Assert.AreEqual((Color)new Color32(255, 0, 0, 255), _console.LineLabel.style.color.value);
        }

        [Test]
        public void WhileOpen_TheSeatsKeysAndPlayInput_AreHeldBack()
        {
            var input = _rig.Angel.GetComponent<SeatInput>();
            Type(ScripterKey.Oemtilde);
            Assert.IsTrue(_rig.Session.InputGate.KeysSuspended);
            Assert.IsTrue(_rig.Session.InputGate.PlaySuspended);

            var start = _rig.Player.Flight.Position;
            input.Dispatch(new SeatInput.Frame { MouseDelta = new Vector2(100f, 0f), Quit = true, Reset = true, Pause = true }, 0.1f);
            Assert.AreEqual(start, _rig.Player.Flight.Position, "getInput is skipped: the mouse does not steer");
            Assert.IsFalse(_events.OfType<QuitRequested>().Any(), "Escape closes the scripter, it does not quit");
            Assert.IsFalse(_events.OfType<WorldReset>().Any(), "R types R");
            Assert.IsFalse(_rig.Session.State.IsPaused);

            Type(ScripterKey.Escape);
            Assert.IsFalse(_rig.Session.InputGate.KeysSuspended);
            input.Dispatch(new SeatInput.Frame { MouseDelta = new Vector2(100f, 0f) }, 0.1f);
            Assert.AreNotEqual(start, _rig.Player.Flight.Position, "closed, the mouse steers again");
        }

        [Test]
        public void ScriptRunningOn_HoldsBackPlayOnly_UntilItIsTurnedOff()
        {
            _console.Run("CHANGE SCRIPT RUNNING ON");
            Type(ScripterKey.Oemtilde, ScripterKey.Escape);
            Assert.IsFalse(_rig.Session.InputGate.KeysSuspended, "OnKeyDown never reads bScriptRunning");
            Assert.IsTrue(_rig.Session.InputGate.PlaySuspended, "getInput: If Not bScriptRunning");

            var frame = SeatInput.Suspend(new SeatInput.Frame { MouseDelta = Vector2.one, ChainLonger = true, Reset = true }, false, true);
            Assert.AreEqual(Vector2.zero, frame.MouseDelta);
            Assert.IsFalse(frame.ChainLonger, "[ and ] are Input.vb's getKeyboard");
            Assert.IsTrue(frame.Reset, "R is Form1.vb's OnKeyDown");

            _console.Run("CHANGE SCRIPT RUNNING OFF");
            Type(ScripterKey.Oemtilde, ScripterKey.Escape);
            Assert.IsFalse(_rig.Session.InputGate.PlaySuspended);
        }
    }
}
