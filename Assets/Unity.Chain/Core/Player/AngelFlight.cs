namespace MindAttic.Chain.Core
{
    public enum Facing
    {
        Left,
        Right,
    }

    /// <summary>
    /// Pure-C# flight model for the angel, ported from Angel.vb's <c>renderAngel</c> (2005):
    /// position moves by raw mouse delta times a sensitivity ratio, the sprite faces the way the
    /// mouse last moved horizontally, a small hover bob rides on top of the flight position,
    /// the wing-flap frames loop at a fixed rate, and the body is clamped to a play-space rect.
    /// Kept free of MonoBehaviour so it is unit-testable; the angel controller feeds it
    /// input and applies the result to a Transform and SpriteRenderer.
    ///
    /// Differences from the original, on purpose:
    /// - Everything is <c>dt</c>-scaled (hover and flap rates are per-second, not per-frame). The hover bob is
    ///   Angel.vb's triangle ping-pong (0 up to +hoverMax, down to -hoverMax and back, hoverRate a frame),
    ///   timed so each 60 fps step lands on the original's per-frame value (U.CHN-T85).
    /// - Mouse Y is not inverted: DirectInput reported Y-down deltas, the Input System reports Y-up.
    /// - The bob is a render offset only: it never feeds back into <see cref="Position"/>, and
    ///   the bounds clamp applies to the flight position, matching Angel.vb (angelOffset was
    ///   added at draw time, after checkBounds).
    /// </summary>
    public sealed class AngelFlight
    {
        public readonly struct Settings
        {
            /// <summary>World units moved per unit of mouse delta (Angel.vb's Ratio3D).</summary>
            public readonly float Sensitivity;
            /// <summary>Play-space rect the flight position is clamped to.</summary>
            public readonly Rect2 Bounds;
            /// <summary>Peak hover bob displacement, world units (Angel.vb's hoverMax).</summary>
            public readonly float HoverAmplitude;
            /// <summary>Seconds for one full up-and-down hover cycle.</summary>
            public readonly float HoverPeriod;
            /// <summary>Seconds each wing-flap frame is shown (Angel.vb's flapRate).</summary>
            public readonly float FrameDuration;
            /// <summary>Number of frames in the flap loop (Angel.vb had 4 per direction).</summary>
            public readonly int FrameCount;
            /// <summary>Hand socket offset from the rendered body when facing right; mirrored
            /// on X when facing left (Angel.vb's linkOffset shift). Link 0 of the chain anchors here.</summary>
            public readonly Vec2 HandOffset;

            public Settings(
                float sensitivity,
                Rect2 bounds,
                float hoverAmplitude,
                float hoverPeriod,
                float frameDuration,
                int frameCount,
                Vec2 handOffset)
            {
                Sensitivity = sensitivity;
                Bounds = bounds;
                HoverAmplitude = hoverAmplitude;
                HoverPeriod = hoverPeriod;
                FrameDuration = frameDuration;
                FrameCount = frameCount;
                HandOffset = handOffset;
            }

            public static Settings Default => new Settings(
                sensitivity: 0.01f,
                bounds: new Rect2(-8f, -4.5f, 16f, 9f),
                hoverAmplitude: 0.08f,
                hoverPeriod: 1.6f,
                frameDuration: 0.1f,
                frameCount: 4,
                handOffset: new Vec2(0.25f, -0.1f));

            public Settings WithBounds(Rect2 bounds) => new Settings(
                Sensitivity, bounds, HoverAmplitude, HoverPeriod, FrameDuration, FrameCount, HandOffset);
        }

        public Settings Config;

        public Vec2 Position { get; private set; }
        public Facing Facing { get; private set; } = Facing.Right;
        public int Frame { get; private set; }
        public float HoverOffset { get; private set; }

        private float _hoverTime;
        private float _frameTime;

        public AngelFlight(Vec2 start, Settings settings)
        {
            Config = settings;
            Position = Clamp(start);
        }

        /// <summary>Where the body is drawn: flight position plus the hover bob.</summary>
        public Vec2 RenderPosition => Position + new Vec2(0f, HoverOffset);

        /// <summary>Where chain link 0 attaches: the hand offset, mirrored by facing.</summary>
        public Vec2 HandSocket
        {
            get
            {
                var offset = Config.HandOffset;
                if (Facing == Facing.Left)
                {
                    offset = offset.WithX(-offset.X);
                }

                return RenderPosition + offset;
            }
        }

        public void Teleport(Vec2 position) => Position = Clamp(position);

        public void Step(Vec2 mouseDelta, float deltaTime)
        {
            // Facing follows the last non-zero horizontal mouse motion; a still mouse keeps it.
            if (mouseDelta.X > 0f)
            {
                Facing = Facing.Right;
            }
            else if (mouseDelta.X < 0f)
            {
                Facing = Facing.Left;
            }

            Position = Clamp(Position + mouseDelta * Config.Sensitivity);

            if (Config.HoverPeriod > 0f)
            {
                _hoverTime = (_hoverTime + deltaTime) % Config.HoverPeriod;
                HoverOffset = Config.HoverAmplitude * Triangle(_hoverTime / Config.HoverPeriod);
            }
            else
            {
                HoverOffset = 0f;
            }

            if (Config.FrameCount > 1 && Config.FrameDuration > 0f)
            {
                _frameTime += deltaTime;
                while (_frameTime >= Config.FrameDuration)
                {
                    _frameTime -= Config.FrameDuration;
                    Frame = (Frame + 1) % Config.FrameCount;
                }
            }
            else
            {
                Frame = 0;
            }
        }

        /// <summary>
        /// Angel.vb's hover: from 0 it climbs to 1 at a quarter of the cycle, falls to -1 at three quarters and
        /// climbs back to 0, in straight lines (hoverCount += / -= hoverRate a frame between -hoverMax and hoverMax).
        /// </summary>
        public static float Triangle(float phase)
        {
            phase -= (float)System.Math.Floor(phase);
            if (phase < 0.25f)
            {
                return 4f * phase;
            }

            return phase < 0.75f ? 2f - 4f * phase : 4f * phase - 4f;
        }

        private Vec2 Clamp(Vec2 p)
        {
            var b = Config.Bounds;
            return new Vec2(
                Calc.Clamp(p.X, b.XMin, b.XMax),
                Calc.Clamp(p.Y, b.YMin, b.YMax));
        }
    }
}
