using UnityEngine;

namespace MindAttic.Chain
{
    /// <summary>
    /// The level's play space: a world-space rect centered on this transform. It is the one
    /// source of truth the rest of the game frames itself by: <see cref="AngelController"/>
    /// clamps the angel's flight to it (inset by the sprite margin, Angel.vb's checkBounds),
    /// <see cref="ChainController"/> can bounce chain links off its walls (Elastic Trail's wall
    /// bounce), and <see cref="CameraRig"/> keeps the camera's view inside it. Put one in the
    /// scene; components that need it find it automatically when not assigned.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelBounds : MonoBehaviour
    {
        [Tooltip("Width and height of the play space, world units.")]
        [SerializeField] private Vector2 size = new Vector2(16f, 9f);
        [Tooltip("Offset of the play space's center from this transform.")]
        [SerializeField] private Vector2 offset = Vector2.zero;

        public Vector2 Size
        {
            get => size;
            set => size = new Vector2(Mathf.Max(0f, value.x), Mathf.Max(0f, value.y));
        }

        public Vector2 Offset
        {
            get => offset;
            set => offset = value;
        }

        /// <summary>The play space in world units.</summary>
        public Rect Bounds
        {
            get
            {
                var center = (Vector2)transform.position + offset;
                return new Rect(center - size * 0.5f, size);
            }
        }

        /// <summary>The play space shrunk by <paramref name="margin"/> on every side.</summary>
        public Rect Inset(Vector2 margin) => PlayArea.Inset(Bounds, margin);

        public bool Contains(Vector2 point) => Bounds.Contains(point);

        public Vector2 Clamp(Vector2 point) => PlayArea.Clamp(Bounds, point);

        /// <summary>
        /// The scene's level bounds, or null. Used as the fallback by components whose own
        /// reference is unassigned.
        /// </summary>
        public static LevelBounds Find() => FindAnyObjectByType<LevelBounds>();

        private void OnValidate() => Size = size;

        private void OnDrawGizmos()
        {
            var b = Bounds;
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.9f);
            Gizmos.DrawWireCube(b.center, b.size);
        }
    }
}
