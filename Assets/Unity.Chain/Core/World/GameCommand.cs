namespace MindAttic.Chain.Core
{
    /// <summary>
    /// A player's request to change the game, sent to the one authority that owns the
    /// <see cref="GameWorld"/> (<see cref="GameWorld.Submit"/>). Every command carries the id of the
    /// player it comes from; commands are plain immutable data (ids, numbers, <see cref="Vec2"/>) so a
    /// networked player can carry them to the authority unchanged. Presentation never changes the
    /// world any other way.
    /// </summary>
    public abstract class GameCommand
    {
        protected GameCommand(PlayerId player)
        {
            Player = player;
        }

        public PlayerId Player { get; }
    }

    /// <summary>
    /// Fly the player's angel by a mouse delta (Angel.vb renderAngel): applied when it arrives, at the
    /// input rate, so steering is as smooth as the display. <see cref="DeltaTime"/> drives the hover
    /// bob and the wing flap. Ignored while paused (Engine.vb renders and moves the angel only when
    /// not paused).
    /// </summary>
    public sealed class SteerAngel : GameCommand
    {
        public SteerAngel(PlayerId player, Vec2 mouseDelta, float deltaTime) : base(player)
        {
            MouseDelta = mouseDelta;
            DeltaTime = deltaTime;
        }

        public Vec2 MouseDelta { get; }
        public float DeltaTime { get; }

        public override string ToString() => $"SteerAngel {Player} {MouseDelta} dt {DeltaTime}";
    }

    /// <summary>The grapple button went down: the next step arms the mace (a press and release between two steps still arms for one step).</summary>
    public sealed class ArmGrapple : GameCommand
    {
        public ArmGrapple(PlayerId player) : base(player)
        {
        }

        public override string ToString() => $"ArmGrapple {Player}";
    }

    /// <summary>The grapple button came up: the next step throws whatever the mace holds, with the mace's velocity.</summary>
    public sealed class ReleaseGrapple : GameCommand
    {
        public ReleaseGrapple(PlayerId player) : base(player)
        {
        }

        public override string ToString() => $"ReleaseGrapple {Player}";
    }

    /// <summary>Lengthen (+) or shorten (-) the player's chain by <see cref="Links"/> links (Chain.vb's chain-plus and chain-minus keys).</summary>
    public sealed class ChangeChainLength : GameCommand
    {
        public ChangeChainLength(PlayerId player, int links) : base(player)
        {
            Links = links;
        }

        public int Links { get; }

        public override string ToString() => $"ChangeChainLength {Player} {Links:+0;-0;0}";
    }

    /// <summary>
    /// Form1.vb's F key (<c>followMouse = Not followMouse</c>) for the player's chain: off, its springs
    /// go slack so it falls from the hand, and its mace neither hits nor catches (BIBLE D17).
    /// </summary>
    public sealed class ToggleFollow : GameCommand
    {
        public ToggleFollow(PlayerId player) : base(player)
        {
        }

        public override string ToString() => $"ToggleFollow {Player}";
    }

    /// <summary>
    /// Form1.vb's live-tuning keys for the player's chain: move <see cref="Knob"/> by <see cref="Steps"/> of its
    /// step (chain.json's <c>tuning</c>), held to its limits (<see cref="ChainTuning"/>). Insert/Delete are
    /// resistance +1/-1, Home/End gravity -1/+1, PageUp/PageDown spring +1/-1. Works paused too, as Form1.vb's
    /// key handler does; a reset puts the data files' figures back.
    /// </summary>
    public sealed class TuneChain : GameCommand
    {
        public TuneChain(PlayerId player, ChainKnob knob, int steps) : base(player)
        {
            Knob = knob;
            Steps = steps;
        }

        public ChainKnob Knob { get; }
        public int Steps { get; }

        public override string ToString() => $"TuneChain {Player} {Knob} {Steps:+0;-0;0}";
    }

    /// <summary>
    /// Input.vb's Minus and Equals: the game speed (<see cref="GameWorld.GameSpeed"/>) moves by game.json's
    /// <c>gameSpeedStep</c> (0.01) for every original frame of <see cref="HeldSeconds"/> the key was held,
    /// <see cref="Direction"/> -1 (Minus) or +1 (Equals), never below 0 (<see cref="TimeScale"/>). The speed is
    /// the whole world's, whoever holds the key; it works paused too, as Input.vb polls the keys every frame.
    /// </summary>
    public sealed class ChangeGameSpeed : GameCommand
    {
        public ChangeGameSpeed(PlayerId player, int direction, float heldSeconds) : base(player)
        {
            Direction = direction;
            HeldSeconds = heldSeconds;
        }

        public int Direction { get; }

        /// <summary>Real seconds the key was held since the last command (unscaled: it works paused and at any speed).</summary>
        public float HeldSeconds { get; }

        public override string ToString() => $"ChangeGameSpeed {Player} {Direction:+0;-0;0} {HeldSeconds:G4}s";
    }

    /// <summary>Pause or resume the game (Form1.vb's Pause key). A finished game cannot be paused.</summary>
    public sealed class Pause : GameCommand
    {
        public Pause(PlayerId player, bool paused) : base(player)
        {
            Paused = paused;
        }

        public bool Paused { get; }

        public override string ToString() => $"Pause {Player} {Paused}";
    }

    /// <summary>Flip the pause state (Form1.vb's Pause key; P too, BIBLE V8).</summary>
    public sealed class TogglePause : GameCommand
    {
        public TogglePause(PlayerId player) : base(player)
        {
        }

        public override string ToString() => $"TogglePause {Player}";
    }

    /// <summary>Start the game over (Root.vb Reset): full HP, no score, angels home, a fresh flock.</summary>
    public sealed class ResetGame : GameCommand
    {
        public ResetGame(PlayerId player) : base(player)
        {
        }

        public override string ToString() => $"ResetGame {Player}";
    }

    /// <summary>
    /// Quit the game (Form1.vb's Escape: <c>bRunning = False</c>, the form closes). The world itself does not
    /// change; it raises <see cref="QuitRequested"/> and the host that runs it ends the application. Works paused
    /// and after a game over, as Form1.vb's key handler does.
    /// </summary>
    public sealed class QuitGame : GameCommand
    {
        public QuitGame(PlayerId player) : base(player)
        {
        }

        public override string ToString() => $"QuitGame {Player}";
    }
}
