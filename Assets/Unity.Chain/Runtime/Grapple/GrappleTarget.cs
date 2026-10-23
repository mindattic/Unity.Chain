using UnityEngine;

namespace MindAttic.Chain
{
    /// <summary>
    /// Default <see cref="IGrappleTarget"/> for anything with a <see cref="Rigidbody2D"/>: while
    /// held the body goes kinematic and is moved to the mace each physics step; on throw its
    /// original body type comes back and the launch velocity is applied. A short cooldown after a
    /// throw stops the still-armed mace from snatching it straight back.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class GrappleTarget : MonoBehaviour, IGrappleTarget
    {
        [Tooltip("Seconds after a throw before this target can be grabbed again.")]
        [Min(0f)]
        [SerializeField] private float regrabCooldown = 0.5f;

        private Rigidbody2D _body;
        private RigidbodyType2D _bodyTypeBeforeGrab;
        private float _thrownAt = float.NegativeInfinity;

        public bool IsHeld { get; private set; }
        public Vector2 LastThrowVelocity { get; private set; }

        public bool CanBeGrappled =>
            isActiveAndEnabled && !IsHeld && Time.time - _thrownAt >= regrabCooldown;

        private Rigidbody2D Body => _body != null ? _body : _body = GetComponent<Rigidbody2D>();

        public void OnGrappled()
        {
            IsHeld = true;
            _bodyTypeBeforeGrab = Body.bodyType;
            Body.bodyType = RigidbodyType2D.Kinematic;
            Body.linearVelocity = Vector2.zero;
            Body.angularVelocity = 0f;
        }

        public void HoldAt(Vector2 position)
        {
            if (Application.isPlaying)
            {
                Body.MovePosition(position);
            }
            else
            {
                Body.position = position;
                transform.position = new Vector3(position.x, position.y, transform.position.z);
            }
        }

        public void Throw(Vector2 velocity)
        {
            IsHeld = false;
            _thrownAt = Time.time;
            LastThrowVelocity = velocity;
            Body.bodyType = _bodyTypeBeforeGrab;
            Body.linearVelocity = velocity;
        }
    }
}
