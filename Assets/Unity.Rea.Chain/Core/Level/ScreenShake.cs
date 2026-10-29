using System;

namespace MindAttic.Chain.Core
{
    /// <summary>
    /// Root.vb's <c>shakeScreen</c>, which Form1.vb's 0 key calls with 0.02, as a pure rule. It toggles: on one call
    /// the camera's look target jumps right by two random draws of 0..<see cref="Amount"/> and down by two more; on
    /// the next it is put back on the camera (<c>target = cam</c>). Its flag (<c>shake</c>) starts <c>False</c>, so
    /// the first press restores (nothing moves) and the second shakes. <c>Reset</c> puts the target back at the origin
    /// but leaves the flag as it was (<see cref="Recenter"/>).
    ///
    /// A fix (BIBLE D19): the original's draws were <c>getRndNum(0, 0.02)</c>, <c>Random.Next(0, 1.02 + 1)</c> on
    /// integers, which is always 0, so the screen never moved; here each draw is a real random 0..<see cref="Amount"/>.
    /// The offset is presentation only; the camera rig adds it on top of its framing.
    /// </summary>
    public sealed class ScreenShake
    {
        private readonly Random _random;
        private float _amount;

        /// <param name="amount">The most one draw moves the view, world units (0.02 of the original's screen).</param>
        /// <param name="random">The draws; a fresh generator when null.</param>
        public ScreenShake(float amount, Random random = null)
        {
            Amount = amount;
            _random = random ?? new Random();
        }

        /// <summary>The most one draw moves the view, world units. Never negative.</summary>
        public float Amount
        {
            get => _amount;
            set => _amount = Calc.Max(0f, value);
        }

        /// <summary>Root.vb's <c>shake</c>: true when the next call shakes, false when it restores.</summary>
        public bool Armed { get; private set; }

        /// <summary>How far the look target is off the camera now: x right, y down (negative), world units.</summary>
        public Vec2 Offset { get; private set; }

        /// <summary>One press of 0: shakes if armed, otherwise restores and arms. Returns the new offset.</summary>
        public Vec2 Shake()
        {
            if (Armed)
            {
                var x = Draw() + Draw();
                var y = Draw() + Draw();
                Offset += new Vec2(x, -y);
                Armed = false;
            }
            else
            {
                Offset = Vec2.Zero;
                Armed = true;
            }

            return Offset;
        }

        /// <summary>Root.vb's <c>Reset</c>: the target back on the camera; the flag is left as it was.</summary>
        public void Recenter() => Offset = Vec2.Zero;

        private float Draw() => (float)_random.NextDouble() * _amount;
    }
}
