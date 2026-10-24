using System.Linq;
using MindAttic.Chain.Core;
using NUnit.Framework;

namespace MindAttic.Chain.Tests
{
    /// <summary>Script.vb's language as a Core parser and executor (UNI-T60, BIBLE D29-D32).</summary>
    public class ScriptTests
    {
        private ScriptRunner _runner;
        private float _now;

        [SetUp]
        public void SetUp()
        {
            _runner = new ScriptRunner();
            _now = 1f;
        }

        private ScriptResult Run(string script, ScriptView? view = null)
        {
            _now += 1f;
            return _runner.Execute(script, _now, view ?? ScriptView.Default);
        }

        private static T Single<T>(ScriptResult result) where T : ScriptEffect
        {
            Assert.AreEqual(ScriptOutcome.Ran, result.Outcome, result.ToString());
            Assert.AreEqual(1, result.Effects.Count, string.Join(", ", result.Effects));
            Assert.IsInstanceOf<T>(result.Effects[0]);
            return (T)result.Effects[0];
        }

        // --- parsing -------------------------------------------------------------------------------------------

        [Test]
        public void Parse_FourParts_CommandTypeItemParameter()
        {
            Assert.IsTrue(ScriptLine.TryParse("CHANGE CAMERA X +1", out var line));
            Assert.AreEqual("CHANGE", line.Command);
            Assert.AreEqual("CAMERA", line.Type);
            Assert.AreEqual("X", line.Item);
            Assert.AreEqual("+1", line.Parameter);
            Assert.AreEqual(4, line.Parts);
        }

        [Test]
        public void Parse_TwoParts_OnlyForRun()
        {
            Assert.IsTrue(ScriptLine.TryParse("RUN NIGHT", out var run));
            Assert.AreEqual("RUN", run.Command);
            Assert.AreEqual("NIGHT", run.Type);
            Assert.IsFalse(ScriptLine.TryParse("CHANGE TIME", out _), "a short non-RUN line no longer reuses the last script's fields (D31)");
            Assert.IsFalse(ScriptLine.TryParse("CHANGE TIME DAYPERIOD", out _));
            Assert.IsFalse(ScriptLine.TryParse("CHANGE TIME DAYPERIOD NIGHT NOW", out _));
            Assert.IsFalse(ScriptLine.TryParse("", out _));
            Assert.IsFalse(ScriptLine.TryParse(null, out _));
        }

        [Test]
        public void Parse_IsCaseInsensitive_ForTheWords_KeepsTheParameter()
        {
            Assert.IsTrue(ScriptLine.TryParse("change chain hp 1", out var line));
            Assert.AreEqual("CHANGE", line.Command);
            Assert.AreEqual("CHAIN", line.Type, "the C P shortcut's lower-case 'chain' and a typed CHAIN are the same");
            Assert.IsTrue(line.IsOkay);
        }

        [TestCase("CHANGE HERO HP 1")]
        [TestCase("CHANGE PLAYER HP 1")]
        [TestCase("CHANGE GOODGUY HP 1")]
        [TestCase("CHANGE ENEMY HP 1")]
        [TestCase("CHANGE BADGUY AI 1")]
        [TestCase("CHANGE chain X 1")]
        [TestCase("CHANGE LIGHT AMBIENT 0,0,0")]
        [TestCase("CHANGE TIME DAYPERIOD NIGHT")]
        [TestCase("CHANGE SCRIPT COLOR RED")]
        [TestCase("CHANGE CAMERA X 1")]
        [TestCase("CHANGE CAM Y 1")]
        [TestCase("MAKE ENEMY WERELYNX 2,0,0")]
        [TestCase("MAKE chain ORCHARD1 0,-0.4,-.1")]
        [TestCase("MOVE HERO 0 1,0,0")]
        [TestCase("MOVE ENEMY 0 1,0,0")]
        [TestCase("MOVE chain 0 -1,0,0")]
        [TestCase("RUN NIGHT")]
        [TestCase("RUN ANYTHING")]
        public void IsScriptOkay_Accepts(string script)
        {
            Assert.IsTrue(ScriptLine.TryParse(script, out var line));
            Assert.IsTrue(line.IsOkay, script);
        }

        [TestCase("CHANGE DAY X 1")]
        [TestCase("CHANGE WORLD X 1")]
        [TestCase("MAKE HERO X 1")]
        [TestCase("MAKE LIGHT X 1")]
        [TestCase("MOVE CAMERA X 1")]
        [TestCase("JUMP HERO X 1")]
        [TestCase("CHANGE CAMERA X ")]
        [TestCase("CHANGE  X 1")]
        public void IsScriptOkay_Rejects(string script)
        {
            Assert.IsTrue(ScriptLine.TryParse(script, out var line), "four parts, some perhaps empty");
            Assert.IsFalse(line.IsOkay, script);
        }

        [Test]
        public void InvalidScripts_AreIgnored()
        {
            foreach (var script in new[] { "CHANGE WORLD X 1", "CHANGE TIME", "HELLO", "", "CHANGE CAMERA X 1 " })
            {
                var result = Run(script);
                Assert.AreEqual(ScriptOutcome.Invalid, result.Outcome, script);
                Assert.IsEmpty(result.Effects, script);
            }
        }

        // --- CHANGE CAMERA -------------------------------------------------------------------------------------

        [Test]
        public void Camera_SignedNumber_MovesEyeAndTarget()
        {
            var moved = Single<CameraMoved>(Run("CHANGE CAMERA X +0.5"));
            Assert.AreEqual(ScriptAxis.X, moved.Axis);
            Assert.AreEqual(0.5f, moved.Delta);

            moved = Single<CameraMoved>(Run("CHANGE CAM Y -2"));
            Assert.AreEqual(ScriptAxis.Y, moved.Axis);
            Assert.AreEqual(-2f, moved.Delta, "relative whatever the result (the original's 0..100 heroHP range is not a camera's, D30)");
        }

        [Test]
        public void Camera_BareNumber_PlacesEyeAndTarget()
        {
            var placed = Single<CameraPlaced>(Run("CHANGE CAMERA Y 0.25"));
            Assert.AreEqual(ScriptAxis.Y, placed.Axis);
            Assert.AreEqual(0.25f, placed.Value);
        }

        [Test]
        public void Camera_NotANumber_OrAnotherAxis_ChangesNothing()
        {
            var result = Run("CHANGE CAMERA X LEFT");
            Assert.AreEqual(ScriptOutcome.Ran, result.Outcome, "the script is valid; its parameter just isn't a number (D31)");
            Assert.IsEmpty(result.Effects);
            Assert.IsEmpty(Run("CHANGE CAMERA Z 1").Effects);
        }

        // --- CHANGE LIGHT --------------------------------------------------------------------------------------

        [Test]
        public void Ambient_RGB_SetsTheColour_WhateverTheRedsWidth()
        {
            Assert.AreEqual(new Rgb24(0, 0, 0), Single<AmbientChanged>(Run("CHANGE LIGHT AMBIENT 0,0,0")).Colour, "Turn2Black");
            Assert.AreEqual(new Rgb24(255, 128, 7), Single<AmbientChanged>(Run("CHANGE LIGHT AMBIENT 255,128,7")).Colour,
                "Chars(1) = \",\" only took a one-digit red (D30)");
            Assert.AreEqual(new Rgb24(1, 2, 255), Single<AmbientChanged>(Run("CHANGE LIGHT AMBIENT 1.9,2.2,999")).Colour,
                "Int() of each, held to 0-255 where FromArgb threw");
            Assert.IsEmpty(Run("CHANGE LIGHT AMBIENT 1,2").Effects);
            Assert.IsEmpty(Run("CHANGE LIGHT AMBIENT a,b,c").Effects);
        }

        [Test]
        public void Ambient_Number_AddsToEachChannel_HeldToAByte()
        {
            var view = ScriptView.Default;
            view.Ambient = new Rgb24(100, 5, 250);

            Assert.AreEqual(new Rgb24(90, 0, 240), Single<AmbientChanged>(Run("CHANGE LIGHT AMBIENT -10", view)).Colour, "Fade2Black");
            Assert.AreEqual(new Rgb24(110, 15, 255), Single<AmbientChanged>(Run("CHANGE LIGHT AMBIENT 10", view)).Colour,
                "unsigned adds too, as the original's Else did");

            view.Ambient = new Rgb24(5, 5, 5);
            Assert.IsEmpty(Run("CHANGE LIGHT AMBIENT -10", view).Effects, "every channel would go below 0: no change, as the original");
        }

        [TestCase("RED", TintChannel.Red)]
        [TestCase("R", TintChannel.Red)]
        [TestCase("GREEN", TintChannel.Green)]
        [TestCase("G", TintChannel.Green)]
        [TestCase("BLUE", TintChannel.Blue)]
        [TestCase("B", TintChannel.Blue)]
        public void Tint_SetsOrStepsItsOwnChannel(string item, TintChannel channel)
        {
            var view = ScriptView.Default;
            view.Red = 10f;
            view.Green = 100f;
            view.Blue = 200f;
            var start = view.Tint(channel);

            var set = Single<TintChanged>(Run($"CHANGE LIGHT {item} 50", view));
            Assert.AreEqual(channel, set.Channel);
            Assert.AreEqual(50f, set.Value);

            Assert.AreEqual(start - 5f, Single<TintChanged>(Run($"CHANGE LIGHT {item} -5", view)).Value,
                "stepped from its own channel (GREEN and BLUE checked red's, D30)");
            Assert.AreEqual(255f, Single<TintChanged>(Run($"CHANGE LIGHT {item} +999", view)).Value, "held to 255, not assigned 999 or thrown");
            Assert.AreEqual(0f, Single<TintChanged>(Run($"CHANGE LIGHT {item} -999", view)).Value);
            Assert.AreEqual(255f, Single<TintChanged>(Run($"CHANGE LIGHT {item} 300", view)).Value, "ValueInRange");
        }

        [Test]
        public void Light_IsNoLongerRejected()
        {
            Assert.AreEqual(ScriptOutcome.Ran, Run("CHANGE LIGHT RED 0").Outcome, "isScriptOkay left LIGHT out (D29)");
        }

        // --- CHANGE TIME ---------------------------------------------------------------------------------------

        [TestCase("MORNING", DayPeriod.Morning)]
        [TestCase("NIGHT", DayPeriod.Night)]
        [TestCase("DAY", DayPeriod.Day)]
        [TestCase("DUSK", DayPeriod.Dusk)]
        [TestCase("NOON", DayPeriod.Day)]
        [TestCase("DAWN", DayPeriod.Morning)]
        [TestCase("SUNRISE", DayPeriod.Morning)]
        [TestCase("SUNSET", DayPeriod.Dusk)]
        [TestCase("LIGHT", DayPeriod.Day)]
        [TestCase("DARK", DayPeriod.Night)]
        [TestCase("Night", DayPeriod.Night)]
        public void DayPeriod_Names(string name, DayPeriod period)
        {
            Assert.AreEqual(period, Single<DayPeriodChanged>(Run($"CHANGE TIME DAYPERIOD {name}")).Period);
        }

        [Test]
        public void DayPeriod_Unknown_ChangesNothing()
        {
            Assert.IsEmpty(Run("CHANGE TIME DAYPERIOD TEATIME").Effects);
            Assert.IsEmpty(Run("CHANGE TIME DATE 12/25/04").Effects);
        }

        // --- CHANGE SCRIPT -------------------------------------------------------------------------------------

        [Test]
        public void ScriptColour_StartsBlue_AndTakesTheNamedColours()
        {
            Assert.AreEqual(new Rgb24(0, 0, 255), _runner.Colour);
            Assert.AreEqual(new Rgb24(255, 0, 0), Single<ScriptColourChanged>(Run("CHANGE SCRIPT COLOR RED")).Colour);
            Assert.AreEqual(new Rgb24(0, 128, 0), Single<ScriptColourChanged>(Run("CHANGE SCRIPT COLOR GREEN")).Colour, "Color.Green");
            Assert.AreEqual(new Rgb24(255, 0, 255), Single<ScriptColourChanged>(Run("CHANGE SCRIPT COLOR FUSCHIA")).Colour);
            Assert.AreEqual(new Rgb24(255, 0, 255), _runner.Colour);
            Assert.AreEqual(new Rgb24(0, 0, 255), Single<ScriptColourChanged>(Run("CHANGE SCRIPT COLOR BLUE")).Colour);
            Assert.IsEmpty(Run("CHANGE SCRIPT COLOR PLAID").Effects);
        }

        [Test]
        public void ScriptRunning_OnAndOff_AndHeroAiStop()
        {
            Assert.IsFalse(_runner.Running);
            Assert.IsTrue(Single<ScriptRunningChanged>(Run("CHANGE SCRIPT RUNNING ON")).Running);
            Assert.IsTrue(_runner.Running);
            Assert.IsFalse(Single<ScriptRunningChanged>(Run("CHANGE HERO AI STOP")).Running, "cmdChangeHero's one live line");
            Assert.IsFalse(_runner.Running);
            Run("CHANGE SCRIPT RUNNING ON");
            Assert.IsFalse(Single<ScriptRunningChanged>(Run("CHANGE SCRIPT RUNNING OFF")).Running);
        }

        // --- stubs and no-ops ----------------------------------------------------------------------------------

        [TestCase("CHANGE HERO HP -10")]
        [TestCase("CHANGE HERO HEALTH +1")]
        [TestCase("CHANGE HERO X 1")]
        [TestCase("CHANGE HERO FACING LEFT")]
        [TestCase("CHANGE HERO AI JUMP")]
        [TestCase("CHANGE ENEMY HP 1")]
        [TestCase("CHANGE ENEMY AI retreat")]
        [TestCase("CHANGE chain X 1")]
        [TestCase("MAKE ENEMY WERELYNX 2,0,0")]
        [TestCase("MOVE chain 0 -0.004,0.01,0")]
        [TestCase("RUN POISON")]
        public void Stubs_RunAndChangeNothing(string script)
        {
            var result = Run(script);
            Assert.AreEqual(ScriptOutcome.Ran, result.Outcome);
            Assert.IsEmpty(result.Effects);
        }

        // --- RUN -----------------------------------------------------------------------------------------------

        [Test]
        public void RunNight_SetsTheNightPeriod()
        {
            Assert.AreEqual(DayPeriod.Night, Single<DayPeriodChanged>(Run("RUN NIGHT")).Period);
        }

        [Test]
        public void RunHeaven_RunsBothOfItsStubs_AndChangesNothing()
        {
            var result = Run("RUN HEAVEN");
            Assert.AreEqual(ScriptOutcome.Ran, result.Outcome);
            Assert.IsEmpty(result.Effects);
            Assert.AreEqual(ScriptOutcome.Throttled, _runner.Execute("CHANGE HERO Y +1", _now, ScriptView.Default).Outcome,
                "its second line ran too (the one lastEventCheck swallowed it, D32)");
        }

        [Test]
        public void Run_FourParts_RunsByItsType()
        {
            Assert.AreEqual(DayPeriod.Night, Single<DayPeriodChanged>(Run("RUN NIGHT NOW PLEASE")).Period);
        }

        // --- throttle ------------------------------------------------------------------------------------------

        [Test]
        public void Throttle_IsPerScript()
        {
            Assert.IsTrue(_runner.Execute("CHANGE CAMERA X +1", 5f, ScriptView.Default).Ran);
            Assert.AreEqual(ScriptOutcome.Throttled, _runner.Execute("CHANGE CAMERA X +1", 5f, ScriptView.Default).Outcome,
                "the same instant: not later than lastEventCheck + 0");
            Assert.IsTrue(_runner.Execute("CHANGE CAMERA Y +1", 5f, ScriptView.Default).Ran, "another script is not held up");
            Assert.IsTrue(_runner.Execute("CHANGE CAMERA X +1", 5.01f, ScriptView.Default).Ran);
        }

        [Test]
        public void Throttle_WaitsOutTheIncrement()
        {
            Assert.IsTrue(_runner.Execute("RUN NIGHT", 10f, ScriptView.Default, 2f).Ran);
            Assert.AreEqual(ScriptOutcome.Throttled, _runner.Execute("RUN NIGHT", 11f, ScriptView.Default, 2f).Outcome);
            Assert.AreEqual(ScriptOutcome.Throttled, _runner.Execute("RUN NIGHT", 12f, ScriptView.Default, 2f).Outcome, "must be later than last + increment");
            Assert.IsTrue(_runner.Execute("RUN NIGHT", 12.5f, ScriptView.Default, 2f).Ran);

            _runner.ResetThrottle();
            Assert.IsTrue(_runner.Execute("RUN NIGHT", 12.5f, ScriptView.Default, 2f).Ran);
        }

        [Test]
        public void InvalidScripts_DoNotStampTheThrottle()
        {
            Assert.AreEqual(ScriptOutcome.Invalid, _runner.Execute("CHANGE WORLD X 1", 5f, ScriptView.Default).Outcome);
            Assert.AreEqual(ScriptOutcome.Invalid, _runner.Execute("CHANGE WORLD X 1", 5f, ScriptView.Default).Outcome);
        }

        // --- the view ------------------------------------------------------------------------------------------

        [Test]
        public void TheResultsView_CarriesTheChanges_AndTheGameIsUntouched()
        {
            var lighting = new LightingState();
            var view = ScriptView.Of(Rgb24.White, lighting);

            var result = Run("CHANGE LIGHT GREEN -55", view);

            Assert.AreEqual(200f, result.View.Green);
            Assert.AreEqual(255f, lighting.Green, "the runner only reports; presentation applies");
            lighting.SetTint(TintChannel.Green, ((TintChanged)result.Effects.Single()).Value);
            Assert.AreEqual(200f, lighting.Green);
        }

        [Test]
        public void LightingState_SetTint_HoldsToAByte()
        {
            var lighting = new LightingState();
            lighting.SetTint(TintChannel.Blue, -3f);
            lighting.SetTint(TintChannel.Red, 400f);
            Assert.AreEqual(0f, lighting.Blue);
            Assert.AreEqual(255f, lighting.Red);
        }
    }
}
