using System;
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
    /// Chain.vb's per-frame offset is not ported). The visual wears the player's mace skin (UNI-T43,
    /// <see cref="PlayerEntity.MaceMode"/>): the spike ball, or the magnet Chain.vb declared but never reached,
    /// chosen by chain.json's <c>maceSkins.mode</c> and flipped by the seat's ToggleMaceMode debug key
    /// (<see cref="ToggleMode"/>). While the grapple is armed it instead wears Input.vb's grapple picture
    /// (<see cref="GrappleSprite"/>, chain.json's <c>maceSkins.files.grapple</c>, Textures.vb's
    /// <c>texMaceGrapple</c>/mace2.png, UNI-T72), over whichever mode is current, reverting to that mode's
    /// picture on release. Only the picture changes; the world hits and grapples the same either way.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)] // after ChainController, so the visual tracks this frame's drawn chain
    [RequireComponent(typeof(ChainController))]
    public sealed class MaceController : MonoBehaviour
    {
        [Header("Rendering")]
        [Tooltip("The spiked-ball sprite (or any transform) drawn at the mace link.")]
        [SerializeField] private Transform maceVisual;

        [Tooltip("Each mace skin's picture, by mode (chain.json's maceSkins.files).")]
        [SerializeField] private SkinArt[] skins = Array.Empty<SkinArt>();

        [Tooltip("Input.vb's grapple picture (chain.json's maceSkins.files.grapple, texMaceGrapple/mace2.png), shown over the current mode's skin while the grapple is armed.")]
        [SerializeField] private Sprite grappleSprite;

        /// <summary>One mace skin's picture, under its <see cref="MaceMode"/>.</summary>
        [Serializable]
        public struct SkinArt
        {
            public SkinArt(MaceMode mode, Sprite sprite)
            {
                Mode = mode;
                Sprite = sprite;
            }

            public MaceMode Mode;
            public Sprite Sprite;
        }

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

        /// <summary>The mace skins' pictures, by mode.</summary>
        public SkinArt[] Skins
        {
            get => skins;
            set => skins = value ?? Array.Empty<SkinArt>();
        }

        /// <summary>What the mace wears: the world's once seated, else chain.json's starting skin.</summary>
        public MaceMode Mode => Chain.Entity?.MaceMode ?? Data.MaceMode;

        /// <summary>Input.vb's grapple picture, shown over the current mode's skin while the grapple is armed.</summary>
        public Sprite GrappleSprite
        {
            get => grappleSprite;
            set => grappleSprite = value;
        }

        /// <summary>
        /// Input.vb's left-mouse-down/up: true from grapple arm to release (armed or holding), whether or not a
        /// target is caught, regardless of this player's <see cref="MaceMode"/>. False before the player is seated.
        /// </summary>
        public bool IsGrappleArmed => Chain.Entity?.Grapple.IsArmed ?? false;

        /// <summary>The picture of <paramref name="mode"/>, or null if none is wired.</summary>
        public Sprite SpriteFor(MaceMode mode)
        {
            foreach (var art in skins)
            {
                if (art.Mode == mode)
                {
                    return art.Sprite;
                }
            }

            return null;
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
        /// The mace skin debug key: sends this player's <see cref="ToggleMaceMode"/>; the world puts the next skin on
        /// (spike ball to magnet and back) and raises <see cref="MaceModeChanged"/>. Returns false before the player is seated.
        /// </summary>
        public bool ToggleMode()
        {
            var session = Chain.Session;
            return session != null && session.Submit(new ToggleMaceMode(Chain.Player));
        }

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
            if (maceVisual == null)
            {
                return;
            }

            ApplySkin();
            if (Chain.Simulation == null)
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

        /// <summary>
        /// Puts the current picture on the visual's sprite renderer, when both are there: Input.vb's grapple
        /// picture while armed (<see cref="IsGrappleArmed"/>), else the current mode's own skin.
        /// </summary>
        private void ApplySkin()
        {
            var sprite = (IsGrappleArmed ? grappleSprite : null) ?? SpriteFor(Mode);
            if (sprite != null && maceVisual.TryGetComponent<SpriteRenderer>(out var renderer) && renderer.sprite != sprite)
            {
                renderer.sprite = sprite;
            }
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
