using System;
using System.Collections.Generic;
using UnityEngine;

namespace MindAttic.Chain
{
    /// <summary>
    /// Mace-vs-enemy hit detection with Physics2D, replacing Chain.vb's single <c>rectMace</c>
    /// AABB test (an axis-aligned box rebuilt each frame at the mace link, checked against each
    /// enemy's box). Every physics step, after <see cref="ChainController"/> has stepped the chain
    /// and <see cref="GrappleController"/> has had its chance to catch, it sweeps a circle the
    /// size of the mace from the mace link's previous position to its current one
    /// (<c>Physics2D.CircleCast</c>),
    /// so a fast swing cannot tunnel through an enemy between steps the way a per-frame box
    /// check can. Each <see cref="Enemy"/> touched is, as in Engine.vb's checkMaceImpact:
    /// caught instead if the grapple is armed with an empty grip, otherwise knocked back at the
    /// mace link's velocity / its mass and damaged by mace speed x <see cref="DamagePerSpeed"/>;
    /// an enemy held by the grapple takes the same damage.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(110)] // after ChainController (0) and GrappleController/MaceController (100)
    public sealed class MaceHitDetector : MonoBehaviour
    {
        [SerializeField] private ChainController chain;
        [Tooltip("Optional: lets an armed grapple catch the enemy the mace touches instead of hitting it.")]
        [SerializeField] private GrappleController grapple;

        [Tooltip("Index of the mace link. Ignored when the chain has a MaceController.")]
        [Min(1)]
        [SerializeField] private int maceIndex = 6;

        [Header("Hit")]
        [Tooltip("Radius of the mace's hit circle, world units.")]
        [Min(0f)]
        [SerializeField] private float hitRadius = 0.25f;
        [SerializeField] private LayerMask enemyLayers = ~0;
        [Tooltip("Damage per world unit per second of mace speed at impact.")]
        [Min(0f)]
        [SerializeField] private float damagePerSpeed = 0.05f;

        private readonly List<RaycastHit2D> _hits = new List<RaycastHit2D>();
        private readonly List<Collider2D> _overlaps = new List<Collider2D>();
        private readonly HashSet<Enemy> _touched = new HashSet<Enemy>();
        private readonly List<Enemy> _ordered = new List<Enemy>();
        private Vector2 _previousPosition;
        private int _previousIndex = -1;

        /// <summary>Raised for each enemy the mace hits, with the damage dealt.</summary>
        public event Action<Enemy, float> Hit;

        public ChainController Chain
        {
            get => chain;
            set
            {
                chain = value;
                _previousIndex = -1;
            }
        }

        public GrappleController Grapple
        {
            get => grapple;
            set => grapple = value;
        }

        public float HitRadius
        {
            get => hitRadius;
            set => hitRadius = Mathf.Max(0f, value);
        }

        public float DamagePerSpeed
        {
            get => damagePerSpeed;
            set => damagePerSpeed = Mathf.Max(0f, value);
        }

        public LayerMask EnemyLayers
        {
            get => enemyLayers;
            set => enemyLayers = value;
        }

        /// <summary>The mace link: the chain's <see cref="MaceController"/> index if it has one, else this component's.</summary>
        public int MaceIndex
        {
            get
            {
                if (chain == null)
                {
                    return maceIndex;
                }

                var mace = chain.GetComponent<MaceController>();
                return mace != null ? mace.MaceIndex : Mathf.Clamp(maceIndex, 1, Mathf.Max(1, chain.LinkCount - 1));
            }
            set => maceIndex = Mathf.Max(1, value);
        }

        public Vector2 MacePosition => chain.GetLinkPosition(MaceIndex);
        public Vector2 MaceVelocity => chain.GetLinkVelocity(MaceIndex);

        /// <summary>
        /// One hit-detection step. Called from <c>FixedUpdate</c>; public so tests can drive it.
        /// Returns the number of enemies hit (caught ones are not counted).
        /// </summary>
        public int Step()
        {
            if (chain == null)
            {
                return 0;
            }

            var index = MaceIndex;
            var position = MacePosition;

            // First step, or the chain was shortened/lengthened: no sweep from a different link.
            var from = index == _previousIndex ? _previousPosition : position;
            _previousIndex = index;
            _previousPosition = position;

            CollectEnemies(from, position);
            if (_ordered.Count == 0)
            {
                return 0;
            }

            var velocity = MaceVelocity;
            var held = grapple != null ? grapple.Target as Enemy : null;
            var hits = 0;

            foreach (var enemy in _ordered)
            {
                if (enemy == null || !enemy.CanBeHit)
                {
                    continue;
                }

                if (grapple != null && grapple.State == GrappleState.Armed && grapple.TryCatch(enemy))
                {
                    held = enemy;
                    continue;
                }

                var damage = enemy.TakeHit(velocity, damagePerSpeed);
                hits++;
                Hit?.Invoke(enemy, damage);

                if (held != null && held != enemy)
                {
                    held.TakeDamage(damage);
                }
            }

            return hits;
        }

        /// <summary>Enemies the mace circle touches between <paramref name="from"/> and <paramref name="to"/>, nearest the start first.</summary>
        private void CollectEnemies(Vector2 from, Vector2 to)
        {
            _touched.Clear();
            _ordered.Clear();

            var filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = enemyLayers,
                useTriggers = true,
            };

            var delta = to - from;
            var distance = delta.magnitude;
            if (distance > 1e-5f)
            {
                _hits.Clear();
                Physics2D.CircleCast(from, hitRadius, delta / distance, filter, _hits, distance);
                _hits.Sort((a, b) => a.fraction.CompareTo(b.fraction));
                foreach (var hit in _hits)
                {
                    Add(hit.collider);
                }
            }

            // Whatever the mace rests in now (also covers a sweep that started inside a collider).
            _overlaps.Clear();
            Physics2D.OverlapCircle(to, hitRadius, filter, _overlaps);
            foreach (var overlap in _overlaps)
            {
                Add(overlap);
            }
        }

        private void Add(Collider2D collider)
        {
            if (collider == null)
            {
                return;
            }

            var enemy = collider.GetComponentInParent<Enemy>();
            if (enemy != null && _touched.Add(enemy))
            {
                _ordered.Add(enemy);
            }
        }

        private void Reset()
        {
            chain = GetComponent<ChainController>();
            grapple = GetComponent<GrappleController>();
        }

        private void Awake()
        {
            if (chain == null)
            {
                chain = GetComponent<ChainController>();
            }

            if (grapple == null)
            {
                grapple = GetComponent<GrappleController>();
            }
        }

        private void FixedUpdate() => Step();

        private void OnDrawGizmosSelected()
        {
            if (chain == null)
            {
                return;
            }

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(MacePosition, hitRadius);
        }
    }
}
