using UnityEngine;

namespace MindAttic.Chain
{
    /// <summary>
    /// Item.vb's one Health10 heart: it floats down with a sway (<see cref="HeartFloat"/>), wraps to
    /// the top when it leaves the bottom, and gives <see cref="HealAmount"/> HP (capped at max HP) when
    /// the angel touches it; <see cref="GameSession"/> does the touch check and the healing. After a
    /// pickup the heart goes back to the top of the play area so it keeps drifting down.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeartPickup : MonoBehaviour
    {
        [Header("Level")]
        [Tooltip("Play space the heart drifts in. Falls back to the scene's LevelBounds, then the fallback rect.")]
        [SerializeField] private LevelBounds levelBounds;
        [SerializeField] private Rect fallbackBounds = new Rect(-8f, -4.5f, 16f, 9f);

        [Header("Heal")]
        [Min(0)]
        [SerializeField] private int healAmount = 10;
        [Tooltip("Radius around the heart that counts as the angel touching it, world units.")]
        [Min(0f)]
        [SerializeField] private float touchRadius = 0.4f;

        [Header("Float (Item.vb)")]
        [SerializeField] private float floatSpeed = 0.002f;
        [SerializeField] private float floatAcceleration = 0.00001f;

        private HeartFloat _float;

        public int HealAmount => healAmount;
        public float TouchRadius => touchRadius;

        public LevelBounds LevelBounds
        {
            get => levelBounds;
            set => levelBounds = value;
        }

        public Rect Area => levelBounds != null ? levelBounds.Bounds : fallbackBounds;

        /// <summary>True when <paramref name="angelPosition"/> is within the touch radius.</summary>
        public bool IsTouching(Vector2 angelPosition) =>
            ((Vector2)transform.position - angelPosition).sqrMagnitude <= touchRadius * touchRadius;

        /// <summary>Puts the heart back at the top of the play area (after a pickup), keeping its x.</summary>
        public void Respawn()
        {
            var p = transform.position;
            transform.position = new Vector3(p.x, Area.yMax, p.z);
        }

        /// <summary>One drift step; public so tests can drive it. Returns true if it wrapped.</summary>
        public bool Step(float deltaTime)
        {
            _float = _float ?? new HeartFloat();
            _float.Config = new HeartFloat.Settings { FloatSpeed = floatSpeed, FloatAcceleration = floatAcceleration };
            var position = (Vector2)transform.position;
            var wrapped = _float.Step(ref position, Area, deltaTime);
            transform.position = new Vector3(position.x, position.y, transform.position.z);
            return wrapped;
        }

        private void Awake()
        {
            if (levelBounds == null)
            {
                levelBounds = LevelBounds.Find();
            }
        }

        private void Update() => Step(Time.deltaTime);
    }
}
