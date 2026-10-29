using System.Collections.Generic;
using MindAttic.Chain.Core;
using UnityEngine;

namespace MindAttic.Chain
{
    /// <summary>
    /// Makes and removes the enemies' views as the world reports them. The world spawns the flock
    /// (Enemy.vb <c>loadEnemy</c>: 14 enemies at random points with a random mass of 1-2, tuned on the
    /// <see cref="GameSession"/>) on every reset; this spawner plays the events: an
    /// <see cref="EnemySpawned"/> gets a new <see cref="Enemy"/>, which shows the animation frame the
    /// world already rolled for it (<see cref="EnemyEntity.Frame"/>, from <c>EnemyBrain</c>'s own port
    /// of <c>loadEnemy</c>'s random start and <c>animateEnemy</c>'s 0.1 s loop), an
    /// <see cref="EnemyKilled"/> plays its death, and an <see cref="EnemyRemoved"/> (a reset clearing
    /// the old flock) destroys it. Online as a client (<see cref="GameSession.Shown"/>, U.CHN-T73) it shows the remote
    /// authority's flock instead: every frame each enemy in the newest snapshot has a view, one gone from it is removed,
    /// and this screen's own frozen flock is hidden until the screen plays locally again.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemySpawner : MonoBehaviour
    {
        [SerializeField] private GameSession session;
        [Tooltip("Enemy.vb's 7-frame loop (Shadow/enemy1-7). Optional: enemies with none keep a plain disc.")]
        [SerializeField] private Sprite[] frames = new Sprite[0];

        private readonly Dictionary<EnemyId, Enemy> _views = new Dictionary<EnemyId, Enemy>();
        private readonly Dictionary<EnemyId, Enemy> _shownViews = new Dictionary<EnemyId, Enemy>();
        private readonly HashSet<EnemyId> _inSnapshot = new HashSet<EnemyId>();
        private readonly List<EnemyId> _gone = new List<EnemyId>();
        private bool _showing;
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

        /// <summary>Whether the views show a remote authority's flock (online as a client) rather than this world's.</summary>
        public bool IsShowingAuthority => _showing;

        /// <summary>Enemy views showing a live enemy (the remote authority's while it is shown).</summary>
        public int Alive
        {
            get
            {
                var alive = 0;
                foreach (var view in (_showing ? _shownViews : _views).Values)
                {
                    if (view != null && view.IsAlive)
                    {
                        alive++;
                    }
                }

                return alive;
            }
        }

        /// <summary>The view showing <paramref name="id"/> (the remote authority's enemy while it is shown), or null.</summary>
        public Enemy ViewOf(EnemyId id) => (_showing ? _shownViews : _views).TryGetValue(id, out var view) ? view : null;

        /// <summary>
        /// Shows the session's remote authority's flock (<see cref="GameSession.Shown"/>) when there is one, and this world's
        /// own again once there is not. Called every frame; public so tests can drive it.
        /// </summary>
        public void ShowAuthority()
        {
            var shown = session != null ? session.Shown : null;
            if (shown == null)
            {
                if (_showing)
                {
                    StopShowing();
                }

                return;
            }

            if (!_showing)
            {
                _showing = true;
                SetLocalViewsShown(false);
            }

            _inSnapshot.Clear();
            foreach (var enemy in shown.Enemies)
            {
                _inSnapshot.Add(enemy.Id);
                if (!_shownViews.TryGetValue(enemy.Id, out var view) || view == null)
                {
                    view = NewView("Enemy " + enemy.Id.Value + " (shown)");
                    _shownViews[enemy.Id] = view;
                }

                view.Show(enemy);
            }

            _gone.Clear();
            foreach (var id in _shownViews.Keys)
            {
                if (!_inSnapshot.Contains(id))
                {
                    _gone.Add(id);
                }
            }

            foreach (var id in _gone)
            {
                DestroyView(_shownViews[id]);
                _shownViews.Remove(id);
            }
        }

        private void StopShowing()
        {
            foreach (var view in _shownViews.Values)
            {
                DestroyView(view);
            }

            _shownViews.Clear();
            _showing = false;
            SetLocalViewsShown(true);
        }

        private void SetLocalViewsShown(bool shown)
        {
            foreach (var view in _views.Values)
            {
                if (view != null)
                {
                    view.gameObject.SetActive(shown);
                }
            }
        }

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

            var view = NewView($"Enemy {entity.Id.Value}");
            view.Bind(entity);
            if (_showing)
            {
                // This screen's own world is not what is drawn while the remote authority's is shown.
                view.gameObject.SetActive(false);
            }

            _views[id] = view;
        }

        private Enemy NewView(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            var view = go.AddComponent<Enemy>();
            view.SpriteRenderer = renderer;

            if (frames != null && frames.Length > 0)
            {
                // Enemy.vb's 7-frame loop (UNI-T19): each enemy starts on the world's own random
                // frame (Enemy.Frame, from EnemyBrain's loadEnemy port) and advances from there.
                view.Frames = frames;
            }
            else
            {
                // No enemy art is imported yet: a plain red disc keeps enemies visible.
                renderer.sprite = FallbackSprite();
                renderer.color = new Color(0.85f, 0.2f, 0.2f);
            }

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

        private void LateUpdate() => ShowAuthority();
    }
}
