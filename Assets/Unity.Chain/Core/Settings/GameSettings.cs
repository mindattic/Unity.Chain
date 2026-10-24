using System;
using System.Collections.Generic;

namespace MindAttic.Chain.Core
{
    /// <summary>
    /// The settings registry: every tuning figure of the game, loaded from the data files (StreamingAssets/Settings
    /// in the build) through an <see cref="ISettingsSource"/> and validated on load.
    ///
    /// Two layers, kept apart on purpose:
    /// <list type="number">
    /// <item><b>The files</b> hold the original's own figures, named and valued exactly as its source has them, in
    /// its own units (screen space, per frame): <see cref="Chain"/>, <see cref="Angel"/>, <see cref="Enemies"/>,
    /// <see cref="Item"/>, <see cref="Game"/>, <see cref="Lighting"/>, <see cref="Camera"/>, <see cref="Audio"/>,
    /// <see cref="Input"/>, <see cref="Hud"/>, <see cref="Area"/>.
    /// A figure the original never had (a circle radius where it tested rectangles, the camera rig's follow) sits in
    /// that file's <c>port</c> object, in world units and seconds, and is never mapped.</item>
    /// <item><b>The port's values</b> (<see cref="Player"/>, <see cref="World"/>, <see cref="Heart"/>,
    /// <see cref="Light"/>, <see cref="CameraTuning"/>, <see cref="AudioTuning"/>, <see cref="MaceScale"/>, <see cref="Steering"/>) are
    /// worked out once, here, by <see cref="OriginalSpace"/> (space.json), the one original-to-port mapping.</item>
    /// </list>
    /// </summary>
    public sealed class GameSettings
    {
        /// <summary>The folder under StreamingAssets the data files live in.</summary>
        public const string Folder = "Settings";

        public const string SpaceFile = "space.json";
        public const string ChainFile = "chain.json";
        public const string AngelFile = "angel.json";
        public const string EnemiesFile = "enemies.json";
        public const string ItemFile = "item.json";
        public const string GameFile = "game.json";
        public const string LightingFile = "lighting.json";
        public const string CameraFile = "camera.json";
        public const string AudioFile = "audio.json";
        public const string InputFile = "input.json";
        public const string HudFile = "hud.json";
        public const string AreaFile = "area.json";

        /// <summary>Every data file the registry reads.</summary>
        public static readonly string[] Files =
        {
            SpaceFile, ChainFile, AngelFile, EnemiesFile, ItemFile, GameFile, LightingFile, CameraFile, AudioFile, InputFile, HudFile, AreaFile,
        };

        private GameSettings()
        {
        }

        public SpaceData Space { get; private set; }
        public ChainData Chain { get; private set; }
        public AngelData Angel { get; private set; }
        public EnemyData Enemies { get; private set; }
        public ItemData Item { get; private set; }
        public GameData Game { get; private set; }
        public LightingData Lighting { get; private set; }
        public CameraData Camera { get; private set; }
        public AudioData Audio { get; private set; }
        public InputData Input { get; private set; }

        /// <summary>hud.json: the HUD's layout, colours and FPS counter, in the original window's pixels (UNI-T50).</summary>
        public HudData Hud { get; private set; }

        /// <summary>area.json: Background.vb's quad, the area pictures and B/N, Cube.vb's brick room (UNI-T40).</summary>
        public AreaData Area { get; private set; }

        /// <summary>The original-to-port mapping (space.json).</summary>
        public OriginalSpace Mapping { get; private set; }

        /// <summary>One player's angel, chain, mace and grapple, in world units and seconds. The angel's bounds are the
        /// default arena inset by <see cref="AngelData.Port"/>'s margin; the authority sets them to its own arena.</summary>
        public PlayerSetup Player { get; private set; }

        /// <summary>The session rules and the enemies, in world units and seconds.</summary>
        public GameWorld.Settings World { get; private set; }

        /// <summary>The heart, in world units, still per original frame (<see cref="HeartFloat"/> steps frame by frame).</summary>
        public HeartFloat.Settings Heart { get; private set; }

        public LightingState.Settings Light { get; private set; }

        public CameraTuning CameraTuning { get; private set; }

        /// <summary>The 0 key's screen shake: the most one draw moves the view, world units (<see cref="ScreenShake"/>).</summary>
        public float ShakeAmount { get; private set; }

        /// <summary>Camera.vb's camera and the numpad, X and Z keys that move it, world units and seconds (<see cref="DebugCamera"/>, UNI-T57).</summary>
        public DebugCamera.Settings DebugCameraSettings { get; private set; }

        public AudioTuning AudioTuning { get; private set; }

        /// <summary>The mace's draw size relative to a link while the chain follows (Chain.vb: 3.5).</summary>
        public float MaceScale { get; private set; }

        /// <summary>How a player's stick adds to their mouse steering (input.json: Input.vb's joystick, BIBLE D14).</summary>
        public SteerInput.Settings Steering { get; private set; }

        /// <summary>Zone.vb's hit box overlay (hud.json's <c>zone</c>) in world units (<see cref="HitZones"/>, UNI-T56).</summary>
        public HitZones.Settings Zones { get; private set; }

        /// <summary>The backdrop quad and the brick room in world units (area.json, <see cref="Core.Backdrop"/>, UNI-T40).</summary>
        public Backdrop.Settings Backdrop { get; private set; }

        /// <summary>
        /// Reads and validates every data file from <paramref name="source"/>, then maps them to the port. Throws a
        /// <see cref="SettingsException"/> listing every problem (a missing file, bad JSON with its line, a missing or
        /// unknown key, a value of the wrong type or out of range) if anything is wrong.
        /// </summary>
        public static GameSettings Load(ISettingsSource source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var errors = new List<string>();
            var settings = new GameSettings
            {
                Space = Read(source, SpaceFile, errors, SpaceData.Read),
                Chain = Read(source, ChainFile, errors, ChainData.Read),
                Angel = Read(source, AngelFile, errors, AngelData.Read),
                Enemies = Read(source, EnemiesFile, errors, EnemyData.Read),
                Item = Read(source, ItemFile, errors, ItemData.Read),
                Game = Read(source, GameFile, errors, GameData.Read),
                Lighting = Read(source, LightingFile, errors, LightingData.Read),
                Camera = Read(source, CameraFile, errors, CameraData.Read),
                Audio = Read(source, AudioFile, errors, AudioData.Read),
                Input = Read(source, InputFile, errors, InputData.Read),
                Hud = Read(source, HudFile, errors, HudData.Read),
                Area = Read(source, AreaFile, errors, AreaData.Read),
            };

            if (errors.Count > 0)
            {
                throw new SettingsException(errors, source.Describe);
            }

            settings.Map();
            return settings;
        }

        private static T Read<T>(ISettingsSource source, string file, List<string> errors, Func<SettingsReader, T> read)
        {
            string text;
            try
            {
                text = source.Read(file);
            }
            catch (SettingsException e)
            {
                // A source that checks what it serves (TuningOverrideSource) names its own problems.
                errors.AddRange(e.Errors);
                return default;
            }
            catch (Exception e)
            {
                errors.Add($"{file}: could not be read ({e.Message})");
                return default;
            }

            if (text == null)
            {
                errors.Add($"{file}: not found");
                return default;
            }

            JsonValue root;
            try
            {
                root = Json.Parse(text);
            }
            catch (JsonException e)
            {
                errors.Add($"{file}: is not valid JSON ({e.Message})");
                return default;
            }

            if (root.Kind != JsonKind.Object)
            {
                errors.Add($"{file}: must hold one JSON object {{ ... }}");
                return default;
            }

            var before = errors.Count;
            var reader = new SettingsReader(root, file, errors);
            var data = read(reader);
            reader.Done();
            return errors.Count == before ? data : default;
        }

        /// <summary>The one place the original's figures become the port's (see <see cref="OriginalSpace"/>).</summary>
        private void Map()
        {
            var m = Mapping = new OriginalSpace(Space.LengthScale, Space.FramesPerSecond);
            var chain = Chain;
            var angel = Angel;
            var enemies = Enemies;
            var item = Item;

            var flight = new AngelFlight.Settings(
                sensitivity: m.PerInput(angel.Ratio3D),
                bounds: PlayArea.Inset(GameWorld.DefaultArena, angel.Port.BoundsMargin),
                hoverAmplitude: m.Length(angel.HoverMax),
                // Angel.vb's triangle wave climbs hoverMax, falls 2 x hoverMax and climbs back, hoverRate a frame.
                hoverPeriod: m.Seconds(4f * angel.HoverMax / angel.HoverRate),
                frameDuration: angel.FlapRate,
                frameCount: angel.FlapFrames,
                handOffset: angel.Port.HandOffset);

            Player = new PlayerSetup
            {
                Start = Vec2.Zero,
                Flight = flight,
                // Chain.vb's links are 0..maxLinks.
                LinkCount = chain.MaxLinks + 1,
                Chain = new ChainSimulation.Settings(
                    segmentLength: m.Length(chain.SegLength),
                    springConstant: m.PerSecondSquared(chain.SpringK),
                    resistance: m.PerSecond(chain.Resistance),
                    gravity: m.Acceleration(chain.Gravity),
                    stopVelocity: m.Speed(chain.StopVel),
                    stopAcceleration: m.Acceleration(chain.StopAcc),
                    overheadBoost: chain.OverheadBoost,
                    // "Above the angel" is within one link size below link(0).
                    overheadMargin: m.Length(chain.LinkSize)),
                KeepChainInArena = chain.Port.KeepInArena,
                WallBounce = chain.Port.WallBounce,
                Following = chain.FollowMouse,
                Tuning = chain.Tuning.ToPort(m),
                MaceIndex = chain.Mace,
                MinMaceIndex = chain.MaceMin,
                MaxMaceIndex = chain.MaceMax,
                MaceMass = chain.LinkMass,
                HitRadius = chain.Port.HitRadius,
                DamagePerSpeed = chain.Port.DamagePerSpeed,
                CatchRadius = chain.Port.CatchRadius,
                ThrowMultiplier = chain.Port.ThrowMultiplier,
                ContactRadius = angel.Port.ContactRadius,
            };

            var seekRate = m.PerSecond(enemies.SeekSpd);
            World = new GameWorld.Settings
            {
                Session = new GameState.Settings
                {
                    MaxHp = angel.MaxHP,
                    HurtDelay = angel.HurtDelay,
                    ContactDamage = Game.ContactDamage,
                    PointsPerDamage = Game.PointsPerDamage,
                },
                Enemies = new GameWorld.EnemySetup
                {
                    // Enemy.vb's enemies are 0..maxEnemy.
                    Count = enemies.MaxEnemy + 1,
                    Hp = enemies.ResetHP,
                    LoadHp = enemies.LoadHP,
                    MassMin = enemies.MassBase,
                    MassMax = enemies.MassBase + enemies.MassRandom,
                    Radius = enemies.Port.Radius,
                    // checkFlocking visits each overlapping pair twice a frame.
                    FlockSpeed = m.Speed(enemies.FlockSpd) * EnemyBrain.OriginalFlockPairVisits,
                    Brain = new EnemyBrain.Settings
                    {
                        SeekRate = seekRate,
                        HurtDeceleration = m.Acceleration(enemies.ResistSPD),
                        // A_near_B(dX, 0, resistSPD): it recovers once both axes are within one step of still.
                        RecoverSpeed = m.Speed(enemies.ResistSPD),
                        // Cautious's velocity is 6 x seekSpd x distance a frame: a rate like seekSpd's, no length in it.
                        CautiousRate = enemies.CautiousSeekMultiple * seekRate,
                        // Align's is a flat seekSpd a frame on each axis: a speed.
                        AlignSpeed = m.Speed(enemies.SeekSpd),
                        HangBackNear = m.Length(enemies.Port.HangBackNear),
                        HangBackFar = m.Length(enemies.Port.HangBackFar),
                    },
                    SeekCap = enemies.Port.SeekCap,
                },
                GameSpeed = Game.GameSpeed,
                // Input.vb's 0.01 a frame while Minus or Equals is held: a rate with no length in it.
                GameSpeedRate = m.PerSecond(Game.GameSpeedStep),
            };

            Heart = new HeartFloat.Settings
            {
                ItemSize = m.Length(item.ItemSize),
                // HeartFloat steps once per original frame, so its speeds stay per frame: only the length maps.
                FallRate = m.Length(item.FallRate),
                FloatMaxSpeed = m.Length(item.FloatMaxSPD),
                FloatAcceleration = m.Length(item.FloatAcc),
                StartFloatSpeed = m.Length(item.FloatSPD),
                ResetFloatSpeed = m.Length(item.ResetFloatSPD),
                StartX = m.Length(item.Start.X),
                StartY = m.Length(item.Start.Y),
                WrapBelow = m.Length(item.WrapBelow),
                WrapTo = m.Length(item.WrapTo),
                HealAmount = item.Heal,
                AngelSize = m.Length(angel.AngelSize),
                RectTopPad = m.Length(item.RectTopPad),
                RectWidth = m.Length(item.RectWidth),
                RectBottom = m.Length(item.RectBottom),
                AngelRectWidth = m.Length(angel.RectWidth),
                AngelRectBottom = m.Length(angel.RectBottom),
            };

            // The world holds the hearts (it moves them, checks them and resets them with everything else).
            var world = World;
            world.Heart = Heart;
            World = world;

            Light =new LightingState.Settings
            {
                DayLength = Lighting.DayLength,
                TintRate = m.PerSecond(Lighting.TintStep),
            };

            CameraTuning = Camera.Port;
            ShakeAmount = m.Length(Camera.ShakeAmount);
            DebugCameraSettings = new DebugCamera.Settings(
                fov: Camera.Fov,
                aspect: Camera.Aspect,
                eyeZ: m.Length(Camera.CamZ),
                stepSpeed: m.Speed(Camera.CamSpd),
                liftSpeed: m.Speed(Camera.MoveStep),
                isometricSpeed: m.Speed(Camera.IsometricStep),
                minDistance: m.Length(Camera.MinDistance));
            AudioTuning = new AudioTuning(
                sfxVolume: AudioData.Linear(Audio.SfxVolume),
                musicVolume: AudioData.Linear(Audio.MusicVolume),
                randomMusicStart: Audio.RndMusicPos,
                port: Audio.Port);
            MaceScale = chain.MaceScale;
            Steering = new SteerInput.Settings(
                joystickEnabled: Input.BJoystick,
                deadZone: Input.JoyOutsideDeadZone / Input.JoyRange,
                stickSpeed: Input.Port.StickSpeed);
            Zones = HitZones.Settings.From(Hud.Zone, m);
            Backdrop = Core.Backdrop.Settings.From(Area, m);
        }
    }

    /// <summary>space.json: the original-to-port mapping (<see cref="OriginalSpace"/>).</summary>
    public sealed class SpaceData
    {
        public float LengthScale { get; private set; }
        public float FramesPerSecond { get; private set; }

        internal static SpaceData Read(SettingsReader r) => new SpaceData
        {
            LengthScale = r.Positive("lengthScale"),
            FramesPerSecond = r.Positive("framesPerSecond"),
        };
    }

    /// <summary>chain.json: Chain.vb, in its own units (screen space, per frame), with the follow flag (UNI-T47).</summary>
    public sealed class ChainData
    {
        public int MaxLinks { get; private set; }
        public float SegLength { get; private set; }
        public float SpringK { get; private set; }
        public float Resistance { get; private set; }
        public Vec2 Gravity { get; private set; }
        public float StopVel { get; private set; }
        public float StopAcc { get; private set; }
        public float OverheadBoost { get; private set; }
        public float LinkSize { get; private set; }
        public float LinkMass { get; private set; }
        public int Mace { get; private set; }
        public int MaceMin { get; private set; }
        public int MaceMax { get; private set; }
        public bool FollowMouse { get; private set; }
        public float MaceScale { get; private set; }

        /// <summary>Form1.vb's live-tuning keys: steps and limits, in the file's own units (UNI-T51).</summary>
        public ChainTuningData Tuning { get; private set; }

        public ChainPort Port { get; private set; }

        internal static ChainData Read(SettingsReader r)
        {
            var data = new ChainData
            {
                MaxLinks = r.Integer("maxLinks", 1, 1000),
                SegLength = r.Number("segLength", 0f),
                SpringK = r.Positive("springK"),
                Resistance = r.Number("resistance", 0f),
                Gravity = r.Pair("gravity"),
                StopVel = r.Number("stopVel", 0f),
                StopAcc = r.Number("stopAcc", 0f),
                OverheadBoost = r.Number("overheadBoost", 1f),
                LinkSize = r.Number("linkSize", 0f),
                LinkMass = r.Positive("linkMass"),
                Mace = r.Integer("Mace", 1),
                MaceMin = r.Integer("maceMin", 1),
                MaceMax = r.Integer("maceMax", 1),
                FollowMouse = r.Flag("followMouse"),
                MaceScale = r.Positive("maceScale"),
            };

            r.Check(data.MaceMin <= data.MaceMax, "maceMin", $"must not be above maceMax ({data.MaceMax})");
            r.Check(data.MaceMax <= data.MaxLinks, "maceMax", $"must not be above maxLinks ({data.MaxLinks})");
            r.Check(data.Mace >= data.MaceMin && data.Mace <= data.MaceMax, "Mace", $"must be between maceMin and maceMax ({data.MaceMin}-{data.MaceMax})");

            var tuning = r.Section("tuning");
            data.Tuning = ChainTuningData.Read(tuning);
            var t = data.Tuning;
            tuning.Check(t.ResistanceMin < t.ResistanceMax, "resistanceMax", $"must be above resistanceMin ({t.ResistanceMin})");
            tuning.Check(t.GravityMin < t.GravityMax, "gravityMax", $"must be above gravityMin ({t.GravityMin})");
            tuning.Check(t.SpringMin < t.SpringMax, "springMax", $"must be above springMin ({t.SpringMin})");
            tuning.Done();

            var port = r.Section("port");
            data.Port = new ChainPort(
                keepInArena: port.Flag("keepInArena"),
                wallBounce: port.Number("wallBounce", 0f, 1f),
                hitRadius: port.Number("hitRadius", 0f),
                damagePerSpeed: port.Number("damagePerSpeed", 0f),
                catchRadius: port.Number("catchRadius", 0f),
                throwMultiplier: port.Number("throwMultiplier", 0f));
            port.Done();
            return data;
        }
    }

    /// <summary>
    /// chain.json's <c>tuning</c>: Form1.vb's live-tuning keys. The steps are the original's (Insert/Delete
    /// <c>changeResistance(+/-0.001)</c>, Home/End <c>changeGravity(-/+0.00001)</c>, PageUp/PageDown
    /// <c>changeSpring(+/-0.001)</c>); the limits are the port's (the original had none, BIBLE V19). All in the
    /// file's own units, as the HUD's Res/Grav/Spring readout shows them, and mapped like the figures they move.
    /// </summary>
    public sealed class ChainTuningData
    {
        public float ResistanceStep { get; private set; }
        public float ResistanceMin { get; private set; }
        public float ResistanceMax { get; private set; }
        public float GravityStep { get; private set; }
        public float GravityMin { get; private set; }
        public float GravityMax { get; private set; }
        public float SpringStep { get; private set; }
        public float SpringMin { get; private set; }
        public float SpringMax { get; private set; }

        internal static ChainTuningData Read(SettingsReader r) => new ChainTuningData
        {
            ResistanceStep = r.Positive("resistanceStep"),
            ResistanceMin = r.Number("resistanceMin", 0f),
            ResistanceMax = r.Number("resistanceMax", 0f),
            GravityStep = r.Positive("gravityStep"),
            GravityMin = r.Number("gravityMin"),
            GravityMax = r.Number("gravityMax"),
            SpringStep = r.Positive("springStep"),
            SpringMin = r.Positive("springMin"),
            SpringMax = r.Positive("springMax"),
        };

        /// <summary>These figures in the port's units (<paramref name="space"/>).</summary>
        public ChainTuning ToPort(OriginalSpace space) => ChainTuning.From(
            space,
            ResistanceStep, ResistanceMin, ResistanceMax,
            GravityStep, GravityMin, GravityMax,
            SpringStep, SpringMin, SpringMax);
    }

    /// <summary>chain.json's port-only figures, world units and seconds.</summary>
    public readonly struct ChainPort
    {
        public ChainPort(bool keepInArena, float wallBounce, float hitRadius, float damagePerSpeed, float catchRadius, float throwMultiplier)
        {
            KeepInArena = keepInArena;
            WallBounce = wallBounce;
            HitRadius = hitRadius;
            DamagePerSpeed = damagePerSpeed;
            CatchRadius = catchRadius;
            ThrowMultiplier = throwMultiplier;
        }

        /// <summary>Links bounce off the arena (V4; Chain.vb's checkBounds is commented out).</summary>
        public bool KeepInArena { get; }
        public float WallBounce { get; }
        /// <summary>The mace's hit circle (D12: a swept circle for Chain.vb's rectMace).</summary>
        public float HitRadius { get; }
        /// <summary>Damage per world unit per second of mace speed (D11: speed for abs(dX + dY/2)).</summary>
        public float DamagePerSpeed { get; }
        public float CatchRadius { get; }
        /// <summary>Launch velocity = mace velocity x this (the original throws at the mace's velocity: 1).</summary>
        public float ThrowMultiplier { get; }
    }

    /// <summary>angel.json: Angel.vb, in its own units.</summary>
    public sealed class AngelData
    {
        public float Ratio3D { get; private set; }
        public float HoverMax { get; private set; }
        public float HoverRate { get; private set; }
        public float FlapRate { get; private set; }
        public int FlapFrames { get; private set; }
        public int MaxHP { get; private set; }
        public float HurtDelay { get; private set; }
        public float AngelSize { get; private set; }
        public float RectWidth { get; private set; }
        public float RectBottom { get; private set; }
        public AngelPort Port { get; private set; }

        internal static AngelData Read(SettingsReader r)
        {
            var data = new AngelData
            {
                Ratio3D = r.Positive("Ratio3D"),
                HoverMax = r.Number("hoverMax", 0f),
                HoverRate = r.Positive("hoverRate"),
                FlapRate = r.Positive("flapRate"),
                FlapFrames = r.Integer("flapFrames", 1, 64),
                MaxHP = r.Integer("maxHP", 1),
                HurtDelay = r.Number("hurtDelay", 0f),
                AngelSize = r.Positive("angelSize"),
                RectWidth = r.Positive("rectWidth"),
                RectBottom = r.Number("rectBottom"),
            };

            var port = r.Section("port");
            data.Port = new AngelPort(
                handOffset: port.Pair("handOffset"),
                boundsMargin: port.Pair("boundsMargin", 0f),
                contactRadius: port.Number("contactRadius", 0f));
            port.Done();
            return data;
        }
    }

    /// <summary>angel.json's port-only figures, world units.</summary>
    public readonly struct AngelPort
    {
        public AngelPort(Vec2 handOffset, Vec2 boundsMargin, float contactRadius)
        {
            HandOffset = handOffset;
            BoundsMargin = boundsMargin;
            ContactRadius = contactRadius;
        }

        /// <summary>Where link 0 hangs from the body's centre facing right (the port's sprites are centred).</summary>
        public Vec2 HandOffset { get; }
        /// <summary>The angel's flight is kept this far inside the arena's edges.</summary>
        public Vec2 BoundsMargin { get; }
        /// <summary>The angel's contact circle (V5: a circle for Angel.vb's rectangle).</summary>
        public float ContactRadius { get; }
    }

    /// <summary>enemies.json: Enemy.vb, in its own units, with the Seek cap for Cautious and Align (UNI-T65).</summary>
    public sealed class EnemyData
    {
        public int MaxEnemy { get; private set; }
        public float SeekSpd { get; private set; }
        public float ResistSPD { get; private set; }
        public float FlockSpd { get; private set; }
        public float MassBase { get; private set; }
        public float MassRandom { get; private set; }
        public float LoadHP { get; private set; }
        public float ResetHP { get; private set; }
        public float CautiousSeekMultiple { get; private set; }
        public EnemyPort Port { get; private set; }

        internal static EnemyData Read(SettingsReader r)
        {
            var data = new EnemyData
            {
                MaxEnemy = r.Integer("maxEnemy", -1, 1000),
                SeekSpd = r.Number("seekSpd", 0f),
                ResistSPD = r.Number("resistSPD", 0f),
                FlockSpd = r.Number("flockSpd", 0f),
                MassBase = r.Positive("massBase"),
                MassRandom = r.Number("massRandom", 0f),
                LoadHP = r.Positive("loadHP"),
                ResetHP = r.Positive("resetHP"),
                CautiousSeekMultiple = r.Number("cautiousSeekMultiple", 0f),
            };

            var port = r.Section("port");
            data.Port = new EnemyPort(
                radius: port.Positive("radius"),
                seekCap: port.Integer("seekCap", 0),
                hangBackNear: port.Positive("hangBackNear"),
                hangBackFar: port.Positive("hangBackFar"));
            port.Check(data.Port.SeekCap <= data.MaxEnemy + 1, "seekCap", $"must not be above the enemy count, maxEnemy + 1 ({data.MaxEnemy + 1})");
            port.Check(data.Port.HangBackFar > data.Port.HangBackNear, "hangBackFar", $"must be beyond hangBackNear ({data.Port.HangBackNear})");
            port.Done();
            return data;
        }
    }

    /// <summary>enemies.json's port-only figures.</summary>
    public readonly struct EnemyPort
    {
        public EnemyPort(float radius, int seekCap, float hangBackNear, float hangBackFar)
        {
            Radius = radius;
            SeekCap = seekCap;
            HangBackNear = hangBackNear;
            HangBackFar = hangBackFar;
        }

        /// <summary>An enemy's collision circle, world units.</summary>
        public float Radius { get; }
        /// <summary>How many enemies may Seek at once (UNI-T65); the enemy count lets every one seek, as the original does.</summary>
        public int SeekCap { get; }
        /// <summary>A waiting enemy stalking in (Align) turns Cautious this close to the angel, screen units (mapped).</summary>
        public float HangBackNear { get; }
        /// <summary>A Cautious enemy backing off turns to Align this far from the angel, screen units (mapped).</summary>
        public float HangBackFar { get; }
    }

    /// <summary>item.json: Item.vb's Health10 heart, in its own units.</summary>
    public sealed class ItemData
    {
        public float ItemSize { get; private set; }
        public float FallRate { get; private set; }
        public float FloatMaxSPD { get; private set; }
        public float FloatAcc { get; private set; }
        public float FloatSPD { get; private set; }
        public float ResetFloatSPD { get; private set; }
        public Vec2 Start { get; private set; }
        public float WrapBelow { get; private set; }
        public float WrapTo { get; private set; }
        public int Heal { get; private set; }
        public float RectTopPad { get; private set; }
        public float RectWidth { get; private set; }
        public float RectBottom { get; private set; }

        internal static ItemData Read(SettingsReader r)
        {
            var data = new ItemData
            {
                ItemSize = r.Positive("itemSize"),
                FallRate = r.Number("fallRate", 0f),
                FloatMaxSPD = r.Number("floatMaxSPD", 0f),
                FloatAcc = r.Number("floatAcc", 0f),
                FloatSPD = r.Number("floatSPD", 0f),
                ResetFloatSPD = r.Number("resetFloatSPD", 0f),
                Start = r.Pair("start"),
                WrapBelow = r.Number("wrapBelow"),
                WrapTo = r.Number("wrapTo"),
                Heal = r.Integer("heal", 0),
                RectTopPad = r.Number("rectTopPad"),
                RectWidth = r.Positive("rectWidth"),
                RectBottom = r.Number("rectBottom"),
            };

            r.Check(data.WrapBelow < data.WrapTo, "wrapBelow", $"must be below wrapTo ({data.WrapTo})");
            return data;
        }
    }

    /// <summary>game.json: Engine.vb's session rules, the game speed Root.vb's Reset starts a round at and Input.vb's Minus/Equals step.</summary>
    public sealed class GameData
    {
        public float PointsPerDamage { get; private set; }
        public int ContactDamage { get; private set; }
        /// <summary>Root.vb's <c>gameSpeed</c>: 1.0, set again by every Reset.</summary>
        public float GameSpeed { get; private set; }

        /// <summary>Input.vb's Minus/Equals: <c>gameSpeed -= 0.01</c> / <c>+= 0.01</c> each frame the key is held (UNI-T52).</summary>
        public float GameSpeedStep { get; private set; }

        internal static GameData Read(SettingsReader r) => new GameData
        {
            PointsPerDamage = r.Number("pointsPerDamage", 0f),
            ContactDamage = r.Integer("contactDamage", 0),
            GameSpeed = r.Number("gameSpeed", 0f),
            GameSpeedStep = r.Positive("gameSpeedStep"),
        };
    }

    /// <summary>lighting.json: Lights.vb, with the playable day length (BIBLE D6).</summary>
    public sealed class LightingData
    {
        public float DayLength { get; private set; }
        public float TintStep { get; private set; }

        internal static LightingData Read(SettingsReader r) => new LightingData
        {
            DayLength = r.Number("dayLength", 1f),
            TintStep = r.Number("tintStep", 0f),
        };
    }

    /// <summary>camera.json: Camera.vb's perspective (kept as data for the debug camera, UNI-T57) and the port's rig.</summary>
    public sealed class CameraData
    {
        /// <summary>Camera.vb's <c>Fov</c>, radians (pi / 4).</summary>
        public float Fov { get; private set; }

        /// <summary>Camera.vb's <c>PerspectiveFovLH</c> aspect (4 / 3).</summary>
        public float Aspect { get; private set; }

        /// <summary>Camera.vb's <c>cam.Z</c> (-1), screen units; the target is on z 0.</summary>
        public float CamZ { get; private set; }

        /// <summary>Chain.vb's <c>camSPD</c> (0.012): numpad plus/Enter and 4/6, screen units a frame.</summary>
        public float CamSpd { get; private set; }

        /// <summary>Camera.vb's <c>move_up</c>/<c>move_down</c> (0.02): numpad 8/2, screen units a frame.</summary>
        public float MoveStep { get; private set; }

        /// <summary>Camera.vb's <c>isometric</c>/<c>deisometric</c> (0.25): X/Z, screen units a frame.</summary>
        public float IsometricStep { get; private set; }

        /// <summary>The port's nearest approach of the debug eye to the plane, screen units (BIBLE D26).</summary>
        public float MinDistance { get; private set; }

        /// <summary>Form1.vb's 0 key: <c>shakeScreen(0.02)</c>, the most one draw moves the look target, screen units.</summary>
        public float ShakeAmount { get; private set; }

        public CameraTuning Port { get; private set; }

        internal static CameraData Read(SettingsReader r)
        {
            var data = new CameraData
            {
                Fov = r.Number("Fov", 0.01f, 3.1f),
                Aspect = r.Number("aspect", 0.01f),
                CamZ = r.Number("camZ", max: -0.0001f),
                CamSpd = r.Number("camSPD", 0f),
                MoveStep = r.Number("moveStep", 0f),
                IsometricStep = r.Number("isometricStep", 0f),
                ShakeAmount = r.Number("shakeAmount", 0f),
            };

            var port = r.Section("port");
            data.Port = new CameraTuning(
                mode: port.Choice<CameraFramingMode>("mode"),
                padding: port.Number("padding", 0f),
                followSize: port.Number("followSize", 0.01f),
                deadZone: port.Pair("deadZone", 0f),
                sharpness: port.Number("sharpness", 0f));
            data.MinDistance = port.Number("minDistance", 0.0001f);
            port.Done();
            return data;
        }
    }

    /// <summary>The camera rig's framing (the port's; the original's camera never moved in play), world units.</summary>
    public readonly struct CameraTuning
    {
        public CameraTuning(CameraFramingMode mode, float padding, float followSize, Vec2 deadZone, float sharpness)
        {
            Mode = mode;
            Padding = padding;
            FollowSize = followSize;
            DeadZone = deadZone;
            Sharpness = sharpness;
        }

        public CameraFramingMode Mode { get; }
        public float Padding { get; }
        public float FollowSize { get; }
        public Vec2 DeadZone { get; }
        public float Sharpness { get; }
    }

    /// <summary>audio.json: Sound.vb's volumes (hundredths of a decibel, DirectSound's scale) and the port's swing sound.</summary>
    public sealed class AudioData
    {
        public int MusicVolume { get; private set; }
        public int SfxVolume { get; private set; }
        public int AmbientVolume { get; private set; }
        public bool RndMusicPos { get; private set; }
        public AudioPort Port { get; private set; }

        /// <summary>A DirectSound volume (0 full, -10000 silent, hundredths of a dB) as a 0-1 gain.</summary>
        public static float Linear(int hundredthsOfDb) => hundredthsOfDb <= -10000 ? 0f : (float)Math.Pow(10d, hundredthsOfDb / 2000d);

        internal static AudioData Read(SettingsReader r)
        {
            var data = new AudioData
            {
                MusicVolume = r.Integer("musicVolume", -10000, 0),
                SfxVolume = r.Integer("sfxVolume", -10000, 0),
                AmbientVolume = r.Integer("ambientVolume", -10000, 0),
                RndMusicPos = r.Flag("rndMusicPos"),
            };

            var port = r.Section("port");
            data.Port = new AudioPort(
                swingSpeed: port.Number("swingSpeed", 0f),
                swingRearmSpeed: port.Number("swingRearmSpeed", 0f),
                swingCooldown: port.Number("swingCooldown", 0f),
                swingRepeatInterval: port.Number("swingRepeatInterval", 0f),
                fullVolumeSpeed: port.Number("fullVolumeSpeed", 0f),
                minSpeedVolume: port.Number("minSpeedVolume", 0f, 1f));
            port.Check(data.Port.SwingRearmSpeed <= data.Port.SwingSpeed, "swingRearmSpeed", $"must not be above swingSpeed ({data.Port.SwingSpeed})");
            port.Done();
            return data;
        }
    }

    /// <summary>audio.json's port-only figures: when the swing whoosh fires (V10), world units per second.</summary>
    public readonly struct AudioPort
    {
        public AudioPort(float swingSpeed, float swingRearmSpeed, float swingCooldown, float swingRepeatInterval, float fullVolumeSpeed, float minSpeedVolume)
        {
            SwingSpeed = swingSpeed;
            SwingRearmSpeed = swingRearmSpeed;
            SwingCooldown = swingCooldown;
            SwingRepeatInterval = swingRepeatInterval;
            FullVolumeSpeed = fullVolumeSpeed;
            MinSpeedVolume = minSpeedVolume;
        }

        public float SwingSpeed { get; }
        public float SwingRearmSpeed { get; }
        public float SwingCooldown { get; }
        public float SwingRepeatInterval { get; }
        public float FullVolumeSpeed { get; }
        public float MinSpeedVolume { get; }
    }

    /// <summary>The audio the port plays with: Sound.vb's volumes as gains, and the swing sound's tuning.</summary>
    public readonly struct AudioTuning
    {
        public AudioTuning(float sfxVolume, float musicVolume, bool randomMusicStart, AudioPort port)
        {
            SfxVolume = sfxVolume;
            MusicVolume = musicVolume;
            RandomMusicStart = randomMusicStart;
            Swing = port;
        }

        /// <summary>sfxVolume -1000 is -10 dB, a gain of about 0.32.</summary>
        public float SfxVolume { get; }
        public float MusicVolume { get; }
        /// <summary>Sound.vb's rndMusicPos: the music starts at a random point.</summary>
        public bool RandomMusicStart { get; }
        public AudioPort Swing { get; }
    }

    /// <summary>input.json: Input.vb's joystick (its range and dead zone, in its own units) and the port's stick speed.</summary>
    public sealed class InputData
    {
        public bool BJoystick { get; private set; }
        public float JoyRange { get; private set; }
        public float JoyOutsideDeadZone { get; private set; }
        public InputPort Port { get; private set; }

        internal static InputData Read(SettingsReader r)
        {
            var data = new InputData
            {
                BJoystick = r.Flag("bJoystick"),
                JoyRange = r.Positive("joyRange"),
                JoyOutsideDeadZone = r.Number("joyOutsideDeadZone", 0f),
            };

            r.Check(data.JoyOutsideDeadZone < data.JoyRange, "joyOutsideDeadZone", $"must be below joyRange ({data.JoyRange})");

            var port = r.Section("port");
            data.Port = new InputPort(stickSpeed: port.Number("stickSpeed", 0f));
            port.Done();
            return data;
        }
    }

    /// <summary>input.json's port-only figures.</summary>
    public readonly struct InputPort
    {
        public InputPort(float stickSpeed)
        {
            StickSpeed = stickSpeed;
        }

        /// <summary>Mouse counts a second a fully pushed stick steers by (the original bound the stick to nothing).</summary>
        public float StickSpeed { get; }
    }
}
