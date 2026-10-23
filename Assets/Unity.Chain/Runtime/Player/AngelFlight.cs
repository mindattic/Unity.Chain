using UnityEngine;

namespace MindAttic.Chain
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
    /// Kept free of MonoBehaviour so it is unit-testable; <see cref="AngelController"/> feeds it
    /// input and applies the result to a Transform and SpriteRenderer.
    ///
    /// Differences from the original, on purpose:
    /// - Everything is <c>dt</c>-scaled (hover and flap rates are per-second, not per-frame).
    /// - Mouse Y is not inverted: DirectInput reported Y-down deltas, the Input System reports Y-up.
    /// - The hover bob is a smooth sine instead of the original's per-frame triangle ping-pong.
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
            public readonly Rect Bounds;
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
            public readonly Vector2 HandOffset;

            public Settings(
                float sensitivity,
                Rect bounds,
                float hoverAmplitude,
                float hoverPeriod,
                float frameDuration,
                int frameCount,
                Vector2 handOffset)
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
                bounds: new Rect(-8f, -4.5f, 16f, 9f),
                hoverAmplitude: 0.08f,
                hoverPeriod: 1.6f,
                frameDuration: 0.1f,
                frameCount: 4,
                handOffset: new Vector2(0.25f, -0.1f));

            public Settings WithBounds(Rect bounds) => new Settings(
                Sensitivity, bounds, HoverAmplitude, HoverPeriod, FrameDuration, FrameCount, HandOffset);
        }

        public Settings Config;

        public Vector2 Position { get; private set; }
        public Facing Facing { get; private set; } = Facing.Right;
        public int Frame { get; private set; }
        public float HoverOffset { get; private set; }

        private float _hoverTime;
        private float _frameTime;

        public AngelFlight(Vector2 start, Settings settings)
        {
            Config = settings;
            Position = Clamp(start);
        }

        /// <summary>Where the body is drawn: flight position plus the hover bob.</summary>
        public Vector2 RenderPosition => Position + new Vector2(0f, HoverOffset);

        /// <summary>Where chain link 0 attaches: the hand offset, mirrored by facing.</summary>
        public Vector2 HandSocket
        {
            get
            {
                var offset = Config.HandOffset;
                if (Facing == Facing.Left)
                {
                    offset.x = -offset.x;
                }

                return RenderPosition + offset;
            }
        }

        public void Teleport(Vector2 position) => Position = Clamp(position);

        public void Step(Vector2 mouseDelta, float deltaTime)
        {
            // Facing follows the last non-zero horizontal mouse motion; a still mouse keeps it.
            if (mouseDelta.x > 0f)
            {
                Facing = Facing.Right;
            }
            else if (mouseDelta.x < 0f)
            {
                Facing = Facing.Left;
            }

            Position = Clamp(Position + mouseDelta * Config.Sensitivity);

            if (Config.HoverPeriod > 0f)
            {
                _hoverTime = (_hoverTime + deltaTime) % Config.HoverPeriod;
                HoverOffset = Config.HoverAmplitude * Mathf.Sin(_hoverTime / Config.HoverPeriod * 2f * Mathf.PI);
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

        private Vector2 Clamp(Vector2 p)
        {
            var b = Config.Bounds;
            return new Vector2(
                Mathf.Clamp(p.x, b.xMin, b.xMax),
                Mathf.Clamp(p.y, b.yMin, b.yMax));
        }
    }
}
