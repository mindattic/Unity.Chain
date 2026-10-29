using System;
using System.Globalization;

namespace MindAttic.Chain.Core
{
    /// <summary>
    /// Gui.vb's <c>updateClock</c>: <c>timerSec</c> counts up once a second and rolls over into <c>timerMin</c> at 60,
    /// written <c>timerMin &amp; ":0" &amp; timerSec</c> below 10 seconds and <c>timerMin &amp; ":" &amp; timerSec</c> above, so
    /// the minutes are never padded and grow without limit (<c>0:05</c>, <c>1:00</c>, <c>75:30</c>). Root.vb's
    /// <c>Reset</c> sets both back to 0. The port reads it off <see cref="GameWorld.RoundTime"/>, the round's
    /// seconds of play, so only whole seconds count.
    /// </summary>
    public static class RoundClock
    {
        /// <summary>Whole seconds of <paramref name="roundTime"/> (0 below zero or for NaN).</summary>
        public static long WholeSeconds(float roundTime) =>
            roundTime > 0f ? (long)Math.Floor(roundTime) : 0L;

        /// <summary><c>timerMin</c>: whole minutes.</summary>
        public static long Minutes(float roundTime) => WholeSeconds(roundTime) / 60L;

        /// <summary><c>timerSec</c>: 0 to 59.</summary>
        public static int Seconds(float roundTime) => (int)(WholeSeconds(roundTime) % 60L);

        /// <summary>The clock as updateClock writes it: <c>m:ss</c>.</summary>
        public static string Format(float roundTime) =>
            Minutes(roundTime).ToString(CultureInfo.InvariantCulture) + ":" +
            Seconds(roundTime).ToString("00", CultureInfo.InvariantCulture);
    }
}
