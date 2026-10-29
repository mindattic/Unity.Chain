namespace MindAttic.Chain.Core
{
    /// <summary>
    /// What a contact is tested with. <see cref="Rectangle"/> (the default, U.CHN-T88) is the original's own: Angel.vb's,
    /// Enemy.vb's and Chain.vb's rectangles through Root.vb's <c>rectIntersect</c>. <see cref="Circle"/> is the port's earlier
    /// circle (BIBLE V5, D12), kept only as a data option.
    /// </summary>
    public enum HitShape
    {
        Rectangle,
        Circle,
    }

    /// <summary>
    /// A hit rectangle as offsets from the thing it hangs on (Y up, <see cref="Top"/> above <see cref="Bottom"/>):
    /// Angel.vb's from the angel's X, Y; Enemy.vb's from the enemy's centre; Chain.vb's rectMace from the mace link.
    /// </summary>
    public readonly struct HitBox
    {
        public readonly float Left;
        public readonly float Right;
        public readonly float Top;
        public readonly float Bottom;

        public HitBox(float left, float right, float top, float bottom)
        {
            Left = left;
            Right = right;
            Top = top;
            Bottom = bottom;
        }

        /// <summary>The rectangle with its anchor at <paramref name="anchor"/>.</summary>
        public HeartRect At(Vec2 anchor) => new HeartRect(anchor.X + Left, anchor.Y + Top, anchor.X + Right, anchor.Y + Bottom);

        /// <summary>The smaller of the box's width and height.</summary>
        public float MinSide => Calc.Min(Right - Left, Top - Bottom);
    }
}
