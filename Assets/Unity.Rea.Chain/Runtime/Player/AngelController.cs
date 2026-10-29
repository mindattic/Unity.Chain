using MindAttic.Chain.Core;
using UnityEngine;

namespace MindAttic.Chain
{
    /// <summary>
    /// One player's angel: their steering and the angel's look. Its <see cref="SeatInput"/> (required) reads the
    /// seat's controls; this sends the steer to the authority as a <see cref="SteerAngel"/> command with this
    /// player's id, and Pause as <see cref="TogglePause"/> (<see cref="SendInput"/>); then it draws the angel where the world's <see cref="AngelFlight"/>
    /// says (position, facing, wing-flap frame) and moves the optional hand-socket transform to the
    /// hand. It never moves the angel itself. Its tuning (sensitivity, hover, flap, hand offset: angel.json)
    /// comes from the data files through the session's <see cref="GameSettings"/>; what this component adds
    /// when the session seats it (<see cref="GameSession.Join(AngelController)"/>) is where it starts and, only with
    /// angel.json's <c>port.arenaBounds</c> (off by default; the default is Angel.vb's checkBounds, U.CHN-T86), its
    /// bounds: the level's <see cref="LevelBounds"/> (assigned, or found in the scene) inset by angel.json's
    /// <c>port.boundsMargin</c>; with no level, the camera's orthographic view, then the default arena.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SeatInput))]
    public sealed class AngelController : MonoBehaviour
    {
        [Header("Player")]
        [Tooltip("This player's seat: the player id every command from this angel carries.")]
        [Min(0)]
        [SerializeField] private int seat;
        [Tooltip("This player's chain. Falls back to the scene's ChainController.")]
        [SerializeField] private ChainController chain;

        [Header("Flight")]
        [Tooltip("Lock and hide the OS cursor while playing, so the mouse steers the angel only.")]
        [SerializeField] private bool lockCursor = true;

        [Header("Bounds")]
        [Tooltip("The level's play space. Falls back to the scene's LevelBounds, then to the camera view.")]
        [SerializeField] private LevelBounds levelBounds;
        [Tooltip("Derive bounds from this camera's orthographic view. Falls back to Camera.main.")]
        [SerializeField] private Camera boundsCamera;

        [Header("Animation")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [Tooltip("Wing-flap frames facing right.")]
        [SerializeField] private Sprite[] rightFrames = new Sprite[0];
        [Tooltip("Wing-flap frames facing left. Leave empty to mirror the right frames with flipX.")]
        [SerializeField] private Sprite[] leftFrames = new Sprite[0];

        [Header("Chain")]
        [Tooltip("Optional transform moved to the hand socket every frame (link 0 of the chain hangs from the hand).")]
        [SerializeField] private Transform handSocketTransform;

        private bool _levelSearched;
        // Where the angel stood when first seated: the sprite is drawn off it afterwards (U.CHN-T93).
        private Vector3? _home;

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
        public Rect Bounds => Flight != null ? Flight.Config.Bounds.ToUnity() : ComputeBounds(Settings.Angel.Port.BoundsMargin.ToUnity());

        private GameSettings Settings => Session != null ? Session.Settings : SettingsStore.Current;

        public Vector2 HandSocket => Flight != null ? Flight.HandSocket.ToUnity() : (Vector2)transform.position;

        /// <summary>Called by the session when it seats this angel.</summary>
        public void Bind(GameSession session, PlayerId id)
        {
            Session = session;
            seat = id.Value;
            Apply();
        }

        /// <summary>
        /// Adds this angel's place to the world's player setup (the rest is <paramref name="settings"/>' angel.json):
        /// where it starts, its bounds, and as many flap frames as it has sprites for (angel.json's flapFrames without art).
        /// </summary>
        public void Contribute(ref PlayerSetup setup, GameSettings settings)
        {
            var flight = setup.Flight;
            var sprites = Mathf.Max(rightFrames.Length, leftFrames.Length);
            _home = _home ?? transform.position;
            setup.Start = ((Vector2)_home.Value).ToCore();
            setup.Flight = new AngelFlight.Settings(
                flight.Sensitivity,
                settings.Angel.Port.ArenaBounds ? ComputeBounds(settings.Angel.Port.BoundsMargin.ToUnity()).ToCore() : flight.Bounds,
                flight.HoverAmplitude,
                flight.HoverPeriod,
                flight.FrameDuration,
                sprites > 0 ? sprites : flight.FrameCount,
                flight.HandOffset,
                flight.HandOffsetLeft);
        }

        /// <summary>
        /// This frame's input: the steer (mouse delta plus stick, in mouse counts) and whether Pause was pressed.
        /// Called by the seat's <see cref="SeatInput"/>; public so tests can drive it. Returns true if the steer was applied.
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

        private void OnValidate()
        {
            if (Session != null)
            {
                Session.Retune(Seat);
            }
        }

        private Rect ComputeBounds(Vector2 boundsMargin)
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
                return PlayArea.Inset(GameWorld.DefaultArena, boundsMargin.ToCore()).ToUnity();
            }

            var halfHeight = cam.orthographicSize;
            var halfWidth = halfHeight * cam.aspect;
            var center = (Vector2)cam.transform.position;
            var hx = Mathf.Max(0f, halfWidth - boundsMargin.x);
            var hy = Mathf.Max(0f, halfHeight - boundsMargin.y);
            return new Rect(center.x - hx, center.y - hy, hx * 2f, hy * 2f);
        }

        /// <summary>
        /// Draws the angel where the world has it: online as a client, where the remote authority has this screen's seat
        /// (<see cref="GameSession.Shown"/>, U.CHN-T73), its hand on the shown chain's link 0. Public so tests can drive it.
        /// </summary>
        public void Apply()
        {
            if (Session != null && Session.Shown != null)
            {
                if (Session.TryGetShownPlayer(out var shown))
                {
                    var links = shown.Links;
                    Draw(shown.Angel, links != null && links.Length > 0 ? links[0] : shown.Angel, shown.Facing, shown.Frame);
                }

                return;
            }

            var flight = Flight;
            if (flight == null)
            {
                return;
            }

            Draw(flight.RenderPosition, flight.HandSocket, flight.Facing, flight.Frame);
        }

        /// <summary>
        /// Where the sprite's centre is drawn from the body (the flight position plus the hover), world units: renderAngel
        /// draws the angelSize quad from X + angelOffset.X, centred on Y + angelOffset.Y (U.CHN-T93).
        /// </summary>
        public Vector2 SpriteOffset
        {
            get
            {
                var angel = Settings.Angel;
                var m = Settings.Mapping;
                return new Vector2(m.Length(angel.AngelOffset.X + angel.AngelSize / 2f), m.Length(angel.AngelOffset.Y));
            }
        }

        /// <summary>The width the whole sprite is drawn at: angel.json's angelSize quad, world units.</summary>
        public float SpriteWidth => Settings.Mapping.Length(Settings.Angel.AngelSize);

        private void Draw(Vec2 body, Vec2 handSocket, Facing facing, int frame)
        {
            var p = body.ToUnity() + SpriteOffset;
            transform.position = new Vector3(p.x, p.y, transform.position.z);

            if (handSocketTransform != null)
            {
                var hand = handSocket.ToUnity();
                handSocketTransform.position = new Vector3(hand.x, hand.y, handSocketTransform.position.z);
            }

            if (spriteRenderer == null)
            {
                return;
            }

            var facingLeft = facing == Facing.Left;
            var useLeftSet = facingLeft && leftFrames.Length > 0;
            var frames = useLeftSet ? leftFrames : rightFrames;
            spriteRenderer.flipX = facingLeft && !useLeftSet;

            if (frames.Length > 0)
            {
                spriteRenderer.sprite = frames[Mathf.Abs(frame) % frames.Length];
            }

            // The whole picture is drawn on renderAngel's angelSize quad (setupVertex), whatever its import size, so the
            // angel stands as large as in the original (U.CHN-T93; it had been shrunk to the port's contact circle).
            var sprite = spriteRenderer.sprite;
            if (sprite != null)
            {
                var nativeWidth = sprite.bounds.size.x;
                var width = SpriteWidth;
                if (nativeWidth > 0f && width > 0f)
                {
                    transform.localScale = Vector3.one * (width / nativeWidth);
                }
            }
        }

        // Online as a client the authority's snapshots move the angel whether or not this screen steers (U.CHN-T73);
        // in local play SendInput draws it after each steer, as before.
        private void LateUpdate()
        {
            if (Session != null && Session.Shown != null)
            {
                Apply();
            }
        }

        private void OnDrawGizmosSelected()
        {
            var b = Bounds;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(b.center, b.size);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(HandSocket, 0.05f);
        }
    }
}
