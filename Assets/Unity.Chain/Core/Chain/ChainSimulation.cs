namespace MindAttic.Chain.Core
{
    /// <summary>
    /// Spring-chain physics ported from Philip Winston's "Elastic Trail" (2000) and the
    /// Chain.vb/DirectX game built on it (2005). See docs/chain-physics.md for the source
    /// reading and the two fixes made versus the original VB.NET port (both-neighbor spring
    /// pull, and dt-scaled integration instead of per-frame units).
    /// </summary>
    public sealed class ChainSimulation
    {
        public readonly struct Settings
        {
            public readonly float SegmentLength;
            public readonly float SpringConstant;
            public readonly float Resistance;
            public readonly Vec2 Gravity;
            public readonly float StopVelocity;
            public readonly float StopAcceleration;

            /// <summary>
            /// Multiplier on a link's upward spring pull while that link is above the anchor
            /// (Chain.vb's "boost for mace when overhead", which doubles it). 1 = no boost.
            /// </summary>
            public readonly float OverheadBoost;

            /// <summary>
            /// How far below the anchor a link still counts as "overhead" (Chain.vb uses one
            /// link size: <c>link(l).Y &gt; link(0).Y - linkSize</c>).
            /// </summary>
            public readonly float OverheadMargin;

            public Settings(
                float segmentLength,
                float springConstant,
                float resistance,
                Vec2 gravity,
                float stopVelocity,
                float stopAcceleration,
                float overheadBoost = 1f,
                float overheadMargin = 0f)
            {
                SegmentLength = segmentLength;
                SpringConstant = springConstant;
                Resistance = resistance;
                Gravity = gravity;
                StopVelocity = stopVelocity;
                StopAcceleration = stopAcceleration;
                OverheadBoost = overheadBoost;
                OverheadMargin = overheadMargin;
            }

            public static Settings Default => new Settings(
                segmentLength: 0.2f,
                springConstant: 60f,
                resistance: 4f,
                gravity: new Vec2(0f, -9.8f),
                stopVelocity: 0.0005f,
                stopAcceleration: 0.0005f);
        }

        private readonly Vec2[] _positions;
        private readonly Vec2[] _velocities;
        private readonly float[] _mass;
        private int _activeLinkCount;

        public Settings Config;
        public int LinkCount => _positions.Length;

        /// <summary>
        /// Optional walls (the level's play space). When set, a simulated link that leaves the
        /// rect is put back on the edge and bounces off it with <see cref="WallBounce"/>, as in
        /// Elastic Trail (Chain.vb kept the code but commented it out). Null = unbounded.
        /// The anchor is not constrained: it follows the hand, which the angel keeps in bounds.
        /// </summary>
        public Rect2? Bounds;

        /// <summary>Fraction of a link's speed into a wall kept after bouncing off it (0-1).</summary>
        public float WallBounce = 0.5f;

        /// <summary>
        /// Chain.vb's <c>followMouse</c>: while false every link's spring force is zero, so the
        /// links feel only gravity and drag and the chain falls away from the hand. Toggled by
        /// <see cref="ChainFollow.Apply"/>; see <see cref="ChainFollow"/> for the mace rules.
        /// </summary>
        public bool IsFollowing = ChainFollow.StartsFollowing;

        /// <summary>
        /// How many links (anchor included) are live. Links past this are not simulated: they
        /// ride along parked on the last active link, so lengthening the chain pays a new link
        /// out from the current end instead of snapping one in from wherever it was left. This is
        /// how the mace's chain is shortened/lengthened without rebuilding (Chain.vb keeps all 25
        /// links and only draws up to <c>Mace</c>). Clamped to [2, LinkCount].
        /// </summary>
        public int ActiveLinkCount
        {
            get => _activeLinkCount;
            set => _activeLinkCount = Calc.Clamp(value, 2, _positions.Length);
        }

        public ChainSimulation(int linkCount, Vec2 anchor, Settings settings)
        {
            if (linkCount < 2)
            {
                throw new System.ArgumentOutOfRangeException(nameof(linkCount), "A chain needs at least an anchor and one link.");
            }

            _positions = new Vec2[linkCount];
            _velocities = new Vec2[linkCount];
            _mass = new float[linkCount];

            for (var i = 0; i < linkCount; i++)
            {
                _positions[i] = anchor;
                _velocities[i] = Vec2.Zero;
                _mass[i] = 1f;
            }

            _activeLinkCount = linkCount;
            Config = settings;
        }

        public Vec2 GetPosition(int index) => _positions[index];

        /// <summary>Velocity of a link after the last step, world units per second.</summary>
        public Vec2 GetVelocity(int index) => _velocities[index];

        public float GetMass(int index) => _mass[index];

        public void SetMass(int index, float mass) => _mass[index] = mass;

        /// <summary>Link 0 is never simulated: it is snapped to whatever it should follow (the
        /// player's hand socket) every step, exactly as link(0) tracks the angel in Chain.vb.</summary>
        public void SetAnchor(Vec2 position)
        {
            _positions[0] = position;
            _velocities[0] = Vec2.Zero;
        }

        public void Step(float deltaTime)
        {
            var end = _activeLinkCount;
            var anchorY = _positions[0].Y;
            for (var i = 1; i < end; i++)
            {
                var spring = Vec2.Zero;
                if (IsFollowing)
                {
                    spring += SpringForce(i, i - 1);
                    if (i < end - 1)
                    {
                        spring += SpringForce(i, i + 1);
                    }
                }

                // Overhead boost: an upward pull on a link that is above the anchor is amplified,
                // so a mace swung overhead carries up and over instead of drooping (Chain.vb).
                if (spring.Y > 0f && _positions[i].Y > anchorY - Config.OverheadMargin)
                {
                    spring = spring.WithY(spring.Y * Config.OverheadBoost);
                }

                var resist = _velocities[i] * Config.Resistance;
                var accel = (spring - resist) / _mass[i] + Config.Gravity;

                var velocity = _velocities[i] + accel * deltaTime;
                if (velocity.SqrMagnitude < Config.StopVelocity * Config.StopVelocity &&
                    accel.SqrMagnitude < Config.StopAcceleration * Config.StopAcceleration)
                {
                    velocity = Vec2.Zero;
                }

                var position = _positions[i] + velocity * deltaTime;
                if (Bounds.HasValue)
                {
                    PlayArea.Bounce(Bounds.Value, ref position, ref velocity, WallBounce);
                }

                _velocities[i] = velocity;
                _positions[i] = position;
            }

            // Park the inactive tail on the current end link.
            for (var i = end; i < _positions.Length; i++)
            {
                _positions[i] = _positions[end - 1];
                _velocities[i] = _velocities[end - 1];
            }
        }

        private Vec2 SpringForce(int i, int j)
        {
            // Pull i toward j: the force points from i to its neighbor.
            var delta = _positions[j] - _positions[i];
            var length = delta.Magnitude;
            if (length <= Config.SegmentLength || length <= Calc.Epsilon)
            {
                return Vec2.Zero;
            }

            var springForce = Config.SpringConstant * (length - Config.SegmentLength);
            return (delta / length) * springForce;
        }
    }
}
