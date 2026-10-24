namespace MindAttic.Chain.Core
{
    /// <summary>
    /// The one mapping from the original's space and time to the port's, read from space.json. Every
    /// figure in the data files is the original's own, in its own units (screen space, where -0.5..0.5
    /// is the whole screen height, and per frame, since the original had no time step); this is the
    /// only place they become world units and seconds. <see cref="GameSettings"/> calls it for each
    /// figure as it maps the files, saying what kind of quantity the figure is:
    /// <list type="bullet">
    /// <item><see cref="Length"/>: positions, sizes, offsets (x <see cref="LengthScale"/>).</item>
    /// <item><see cref="Speed"/>: lengths per frame (x scale x fps).</item>
    /// <item><see cref="Acceleration"/>: lengths per frame per frame (x scale x fps^2).</item>
    /// <item><see cref="PerSecond"/> and <see cref="PerSecondSquared"/>: rates with no length in them
    /// (spring stiffness, drag, seek rate, tint steps).</item>
    /// <item><see cref="Seconds"/>: frame counts to seconds.</item>
    /// </list>
    /// Per-frame figures are converted at <see cref="FramesPerSecond"/> (BIBLE D9). The length scale is 9 (UNI-T66): the original's
    /// screen height is the arena's height; changing it here moves every ported figure together.
    /// </summary>
    public readonly struct OriginalSpace
    {
        public OriginalSpace(float lengthScale, float framesPerSecond)
        {
            LengthScale = lengthScale;
            FramesPerSecond = framesPerSecond;
        }

        /// <summary>World units per original screen unit.</summary>
        public float LengthScale { get; }

        /// <summary>The frame rate the original's per-frame figures are taken at.</summary>
        public float FramesPerSecond { get; }

        /// <summary>
        /// The shipped length scale (space.json): the original's screen height, 1.0, is the arena's 9 world
        /// units (<c>GameWorld.DefaultArena</c> is 16 x 9). Defaults built without the data files use it.
        /// </summary>
        public const float ArenaLengthScale = 9f;

        /// <summary>No change at all: original units, per frame at one frame a second. For tests.</summary>
        public static OriginalSpace Identity => new OriginalSpace(1f, 1f);

        public float Length(float original) => original * LengthScale;

        public Vec2 Length(Vec2 original) => original * LengthScale;

        public float Speed(float perFrame) => perFrame * LengthScale * FramesPerSecond;

        public float Acceleration(float perFrameSquared) => perFrameSquared * LengthScale * FramesPerSecond * FramesPerSecond;

        public Vec2 Acceleration(Vec2 perFrameSquared) => perFrameSquared * (LengthScale * FramesPerSecond * FramesPerSecond);

        public float PerSecond(float perFrame) => perFrame * FramesPerSecond;

        public float PerSecondSquared(float perFrameSquared) => perFrameSquared * FramesPerSecond * FramesPerSecond;

        public float Seconds(float frames) => frames / FramesPerSecond;

        /// <summary>World units moved per unit of input (Angel.vb's Ratio3D, screen units per mouse count).</summary>
        public float PerInput(float originalPerInput) => originalPerInput * LengthScale;

        // The way back, for readouts that show the original's own figures (the HUD's Res/Grav/Spring, UNI-T50).

        /// <summary>The inverse of <see cref="PerSecond"/>: a rate per second back to the original's per frame.</summary>
        public float ToPerFrame(float perSecond) => (float)((double)perSecond / FramesPerSecond);

        /// <summary>The inverse of <see cref="PerSecondSquared"/>.</summary>
        public float ToPerFrameSquared(float perSecondSquared) => (float)((double)perSecondSquared / ((double)FramesPerSecond * FramesPerSecond));

        /// <summary>The inverse of <see cref="Acceleration(float)"/>: world units per second squared back to screen units per frame squared.</summary>
        public float ToOriginalAcceleration(float acceleration) =>
            (float)((double)acceleration / ((double)LengthScale * FramesPerSecond * FramesPerSecond));
    }
}
