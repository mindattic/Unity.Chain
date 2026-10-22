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

            public Settings(
                float segmentLength,
                float springConstant,
                float resistance,
                Vector2 gravity,
                float stopVelocity,
                float stopAcceleration)
            {
                SegmentLength = segmentLength;
                SpringConstant = springConstant;
                Resistance = resistance;
                Gravity = gravity;
                StopVelocity = stopVelocity;
                StopAcceleration = stopAcceleration;
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

        public Settings Config;
        public int LinkCount => _positions.Length;

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

            Config = settings;
        }

        public Vector2 GetPosition(int index) => _positions[index];

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
            for (var i = 1; i < _positions.Length; i++)
            {
                var spring = Vector2.zero;
                spring += SpringForce(i, i - 1);
                if (i < _positions.Length - 1)
                {
                    spring += SpringForce(i, i + 1);
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
        }

        private Vector2 SpringForce(int i, int j)
        {
            var delta = _positions[i] - _positions[j];
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
