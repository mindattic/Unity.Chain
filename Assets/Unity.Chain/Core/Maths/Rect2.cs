using System;

namespace MindAttic.Chain.Core
{
    /// <summary>Core's own axis-aligned rectangle (origin at the minimum corner, y up), standing in for <c>UnityEngine.Rect</c>.</summary>
    public readonly struct Rect2 : IEquatable<Rect2>
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Width;
        public readonly float Height;

        public Rect2(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public float XMin => X;
        public float YMin => Y;
        public float XMax => X + Width;
        public float YMax => Y + Height;
        public Vec2 Center => new Vec2(X + Width * 0.5f, Y + Height * 0.5f);
        public Vec2 Size => new Vec2(Width, Height);

        public static bool operator ==(Rect2 a, Rect2 b) => a.X == b.X && a.Y == b.Y && a.Width == b.Width && a.Height == b.Height;
        public static bool operator !=(Rect2 a, Rect2 b) => !(a == b);

        public bool Equals(Rect2 other) => this == other;
        public override bool Equals(object obj) => obj is Rect2 other && this == other;
        public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);
        public override string ToString() => $"(x:{X:0.####}, y:{Y:0.####}, w:{Width:0.####}, h:{Height:0.####})";
    }
}
