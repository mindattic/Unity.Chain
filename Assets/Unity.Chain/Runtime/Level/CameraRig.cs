using MindAttic.Chain.Core;
using UnityEngine;

namespace MindAttic.Chain
{
    /// <summary>
    /// Frames the play space with this orthographic camera. In <see cref="CameraFramingMode.FitLevel"/>
    /// the view is sized and centered to show all of <see cref="LevelBounds"/> on any aspect
    /// ratio; in <see cref="CameraFramingMode.Follow"/> it keeps a fixed orthographic size and
    /// follows the target (the angel by default) through a dead zone with smoothing, clamped so
    /// the view stays inside the level. Runs in <c>LateUpdate</c>, after the angel has moved.
    /// The math is in <see cref="CameraFraming"/>. Each reset of the session recentres it (<see cref="Recenter"/>).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(200)]
    public sealed class CameraRig : MonoBehaviour
    {
        [Tooltip("The play space to frame. Falls back to the scene's LevelBounds.")]
        [SerializeField] private LevelBounds level;

        [Tooltip("What to follow. Falls back to the scene's AngelController.")]
        [SerializeField] private Transform target;

        [Tooltip("Whose resets recentre the camera (Root.vb's Reset). Falls back to the scene's GameSession.")]
        [SerializeField] private GameSession session;

        private GameSession _subscribed;

        // camera.json's port figures, read on first use; the properties override them at run time.
        private bool _tuned;
        private CameraFramingMode mode;
        private float padding;
        private float followSize;
        private Vector2 deadZone;
        private float sharpness;

        private Camera _camera;

        public CameraFramingMode Mode
        {
            get => Tuned().mode;
            set => Tuned().mode = value;
        }

        public LevelBounds Level
        {
            get => level;
            set => level = value;
        }

        public Transform Target
        {
            get => target;
            set => target = value;
        }

        /// <summary>The session whose resets recentre the camera.</summary>
        public GameSession Session
        {
            get => session;
            set
            {
                Unsubscribe();
                session = value;
                if (isActiveAndEnabled)
                {
                    Subscribe();
                }
            }
        }

        /// <summary>Orthographic size (half the view height) while following.</summary>
        public float FollowSize
        {
            get => Tuned().followSize;
            set => Tuned().followSize = Mathf.Max(0.01f, value);
        }

        /// <summary>Half size of the box around the view center the target can move in without moving the camera.</summary>
        public Vector2 DeadZoneHalf
        {
            get => Tuned().deadZone;
            set => Tuned().deadZone = value;
        }

        /// <summary>How quickly the camera catches up, per second. 0 = snaps.</summary>
        public float Sharpness
        {
            get => Tuned().sharpness;
            set => Tuned().sharpness = Mathf.Max(0f, value);
        }

        /// <summary>Extra world units shown around the level on every side.</summary>
        public float Padding
        {
            get => Tuned().padding;
            set => Tuned().padding = Mathf.Max(0f, value);
        }

        /// <summary>Reads camera.json's framing the first time it is needed.</summary>
        private CameraRig Tuned()
        {
            if (!_tuned)
            {
                var tuning = SettingsStore.Current.CameraTuning;
                mode = tuning.Mode;
                padding = tuning.Padding;
                followSize = tuning.FollowSize;
                deadZone = tuning.DeadZone.ToUnity();
                sharpness = tuning.Sharpness;
                _tuned = true;
            }

            return this;
        }

        public Camera Camera => _camera != null ? _camera : _camera = GetComponent<Camera>();

        /// <summary>The world rect the camera currently shows.</summary>
        public Rect ViewRect
        {
            get
            {
                var half = CameraFraming.HalfExtents(Camera.orthographicSize, Camera.aspect).ToUnity();
                return new Rect((Vector2)transform.position - half, half * 2f);
            }
        }

        /// <summary>Jumps straight to the framing with no smoothing (scene start, restart).</summary>
        public void Snap() => Frame(0f, snap: true);

        /// <summary>
        /// Root.vb's Reset for the camera: <c>cam</c> and <c>target</c> back at the origin (x 0, y 0, where
        /// every angel starts again), so a shake or a debug pan is undone, then framed with no smoothing.
        /// </summary>
        public void Recenter()
        {
            transform.position = new Vector3(0f, 0f, transform.position.z);
            Snap();
        }

        /// <summary>One framing update. Called from <c>LateUpdate</c>; public so tests can drive it.</summary>
        public void Frame(float deltaTime) => Frame(deltaTime, snap: false);

        private void Frame(float deltaTime, bool snap)
        {
            Tuned();
            var cam = Camera;
            cam.orthographic = true;
            var levelRect = ResolveLevel(out var hasLevel);
            var current = (Vector2)transform.position;
            Vector2 next;

            if (mode == CameraFramingMode.FitLevel)
            {
                if (!hasLevel)
                {
                    return;
                }

                cam.orthographicSize = CameraFraming.FitOrthographicSize(levelRect.size.ToCore(), cam.aspect, padding);
                next = levelRect.center;
            }
            else
            {
                cam.orthographicSize = followSize;
                var half = CameraFraming.HalfExtents(followSize, cam.aspect);
                var follow = ResolveTarget();
                var currentCore = current.ToCore();
                var goal = follow != null
                    ? CameraFraming.DeadZone(currentCore, follow.position.ToCore(), deadZone.ToCore())
                    : currentCore;

                if (hasLevel)
                {
                    goal = CameraFraming.ClampCenter(goal, half, levelRect.ToCore());
                }

                var smoothed = snap ? goal : CameraFraming.Smooth(currentCore, goal, sharpness, deltaTime);
                if (hasLevel)
                {
                    smoothed = CameraFraming.ClampCenter(smoothed, half, levelRect.ToCore());
                }

                next = smoothed.ToUnity();
            }

            transform.position = new Vector3(next.x, next.y, transform.position.z);
        }

        private Rect ResolveLevel(out bool hasLevel)
        {
            if (level == null)
            {
                level = LevelBounds.Find();
            }

            hasLevel = level != null;
            return hasLevel ? level.Bounds : default;
        }

        private Transform ResolveTarget()
        {
            if (target == null)
            {
                var angel = FindAnyObjectByType<AngelController>();
                if (angel != null)
                {
                    target = angel.transform;
                }
            }

            return target;
        }

        private void Start()
        {
            if (Mode == CameraFramingMode.Follow)
            {
                // Start centered on the target rather than easing in from wherever the camera was placed.
                var follow = ResolveTarget();
                if (follow != null)
                {
                    transform.position = new Vector3(follow.position.x, follow.position.y, transform.position.z);
                }
            }

            Snap();
        }

        private void Awake()
        {
            if (session == null)
            {
                session = FindAnyObjectByType<GameSession>();
            }
        }

        private void OnEnable() => Subscribe();

        private void OnDisable() => Unsubscribe();

        private void Subscribe()
        {
            if (_subscribed == session)
            {
                return;
            }

            Unsubscribe();
            if (session != null)
            {
                session.Restarted += Recenter;
                _subscribed = session;
            }
        }

        private void Unsubscribe()
        {
            if (_subscribed != null)
            {
                _subscribed.Restarted -= Recenter;
                _subscribed = null;
            }
        }

        // Scaled time: while paused (timeScale 0) the camera holds still with the game.
        private void LateUpdate() => Frame(Time.deltaTime);

        private void OnDrawGizmosSelected()
        {
            if (Mode != CameraFramingMode.Follow)
            {
                return;
            }

            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(transform.position, new Vector3(deadZone.x * 2f, deadZone.y * 2f, 0f));
        }
    }
}
