namespace MindAttic.Chain.Core
{
    /// <summary>
    /// One enemy in the <see cref="GameWorld"/>: its id, its rules (<see cref="EnemyBrain"/>), where it
    /// is and how big it is (a circle, as its sprite's collider was). The world moves it; presentation
    /// reads it. It is the grapple's target type: the armed mace grabs it, carries it and throws it.
    /// </summary>
    public sealed class EnemyEntity : IGrappleTarget
    {
        internal EnemyEntity(EnemyId id, EnemyBrain brain, Vec2 position, float radius)
        {
            Id = id;
            Brain = brain;
            Position = position;
            Radius = radius;
        }

        public EnemyId Id { get; }
        public EnemyBrain Brain { get; }
        public Vec2 Position { get; internal set; }
        public float Radius { get; }

        public EnemyState State => Brain.State;
        public float Hp => Brain.Hp;
        public bool IsAlive => Brain.IsAlive;
        public Vec2 Velocity => Brain.Velocity;

        /// <summary>True while the enemy is still in the world (it leaves when killed or cleared).</summary>
        public bool InWorld { get; internal set; } = true;

        /// <summary>Enemy.vb: only a seeking enemy can be hit or grabbed.</summary>
        public bool CanBeHit => InWorld && Brain.CanBeHit;

        bool IGrappleTarget.CanBeGrappled => CanBeHit;

        void IGrappleTarget.OnGrappled() => Brain.Grab();

        void IGrappleTarget.HoldAt(Vec2 position) => Position = position;

        void IGrappleTarget.Throw(Vec2 velocity) => Brain.Throw(velocity);

        /// <summary>True when this enemy's circle overlaps a circle of <paramref name="radius"/> at <paramref name="center"/>.</summary>
        public bool Overlaps(Vec2 center, float radius)
        {
            var reach = Radius + radius;
            return (Position - center).SqrMagnitude < reach * reach;
        }

        public override string ToString() => $"{Id} {State} hp {Hp:0.###} at {Position}";
    }
}
