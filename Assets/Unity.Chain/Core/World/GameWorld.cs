using System;
using System.Collections.Generic;

namespace MindAttic.Chain.Core
{
    /// <summary>
    /// The whole game's state and rules in one place, held by one authority. Players change it only by
    /// <see cref="Submit"/>ting <see cref="GameCommand"/>s (each carrying the sender's
    /// <see cref="PlayerId"/>); the authority advances it with <see cref="Step"/>, once per fixed step.
    /// Everything that happens comes out as <see cref="GameEvent"/>s, in order
    /// (<see cref="TakeEvents"/>), for presentation to play. Nothing else mutates it: presentation reads
    /// <see cref="State"/>, <see cref="Players"/> and <see cref="Enemies"/>.
    ///
    /// One <see cref="Step"/> runs the original's frame (Engine.vb oneFrameRender) in the order the port's
    /// components ran it: each chain swings from its angel's hand (Chain.vb), each enemy seeks, flocks and,
    /// thrown, knocks the enemies it touches (Enemy.vb, checkFlocking, checkThrowImpact), each grapple arms,
    /// catches, carries or throws, each mace hits what its swept circle touched (checkMaceImpact), enemies
    /// touching an angel cost HP (checkAngelHit), and a finished game fades out and resets itself
    /// (GameOver, Root.vb Reset). While paused nothing moves.
    /// </summary>
    public sealed class GameWorld
    {
        /// <summary>The enemies a reset spawns (Enemy.vb loadEnemy).</summary>
        public struct EnemySetup
        {
            /// <summary>Enemies per wave (Enemy.vb: maxEnemy 13, so 14).</summary>
            public int Count;
            /// <summary>Starting HP (Root.vb Reset: 1.0).</summary>
            public float Hp;
            /// <summary>Random mass range (Enemy.vb: 1-2). Knockback is divided by mass.</summary>
            public float MassMin;
            public float MassMax;
            /// <summary>Collision circle radius, world units.</summary>
            public float Radius;
            /// <summary>Per-neighbour flocking push, units/s per axis (Engine.vb checkFlocking). 0 turns it off.</summary>
            public float FlockSpeed;
            public EnemyBrain.Settings Brain;

            public static EnemySetup Default => new EnemySetup
            {
                Count = 14,
                Hp = 1f,
                MassMin = 1f,
                MassMax = 2f,
                Radius = 0.3f,
                FlockSpeed = EnemyBrain.DefaultFlockSpeed,
                Brain = EnemyBrain.Settings.Default,
            };
        }

        public struct Settings
        {
            public GameState.Settings Session;
            public EnemySetup Enemies;

            public static Settings Default => new Settings
            {
                Session = GameState.Settings.Default,
                Enemies = EnemySetup.Default,
            };
        }

        /// <summary>Where enemies spawn when there is no arena.</summary>
        public static readonly Rect2 DefaultArena = new Rect2(-8f, -4.5f, 16f, 9f);

        private readonly List<PlayerEntity> _players = new List<PlayerEntity>();
        private readonly List<EnemyEntity> _enemies = new List<EnemyEntity>();
        private readonly List<GameEvent> _events = new List<GameEvent>();
        private readonly List<EnemyEntity> _touched = new List<EnemyEntity>();
        private readonly List<float> _touchedAt = new List<float>();
        private readonly Random _random;
        private Vec2[] _snapshot = new Vec2[0];
        private int _nextEnemyId = 1;

        public GameWorld() : this(Settings.Default)
        {
        }

        public GameWorld(Settings settings, Random random = null)
        {
            Config = settings;
            _random = random ?? new Random();
            State = new GameState(settings.Session);
        }

        public Settings Config;

        /// <summary>HP, score, pause, game over and the fade: the session rules.</summary>
        public GameState State { get; }

        /// <summary>The play space: where enemies spawn and, for players that keep their chain in it, the walls links bounce off.</summary>
        public Rect2? Arena { get; set; }

        /// <summary>Seconds of play stepped so far (paused time does not count).</summary>
        public float Time { get; private set; }

        public IReadOnlyList<PlayerEntity> Players => _players;

        /// <summary>The enemies in the world. A killed or cleared enemy leaves this list at once.</summary>
        public IReadOnlyList<EnemyEntity> Enemies => _enemies;

        /// <summary>Events produced since the last <see cref="TakeEvents"/>, oldest first.</summary>
        public IReadOnlyList<GameEvent> PendingEvents => _events;

        // --- Players -------------------------------------------------------------------------

        /// <summary>Seats a player. Throws if <paramref name="id"/> is already in the world.</summary>
        public PlayerEntity AddPlayer(PlayerId id, PlayerSetup setup)
        {
            if (FindPlayer(id) != null)
            {
                throw new ArgumentException($"Player {id} is already in the world.", nameof(id));
            }

            var player = new PlayerEntity(id, setup);
            _players.Add(player);
            return player;
        }

        /// <summary>Takes a player out of the world. Whatever their grapple held is let go. Returns false if they were not in it.</summary>
        public bool RemovePlayer(PlayerId id)
        {
            var player = FindPlayer(id);
            if (player == null)
            {
                return false;
            }

            player.Grapple.SetHeld(false, Vec2.Zero);
            return _players.Remove(player);
        }

        /// <summary>The player with <paramref name="id"/>, or null.</summary>
        public PlayerEntity FindPlayer(PlayerId id)
        {
            foreach (var player in _players)
            {
                if (player.Id == id)
                {
                    return player;
                }
            }

            return null;
        }

        /// <summary>Retunes a seated player (the authority's settings, not a player command). Returns false if they are not seated.</summary>
        public bool Configure(PlayerId id, PlayerSetup setup)
        {
            var player = FindPlayer(id);
            player?.Configure(setup);
            return player != null;
        }

        // --- Enemies -------------------------------------------------------------------------

        /// <summary>The enemy with <paramref name="id"/> still in the world, or null.</summary>
        public EnemyEntity FindEnemy(EnemyId id)
        {
            foreach (var enemy in _enemies)
            {
                if (enemy.Id == id)
                {
                    return enemy;
                }
            }

            return null;
        }

        /// <summary>
        /// Puts an enemy in the world at <paramref name="position"/>, set up from
        /// <see cref="Settings.Enemies"/> with a random mass in its range unless <paramref name="mass"/>
        /// fixes it, and <paramref name="hp"/> overriding the setup's HP.
        /// </summary>
        public EnemyEntity SpawnEnemy(Vec2 position, float? mass = null, float? hp = null)
        {
            var setup = Config.Enemies;
            var lo = Calc.Min(setup.MassMin, setup.MassMax);
            var hi = Calc.Max(setup.MassMin, setup.MassMax);
            var rolled = mass ?? (hi - lo < 1e-6f ? lo : lo + (float)_random.NextDouble() * (hi - lo));
            var brain = new EnemyBrain(hp ?? setup.Hp, rolled, setup.Brain);
            var enemy = new EnemyEntity(new EnemyId(_nextEnemyId++), brain, position, Calc.Max(0f, setup.Radius));
            _enemies.Add(enemy);
            Emit(new EnemySpawned(enemy.Id, position));
            return enemy;
        }

        /// <summary>Spawns <see cref="EnemySetup.Count"/> enemies at random points in the arena (Enemy.vb loadEnemy).</summary>
        public void SpawnWave()
        {
            foreach (var point in RandomPositions(Arena ?? DefaultArena, Config.Enemies.Count, _random))
            {
                SpawnEnemy(point);
            }
        }

        /// <summary>Takes every enemy out of the world; grapples holding one let go.</summary>
        public void ClearEnemies()
        {
            foreach (var player in _players)
            {
                if (player.Grapple.State == GrappleState.Holding)
                {
                    player.Grapple.Drop();
                }
            }

            foreach (var enemy in _enemies)
            {
                enemy.InWorld = false;
                Emit(new EnemyRemoved(enemy.Id));
            }

            _enemies.Clear();
        }

        /// <summary>Random points inside <paramref name="area"/>, <paramref name="count"/> of them.</summary>
        public static Vec2[] RandomPositions(Rect2 area, int count, Random random)
        {
            var points = new Vec2[Calc.Max(0, count)];
            for (var i = 0; i < points.Length; i++)
            {
                points[i] = new Vec2(
                    area.XMin + (float)random.NextDouble() * area.Width,
                    area.YMin + (float)random.NextDouble() * area.Height);
            }

            return points;
        }

        // --- The session ---------------------------------------------------------------------

        /// <summary>
        /// Starts the game over (Root.vb Reset): full HP, no score, unpaused, full light, every angel home
        /// with its chain collapsed on its hand and its grapple idle, and a fresh wave of enemies. The
        /// authority calls this once to begin; a finished game calls it itself when the fade is done.
        /// </summary>
        public void Reset()
        {
            ClearEnemies();
            State.Reset();
            foreach (var player in _players)
            {
                player.Restart();
            }

            Emit(new WorldReset());
            SpawnWave();
        }

        /// <summary>
        /// The authority's heart check found <paramref name="player"/>'s angel touching a heart worth
        /// <paramref name="amount"/> HP (Engine.vb checkItemCollect). Heals up to max HP. Returns the HP gained.
        /// </summary>
        public int CollectHeart(PlayerId player, int amount)
        {
            if (!State.IsPlaying || FindPlayer(player) == null)
            {
                return 0;
            }

            var gained = State.Heal(amount);
            if (gained > 0)
            {
                Emit(new AngelHealed(player, State.Hp, gained));
            }

            return gained;
        }

        // --- Commands ------------------------------------------------------------------------

        /// <summary>
        /// Applies a player's command. Returns false when it changed nothing: the sender is not seated,
        /// the game is paused (only pause and reset work then), or the command had no effect.
        /// </summary>
        public bool Submit(GameCommand command)
        {
            switch (command)
            {
                case null:
                    throw new ArgumentNullException(nameof(command));

                case Pause pause:
                    return SetPaused(pause.Player, pause.Paused);

                case TogglePause toggle:
                    return SetPaused(toggle.Player, !State.IsPaused);

                case ResetGame _:
                    Reset();
                    return true;
            }

            var player = FindPlayer(command.Player);
            if (player == null)
            {
                return false;
            }

            switch (command)
            {
                case SteerAngel steer:
                    if (State.IsPaused)
                    {
                        return false;
                    }

                    player.Flight.Step(steer.MouseDelta, Calc.Max(0f, steer.DeltaTime));
                    return true;

                case ArmGrapple _:
                    if (player.ButtonHeld)
                    {
                        return false;
                    }

                    player.ButtonHeld = true;
                    player.PressedSinceStep = true;
                    return true;

                case ReleaseGrapple _:
                    if (!player.ButtonHeld)
                    {
                        return false;
                    }

                    player.ButtonHeld = false;
                    return true;

                case ToggleFollow _:
                    var changed = ChainFollow.Apply(player.Chain, new ToggleFollowCommand(player.Id.Value));
                    Emit(new FollowChanged(player.Id, changed.IsFollowing));
                    return true;

                case ChangeChainLength change:
                    if (State.IsPaused || !player.SetMaceIndex(player.MaceIndex + change.Links))
                    {
                        return false;
                    }

                    Emit(new ChainLengthChanged(player.Id, player.MaceIndex));
                    return true;

                default:
                    throw new ArgumentException($"Unknown command {command.GetType().Name}.", nameof(command));
            }
        }

        private bool SetPaused(PlayerId by, bool paused)
        {
            var before = State.IsPaused;
            if (State.SetPaused(paused) == before)
            {
                return false;
            }

            Emit(new PauseChanged(by, State.IsPaused));
            return true;
        }

        // --- The step ------------------------------------------------------------------------

        /// <summary>One fixed step of <paramref name="deltaTime"/> seconds. Does nothing while paused.</summary>
        public void Step(float deltaTime)
        {
            if (State.IsPaused || deltaTime <= 0f)
            {
                return;
            }

            Time += deltaTime;

            foreach (var player in _players)
            {
                StepChain(player, deltaTime);
            }

            StepEnemies(deltaTime);

            foreach (var player in _players)
            {
                StepGrapple(player);
            }

            foreach (var player in _players)
            {
                StepMace(player);
            }

            foreach (var player in _players)
            {
                CheckContact(player);
            }

            StepGameOver();
        }

        /// <summary>Link 0 follows the angel's hand; the rest swing on their springs.</summary>
        private void StepChain(PlayerEntity player, float deltaTime)
        {
            var chain = player.Chain;
            chain.Bounds = player.Setup.KeepChainInArena ? Arena : null;
            chain.SetAnchor(player.Flight.HandSocket);
            chain.Step(deltaTime);
        }

        /// <summary>
        /// Every free enemy moves by its brain's velocity plus the flocking push from each live, ungrabbed
        /// enemy it overlaps; a thrown one knocks every hittable enemy it touches. Overlaps are judged on
        /// where everyone stood at the start of the step, so the order enemies move in does not matter.
        /// </summary>
        private void StepEnemies(float deltaTime)
        {
            var count = _enemies.Count;
            if (_snapshot.Length < count)
            {
                _snapshot = new Vec2[count];
            }

            for (var i = 0; i < count; i++)
            {
                _snapshot[i] = _enemies[i].Position;
            }

            var flockSpeed = Config.Enemies.FlockSpeed;
            for (var i = 0; i < count; i++)
            {
                var enemy = _enemies[i];
                if (!enemy.IsAlive || enemy.State == EnemyState.Grabbed)
                {
                    continue;
                }

                var here = _snapshot[i];
                var velocity = enemy.Brain.Step(here, SeekPoint(here), deltaTime);
                var push = Vec2.Zero;

                for (var j = 0; j < count; j++)
                {
                    var other = _enemies[j];
                    if (j == i || !Touching(here, enemy.Radius, _snapshot[j], other.Radius))
                    {
                        continue;
                    }

                    if (flockSpeed > 0f && other.IsAlive && other.State != EnemyState.Grabbed)
                    {
                        var tieBreak = enemy.Id.Value < other.Id.Value ? 1 : -1;
                        push += EnemyBrain.FlockPush(here, _snapshot[j], flockSpeed, deltaTime, tieBreak);
                    }

                    if (enemy.Brain.IsThrown && other.CanBeHit)
                    {
                        other.Brain.Knock(enemy.Brain.Velocity);
                        Emit(new EnemyKnocked(enemy.Id, other.Id));
                    }
                }

                enemy.Position = here + velocity * deltaTime + push;
            }
        }

        /// <summary>The nearest angel: enemies home on whoever is closest.</summary>
        private Vec2 SeekPoint(Vec2 from)
        {
            var best = from;
            var bestDistance = float.PositiveInfinity;
            foreach (var player in _players)
            {
                var distance = (player.AngelPosition - from).SqrMagnitude;
                if (distance < bestDistance)
                {
                    best = player.AngelPosition;
                    bestDistance = distance;
                }
            }

            return best;
        }

        /// <summary>
        /// Arms or releases from the button, catches the nearest grabbable enemy inside the catch radius
        /// while armed, and carries what it holds with the mace.
        /// </summary>
        private void StepGrapple(PlayerEntity player)
        {
            var grapple = player.Grapple;
            var before = grapple.State;

            // A press and release that both landed between two steps still counts as a press: arm for
            // this step so a quick tap can catch what the mace is touching.
            var held = player.ButtonHeld || player.PressedSinceStep;
            player.PressedSinceStep = false;

            if (grapple.SetHeld(held, player.MaceVelocity) is EnemyEntity thrown)
            {
                Emit(new Thrown(player.Id, thrown.Id, player.MaceVelocity * grapple.ThrowMultiplier));
            }

            // The catch is the mace's touch, which is not kept while the chain is not following (BIBLE D17).
            if (grapple.State == GrappleState.Armed && ChainFollow.MaceHits(player.IsFollowing))
            {
                var target = NearestGrabbable(player.MacePosition, player.Setup.CatchRadius);
                if (target != null && grapple.TryCatch(target))
                {
                    Emit(new Grabbed(player.Id, target.Id));
                }
            }

            grapple.Carry(player.MacePosition);
            EmitGrappleChange(player, before);
        }

        private EnemyEntity NearestGrabbable(Vec2 center, float radius)
        {
            EnemyEntity nearest = null;
            var nearestDistance = float.PositiveInfinity;
            foreach (var enemy in _enemies)
            {
                if (!enemy.CanBeHit || !enemy.Overlaps(center, radius))
                {
                    continue;
                }

                var distance = (enemy.Position - center).SqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearest = enemy;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Engine.vb checkMaceImpact on the mace's swept circle: every enemy it touched since the last
        /// step, nearest the start first, is caught instead if the grapple is armed with an empty grip,
        /// else knocked back at the mace's velocity / its mass, damaged by mace speed x damage per speed
        /// and scored; an enemy held in the grapple takes the same damage.
        /// </summary>
        private void StepMace(PlayerEntity player)
        {
            // Not following (F key): Chain.vb stops keeping rectMace on the mace, so it neither hits nor
            // catches (BIBLE D17). Forget the last position so turning it back on does not sweep from there.
            if (!ChainFollow.MaceHits(player.IsFollowing))
            {
                player.PreviousMaceIndex = -1;
                return;
            }

            var index = player.MaceIndex;
            var position = player.MacePosition;

            // First step, or the chain was shortened/lengthened: no sweep from a different link.
            var from = index == player.PreviousMaceIndex ? player.PreviousMacePosition : position;
            player.PreviousMaceIndex = index;
            player.PreviousMacePosition = position;

            CollectTouched(from, position, player.Setup.HitRadius);
            if (_touched.Count == 0)
            {
                return;
            }

            var grapple = player.Grapple;
            var before = grapple.State;
            var velocity = player.MaceVelocity;
            var held = player.HeldEnemy;

            foreach (var enemy in _touched.ToArray())
            {
                if (!enemy.CanBeHit)
                {
                    continue;
                }

                if (grapple.State == GrappleState.Armed && grapple.TryCatch(enemy))
                {
                    grapple.Carry(position);
                    held = enemy;
                    Emit(new Grabbed(player.Id, enemy.Id));
                    continue;
                }

                var damage = enemy.Brain.Hit(velocity, player.Setup.DamagePerSpeed);
                var points = State.AddHitScore(damage);
                var killed = !enemy.IsAlive;
                Emit(new EnemyHit(player.Id, enemy.Id, damage, velocity, points, killed));
                if (killed)
                {
                    Kill(enemy, player.Id);
                }

                if (held != null && held != enemy && held.InWorld && held.Brain.Damage(damage))
                {
                    Kill(held, player.Id);
                    held = null;
                }
            }

            EmitGrappleChange(player, before);
        }

        /// <summary>Enemies a circle of <paramref name="radius"/> touches moving from <paramref name="from"/> to <paramref name="to"/>, nearest the start first.</summary>
        private void CollectTouched(Vec2 from, Vec2 to, float radius)
        {
            _touched.Clear();
            _touchedAt.Clear();
            var moved = (to - from).SqrMagnitude > 1e-10f;

            foreach (var enemy in _enemies)
            {
                float fraction;
                if (moved && Sweep.CircleCast(from, to, radius, enemy.Position, enemy.Radius, out fraction))
                {
                    Insert(enemy, fraction);
                }
                else if (enemy.Overlaps(to, radius))
                {
                    // Whatever the mace rests in now comes after everything the sweep met.
                    Insert(enemy, 2f);
                }
            }
        }

        private void Insert(EnemyEntity enemy, float fraction)
        {
            var at = _touched.Count;
            while (at > 0 && _touchedAt[at - 1] > fraction)
            {
                at--;
            }

            _touched.Insert(at, enemy);
            _touchedAt.Insert(at, fraction);
        }

        private void Kill(EnemyEntity enemy, PlayerId by)
        {
            foreach (var player in _players)
            {
                if (player.HeldEnemy == enemy)
                {
                    var before = player.Grapple.State;
                    player.Grapple.Drop();
                    EmitGrappleChange(player, before);
                }
            }

            enemy.InWorld = false;
            _enemies.Remove(enemy);
            Emit(new EnemyKilled(by, enemy.Id, enemy.Position));
        }

        /// <summary>Engine.vb checkAngelHit: a live, ungrabbed enemy touching the angel costs HP, at most once per hurt delay.</summary>
        private void CheckContact(PlayerEntity player)
        {
            var radius = player.Setup.ContactRadius;
            if (!State.IsPlaying || radius <= 0f)
            {
                return;
            }

            var angel = player.AngelPosition;
            foreach (var enemy in _enemies)
            {
                if (!enemy.IsAlive || enemy.State == EnemyState.Grabbed || !enemy.Overlaps(angel, radius))
                {
                    continue;
                }

                if (State.TryHurt(Time))
                {
                    Emit(new AngelHurt(player.Id, State.Hp));
                    if (State.IsGameOver)
                    {
                        Emit(new GameOver());
                    }
                }

                return;
            }
        }

        /// <summary>Engine.vb GameOver: the light fades 5 a step, then the game resets itself.</summary>
        private void StepGameOver()
        {
            if (State.IsGameOver && State.AdvanceGameOver())
            {
                Reset();
            }
        }

        // --- Events --------------------------------------------------------------------------

        /// <summary>Hands over every event produced since the last call, oldest first, and forgets them.</summary>
        public GameEvent[] TakeEvents()
        {
            var events = _events.ToArray();
            _events.Clear();
            return events;
        }

        private void Emit(GameEvent gameEvent) => _events.Add(gameEvent);

        private void EmitGrappleChange(PlayerEntity player, GrappleState before)
        {
            if (player.Grapple.State != before)
            {
                Emit(new GrappleChanged(player.Id, player.Grapple.State));
            }
        }

        private static bool Touching(Vec2 a, float ra, Vec2 b, float rb)
        {
            var reach = ra + rb;
            return (a - b).SqrMagnitude < reach * reach;
        }
    }
}
