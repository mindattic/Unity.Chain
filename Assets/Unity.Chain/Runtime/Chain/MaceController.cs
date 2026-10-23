using MindAttic.Chain.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MindAttic.Chain
{
    /// <summary>
    /// The heavy end of one player's chain. One link index (<c>Mace</c> in Chain.vb, default 6,
    /// adjustable 1-12 at runtime) is the business end: it is drawn bigger (3.5x the link) as a
    /// spiked ball and everything past it is hidden. The <c>[</c> and <c>]</c> keys send
    /// <see cref="ChangeChainLength"/> commands with this player's id; the world moves the mace link,
    /// ends the live chain on it and gives it the mace's mass. The mace's hit tuning (hit radius,
    /// damage per speed) lives here and goes to the world when the player is seated; the world does
    /// the hitting (Engine.vb checkMaceImpact, on a swept circle). The sprite offset is a
    /// render-only transform, never a position mutation (see docs/chain-physics.md for why
    /// Chain.vb's per-frame offset is not ported).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)] // after ChainController, so the visual tracks this frame's drawn chain
    [RequireComponent(typeof(ChainController))]
    public sealed class MaceController : MonoBehaviour
    {
        [Header("Mace link")]
        [Tooltip("Index of the heavy end link (Chain.vb's Mace). Links past it are hidden.")]
        [SerializeField] private int maceIndex = 6;
        [Tooltip("Shortest the chain can go (Chain.vb: 1).")]
        [Min(1)]
        [SerializeField] private int minIndex = 1;
        [Tooltip("Longest the chain can go (Chain.vb: 12). Also capped by the chain's link count.")]
        [Min(1)]
        [SerializeField] private int maxIndex = 12;
        [Tooltip("Mass of the mace link; every other link has mass 1. Chain.vb uses 1.")]
        [Min(0.01f)]
        [SerializeField] private float maceMass = 1f;

        [Header("Hit")]
        [Tooltip("Radius of the mace's hit circle, world units.")]
        [Min(0f)]
        [SerializeField] private float hitRadius = 0.25f;
        [Tooltip("Damage per world unit per second of mace speed at impact.")]
        [Min(0f)]
        [SerializeField] private float damagePerSpeed = 0.05f;

        [Header("Rendering")]
        [Tooltip("The spiked-ball sprite (or any transform) drawn at the mace link.")]
        [SerializeField] private Transform maceVisual;
        [Tooltip("Mace size relative to the chain's line width (Chain.vb: 3.5x the link quad). " +
                 "Assumes the visual is one world unit across at scale 1.")]
        [Min(0f)]
        [SerializeField] private float maceScale = ChainFollow.MaceScale;
        [Tooltip("Render-only offset of the visual from the mace link, in world units.")]
        [SerializeField] private Vector2 spriteOffset = Vector2.zero;

        [Header("Input")]
        [SerializeField] private InputAction shorten = Button("Shorten Chain", "<Keyboard>/leftBracket", "<Keyboard>/minus", "<Keyboard>/numpadMinus");
        [SerializeField] private InputAction lengthen = Button("Lengthen Chain", "<Keyboard>/rightBracket", "<Keyboard>/equals", "<Keyboard>/numpadPlus");

        private ChainController _chain;

        public ChainController Chain => _chain != null ? _chain : _chain = GetComponent<ChainController>();

        /// <summary>Upper bound for <see cref="MaceIndex"/>: <c>maxIndex</c>, capped by the chain.</summary>
        public int MaxIndex => Chain.Entity?.MaxMaceIndex ?? Mathf.Clamp(maxIndex, 1, Mathf.Max(1, Chain.LinkCount - 1));

        public int MinIndex => Chain.Entity?.MinMaceIndex ?? Mathf.Clamp(minIndex, 1, MaxIndex);

        /// <summary>The mace link: the world's once seated, else the configured starting link.</summary>
        public int MaceIndex => Chain.Entity?.MaceIndex ?? Mathf.Clamp(maceIndex, MinIndex, MaxIndex);

        public float MaceMass
        {
            get => maceMass;
            set
            {
                maceMass = Mathf.Max(0.01f, value);
                Retune();
            }
        }

        public float HitRadius
        {
            get => hitRadius;
            set
            {
                hitRadius = Mathf.Max(0f, value);
                Retune();
            }
        }

        public float DamagePerSpeed
        {
            get => damagePerSpeed;
            set
            {
                damagePerSpeed = Mathf.Max(0f, value);
                Retune();
            }
        }

        public Transform MaceVisual
        {
            get => maceVisual;
            set => maceVisual = value;
        }

        public float MaceScale
        {
            get => maceScale;
            set => maceScale = Mathf.Max(0f, value);
        }

        public Vector2 SpriteOffset
        {
            get => spriteOffset;
            set => spriteOffset = value;
        }

        /// <summary>The mace link's simulated position.</summary>
        public Vector2 MacePosition => Chain.GetLinkPosition(MaceIndex);

        /// <summary>The mace link's velocity (the grapple throw's launch velocity).</summary>
        public Vector2 MaceVelocity => Chain.GetLinkVelocity(MaceIndex);

        /// <summary>Called when the chain's player is seated.</summary>
        public void Bind(ChainController chain) => _chain = chain;

        /// <summary>Hands the mace's tuning to the world's player setup.</summary>
        public void Contribute(ref PlayerSetup setup)
        {
            setup.MaxMaceIndex = Mathf.Max(1, maxIndex);
            setup.MinMaceIndex = Mathf.Clamp(minIndex, 1, setup.MaxMaceIndex);
            setup.MaceIndex = Mathf.Clamp(maceIndex, setup.MinMaceIndex, setup.MaxMaceIndex);
            setup.MaceMass = maceMass;
            setup.HitRadius = hitRadius;
            setup.DamagePerSpeed = damagePerSpeed;
        }

        /// <summary>One link shorter (Chain.vb's chain-minus key). Returns false at the minimum or before the player is seated.</summary>
        public bool Shorten() => Send(-1);

        /// <summary>One link longer (Chain.vb's chain-plus key). Returns false at the maximum or before the player is seated.</summary>
        public bool Lengthen() => Send(1);

        /// <summary>
        /// The visual's size relative to the chain's line width: <see cref="MaceScale"/> while the
        /// chain follows, one link's width when it does not (Chain.vb skips the 3.5x then).
        /// </summary>
        public float CurrentScale => ChainFollow.MaceScaleFor(Chain.IsFollowing, maceScale);

        /// <summary>
        /// Places and sizes the mace visual on the drawn (interpolated) mace link. With the chain
        /// not following, the sprite offset and the 3.5x scale are skipped, as in Chain.vb.
        /// </summary>
        public void RenderMace()
        {
            if (maceVisual == null || Chain.Simulation == null)
            {
                return;
            }

            var following = Chain.IsFollowing;
            var linkPosition = Chain.GetRenderedLinkPosition(MaceIndex);
            maceVisual.position = linkPosition + (following ? (Vector3)spriteOffset : Vector3.zero);

            var line = Chain.GetComponent<LineRenderer>();
            var width = line != null ? line.widthMultiplier : 1f;
            maceVisual.localScale = Vector3.one * (width * CurrentScale);
        }

        private bool Send(int links)
        {
            var session = Chain.Session;
            return session != null && session.Submit(new ChangeChainLength(Chain.Player, links));
        }

        private void Retune()
        {
            if (Chain.Session != null)
            {
                Chain.Session.Retune(Chain.Player);
            }
        }

        private static InputAction Button(string name, params string[] bindings)
        {
            var action = new InputAction(name, InputActionType.Button);
            foreach (var binding in bindings)
            {
                action.AddBinding(binding);
            }

            return action;
        }

        private void OnEnable()
        {
            shorten.Enable();
            lengthen.Enable();
        }

        private void OnDisable()
        {
            shorten.Disable();
            lengthen.Disable();
        }

        private void Update()
        {
            if (shorten.WasPressedThisFrame())
            {
                Shorten();
            }

            if (lengthen.WasPressedThisFrame())
            {
                Lengthen();
            }
        }

        private void LateUpdate() => RenderMace();

        private void OnValidate()
        {
            maxIndex = Mathf.Max(1, maxIndex);
            minIndex = Mathf.Clamp(minIndex, 1, maxIndex);
            maceIndex = Mathf.Clamp(maceIndex, minIndex, maxIndex);
            if (_chain != null)
            {
                Retune();
            }
        }
    }
}
