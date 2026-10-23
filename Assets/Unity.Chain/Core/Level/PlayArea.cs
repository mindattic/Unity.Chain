

namespace MindAttic.Chain.Core
{
    /// <summary>
    /// Pure rect math for the level's play space: insetting, clamping a point, and Elastic
    /// Trail's wall bounce (a dot that leaves the window is put back on the edge and its velocity
    /// on that axis is reflected and scaled by the bounce factor). Chain.vb carried the bounce
    /// over but left its <c>checkBounds</c> commented out; the Unity port brings it back as an
    /// opt-in on the chain controller so the mace cannot be swung off the level.
    /// </summary>
    public static class PlayArea
    {
        /// <summary>
        /// Shrinks <paramref name="rect"/> by <paramref name="margin"/> on every side. A margin
        /// larger than half the rect collapses that axis onto the rect's center instead of
        /// producing a negative size.
        /// </summary>
        public static Rect2 Inset(Rect2 rect, Vec2 margin)
        {
            var hx = Calc.Max(0f, rect.Width * 0.5f - margin.X);
            var hy = Calc.Max(0f, rect.Height * 0.5f - margin.Y);
            var c = rect.Center;
            return new Rect2(c.X - hx, c.Y - hy, hx * 2f, hy * 2f);
        }

        public static Vec2 Clamp(Rect2 rect, Vec2 point) => new Vec2(
            Calc.Clamp(point.X, rect.XMin, rect.XMax),
            Calc.Clamp(point.Y, rect.YMin, rect.YMax));

        /// <summary>
        /// Keeps a moving point inside <paramref name="rect"/>: past an edge it is placed on the
        /// edge and its velocity on that axis is reversed and multiplied by
        /// <paramref name="bounce"/> (0 = stops dead, 1 = perfectly elastic). Velocity already
        /// heading back inside is left alone, so a point resting on an edge does not jitter.
        /// Returns true if the point touched a wall.
        /// </summary>
        public static bool Bounce(Rect2 rect, ref Vec2 position, ref Vec2 velocity, float bounce)
        {
            var hit = false;
            bounce = Calc.Max(0f, bounce);
            var px = position.X;
            var py = position.Y;
            var vx = velocity.X;
            var vy = velocity.Y;

            if (px < rect.XMin)
            {
                px = rect.XMin;
                if (vx < 0f) vx = -vx * bounce;
                hit = true;
            }
            else if (px > rect.XMax)
            {
                px = rect.XMax;
                if (vx > 0f) vx = -vx * bounce;
                hit = true;
            }

            if (py < rect.YMin)
            {
                py = rect.YMin;
                if (vy < 0f) vy = -vy * bounce;
                hit = true;
            }
            else if (py > rect.YMax)
            {
                py = rect.YMax;
                if (vy > 0f) vy = -vy * bounce;
                hit = true;
            }

            position = new Vec2(px, py);
            velocity = new Vec2(vx, vy);
            return hit;
        }
    }
}
