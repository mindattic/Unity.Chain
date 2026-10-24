namespace MindAttic.Chain.Core
{
    public enum CameraFramingMode
    {
        /// <summary>Static camera that shows the whole level, letterboxed to any aspect (the original's fixed view).</summary>
        FitLevel,
        /// <summary>Fixed zoom that follows the target with a dead zone, never showing past the level's edges.</summary>
        Follow,
    }

    /// <summary>
    /// Pure 2D orthographic camera math used by the camera rig: how big the view must
    /// be to show (or stay inside) the level, where its center may sit so it never shows past
    /// the level's edges, a dead-zone follow, and frame-rate independent smoothing. The 2005
    /// game had a fixed camera looking at a 4:3 play field; the port frames the level on any
    /// aspect ratio and can optionally follow the angel across a level larger than one screen.
    /// </summary>
    public static class CameraFraming
    {
        /// <summary>Half the width and height of an orthographic view.</summary>
        public static Vec2 HalfExtents(float orthographicSize, float aspect) =>
            new Vec2(orthographicSize * Calc.Max(0f, aspect), orthographicSize);

        /// <summary>
        /// The smallest orthographic size whose view shows the whole level (letterboxed on the
        /// axis with room to spare), plus <paramref name="padding"/> world units on every side.
        /// </summary>
        public static float FitOrthographicSize(Vec2 levelSize, float aspect, float padding = 0f)
        {
            aspect = Calc.Max(0.0001f, aspect);
            var paddedHeight = levelSize.Y + padding * 2f;
            var paddedWidth = levelSize.X + padding * 2f;
            return Calc.Max(0.0001f, Calc.Max(paddedHeight * 0.5f, paddedWidth * 0.5f / aspect));
        }

        /// <summary>
        /// The largest orthographic size whose view still fits inside the level on both axes
        /// (cropping the axis with room to spare), so nothing outside the level is ever shown.
        /// </summary>
        public static float FillOrthographicSize(Vec2 levelSize, float aspect)
        {
            aspect = Calc.Max(0.0001f, aspect);
            return Calc.Max(0.0001f, Calc.Min(levelSize.Y * 0.5f, levelSize.X * 0.5f / aspect));
        }

        /// <summary>
        /// Clamps a view center so a view of <paramref name="halfExtents"/> stays inside
        /// <paramref name="level"/>. On an axis where the view is as big as the level or bigger,
        /// the view is centered on the level instead.
        /// </summary>
        public static Vec2 ClampCenter(Vec2 center, Vec2 halfExtents, Rect2 level) => new Vec2(
            ClampAxis(center.X, halfExtents.X, level.XMin, level.XMax),
            ClampAxis(center.Y, halfExtents.Y, level.YMin, level.YMax));

        /// <summary>
        /// Moves <paramref name="center"/> only as far as needed for <paramref name="target"/> to
        /// sit within <paramref name="deadZoneHalf"/> of it on each axis, so small movements
        /// (the angel's hover bob, a short dodge) do not drag the camera around.
        /// </summary>
        public static Vec2 DeadZone(Vec2 center, Vec2 target, Vec2 deadZoneHalf)
        {
            var dx = target.X - center.X;
            var dy = target.Y - center.Y;
            var hx = Calc.Max(0f, deadZoneHalf.X);
            var hy = Calc.Max(0f, deadZoneHalf.Y);

            var x = center.X;
            var y = center.Y;

            if (dx > hx) x = target.X - hx;
            else if (dx < -hx) x = target.X + hx;

            if (dy > hy) y = target.Y - hy;
            else if (dy < -hy) y = target.Y + hy;

            return new Vec2(x, y);
        }

        /// <summary>
        /// Exponential approach toward <paramref name="goal"/>, independent of frame rate:
        /// <paramref name="sharpness"/> is the fraction (in e-folds) closed per second. A
        /// sharpness of 0 or less snaps straight to the goal.
        /// </summary>
        public static Vec2 Smooth(Vec2 current, Vec2 goal, float sharpness, float deltaTime)
        {
            if (sharpness <= 0f)
            {
                return goal;
            }

            var t = 1f - Calc.Exp(-sharpness * Calc.Max(0f, deltaTime));
            return Vec2.LerpUnclamped(current, goal, t);
        }

        private static float ClampAxis(float center, float half, float min, float max)
        {
            if (half * 2f >= max - min)
            {
                return (min + max) * 0.5f;
            }

            return Calc.Clamp(center, min + half, max - half);
        }
    }
}
