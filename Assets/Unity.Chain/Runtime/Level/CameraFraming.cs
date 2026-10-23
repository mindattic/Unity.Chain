using UnityEngine;

namespace MindAttic.Chain
{
    /// <summary>
    /// Pure 2D orthographic camera math used by <see cref="CameraRig"/>: how big the view must
    /// be to show (or stay inside) the level, where its center may sit so it never shows past
    /// the level's edges, a dead-zone follow, and frame-rate independent smoothing. The 2005
    /// game had a fixed camera looking at a 4:3 play field; the port frames the level on any
    /// aspect ratio and can optionally follow the angel across a level larger than one screen.
    /// </summary>
    public static class CameraFraming
    {
        /// <summary>Half the width and height of an orthographic view.</summary>
        public static Vector2 HalfExtents(float orthographicSize, float aspect) =>
            new Vector2(orthographicSize * Mathf.Max(0f, aspect), orthographicSize);

        /// <summary>
        /// The smallest orthographic size whose view shows the whole level (letterboxed on the
        /// axis with room to spare), plus <paramref name="padding"/> world units on every side.
        /// </summary>
        public static float FitOrthographicSize(Vector2 levelSize, float aspect, float padding = 0f)
        {
            aspect = Mathf.Max(0.0001f, aspect);
            var paddedHeight = levelSize.y + padding * 2f;
            var paddedWidth = levelSize.x + padding * 2f;
            return Mathf.Max(0.0001f, Mathf.Max(paddedHeight * 0.5f, paddedWidth * 0.5f / aspect));
        }

        /// <summary>
        /// The largest orthographic size whose view still fits inside the level on both axes
        /// (cropping the axis with room to spare), so nothing outside the level is ever shown.
        /// </summary>
        public static float FillOrthographicSize(Vector2 levelSize, float aspect)
        {
            aspect = Mathf.Max(0.0001f, aspect);
            return Mathf.Max(0.0001f, Mathf.Min(levelSize.y * 0.5f, levelSize.x * 0.5f / aspect));
        }

        /// <summary>
        /// Clamps a view center so a view of <paramref name="halfExtents"/> stays inside
        /// <paramref name="level"/>. On an axis where the view is as big as the level or bigger,
        /// the view is centered on the level instead.
        /// </summary>
        public static Vector2 ClampCenter(Vector2 center, Vector2 halfExtents, Rect level) => new Vector2(
            ClampAxis(center.x, halfExtents.x, level.xMin, level.xMax),
            ClampAxis(center.y, halfExtents.y, level.yMin, level.yMax));

        /// <summary>
        /// Moves <paramref name="center"/> only as far as needed for <paramref name="target"/> to
        /// sit within <paramref name="deadZoneHalf"/> of it on each axis, so small movements
        /// (the angel's hover bob, a short dodge) do not drag the camera around.
        /// </summary>
        public static Vector2 DeadZone(Vector2 center, Vector2 target, Vector2 deadZoneHalf)
        {
            var dx = target.x - center.x;
            var dy = target.y - center.y;
            var hx = Mathf.Max(0f, deadZoneHalf.x);
            var hy = Mathf.Max(0f, deadZoneHalf.y);

            if (dx > hx) center.x = target.x - hx;
            else if (dx < -hx) center.x = target.x + hx;

            if (dy > hy) center.y = target.y - hy;
            else if (dy < -hy) center.y = target.y + hy;

            return center;
        }

        /// <summary>
        /// Exponential approach toward <paramref name="goal"/>, independent of frame rate:
        /// <paramref name="sharpness"/> is the fraction (in e-folds) closed per second. A
        /// sharpness of 0 or less snaps straight to the goal.
        /// </summary>
        public static Vector2 Smooth(Vector2 current, Vector2 goal, float sharpness, float deltaTime)
        {
            if (sharpness <= 0f)
            {
                return goal;
            }

            var t = 1f - Mathf.Exp(-sharpness * Mathf.Max(0f, deltaTime));
            return Vector2.LerpUnclamped(current, goal, t);
        }

        private static float ClampAxis(float center, float half, float min, float max)
        {
            if (half * 2f >= max - min)
            {
                return (min + max) * 0.5f;
            }

            return Mathf.Clamp(center, min + half, max - half);
        }
    }
}
