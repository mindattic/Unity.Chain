using System.Linq;
using MindAttic.Chain.Core;
using NUnit.Framework;

namespace MindAttic.Chain.Tests
{
    /// <summary>Form1.vb's scripter mode and Script.vb's showScripter (U.CHN-T61, BIBLE D33).</summary>
    public class ScripterTests
    {
        private Scripter _scripter;

        [SetUp]
        public void SetUp()
        {
            _scripter = new Scripter();
        }

        private void Press(params ScripterKey[] keys)
        {
            foreach (var key in keys)
            {
                Assert.IsNull(_scripter.Press(key), key.ToString());
            }
        }

        private Scripter Opened()
        {
            _scripter.Press(ScripterKey.Oemtilde);
            Assert.IsTrue(_scripter.IsOpen);
            return _scripter;
        }

        [Test]
        public void Grave_OpensIt_AndClosesItAgain_KeepingTheLine()
        {
            Assert.IsFalse(_scripter.IsOpen, "bScripter starts False");
            Opened();
            Press(ScripterKey.R, ScripterKey.U, ScripterKey.N);
            Press(ScripterKey.Oemtilde);
            Assert.IsFalse(_scripter.IsOpen, "the first Oemtilde case closes it; the second (typing ~) is unreachable");
            Assert.AreEqual("RUN", _scripter.Text);
        }

        [Test]
        public void Closed_OnlyTheGraveKeyDoesAnything()
        {
            Press(ScripterKey.A, ScripterKey.Enter, ScripterKey.Insert, ScripterKey.Escape);
            Assert.IsFalse(_scripter.IsOpen);
            Assert.AreEqual(string.Empty, _scripter.Text);
        }

        [Test]
        public void EscapeAndF11_Close()
        {
            Opened();
            Press(ScripterKey.Escape);
            Assert.IsFalse(_scripter.IsOpen);
            Opened();
            Press(ScripterKey.F11);
            Assert.IsFalse(_scripter.IsOpen);
        }

        [Test]
        public void Letters_TypeThemselves_UpperCase()
        {
            Opened();
            Press(ScripterKey.R, ScripterKey.U, ScripterKey.N, ScripterKey.Space, ScripterKey.N, ScripterKey.I,
                ScripterKey.G, ScripterKey.H, ScripterKey.T);
            Assert.AreEqual("RUN NIGHT", _scripter.Text, "H and T away from CHANGE type their letter");
        }

        [Test]
        public void C_OnAnEmptyLine_TypesChange_AndAgain_ChangeCamera()
        {
            Opened();
            Press(ScripterKey.C);
            Assert.AreEqual("CHANGE ", _scripter.Text);
            Press(ScripterKey.C);
            Assert.AreEqual("CHANGE CAMERA ", _scripter.Text);
            Press(ScripterKey.C);
            Assert.AreEqual("CHANGE CAMERA C", _scripter.Text, "past the shortcut C types its letter");
        }

        [TestCase(ScripterKey.H, "CHANGE HERO ")]
        [TestCase(ScripterKey.P, "CHANGE chain ")]
        [TestCase(ScripterKey.S, "CHANGE SCRIPT ")]
        [TestCase(ScripterKey.T, "CHANGE TIME ")]
        public void Shortcuts_AfterChange(ScripterKey key, string expected)
        {
            Opened();
            Press(ScripterKey.C, key);
            Assert.AreEqual(expected, _scripter.Text);
        }

        [Test]
        public void SpaceDigitsAndPunctuation_Type()
        {
            Opened();
            Press(ScripterKey.Space, ScripterKey.D0, ScripterKey.D1, ScripterKey.D9, ScripterKey.OemSemicolon,
                ScripterKey.OemPipe, ScripterKey.OemPeriod, ScripterKey.OemBackslash, ScripterKey.OemQuestion,
                ScripterKey.Oemplus, ScripterKey.OemMinus, ScripterKey.Oemcomma, ScripterKey.OemQuotes);
            Assert.AreEqual(" 019:|./?+-,\"", _scripter.Text, "as Form1.vb's Punctuation and Number Keys cases type them");
        }

        [Test]
        public void OtherKeys_TypeNothing_D33()
        {
            Opened();
            Press(ScripterKey.None);
            Assert.AreEqual(string.Empty, _scripter.Text, "the original's Case Else typed a stray key's name (F5, NumPad7)");
        }

        [Test]
        public void Back_DeletesALetter_AndDelete_Clears()
        {
            Opened();
            Press(ScripterKey.R, ScripterKey.U, ScripterKey.N, ScripterKey.Back);
            Assert.AreEqual("RU", _scripter.Text);
            Press(ScripterKey.Delete);
            Assert.AreEqual(string.Empty, _scripter.Text);
            Press(ScripterKey.Back, ScripterKey.Delete);
            Assert.AreEqual(string.Empty, _scripter.Text, "nothing to delete is no error");
        }

        [Test]
        public void Enter_ReturnsTheLine_RemembersIt_ClearsAndCloses()
        {
            Opened();
            Press(ScripterKey.R, ScripterKey.U, ScripterKey.N, ScripterKey.Space, ScripterKey.N);
            var ran = _scripter.Press(ScripterKey.Enter);
            Assert.AreEqual("RUN N", ran);
            Assert.AreEqual("RUN N", _scripter.LastScript);
            Assert.AreEqual(string.Empty, _scripter.Text);
            Assert.IsFalse(_scripter.IsOpen);
        }

        [Test]
        public void Insert_RecallsTheLastScript()
        {
            Opened();
            Press(ScripterKey.C, ScripterKey.T);
            _scripter.Press(ScripterKey.Enter);
            Opened();
            Press(ScripterKey.A, ScripterKey.Insert);
            Assert.AreEqual("CHANGE TIME ", _scripter.Text);
            Assert.IsTrue(_scripter.IsOpen, "Insert keeps it open");
        }

        [Test]
        public void TypedLine_RunsThroughTheRunner()
        {
            Opened();
            // C C types CHANGE CAMERA; the rest is typed key by key.
            Press(ScripterKey.C, ScripterKey.C, ScripterKey.X, ScripterKey.Space, ScripterKey.Oemplus, ScripterKey.D2);
            var script = _scripter.Press(ScripterKey.Enter);
            Assert.AreEqual("CHANGE CAMERA X +2", script);
            var result = _scripter.Run(script, 1f, ScriptView.Default);
            Assert.IsTrue(result.Ran);
            var moved = (CameraMoved)result.Effects.Single();
            Assert.AreEqual(ScriptAxis.X, moved.Axis);
            Assert.AreEqual(2f, moved.Delta);
        }

        [Test]
        public void Type_PressesEachCharactersKey_ShortcutsIncluded()
        {
            Opened();
            _scripter.Type("cs");
            Assert.AreEqual("CHANGE SCRIPT ", _scripter.Text);
            _scripter.Type("COLOR RED");
            Assert.AreEqual("CHANGE SCRIPT COLOR RED", _scripter.Text);
            Assert.AreEqual("CHANGE SCRIPT COLOR RED", _scripter.Type("\n"));
        }

        [Test]
        public void Line_ShowsTheBeam_FlippingEveryHalfSecond()
        {
            Opened();
            _scripter.Type("RUN");
            Assert.AreEqual("\\ > RUN ", _scripter.Line, "IBeam starts False");
            Assert.IsFalse(_scripter.Tick(0.5f), "not past lastBeamCheck + 0.5 yet");
            Assert.IsTrue(_scripter.Tick(0.51f));
            Assert.AreEqual("/ > RUN|", _scripter.Line);
            Assert.IsTrue(_scripter.Tick(1.0f));
            Assert.IsFalse(_scripter.Tick(1.02f));
        }

        [Test]
        public void Colour_IsTheRunners_AndScriptColorChangesIt()
        {
            Assert.AreEqual(new Rgb24(0, 0, 255), _scripter.Colour, "scriptColor starts Color.FromArgb(0, 0, 255)");
            _scripter.Run("CHANGE SCRIPT COLOR RED", 1f, ScriptView.Default);
            Assert.AreEqual(new Rgb24(255, 0, 0), _scripter.Colour);
        }

        [Test]
        public void OpenChanged_IsRaisedOnEachChange()
        {
            var changes = 0;
            _scripter.OpenChanged += _ => changes++;
            _scripter.Toggle();
            _scripter.Open();
            _scripter.Toggle();
            Assert.AreEqual(2, changes);
        }

        [Test]
        public void HudJson_HoldsShowScripter()
        {
            var scripter = GameSettings.Load(new DirectorySettingsSource(GameSettingsTests.ShippedFolder)).Hud.Scripter;
            Assert.AreEqual(HudAnchor.TopLeft, scripter.Style.Anchor);
            Assert.AreEqual(new Vec2(50f, 398f), scripter.Style.Position, "50, iHeight - 50");
            Assert.AreEqual(10f, scripter.Style.FontSize, "fontMenu: Arial 10 bold");
            Assert.IsTrue(scripter.Style.Bold);
            Assert.AreEqual(new Rgb24(0, 0, 255), scripter.Colour, "scriptColor = Color.FromArgb(0, 0, 255)");
            Assert.AreEqual(0.5f, scripter.BeamSeconds, "lastBeamCheck + 0.5");
        }

        // --- the gate ----------------------------------------------------------------------------------------------

        [Test]
        public void Gate_ScripterHoldsBackBoth_ARunningScriptOnlyPlay()
        {
            var gate = new InputGate();
            Assert.IsFalse(gate.KeysSuspended);
            Assert.IsFalse(gate.PlaySuspended);

            gate.Set(scripterOpen: true, scriptRunning: false);
            Assert.IsTrue(gate.KeysSuspended, "bScripter: OnKeyDown types instead");
            Assert.IsTrue(gate.PlaySuspended, "bScripter: getInput is skipped");

            gate.Set(scripterOpen: false, scriptRunning: true);
            Assert.IsFalse(gate.KeysSuspended, "OnKeyDown never reads bScriptRunning");
            Assert.IsTrue(gate.PlaySuspended, "getInput: If Not bScriptRunning");

            gate.Clear();
            Assert.IsFalse(gate.PlaySuspended);
        }

        // --- applying the effects ----------------------------------------------------------------------------------

        [Test]
        public void Effects_MoveAndPlaceTheCamera_InTheOriginalsScreenUnits()
        {
            var camera = new DebugCamera(DebugCamera.Settings.Original);
            var space = new OriginalSpace(9f, 60f);
            var runner = new ScriptRunner();

            ScriptEffects.Apply(runner.Execute("CHANGE CAMERA X +0.5", 1f).Effects, null, camera, space);
            Assert.AreEqual(4.5f, camera.Eye.X, 1e-5f, "half a screen at 9 world units a screen");
            Assert.AreEqual(4.5f, camera.Target.X, 1e-5f, "eye and target together");

            camera.Isometric(+1, 1f);
            ScriptEffects.Apply(runner.Execute("CHANGE CAM Y 0.1", 2f).Effects, null, camera, space);
            Assert.AreEqual(0.9f, camera.Eye.Y, 1e-5f, "cam.Y = Parameter");
            Assert.AreEqual(0.9f, camera.Target.Y, 1e-5f, "target.Y = Parameter");
        }

        [Test]
        public void Effects_SetTheLight()
        {
            var lighting = new LightingState();
            var runner = new ScriptRunner();
            var view = ScriptView.Of(lighting.Ambient, lighting);

            Assert.AreEqual(1, ScriptEffects.Apply(runner.Execute("CHANGE LIGHT AMBIENT 255,128,0", 1f, view).Effects, lighting, null, OriginalSpace.Identity));
            Assert.AreEqual(new Rgb24(255, 128, 0), lighting.Ambient);
            var lit = lighting.LightColour(1f);
            Assert.AreEqual(1f, lit.R, 1e-5f);
            Assert.AreEqual(128f / 255f, lit.G, 1e-5f, "the scene is lit by the ambient colour times the tint");
            Assert.AreEqual(0f, lit.B, 1e-5f);

            ScriptEffects.Apply(runner.Execute("CHANGE LIGHT RED 100", 2f, view).Effects, lighting, null, OriginalSpace.Identity);
            Assert.AreEqual(100f, lighting.Red);

            ScriptEffects.Apply(runner.Execute("RUN NIGHT", 3f, view).Effects, lighting, null, OriginalSpace.Identity);
            Assert.AreEqual(DayPeriod.Night, lighting.Period);

            lighting.NewRound();
            Assert.AreEqual(Rgb24.White, lighting.Ambient, "Root.vb's Reset: AmbientColor = Color.White");
        }

        [Test]
        public void Effects_WithNoTarget_AreDropped()
        {
            var runner = new ScriptRunner();
            Assert.AreEqual(0, ScriptEffects.Apply(runner.Execute("CHANGE CAMERA X 1", 1f).Effects, null, null, OriginalSpace.Identity));
            Assert.AreEqual(0, ScriptEffects.Apply(runner.Execute("CHANGE SCRIPT RUNNING ON", 2f).Effects, new LightingState(), null, OriginalSpace.Identity),
                "the running flag is the runner's own");
            Assert.IsTrue(runner.Running);
        }
    }
}
