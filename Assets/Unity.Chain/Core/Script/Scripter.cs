using System;

namespace MindAttic.Chain.Core
{
    /// <summary>
    /// The keys Form1.vb's scripter mode reads, named after the WinForms <c>Keys</c> it switches on: the letters A to Z,
    /// the top-row digits D0 to D9, Space, the Oem punctuation keys, and Back, Delete, Enter, Insert, Escape and F11.
    /// Anything else is <see cref="None"/> (the original's "Null Keys" and its Case Else, BIBLE D33).
    /// </summary>
    public enum ScripterKey
    {
        None,
        A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
        D0, D1, D2, D3, D4, D5, D6, D7, D8, D9,
        Space,
        /// <summary>The <c>;:</c> key: types <c>:</c> (the first of the original's two OemSemicolon cases wins).</summary>
        OemSemicolon,
        /// <summary>The <c>\|</c> key (Oem5): types <c>|</c>.</summary>
        OemPipe,
        OemPeriod,
        /// <summary>The 102nd key (<c>&lt;&gt;</c> or <c>\</c> beside the left shift, Oem102): types <c>/</c>.</summary>
        OemBackslash,
        /// <summary>The <c>/?</c> key: types <c>?</c>.</summary>
        OemQuestion,
        /// <summary>The <c>=+</c> key: types <c>+</c>.</summary>
        Oemplus,
        OemMinus,
        Oemcomma,
        /// <summary>The <c>'"</c> key: types <c>"</c>.</summary>
        OemQuotes,
        /// <summary>The grave/tilde key: opens the scripter, and closes it again (its second case, typing <c>~</c>, is unreachable).</summary>
        Oemtilde,
        Back,
        Delete,
        Enter,
        Insert,
        Escape,
        F11,
    }

    /// <summary>
    /// Form1.vb's scripter mode (<c>OnKeyDown</c> while <c>bScripter</c>) and Script.vb's <c>showScripter</c>, engine-free
    /// (U.CHN-T61). The grave key opens it (<see cref="IsOpen"/>, Root.vb's <c>bScripter</c>); while open each key edits
    /// <see cref="Text"/> (Root.vb's <c>theScript</c>) as the original's Select Case does:
    /// <list type="bullet">
    /// <item>C on an empty line types <c>CHANGE </c>; C, H, P, S and T right after <c>CHANGE </c> type <c>CHANGE CAMERA </c>,
    /// <c>CHANGE HERO </c>, <c>CHANGE chain </c>, <c>CHANGE SCRIPT </c> and <c>CHANGE TIME </c>; otherwise they type their letter.</item>
    /// <item>Every other letter types itself, upper case (the key code's name); Space, the digits and the punctuation of
    /// <see cref="ScripterKey"/> type their character.</item>
    /// <item>Back deletes the last character, Delete clears the line.</item>
    /// <item>Enter hands the line back to run (<see cref="Press"/> returns it), remembers it (<c>lastScript</c>), clears it and
    /// closes; Insert puts the last script back on the line.</item>
    /// <item>Escape, F11 and the grave key close it, keeping the line.</item>
    /// </list>
    /// <see cref="Tick"/> blinks the I-beam every half second (<c>lastBeamCheck + 0.5</c>) and <see cref="Line"/> is what
    /// showScripter writes: <c>/ &gt; text|</c> with the beam, <c>\ &gt; text </c> without. The script language itself is
    /// <see cref="ScriptRunner"/> (<see cref="Runner"/>, whose colour the line is drawn in).
    ///
    /// Fix (BIBLE D33): the original's Case Else typed any other key's name (F5, NumPad7, Return's siblings, Capital), so
    /// a stray key wrote a word into the script; here only the keys it lists type, as its "Write the Letter" comment meant.
    /// </summary>
    public sealed class Scripter
    {
        /// <summary>Script.vb's showScripter: the beam flips once more than half a second has passed.</summary>
        public const float DefaultBeamSeconds = 0.5f;

        private float _lastBeamCheck;

        public Scripter() : this(new ScriptRunner(), DefaultBeamSeconds)
        {
        }

        public Scripter(ScriptRunner runner, float beamSeconds = DefaultBeamSeconds)
        {
            Runner = runner ?? throw new ArgumentNullException(nameof(runner));
            BeamSeconds = beamSeconds;
        }

        /// <summary>What runs the lines typed here (and holds the script colour and <c>bScriptRunning</c>).</summary>
        public ScriptRunner Runner { get; }

        /// <summary>How long the I-beam stays on or off, seconds.</summary>
        public float BeamSeconds { get; }

        /// <summary>Root.vb's <c>bScripter</c>: the console is open and the keys type into it.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>Root.vb's <c>theScript</c>: the line being typed.</summary>
        public string Text { get; private set; } = string.Empty;

        /// <summary>Root.vb's <c>lastScript</c>: the last line Enter ran, which Insert recalls.</summary>
        public string LastScript { get; private set; } = string.Empty;

        /// <summary>Script.vb's <c>IBeam</c>: whether the cursor bar is showing (it starts off).</summary>
        public bool Beam { get; private set; }

        /// <summary>The line showScripter writes: <c>/ &gt; </c>, the text and the bar, or <c>\ &gt; </c>, the text and a space.</summary>
        public string Line => Beam ? "/ > " + Text + "|" : "\\ > " + Text + " ";

        /// <summary>The colour the line is drawn in (Script.vb's <c>scriptColor</c>, which <c>CHANGE SCRIPT COLOR</c> sets).</summary>
        public Rgb24 Colour => Runner.Colour;

        /// <summary>Raised whenever it opens or closes, with whether it is now open.</summary>
        public event Action<bool> OpenChanged;

        /// <summary>Opens it (the grave key outside scripter mode). The line is left as it was.</summary>
        public void Open() => SetOpen(true);

        /// <summary>Closes it (Escape, F11, the grave key). The line is left as it was.</summary>
        public void Close() => SetOpen(false);

        /// <summary>The grave key: opens it, or closes it again. Returns whether it is open.</summary>
        public bool Toggle()
        {
            SetOpen(!IsOpen);
            return IsOpen;
        }

        /// <summary>
        /// One key down. Closed, only the grave key does anything (it opens the console). Open, the key edits the line
        /// as Form1.vb's scripter mode does; Enter returns the line to run (and remembers, clears and closes it), every
        /// other key returns null.
        /// </summary>
        public string Press(ScripterKey key)
        {
            if (!IsOpen)
            {
                if (key == ScripterKey.Oemtilde)
                {
                    Open();
                }

                return null;
            }

            switch (key)
            {
                case ScripterKey.C:
                    Text = Text.Length == 0 ? "CHANGE " : Text == "CHANGE " ? "CHANGE CAMERA " : Text + "C";
                    return null;
                case ScripterKey.H:
                    Shortcut("CHANGE HERO ", "H");
                    return null;
                case ScripterKey.P:
                    Shortcut("CHANGE chain ", "P");
                    return null;
                case ScripterKey.S:
                    Shortcut("CHANGE SCRIPT ", "S");
                    return null;
                case ScripterKey.T:
                    Shortcut("CHANGE TIME ", "T");
                    return null;

                case ScripterKey.Escape:
                case ScripterKey.F11:
                case ScripterKey.Oemtilde:
                    Close();
                    return null;

                case ScripterKey.Insert:
                    Text = LastScript;
                    return null;

                case ScripterKey.Delete:
                    Text = string.Empty;
                    return null;

                case ScripterKey.Back:
                    if (Text.Length > 0)
                    {
                        Text = Text.Substring(0, Text.Length - 1);
                    }

                    return null;

                case ScripterKey.Enter:
                    var script = Text;
                    LastScript = script;
                    Text = string.Empty;
                    Close();
                    return script;

                case ScripterKey.None:
                    return null;
            }

            var typed = Character(key);
            if (typed != null)
            {
                Text += typed;
            }

            return null;
        }

        /// <summary>Types <paramref name="text"/> one key at a time, as if each character's key were pressed (tests, a pasted line).</summary>
        public string Type(string text)
        {
            string ran = null;
            foreach (var c in text ?? string.Empty)
            {
                ran = Press(KeyFor(c)) ?? ran;
            }

            return ran;
        }

        /// <summary>
        /// Runs <paramref name="script"/> at <paramref name="now"/> seconds against <paramref name="view"/> (Form1.vb's Enter:
        /// <c>Script.Execute(theScript)</c>, increment 0).
        /// </summary>
        public ScriptResult Run(string script, float now, ScriptView view) => Runner.Execute(script, now, view);

        /// <summary>
        /// showScripter's beam at <paramref name="now"/> seconds: once more than <see cref="BeamSeconds"/> have passed since
        /// the last flip it flips again. Call it each frame the console is drawn. Returns whether the bar shows.
        /// </summary>
        public bool Tick(float now)
        {
            if (now > _lastBeamCheck + BeamSeconds)
            {
                Beam = !Beam;
                _lastBeamCheck = now;
            }

            return Beam;
        }

        /// <summary>The character a key types (the letters upper case, as the key code's name), or null if it types none.</summary>
        public static string Character(ScripterKey key)
        {
            if (key >= ScripterKey.A && key <= ScripterKey.Z)
            {
                return ((char)('A' + (key - ScripterKey.A))).ToString();
            }

            if (key >= ScripterKey.D0 && key <= ScripterKey.D9)
            {
                return ((char)('0' + (key - ScripterKey.D0))).ToString();
            }

            switch (key)
            {
                case ScripterKey.Space: return " ";
                case ScripterKey.OemSemicolon: return ":";
                case ScripterKey.OemPipe: return "|";
                case ScripterKey.OemPeriod: return ".";
                case ScripterKey.OemBackslash: return "/";
                case ScripterKey.OemQuestion: return "?";
                case ScripterKey.Oemplus: return "+";
                case ScripterKey.OemMinus: return "-";
                case ScripterKey.Oemcomma: return ",";
                case ScripterKey.OemQuotes: return "\"";
                default: return null;
            }
        }

        /// <summary>The key that types <paramref name="c"/> (letters either case), or <see cref="ScripterKey.None"/>.</summary>
        public static ScripterKey KeyFor(char c)
        {
            if (c >= 'a' && c <= 'z')
            {
                c = (char)(c - 'a' + 'A');
            }

            if (c >= 'A' && c <= 'Z')
            {
                return ScripterKey.A + (c - 'A');
            }

            if (c >= '0' && c <= '9')
            {
                return ScripterKey.D0 + (c - '0');
            }

            switch (c)
            {
                case ' ': return ScripterKey.Space;
                case ':': return ScripterKey.OemSemicolon;
                case '|': return ScripterKey.OemPipe;
                case '.': return ScripterKey.OemPeriod;
                case '/': return ScripterKey.OemBackslash;
                case '?': return ScripterKey.OemQuestion;
                case '+': return ScripterKey.Oemplus;
                case '-': return ScripterKey.OemMinus;
                case ',': return ScripterKey.Oemcomma;
                case '"': return ScripterKey.OemQuotes;
                case '\n':
                case '\r': return ScripterKey.Enter;
                default: return ScripterKey.None;
            }
        }

        private void Shortcut(string expansion, string letter) =>
            Text = Text == "CHANGE " ? expansion : Text + letter;

        private void SetOpen(bool open)
        {
            if (IsOpen == open)
            {
                return;
            }

            IsOpen = open;
            OpenChanged?.Invoke(open);
        }
    }
}
