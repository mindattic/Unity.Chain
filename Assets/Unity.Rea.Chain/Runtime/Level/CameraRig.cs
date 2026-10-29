using MindAttic.Chain.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MindAttic.Chain
{
    /// <summary>
    /// Frames the play space with this orthographic camera. In <see cref="CameraFramingMode.FitLevel"/>
    /// the view is sized and centered to show all of <see cref="LevelBounds"/> on any aspect
    /// ratio; in <see cref="CameraFramingMode.Follow"/> it keeps a fixed orthographic size and
    /// follows the target (the angel by default) through a dead zone with smoothing, clamped so
    /// the view stays inside the level. Runs in <c>LateUpdate</c>, after the angel has moved.
    /// The math is in <see cref="CameraFraming"/>. Each reset of the session recentres it (<see cref="Recenter"/>).
    /// 0 (the Debug map's ShakeScreen, <see cref="ChainControls"/>) is Root.vb's <c>shakeScreen</c>: the
    /// <see cref="ScreenShake"/> offset, laid on top of the framing and taken off again before the next one, so the
    /// shake never feeds the follow or its smoothing.
    ///
    /// Input.vb's camera keys (the Debug map's CameraForward/Backward/Left/Right/Up/Down, Isometric, Deisometric) move
    /// Camera.vb's camera, a Core <see cref="DebugCamera"/> (<see cref="View"/>), on top of the framing too: its target
    /// moves the point the camera looks at, the eye's distance to that target scales the orthographic size (the
    /// original's perspective sees less as it nears the plane), and an eye off its target's axis (X and Z) turns the
    /// camera to look at the target at that slant. The original's perspective itself (<see cref="Perspective"/>: 45
    /// degrees, 4:3, z -1 looking at z 0) is kept as data; the rig draws orthographic (BIBLE V1). The keys are read every
    /// frame, paused too, as Input.vb polls them; like Engine.vb's <c>initCamera</c>, the view takes the move only while
    /// the game runs.
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
        private float originalHalfHeight;
        private float originalAspect;

        private Camera _camera;
        // Clears the bars around the original's 4:3 view on any other screen shape (U.CHN-T86).
        private Camera _bars;

        private ScreenShake _shake;
        // The shake offset on the view now, laid on after each framing so the base view is what is framed.
        private Vector2 _appliedShake;
        private ChainControls _controls;

        private DebugCamera _view;
        // Where the framing put the view's centre and its orthographic size, before the shake and the debug camera.
        private Vector2 _framedCenter;
        private float _framedSize;
        private bool _hasFramed;
        // The debug camera as the view last took it (Engine.vb rebuilds the camera only while not paused).
        private Vector2 _shownEye;
        private Vector2 _shownTarget;
        private float _shownEyeZ;
        private bool _hasShown;
        // How far behind the point it looks at the camera sits (its z distance as the scene placed it).
        private float _depth;
        private Vector2 _lookPoint;

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

        /// <summary>The 0 key's screen shake (camera.json's <c>shakeAmount</c>, in world units).</summary>
        public ScreenShake Shake => _shake ?? (_shake = new ScreenShake(SettingsStore.Current.ShakeAmount));

        /// <summary>The shake offset on the camera now, world units.</summary>
        public Vector2 ShakeOffset => _appliedShake;

        /// <summary>Camera.vb's camera, moved by Input.vb's numpad, X and Z keys (camera.json, world units).</summary>
        public DebugCamera View => _view ?? (_view = new DebugCamera(SettingsStore.Current.DebugCameraSettings));

        /// <summary>Camera.vb's lens and start, kept as data: fov pi / 4, aspect 4:3, the eye at z -1 (world units) looking at z 0.</summary>
        public DebugCamera.Settings Perspective => View.Config;

        /// <summary>The point the camera looks at now, world units: the framed centre plus the shake and the debug camera's target.</summary>
        public Vector2 LookPoint => _hasFramed ? _lookPoint : (Vector2)transform.position;

        /// <summary>
        /// Where Camera.vb's eye is now, world units: x and y the look point less the debug camera's slant, z its eye z
        /// (below 0, in front of the play plane), as the view last took it. The brick room's far wall is projected from here
        /// (<see cref="AreaBackdrop"/>).
        /// </summary>
        public Vector3 Eye
        {
            get
            {
                if (!_hasShown)
                {
                    TakeView();
                }

                var eye = LookPoint - (_shownTarget - _shownEye);
                return new Vector3(eye.x, eye.y, _shownEyeZ);
            }
        }

        /// <summary>0: Root.vb's <c>shakeScreen</c>, the view jumps or is put back, at once. Returns the new offset.</summary>
        public Vector2 ShakeScreen()
        {
            Shake.Shake();
            if (Shake.Armed)
            {
                // The restore is target = cam: the debug camera's target goes back under its eye too.
                View.LookStraight();
                TakeView();
            }

            Place();
            return _appliedShake;
        }

        /// <summary>Numpad plus or Enter (+1 / -1) held <paramref name="seconds"/>: the eye nearer or further.</summary>
        public void MoveForward(int direction, float seconds)
        {
            View.MoveForward(direction, seconds);
            Refresh();
        }

        /// <summary>Numpad 6 or 4 (+1 / -1) held <paramref name="seconds"/>: eye and target along x.</summary>
        public void Strafe(int direction, float seconds)
        {
            View.Strafe(direction, seconds);
            Refresh();
        }

        /// <summary>Numpad 8 or 2 (+1 / -1) held <paramref name="seconds"/>: eye and target along y.</summary>
        public void Lift(int direction, float seconds)
        {
            View.Lift(direction, seconds);
            Refresh();
        }

        /// <summary>X or Z (+1 / -1) held <paramref name="seconds"/>: the eye alone, on x and y.</summary>
        public void Isometric(int direction, float seconds)
        {
            View.Isometric(direction, seconds);
            Refresh();
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
                originalHalfHeight = tuning.OriginalHalfHeight;
                originalAspect = tuning.OriginalAspect;
                _tuned = true;
            }

            return this;
        }

        public Camera Camera => _camera != null ? _camera : _camera = GetComponent<Camera>();

        /// <summary>The world rect the camera currently shows (on the plane it looks at, ignoring a slant).</summary>
        public Rect ViewRect
        {
            get
            {
                var half = CameraFraming.HalfExtents(Camera.orthographicSize, Camera.aspect).ToUnity();
                return new Rect(LookPoint - half, half * 2f);
            }
        }

        /// <summary>Jumps straight to the framing with no smoothing (scene start, restart).</summary>
        public void Snap() => Frame(0f, snap: true);

        /// <summary>
        /// Root.vb's Reset for the camera: <c>cam</c> and <c>target</c> back at the origin (x 0, y 0, where
        /// every angel starts again, the eye at z -1), so a shake or a debug move is undone, then framed with no smoothing.
        /// </summary>
        public void Recenter()
        {
            Shake.Recenter();
            View.Recenter();
            TakeView();
            _appliedShake = Vector2.zero;
            _framedCenter = Vector2.zero;
            _framedSize = Camera.orthographicSize;
            _hasFramed = true;
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
            var current = _hasFramed ? _framedCenter : (Vector2)transform.position;
            Vector2 next;
            float size;

            if (mode == CameraFramingMode.Original)
            {
                // Camera.vb: cam (0, 0, -1) looking at target (0, 0, 0) through a 45 degree, 4:3 lens. The shake and the
                // debug camera still move it on top, as Root.vb and Input.vb move cam and target.
                ShowAspect(originalAspect);
                PlaceAt(Vector2.zero, originalHalfHeight > 0f ? originalHalfHeight : cam.orthographicSize);
                return;
            }

            ShowAspect(0f);
            if (mode == CameraFramingMode.FitLevel)
            {
                if (!hasLevel)
                {
                    PlaceAt(current, _hasFramed ? _framedSize : cam.orthographicSize);
                    return;
                }

                size = CameraFraming.FitOrthographicSize(levelRect.size.ToCore(), cam.aspect, padding);
                next = levelRect.center;
            }
            else
            {
                size = followSize;
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

            PlaceAt(next, size);
        }

        /// <summary>
        /// Shows the view in an <paramref name="aspect"/>-shaped viewport centred on the screen, with black bars beside or
        /// above and below it on any other screen shape (the original ran 640x480 or a 4:3 full screen); 0 shows the whole
        /// screen.
        /// </summary>
        private void ShowAspect(float aspect)
        {
            var cam = Camera;
            var full = new Rect(0f, 0f, 1f, 1f);
            var rect = full;
            if (aspect > 0f && Screen.height > 0)
            {
                rect = CameraFraming.Viewport(aspect, (float)Screen.width / Screen.height).ToUnity();
            }

            if (cam.rect != rect)
            {
                cam.rect = rect;
            }

            var barred = rect != full;
            if (barred && _bars == null)
            {
                var go = new GameObject("Bars");
                go.transform.SetParent(transform, false);
                _bars = go.AddComponent<Camera>();
                _bars.clearFlags = CameraClearFlags.SolidColor;
                _bars.backgroundColor = Color.black;
                _bars.cullingMask = 0;
                _bars.orthographic = true;
            }

            if (_bars != null)
            {
                _bars.depth = cam.depth - 1f;
                _bars.enabled = barred;
            }
        }

        /// <summary>The viewport the view is shown in (the whole screen, or the original's 4:3 between bars).</summary>
        public Rect Viewport => Camera.rect;

        /// <summary>Frames the view at <paramref name="center"/> and <paramref name="size"/>, then lays the shake and the debug camera on top.</summary>
        private void PlaceAt(Vector2 center, float size)
        {
            _framedCenter = center;
            _framedSize = size;
            _hasFramed = true;
            Place();
        }

        /// <summary>
        /// Puts the camera on the framed centre plus the shake and the debug camera as last taken: it looks at that point
        /// plus the target, from the eye's slant, with the framed size times the eye's zoom.
        /// </summary>
        private void Place()
        {
            if (!_hasFramed)
            {
                _framedCenter = transform.position;
                _framedSize = Camera.orthographicSize;
                _hasFramed = true;
            }

            if (!_hasShown)
            {
                TakeView();
            }

            if (_depth <= 0f)
            {
                _depth = transform.position.z < 0f ? -transform.position.z : 10f;
            }

            _appliedShake = _shake != null ? _shake.Offset.ToUnity() : Vector2.zero;
            _lookPoint = _framedCenter + _appliedShake + _shownTarget;

            var slant = _shownTarget - _shownEye;
            var look = new Vector3(slant.x, slant.y, -_shownEyeZ);
            var start = View.StartDistance;
            Camera.orthographicSize = _framedSize * (start > 0f ? look.magnitude / start : 1f);

            var point = new Vector3(_lookPoint.x, _lookPoint.y, 0f);
            if (slant == Vector2.zero)
            {
                transform.rotation = Quaternion.identity;
                transform.position = point - Vector3.forward * _depth;
            }
            else
            {
                // LookAtLH's up is +y, as Unity's: the same left-handed look from the eye to the target.
                var forward = look.normalized;
                transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
                transform.position = point - forward * _depth;
            }
        }

        /// <summary>The view takes the debug camera as it is now (Engine.vb's <c>initCamera</c>).</summary>
        private void TakeView()
        {
            var view = View;
            _shownEye = view.Eye.ToUnity();
            _shownTarget = view.Target.ToUnity();
            _shownEyeZ = view.EyeZ;
            _hasShown = true;
        }

        /// <summary>After a key moved the debug camera: the view takes it now unless the game is paused.</summary>
        private void Refresh()
        {
            if (!IsPaused)
            {
                TakeView();
            }

            Place();
        }

        private bool IsPaused => session != null && session.ShowsPaused; // the remote authority's pause online (U.CHN-T73)

        private int _unpausedMask;
        private bool _showingOnlyBackdrop;

        /// <summary>True while the camera shows only the backdrop and the room, as a paused frame does.</summary>
        public bool ShowsOnlyBackdrop => _showingOnlyBackdrop;

        /// <summary>
        /// Engine.vb's oneFrameRender draws only renderCube, renderBackground and the pause banner while paused: the
        /// angel, chain, mace, enemies, heart and zones are not drawn. So while the shown game is paused the camera
        /// culls everything but <see cref="AreaBackdrop.Layer"/>, and gets its own mask back on resume (U.CHN-T78).
        /// Called every LateUpdate; public so tests can drive it.
        /// </summary>
        public void ApplyPauseView()
        {
            var cam = Camera;
            if (cam == null)
            {
                return;
            }

            if (IsPaused)
            {
                if (!_showingOnlyBackdrop)
                {
                    _unpausedMask = cam.cullingMask;
                    _showingOnlyBackdrop = true;
                }

                cam.cullingMask = AreaBackdrop.OnlyBackdropMask;
            }
            else
            {
                RestoreMask();
            }
        }

        private void RestoreMask()
        {
            if (_showingOnlyBackdrop && Camera != null)
            {
                Camera.cullingMask = _unpausedMask;
            }

            _showingOnlyBackdrop = false;
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
                    _framedCenter = follow.position;
                    _framedSize = Camera.orthographicSize;
                    _hasFramed = true;
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

        private void OnEnable()
        {
            Subscribe();
            (_controls ?? (_controls = new ChainControls())).Debug.Enable();
        }

        private void OnDisable()
        {
            RestoreMask();
            Unsubscribe();
            _controls?.Debug.Disable();
        }

        private void OnDestroy()
        {
            _controls?.Dispose();
            _controls = null;
        }

        private void Update()
        {
            if (_controls == null)
            {
                return;
            }

            // Form1.vb's 0 is a normal key (held back while the scripter is open); the camera keys are Input.vb's
            // getKeyboard (held back while it is open or a script runs, U.CHN-T61).
            if (!GameSession.KeysSuspended(session) && _controls.ShakeScreen.WasPressedThisFrame())
            {
                ShakeScreen();
            }

            if (GameSession.PlaySuspended(session))
            {
                return;
            }

            // Input.vb polls the camera keys every frame, paused or not: real seconds held. LateUpdate shows the move.
            var dt = Time.unscaledDeltaTime;
            var forward = Held(_controls.CameraForward, _controls.CameraBackward);
            var strafe = Held(_controls.CameraRight, _controls.CameraLeft);
            var lift = Held(_controls.CameraUp, _controls.CameraDown);
            var iso = Held(_controls.Isometric, _controls.Deisometric);
            if (forward != 0)
            {
                View.MoveForward(forward, dt);
            }

            if (strafe != 0)
            {
                View.Strafe(strafe, dt);
            }

            if (lift != 0)
            {
                View.Lift(lift, dt);
            }

            if (iso != 0)
            {
                View.Isometric(iso, dt);
            }
        }

        /// <summary>+1 while only <paramref name="plus"/> is held, -1 while only <paramref name="minus"/> is, else 0 (the original's two steps cancel).</summary>
        private static int Held(InputAction plus, InputAction minus) => (plus.IsPressed() ? 1 : 0) - (minus.IsPressed() ? 1 : 0);

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

        // Scaled time: while paused (timeScale 0) the camera holds still with the game, and the debug view waits too.
        private void LateUpdate()
        {
            if (!IsPaused)
            {
                TakeView();
            }

            Frame(Time.deltaTime);
            ApplyPauseView();
        }

        private void OnDrawGizmosSelected()
        {
            if (Mode != CameraFramingMode.Follow)
            {
                return;
            }

            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(LookPoint, new Vector3(deadZone.x * 2f, deadZone.y * 2f, 0f));
        }
    }
}
