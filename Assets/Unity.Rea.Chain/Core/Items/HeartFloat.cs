namespace MindAttic.Chain.Core
{
    /// <summary>Which way Item.vb's heart is swaying (<c>itemFloat</c>).</summary>
    public enum HeartSway
    {
        Left,
        Right,
    }

    /// <summary>An <c>Item.vb</c> / <c>Angel.vb</c> hit rectangle: Top is above Bottom, as the original builds them.</summary>
    public readonly struct HeartRect
    {
        public readonly float Left;
        public readonly float Top;
        public readonly float Right;
        public readonly float Bottom;

        public HeartRect(float left, float top, float right, float bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }
    }

    /// <summary>
    /// The heart pickup's rules, a line-for-line port of Item.vb (the one Health10 heart) and
    /// Engine.vb's <c>checkItemCollect</c>. Plain C# with no engine types. Every figure is the
    /// original's own and the port's units are the original's (D9: per-frame figures at the fixed
    /// 60 fps step, so the fall of 0.001 per frame is 0.06 u/s and the sway starts at 0.12 u/s).
    /// <list type="bullet">
    /// <item>It starts at x 0, y 0.4 (<c>loadItem</c>) swaying left with <c>floatSPD</c> 0.002 and
    /// <c>floatAcc</c> 0.00001; <c>Reset</c> puts it back at the same place with <c>floatSPD</c>
    /// 0.001.</item>
    /// <item>Each frame it falls <c>fallRate</c> 0.001 (not floatSPD), wraps from below -0.5 to 0.5,
    /// and sways through Item.vb's six sequential <c>If</c> blocks, which run in order on state the
    /// earlier blocks have just changed, so a frame can flip direction and move again.</item>
    /// <item>Touching it (<see cref="RectIntersect"/>, which tests only the angel's corners against
    /// the heart's rectangle, as <c>rectIntersect</c> does) consumes it for +10 HP, capped at max HP
    /// by the caller; a consumed heart does not move or draw until <see cref="Reset"/>.</item>
    /// </list>
    /// </summary>
    public sealed class HeartFloat
    {
        /// <summary>Item.vb steps once per frame; D9 fixes the rate at 60 fps.</summary>
        public const int FramesPerSecond = 60;

        /// <summary>One original frame in seconds.</summary>
        public const float FrameSeconds = 1f / FramesPerSecond;

        /// <summary>Every figure of Item.vb, Angel.vb and Root.vb's Reset that the heart uses.</summary>
        public struct Settings
        {
            /// <summary><c>itemSize</c>: the sprite quad's width; its height is the same, centred on Y.</summary>
            public float ItemSize;
            /// <summary><c>fallRate</c>, per frame.</summary>
            public float FallRate;
            /// <summary><c>floatMaxSPD</c>, per frame.</summary>
            public float FloatMaxSpeed;
            /// <summary><c>floatAcc</c>, per frame.</summary>
            public float FloatAcceleration;
            /// <summary><c>floatSPD</c> from <c>loadItem</c>, per frame.</summary>
            public float StartFloatSpeed;
            /// <summary><c>floatSPD</c> from <c>Reset</c>, per frame.</summary>
            public float ResetFloatSpeed;
            /// <summary>Start x (<c>Reset</c>; <c>loadItem</c> leaves the default 0).</summary>
            public float StartX;
            /// <summary>Start y.</summary>
            public float StartY;
            /// <summary>The heart wraps when y is below this...</summary>
            public float WrapBelow;
            /// <summary>...to this.</summary>
            public float WrapTo;
            /// <summary><c>itemType.Health10</c>.</summary>
            public int HealAmount;
            /// <summary><c>angelSize</c>, which sizes the angel's hit rectangle.</summary>
            public float AngelSize;
            /// <summary>Item.vb: the heart's rectangle reaches this far above <c>itemSize / 2</c> (0.005).</summary>
            public float RectTopPad;
            /// <summary>Item.vb: the heart's rectangle width from X (0.045).</summary>
            public float RectWidth;
            /// <summary>Item.vb: the heart's rectangle bottom below Y (0.02).</summary>
            public float RectBottom;
            /// <summary>Angel.vb: the angel's rectangle width from X (0.06).</summary>
            public float AngelRectWidth;
            /// <summary>Angel.vb: the angel's rectangle bottom above Y (0.03).</summary>
            public float AngelRectBottom;

            public static Settings Default => new Settings
            {
                ItemSize = 0.05f,
                FallRate = 0.001f,
                FloatMaxSpeed = 0.001f,
                FloatAcceleration = 0.00001f,
                StartFloatSpeed = 0.002f,
                ResetFloatSpeed = 0.001f,
                StartX = 0f,
                StartY = 0.4f,
                WrapBelow = -0.5f,
                WrapTo = 0.5f,
                HealAmount = 10,
                AngelSize = 0.3f,
                RectTopPad = 0.005f,
                RectWidth = 0.045f,
                RectBottom = 0.02f,
                AngelRectWidth = 0.06f,
                AngelRectBottom = 0.03f,
            };
        }

        private readonly Settings _settings;
        private float _accumulator;

        public HeartFloat() : this(Settings.Default)
        {
        }

        public HeartFloat(Settings settings)
        {
            _settings = settings;
            X = settings.StartX;
            Y = settings.StartY;
            Sway = HeartSway.Left;
            FloatSpeed = settings.StartFloatSpeed;
            Rect = BuildRect();
        }

        public float X { get; private set; }
        public float Y { get; private set; }

        /// <summary><c>floatSPD</c>: the sway speed this frame, per frame.</summary>
        public float FloatSpeed { get; private set; }

        public HeartSway Sway { get; private set; }

        /// <summary><c>turnAround</c>.</summary>
        public bool TurnAround { get; private set; }

        public bool PickedUp { get; private set; }

        /// <summary>The heart's hit rectangle, rebuilt each frame after it moves.</summary>
        public HeartRect Rect { get; private set; }

        public int HealAmount => _settings.HealAmount;

        public Settings Config => _settings;

        /// <summary>
        /// Feeds <paramref name="deltaSeconds"/> of time in and runs as many whole original frames as
        /// fit (the fixed 60 fps step). Returns how many ran.
        /// </summary>
        public int Advance(float deltaSeconds)
        {
            if (deltaSeconds > 0f)
            {
                _accumulator += deltaSeconds;
            }

            var frames = 0;
            while (_accumulator >= FrameSeconds)
            {
                _accumulator -= FrameSeconds;
                Frame();
                frames++;
            }

            return frames;
        }

        /// <summary>One <c>renderItem</c> frame for the heart. A picked-up heart does nothing.</summary>
        public void Frame()
        {
            if (PickedUp)
            {
                return;
            }

            // itemMotion.FloatDown: vertical fall
            Y -= _settings.FallRate;
            if (Y < _settings.WrapBelow)
            {
                Y = _settings.WrapTo;
            }

            // Horizontal motion: six sequential Ifs, each seeing what the ones above changed.
            if (Sway == HeartSway.Left && !TurnAround)
            {
                X -= FloatSpeed;
                FloatSpeed -= _settings.FloatAcceleration;
                if (FloatSpeed <= 0f)
                {
                    TurnAround = true;
                }
            }

            if (Sway == HeartSway.Left && TurnAround)
            {
                X += FloatSpeed;
                FloatSpeed += _settings.FloatAcceleration;
            }

            if (Sway == HeartSway.Left && FloatSpeed >= _settings.FloatMaxSpeed)
            {
                TurnAround = false;
                Sway = HeartSway.Right;
            }

            if (Sway == HeartSway.Right && !TurnAround)
            {
                X += FloatSpeed;
                FloatSpeed -= _settings.FloatAcceleration;
                if (FloatSpeed <= 0f)
                {
                    TurnAround = true;
                }
            }

            if (Sway == HeartSway.Right && TurnAround)
            {
                X -= FloatSpeed;
                FloatSpeed += _settings.FloatAcceleration;
            }

            if (Sway == HeartSway.Right && FloatSpeed >= _settings.FloatMaxSpeed)
            {
                TurnAround = false;
                Sway = HeartSway.Left;
            }

            Rect = BuildRect();
        }

        /// <summary>
        /// <c>checkItemCollect</c> for an angel at (<paramref name="angelX"/>, <paramref name="angelY"/>),
        /// the origin Angel.vb builds its rectangle from. Returns true the frame the heart is picked up.
        /// </summary>
        public bool TryCollect(float angelX, float angelY)
        {
            if (PickedUp || !RectIntersect(AngelRect(angelX, angelY), Rect))
            {
                return false;
            }

            PickedUp = true;
            return true;
        }

        /// <summary>Root.vb's Reset for the item: back to the start, floatSPD 0.001, not picked up. <c>turnAround</c> is not reset, as in the original.</summary>
        public void Reset()
        {
            X = _settings.StartX;
            Y = _settings.StartY;
            Sway = HeartSway.Left;
            PickedUp = false;
            FloatSpeed = _settings.ResetFloatSpeed;
            Rect = BuildRect();
        }

        /// <summary>Angel.vb: Left X, Right X + 0.06, Top Y + angelSize / 2, Bottom Y + 0.03.</summary>
        public HeartRect AngelRect(float angelX, float angelY) =>
            new HeartRect(angelX, angelY + _settings.AngelSize / 2f, angelX + _settings.AngelRectWidth, angelY + _settings.AngelRectBottom);

        /// <summary>Root.vb's <c>rectIntersect</c>: a's top or bottom edge lies within b's, and a's left or right edge lies within b's.</summary>
        public static bool RectIntersect(HeartRect a, HeartRect b) =>
            (Between(a.Top, b.Bottom, b.Top) || Between(a.Bottom, b.Bottom, b.Top))
            && (Between(a.Left, b.Left, b.Right) || Between(a.Right, b.Left, b.Right));

        /// <summary>Root.vb's <c>A_between_BC</c>.</summary>
        private static bool Between(float a, float b, float c) => a >= b && a <= c;

        // Item.vb: Left X, Top Y + itemSize / 2 + 0.005, Right X + 0.045, Bottom Y - 0.02.
        private HeartRect BuildRect() =>
            new HeartRect(X, Y + _settings.ItemSize / 2f + _settings.RectTopPad, X + _settings.RectWidth, Y - _settings.RectBottom);
    }
}
