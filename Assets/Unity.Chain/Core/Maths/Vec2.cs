using System;

namespace MindAttic.Chain.Core
{
    /// <summary>
    /// Core's own 2D vector: an immutable pair of floats with the few operations the rules use.
    /// Core has no engine reference, so it cannot use <c>UnityEngine.Vector2</c>; Runtime converts
    /// at the boundary (<c>CoreConvert</c>).
    /// </summary>
    public readonly struct Vec2 : IEquatable<Vec2>
    {
        public readonly float X;
        public readonly float Y;

        public Vec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static Vec2 Zero => new Vec2(0f, 0f);
        public static Vec2 One => new Vec2(1f, 1f);

        public float SqrMagnitude => X * X + Y * Y;
        public float Magnitude => MathF.Sqrt(SqrMagnitude);

        public static float Distance(Vec2 a, Vec2 b) => (a - b).Magnitude;

        public Vec2 WithX(float x) => new Vec2(x, Y);
        public Vec2 WithY(float y) => new Vec2(X, y);

        /// <summary>Linear interpolation from <paramref name="a"/> to <paramref name="b"/>; <paramref name="t"/> is not clamped.</summary>
        public static Vec2 LerpUnclamped(Vec2 a, Vec2 b, float t) => new Vec2(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);
        public static Vec2 operator -(Vec2 a) => new Vec2(-a.X, -a.Y);
        public static Vec2 operator *(Vec2 a, float s) => new Vec2(a.X * s, a.Y * s);
        public static Vec2 operator *(float s, Vec2 a) => new Vec2(a.X * s, a.Y * s);
        public static Vec2 operator /(Vec2 a, float s) => new Vec2(a.X / s, a.Y / s);
        public static bool operator ==(Vec2 a, Vec2 b) => a.X == b.X && a.Y == b.Y;
        public static bool operator !=(Vec2 a, Vec2 b) => !(a == b);

        public bool Equals(Vec2 other) => this == other;
        public override bool Equals(object obj) => obj is Vec2 other && this == other;
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public override string ToString() => $"({X:0.####}, {Y:0.####})";
    }
}
