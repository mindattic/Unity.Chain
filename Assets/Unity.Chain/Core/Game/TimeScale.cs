namespace MindAttic.Chain.Core
{
    /// <summary>
    /// Root.vb's <c>gameSpeed</c> as the world's time scale (UNI-T52). Input.vb's <c>getKeyboard</c> runs every
    /// frame, paused or not: Minus takes 0.01 off <c>gameSpeed</c> and Equals adds 0.01 for each frame the key is
    /// held, and it never goes below 0. The original only drew the figure (<c>FormatNumber(gameSpeed, 2)</c>) and
    /// never applied it to anything; the port scales every step by it (BIBLE D13, V20). It holds the rule; the
    /// world (<see cref="GameWorld.GameSpeed"/>) holds the figure.
    /// </summary>
    public static class TimeScale
    {
        /// <summary>
        /// The most sub-steps one fixed step is cut into. Past this many the sub-steps simply grow, so a held
        /// Equals can never make one fixed step unboundedly expensive.
        /// </summary>
        public const int MaxSubsteps = 64;

        /// <summary>Speeds this close above a whole number still take that many sub-steps (0.01 steps add up inexactly).</summary>
        private const float Slack = 1e-3f;

        /// <summary>
        /// <paramref name="speed"/> after Minus (<paramref name="direction"/> -1) or Equals (+1) was held for
        /// <paramref name="heldSeconds"/>, at <paramref name="ratePerSecond"/> (game.json's <c>gameSpeedStep</c>,
        /// 0.01 a frame, so 0.6 a second at 60 fps). Never below 0, as Input.vb's
        /// <c>If gameSpeed &lt; 0 Then gameSpeed = 0</c>; no upper limit, as in the original.
        /// </summary>
        public static float Change(float speed, int direction, float ratePerSecond, float heldSeconds)
        {
            if (direction == 0 || ratePerSecond <= 0f || heldSeconds <= 0f)
            {
                return Calc.Max(0f, speed);
            }

            var sign = direction > 0 ? 1f : -1f;
            return Calc.Max(0f, speed + sign * ratePerSecond * heldSeconds);
        }

        /// <summary>
        /// How many sub-steps a fixed step runs at <paramref name="speed"/>: one at 1.0 or below, and enough above it
        /// that no sub-step is longer than the fixed step itself (the chain's springs are only stable at the fixed
        /// step), up to <see cref="MaxSubsteps"/>. None at 0: time stands still.
        /// </summary>
        public static int Substeps(float speed)
        {
            if (speed <= 0f)
            {
                return 0;
            }

            var n = (int)System.Math.Ceiling(speed - Slack);
            return n < 1 ? 1 : n > MaxSubsteps ? MaxSubsteps : n;
        }

        /// <summary>
        /// How many sub-steps <paramref name="deltaTime"/> real seconds (scaled by <paramref name="speed"/> of
        /// game time) is cut into so each one lands close to 1 / <paramref name="physicsStepRate"/> seconds
        /// (<c>GameWorld.Settings.PhysicsStepRate</c>, game.json's own frame rate, 60: BIBLE D9), whatever the
        /// host's own frame rate is. The original's chain integration is a discrete per-frame recurrence, so
        /// the port only feels the same if it keeps advancing in close to the original's own frame length,
        /// not a single sub-step scaled to a longer or shorter host frame. At least one sub-step runs
        /// whenever time should pass at all, and never more than <see cref="MaxSubsteps"/>.
        /// <paramref name="physicsStepRate"/> no more than 0 falls back to <see cref="Substeps(float)"/>: the
        /// coarser, speed-only rule (enough sub-steps that none is longer than <paramref name="deltaTime"/>
        /// itself).
        /// </summary>
        public static int Substeps(float speed, float deltaTime, float physicsStepRate)
        {
            if (speed <= 0f || deltaTime <= 0f)
            {
                return 0;
            }

            if (physicsStepRate <= 0f)
            {
                return Substeps(speed);
            }

            var gameSeconds = (double)deltaTime * speed;
            var n = (int)System.Math.Round(gameSeconds * physicsStepRate, System.MidpointRounding.AwayFromZero);
            return n < 1 ? 1 : n > MaxSubsteps ? MaxSubsteps : n;
        }
    }
}
