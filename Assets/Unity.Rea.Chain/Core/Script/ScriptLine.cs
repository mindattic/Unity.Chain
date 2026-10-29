using System;
using System.Globalization;

namespace MindAttic.Chain.Core
{
    /// <summary>
    /// One line of Script.vb's language ("Ryan's Scripting Language"): <c>Command Type Item Parameter</c>, split on
    /// single spaces as <c>divideScript</c> does. Four parts make a full script; two (<c>RUN NIGHT</c>) are a
    /// <c>RUN</c> of a hard-coded script. Command, Type and Item are matched without regard to case (the scripter
    /// types upper case from the key codes; its C P shortcut types <c>chain</c>); the Parameter is kept as typed.
    ///
    /// Fix (BIBLE D31): <c>divideScript</c> only wrote its module-level fields for four or two parts, so a line of any
    /// other length, or a two-part line that was not a RUN, ran with the previous script's leftover fields. Here such a
    /// line is simply invalid.
    /// </summary>
    public readonly struct ScriptLine
    {
        private ScriptLine(string command, string type, string item, string parameter, int parts)
        {
            Command = command;
            Type = type;
            Item = item;
            Parameter = parameter;
            Parts = parts;
        }

        /// <summary>The action (CHANGE, MAKE, MOVE, RUN), upper case.</summary>
        public string Command { get; }

        /// <summary>The object class (HERO, CAMERA, LIGHT, ...), or the RUN script's name, upper case.</summary>
        public string Type { get; }

        /// <summary>The specific object (X, AMBIENT, DAYPERIOD, ...), upper case; empty for a two-part RUN.</summary>
        public string Item { get; }

        /// <summary>The change to make, as typed (<c>+1</c>, <c>0,0,0</c>, <c>NIGHT</c>); empty for a two-part RUN.</summary>
        public string Parameter { get; }

        /// <summary>How many space-separated parts the line had (4, or 2 for a RUN).</summary>
        public int Parts { get; }

        /// <summary>
        /// Splits <paramref name="script"/> on spaces. Returns false (and a default line) unless it has four parts, or
        /// two with the command RUN.
        /// </summary>
        public static bool TryParse(string script, out ScriptLine line)
        {
            line = default;
            if (script == null)
            {
                return false;
            }

            var parts = script.Split(' ');
            if (parts.Length == 4)
            {
                line = new ScriptLine(Upper(parts[0]), Upper(parts[1]), Upper(parts[2]), parts[3], 4);
                return true;
            }

            if (parts.Length == 2 && Upper(parts[0]) == "RUN")
            {
                line = new ScriptLine("RUN", Upper(parts[1]), string.Empty, string.Empty, 2);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Script.vb's <c>isScriptOkay</c>, with Execute's <c>Or Command = "RUN"</c>: a RUN always passes; otherwise all
        /// four parts must be non-empty and the Type one its command accepts. CHANGE takes HERO/PLAYER/GOODGUY,
        /// ENEMY/BADGUY, CHAIN, LIGHT, TIME, SCRIPT and CAMERA/CAM; MAKE takes ENEMY and CHAIN; MOVE takes HERO, ENEMY
        /// and CHAIN. Fix (BIBLE D29): the original's list left out LIGHT, so every CHANGE LIGHT was thrown away and
        /// <c>cmdChangeLight</c> could never run.
        /// </summary>
        public bool IsOkay
        {
            get
            {
                if (Command == "RUN")
                {
                    return true;
                }

                if (Parts != 4 || string.IsNullOrEmpty(Command) || string.IsNullOrEmpty(Type)
                    || string.IsNullOrEmpty(Item) || string.IsNullOrEmpty(Parameter))
                {
                    return false;
                }

                switch (Command)
                {
                    case "CHANGE":
                        switch (Type)
                        {
                            case "HERO":
                            case "PLAYER":
                            case "GOODGUY":
                            case "ENEMY":
                            case "BADGUY":
                            case "CHAIN":
                            case "LIGHT":
                            case "TIME":
                            case "SCRIPT":
                            case "CAMERA":
                            case "CAM":
                                return true;
                            default:
                                return false;
                        }

                    case "MAKE":
                        return Type == "ENEMY" || Type == "CHAIN";

                    case "MOVE":
                        return Type == "HERO" || Type == "ENEMY" || Type == "CHAIN";

                    default:
                        return false;
                }
            }
        }

        /// <summary>The line as it would be typed (Command Type Item Parameter, or Command Type for a two-part RUN).</summary>
        public override string ToString() => Parts == 2 ? $"{Command} {Type}" : $"{Command} {Type} {Item} {Parameter}";

        /// <summary>
        /// Reads <paramref name="text"/> as a number the way VB's <c>CSng</c> read a typed parameter (a leading + or -,
        /// a decimal point), culture-invariant. Returns false for anything else, where the original threw out of
        /// <c>Execute</c> and took the game down (BIBLE D31).
        /// </summary>
        public static bool TryNumber(string text, out float value)
        {
            value = 0f;
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                || float.IsNaN(value) || float.IsInfinity(value))
            {
                value = 0f;
                return false;
            }

            return true;
        }

        /// <summary>Script.vb's <c>isArithmetic</c> sign test: a parameter starting with + or - is relative.</summary>
        public static bool IsArithmetic(string parameter) =>
            !string.IsNullOrEmpty(parameter) && (parameter[0] == '+' || parameter[0] == '-');

        private static string Upper(string part) => part.ToUpperInvariant();
    }
}
