using System;

namespace MindAttic.Chain.Core
{
    /// <summary>A camera axis a script moves (Script.vb's <c>CHANGE CAMERA X</c> / <c>Y</c>).</summary>
    public enum ScriptAxis
    {
        X,
        Y,
    }

    /// <summary>A colour as 0-255 channels, as <c>System.Drawing.Color</c> held Script.vb's ambient and script colours.</summary>
    public readonly struct Rgb24 : IEquatable<Rgb24>
    {
        public const int Full = 255;

        public static readonly Rgb24 White = new Rgb24(Full, Full, Full);

        public Rgb24(int r, int g, int b)
        {
            R = Calc.Clamp(r, 0, Full);
            G = Calc.Clamp(g, 0, Full);
            B = Calc.Clamp(b, 0, Full);
        }

        public int R { get; }
        public int G { get; }
        public int B { get; }

        /// <summary>The colour as 0-1 channels.</summary>
        public Rgb ToRgb() => new Rgb(R / (float)Full, G / (float)Full, B / (float)Full);

        public bool Equals(Rgb24 other) => R == other.R && G == other.G && B == other.B;
        public override bool Equals(object obj) => obj is Rgb24 other && Equals(other);
        public override int GetHashCode() => (R * 397 ^ G) * 397 ^ B;
        public override string ToString() => $"{R},{G},{B}";
    }

    /// <summary>
    /// What the game looked like when a script ran: the figures Script.vb's arithmetic reads before it changes them.
    /// The runner works on a copy, so a RUN's lines see each other's changes; the game is never touched.
    /// </summary>
    public struct ScriptView
    {
        /// <summary>Lights.vb's <c>AmbientColor</c>, 0-255 each.</summary>
        public Rgb24 Ambient;

        /// <summary>Root.vb's material tint <c>r</c>, <c>g</c>, <c>b</c>, 0-255 each (<see cref="LightingState"/>).</summary>
        public float Red;
        public float Green;
        public float Blue;

        /// <summary>A view of a game at its start: white ambient, full tint.</summary>
        public static ScriptView Default => new ScriptView
        {
            Ambient = Rgb24.White,
            Red = LightingState.FullTint,
            Green = LightingState.FullTint,
            Blue = LightingState.FullTint,
        };

        /// <summary>The view of a running game: its ambient and its lighting state's tint.</summary>
        public static ScriptView Of(Rgb24 ambient, LightingState lighting) => new ScriptView
        {
            Ambient = ambient,
            Red = lighting?.Red ?? LightingState.FullTint,
            Green = lighting?.Green ?? LightingState.FullTint,
            Blue = lighting?.Blue ?? LightingState.FullTint,
        };

        public float Tint(TintChannel channel) => channel == TintChannel.Red ? Red : channel == TintChannel.Green ? Green : Blue;

        internal void SetTint(TintChannel channel, float value)
        {
            switch (channel)
            {
                case TintChannel.Red: Red = value; break;
                case TintChannel.Green: Green = value; break;
                default: Blue = value; break;
            }
        }
    }

    /// <summary>
    /// A change a script asks of the game. The runner only reports these; presentation (the scripter console,
    /// U.CHN-T61) applies each to the camera rig, the scene light and the HUD, as the rest of the game plays events.
    /// </summary>
    public abstract class ScriptEffect
    {
    }

    /// <summary><c>CHANGE CAMERA X|Y +n|-n</c>: eye and target both move by <see cref="Delta"/>, in the original's screen units.</summary>
    public sealed class CameraMoved : ScriptEffect
    {
        public CameraMoved(ScriptAxis axis, float delta)
        {
            Axis = axis;
            Delta = delta;
        }

        public ScriptAxis Axis { get; }
        public float Delta { get; }

        public override string ToString() => $"CameraMoved {Axis} {Delta:+0.###;-0.###;0}";
    }

    /// <summary><c>CHANGE CAMERA X|Y n</c>: eye and target both set to <see cref="Value"/> on that axis, in the original's screen units.</summary>
    public sealed class CameraPlaced : ScriptEffect
    {
        public CameraPlaced(ScriptAxis axis, float value)
        {
            Axis = axis;
            Value = value;
        }

        public ScriptAxis Axis { get; }
        public float Value { get; }

        public override string ToString() => $"CameraPlaced {Axis} {Value:0.###}";
    }

    /// <summary><c>CHANGE LIGHT AMBIENT</c>: the ambient colour is now <see cref="Colour"/>.</summary>
    public sealed class AmbientChanged : ScriptEffect
    {
        public AmbientChanged(Rgb24 colour)
        {
            Colour = colour;
        }

        public Rgb24 Colour { get; }

        public override string ToString() => $"AmbientChanged {Colour}";
    }

    /// <summary><c>CHANGE LIGHT RED|GREEN|BLUE</c>: that tint channel is now <see cref="Value"/> (0-255).</summary>
    public sealed class TintChanged : ScriptEffect
    {
        public TintChanged(TintChannel channel, float value)
        {
            Channel = channel;
            Value = value;
        }

        public TintChannel Channel { get; }
        public float Value { get; }

        public override string ToString() => $"TintChanged {Channel} {Value:0.###}";
    }

    /// <summary><c>CHANGE TIME DAYPERIOD</c> (and <c>RUN NIGHT</c>): the day jumps to <see cref="Period"/> (<see cref="LightingState.SetPeriod"/>).</summary>
    public sealed class DayPeriodChanged : ScriptEffect
    {
        public DayPeriodChanged(DayPeriod period)
        {
            Period = period;
        }

        public DayPeriod Period { get; }

        public override string ToString() => $"DayPeriodChanged {Period}";
    }

    /// <summary><c>CHANGE SCRIPT COLOR</c>: the scripter's text is drawn in <see cref="Colour"/> from now on.</summary>
    public sealed class ScriptColourChanged : ScriptEffect
    {
        public ScriptColourChanged(Rgb24 colour)
        {
            Colour = colour;
        }

        public Rgb24 Colour { get; }

        public override string ToString() => $"ScriptColourChanged {Colour}";
    }

    /// <summary><c>CHANGE SCRIPT RUNNING ON|OFF</c> (and <c>CHANGE HERO AI STOP</c>): Script.vb's <c>bScriptRunning</c>.</summary>
    public sealed class ScriptRunningChanged : ScriptEffect
    {
        public ScriptRunningChanged(bool running)
        {
            Running = running;
        }

        public bool Running { get; }

        public override string ToString() => $"ScriptRunningChanged {Running}";
    }
}
