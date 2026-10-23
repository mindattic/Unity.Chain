using System;
using System.Collections.Generic;
using UnityEngine;

namespace MindAttic.Chain
{
    /// <summary>
    /// An enemy (Enemy.vb): a kinematic <see cref="Rigidbody2D"/> with a <see cref="Collider2D"/>
    /// driven by <see cref="EnemyBrain"/>. It seeks the angel, is knocked back and damaged by
    /// mace hits (<see cref="MaceHitDetector"/>), can be caught and thrown by the grapple (it is
    /// its own <see cref="IGrappleTarget"/>, so do not add a <see cref="GrappleTarget"/> too), and
    /// while thrown knocks any enemy its collider touches. It is destroyed when its HP runs out.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class Enemy : MonoBehaviour, IGrappleTarget
    {
        [Header("Stats")]
        [Min(0.0001f)]
        [SerializeField] private float hp = 1f;
        [Tooltip("Random mass range (Enemy.vb: 1-2). Knockback is divided by mass.")]
        [SerializeField] private Vector2 massRange = new Vector2(1f, 2f);

        [Header("Movement")]
        [Tooltip("What the enemy seeks. Defaults to the scene's AngelController.")]
        [SerializeField] private Transform target;
        [Tooltip("Offset from the target that the enemy homes on.")]
        [SerializeField] private Vector2 seekOffset = Vector2.zero;
        [Min(0f)]
        [SerializeField] private float seekRate = 0.36f;
        [Min(0f)]
        [SerializeField] private float hurtDeceleration = 6f;
        [Min(0f)]
        [SerializeField] private float recoverSpeed = 0.05f;

        [Header("Flocking")]
        [Tooltip("Engine.vb flockSpd: speed (units/s, per axis) at which overlapping live, non-grabbed enemies push each other apart. 0 turns separation off.")]
        [Min(0f)]
        [SerializeField] private float flockSpeed = 1.5f;

        [Header("Death")]
        [Tooltip("Seconds after death before the GameObject is destroyed (play mode).")]
        [Min(0f)]
        [SerializeField] private float destroyDelay = 0f;

        private readonly List<Collider2D> _overlaps = new List<Collider2D>();
        private EnemyBrain _brain;
        private Rigidbody2D _body;

        /// <summary>Raised on each mace hit with the damage dealt.</summary>
        public event Action<Enemy, float> Hurt;

        /// <summary>Raised once when the enemy's HP runs out.</summary>
        public event Action<Enemy> Died;

        public EnemyBrain Brain => _brain ?? (_brain = CreateBrain());
        public EnemyState State => Brain.State;
        public float Hp => Brain.Hp;
        public bool IsAlive => Brain.IsAlive;
        public bool CanBeHit => isActiveAndEnabled && Brain.CanBeHit;
        public Vector2 Velocity => Brain.Velocity;
        public Vector2 Position => Body.position;

        public Transform Target
        {
            get => target;
            set => target = value;
        }

        /// <summary>Starting HP. Only takes effect before the brain is created (first use).</summary>
        public float StartingHp
        {
            get => hp;
            set
            {
                hp = Mathf.Max(0.0001f, value);
                _brain = null;
            }
        }

        /// <summary>Fixes the mass (both ends of the random range). Takes effect like <see cref="StartingHp"/>.</summary>
        public float FixedMass
        {
            set
            {
                massRange = new Vector2(value, value);
                _brain = null;
            }
        }

        private Rigidbody2D Body => _body != null ? _body : _body = GetComponent<Rigidbody2D>();

        // --- IGrappleTarget -------------------------------------------------------------

        public bool CanBeGrappled => CanBeHit;

        public void OnGrappled() => Brain.Grab();

        public void HoldAt(Vector2 position) => MoveTo(position);

        public void Throw(Vector2 velocity) => Brain.Throw(velocity);

        // --- Combat ---------------------------------------------------------------------

        /// <summary>
        /// A mace hit: knockback at <paramref name="maceVelocity"/> / mass and damage of mace
        /// speed x <paramref name="damagePerSpeed"/>. Returns the damage dealt (0 if it could not
        /// be hit).
        /// </summary>
        public float TakeHit(Vector2 maceVelocity, float damagePerSpeed)
        {
            if (!CanBeHit)
            {
                return 0f;
            }

            var damage = Brain.Hit(maceVelocity, damagePerSpeed);
            Hurt?.Invoke(this, damage);
            if (!Brain.IsAlive)
            {
                Die();
            }

            return damage;
        }

        /// <summary>Takes damage without knockback (a held enemy when the mace hits something). Returns true if it died.</summary>
        public bool TakeDamage(float amount)
        {
            if (!isActiveAndEnabled || !Brain.Damage(amount))
            {
                return false;
            }

            Die();
            return true;
        }

        /// <summary>One AI step: move by the brain's velocity; while thrown, knock enemies touched.</summary>
        public void Step(float deltaTime)
        {
            if (!Brain.IsAlive || Brain.State == EnemyState.Grabbed)
            {
                return;
            }

            var seekPoint = (target != null ? (Vector2)target.position : Position) + seekOffset;
            var velocity = Brain.Step(Position, seekPoint, deltaTime);
            MoveTo(Position + velocity * deltaTime + FlockingPush(deltaTime));

            if (Brain.IsThrown)
            {
                KnockTouchedEnemies();
            }
        }

        /// <summary>Enemy-vs-enemy separation (Engine.vb checkFlocking): pushed away from each live, non-grabbed enemy this one overlaps.</summary>
        private Vector2 FlockingPush(float deltaTime)
        {
            if (flockSpeed <= 0f)
            {
                return Vector2.zero;
            }

            var push = Vector2.zero;
            var counted = new HashSet<Enemy>();
            var filter = new ContactFilter2D { useTriggers = true };
            filter.NoFilter();

            foreach (var collider in GetComponentsInChildren<Collider2D>())
            {
                if (!collider.enabled)
                {
                    continue;
                }

                _overlaps.Clear();
                collider.Overlap(filter, _overlaps);
                foreach (var hit in _overlaps)
                {
                    var other = hit.GetComponentInParent<Enemy>();
                    if (other == null || other == this || !other.isActiveAndEnabled || !other.IsAlive
                        || other.State == EnemyState.Grabbed || !counted.Add(other))
                    {
                        continue;
                    }

                    var tieBreak = GetInstanceID() < other.GetInstanceID() ? 1 : -1;
                    push += EnemyBrain.FlockPush(Position, other.Position, flockSpeed, deltaTime, tieBreak);
                }
            }

            return push;
        }

        private void KnockTouchedEnemies()
        {
            var filter = new ContactFilter2D { useTriggers = true };
            filter.NoFilter();

            foreach (var collider in GetComponentsInChildren<Collider2D>())
            {
                if (!collider.enabled)
                {
                    continue;
                }

                _overlaps.Clear();
                collider.Overlap(filter, _overlaps);
                foreach (var hit in _overlaps)
                {
                    var other = hit.GetComponentInParent<Enemy>();
                    if (other != null && other != this && other.CanBeHit)
                    {
                        other.Brain.Knock(Brain.Velocity);
                    }
                }
            }
        }

        private void MoveTo(Vector2 position)
        {
            if (Application.isPlaying)
            {
                Body.MovePosition(position);
            }
            else
            {
                Body.position = position;
                transform.position = new Vector3(position.x, position.y, transform.position.z);
            }
        }

        private void Die()
        {
            Died?.Invoke(this);

            foreach (var collider in GetComponentsInChildren<Collider2D>())
            {
                collider.enabled = false;
            }

            if (Application.isPlaying)
            {
                Destroy(gameObject, destroyDelay);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private EnemyBrain CreateBrain()
        {
            var lo = Mathf.Min(massRange.x, massRange.y);
            var hi = Mathf.Max(massRange.x, massRange.y);
            var mass = Mathf.Approximately(lo, hi) ? lo : UnityEngine.Random.Range(lo, hi);
            return new EnemyBrain(hp, mass, new EnemyBrain.Settings
            {
                SeekRate = seekRate,
                HurtDeceleration = hurtDeceleration,
                RecoverSpeed = recoverSpeed,
            });
        }

        private void Reset()
        {
            GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        }

        private void Awake()
        {
            Body.bodyType = RigidbodyType2D.Kinematic;
            Body.gravityScale = 0f;

            if (target == null)
            {
                var angel = FindAnyObjectByType<AngelController>();
                if (angel != null)
                {
                    target = angel.transform;
                }
            }
        }

        private void FixedUpdate() => Step(Time.fixedDeltaTime);

        private void OnValidate()
        {
            if (_brain != null)
            {
                var config = _brain.Config;
                config.SeekRate = seekRate;
                config.HurtDeceleration = hurtDeceleration;
                config.RecoverSpeed = recoverSpeed;
                _brain.Config = config;
            }
        }
    }
}
