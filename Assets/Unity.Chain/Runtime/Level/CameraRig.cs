using UnityEngine;

namespace MindAttic.Chain
{
    public enum CameraFramingMode
    {
        /// <summary>Static camera that shows the whole level, letterboxed to any aspect (the original's fixed view).</summary>
        FitLevel,
        /// <summary>Fixed zoom that follows the target with a dead zone, never showing past the level's edges.</summary>
        Follow,
    }

    /// <summary>
    /// Frames the play space with this orthographic camera. In <see cref="CameraFramingMode.FitLevel"/>
    /// the view is sized and centered to show all of <see cref="LevelBounds"/> on any aspect
    /// ratio; in <see cref="CameraFramingMode.Follow"/> it keeps a fixed orthographic size and
    /// follows the target (the angel by default) through a dead zone with smoothing, clamped so
    /// the view stays inside the level. Runs in <c>LateUpdate</c>, after the angel has moved.
    /// The math is in <see cref="CameraFraming"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(200)]
    public sealed class CameraRig : MonoBehaviour
    {
        [SerializeField] private CameraFramingMode mode = CameraFramingMode.FitLevel;
        [Tooltip("The play space to frame. Falls back to the scene's LevelBounds.")]
        [SerializeField] private LevelBounds level;

        [Header("Fit level")]
        [Tooltip("Extra world units shown around the level on every side.")]
        [Min(0f)]
        [SerializeField] private float padding = 0f;

        [Header("Follow")]
        [Tooltip("What to follow. Falls back to the scene's AngelController.")]
        [SerializeField] private Transform target;
        [Tooltip("Orthographic size (half the view height) while following.")]
        [Min(0.01f)]
        [SerializeField] private float followSize = 4.5f;
        [Tooltip("Half size of the box around the view center the target can move in without moving the camera.")]
        [SerializeField] private Vector2 deadZone = new Vector2(1.5f, 1f);
        [Tooltip("How quickly the camera catches up, per second. 0 = snaps.")]
        [Min(0f)]
        [SerializeField] private float sharpness = 6f;

        private Camera _camera;

        public CameraFramingMode Mode
        {
            get => mode;
            set => mode = value;
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

        public float FollowSize
        {
            get => followSize;
            set => followSize = Mathf.Max(0.01f, value);
        }

        public Vector2 DeadZoneHalf
        {
            get => deadZone;
            set => deadZone = value;
        }

        public float Sharpness
        {
            get => sharpness;
            set => sharpness = Mathf.Max(0f, value);
        }

        public float Padding
        {
            get => padding;
            set => padding = Mathf.Max(0f, value);
        }

        public Camera Camera => _camera != null ? _camera : _camera = GetComponent<Camera>();

        /// <summary>The world rect the camera currently shows.</summary>
        public Rect ViewRect
        {
            get
            {
                var half = CameraFraming.HalfExtents(Camera.orthographicSize, Camera.aspect);
                return new Rect((Vector2)transform.position - half, half * 2f);
            }
        }

        /// <summary>Jumps straight to the framing with no smoothing (scene start, restart).</summary>
        public void Snap() => Frame(0f, snap: true);

        /// <summary>One framing update. Called from <c>LateUpdate</c>; public so tests can drive it.</summary>
        public void Frame(float deltaTime) => Frame(deltaTime, snap: false);

        private void Frame(float deltaTime, bool snap)
        {
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

                cam.orthographicSize = CameraFraming.FitOrthographicSize(levelRect.size, cam.aspect, padding);
                next = levelRect.center;
            }
            else
            {
                cam.orthographicSize = followSize;
                var half = CameraFraming.HalfExtents(followSize, cam.aspect);
                var follow = ResolveTarget();
                var goal = follow != null
                    ? CameraFraming.DeadZone(current, follow.position, deadZone)
                    : current;

                if (hasLevel)
                {
                    goal = CameraFraming.ClampCenter(goal, half, levelRect);
                }

                next = snap ? goal : CameraFraming.Smooth(current, goal, sharpness, deltaTime);
                if (hasLevel)
                {
                    next = CameraFraming.ClampCenter(next, half, levelRect);
                }
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
            if (mode == CameraFramingMode.Follow)
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

        // Scaled time: while paused (timeScale 0) the camera holds still with the game.
        private void LateUpdate() => Frame(Time.deltaTime);

        private void OnDrawGizmosSelected()
        {
            if (mode != CameraFramingMode.Follow)
            {
                return;
            }

            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(transform.position, new Vector3(deadZone.x * 2f, deadZone.y * 2f, 0f));
        }
    }
}
