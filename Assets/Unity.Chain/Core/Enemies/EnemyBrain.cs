

namespace MindAttic.Chain.Core
{
    public enum EnemyState
    {
        /// <summary>Homing on the angel (Enemy.vb's Seek AI). Only the world's Seek cap of enemies do at once.</summary>
        Seek,

        /// <summary>
        /// Waiting for a Seek slot, too close: backs away from the angel at 6 x seekSpd x distance / mass
        /// (Enemy.vb's Cautious AI, its sign fixed, BIBLE D18) until it is out at the hang-back's far edge.
        /// </summary>
        Cautious,

        /// <summary>
        /// Waiting for a Seek slot, out of range: stalks in at seekSpd on each axis (Enemy.vb's Align AI)
        /// until it reaches the hang-back's near edge.
        /// </summary>
        Align,

        /// <summary>Knocked back by the mace or a thrown enemy; coasts and slows, then seeks again.</summary>
        Hurt,

        /// <summary>In the grapple's grip; the grapple moves it.</summary>
        Grabbed,

        /// <summary>HP ran out.</summary>
        Dead,
    }

    /// <summary>
    /// Which formula a mace hit computes its damage with (chain.json's <c>port</c>, U.CHN-T45, BIBLE D11).
    /// </summary>
    public enum MaceDamageFormula
    {
        /// <summary>
        /// Engine.vb's <c>checkMaceImpact</c>: <c>abs(dX + dY/2)</c> of the mass-divided impact velocity, in
        /// the original's own per-frame screen-space units (<see cref="PlayerSetup.VelocityToOriginalScale"/>
        /// converts back to them). Cancels to zero on certain diagonals, as the original's did (BIBLE D11);
        /// this is the default, the original's own behaviour.
        /// </summary>
        Original,

        /// <summary>The rebuild's own earlier formula: impact speed (the raw, not mass-divided, magnitude) x a damage-per-speed figure. Kept only where asked for explicitly.</summary>
        SpeedTimesFactor,
    }

    /// <summary>
    /// One enemy's rules from Enemy.vb/Engine.vb, free of Unity physics so it can be tested:
    /// it seeks the angel (or, while the world's Seek cap is full, hangs back in Cautious or stalks in
    /// Align, UNI-T65) with a velocity proportional to the distance; a mace hit sets its
    /// velocity to the mace's velocity divided by its mass, takes HP, and leaves it Hurt (it
    /// cannot be hit or grabbed again until it has slowed to a stop); a thrown enemy is Hurt
    /// too and knocks any enemy it touches the same way, without damage. Velocities are world
    /// units per second.
    /// </summary>
    public sealed class EnemyBrain
    {
        /// <summary>Enemy.vb flockSpd: the original's push, screen units per axis per frame (x the arena length scale for world units).</summary>
        public const float OriginalFlockPerFrame = 0.0005f;

        /// <summary>The frame rate the original's per-frame figures are converted at (BIBLE D9).</summary>
        public const float OriginalFrameRate = 60f;

        /// <summary>
        /// Engine.vb checkFlocking loops d and c over every enemy, so each overlapping pair is visited twice
        /// (d,c then c,d), pushing both the same way each time: an enemy moves 2 x flockSpd per frame per neighbour.
        /// </summary>
        public const float OriginalFlockPairVisits = 2f;

        /// <summary>Per-neighbour push speed (units/s per axis) equal to the original's: 0.0005 x 2 x 60 x 9 (the arena length scale) = 0.54.</summary>
        public const float DefaultFlockSpeed = OriginalFlockPerFrame * OriginalFlockPairVisits * OriginalFrameRate * OriginalSpace.ArenaLengthScale;

        public struct Settings
        {
            /// <summary>Seek velocity per unit of distance to the target, per second (Enemy.vb seekSpd 0.006/frame).</summary>
            public float SeekRate;

            /// <summary>How fast a Hurt enemy slows on each axis, units per second squared (Enemy.vb resistSPD).</summary>
            public float HurtDeceleration;

            /// <summary>A Hurt enemy slower than this on both axes recovers and seeks again.</summary>
            public float RecoverSpeed;

            /// <summary>Cautious: backing-away velocity per unit of distance from the angel before mass divides it, per second (6 x seekSpd/frame).</summary>
            public float CautiousRate;

            /// <summary>Align: the fixed stalking speed on each axis, units/s (seekSpd/frame).</summary>
            public float AlignSpeed;

            /// <summary>A waiting enemy that stalks in this close to the angel turns Cautious and backs off, units.</summary>
            public float HangBackNear;

            /// <summary>A Cautious enemy backed out this far turns to Align and stalks in again, units.</summary>
            public float HangBackFar;

            public static Settings Default => new Settings
            {
                SeekRate = 0.36f,
                HurtDeceleration = 6f,
                RecoverSpeed = 0.05f,
                CautiousRate = 6f * 0.36f,
                AlignSpeed = 0.006f * OriginalFrameRate * OriginalSpace.ArenaLengthScale,
                HangBackNear = 0.3f * OriginalSpace.ArenaLengthScale,
                HangBackFar = 0.45f * OriginalSpace.ArenaLengthScale,
            };
        }

        /// <summary>Enemy.vb's animateEnemy: 0.1 s per frame.</summary>
        public const float DefaultFrameDuration = 0.1f;

        /// <summary>Enemy.vb's eFrames = 6, so 7 frames (0..6), Shadow\enemy1..7.</summary>
        public const int FrameCount = 7;

        public Settings Config;

        public float MaxHp { get; }
        public float Hp { get; private set; }
        public float Mass { get; }
        public EnemyState State { get; private set; } = EnemyState.Seek;
        public Vec2 Velocity { get; private set; }

        /// <summary>
        /// Enemy.vb's <c>Enemy(n).Frame</c>: loadEnemy starts it at a random 0..6, then
        /// <c>animateEnemy</c> advances it every <see cref="DefaultFrameDuration"/> while this enemy
        /// is stepped (not while Grabbed: the world does not step a held enemy's brain, a minor
        /// deviation -- the original kept animating a grabbed enemy too).
        /// </summary>
        public int Frame { get; private set; }

        private float _frameTime;

        /// <summary>True from a throw until the enemy recovers: it knocks other enemies it touches.</summary>
        public bool IsThrown { get; private set; }

        public bool IsAlive => State != EnemyState.Dead;

        /// <summary>Enemy.vb only lets the mace hit (or grab) an enemy that is alive, not Hurt and not held.</summary>
        public bool CanBeHit => State == EnemyState.Seek || IsWaiting;

        /// <summary>Hanging back (Cautious) or stalking (Align) until the world gives it a Seek slot.</summary>
        public bool IsWaiting => State == EnemyState.Cautious || State == EnemyState.Align;

        /// <summary>The world's Seek cap is full: a seeking enemy waits, Cautious inside the hang-back, Align outside it.</summary>
        public void Wait(float distanceToTarget)
        {
            if (State == EnemyState.Seek)
            {
                State = distanceToTarget < Config.HangBackNear ? EnemyState.Cautious : EnemyState.Align;
            }
        }

        /// <summary>A Seek slot is free: a waiting enemy takes it.</summary>
        public void Promote()
        {
            if (IsWaiting)
            {
                State = EnemyState.Seek;
            }
        }

        public EnemyBrain(float hp = 1f, float mass = 1f, Settings? settings = null, int initialFrame = 0)
        {
            MaxHp = Calc.Max(0.0001f, hp);
            Hp = MaxHp;
            Mass = Calc.Max(0.01f, mass);
            Config = settings ?? Settings.Default;
            Frame = ((initialFrame % FrameCount) + FrameCount) % FrameCount;
        }

        /// <summary>
        /// Advances the AI one step and returns the velocity to move with. Seek heads for
        /// <paramref name="seekPoint"/>; Hurt slows each axis toward zero and recovers below
        /// <see cref="Settings.RecoverSpeed"/>. Waiting, Cautious backs straight away from it at
        /// <see cref="Settings.CautiousRate"/> x distance / mass until <see cref="Settings.HangBackFar"/> out,
        /// then Align stalks in at <see cref="Settings.AlignSpeed"/> on each axis until
        /// <see cref="Settings.HangBackNear"/>. Grabbed and Dead do not move on their own.
        /// </summary>
        public Vec2 Step(Vec2 position, Vec2 seekPoint, float deltaTime)
        {
            AdvanceFrame(deltaTime);

            var offset = seekPoint - position;
            switch (State)
            {
                case EnemyState.Seek:
                    Velocity = offset * Config.SeekRate;
                    break;

                case EnemyState.Cautious:
                    if (offset.Magnitude >= Config.HangBackFar)
                    {
                        State = EnemyState.Align;
                        goto case EnemyState.Align;
                    }

                    Velocity = -offset * (Config.CautiousRate / Mass);
                    break;

                case EnemyState.Align:
                    if (offset.Magnitude <= Config.HangBackNear)
                    {
                        State = EnemyState.Cautious;
                        Velocity = -offset * (Config.CautiousRate / Mass);
                        break;
                    }

                    Velocity = new Vec2(AlignAxis(offset.X, deltaTime), AlignAxis(offset.Y, deltaTime));
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
        /// A mace hit at <paramref name="impactVelocity"/>: knocked back at impact / mass (<see cref="Knock"/>)
        /// and damaged by one of two formulas (<see cref="MaceDamageFormula"/>). The default,
        /// <see cref="MaceDamageFormula.Original"/>, is Engine.vb's own <c>abs(dX + dY/2)</c> of that same
        /// mass-divided velocity, converted back to the original's per-frame units by
        /// <paramref name="velocityToOriginalScale"/> (BIBLE D11); <see cref="MaceDamageFormula.SpeedTimesFactor"/>
        /// is the rebuild's own earlier formula, the raw impact speed (not mass-divided) x
        /// <paramref name="damagePerSpeed"/>. Returns the damage dealt (0 if it could not be hit).
        /// </summary>
        public float Hit(
            Vec2 impactVelocity,
            float damagePerSpeed,
            MaceDamageFormula formula = MaceDamageFormula.Original,
            float velocityToOriginalScale = 1f)
        {
            if (!CanBeHit)
            {
                return 0f;
            }

            Knock(impactVelocity);

            float damage;
            if (formula == MaceDamageFormula.SpeedTimesFactor)
            {
                damage = impactVelocity.Magnitude * Calc.Max(0f, damagePerSpeed);
            }
            else
            {
                var original = Velocity * velocityToOriginalScale;
                damage = Calc.Abs(original.X + original.Y * 0.5f);
            }

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

        /// <summary>
        /// Align on one axis: seekSpd toward the angel. The original flips sign every frame once level; this
        /// moves only the rest of the way instead, so a level enemy holds still rather than shimmering.
        /// </summary>
        private float AlignAxis(float offset, float deltaTime)
        {
            var speed = Calc.Max(0f, Config.AlignSpeed);
            if (deltaTime > 0f && Calc.Abs(offset) < speed * deltaTime)
            {
                return offset / deltaTime;
            }

            return Calc.Sign(offset) * speed;
        }

        private static float AwaySign(float difference, int tieBreak)
        {
            if (Calc.Abs(difference) < 1e-6f)
            {
                return tieBreak < 0 ? -1f : 1f;
            }

            return Calc.Sign(difference);
        }

        /// <summary>Enemy.vb's animateEnemy: loops 0..<see cref="FrameCount"/>-1 every <see cref="DefaultFrameDuration"/>.</summary>
        private void AdvanceFrame(float deltaTime)
        {
            _frameTime += deltaTime;
            while (_frameTime >= DefaultFrameDuration)
            {
                _frameTime -= DefaultFrameDuration;
                Frame = (Frame + 1) % FrameCount;
            }
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
