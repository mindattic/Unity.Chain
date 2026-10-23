

namespace MindAttic.Chain.Core
{
    public enum EnemyState
    {
        /// <summary>Homing on the angel (Enemy.vb's Seek AI).</summary>
        Seek,

        /// <summary>Knocked back by the mace or a thrown enemy; coasts and slows, then seeks again.</summary>
        Hurt,

        /// <summary>In the grapple's grip; the grapple moves it.</summary>
        Grabbed,

        /// <summary>HP ran out.</summary>
        Dead,
    }

    /// <summary>
    /// One enemy's rules from Enemy.vb/Engine.vb, free of Unity physics so it can be tested:
    /// it seeks the angel with a velocity proportional to the distance; a mace hit sets its
    /// velocity to the mace's velocity divided by its mass, takes HP, and leaves it Hurt (it
    /// cannot be hit or grabbed again until it has slowed to a stop); a thrown enemy is Hurt
    /// too and knocks any enemy it touches the same way, without damage. Velocities are world
    /// units per second.
    /// </summary>
    public sealed class EnemyBrain
    {
        /// <summary>Enemy.vb flockSpd: the original's push, world units per axis per frame (the port's units are the original's, as for seekSpd 0.006 to 0.36).</summary>
        public const float OriginalFlockPerFrame = 0.0005f;

        /// <summary>The frame rate the original's per-frame figures are converted at (BIBLE D9).</summary>
        public const float OriginalFrameRate = 60f;

        /// <summary>
        /// Engine.vb checkFlocking loops d and c over every enemy, so each overlapping pair is visited twice
        /// (d,c then c,d), pushing both the same way each time: an enemy moves 2 x flockSpd per frame per neighbour.
        /// </summary>
        public const float OriginalFlockPairVisits = 2f;

        /// <summary>Per-neighbour push speed (units/s per axis) equal to the original's: 0.0005 x 2 x 60 = 0.06.</summary>
        public const float DefaultFlockSpeed = OriginalFlockPerFrame * OriginalFlockPairVisits * OriginalFrameRate;

        public struct Settings
        {
            /// <summary>Seek velocity per unit of distance to the target, per second (Enemy.vb seekSpd 0.006/frame).</summary>
            public float SeekRate;

            /// <summary>How fast a Hurt enemy slows on each axis, units per second squared (Enemy.vb resistSPD).</summary>
            public float HurtDeceleration;

            /// <summary>A Hurt enemy slower than this on both axes recovers and seeks again.</summary>
            public float RecoverSpeed;

            public static Settings Default => new Settings
            {
                SeekRate = 0.36f,
                HurtDeceleration = 6f,
                RecoverSpeed = 0.05f,
            };
        }

        public Settings Config;

        public float MaxHp { get; }
        public float Hp { get; private set; }
        public float Mass { get; }
        public EnemyState State { get; private set; } = EnemyState.Seek;
        public Vec2 Velocity { get; private set; }

        /// <summary>True from a throw until the enemy recovers: it knocks other enemies it touches.</summary>
        public bool IsThrown { get; private set; }

        public bool IsAlive => State != EnemyState.Dead;

        /// <summary>Enemy.vb only lets the mace hit (or grab) an enemy that is alive, not Hurt and not held.</summary>
        public bool CanBeHit => State == EnemyState.Seek;

        public EnemyBrain(float hp = 1f, float mass = 1f, Settings? settings = null)
        {
            MaxHp = Calc.Max(0.0001f, hp);
            Hp = MaxHp;
            Mass = Calc.Max(0.01f, mass);
            Config = settings ?? Settings.Default;
        }

        /// <summary>
        /// Advances the AI one step and returns the velocity to move with. Seek heads for
        /// <paramref name="seekPoint"/>; Hurt slows each axis toward zero and recovers below
        /// <see cref="Settings.RecoverSpeed"/>. Grabbed and Dead do not move on their own.
        /// </summary>
        public Vec2 Step(Vec2 position, Vec2 seekPoint, float deltaTime)
        {
            switch (State)
            {
                case EnemyState.Seek:
                    Velocity = (seekPoint - position) * Config.SeekRate;
                    break;

                case EnemyState.Hurt:
                    var slow = Config.HurtDeceleration * deltaTime;
                    var v = new Vec2(
                        Calc.MoveTowards(Velocity.X, 0f, slow),
                        Calc.MoveTowards(Velocity.Y, 0f, slow));
                    Velocity = v;
                    if (Calc.Abs(v.X) <= Config.RecoverSpeed && Calc.Abs(v.Y) <= Config.RecoverSpeed)
                    {
                        State = EnemyState.Seek;
                        IsThrown = false;
                    }

                    break;

                default:
                    Velocity = Vec2.Zero;
                    break;
            }

            return Velocity;
        }

        /// <summary>
        /// A mace hit at <paramref name="impactVelocity"/>: knocked back at impact / mass and
        /// damaged by impact speed x <paramref name="damagePerSpeed"/>. Returns the damage dealt
        /// (0 if it could not be hit).
        /// </summary>
        public float Hit(Vec2 impactVelocity, float damagePerSpeed)
        {
            if (!CanBeHit)
            {
                return 0f;
            }

            var damage = impactVelocity.Magnitude * Calc.Max(0f, damagePerSpeed);
            Knock(impactVelocity);
            Damage(damage);
            return damage;
        }

        /// <summary>Knocked by something (a thrown enemy) without damage: velocity = impact / mass, Hurt.</summary>
        public void Knock(Vec2 impactVelocity)
        {
            if (!IsAlive || State == EnemyState.Grabbed)
            {
                return;
            }

            Velocity = impactVelocity / Mass;
            State = EnemyState.Hurt;
        }

        /// <summary>Takes HP; at zero the enemy is Dead. Returns true if this killed it.</summary>
        public bool Damage(float amount)
        {
            if (!IsAlive || amount <= 0f)
            {
                return false;
            }

            Hp = Calc.Max(0f, Hp - amount);
            if (Hp > 0f)
            {
                return false;
            }

            State = EnemyState.Dead;
            Velocity = Vec2.Zero;
            IsThrown = false;
            return true;
        }

        /// <summary>
        /// Engine.vb checkFlocking: the push on an enemy at <paramref name="self"/> overlapping one
        /// at <paramref name="other"/>, <paramref name="speed"/> x <paramref name="deltaTime"/> per
        /// axis away from it. On an axis where they are level the push goes by
        /// <paramref name="tieBreak"/> (+1 or -1, opposite for the two enemies) so a perfect stack
        /// still comes apart.
        /// </summary>
        public static Vec2 FlockPush(Vec2 self, Vec2 other, float speed, float deltaTime, int tieBreak = 1)
        {
            var step = Calc.Max(0f, speed) * deltaTime;
            return new Vec2(AwaySign(self.X - other.X, tieBreak) * step, AwaySign(self.Y - other.Y, tieBreak) * step);
        }

        private static float AwaySign(float difference, int tieBreak)
        {
            if (Calc.Abs(difference) < 1e-6f)
            {
                return tieBreak < 0 ? -1f : 1f;
            }

            return Calc.Sign(difference);
        }

        /// <summary>Caught by the grapple. Returns false if it cannot be caught.</summary>
        public bool Grab()
        {
            if (!CanBeHit)
            {
                return false;
            }

            State = EnemyState.Grabbed;
            Velocity = Vec2.Zero;
            return true;
        }

        /// <summary>Thrown by the grapple at <paramref name="velocity"/> (not divided by mass: the mace carried it).</summary>
        public void Throw(Vec2 velocity)
        {
            if (State != EnemyState.Grabbed)
            {
                return;
            }

            Velocity = velocity;
            State = EnemyState.Hurt;
            IsThrown = true;
        }
    }
}
