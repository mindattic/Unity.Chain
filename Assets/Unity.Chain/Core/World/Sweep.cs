namespace MindAttic.Chain.Core
{
    /// <summary>Geometry for the world's swept mace checks (BIBLE D12): a circle cast, and the rectangle cast that sweeps Chain.vb's rectMace.</summary>
    public static class Sweep
    {
        /// <summary>
        /// Where along the move from <paramref name="from"/> to <paramref name="to"/> a circle of
        /// <paramref name="radius"/> first touches a circle of <paramref name="targetRadius"/> at
        /// <paramref name="target"/>, as a fraction 0-1 (0 when it starts touching). Returns false when
        /// it never touches. A fast swing cannot pass through an enemy between two steps.
        /// </summary>
        public static bool CircleCast(Vec2 from, Vec2 to, float radius, Vec2 target, float targetRadius, out float fraction)
        {
            var reach = radius + targetRadius;
            var reachSq = reach * reach;
            var f = from - target;
            var c = f.SqrMagnitude - reachSq;
            if (c <= 0f)
            {
                fraction = 0f;
                return true;
            }

            var d = to - from;
            var a = d.SqrMagnitude;
            if (a <= 1e-12f)
            {
                fraction = 0f;
                return false;
            }

            var b = 2f * (f.X * d.X + f.Y * d.Y);
            var discriminant = b * b - 4f * a * c;
            if (discriminant < 0f)
            {
                fraction = 0f;
                return false;
            }

            var t = (-b - Calc.Sqrt(discriminant)) / (2f * a);
            if (t < 0f || t > 1f)
            {
                fraction = 0f;
                return false;
            }

            fraction = t;
            return true;
        }

        /// <summary>The most samples one <see cref="RectCast"/> takes along a move.</summary>
        private const int MaxRectSamples = 256;

        /// <summary>
        /// Where along a move of <paramref name="delta"/> the rectangle <paramref name="mover"/> (as it stands at the start) first
        /// intersects <paramref name="target"/> by Root.vb's <c>rectIntersect</c> (only the mover's edges inside the target's),
        /// as a fraction 0-1 (0 when it starts touching). Samples the move at steps of at most a quarter of
        /// <paramref name="minSide"/> (the smaller side of the rectangles), so a fast swing cannot pass through between two steps.
        /// </summary>
        public static bool RectCast(HeartRect mover, Vec2 delta, HeartRect target, float minSide, out float fraction)
        {
            var step = Calc.Max(minSide * 0.25f, 1e-4f);
            var samples = (int)(delta.Magnitude / step) + 1;
            if (samples > MaxRectSamples)
            {
                samples = MaxRectSamples;
            }

            for (var i = 0; i <= samples; i++)
            {
                var t = (float)i / samples;
                var dx = delta.X * t;
                var dy = delta.Y * t;
                var at = new HeartRect(mover.Left + dx, mover.Top + dy, mover.Right + dx, mover.Bottom + dy);
                if (HeartFloat.RectIntersect(at, target))
                {
                    fraction = t;
                    return true;
                }
            }

            fraction = 0f;
            return false;
        }
    }
}
