using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MindAttic.Chain
{
    /// <summary>
    /// Wires <see cref="Grapple"/> to the game: reads the action button (left mouse by default),
    /// and every physics step, after <see cref="ChainController"/> has stepped the chain, arms or
    /// releases the grapple, catches the first <see cref="IGrappleTarget"/> overlapping the mace
    /// link while armed, carries it along with the mace, and on release throws it with the mace
    /// link's current velocity.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)] // after ChainController.FixedUpdate, so the mace velocity is this step's
    public sealed class GrappleController : MonoBehaviour
    {
        [SerializeField] private ChainController chain;

        [Tooltip("Index of the chain link that acts as the mace (Chain.vb default 6). Ignored when the chain has a MaceController.")]
        [Min(1)]
        [SerializeField] private int maceIndex = 6;

        [Header("Catch")]
        [Tooltip("Radius around the mace link that catches a target while armed, world units.")]
        [Min(0f)]
        [SerializeField] private float catchRadius = 0.3f;
        [SerializeField] private LayerMask targetLayers = ~0;

        [Header("Throw")]
        [Tooltip("Launch velocity = mace velocity x this.")]
        [Min(0f)]
        [SerializeField] private float throwMultiplier = 1f;

        [Header("Input")]
        [Tooltip("Read the left mouse button as the grapple button. Turn off to drive SetButtonHeld yourself.")]
        [SerializeField] private bool useMouseButton = true;

        private readonly List<Collider2D> _overlaps = new List<Collider2D>();
        private Grapple _grapple;
        private bool _buttonHeld;
        private bool _pressedSinceLastStep;

        /// <summary>Raised when the armed mace catches a target.</summary>
        public event Action<IGrappleTarget> Caught;

        /// <summary>Raised on release with the thrown target and its launch velocity.</summary>
        public event Action<IGrappleTarget, Vector2> Thrown;

        public Grapple Grapple => _grapple ?? (_grapple = new Grapple(throwMultiplier));
        public GrappleState State => Grapple.State;
        public bool IsArmed => Grapple.IsArmed;
        public IGrappleTarget Target => Grapple.Target;

        public ChainController Chain
        {
            get => chain;
            set => chain = value;
        }

        /// <summary>
        /// The mace link. Follows the chain's <see cref="MaceController"/> when it has one, so a
        /// shortened/lengthened chain grapples from its current end; otherwise this component's
        /// own index.
        /// </summary>
        public int MaceIndex
        {
            get
            {
                if (chain == null)
                {
                    return maceIndex;
                }

                var mace = chain.GetComponent<MaceController>();
                return mace != null ? mace.MaceIndex : Mathf.Clamp(maceIndex, 1, chain.LinkCount - 1);
            }
            set => maceIndex = Mathf.Max(1, value);
        }

        public float CatchRadius
        {
            get => catchRadius;
            set => catchRadius = Mathf.Max(0f, value);
        }

        public float ThrowMultiplier
        {
            get => throwMultiplier;
            set => Grapple.ThrowMultiplier = throwMultiplier = Mathf.Max(0f, value);
        }

        public bool UseMouseButton
        {
            get => useMouseButton;
            set => useMouseButton = value;
        }

        public Vector2 MacePosition => chain.GetLinkPosition(MaceIndex);
        public Vector2 MaceVelocity => chain.GetLinkVelocity(MaceIndex);

        /// <summary>Sets the grapple button state, for custom input or tests.</summary>
        public void SetButtonHeld(bool held)
        {
            if (held && !_buttonHeld)
            {
                _pressedSinceLastStep = true;
            }

            _buttonHeld = held;
        }

        /// <summary>
        /// One grapple step: arm/release from the button, catch while armed, carry what is held.
        /// Called from <c>FixedUpdate</c>; public so tests can drive it.
        /// </summary>
        public void Step()
        {
            if (chain == null)
            {
                return;
            }

            Grapple.ThrowMultiplier = throwMultiplier;

            if (Grapple.State == GrappleState.Holding && IsGone(Grapple.Target))
            {
                Grapple.Drop();
            }

            // A press and release that both landed between two physics steps still counts as a
            // press: arm for this step so a quick tap can catch what the mace is touching.
            var held = _buttonHeld || _pressedSinceLastStep;
            _pressedSinceLastStep = false;

            var thrown = Grapple.SetHeld(held, MaceVelocity);
            if (thrown != null)
            {
                Thrown?.Invoke(thrown, MaceVelocity * throwMultiplier);
            }

            // The catch is the mace's touch (Engine.vb's checkMaceImpact on rectMace), which is not
            // kept while the chain is not following (F key, BIBLE D17).
            if (Grapple.State == GrappleState.Armed && MindAttic.Chain.Core.ChainFollow.MaceHits(chain.IsFollowing))
            {
                TryCatch(FindTargetAtMace());
            }

            Grapple.Carry(MacePosition);
        }

        /// <summary>Catches <paramref name="target"/> if the grapple is armed with an empty grip.</summary>
        public bool TryCatch(IGrappleTarget target)
        {
            if (IsGone(target) || !Grapple.TryCatch(target))
            {
                return false;
            }

            Grapple.Carry(MacePosition);
            Caught?.Invoke(target);
            return true;
        }

        private IGrappleTarget FindTargetAtMace()
        {
            var filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = targetLayers,
                useTriggers = true,
            };

            _overlaps.Clear();
            Physics2D.OverlapCircle(MacePosition, catchRadius, filter, _overlaps);

            IGrappleTarget nearest = null;
            var nearestDistance = float.PositiveInfinity;
            foreach (var hit in _overlaps)
            {
                var target = hit.GetComponentInParent<IGrappleTarget>();
                if (target == null || !target.CanBeGrappled)
                {
                    continue;
                }

                var distance = ((Vector2)hit.bounds.center - MacePosition).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearest = target;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        // A destroyed MonoBehaviour behind the interface is not C# null; ask Unity.
        private static bool IsGone(IGrappleTarget target) =>
            target == null || (target is UnityEngine.Object o && o == null);

        private void Reset() => chain = GetComponent<ChainController>();

        private void Awake()
        {
            if (chain == null)
            {
                chain = GetComponent<ChainController>();
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

        private void FixedUpdate() => Step();

        private void OnDisable()
        {
            // Never leave a target frozen kinematic in mid-air: let go of it gently.
            if (Grapple.State == GrappleState.Holding && IsGone(Grapple.Target))
            {
                Grapple.Drop();
            }

            Grapple.SetHeld(false, Vector2.zero);

            _buttonHeld = false;
            _pressedSinceLastStep = false;
        }

        private void OnDrawGizmosSelected()
        {
            if (chain == null)
            {
                return;
            }

            Gizmos.color = IsArmed ? Color.green : Color.gray;
            Gizmos.DrawWireSphere(MacePosition, catchRadius);
        }
    }
}
