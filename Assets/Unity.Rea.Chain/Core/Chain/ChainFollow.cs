namespace MindAttic.Chain.Core
{
    /// <summary>
    /// Form1.vb's F key (<c>followMouse = Not followMouse</c>) as a command. Carries the player
    /// whose chain it toggles so it can join the GameWorld's command set (UNI-T32) unchanged.
    /// </summary>
    public readonly struct ToggleFollowCommand
    {
        public readonly int PlayerId;

        public ToggleFollowCommand(int playerId) => PlayerId = playerId;
    }

    /// <summary>Raised when a chain's follow flag changes: the player and the new state.</summary>
    public readonly struct FollowChangedEvent
    {
        public readonly int PlayerId;
        public readonly bool IsFollowing;

        public FollowChangedEvent(int playerId, bool isFollowing)
        {
            PlayerId = playerId;
            IsFollowing = isFollowing;
        }
    }

    /// <summary>
    /// The rules of Chain.vb's <c>followMouse</c> flag, in one place. While following (the
    /// default, <c>followMouse As Boolean = True</c>) the links are pulled by their springs and the
    /// mace is the mace: drawn at 3.5x the link (<c>Matrix.Scaling(3.5, 3.5, 3.5)</c>) with its hit
    /// rectangle kept on it. Off, <c>animateChain</c> leaves every spring force zero, so only
    /// gravity and drag act and the chain falls away from the hand, and <c>renderChain</c> skips
    /// the mace's scale, offset and rectangle, so it is drawn as a plain link. The flag is not
    /// touched by <c>Reset</c>. The scale is a ratio of the link size, not a screen-space unit,
    /// so the arena remap (UNI-T66) leaves it alone.
    /// </summary>
    public static class ChainFollow
    {
        /// <summary>A chain starts following (Chain.vb: <c>followMouse As Boolean = True</c>).</summary>
        public const bool StartsFollowing = true;

        /// <summary>The mace's size relative to a link while following (Chain.vb: 3.5x).</summary>
        public const float MaceScale = 3.5f;

        /// <summary>The mace's size relative to a link while not following: drawn as a link.</summary>
        public const float SlackMaceScale = 1f;

        /// <summary>The mace's draw scale relative to a link: <paramref name="maceScale"/> while following, else a link's.</summary>
        public static float MaceScaleFor(bool following, float maceScale = MaceScale) =>
            following ? maceScale : SlackMaceScale;

        /// <summary>Whether the mace's hit area is live (Chain.vb rebuilds <c>rectMace</c> only while following).</summary>
        public static bool MaceHits(bool following) => following;

        /// <summary>
        /// Applies <paramref name="command"/> to <paramref name="chain"/> (the chain of that
        /// command's player) and returns the event it produces.
        /// </summary>
        public static FollowChangedEvent Apply(ChainSimulation chain, ToggleFollowCommand command)
        {
            chain.IsFollowing = !chain.IsFollowing;
            return new FollowChangedEvent(command.PlayerId, chain.IsFollowing);
        }
    }
}
