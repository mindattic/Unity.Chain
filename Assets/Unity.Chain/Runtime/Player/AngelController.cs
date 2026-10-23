using UnityEngine;
using UnityEngine.InputSystem;

namespace MindAttic.Chain
{
    /// <summary>
    /// Mouse-steered angel: reads the mouse delta from the Input System, steps
    /// <see cref="AngelFlight"/>, and applies position, facing and the wing-flap frame to this
    /// GameObject's Transform and SpriteRenderer. Play-space bounds come from the camera's
    /// orthographic view (inset by <see cref="boundsMargin"/>) unless explicit bounds are set.
    /// The chain anchors its link 0 to <see cref="HandSocket"/>, published to
    /// <see cref="ChainController"/> through the optional hand-socket transform.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AngelController : MonoBehaviour
    {
        [Header("Flight")]
        [Tooltip("World units moved per pixel of mouse motion.")]
        [SerializeField] private float sensitivity = 0.01f;
        [Tooltip("Lock and hide the OS cursor while playing, so the mouse steers the angel only.")]
        [SerializeField] private bool lockCursor = true;

        [Header("Bounds")]
        [Tooltip("Derive bounds from this camera's orthographic view. Falls back to Camera.main.")]
        [SerializeField] private Camera boundsCamera;
        [Tooltip("Inset from the camera edges, world units, so the sprite stays fully on screen.")]
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
        [Tooltip("Optional transform moved to the hand socket every frame; assign it as the " +
                 "ChainController's Hand Socket so link 0 follows the angel's hand.")]
        [SerializeField] private Transform handSocketTransform;

        private AngelFlight _flight;

        public AngelFlight Flight => _flight;
        public Facing Facing => _flight != null ? _flight.Facing : Facing.Right;
        public Vector2 HandSocket => _flight != null ? _flight.HandSocket : (Vector2)transform.position;

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            _flight = new AngelFlight(transform.position, BuildSettings());
            Apply();
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
            // Re-read settings each frame so inspector tweaks and camera resizes apply live.
            _flight.Config = BuildSettings();

            var mouse = Mouse.current;
            var delta = mouse != null ? mouse.delta.ReadValue() : Vector2.zero;
            _flight.Step(delta, Time.deltaTime);
            Apply();
        }

        private AngelFlight.Settings BuildSettings()
        {
            var frameCount = Mathf.Max(rightFrames.Length, leftFrames.Length);
            return new AngelFlight.Settings(
                sensitivity,
                ComputeBounds(),
                hoverAmplitude,
                hoverPeriod,
                frameDuration,
                Mathf.Max(1, frameCount),
                handOffset);
        }

        private Rect ComputeBounds()
        {
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

        private void Apply()
        {
            var p = _flight.RenderPosition;
            transform.position = new Vector3(p.x, p.y, transform.position.z);

            if (handSocketTransform != null)
            {
                var hand = _flight.HandSocket;
                handSocketTransform.position = new Vector3(hand.x, hand.y, handSocketTransform.position.z);
            }

            if (spriteRenderer == null)
            {
                return;
            }

            var facingLeft = _flight.Facing == Facing.Left;
            var useLeftSet = facingLeft && leftFrames.Length > 0;
            var frames = useLeftSet ? leftFrames : rightFrames;
            spriteRenderer.flipX = facingLeft && !useLeftSet;

            if (frames.Length > 0)
            {
                spriteRenderer.sprite = frames[_flight.Frame % frames.Length];
            }
        }

        private void OnDrawGizmosSelected()
        {
            var b = Application.isPlaying && _flight != null ? _flight.Config.Bounds : ComputeBounds();
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(b.center, b.size);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(HandSocket, 0.05f);
        }
    }
}
