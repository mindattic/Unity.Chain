using MindAttic.Chain.Core;
using UnityEngine;

namespace MindAttic.Chain
{
    /// <summary>
    /// The heavy end of one player's chain. One link index (<c>Mace</c> in Chain.vb, default 6,
    /// adjustable 1-12 at runtime) is the business end: it is drawn bigger (3.5x the link) as a
    /// spiked ball and everything past it is hidden. The <c>[</c> and <c>]</c> keys (the seat's
    /// ChainShorter and ChainLonger actions, <see cref="SeatInput"/>) send
    /// <see cref="ChangeChainLength"/> commands with this player's id; the world moves the mace link,
    /// ends the live chain on it and gives it the mace's mass. The mace's tuning (its link and range, mass,
    /// hit radius, damage per speed, draw scale) is chain.json's, which this component can override at run
    /// time; overrides go to the world when the player is seated or retuned. The world does
    /// the hitting (Engine.vb checkMaceImpact, on a swept circle). The sprite offset is a
    /// render-only transform, never a position mutation (see docs/chain-physics.md for why
    /// Chain.vb's per-frame offset is not ported).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)] // after ChainController, so the visual tracks this frame's drawn chain
    [RequireComponent(typeof(ChainController))]
    public sealed class MaceController : MonoBehaviour
    {
        [Header("Rendering")]
        [Tooltip("The spiked-ball sprite (or any transform) drawn at the mace link.")]
        [SerializeField] private Transform maceVisual;

        // Run-time overrides of chain.json (null: the data file's figure).
        private float? _maceMass;
        private float? _hitRadius;
        private float? _damagePerSpeed;
        private float? _maceScale;
        private Vector2 _spriteOffset = Vector2.zero;

        private ChainController _chain;

        public ChainController Chain => _chain != null ? _chain : _chain = GetComponent<ChainController>();

        /// <summary>Upper bound for <see cref="MaceIndex"/>: chain.json's <c>maceMax</c>, capped by the chain.</summary>
        public int MaxIndex => Chain.Entity?.MaxMaceIndex ?? Mathf.Clamp(Data.MaxMaceIndex, 1, Mathf.Max(1, Chain.LinkCount - 1));

        public int MinIndex => Chain.Entity?.MinMaceIndex ?? Mathf.Clamp(Data.MinMaceIndex, 1, MaxIndex);

        /// <summary>The mace link: the world's once seated, else chain.json's starting link.</summary>
        public int MaceIndex => Chain.Entity?.MaceIndex ?? Mathf.Clamp(Data.MaceIndex, MinIndex, MaxIndex);

        public float MaceMass
        {
            get => _maceMass ?? Data.MaceMass;
            set
            {
                _maceMass = Mathf.Max(0.01f, value);
                Retune();
            }
        }

        public float HitRadius
        {
            get => _hitRadius ?? Data.HitRadius;
            set
            {
                _hitRadius = Mathf.Max(0f, value);
                Retune();
            }
        }

        public float DamagePerSpeed
        {
            get => _damagePerSpeed ?? Data.DamagePerSpeed;
            set
            {
                _damagePerSpeed = Mathf.Max(0f, value);
                Retune();
            }
        }

        public Transform MaceVisual
        {
            get => maceVisual;
            set => maceVisual = value;
        }

        /// <summary>Mace size relative to the chain's line width while following (chain.json's maceScale, Chain.vb's 3.5x). Assumes the visual is one world unit across at scale 1.</summary>
        public float MaceScale
        {
            get => _maceScale ?? Chain.Settings.MaceScale;
            set => _maceScale = Mathf.Max(0f, value);
        }

        /// <summary>Render-only offset of the visual from the mace link, in world units (none by default; BIBLE D8).</summary>
        public Vector2 SpriteOffset
        {
            get => _spriteOffset;
            set => _spriteOffset = value;
        }

        private PlayerSetup Data => Chain.Settings.Player;

        /// <summary>The mace link's simulated position.</summary>
        public Vector2 MacePosition => Chain.GetLinkPosition(MaceIndex);

        /// <summary>The mace link's velocity (the grapple throw's launch velocity).</summary>
        public Vector2 MaceVelocity => Chain.GetLinkVelocity(MaceIndex);

        /// <summary>Called when the chain's player is seated.</summary>
        public void Bind(ChainController chain) => _chain = chain;

        /// <summary>Puts the mace's overrides over the data files' player setup.</summary>
        public void Contribute(ref PlayerSetup setup)
        {
            setup.MaceMass = _maceMass ?? setup.MaceMass;
            setup.HitRadius = _hitRadius ?? setup.HitRadius;
            setup.DamagePerSpeed = _damagePerSpeed ?? setup.DamagePerSpeed;
        }

        /// <summary>One link shorter (Chain.vb's chain-minus key). Returns false at the minimum or before the player is seated.</summary>
        public bool Shorten() => Send(-1);

        /// <summary>One link longer (Chain.vb's chain-plus key). Returns false at the maximum or before the player is seated.</summary>
        public bool Lengthen() => Send(1);

        /// <summary>
        /// The visual's size relative to the chain's line width: <see cref="MaceScale"/> while the
        /// chain follows, one link's width when it does not (Chain.vb skips the 3.5x then).
        /// </summary>
        public float CurrentScale => ChainFollow.MaceScaleFor(Chain.IsFollowing, MaceScale);

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
            maceVisual.position = linkPosition + (following ? (Vector3)_spriteOffset : Vector3.zero);

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

        private void LateUpdate() => RenderMace();
    }
}
