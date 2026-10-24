using System.Globalization;
using MindAttic.Chain.Core;
using UnityEngine;

namespace MindAttic.Chain
{
    /// <summary>
    /// The HUD's strings, in Engine.vb's <c>renderDisplay</c> and Gui.vb's <c>renderFPS</c> wording: an HP bar of
    /// one <c>|</c> per hit point, the score, the grappled enemy's HP, the <c>Res</c>/<c>Grav</c>/<c>Spring</c>
    /// readout, the game speed (<c>FormatNumber(gameSpeed, 2)</c>), the frame rate and the pause banner. Pure so
    /// it is unit-testable; <see cref="GameHud"/> draws them.
    /// </summary>
    public static class HudText
    {
        public const string Paused = "- PAUSE -";
        public const string GameOver = "GAME OVER";

        public static string Hp(int hp) => "HP : " + new string('|', Mathf.Max(0, hp));

        public static string Score(int score) => "Score : " + score.ToString(CultureInfo.InvariantCulture);

        public static string Grappled(float hp) =>
            "Grappled: " + Mathf.Max(0f, hp).ToString("0.##", CultureInfo.InvariantCulture);

        /// <summary>
        /// <c>"Res " &amp; resistance &amp; vbNewLine &amp; "Grav " &amp; gravity.Y &amp; vbNewLine &amp; "Spring " &amp; springK</c>: each
        /// figure as VB writes a Single (its shortest form; 6 significant digits here, so the float round trip through the port's units never shows as 0.09999999).
        /// </summary>
        public static string Readout(ChainReadout chain) =>
            "Res " + Single(chain.Resistance) + "\nGrav " + Single(chain.Gravity) + "\nSpring " + Single(chain.Spring);

        /// <summary><c>FormatNumber(gameSpeed, 2)</c>: two decimals ("1.00").</summary>
        public static string GameSpeed(float gameSpeed) => gameSpeed.ToString("N2", CultureInfo.InvariantCulture);

        /// <summary><c>iFrameRate.ToString &amp; " fps"</c>.</summary>
        public static string Fps(int frameRate) => frameRate.ToString(CultureInfo.InvariantCulture) + " fps";

        private static string Single(float value) => value.ToString("G6", CultureInfo.InvariantCulture);
    }
}
