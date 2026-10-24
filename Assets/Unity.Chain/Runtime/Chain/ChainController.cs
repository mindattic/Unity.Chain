using MindAttic.Chain.Core;
using UnityEngine;

namespace MindAttic.Chain
{
    /// <summary>
    /// Draws one player's chain. The chain itself (<see cref="ChainSimulation"/>, link 0 pinned to the
    /// angel's hand) lives in the <see cref="GameWorld"/> and is stepped by the authority, tuned from
    /// chain.json (the session's <see cref="GameSettings"/>); this component can override a few of those
    /// figures at run time (live tuning, tests), which the world takes when the player is seated, and each
    /// frame writes the live links into a world-space <see cref="LineRenderer"/>, interpolating between
    /// the last two physics steps so the chain stays smooth when the render rate and the fixed timestep
    /// differ. Seating it also binds the mace, grapple and audio components beside it.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LineRenderer))]
    public sealed class ChainController : MonoBehaviour
    {
        [Header("Rendering")]
        [Tooltip("Blend between the previous and current physics step when drawing.")]
        [SerializeField] private bool interpolate = true;

        // Run-time overrides of chain.json (null: the data file's figure).
        private int? _linkCount;
        private float? _overheadBoost;
        private float? _overheadMargin;
        private bool? _keepInLevel;
        private float? _wallBounce;

        private LineRenderer _line;
        private ChainSimulation _captured;
        private Vector2[] _previous = new Vector2[0];
        private Vector2[] _latest = new Vector2[0];
        private Vector3[] _renderPositions = new Vector3[0];
        private float _lastStepTime;

        /// <summary>The session that owns this chain's world, once seated.</summary>
        public GameSession Session { get; private set; }

        /// <summary>Whose chain this is.</summary>
        public PlayerId Player { get; private set; }

        /// <summary>This player in the world (null until seated).</summary>
        public PlayerEntity Entity => Session != null ? Session.World.FindPlayer(Player) : null;

        /// <summary>The world's chain for this player (null until seated). Read only.</summary>
        public ChainSimulation Simulation => Entity?.Chain;

        /// <summary>The settings this chain is tuned from: its session's, or the data files' before it is seated.</summary>
        public GameSettings Settings => Session != null ? Session.Settings : SettingsStore.Current;

        private PlayerSetup Data => Settings.Player;

        public int LinkCount => _linkCount ?? Data.LinkCount;

        /// <summary>How many links (anchor included) are live: the mace's link + 1.</summary>
        public int ActiveLinkCount => Simulation != null ? Simulation.ActiveLinkCount : LinkCount;

        public bool KeepInLevel
        {
            get => _keepInLevel ?? Data.KeepChainInArena;
            set
            {
                _keepInLevel = value;
                Retune();
            }
        }

        public float WallBounce
        {
            get => _wallBounce ?? Data.WallBounce;
            set
            {
                _wallBounce = Mathf.Clamp01(value);
                Retune();
            }
        }

        public bool Interpolate
        {
            get => interpolate;
            set => interpolate = value;
        }

        public float OverheadBoost
        {
            get => _overheadBoost ?? Data.Chain.OverheadBoost;
            set
            {
                _overheadBoost = Mathf.Max(1f, value);
                Retune();
            }
        }

        public float OverheadMargin
        {
            get => _overheadMargin ?? Data.Chain.OverheadMargin;
            set
            {
                _overheadMargin = Mathf.Max(0f, value);
                Retune();
            }
        }

        /// <summary>
        /// Chain.vb's <c>followMouse</c>: true pulls the links by their springs; false leaves the
        /// springs at zero so the chain falls under gravity and the mace neither hits nor catches
        /// (see <see cref="ChainFollow"/>). The world's flag once seated, else the starting one.
        /// </summary>
        public bool IsFollowing => Simulation != null ? Simulation.IsFollowing : Data.Following;

        /// <summary>
        /// The F key: sends this chain's player a <see cref="Core.ToggleFollow"/> command; the world
        /// flips the flag and raises <see cref="FollowChanged"/>. Returns the new state.
        /// </summary>
        public bool ToggleFollow()
        {
            if (Session != null)
            {
                Session.Submit(new Core.ToggleFollow(Player));
            }

            return IsFollowing;
        }

        /// <summary>
        /// Form1.vb's live-tuning keys: sends this chain's player a <see cref="TuneChain"/> command moving
        /// <paramref name="knob"/> by <paramref name="steps"/> of its step; the world holds it to chain.json's
        /// limits and raises <see cref="ChainTuned"/>. Returns false if nothing moved.
        /// </summary>
        public bool Tune(ChainKnob knob, int steps) =>
            Session != null && Session.Submit(new TuneChain(Player, knob, steps));

        /// <summary>chain.json's spring figures (in world units and seconds) with this chain's overrides.</summary>
        public ChainSimulation.Settings CurrentSettings
        {
            get
            {
                var data = Data.Chain;
                return new ChainSimulation.Settings(
                    data.SegmentLength,
                    data.SpringConstant,
                    data.Resistance,
                    data.Gravity,
                    data.StopVelocity,
                    data.StopAcceleration,
                    OverheadBoost,
                    OverheadMargin);
            }
        }

        public Vector2 GetLinkPosition(int index) => Simulation.GetPosition(index).ToUnity();

        public Vector2 GetLinkVelocity(int index) => Simulation.GetVelocity(index).ToUnity();

        /// <summary>Where link <paramref name="index"/> was last drawn (interpolated), in world space.</summary>
        public Vector3 GetRenderedLinkPosition(int index) => _renderPositions[index];

        /// <summary>Called by the session when it seats this chain's player; binds the mace, grapple and audio beside it too.</summary>
        public void Bind(GameSession session, PlayerId player)
        {
            Session = session;
            Player = player;

            var mace = GetComponent<MaceController>();
            if (mace != null)
            {
                mace.Bind(this);
            }

            var grapple = GetComponent<GrappleController>();
            if (grapple != null)
            {
                grapple.Bind(this);
            }

            var audio = GetComponent<ChainAudio>();
            if (audio != null)
            {
                audio.Bind(this);
            }

            CaptureStep();
            RenderChain(1f);
        }

        /// <summary>Puts this chain's overrides (and the mace's and grapple's) over the data files' player setup.</summary>
        public void Contribute(ref PlayerSetup setup)
        {
            setup.LinkCount = Mathf.Max(2, _linkCount ?? setup.LinkCount);
            setup.Chain = new ChainSimulation.Settings(
                setup.Chain.SegmentLength,
                setup.Chain.SpringConstant,
                setup.Chain.Resistance,
                setup.Chain.Gravity,
                setup.Chain.StopVelocity,
                setup.Chain.StopAcceleration,
                _overheadBoost ?? setup.Chain.OverheadBoost,
                _overheadMargin ?? setup.Chain.OverheadMargin);
            setup.KeepChainInArena = _keepInLevel ?? setup.KeepChainInArena;
            setup.WallBounce = _wallBounce ?? setup.WallBounce;

            var mace = GetComponent<MaceController>();
            if (mace != null)
            {
                mace.Contribute(ref setup);
            }

            var grapple = GetComponent<GrappleController>();
            if (grapple != null)
            {
                grapple.Contribute(ref setup);
            }
        }

        /// <summary>Changes the link count; the world rebuilds the chain at the hand.</summary>
        public void SetLinkCount(int count)
        {
            _linkCount = Mathf.Max(2, count);
            Retune();
        }

        /// <summary>
        /// Remembers the pose of the step just taken, and the one before it, so drawing can blend
        /// between them. Called from <c>FixedUpdate</c>, once per authority step and after it; public
        /// so tests can drive it.
        /// </summary>
        public void CaptureStep()
        {
            var simulation = Simulation;
            if (simulation == null)
            {
                return;
            }

            var count = simulation.LinkCount;
            var fresh = simulation != _captured || _latest.Length != count;
            if (fresh)
            {
                // A new chain (seated, reset, rebuilt): nothing to blend from.
                _previous = new Vector2[count];
                _latest = new Vector2[count];
                _renderPositions = new Vector3[count];
                _captured = simulation;
            }

            for (var i = 0; i < count; i++)
            {
                var now = simulation.GetPosition(i).ToUnity();
                _previous[i] = fresh ? now : _latest[i];
                _latest[i] = now;
            }

            _lastStepTime = Time.fixedTime;
        }

        /// <summary>
        /// Writes the chain into the LineRenderer. <paramref name="blend"/> is how far between the
        /// previous physics step (0) and the current one (1) to draw.
        /// </summary>
        public void RenderChain(float blend)
        {
            var simulation = Simulation;
            if (simulation == null)
            {
                return;
            }

            if (simulation != _captured)
            {
                CaptureStep();
            }

            blend = Mathf.Clamp01(blend);
            var z = transform.position.z;
            for (var i = 0; i < _previous.Length; i++)
            {
                var p = Vector2.LerpUnclamped(_previous[i], simulation.GetPosition(i).ToUnity(), blend);
                _renderPositions[i] = new Vector3(p.x, p.y, z);
            }

            // Only the live part of the chain is drawn; the parked tail stays hidden.
            var line = Line;
            line.useWorldSpace = true;
            var visible = simulation.ActiveLinkCount;
            if (line.positionCount != visible)
            {
                line.positionCount = visible;
            }

            for (var i = 0; i < visible; i++)
            {
                line.SetPosition(i, _renderPositions[i]);
            }
        }

        private LineRenderer Line => _line != null ? _line : _line = GetComponent<LineRenderer>();

        private void Retune()
        {
            if (Session != null)
            {
                Session.Retune(Player);
            }
        }

        private void FixedUpdate() => CaptureStep();

        private void LateUpdate()
        {
            var blend = 1f;
            if (interpolate && Time.fixedDeltaTime > 0f)
            {
                blend = (Time.time - _lastStepTime) / Time.fixedDeltaTime;
            }

            RenderChain(blend);
        }

    }
}
