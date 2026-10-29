namespace MindAttic.Chain.Core
{
    /// <summary>Something the mace can grab while the grapple is armed and then throw (in the game, an <see cref="EnemyEntity"/>).</summary>
    public interface IGrappleTarget
    {
        /// <summary>False while the target cannot be grabbed (hurt, dead, already held).</summary>
        bool CanBeGrappled { get; }

        /// <summary>Called once when the armed mace catches this target.</summary>
        void OnGrappled();

        /// <summary>Called every step while held, with the mace's position.</summary>
        void HoldAt(Vec2 position);

        /// <summary>Called once on release with the launch velocity (the mace's velocity, scaled).</summary>
        void Throw(Vec2 velocity);
    }
}
