using System;
using System.Collections.Generic;
using MindAttic.Chain.Core;
using UnityEngine;

namespace MindAttic.Chain
{
    /// <summary>
    /// The one authority: it holds the <see cref="GameWorld"/>, is the only thing that steps it (once
    /// per physics step), and is where every player's <see cref="GameCommand"/>s go
    /// (<see cref="Submit"/>). After each command and step it hands the world's
    /// <see cref="GameEvent"/>s, in order, to whoever listens (<see cref="EventRaised"/>): the enemy
    /// views, the audio, the HUD. Presentation reads the world and plays events; nothing else changes it.
    ///
    /// Players are seated with <see cref="Join"/>: each seat is a <see cref="PlayerId"/> with its own
    /// angel and chain components, which hold that id and send commands with it. There is no
    /// current-player singleton; a networked player's NetEntity would carry its seat's commands here
    /// the same way (UNI-T26). The session also runs the authority's own heart check
    /// (<see cref="CheckPickups"/>) and freezes time while the world is paused.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)] // the world steps before any view reads it
    public sealed class GameSession : MonoBehaviour
    {
        [Header("Players")]
        [Tooltip("The angel seated when the session starts (its Seat is its player id). Falls back to the scene's AngelController.")]
        [SerializeField] private AngelController angel;
        [Tooltip("The play space: where enemies spawn and the chains bounce. Falls back to the scene's LevelBounds.")]
        [SerializeField] private LevelBounds level;

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

        [Header("Score")]
        [Tooltip("Score per point of mace damage (Engine.vb: 1000).")]
        [Min(0f)]
        [SerializeField] private float pointsPerDamage = 1000f;

        [Header("Enemies (Enemy.vb loadEnemy)")]
        [Tooltip("Enemies per wave (maxEnemy 13, so 14).")]
        [Min(0)]
        [SerializeField] private int enemyCount = 14;
        [Min(0.0001f)]
        [SerializeField] private float enemyHp = 1f;
        [Tooltip("Random mass range (Enemy.vb: 1-2). Knockback is divided by mass.")]
        [SerializeField] private Vector2 enemyMassRange = new Vector2(1f, 2f);
        [Tooltip("Collision radius of an enemy, world units.")]
        [Min(0.01f)]
        [SerializeField] private float enemyRadius = 0.3f;
        [Min(0f)]
        [SerializeField] private float seekRate = 0.36f;
        [Min(0f)]
        [SerializeField] private float hurtDeceleration = 6f;
        [Min(0f)]
        [SerializeField] private float recoverSpeed = 0.05f;
        [Tooltip("Engine.vb flockSpd 0.0005/frame, converted at 60 fps and for the original's double visit of each pair (0.06): speed (units/s, per axis, per overlapping neighbour) at which overlapping live, non-grabbed enemies push each other apart. 0 turns separation off.")]
        [Min(0f)]
        [SerializeField] private float flockSpeed = EnemyBrain.DefaultFlockSpeed;

        private readonly List<Seat> _seats = new List<Seat>();
        private GameWorld _world;
        private bool _ownsTimeScale;

        private struct Seat
        {
            public PlayerId Id;
            public AngelController Angel;
            public ChainController Chain;
        }

        /// <summary>Every event the world produced, in order, after each command and step.</summary>
        public event Action<GameEvent> EventRaised;

        /// <summary>Raised after the world resets (its <see cref="WorldReset"/> event), once the hearts are back.</summary>
        public event Action Restarted;

        /// <summary>The world this session is the authority for. Read it; change it only through <see cref="Submit"/>.</summary>
        public GameWorld World
        {
            get
            {
                if (_world == null)
                {
                    _world = new GameWorld(BuildSettings());
                    _world.Arena = ResolveLevel() != null ? level.Bounds.ToCore() : (Rect2?)null;
                }

                return _world;
            }
        }

        /// <summary>HP, score, pause and game over, for the HUD.</summary>
        public GameState State => World.State;

        public AngelController Angel
        {
            get => angel;
            set => angel = value;
        }

        public LevelBounds Level
        {
            get => level;
            set
            {
                level = value;
                if (_world != null)
                {
                    _world.Arena = value != null ? value.Bounds.ToCore() : (Rect2?)null;
                }
            }
        }

        public float ContactRadius
        {
            get => contactRadius;
            set
            {
                contactRadius = Mathf.Max(0f, value);
                RetuneAll();
            }
        }

        /// <summary>Sends a player's command to the world and plays what it produced. Returns false if it changed nothing.</summary>
        public bool Submit(GameCommand command)
        {
            var applied = World.Submit(command);
            Flush();
            return applied;
        }

        /// <summary>One authority step: the world, then the heart check. Called from <c>FixedUpdate</c>; public so tests can drive it.</summary>
        public void Step(float deltaTime)
        {
            World.Step(deltaTime);
            CheckPickups();
            Flush();
        }

        /// <summary>
        /// Seats <paramref name="angel"/> and <paramref name="chain"/> (and the mace, grapple and audio
        /// components beside the chain) as player <paramref name="id"/>: the world gets a player tuned
        /// from them, and each of them is bound to this session and id.
        /// </summary>
        public PlayerEntity Join(PlayerId id, AngelController angel, ChainController chain)
        {
            if (angel == null)
            {
                throw new ArgumentNullException(nameof(angel));
            }

            var player = World.AddPlayer(id, BuildSetup(angel, chain));
            _seats.Add(new Seat { Id = id, Angel = angel, Chain = chain });
            angel.Bind(this, id);
            if (chain != null)
            {
                chain.Bind(this, id);
            }

            return player;
        }

        /// <summary>Seats <paramref name="angel"/> at its own seat with its own chain.</summary>
        public PlayerEntity Join(AngelController angel) => Join(angel.Seat, angel, angel.Chain);

        /// <summary>Re-reads a seated player's tuning from their components (live inspector tweaks).</summary>
        public void Retune(PlayerId id)
        {
            foreach (var seat in _seats)
            {
                if (seat.Id == id && seat.Angel != null)
                {
                    World.Configure(id, BuildSetup(seat.Angel, seat.Chain));
                }
            }
        }

        /// <summary>Starts over in place (Root.vb Reset): the world resets and the views follow its events.</summary>
        public void Restart()
        {
            World.Reset();
            Flush();
        }

        /// <summary>
        /// Item.vb's checkItemCollect, run by the authority: a heart an angel touches is consumed (it
        /// stays gone until a reset) and heals that player +10 HP, capped at max HP. Returns the HP gained.
        /// </summary>
        public int CheckPickups()
        {
            if (!State.IsPlaying || World.Players.Count == 0)
            {
                return 0;
            }

            var gained = 0;
            foreach (var heart in FindObjectsByType<HeartPickup>(FindObjectsSortMode.None))
            {
                if (!heart.isActiveAndEnabled)
                {
                    continue;
                }

                foreach (var player in World.Players)
                {
                    if (heart.TryCollect(player.AngelPosition.ToUnity()))
                    {
                        gained += World.CollectHeart(player.Id, heart.HealAmount);
                        break;
                    }
                }
            }

            return gained;
        }

        private PlayerSetup BuildSetup(AngelController angel, ChainController chain)
        {
            var setup = PlayerSetup.Default;
            setup.ContactRadius = contactRadius;
            angel.Contribute(ref setup);
            if (chain != null)
            {
                chain.Contribute(ref setup);
            }

            return setup;
        }

        private GameWorld.Settings BuildSettings() => new GameWorld.Settings
        {
            Session = new GameState.Settings
            {
                MaxHp = maxHp,
                HurtDelay = hurtDelay,
                ContactDamage = contactDamage,
                PointsPerDamage = pointsPerDamage,
            },
            Enemies = new GameWorld.EnemySetup
            {
                Count = enemyCount,
                Hp = enemyHp,
                MassMin = enemyMassRange.x,
                MassMax = enemyMassRange.y,
                Radius = enemyRadius,
                FlockSpeed = flockSpeed,
                Brain = new EnemyBrain.Settings
                {
                    SeekRate = seekRate,
                    HurtDeceleration = hurtDeceleration,
                    RecoverSpeed = recoverSpeed,
                },
            },
        };

        private LevelBounds ResolveLevel()
        {
            if (level == null)
            {
                level = LevelBounds.Find();
            }

            return level;
        }

        private void RetuneAll()
        {
            if (_world == null)
            {
                return;
            }

            foreach (var seat in _seats)
            {
                Retune(seat.Id);
            }
        }

        /// <summary>Hands the world's events to the listeners, and freezes or frees time on a pause change.</summary>
        private void Flush()
        {
            if (_world == null || _world.PendingEvents.Count == 0)
            {
                return;
            }

            foreach (var gameEvent in _world.TakeEvents())
            {
                if (gameEvent is PauseChanged)
                {
                    ApplyTimeScale();
                }

                if (gameEvent is WorldReset)
                {
                    ResetHearts();
                }

                EventRaised?.Invoke(gameEvent);
                if (gameEvent is WorldReset)
                {
                    Restarted?.Invoke();
                }
            }
        }

        /// <summary>Root.vb's Reset for the hearts: back at their start, drawn again.</summary>
        private static void ResetHearts()
        {
            HeartPickup.AfterReset = true;
            foreach (var heart in FindObjectsByType<HeartPickup>(FindObjectsSortMode.None))
            {
                heart.ResetHeart();
            }
        }

        private void ApplyTimeScale()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (State.IsPaused)
            {
                Time.timeScale = 0f;
                _ownsTimeScale = true;
            }
            else
            {
                ReleaseTimeScale();
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

        private void Awake()
        {
            if (angel == null)
            {
                angel = FindAnyObjectByType<AngelController>();
            }

            ResolveLevel();
            if (angel != null && World.FindPlayer(angel.Seat) == null)
            {
                Join(angel);
            }
        }

        private void Start() => Restart();

        private void FixedUpdate() => Step(Time.fixedDeltaTime);

        private void OnDisable() => ReleaseTimeScale();

        private void OnValidate()
        {
            if (_world == null)
            {
                return;
            }

            var settings = BuildSettings();
            _world.Config = settings;
            _world.State.Config = settings.Session;
            RetuneAll();
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
