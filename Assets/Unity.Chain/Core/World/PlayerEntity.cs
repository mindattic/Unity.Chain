namespace MindAttic.Chain.Core
{
    /// <summary>
    /// How one player's angel, chain, mace and grapple are tuned when they join the world. Every figure
    /// is the original's, converted to world units per second (see docs/chain-physics.md).
    /// </summary>
    public struct PlayerSetup
    {
        /// <summary>Where the angel starts, and where a reset puts it back (Root.vb Reset: Angel(0) at 0,0).</summary>
        public Vec2 Start;
        public AngelFlight.Settings Flight;

        /// <summary>Links in the chain, anchor included (Chain.vb: 25).</summary>
        public int LinkCount;
        public ChainSimulation.Settings Chain;
        /// <summary>Bounce the links off the world's arena (Elastic Trail's wall bounce).</summary>
        public bool KeepChainInArena;
        /// <summary>Fraction of a link's speed into a wall kept after the bounce.</summary>
        public float WallBounce;
        /// <summary>Whether the chain starts following the hand (Chain.vb: <c>followMouse As Boolean = True</c>).</summary>
        public bool Following;

        /// <summary>The mace's link (Chain.vb's Mace: 6, adjustable 1-12).</summary>
        public int MaceIndex;
        public int MinMaceIndex;
        public int MaxMaceIndex;
        /// <summary>Mass of the mace link; every other link has mass 1 (Chain.vb: 1).</summary>
        public float MaceMass;
        /// <summary>Radius of the mace's hit circle, world units.</summary>
        public float HitRadius;
        /// <summary>Damage per world unit per second of mace speed at impact.</summary>
        public float DamagePerSpeed;

        /// <summary>Radius around the mace that catches an enemy while the grapple is armed.</summary>
        public float CatchRadius;
        /// <summary>Launch velocity = mace velocity x this.</summary>
        public float ThrowMultiplier;

        /// <summary>Radius around the angel that counts as an enemy touching it.</summary>
        public float ContactRadius;

        public static PlayerSetup Default => new PlayerSetup
        {
            Start = Vec2.Zero,
            Flight = AngelFlight.Settings.Default,
            LinkCount = 25,
            Chain = ChainSimulation.Settings.Default,
            KeepChainInArena = true,
            WallBounce = 0.5f,
            Following = ChainFollow.StartsFollowing,
            MaceIndex = 6,
            MinMaceIndex = 1,
            MaxMaceIndex = 12,
            MaceMass = 1f,
            HitRadius = 0.25f,
            DamagePerSpeed = 0.05f,
            CatchRadius = 0.3f,
            ThrowMultiplier = 1f,
            ContactRadius = 0.35f,
        };
    }

    /// <summary>
    /// One player in the <see cref="GameWorld"/>: their angel's flight, their chain, the mace at its end
    /// and their grapple. The world steps all of it; the player's presentation reads it and sends
    /// commands, each carrying this player's <see cref="Id"/>.
    /// </summary>
    public sealed class PlayerEntity
    {
        private PlayerSetup _setup;
        private int _maceIndex;

        internal bool ButtonHeld;
        internal bool PressedSinceStep;
        internal Vec2 PreviousMacePosition;
        internal int PreviousMaceIndex = -1;

        internal PlayerEntity(PlayerId id, PlayerSetup setup)
        {
            Id = id;
            _setup = Sanitize(setup);
            _maceIndex = _setup.MaceIndex;
            Restart();
        }

        public PlayerId Id { get; }
        public PlayerSetup Setup => _setup;

        public AngelFlight Flight { get; private set; }
        public ChainSimulation Chain { get; private set; }
        public Grapple Grapple { get; private set; }

        public int MaxMaceIndex => Calc.Clamp(_setup.MaxMaceIndex, 1, Calc.Max(1, Chain.LinkCount - 1));
        public int MinMaceIndex => Calc.Clamp(_setup.MinMaceIndex, 1, MaxMaceIndex);

        /// <summary>The mace's link: the chain's live end.</summary>
        public int MaceIndex => Calc.Clamp(_maceIndex, MinMaceIndex, MaxMaceIndex);

        public Vec2 MacePosition => Chain.GetPosition(MaceIndex);
        public Vec2 MaceVelocity => Chain.GetVelocity(MaceIndex);

        /// <summary>Where the angel's body is (flight position plus the hover bob): what enemies seek and touch.</summary>
        public Vec2 AngelPosition => Flight.RenderPosition;

        /// <summary>Chain.vb's followMouse: while false the chain hangs slack and the mace neither hits nor catches.</summary>
        public bool IsFollowing => Chain.IsFollowing;

        /// <summary>The enemy the grapple holds, or null.</summary>
        public EnemyEntity HeldEnemy => Grapple.Target as EnemyEntity;

        /// <summary>
        /// Retunes the player (live inspector tweaks, a resized view) without disturbing their state.
        /// A different link count rebuilds the chain at the hand.
        /// </summary>
        internal void Configure(PlayerSetup setup)
        {
            var rebuild = setup.LinkCount != _setup.LinkCount;
            _setup = Sanitize(setup);
            Flight.Config = _setup.Flight;
            Grapple.ThrowMultiplier = _setup.ThrowMultiplier;
            if (rebuild)
            {
                var following = Chain.IsFollowing;
                Chain = new ChainSimulation(_setup.LinkCount, Flight.HandSocket, _setup.Chain) { IsFollowing = following };
                PreviousMaceIndex = -1;
            }

            Chain.Config = _setup.Chain;
            Chain.WallBounce = _setup.WallBounce;
            ApplyMace();
        }

        /// <summary>Sets the mace link, clamped to the chain. Returns true if it moved.</summary>
        internal bool SetMaceIndex(int index)
        {
            var before = MaceIndex;
            _maceIndex = Calc.Clamp(index, MinMaceIndex, MaxMaceIndex);
            ApplyMace();
            return MaceIndex != before;
        }

        /// <summary>
        /// Back to the start (a reset): angel home, chain collapsed on the hand, grapple idle. The chain
        /// length and the follow flag are kept (Root.vb's Reset touches neither).
        /// </summary>
        internal void Restart()
        {
            var following = Chain != null ? Chain.IsFollowing : _setup.Following;
            Flight = new AngelFlight(_setup.Start, _setup.Flight);
            Chain = new ChainSimulation(_setup.LinkCount, Flight.HandSocket, _setup.Chain)
            {
                WallBounce = _setup.WallBounce,
                IsFollowing = following,
            };
            Grapple = new Grapple(_setup.ThrowMultiplier);
            ButtonHeld = false;
            PressedSinceStep = false;
            PreviousMaceIndex = -1;
            ApplyMace();
        }

        /// <summary>The live chain ends at the mace link, which carries the mace's mass; every other link has mass 1.</summary>
        private void ApplyMace()
        {
            var index = MaceIndex;
            Chain.ActiveLinkCount = index + 1;
            for (var i = 1; i < Chain.LinkCount; i++)
            {
                Chain.SetMass(i, i == index ? _setup.MaceMass : 1f);
            }
        }

        private static PlayerSetup Sanitize(PlayerSetup setup)
        {
            setup.LinkCount = Calc.Max(2, setup.LinkCount);
            setup.MaxMaceIndex = Calc.Max(1, setup.MaxMaceIndex);
            setup.MinMaceIndex = Calc.Clamp(setup.MinMaceIndex, 1, setup.MaxMaceIndex);
            setup.MaceMass = Calc.Max(0.01f, setup.MaceMass);
            setup.HitRadius = Calc.Max(0f, setup.HitRadius);
            setup.DamagePerSpeed = Calc.Max(0f, setup.DamagePerSpeed);
            setup.CatchRadius = Calc.Max(0f, setup.CatchRadius);
            setup.ThrowMultiplier = Calc.Max(0f, setup.ThrowMultiplier);
            setup.ContactRadius = Calc.Max(0f, setup.ContactRadius);
            setup.WallBounce = Calc.Clamp01(setup.WallBounce);
            return setup;
        }
    }
}
