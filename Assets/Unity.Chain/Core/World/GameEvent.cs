namespace MindAttic.Chain.Core
{
    /// <summary>
    /// Something that happened in the <see cref="GameWorld"/>, produced by a command or a step and
    /// collected in order (<see cref="GameWorld.TakeEvents"/>). Presentation plays events (sounds,
    /// deaths, spawns) and reads state; it never changes the world. Events are plain immutable data,
    /// so an authority can send them to remote players as they are.
    /// </summary>
    public abstract class GameEvent
    {
    }

    /// <summary>An enemy entered the world at <see cref="Position"/>.</summary>
    public sealed class EnemySpawned : GameEvent
    {
        public EnemySpawned(EnemyId enemy, Vec2 position)
        {
            Enemy = enemy;
            Position = position;
        }

        public EnemyId Enemy { get; }
        public Vec2 Position { get; }

        public override string ToString() => $"EnemySpawned {Enemy} at {Position}";
    }

    /// <summary>An enemy left the world without dying (a reset cleared it).</summary>
    public sealed class EnemyRemoved : GameEvent
    {
        public EnemyRemoved(EnemyId enemy)
        {
            Enemy = enemy;
        }

        public EnemyId Enemy { get; }

        public override string ToString() => $"EnemyRemoved {Enemy}";
    }

    /// <summary>
    /// The player's mace hit an enemy (Engine.vb checkMaceImpact): it took <see cref="Damage"/>, was
    /// knocked back by <see cref="MaceVelocity"/> / its mass, scored <see cref="Points"/>, and
    /// <see cref="Killed"/> says whether the hit was lethal.
    /// </summary>
    public sealed class EnemyHit : GameEvent
    {
        public EnemyHit(PlayerId player, EnemyId enemy, float damage, Vec2 maceVelocity, int points, bool killed)
        {
            Player = player;
            Enemy = enemy;
            Damage = damage;
            MaceVelocity = maceVelocity;
            Points = points;
            Killed = killed;
        }

        public PlayerId Player { get; }
        public EnemyId Enemy { get; }
        public float Damage { get; }
        public Vec2 MaceVelocity { get; }
        public int Points { get; }
        public bool Killed { get; }

        public override string ToString() => $"EnemyHit {Player} {Enemy} dmg {Damage:0.###} pts {Points}{(Killed ? " lethal" : "")}";
    }

    /// <summary>A thrown enemy knocked another one (Engine.vb checkThrowImpact).</summary>
    public sealed class EnemyKnocked : GameEvent
    {
        public EnemyKnocked(EnemyId thrown, EnemyId enemy)
        {
            Thrown = thrown;
            Enemy = enemy;
        }

        public EnemyId Thrown { get; }
        public EnemyId Enemy { get; }

        public override string ToString() => $"EnemyKnocked {Enemy} by {Thrown}";
    }

    /// <summary>An enemy's HP ran out; it is gone from the world. <see cref="Player"/> is whose mace did it.</summary>
    public sealed class EnemyKilled : GameEvent
    {
        public EnemyKilled(PlayerId player, EnemyId enemy, Vec2 position)
        {
            Player = player;
            Enemy = enemy;
            Position = position;
        }

        public PlayerId Player { get; }
        public EnemyId Enemy { get; }
        public Vec2 Position { get; }

        public override string ToString() => $"EnemyKilled {Enemy} by {Player}";
    }

    /// <summary>The player's grapple changed state (armed, holding, idle).</summary>
    public sealed class GrappleChanged : GameEvent
    {
        public GrappleChanged(PlayerId player, GrappleState state)
        {
            Player = player;
            State = state;
        }

        public PlayerId Player { get; }
        public GrappleState State { get; }

        public override string ToString() => $"GrappleChanged {Player} {State}";
    }

    /// <summary>The player's armed mace caught an enemy.</summary>
    public sealed class Grabbed : GameEvent
    {
        public Grabbed(PlayerId player, EnemyId enemy)
        {
            Player = player;
            Enemy = enemy;
        }

        public PlayerId Player { get; }
        public EnemyId Enemy { get; }

        public override string ToString() => $"Grabbed {Player} {Enemy}";
    }

    /// <summary>The player let go of the grapple and threw the enemy it held at <see cref="Velocity"/>.</summary>
    public sealed class Thrown : GameEvent
    {
        public Thrown(PlayerId player, EnemyId enemy, Vec2 velocity)
        {
            Player = player;
            Enemy = enemy;
            Velocity = velocity;
        }

        public PlayerId Player { get; }
        public EnemyId Enemy { get; }
        public Vec2 Velocity { get; }

        public override string ToString() => $"Thrown {Player} {Enemy} at {Velocity}";
    }

    /// <summary>The player's chain was lengthened or shortened; <see cref="MaceIndex"/> is the new end link.</summary>
    public sealed class ChainLengthChanged : GameEvent
    {
        public ChainLengthChanged(PlayerId player, int maceIndex)
        {
            Player = player;
            MaceIndex = maceIndex;
        }

        public PlayerId Player { get; }
        public int MaceIndex { get; }

        public override string ToString() => $"ChainLengthChanged {Player} mace {MaceIndex}";
    }

    /// <summary>Form1.vb's U put the gems on the player's chain (<see cref="PlayerEntity.LinkSkin"/> now names them).</summary>
    public sealed class LinksSkinned : GameEvent
    {
        public LinksSkinned(PlayerId player)
        {
            Player = player;
        }

        public PlayerId Player { get; }

        public override string ToString() => $"LinksSkinned {Player}";
    }

    /// <summary>The player's chain started or stopped following the hand (the F key, <see cref="ChainFollow"/>).</summary>
    public sealed class FollowChanged : GameEvent
    {
        public FollowChanged(PlayerId player, bool isFollowing)
        {
            Player = player;
            IsFollowing = isFollowing;
        }

        public PlayerId Player { get; }
        public bool IsFollowing { get; }

        public override string ToString() => $"FollowChanged {Player} {IsFollowing}";
    }

    /// <summary>
    /// A live-tuning key moved the player's chain's <see cref="Knob"/> to <see cref="Value"/> (world units and
    /// seconds; the HUD's Res/Grav/Spring readout maps it back, <see cref="ChainReadout"/>).
    /// </summary>
    public sealed class ChainTuned : GameEvent
    {
        public ChainTuned(PlayerId player, ChainKnob knob, float value)
        {
            Player = player;
            Knob = knob;
            Value = value;
        }

        public PlayerId Player { get; }
        public ChainKnob Knob { get; }
        public float Value { get; }

        public override string ToString() => $"ChainTuned {Player} {Knob} {Value:G6}";
    }

    /// <summary>
    /// Minus or Equals moved the world's game speed to <see cref="Speed"/> (the time scale every step runs at; the
    /// HUD shows it with two decimals, <c>FormatNumber(gameSpeed, 2)</c>). <see cref="Player"/> held the key.
    /// </summary>
    public sealed class GameSpeedChanged : GameEvent
    {
        public GameSpeedChanged(PlayerId player, float speed)
        {
            Player = player;
            Speed = speed;
        }

        public PlayerId Player { get; }
        public float Speed { get; }

        public override string ToString() => $"GameSpeedChanged {Player} {Speed:0.00}";
    }

    /// <summary>An enemy touched the player's angel and cost HP (Engine.vb checkAngelHit).</summary>
    public sealed class AngelHurt : GameEvent
    {
        public AngelHurt(PlayerId player, int hp)
        {
            Player = player;
            Hp = hp;
        }

        public PlayerId Player { get; }
        public int Hp { get; }

        public override string ToString() => $"AngelHurt {Player} hp {Hp}";
    }

    /// <summary>The player's angel collected a heart (Engine.vb checkItemCollect).</summary>
    public sealed class AngelHealed : GameEvent
    {
        public AngelHealed(PlayerId player, int hp, int gained)
        {
            Player = player;
            Hp = hp;
            Gained = gained;
        }

        public PlayerId Player { get; }
        public int Hp { get; }
        public int Gained { get; }

        public override string ToString() => $"AngelHealed {Player} +{Gained} hp {Hp}";
    }

    /// <summary>The game was paused or resumed by <see cref="Player"/>.</summary>
    public sealed class PauseChanged : GameEvent
    {
        public PauseChanged(PlayerId player, bool paused)
        {
            Player = player;
            Paused = paused;
        }

        public PlayerId Player { get; }
        public bool Paused { get; }

        public override string ToString() => $"PauseChanged {Player} {Paused}";
    }

    /// <summary>HP ran out: the light starts to fade, and the world resets itself when it has (Engine.vb GameOver).</summary>
    public sealed class GameOver : GameEvent
    {
        public override string ToString() => "GameOver";
    }

    /// <summary>The world started over (Root.vb Reset). The fresh flock's <see cref="EnemySpawned"/> events follow.</summary>
    public sealed class WorldReset : GameEvent
    {
        public override string ToString() => "WorldReset";
    }

    /// <summary><see cref="Player"/> asked to quit (Form1.vb's Escape): the host ends the application.</summary>
    public sealed class QuitRequested : GameEvent
    {
        public QuitRequested(PlayerId player)
        {
            Player = player;
        }

        public PlayerId Player { get; }

        public override string ToString() => $"QuitRequested {Player}";
    }
}
