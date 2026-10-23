using System.Collections.Generic;
using MindAttic.Chain.Core;
using UnityEngine;

namespace MindAttic.Chain
{
    /// <summary>
    /// Makes and removes the enemies' views as the world reports them. The world spawns the flock
    /// (Enemy.vb <c>loadEnemy</c>: 14 enemies at random points with a random mass of 1-2, tuned on the
    /// <see cref="GameSession"/>) on every reset; this spawner plays the events: an
    /// <see cref="EnemySpawned"/> gets a new <see cref="Enemy"/> with a random frame, an
    /// <see cref="EnemyKilled"/> plays its death, and an <see cref="EnemyRemoved"/> (a reset clearing
    /// the old flock) destroys it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemySpawner : MonoBehaviour
    {
        [SerializeField] private GameSession session;
        [Tooltip("Frames an enemy picks from at random. Optional: enemies with none keep a plain disc.")]
        [SerializeField] private Sprite[] frames = new Sprite[0];

        private readonly Dictionary<EnemyId, Enemy> _views = new Dictionary<EnemyId, Enemy>();
        private readonly System.Random _random = new System.Random();
        private GameSession _subscribed;

        public GameSession Session
        {
            get => session;
            set
            {
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

        /// <summary>Enemy views showing a live enemy.</summary>
        public int Alive
        {
            get
            {
                var alive = 0;
                foreach (var view in _views.Values)
                {
                    if (view != null && view.IsAlive)
                    {
                        alive++;
                    }
                }

                return alive;
            }
        }

        /// <summary>The view showing <paramref name="id"/>, or null.</summary>
        public Enemy ViewOf(EnemyId id) => _views.TryGetValue(id, out var view) ? view : null;

        /// <summary>Plays one world event. Hooked to <see cref="GameSession.EventRaised"/>; public so tests can drive it.</summary>
        public void Play(GameEvent gameEvent)
        {
            switch (gameEvent)
            {
                case EnemySpawned spawned:
                    Show(spawned.Enemy);
                    break;

                case EnemyKilled killed:
                    if (_views.TryGetValue(killed.Enemy, out var dying))
                    {
                        _views.Remove(killed.Enemy);
                        if (dying != null)
                        {
                            dying.PlayDeath();
                        }
                    }

                    break;

                case EnemyRemoved removed:
                    if (_views.TryGetValue(removed.Enemy, out var gone))
                    {
                        _views.Remove(removed.Enemy);
                        DestroyView(gone);
                    }

                    break;
            }
        }

        private void Show(EnemyId id)
        {
            var entity = session != null ? session.World.FindEnemy(id) : null;
            if (entity == null || _views.ContainsKey(id))
            {
                return;
            }

            _views[id] = CreateView(entity);
        }

        private Enemy CreateView(EnemyEntity entity)
        {
            var go = new GameObject($"Enemy {entity.Id.Value}");
            go.transform.SetParent(transform, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            if (frames != null && frames.Length > 0)
            {
                renderer.sprite = frames[_random.Next(frames.Length)];
            }
            else
            {
                // No enemy art is imported yet: a plain red disc keeps enemies visible.
                renderer.sprite = FallbackSprite();
                renderer.color = new Color(0.85f, 0.2f, 0.2f);
            }

            var view = go.AddComponent<Enemy>();
            view.Bind(entity);
            return view;
        }

        private static void DestroyView(Enemy view)
        {
            if (view == null)
            {
                return;
            }

            view.gameObject.SetActive(false);
            if (Application.isPlaying)
            {
                Destroy(view.gameObject);
            }
            else
            {
                DestroyImmediate(view.gameObject);
            }
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

        /// <summary>Listens to the session, and shows any enemies already in its world.</summary>
        private void Subscribe()
        {
            if (_subscribed == session)
            {
                return;
            }

            Unsubscribe();
            if (session == null)
            {
                return;
            }

            session.EventRaised += Play;
            _subscribed = session;
            foreach (var enemy in session.World.Enemies)
            {
                Show(enemy.Id);
            }
        }

        private void Unsubscribe()
        {
            if (_subscribed != null)
            {
                _subscribed.EventRaised -= Play;
                _subscribed = null;
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
    }
}
