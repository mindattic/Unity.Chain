using UnityEngine;
using UnityEngine.InputSystem;

namespace MindAttic.Chain
{
    /// <summary>
    /// The heavy end of the chain. One link index (<c>Mace</c> in Chain.vb, default 6,
    /// adjustable 1-12 at runtime) is the business end: it is drawn bigger (3.5x the link) as a
    /// spiked ball and everything past it is hidden. Shortening/lengthening the chain moves that
    /// index, and the chain's live length follows it (<see cref="ChainController.ActiveLinkCount"/>),
    /// so the mace is always the end link. The sprite offset is a render-only transform, never a
    /// position mutation (see docs/chain-physics.md for why Chain.vb's per-frame offset is not
    /// ported). The overhead-swing boost itself is chain physics and is tuned on
    /// <see cref="ChainController"/>.
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

        [Header("Rendering")]
        [Tooltip("The spiked-ball sprite (or any transform) drawn at the mace link.")]
        [SerializeField] private Transform maceVisual;
        [Tooltip("Mace size relative to the chain's line width (Chain.vb: 3.5x the link quad). " +
                 "Assumes the visual is one world unit across at scale 1.")]
        [Min(0f)]
        [SerializeField] private float maceScale = 3.5f;
        [Tooltip("Render-only offset of the visual from the mace link, in world units.")]
        [SerializeField] private Vector2 spriteOffset = Vector2.zero;

        [Header("Input")]
        [SerializeField] private InputAction shorten = Button("Shorten Chain", "<Keyboard>/leftBracket", "<Keyboard>/minus", "<Keyboard>/numpadMinus");
        [SerializeField] private InputAction lengthen = Button("Lengthen Chain", "<Keyboard>/rightBracket", "<Keyboard>/equals", "<Keyboard>/numpadPlus");

        private ChainController _chain;

        public ChainController Chain
        {
            get
            {
                if (_chain == null)
                {
                    _chain = GetComponent<ChainController>();
                }

                return _chain;
            }
        }

        /// <summary>Upper bound for <see cref="MaceIndex"/>: <c>maxIndex</c>, capped by the chain.</summary>
        public int MaxIndex => Mathf.Clamp(maxIndex, 1, Mathf.Max(1, Chain.LinkCount - 1));

        public int MinIndex => Mathf.Clamp(minIndex, 1, MaxIndex);

        public int MaceIndex
        {
            get => Mathf.Clamp(maceIndex, MinIndex, MaxIndex);
            set
            {
                maceIndex = Mathf.Clamp(value, MinIndex, MaxIndex);
                ApplyToChain();
            }
        }

        public float MaceMass
        {
            get => maceMass;
            set
            {
                maceMass = Mathf.Max(0.01f, value);
                ApplyToChain();
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

        /// <summary>The mace link's simulated position (for hit detection and grapple throws).</summary>
        public Vector2 MacePosition => Chain.GetLinkPosition(MaceIndex);

        /// <summary>The mace link's velocity (the grapple throw's launch velocity).</summary>
        public Vector2 MaceVelocity => Chain.Simulation.GetVelocity(MaceIndex);

        /// <summary>One link shorter (Chain.vb's chain-minus key). Returns false at the minimum.</summary>
        public bool Shorten()
        {
            var before = MaceIndex;
            MaceIndex = before - 1;
            return MaceIndex != before;
        }

        /// <summary>One link longer (Chain.vb's chain-plus key). Returns false at the maximum.</summary>
        public bool Lengthen()
        {
            var before = MaceIndex;
            MaceIndex = before + 1;
            return MaceIndex != before;
        }

        /// <summary>
        /// Pushes the mace onto the chain: the live length ends at the mace link, and the mace
        /// link carries <see cref="MaceMass"/> while every other link has mass 1.
        /// </summary>
        public void ApplyToChain()
        {
            var chain = Chain;
            var index = MaceIndex;
            chain.ActiveLinkCount = index + 1;

            var simulation = chain.Simulation;
            for (var i = 1; i < simulation.LinkCount; i++)
            {
                simulation.SetMass(i, i == index ? maceMass : 1f);
            }
        }

        /// <summary>Places and sizes the mace visual on the drawn (interpolated) mace link.</summary>
        public void RenderMace()
        {
            if (maceVisual == null)
            {
                return;
            }

            var linkPosition = Chain.GetRenderedLinkPosition(MaceIndex);
            maceVisual.position = linkPosition + (Vector3)spriteOffset;

            var line = Chain.GetComponent<LineRenderer>();
            var width = line != null ? line.widthMultiplier : 1f;
            maceVisual.localScale = Vector3.one * (width * maceScale);
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

        private void Awake() => ApplyToChain();

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

            // Re-apply every frame: a chain rebuild (link count change in the inspector) resets masses.
            ApplyToChain();
        }

        private void LateUpdate() => RenderMace();

        private void OnValidate()
        {
            maxIndex = Mathf.Max(1, maxIndex);
            minIndex = Mathf.Clamp(minIndex, 1, maxIndex);
            maceIndex = Mathf.Clamp(maceIndex, minIndex, maxIndex);
            if (Application.isPlaying && _chain != null)
            {
                ApplyToChain();
            }
        }
    }
}
