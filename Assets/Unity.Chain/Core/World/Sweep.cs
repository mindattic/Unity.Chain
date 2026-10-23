namespace MindAttic.Chain.Core
{
    /// <summary>Circle geometry for the world's contact checks (the mace's swept circle, which replaced Chain.vb's per-frame rectMace box).</summary>
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
    }
}
