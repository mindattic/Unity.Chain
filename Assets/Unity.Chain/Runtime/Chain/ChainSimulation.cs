using UnityEngine;

namespace MindAttic.Chain
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
            public readonly Vector2 Gravity;
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
                Vector2 gravity,
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
                gravity: new Vector2(0f, -9.8f),
                stopVelocity: 0.0005f,
                stopAcceleration: 0.0005f);
        }

        private readonly Vector2[] _positions;
        private readonly Vector2[] _velocities;
        private readonly float[] _mass;
        private int _activeLinkCount;

        public Settings Config;
        public int LinkCount => _positions.Length;

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
            set => _activeLinkCount = Mathf.Clamp(value, 2, _positions.Length);
        }

        public ChainSimulation(int linkCount, Vector2 anchor, Settings settings)
        {
            if (linkCount < 2)
            {
                throw new System.ArgumentOutOfRangeException(nameof(linkCount), "A chain needs at least an anchor and one link.");
            }

            _positions = new Vector2[linkCount];
            _velocities = new Vector2[linkCount];
            _mass = new float[linkCount];

            for (var i = 0; i < linkCount; i++)
            {
                _positions[i] = anchor;
                _velocities[i] = Vector2.zero;
                _mass[i] = 1f;
            }

            _activeLinkCount = linkCount;
            Config = settings;
        }

        public Vector2 GetPosition(int index) => _positions[index];

        /// <summary>Velocity of a link after the last step, world units per second.</summary>
        public Vector2 GetVelocity(int index) => _velocities[index];

        public float GetMass(int index) => _mass[index];

        public void SetMass(int index, float mass) => _mass[index] = mass;

        /// <summary>Link 0 is never simulated: it is snapped to whatever it should follow (the
        /// player's hand socket) every step, exactly as link(0) tracks the angel in Chain.vb.</summary>
        public void SetAnchor(Vector2 position)
        {
            _positions[0] = position;
            _velocities[0] = Vector2.zero;
        }

        public void Step(float deltaTime)
        {
            var end = _activeLinkCount;
            var anchorY = _positions[0].y;
            for (var i = 1; i < end; i++)
            {
                var spring = Vector2.zero;
                spring += SpringForce(i, i - 1);
                if (i < end - 1)
                {
                    spring += SpringForce(i, i + 1);
                }

                // Overhead boost: an upward pull on a link that is above the anchor is amplified,
                // so a mace swung overhead carries up and over instead of drooping (Chain.vb).
                if (spring.y > 0f && _positions[i].y > anchorY - Config.OverheadMargin)
                {
                    spring.y *= Config.OverheadBoost;
                }

                var resist = _velocities[i] * Config.Resistance;
                var accel = (spring - resist) / _mass[i] + Config.Gravity;

                var velocity = _velocities[i] + accel * deltaTime;
                if (velocity.sqrMagnitude < Config.StopVelocity * Config.StopVelocity &&
                    accel.sqrMagnitude < Config.StopAcceleration * Config.StopAcceleration)
                {
                    velocity = Vector2.zero;
                }

                _velocities[i] = velocity;
                _positions[i] += velocity * deltaTime;
            }

            // Park the inactive tail on the current end link.
            for (var i = end; i < _positions.Length; i++)
            {
                _positions[i] = _positions[end - 1];
                _velocities[i] = _velocities[end - 1];
            }
        }

        private Vector2 SpringForce(int i, int j)
        {
            // Pull i toward j: the force points from i to its neighbor.
            var delta = _positions[j] - _positions[i];
            var length = delta.magnitude;
            if (length <= Config.SegmentLength || length <= Mathf.Epsilon)
            {
                return Vector2.zero;
            }

            var springForce = Config.SpringConstant * (length - Config.SegmentLength);
            return (delta / length) * springForce;
        }
    }
}
