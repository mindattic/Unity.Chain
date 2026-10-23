using UnityEngine;

namespace MindAttic.Chain
{
    /// <summary>
    /// The heart pickup's drift (Item.vb): it floats down at <see cref="Settings.FloatSpeed"/> with a
    /// left/right sway whose acceleration is <see cref="Settings.FloatAcceleration"/>, and wraps to the
    /// top of the play area when it leaves the bottom. Item.vb's figures (floatSPD 0.002, floatAcc
    /// 0.00001) are per 60 Hz frame; here they are fractions of the play area's height (fall) and width
    /// (sway) per reference frame, so the drift scales with the level. The sway accelerates one way
    /// until its speed reaches the fall speed, then accelerates the other (an assumption: the
    /// original source is not in this repo, only the constants and the shape of the motion).
    /// Pure and engine-light so it is unit-testable.
    /// </summary>
    public sealed class HeartFloat
    {
        /// <summary>Item.vb steps once per frame at this rate.</summary>
        public const float ReferenceFrameRate = 60f;

        public struct Settings
        {
            /// <summary>Fall per reference frame, as a fraction of the play area's height (floatSPD).</summary>
            public float FloatSpeed;
            /// <summary>Sway speed change per reference frame, as a fraction of the play area's width (floatAcc).</summary>
            public float FloatAcceleration;

            public static Settings Default => new Settings { FloatSpeed = 0.002f, FloatAcceleration = 0.00001f };
        }

        public HeartFloat() : this(Settings.Default)
        {
        }

        public HeartFloat(Settings settings)
        {
            Config = settings;
            _swayDirection = 1f;
        }

        public Settings Config;

        private float _swayVelocity; // fraction of width per reference frame
        private float _swayDirection;

        /// <summary>Sway speed, fraction of play width per reference frame (sign = direction).</summary>
        public float SwayVelocity => _swayVelocity;

        /// <summary>Advances <paramref name="position"/> by <paramref name="deltaTime"/> inside <paramref name="area"/>. Returns true if it wrapped to the top.</summary>
        public bool Step(ref Vector2 position, Rect area, float deltaTime)
        {
            var frames = Mathf.Max(0f, deltaTime) * ReferenceFrameRate;

            _swayVelocity += _swayDirection * Config.FloatAcceleration * frames;
            var limit = Mathf.Abs(Config.FloatSpeed);
            if (limit > 0f && Mathf.Abs(_swayVelocity) >= limit)
            {
                _swayVelocity = Mathf.Sign(_swayVelocity) * limit;
                _swayDirection = -Mathf.Sign(_swayVelocity);
            }

            position.x += _swayVelocity * area.width * frames;
            position.y -= Config.FloatSpeed * area.height * frames;
            position.x = Mathf.Clamp(position.x, area.xMin, area.xMax);

            if (position.y < area.yMin)
            {
                position.y = area.yMax;
                return true;
            }

            return false;
        }
    }
}
