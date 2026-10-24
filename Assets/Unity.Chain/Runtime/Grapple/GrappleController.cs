using MindAttic.Chain.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MindAttic.Chain
{
    /// <summary>
    /// One player's grapple button (left mouse by default). Pressing it sends
    /// <see cref="ArmGrapple"/> and letting go sends <see cref="ReleaseGrapple"/>, each with this
    /// player's id; the world arms the mace, catches the nearest enemy inside
    /// <see cref="CatchRadius"/> while armed, carries it on the mace, and on release throws it with
    /// the mace's velocity x <see cref="ThrowMultiplier"/> (the <see cref="Grabbed"/> and
    /// <see cref="Thrown"/> events). The catch and throw tuning is chain.json's; this component can
    /// override it at run time, and the overrides go to the world when the player is seated or retuned. Sits beside the player's <see cref="ChainController"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GrappleController : MonoBehaviour
    {
        [Header("Input")]
        [Tooltip("Read the left mouse button as the grapple button. Turn off to drive SetButtonHeld yourself.")]
        [SerializeField] private bool useMouseButton = true;

        // Run-time overrides of chain.json (null: the data file's figure).
        private float? _catchRadius;
        private float? _throwMultiplier;

        private ChainController _chain;
        private bool _buttonHeld;

        public ChainController Chain => _chain != null ? _chain : _chain = GetComponent<ChainController>();

        /// <summary>The world's grapple state for this player (Idle until seated).</summary>
        public GrappleState State => Chain != null && Chain.Entity != null ? Chain.Entity.Grapple.State : GrappleState.Idle;

        public bool IsArmed => State != GrappleState.Idle;

        /// <summary>The enemy in the mace's grip, or null.</summary>
        public EnemyEntity HeldEnemy => Chain != null ? Chain.Entity?.HeldEnemy : null;

        /// <summary>Radius around the mace link that catches a target while armed, world units.</summary>
        public float CatchRadius
        {
            get => _catchRadius ?? Data.CatchRadius;
            set
            {
                _catchRadius = Mathf.Max(0f, value);
                Retune();
            }
        }

        /// <summary>Launch velocity = mace velocity x this.</summary>
        public float ThrowMultiplier
        {
            get => _throwMultiplier ?? Data.ThrowMultiplier;
            set
            {
                _throwMultiplier = Mathf.Max(0f, value);
                Retune();
            }
        }

        private PlayerSetup Data => Chain != null ? Chain.Settings.Player : SettingsStore.Current.Player;

        public bool UseMouseButton
        {
            get => useMouseButton;
            set => useMouseButton = value;
        }

        /// <summary>Called when the chain's player is seated.</summary>
        public void Bind(ChainController chain) => _chain = chain;

        /// <summary>Puts the grapple's overrides over the data files' player setup.</summary>
        public void Contribute(ref PlayerSetup setup)
        {
            setup.CatchRadius = _catchRadius ?? setup.CatchRadius;
            setup.ThrowMultiplier = _throwMultiplier ?? setup.ThrowMultiplier;
        }

        /// <summary>Sets the grapple button state, for custom input or tests: a change is sent as a command.</summary>
        public void SetButtonHeld(bool held)
        {
            if (held == _buttonHeld)
            {
                return;
            }

            var session = Chain != null ? Chain.Session : null;
            if (session == null)
            {
                return;
            }

            _buttonHeld = held;
            session.Submit(held ? (GameCommand)new ArmGrapple(Chain.Player) : new ReleaseGrapple(Chain.Player));
        }

        private void Retune()
        {
            if (Chain != null && Chain.Session != null)
            {
                Chain.Session.Retune(Chain.Player);
            }
        }

        private void Update()
        {
            if (!useMouseButton)
            {
                return;
            }

            var mouse = Mouse.current;
            SetButtonHeld(mouse != null && mouse.leftButton.isPressed);
        }

        // Never leave an enemy held by a button nobody can let go of.
        private void OnDisable() => SetButtonHeld(false);

        private void OnDrawGizmosSelected()
        {
            var player = Chain != null ? Chain.Entity : null;
            if (player == null)
            {
                return;
            }

            Gizmos.color = IsArmed ? Color.green : Color.gray;
            Gizmos.DrawWireSphere(player.MacePosition.ToUnity(), player.Setup.CatchRadius);
        }
    }
}
