using MindAttic.Chain.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MindAttic.Chain
{
    /// <summary>
    /// One player's angel: their input and the angel's look. It reads the mouse delta and sends it to
    /// the authority as a <see cref="SteerAngel"/> command with this player's id, and P or Escape as
    /// <see cref="TogglePause"/>; then it draws the angel where the world's <see cref="AngelFlight"/>
    /// says (position, facing, wing-flap frame) and moves the optional hand-socket transform to the
    /// hand. It never moves the angel itself. Its tuning (sensitivity, bounds, hover, flap) is handed
    /// to the world when the session seats it (<see cref="GameSession.Join(AngelController)"/>):
    /// bounds come from the level's <see cref="LevelBounds"/> (assigned, or found in the scene) inset by
    /// <see cref="boundsMargin"/>; with no level they fall back to the camera's orthographic view,
    /// then to <see cref="fallbackBounds"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AngelController : MonoBehaviour
    {
        [Header("Player")]
        [Tooltip("This player's seat: the player id every command from this angel carries.")]
        [Min(0)]
        [SerializeField] private int seat;
        [Tooltip("This player's chain. Falls back to the scene's ChainController.")]
        [SerializeField] private ChainController chain;

        [Header("Flight")]
        [Tooltip("World units moved per pixel of mouse motion.")]
        [SerializeField] private float sensitivity = 0.01f;
        [Tooltip("Lock and hide the OS cursor while playing, so the mouse steers the angel only.")]
        [SerializeField] private bool lockCursor = true;

        [Header("Bounds")]
        [Tooltip("The level's play space. Falls back to the scene's LevelBounds, then to the camera view.")]
        [SerializeField] private LevelBounds levelBounds;
        [Tooltip("Derive bounds from this camera's orthographic view. Falls back to Camera.main.")]
        [SerializeField] private Camera boundsCamera;
        [Tooltip("Inset from the level (or camera) edges, world units, so the sprite stays fully inside.")]
        [SerializeField] private Vector2 boundsMargin = new Vector2(0.5f, 0.6f);
        [Tooltip("Used when there is no orthographic camera.")]
        [SerializeField] private Rect fallbackBounds = new Rect(-8f, -4.5f, 16f, 9f);

        [Header("Hover")]
        [SerializeField] private float hoverAmplitude = 0.08f;
        [SerializeField] private float hoverPeriod = 1.6f;

        [Header("Animation")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [Tooltip("Wing-flap frames facing right.")]
        [SerializeField] private Sprite[] rightFrames = new Sprite[0];
        [Tooltip("Wing-flap frames facing left. Leave empty to mirror the right frames with flipX.")]
        [SerializeField] private Sprite[] leftFrames = new Sprite[0];
        [SerializeField] private float frameDuration = 0.1f;

        [Header("Chain")]
        [Tooltip("Hand socket offset from the body when facing right; mirrored when facing left.")]
        [SerializeField] private Vector2 handOffset = new Vector2(0.25f, -0.1f);
        [Tooltip("Optional transform moved to the hand socket every frame (link 0 of the chain hangs from the hand).")]
        [SerializeField] private Transform handSocketTransform;

        private bool _levelSearched;

        /// <summary>The session this angel sends its commands to, once seated.</summary>
        public GameSession Session { get; private set; }

        /// <summary>This angel's player id.</summary>
        public PlayerId Seat
        {
            get => new PlayerId(seat);
            set => seat = Mathf.Max(0, value.Value);
        }

        public ChainController Chain
        {
            get
            {
                if (chain == null)
                {
                    chain = FindAnyObjectByType<ChainController>();
                }

                return chain;
            }
            set => chain = value;
        }

        /// <summary>The world's flight model for this angel (null until seated). Read only.</summary>
        public AngelFlight Flight => Session != null ? Session.World.FindPlayer(Seat)?.Flight : null;

        public Facing Facing => Flight != null ? Flight.Facing : Facing.Right;

        public LevelBounds LevelBounds
        {
            get => levelBounds;
            set
            {
                levelBounds = value;
                _levelSearched = value != null;
            }
        }

        /// <summary>The rect the flight position is clamped to.</summary>
        public Rect Bounds => ComputeBounds();

        public Vector2 HandSocket => Flight != null ? Flight.HandSocket.ToUnity() : (Vector2)transform.position;

        /// <summary>Called by the session when it seats this angel.</summary>
        public void Bind(GameSession session, PlayerId id)
        {
            Session = session;
            seat = id.Value;
            Apply();
        }

        /// <summary>Hands this angel's tuning to the world's player setup.</summary>
        public void Contribute(ref PlayerSetup setup)
        {
            setup.Start = transform.position.ToCore();
            setup.Flight = BuildSettings();
        }

        /// <summary>
        /// This frame's input: the mouse delta steers, P or Escape pauses. Called from <c>Update</c>;
        /// public so tests can drive it. Returns true if the steer was applied.
        /// </summary>
        public bool SendInput(Vector2 mouseDelta, float deltaTime, bool pausePressed = false)
        {
            if (Session == null)
            {
                return false;
            }

            if (pausePressed)
            {
                Session.Submit(new TogglePause(Seat));
            }

            var steered = Session.Submit(new SteerAngel(Seat, mouseDelta.ToCore(), deltaTime));
            Apply();
            return steered;
        }

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
        }

        private void OnEnable()
        {
            if (lockCursor)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void OnDisable()
        {
            if (lockCursor)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void Update()
        {
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            var delta = mouse != null ? mouse.delta.ReadValue() : Vector2.zero;
            var pause = keyboard != null && (keyboard.pKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame);
            SendInput(delta, Time.deltaTime, pause);
        }

        private void OnValidate()
        {
            if (Session != null)
            {
                Session.Retune(Seat);
            }
        }

        private AngelFlight.Settings BuildSettings()
        {
            var frameCount = Mathf.Max(rightFrames.Length, leftFrames.Length);
            return new AngelFlight.Settings(
                sensitivity,
                ComputeBounds().ToCore(),
                hoverAmplitude,
                hoverPeriod,
                frameDuration,
                Mathf.Max(1, frameCount),
                handOffset.ToCore());
        }

        private Rect ComputeBounds()
        {
            if (levelBounds == null && !_levelSearched)
            {
                levelBounds = LevelBounds.Find();
                _levelSearched = true;
            }

            if (levelBounds != null)
            {
                return levelBounds.Inset(boundsMargin);
            }

            var cam = boundsCamera != null ? boundsCamera : Camera.main;
            if (cam == null || !cam.orthographic)
            {
                return fallbackBounds;
            }

            var halfHeight = cam.orthographicSize;
            var halfWidth = halfHeight * cam.aspect;
            var center = (Vector2)cam.transform.position;
            var hx = Mathf.Max(0f, halfWidth - boundsMargin.x);
            var hy = Mathf.Max(0f, halfHeight - boundsMargin.y);
            return new Rect(center.x - hx, center.y - hy, hx * 2f, hy * 2f);
        }

        /// <summary>Draws the angel where the world has it.</summary>
        private void Apply()
        {
            var flight = Flight;
            if (flight == null)
            {
                return;
            }

            var p = flight.RenderPosition.ToUnity();
            transform.position = new Vector3(p.x, p.y, transform.position.z);

            if (handSocketTransform != null)
            {
                var hand = flight.HandSocket.ToUnity();
                handSocketTransform.position = new Vector3(hand.x, hand.y, handSocketTransform.position.z);
            }

            if (spriteRenderer == null)
            {
                return;
            }

            var facingLeft = flight.Facing == Facing.Left;
            var useLeftSet = facingLeft && leftFrames.Length > 0;
            var frames = useLeftSet ? leftFrames : rightFrames;
            spriteRenderer.flipX = facingLeft && !useLeftSet;

            if (frames.Length > 0)
            {
                spriteRenderer.sprite = frames[flight.Frame % frames.Length];
            }
        }

        private void OnDrawGizmosSelected()
        {
            var b = Flight != null ? Flight.Config.Bounds.ToUnity() : ComputeBounds();
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(b.center, b.size);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(HandSocket, 0.05f);
        }
    }
}
