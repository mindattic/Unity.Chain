using MindAttic.Chain.Core;
using UnityEngine;

namespace MindAttic.Chain
{
    /// <summary>
    /// The one place Runtime converts between the engine's maths types and Core's own
    /// (<see cref="Vec2"/>, <see cref="Rect2"/>). Core never sees UnityEngine; Runtime adapts here.
    /// </summary>
    public static class CoreConvert
    {
        public static Vec2 ToCore(this Vector2 v) => new Vec2(v.x, v.y);
        public static Vec2 ToCore(this Vector3 v) => new Vec2(v.x, v.y);
        public static Rect2 ToCore(this Rect r) => new Rect2(r.x, r.y, r.width, r.height);

        public static Vector2 ToUnity(this Vec2 v) => new Vector2(v.X, v.Y);
        public static Rect ToUnity(this Rect2 r) => new Rect(r.X, r.Y, r.Width, r.Height);
    }
}
