using System.Collections.Generic;
using UnityEngine;

namespace MindAttic.Chain
{
    /// <summary>
    /// Fills the arena with enemies (Enemy.vb <c>loadEnemy</c>: 14 enemies, <c>maxEnemy</c> 13, each at a
    /// random position with a random frame and a random mass of 1-2, which <see cref="Enemy"/> rolls
    /// itself). It spawns on start and, when a <see cref="GameSession"/> restarts in place
    /// (Engine.vb <c>Reset</c>), clears what is left and spawns a fresh set. A scene reload recreates
    /// the spawner, so that path spawns on start again.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemySpawner : MonoBehaviour
    {
        [Min(0)]
        [SerializeField] private int count = 14;
        [Tooltip("Where enemies appear. Falls back to the scene's LevelBounds.")]
        [SerializeField] private LevelBounds level;
        [SerializeField] private GameSession session;
        [Tooltip("Frames an enemy picks from at random. Optional: enemies with none keep their sprite.")]
        [SerializeField] private Sprite[] frames = new Sprite[0];
        [Tooltip("Collider radius of a spawned enemy, world units.")]
        [Min(0.01f)]
        [SerializeField] private float colliderRadius = 0.3f;

        private readonly List<Enemy> _spawned = new List<Enemy>();

        public int Count
        {
            get => count;
            set => count = Mathf.Max(0, value);
        }

        public LevelBounds Level
        {
            get => level;
            set => level = value;
        }

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

        public Sprite[] Frames
        {
            get => frames;
            set => frames = value ?? new Sprite[0];
        }

        /// <summary>Enemies spawned by the last <see cref="Spawn"/> that still exist.</summary>
        public int Alive
        {
            get
            {
                _spawned.RemoveAll(e => e == null);
                return _spawned.Count;
            }
        }

        /// <summary>
        /// Random points inside <paramref name="area"/>, <paramref name="count"/> of them (pure; tests drive it).
        /// </summary>
        public static Vector2[] RandomPositions(Rect area, int count, System.Random rng)
        {
            var points = new Vector2[Mathf.Max(0, count)];
            for (var i = 0; i < points.Length; i++)
            {
                points[i] = new Vector2(
                    area.xMin + (float)rng.NextDouble() * area.width,
                    area.yMin + (float)rng.NextDouble() * area.height);
            }

            return points;
        }

        /// <summary>Spawns <see cref="Count"/> enemies, removing any from an earlier spawn first.</summary>
        public void Spawn()
        {
            Clear();
            var bounds = level != null ? level : (level = LevelBounds.Find());
            var area = bounds != null ? bounds.Bounds : new Rect(-8f, -4.5f, 16f, 9f);
            var rng = new System.Random();

            foreach (var point in RandomPositions(area, count, rng))
            {
                _spawned.Add(CreateEnemy(point, rng));
            }
        }

        /// <summary>Destroys the enemies this spawner made.</summary>
        public void Clear()
        {
            foreach (var enemy in _spawned)
            {
                if (enemy != null)
                {
                    enemy.gameObject.SetActive(false);
                    if (Application.isPlaying)
                    {
                        Destroy(enemy.gameObject);
                    }
                    else
                    {
                        DestroyImmediate(enemy.gameObject);
                    }
                }
            }

            _spawned.Clear();
        }

        private Enemy CreateEnemy(Vector2 position, System.Random rng)
        {
            var go = new GameObject("Enemy");
            go.SetActive(false); // configure before Enemy.Awake runs
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(position.x, position.y, 0f);

            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            go.AddComponent<CircleCollider2D>().radius = colliderRadius;

            var renderer = go.AddComponent<SpriteRenderer>();
            if (frames != null && frames.Length > 0)
            {
                renderer.sprite = frames[rng.Next(frames.Length)];
            }
            else
            {
                // No enemy art is imported yet: a plain red disc keeps enemies visible.
                renderer.sprite = FallbackSprite();
                renderer.color = new Color(0.85f, 0.2f, 0.2f);
            }

            var enemy = go.AddComponent<Enemy>();
            go.SetActive(true);
            return enemy;
        }

        private static Sprite _fallback;

        private static Sprite FallbackSprite()
        {
            if (_fallback != null)
            {
                return _fallback;
            }

            const int size = 32;
            var texture = new Texture2D(size, size) { hideFlags = HideFlags.HideAndDontSave };
            var c = (size - 1) * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var inside = (x - c) * (x - c) + (y - c) * (y - c) <= c * c;
                    texture.SetPixel(x, y, inside ? Color.white : Color.clear);
                }
            }

            texture.Apply();
            _fallback = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size / 0.6f);
            _fallback.hideFlags = HideFlags.HideAndDontSave;
            return _fallback;
        }

        private void Subscribe()
        {
            if (session != null)
            {
                session.Restarted -= Spawn;
                session.Restarted += Spawn;
            }
        }

        private void Unsubscribe()
        {
            if (session != null)
            {
                session.Restarted -= Spawn;
            }
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

        private void Start() => Spawn();
    }
}
