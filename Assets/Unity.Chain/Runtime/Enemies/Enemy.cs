using MindAttic.Chain.Core;
using UnityEngine;

namespace MindAttic.Chain
{
    /// <summary>
    /// How one enemy (Enemy.vb) looks: a sprite that stands wherever the world's
    /// <see cref="EnemyEntity"/> is. The enemy's rules (seek, knockback, grab, throw, flocking, HP)
    /// all run in the <see cref="GameWorld"/>; this view only reads them. <see cref="EnemySpawner"/>
    /// makes one for every <see cref="EnemySpawned"/> event and plays <see cref="PlayDeath"/> on
    /// <see cref="EnemyKilled"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Enemy : MonoBehaviour
    {
        [Header("Death")]
        [Tooltip("Seconds after death before the GameObject is destroyed (play mode).")]
        [Min(0f)]
        [SerializeField] private float destroyDelay = 0f;

        /// <summary>The world enemy this view shows. Kept after it dies, so its last state can still be read.</summary>
        public EnemyEntity Entity { get; private set; }

        public EnemyId Id => Entity != null ? Entity.Id : default;
        public EnemyState State => Entity != null ? Entity.State : EnemyState.Dead;
        public float Hp => Entity != null ? Entity.Hp : 0f;
        public bool IsAlive => Entity != null && Entity.IsAlive;
        public Vector2 Velocity => Entity != null ? Entity.Velocity.ToUnity() : Vector2.zero;
        public Vector2 Position => Entity != null ? Entity.Position.ToUnity() : (Vector2)transform.position;

        /// <summary>Shows <paramref name="entity"/>.</summary>
        public void Bind(EnemyEntity entity)
        {
            Entity = entity;
            Follow();
        }

        /// <summary>Stands the sprite where the world has the enemy. Called every frame; public so tests can drive it.</summary>
        public void Follow()
        {
            if (Entity == null || !Entity.InWorld)
            {
                return;
            }

            var p = Entity.Position;
            transform.position = new Vector3(p.X, p.Y, transform.position.z);
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
