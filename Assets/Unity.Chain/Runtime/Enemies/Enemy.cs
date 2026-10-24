using MindAttic.Chain.Core;
using UnityEngine;

namespace MindAttic.Chain
{
    /// <summary>
    /// How one enemy (Enemy.vb) looks: a sprite that stands wherever the world's
    /// <see cref="EnemyEntity"/> is. The enemy's rules (seek, knockback, grab, throw, flocking, HP)
    /// all run in the <see cref="GameWorld"/>; this view only reads them. <see cref="EnemySpawner"/>
    /// makes one for every <see cref="EnemySpawned"/> event and plays <see cref="PlayDeath"/> on
    /// <see cref="EnemyKilled"/>. Online as a client it shows one of the remote authority's enemies instead
    /// (<see cref="Show"/>, U.CHN-T73), as the newest snapshot has it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Enemy : MonoBehaviour
    {
        [Header("Death")]
        [Tooltip("Seconds after death before the GameObject is destroyed (play mode).")]
        [Min(0f)]
        [SerializeField] private float destroyDelay = 0f;

        [Header("Animation")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [Tooltip("Enemy.vb's 7-frame loop (Shadow/enemy1-7). Left empty, the sprite never changes.")]
        [SerializeField] private Sprite[] frames = new Sprite[0];

        /// <summary>The world enemy this view shows. Kept after it dies, so its last state can still be read.</summary>
        public EnemyEntity Entity { get; private set; }

        /// <summary>Whether this view shows a remote authority's enemy (<see cref="Show"/>) rather than a world's.</summary>
        public bool IsShown { get; private set; }

        private WorldSnapshot.Enemy _shown;

        public EnemyId Id => Entity != null ? Entity.Id : IsShown ? _shown.Id : default;
        public EnemyState State => Entity != null ? Entity.State : IsShown ? _shown.State : EnemyState.Dead;
        public float Hp => Entity != null ? Entity.Hp : IsShown ? _shown.Hp : 0f;
        public bool IsAlive => Entity != null ? Entity.IsAlive : IsShown && _shown.State != EnemyState.Dead;
        public Vector2 Velocity => Entity != null ? Entity.Velocity.ToUnity() : Vector2.zero;
        public Vector2 Position => Entity != null ? Entity.Position.ToUnity() : IsShown ? _shown.Position.ToUnity() : (Vector2)transform.position;

        public SpriteRenderer SpriteRenderer
        {
            get => spriteRenderer;
            set => spriteRenderer = value;
        }

        public Sprite[] Frames
        {
            get => frames;
            set => frames = value ?? new Sprite[0];
        }

        /// <summary>Shows <paramref name="entity"/>.</summary>
        public void Bind(EnemyEntity entity)
        {
            Entity = entity;
            Follow();
        }

        /// <summary>Shows <paramref name="enemy"/>, a remote authority's enemy as its newest snapshot has it (U.CHN-T73).</summary>
        public void Show(WorldSnapshot.Enemy enemy)
        {
            Entity = null;
            IsShown = true;
            _shown = enemy;
            Follow();
        }

        /// <summary>Stands the sprite where the world has the enemy and shows its current animation frame. Called every frame; public so tests can drive it.</summary>
        public void Follow()
        {
            if (Entity != null)
            {
                if (Entity.InWorld)
                {
                    Draw(Entity.Position, Entity.Frame, Entity.Radius);
                }
            }
            else if (IsShown)
            {
                Draw(_shown.Position, _shown.Frame, _shown.Radius);
            }
        }

        private void Draw(Vec2 p, int frame, float radius)
        {
            transform.position = new Vector3(p.X, p.Y, transform.position.z);

            if (spriteRenderer == null)
            {
                return;
            }

            if (frames.Length > 0)
            {
                spriteRenderer.sprite = frames[Mathf.Abs(frame) % frames.Length];
            }

            // The sprite's import is a 1-unit-wide baseline (ArtImportSettings, UNI-T19); scale it to
            // this enemy's own physics circle (EnemyEntity.Radius x2) the same way HeartPickup scales
            // heart.png to itemSize, so the art matches what it collides, is hit and is grabbed as.
            var sprite = spriteRenderer.sprite;
            if (sprite != null)
            {
                var nativeWidth = sprite.bounds.size.x;
                var diameter = radius * 2f;
                if (nativeWidth > 0f && diameter > 0f)
                {
                    transform.localScale = Vector3.one * (diameter / nativeWidth);
                }
            }
        }

        /// <summary>The enemy died: hide it now and remove it (after <c>destroyDelay</c> in play mode).</summary>
        public void PlayDeath()
        {
            if (Application.isPlaying)
            {
                Destroy(gameObject, destroyDelay);
                if (destroyDelay > 0f)
                {
                    return;
                }
            }

            gameObject.SetActive(false);
        }

        private void LateUpdate() => Follow();
    }
}
