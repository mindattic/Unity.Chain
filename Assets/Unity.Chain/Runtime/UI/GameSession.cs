using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace MindAttic.Chain
{
    /// <summary>
    /// Runs the <see cref="GameState"/> the HUD shows. Each physics step it checks for enemies
    /// touching the angel (a Physics2D circle at the angel, replacing Engine.vb's
    /// <c>checkAngelHit</c> box test) and costs HP through the hurt delay; it scores every
    /// <see cref="MaceHitDetector.Hit"/>; P or Escape pauses (time scale 0); and when HP runs out
    /// the ambient light fades down 5 per frame
    /// (<see cref="GameState.AdvanceGameOver"/>) and then the game restarts by itself with no click, as
    /// Engine.vb's <c>GameOver</c> does (the scene reloads when it is in the build, otherwise the
    /// session resets in place). Grabbed and dead enemies do not hurt the angel.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(120)] // after MaceHitDetector (110), so this step's hits are scored first
    public sealed class GameSession : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The angel enemies hurt. Falls back to the scene's AngelController.")]
        [SerializeField] private AngelController angel;
        [Tooltip("Mace hits are scored from this detector. Falls back to the scene's MaceHitDetector.")]
        [SerializeField] private MaceHitDetector maceHits;

        [Header("Health")]
        [Min(1)]
        [SerializeField] private int maxHp = 30;
        [Tooltip("Seconds after a hurt before the angel can be hurt again (Angel.vb: 0.25).")]
        [Min(0f)]
        [SerializeField] private float hurtDelay = 0.25f;
        [Min(0)]
        [SerializeField] private int contactDamage = 1;
        [Tooltip("Radius around the angel that counts as an enemy touching it, world units.")]
        [Min(0f)]
        [SerializeField] private float contactRadius = 0.35f;
        [SerializeField] private LayerMask enemyLayers = ~0;

        [Header("Score")]
        [Tooltip("Score per point of mace damage (Engine.vb: 1000).")]
        [Min(0f)]
        [SerializeField] private float pointsPerDamage = 1000f;

        private readonly List<Collider2D> _overlaps = new List<Collider2D>();
        private GameState _state;
        private MaceHitDetector _subscribed;
        private bool _ownsTimeScale;
        private bool _restarting;

        /// <summary>Raised after an in-place restart (when the scene is not reloaded).</summary>
        public event Action Restarted;

        public GameState State => _state ?? (_state = new GameState(BuildSettings()));

        public AngelController Angel
        {
            get => angel;
            set => angel = value;
        }

        public MaceHitDetector MaceHits
        {
            get => maceHits;
            set
            {
                maceHits = value;
                if (isActiveAndEnabled)
                {
                    Subscribe();
                }
            }
        }

        public float ContactRadius
        {
            get => contactRadius;
            set => contactRadius = Mathf.Max(0f, value);
        }

        /// <summary>
        /// One frame of the game-over fade; when the light has faded out, restarts. Called from
        /// <c>FixedUpdate</c> (so the fade runs at the fixed step, not the display rate); public so tests can drive it. Returns true if it restarted.
        /// </summary>
        public bool StepGameOver()
        {
            if (!State.IsGameOver)
            {
                return false;
            }

            ReleaseTimeScale();
            if (_restarting || !State.AdvanceGameOver())
            {
                return false;
            }

            Restart();
            return true;
        }

        /// <summary>
        /// One contact check at time <paramref name="now"/>: if a live, ungrabbed enemy overlaps
        /// the angel, the angel is hurt (subject to the hurt delay). Called from
        /// <c>FixedUpdate</c>; public so tests can drive it. Returns true if HP was lost.
        /// </summary>
        public bool CheckContact(float now)
        {
            if (!State.IsPlaying || angel == null || contactRadius <= 0f)
            {
                return false;
            }

            return TouchingEnemy(angel.transform.position) && State.TryHurt(now);
        }

        /// <summary>
        /// Heals for every heart the angel touches (Item.vb's checkItemCollect: +10 HP capped at
        /// max HP) and sends each collected heart back to the top. Called from FixedUpdate;
        /// public so tests can drive it. Returns the HP gained.
        /// </summary>
        public int CheckPickups()
        {
            if (!State.IsPlaying || angel == null)
            {
                return 0;
            }

            var gained = 0;
            var position = (Vector2)angel.transform.position;
            foreach (var heart in FindObjectsByType<HeartPickup>(FindObjectsSortMode.None))
            {
                if (heart.isActiveAndEnabled && heart.IsTouching(position))
                {
                    gained += State.Heal(heart.HealAmount);
                    heart.Respawn();
                }
            }

            return gained;
        }

        /// <summary>Scores a mace hit. Hooked to <see cref="MaceHitDetector.Hit"/>.</summary>
        public void RegisterHit(Enemy enemy, float damage) => State.AddHitScore(damage);

        public void SetPaused(bool paused)
        {
            State.SetPaused(paused);
            ApplyTimeScale();
        }

        public void TogglePause() => SetPaused(!State.IsPaused);

        /// <summary>Starts over: reloads the scene when it is in the build, else resets in place.</summary>
        public void Restart()
        {
            ReleaseTimeScale();
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying && scene.buildIndex >= 0)
            {
                _restarting = true;
                SceneManager.LoadScene(scene.buildIndex);
                return;
            }

            State.Reset();
            Restarted?.Invoke();
        }

        private bool TouchingEnemy(Vector2 center)
        {
            var filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(enemyLayers);
            _overlaps.Clear();
            Physics2D.OverlapCircle(center, contactRadius, filter, _overlaps);

            foreach (var hit in _overlaps)
            {
                var enemy = hit.GetComponentInParent<Enemy>();
                if (enemy != null && enemy.isActiveAndEnabled && enemy.IsAlive && enemy.State != EnemyState.Grabbed)
                {
                    return true;
                }
            }

            return false;
        }

        private GameState.Settings BuildSettings() => new GameState.Settings
        {
            MaxHp = maxHp,
            HurtDelay = hurtDelay,
            ContactDamage = contactDamage,
            PointsPerDamage = pointsPerDamage,
        };

        private void ApplyTimeScale()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (!State.IsPaused)
            {
                ReleaseTimeScale();
            }
            else
            {
                Time.timeScale = 0f;
                _ownsTimeScale = true;
            }
        }

        private void ReleaseTimeScale()
        {
            if (_ownsTimeScale)
            {
                Time.timeScale = 1f;
                _ownsTimeScale = false;
            }
        }

        private void Subscribe()
        {
            if (_subscribed == maceHits)
            {
                return;
            }

            Unsubscribe();
            if (maceHits != null)
            {
                maceHits.Hit += RegisterHit;
                _subscribed = maceHits;
            }
        }

        private void Unsubscribe()
        {
            if (_subscribed != null)
            {
                _subscribed.Hit -= RegisterHit;
                _subscribed = null;
            }
        }

        private void Awake()
        {
            if (angel == null)
            {
                angel = FindAnyObjectByType<AngelController>();
            }

            if (maceHits == null)
            {
                maceHits = FindAnyObjectByType<MaceHitDetector>();
            }
        }

        private void OnEnable() => Subscribe();

        private void OnDisable()
        {
            Unsubscribe();
            ReleaseTimeScale();
        }

        private void FixedUpdate()
        {
            CheckContact(Time.time);
            CheckPickups();
            StepGameOver();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.pKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame))
            {
                TogglePause();
            }
        }

        private void OnValidate()
        {
            if (_state != null)
            {
                _state.Config = BuildSettings();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (angel == null)
            {
                return;
            }

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(angel.transform.position, contactRadius);
        }
    }
}
