using UnityEngine;

namespace MindAttic.Chain
{
    /// <summary>Something the mace can grab while the grapple is armed and then throw.</summary>
    public interface IGrappleTarget
    {
        /// <summary>False while the target cannot be grabbed (already thrown, dead, disabled).</summary>
        bool CanBeGrappled { get; }

        /// <summary>Called once when the armed mace catches this target.</summary>
        void OnGrappled();

        /// <summary>Called every physics step while held, with the mace's position.</summary>
        void HoldAt(Vector2 position);

        /// <summary>Called once on release with the launch velocity (the mace's velocity, scaled).</summary>
        void Throw(Vector2 velocity);
    }
}
