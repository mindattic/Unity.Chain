using System.Globalization;
using UnityEngine;

namespace MindAttic.Chain
{
    /// <summary>
    /// The HUD's strings, in Engine.vb's <c>renderDisplay</c> wording: an HP bar of one
    /// <c>|</c> per hit point, the score, the grappled enemy's HP, and the pause banner. Pure so
    /// it is unit-testable; <see cref="GameHud"/> draws them.
    /// </summary>
    public static class HudText
    {
        public const string Paused = "- PAUSE -";
        public const string GameOver = "GAME OVER";
        public const string Restart = "Click to play again";

        public static string Hp(int hp) => "HP : " + new string('|', Mathf.Max(0, hp));

        public static string Score(int score) => "Score : " + score.ToString(CultureInfo.InvariantCulture);

        public static string Grappled(float hp) =>
            "Grappled: " + Mathf.Max(0f, hp).ToString("0.##", CultureInfo.InvariantCulture);
    }
}
