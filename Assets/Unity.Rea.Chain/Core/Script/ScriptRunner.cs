using System;
using System.Collections.Generic;

namespace MindAttic.Chain.Core
{
    /// <summary>What became of a script handed to <see cref="ScriptRunner.Execute"/>.</summary>
    public enum ScriptOutcome
    {
        /// <summary>It ran (whether or not its command changes anything: MAKE, MOVE and the HERO/ENEMY stubs do not).</summary>
        Ran,

        /// <summary>It failed <c>isScriptOkay</c> (or would not split into a script) and was ignored.</summary>
        Invalid,

        /// <summary>The same script ran too recently (its increment has not passed) and was ignored.</summary>
        Throttled,
    }

    /// <summary>A script's outcome and the changes it asks of the game, in order.</summary>
    public sealed class ScriptResult
    {
        internal ScriptResult(string script, ScriptOutcome outcome, ScriptLine line, IReadOnlyList<ScriptEffect> effects, ScriptView view)
        {
            Script = script;
            Outcome = outcome;
            Line = line;
            Effects = effects;
            View = view;
        }

        public string Script { get; }
        public ScriptOutcome Outcome { get; }
        public ScriptLine Line { get; }
        public IReadOnlyList<ScriptEffect> Effects { get; }

        /// <summary>The view once every effect is applied.</summary>
        public ScriptView View { get; }

        public bool Ran => Outcome == ScriptOutcome.Ran;

        public override string ToString() => $"{Outcome} \"{Script}\" ({Effects.Count} effects)";
    }

    /// <summary>
    /// Script.vb's <c>Execute</c> and its <c>cmd*</c> handlers as an engine-free executor. A script is parsed
    /// (<see cref="ScriptLine"/>), checked as <c>isScriptOkay</c> checks it (invalid ones are ignored), throttled, then
    /// run against a <see cref="ScriptView"/> of the game; the result lists the <see cref="ScriptEffect"/>s it asks for,
    /// which presentation applies. The runner itself holds only the script's own state, <c>scriptColor</c>
    /// (<see cref="Colour"/>) and <c>bScriptRunning</c> (<see cref="Running"/>).
    ///
    /// <list type="bullet">
    /// <item><c>CHANGE CAMERA|CAM X|Y p</c>: <c>+n</c>/<c>-n</c> moves eye and target by n, a bare number places both there.</item>
    /// <item><c>CHANGE LIGHT AMBIENT r,g,b</c> sets the ambient colour; <c>AMBIENT n</c> (signed or not) adds n to each channel.</item>
    /// <item><c>CHANGE LIGHT RED|R|GREEN|G|BLUE|B p</c>: <c>+n</c>/<c>-n</c> adds to that tint channel, a bare number sets it; 0-255.</item>
    /// <item><c>CHANGE TIME DAYPERIOD</c> MORNING, NIGHT, DAY, DUSK, NOON (day), DAWN and SUNRISE (morning), SUNSET (dusk), LIGHT (day), DARK (night).</item>
    /// <item><c>CHANGE SCRIPT COLOR</c> RED, GREEN, BLUE, MAGENTA/FUSCHIA; <c>CHANGE SCRIPT RUNNING ON|OFF</c>.</item>
    /// <item><c>CHANGE HERO|PLAYER|GOODGUY</c> HP/HEALTH/LIFE, X, Y, FACING, AI and <c>CHANGE ENEMY|BADGUY</c> HP, AI: the original's
    /// stubs (every body commented out), except <c>HERO AI STOP</c>, which clears <c>bScriptRunning</c>; <c>CHANGE CHAIN</c> has no handler.</item>
    /// <item><c>MAKE ENEMY|CHAIN</c> and <c>MOVE HERO|ENEMY|CHAIN</c>: validated, do nothing (their handlers are commented out).</item>
    /// <item><c>RUN NIGHT</c> runs <c>CHANGE TIME DAYPERIOD NIGHT</c>; <c>RUN HEAVEN</c> runs <c>CHANGE HERO AI JUMP</c> and
    /// <c>CHANGE HERO Y +1</c> (both stubs, so nothing changes). Any other RUN does nothing.</item>
    /// </list>
    ///
    /// Fixes (BIBLE D29-D32): LIGHT passes the check; AMBIENT r,g,b works whatever the red figure's width; a channel
    /// is held to 0-255 where <c>Color.FromArgb</c> or a <c>Byte</c> threw; <c>+n</c>/<c>-n</c> is always relative
    /// (the original's <c>isArithmetic</c> turned an out-of-range step into an assignment, and checked GREEN and BLUE
    /// against red); a parameter that is not a number changes nothing where the original threw; and the throttle is
    /// per script, where the original's one <c>lastEventCheck</c> swallowed RUN HEAVEN's second line.
    /// </summary>
    public sealed class ScriptRunner
    {
        /// <summary>Script.vb's starting <c>scriptColor</c>: <c>Color.FromArgb(0, 0, 255)</c>.</summary>
        public static readonly Rgb24 DefaultColour = new Rgb24(0, 0, 255);

        private readonly Dictionary<string, float> _lastRun = new Dictionary<string, float>(StringComparer.Ordinal);

        public ScriptRunner() : this(DefaultColour)
        {
        }

        public ScriptRunner(Rgb24 colour)
        {
            Colour = colour;
        }

        /// <summary>Script.vb's <c>scriptColor</c>, the scripter's text colour.</summary>
        public Rgb24 Colour { get; private set; }

        /// <summary>Script.vb's <c>bScriptRunning</c> (nothing in the original reads it; kept for the console).</summary>
        public bool Running { get; private set; }

        /// <summary>
        /// Runs <paramref name="script"/> at <paramref name="now"/> seconds against <paramref name="view"/>. It is ignored
        /// if invalid, or if the same script last ran no more than <paramref name="increment"/> seconds ago (Execute's
        /// <c>Time.Clock &gt; lastEventCheck + Increment</c>; the scripter's Enter passes 0, so the same script twice in
        /// one instant runs once).
        /// </summary>
        public ScriptResult Execute(string script, float now, ScriptView view, float increment = 0f)
        {
            var effects = new List<ScriptEffect>();
            var outcome = Run(script ?? string.Empty, now, increment, ref view, effects, out var line);
            return new ScriptResult(script ?? string.Empty, outcome, line, effects, view);
        }

        /// <summary>Runs <paramref name="script"/> against a <see cref="ScriptView.Default"/> view.</summary>
        public ScriptResult Execute(string script, float now) => Execute(script, now, ScriptView.Default);

        /// <summary>Forgets when each script last ran (a new session).</summary>
        public void ResetThrottle() => _lastRun.Clear();

        private ScriptOutcome Run(string script, float now, float increment, ref ScriptView view, List<ScriptEffect> effects, out ScriptLine line)
        {
            if (!ScriptLine.TryParse(script, out line) || !line.IsOkay)
            {
                return ScriptOutcome.Invalid;
            }

            var key = line.ToString();
            if (_lastRun.TryGetValue(key, out var last) && !(now > last + increment))
            {
                return ScriptOutcome.Throttled;
            }

            switch (line.Command)
            {
                case "CHANGE":
                    switch (line.Type)
                    {
                        case "HERO":
                        case "PLAYER":
                        case "GOODGUY":
                            ChangeHero(line, effects);
                            break;
                        case "ENEMY":
                        case "BADGUY":
                            break; // cmdChangeEnemy: HP and AI, both empty.
                        case "LIGHT":
                            ChangeLight(line, ref view, effects);
                            break;
                        case "TIME":
                            ChangeTime(line, effects);
                            break;
                        case "SCRIPT":
                            ChangeScript(line, effects);
                            break;
                        case "CAMERA":
                        case "CAM":
                            ChangeCamera(line, effects);
                            break;
                    }

                    break;

                case "MAKE":
                case "MOVE":
                    break; // Every MAKE and MOVE handler is commented out in Script.vb.

                case "RUN":
                    _lastRun[key] = now; // Execute stamps lastEventCheck after the nested Executes; stamp first so a RUN cannot recurse.
                    RunScript(line, now, increment, ref view, effects);
                    break;
            }

            _lastRun[key] = now;
            return ScriptOutcome.Ran;
        }

        private void ChangeHero(ScriptLine line, List<ScriptEffect> effects)
        {
            // HP/HEALTH/LIFE, X, Y, FACING and every AI state are commented out in cmdChangeHero; only AI STOP is live.
            if (line.Item == "AI" && Upper(line.Parameter) == "STOP")
            {
                SetRunning(false, effects);
            }
        }

        private static void ChangeCamera(ScriptLine line, List<ScriptEffect> effects)
        {
            ScriptAxis axis;
            switch (line.Item)
            {
                case "X": axis = ScriptAxis.X; break;
                case "Y": axis = ScriptAxis.Y; break;
                default: return;
            }

            if (!ScriptLine.TryNumber(line.Parameter, out var value))
            {
                return;
            }

            effects.Add(ScriptLine.IsArithmetic(line.Parameter)
                ? new CameraMoved(axis, value)
                : (ScriptEffect)new CameraPlaced(axis, value));
        }

        private static void ChangeLight(ScriptLine line, ref ScriptView view, List<ScriptEffect> effects)
        {
            switch (line.Item)
            {
                case "AMBIENT":
                    ChangeAmbient(line.Parameter, ref view, effects);
                    break;
                case "RED":
                case "R":
                    ChangeTint(TintChannel.Red, line.Parameter, ref view, effects);
                    break;
                case "GREEN":
                case "G":
                    ChangeTint(TintChannel.Green, line.Parameter, ref view, effects);
                    break;
                case "BLUE":
                case "B":
                    ChangeTint(TintChannel.Blue, line.Parameter, ref view, effects);
                    break;
            }
        }

        private static void ChangeAmbient(string parameter, ref ScriptView view, List<ScriptEffect> effects)
        {
            Rgb24 colour;
            if (parameter.IndexOf(',') >= 0)
            {
                // r,g,b: Int() of each, as Color.FromArgb(Int(arySplit(0)), ...).
                var split = parameter.Split(',');
                if (split.Length < 3
                    || !ScriptLine.TryNumber(split[0], out var r)
                    || !ScriptLine.TryNumber(split[1], out var g)
                    || !ScriptLine.TryNumber(split[2], out var b))
                {
                    return;
                }

                colour = new Rgb24(Floor(r), Floor(g), Floor(b));
            }
            else
            {
                // n: added to each channel, unless every channel would go below 0.
                if (!ScriptLine.TryNumber(parameter, out var n))
                {
                    return;
                }

                var a = view.Ambient;
                if (!(a.R + n > -1f || a.G + n > -1f || a.B + n > -1f))
                {
                    return;
                }

                colour = new Rgb24(Round(a.R + n), Round(a.G + n), Round(a.B + n));
            }

            view.Ambient = colour;
            effects.Add(new AmbientChanged(colour));
        }

        private static void ChangeTint(TintChannel channel, string parameter, ref ScriptView view, List<ScriptEffect> effects)
        {
            if (!ScriptLine.TryNumber(parameter, out var n))
            {
                return;
            }

            var value = ScriptLine.IsArithmetic(parameter) ? view.Tint(channel) + n : n;
            value = Calc.Clamp(value, 0f, LightingState.FullTint);
            view.SetTint(channel, value);
            effects.Add(new TintChanged(channel, value));
        }

        private static void ChangeTime(ScriptLine line, List<ScriptEffect> effects)
        {
            if (line.Item != "DAYPERIOD")
            {
                return;
            }

            DayPeriod period;
            switch (Upper(line.Parameter))
            {
                case "MORNING":
                case "DAWN":
                case "SUNRISE":
                    period = DayPeriod.Morning;
                    break;
                case "NIGHT":
                case "DARK":
                    period = DayPeriod.Night;
                    break;
                case "DAY":
                case "NOON":
                case "LIGHT":
                    period = DayPeriod.Day;
                    break;
                case "DUSK":
                case "SUNSET":
                    period = DayPeriod.Dusk;
                    break;
                default:
                    return;
            }

            effects.Add(new DayPeriodChanged(period));
        }

        private void ChangeScript(ScriptLine line, List<ScriptEffect> effects)
        {
            var parameter = Upper(line.Parameter);
            switch (line.Item)
            {
                case "COLOR":
                    Rgb24 colour;
                    switch (parameter)
                    {
                        case "RED": colour = new Rgb24(255, 0, 0); break;      // Color.Red
                        case "GREEN": colour = new Rgb24(0, 128, 0); break;    // Color.Green
                        case "BLUE": colour = new Rgb24(0, 0, 255); break;     // Color.Blue
                        case "MAGENTA":
                        case "FUSCHIA": colour = new Rgb24(255, 0, 255); break; // Color.Magenta
                        default: return;
                    }

                    Colour = colour;
                    effects.Add(new ScriptColourChanged(colour));
                    break;

                case "RUNNING":
                    switch (parameter)
                    {
                        case "ON": SetRunning(true, effects); break;
                        case "OFF": SetRunning(false, effects); break;
                    }

                    break;
            }
        }

        private void RunScript(ScriptLine line, float now, float increment, ref ScriptView view, List<ScriptEffect> effects)
        {
            switch (line.Type)
            {
                case "NIGHT":
                    Run("CHANGE TIME DAYPERIOD NIGHT", now, increment, ref view, effects, out _);
                    break;
                case "HEAVEN":
                    Run("CHANGE HERO AI JUMP", now, increment, ref view, effects, out _);
                    Run("CHANGE HERO Y +1", now, increment, ref view, effects, out _);
                    break;
            }
        }

        private void SetRunning(bool running, List<ScriptEffect> effects)
        {
            Running = running;
            effects.Add(new ScriptRunningChanged(running));
        }

        // Held to a byte's neighbourhood before the cast, so a huge figure cannot wrap round int.
        private static int Floor(float value) => (int)Math.Floor(Calc.Clamp(value, -1f, Rgb24.Full + 1f));

        private static int Round(float value) => (int)Math.Round(Calc.Clamp(value, -1f, Rgb24.Full + 1f));

        private static string Upper(string text) => text.ToUpperInvariant();
    }
}
