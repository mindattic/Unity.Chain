using UnityEngine;

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

        [Header("Rendering")]
        [Tooltip("Blend between the previous and current physics step when drawing.")]
        [SerializeField] private bool interpolate = true;

        private ChainSimulation _simulation;
        private LineRenderer _line;
        private Vector2[] _previous;
        private Vector3[] _renderPositions;
        private float _lastStepTime;

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

        public bool Interpolate
        {
            get => interpolate;
            set => interpolate = value;
        }

        /// <summary>The world point link 0 is pinned to: the hand socket, or this transform.</summary>
        public Vector2 AnchorPosition => (handSocket != null ? handSocket : transform).position;

        public ChainSimulation.Settings CurrentSettings => new ChainSimulation.Settings(
            segmentLength,
            springConstant,
            resistance,
            gravity,
            stopVelocity,
            stopAcceleration);

        public Vector2 GetLinkPosition(int index) => Simulation.GetPosition(index);

        public Vector2 GetLinkVelocity(int index) => Simulation.GetVelocity(index);

        /// <summary>Changes the link count and rebuilds the chain, all links starting at the anchor.</summary>
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

            _simulation = new ChainSimulation(linkCount, AnchorPosition, CurrentSettings);
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
            _simulation.SetAnchor(AnchorPosition);
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
                var p = Vector2.LerpUnclamped(_previous[i], _simulation.GetPosition(i), blend);
                _renderPositions[i] = new Vector3(p.x, p.y, z);
            }

            if (_line.positionCount != linkCount)
            {
                _line.positionCount = linkCount;
            }

            _line.SetPositions(_renderPositions);
        }

        private void Awake() => EnsureInitialized();

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

        private void CaptureCurrent(Vector2[] into)
        {
            for (var i = 0; i < into.Length; i++)
            {
                into[i] = _simulation.GetPosition(i);
            }
        }
    }
}
