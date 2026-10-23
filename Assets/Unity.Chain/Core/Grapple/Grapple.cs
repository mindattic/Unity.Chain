namespace MindAttic.Chain.Core
{
    public enum GrappleState
    {
        /// <summary>Button up: the mace is a plain spiked ball.</summary>
        Idle,

        /// <summary>Button held, nothing caught yet: the mace grabs the first target it touches.</summary>
        Armed,

        /// <summary>Button held with a target in the mace's grip.</summary>
        Holding,
    }

    /// <summary>
    /// The grapple/throw rule from Chain.vb: holding the action button arms the grapple; while
    /// armed the mace catches one target; on release a caught target is thrown with the mace's
    /// current velocity (times <see cref="ThrowMultiplier"/>) as its launch velocity. Releasing
    /// with nothing caught just disarms. Each player's grapple lives in the <see cref="GameWorld"/>.
    /// </summary>
    public sealed class Grapple
    {
        public float ThrowMultiplier;

        public GrappleState State { get; private set; } = GrappleState.Idle;
        public IGrappleTarget Target { get; private set; }

        /// <summary>True while the button is held (armed or holding): the mace shows the grapple texture.</summary>
        public bool IsArmed => State != GrappleState.Idle;

        public Grapple(float throwMultiplier = 1f)
        {
            ThrowMultiplier = throwMultiplier;
        }

        /// <summary>
        /// Feeds the button state for this step. Pressing arms; releasing throws whatever is held
        /// using <paramref name="maceVelocity"/> and returns the thrown target (null if none).
        /// </summary>
        public IGrappleTarget SetHeld(bool held, Vec2 maceVelocity)
        {
            if (held)
            {
                if (State == GrappleState.Idle)
                {
                    State = GrappleState.Armed;
                }

                return null;
            }

            if (State == GrappleState.Idle)
            {
                return null;
            }

            var thrown = Target;
            Target = null;
            State = GrappleState.Idle;
            thrown?.Throw(maceVelocity * ThrowMultiplier);
            return thrown;
        }

        /// <summary>Catches <paramref name="target"/> if armed with an empty grip. Returns true on a catch.</summary>
        public bool TryCatch(IGrappleTarget target)
        {
            if (State != GrappleState.Armed || target == null || !target.CanBeGrappled)
            {
                return false;
            }

            Target = target;
            State = GrappleState.Holding;
            target.OnGrappled();
            return true;
        }

        /// <summary>Drags a held target along with the mace. No-op unless holding.</summary>
        public void Carry(Vec2 macePosition)
        {
            if (State == GrappleState.Holding)
            {
                Target.HoldAt(macePosition);
            }
        }

        /// <summary>Lets go of a held target without throwing it (it died, say). Stays armed.</summary>
        public void Drop()
        {
            if (State != GrappleState.Holding)
            {
                return;
            }

            Target = null;
            State = GrappleState.Armed;
        }
    }
}
