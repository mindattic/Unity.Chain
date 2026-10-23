using System;
using MindAttic.Chain.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MindAttic.Chain
{
    /// <summary>
    /// Drives a <see cref="ChainSimulation"/> from <c>FixedUpdate</c>, pins link 0 to the
    /// player's hand socket every physics step (link(0) tracking the angel in Chain.vb), and
    /// draws the chain through a world-space <see cref="LineRenderer"/> each frame. Rendering
    /// interpolates between the last two physics steps so the chain stays smooth when the
    /// render rate and the fixed timestep differ.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LineRenderer))]
    public sealed class ChainController : MonoBehaviour
    {
        [Header("Anchor")]
        [Tooltip("Link 0 is snapped here every physics step. Falls back to this transform when unset.")]
        [SerializeField] private Transform handSocket;

        [Header("Chain")]
        [Min(2)]
        [SerializeField] private int linkCount = 25;
        [Min(0f)]
        [SerializeField] private float segmentLength = 0.2f;
        [Min(0f)]
        [SerializeField] private float springConstant = 60f;
        [Min(0f)]
        [SerializeField] private float resistance = 4f;
        [SerializeField] private Vector2 gravity = new Vector2(0f, -9.8f);
        [Min(0f)]
        [SerializeField] private float stopVelocity = 0.0005f;
        [Min(0f)]
        [SerializeField] private float stopAcceleration = 0.0005f;

        [Header("Overhead swing")]
        [Tooltip("Multiplier on a link's upward spring pull while it is above the hand (Chain.vb doubles it). 1 = off.")]
        [Min(1f)]
        [SerializeField] private float overheadBoost = 2f;
        [Tooltip("How far below the hand a link still counts as overhead (Chain.vb: one link size).")]
        [Min(0f)]
        [SerializeField] private float overheadMargin = 0.25f;

        [Header("Level bounds")]
        [Tooltip("Bounce links off the level's walls (Elastic Trail's wall bounce) so the mace cannot be swung out of the play space.")]
        [SerializeField] private bool keepInLevel = true;
        [Tooltip("The play space. Falls back to the scene's LevelBounds.")]
        [SerializeField] private LevelBounds levelBounds;
        [Tooltip("Fraction of a link's speed into a wall kept after the bounce.")]
        [Range(0f, 1f)]
        [SerializeField] private float wallBounce = 0.5f;

        [Header("Follow (F key)")]
        [Tooltip("Chain.vb's followMouse: off, every spring force is zero and the chain falls under gravity.")]
        [SerializeField] private bool following = ChainFollow.StartsFollowing;
        [Tooltip("The player this chain belongs to; carried by the follow command and its event.")]
        [SerializeField] private int playerId;
        [SerializeField] private InputAction toggleFollow = new InputAction("Toggle Follow", InputActionType.Button, "<Keyboard>/f");

        [Header("Rendering")]
        [Tooltip("Blend between the previous and current physics step when drawing.")]
        [SerializeField] private bool interpolate = true;

        private ChainSimulation _simulation;
        private LineRenderer _line;
        private Vector2[] _previous;
        private Vector3[] _renderPositions;
        private float _lastStepTime;
        private int _activeLinkCount = -1;
        private bool _levelSearched;

        public ChainSimulation Simulation
        {
            get
            {
                EnsureInitialized();
                return _simulation;
            }
        }

        public int LinkCount => linkCount;

        public Transform HandSocket
        {
            get => handSocket;
            set => handSocket = value;
        }

        /// <summary>The play space links bounce inside, or null (none assigned or found).</summary>
        public LevelBounds LevelBounds
        {
            get => levelBounds;
            set
            {
                levelBounds = value;
                _levelSearched = value != null;
            }
        }

        public bool KeepInLevel
        {
            get => keepInLevel;
            set => keepInLevel = value;
        }

        public float WallBounce
        {
            get => wallBounce;
            set => wallBounce = Mathf.Clamp01(value);
        }

        public bool Interpolate
        {
            get => interpolate;
            set => interpolate = value;
        }

        /// <summary>Raised when the F key (or <see cref="ToggleFollow"/>) changes the follow flag.</summary>
        public event Action<FollowChangedEvent> FollowChanged;

        /// <summary>
        /// Chain.vb's <c>followMouse</c>: true pulls the links by their springs; false leaves the
        /// springs at zero so the chain falls under gravity (see <see cref="ChainFollow"/>).
        /// </summary>
        public bool IsFollowing
        {
            get => following;
            set
            {
                following = value;
                if (_simulation != null)
                {
                    _simulation.IsFollowing = value;
                }
            }
        }

        public int PlayerId
        {
            get => playerId;
            set => playerId = value;
        }

        /// <summary>
        /// The F key: sends this chain's player a <see cref="ToggleFollowCommand"/> through Core and
        /// raises <see cref="FollowChanged"/> with the event it produces. Returns the new state.
        /// </summary>
        public bool ToggleFollow()
        {
            EnsureInitialized();
            _simulation.IsFollowing = following;
            var changed = ChainFollow.Apply(_simulation, new ToggleFollowCommand(playerId));
            following = changed.IsFollowing;
            FollowChanged?.Invoke(changed);
            return following;
        }

        /// <summary>The world point link 0 is pinned to: the hand socket, or this transform.</summary>
        public Vector2 AnchorPosition => (handSocket != null ? handSocket : transform).position;

        public ChainSimulation.Settings CurrentSettings => new ChainSimulation.Settings(
            segmentLength,
            springConstant,
            resistance,
            gravity.ToCore(),
            stopVelocity,
            stopAcceleration,
            overheadBoost,
            overheadMargin);

        public float OverheadBoost
        {
            get => overheadBoost;
            set => overheadBoost = Mathf.Max(1f, value);
        }

        public float OverheadMargin
        {
            get => overheadMargin;
            set => overheadMargin = Mathf.Max(0f, value);
        }

        /// <summary>
        /// How many links (anchor included) are simulated and drawn; the rest stay parked on the
        /// end link. Lets the chain be shortened/lengthened live without a rebuild. Defaults to
        /// every link; clamped to [2, LinkCount].
        /// </summary>
        public int ActiveLinkCount
        {
            get => _activeLinkCount < 0 ? linkCount : Mathf.Clamp(_activeLinkCount, 2, linkCount);
            set
            {
                _activeLinkCount = Mathf.Clamp(value, 2, Mathf.Max(2, linkCount));
                if (_simulation != null)
                {
                    _simulation.ActiveLinkCount = _activeLinkCount;
                }
            }
        }

        public Vector2 GetLinkPosition(int index) => Simulation.GetPosition(index).ToUnity();

        public Vector2 GetLinkVelocity(int index) => Simulation.GetVelocity(index).ToUnity();

        /// <summary>Where link <paramref name="index"/> was last drawn (interpolated), in world space.</summary>
        public Vector3 GetRenderedLinkPosition(int index)
        {
            EnsureInitialized();
            return _renderPositions[index];
        }

        /// <summary>
        /// Changes the link count and rebuilds the chain, all links starting at the anchor. For
        /// live shortening/lengthening use <see cref="ActiveLinkCount"/> instead.
        /// </summary>
        public void SetLinkCount(int count)
        {
            linkCount = Mathf.Max(2, count);
            Rebuild();
        }

        /// <summary>Recreates the simulation with every link collapsed onto the anchor.</summary>
        public void Rebuild()
        {
            linkCount = Mathf.Max(2, linkCount);
            _line = GetComponent<LineRenderer>();
            _line.useWorldSpace = true;

            _simulation = new ChainSimulation(linkCount, AnchorPosition.ToCore(), CurrentSettings);
            _simulation.ActiveLinkCount = ActiveLinkCount;
            _simulation.IsFollowing = following;
            _previous = new Vector2[linkCount];
            _renderPositions = new Vector3[linkCount];
            CaptureCurrent(_previous);
            _lastStepTime = Time.fixedTime;
            RenderChain(1f);
        }

        /// <summary>
        /// One physics step: re-pin link 0 to the hand socket, then integrate. Called from
        /// <c>FixedUpdate</c> with <c>Time.fixedDeltaTime</c>; public so tests can drive it.
        /// </summary>
        public void Simulate(float deltaTime)
        {
            EnsureInitialized();
            CaptureCurrent(_previous);
            _simulation.Config = CurrentSettings;
            _simulation.Bounds = ResolveBounds();
            _simulation.WallBounce = wallBounce;
            _simulation.IsFollowing = following;
            _simulation.SetAnchor(AnchorPosition.ToCore());
            _simulation.Step(deltaTime);
        }

        /// <summary>
        /// Writes the chain into the LineRenderer. <paramref name="blend"/> is how far between the
        /// previous physics step (0) and the current one (1) to draw.
        /// </summary>
        public void RenderChain(float blend)
        {
            EnsureInitialized();
            blend = Mathf.Clamp01(blend);
            var z = (handSocket != null ? handSocket : transform).position.z;

            for (var i = 0; i < linkCount; i++)
            {
                var p = Vector2.LerpUnclamped(_previous[i], _simulation.GetPosition(i).ToUnity(), blend);
                _renderPositions[i] = new Vector3(p.x, p.y, z);
            }

            // Only the live part of the chain is drawn; the parked tail stays hidden.
            var visible = _simulation.ActiveLinkCount;
            if (_line.positionCount != visible)
            {
                _line.positionCount = visible;
            }

            for (var i = 0; i < visible; i++)
            {
                _line.SetPosition(i, _renderPositions[i]);
            }
        }

        private void Awake() => EnsureInitialized();

        private void OnEnable() => toggleFollow.Enable();

        private void OnDisable() => toggleFollow.Disable();

        private void Update()
        {
            if (toggleFollow.WasPressedThisFrame())
            {
                ToggleFollow();
            }
        }

        private void FixedUpdate()
        {
            Simulate(Time.fixedDeltaTime);
            _lastStepTime = Time.fixedTime;
        }

        private void LateUpdate()
        {
            var blend = 1f;
            if (interpolate && Time.fixedDeltaTime > 0f)
            {
                blend = (Time.time - _lastStepTime) / Time.fixedDeltaTime;
            }

            RenderChain(blend);
        }

        private void OnValidate()
        {
            linkCount = Mathf.Max(2, linkCount);
            if (_simulation == null)
            {
                return;
            }

            if (_simulation.LinkCount != linkCount)
            {
                Rebuild();
            }
            else
            {
                _simulation.Config = CurrentSettings;
            }
        }

        private void EnsureInitialized()
        {
            if (_simulation == null || _simulation.LinkCount != linkCount)
            {
                Rebuild();
            }
        }

        private Rect2? ResolveBounds()
        {
            if (!keepInLevel)
            {
                return null;
            }

            // Look the scene's LevelBounds up once, not every physics step.
            if (levelBounds == null && !_levelSearched)
            {
                levelBounds = LevelBounds.Find();
                _levelSearched = true;
            }

            return levelBounds != null ? levelBounds.Bounds.ToCore() : (Rect2?)null;
        }

        private void CaptureCurrent(Vector2[] into)
        {
            for (var i = 0; i < into.Length; i++)
            {
                into[i] = _simulation.GetPosition(i).ToUnity();
            }
        }
    }
}
