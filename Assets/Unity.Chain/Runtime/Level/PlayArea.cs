using UnityEngine;

namespace MindAttic.Chain
{
    /// <summary>
    /// Pure rect math for the level's play space: insetting, clamping a point, and Elastic
    /// Trail's wall bounce (a dot that leaves the window is put back on the edge and its velocity
    /// on that axis is reflected and scaled by the bounce factor). Chain.vb carried the bounce
    /// over but left its <c>checkBounds</c> commented out; the Unity port brings it back as an
    /// opt-in on <see cref="ChainController"/> so the mace cannot be swung off the level.
    /// </summary>
    public static class PlayArea
    {
        /// <summary>
        /// Shrinks <paramref name="rect"/> by <paramref name="margin"/> on every side. A margin
        /// larger than half the rect collapses that axis onto the rect's center instead of
        /// producing a negative size.
        /// </summary>
        public static Rect Inset(Rect rect, Vector2 margin)
        {
            var hx = Mathf.Max(0f, rect.width * 0.5f - margin.x);
            var hy = Mathf.Max(0f, rect.height * 0.5f - margin.y);
            var c = rect.center;
            return new Rect(c.x - hx, c.y - hy, hx * 2f, hy * 2f);
        }

        public static Vector2 Clamp(Rect rect, Vector2 point) => new Vector2(
            Mathf.Clamp(point.x, rect.xMin, rect.xMax),
            Mathf.Clamp(point.y, rect.yMin, rect.yMax));

        /// <summary>
        /// Keeps a moving point inside <paramref name="rect"/>: past an edge it is placed on the
        /// edge and its velocity on that axis is reversed and multiplied by
        /// <paramref name="bounce"/> (0 = stops dead, 1 = perfectly elastic). Velocity already
        /// heading back inside is left alone, so a point resting on an edge does not jitter.
        /// Returns true if the point touched a wall.
        /// </summary>
        public static bool Bounce(Rect rect, ref Vector2 position, ref Vector2 velocity, float bounce)
        {
            var hit = false;
            bounce = Mathf.Max(0f, bounce);

            if (position.x < rect.xMin)
            {
                position.x = rect.xMin;
                if (velocity.x < 0f) velocity.x = -velocity.x * bounce;
                hit = true;
            }
            else if (position.x > rect.xMax)
            {
                position.x = rect.xMax;
                if (velocity.x > 0f) velocity.x = -velocity.x * bounce;
                hit = true;
            }

            if (position.y < rect.yMin)
            {
                position.y = rect.yMin;
                if (velocity.y < 0f) velocity.y = -velocity.y * bounce;
                hit = true;
            }
            else if (position.y > rect.yMax)
            {
                position.y = rect.yMax;
                if (velocity.y > 0f) velocity.y = -velocity.y * bounce;
                hit = true;
            }

            return hit;
        }
    }
}
