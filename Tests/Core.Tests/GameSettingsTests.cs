using System;
using System.IO;
using System.Linq;
using MindAttic.Chain.Core;
using NUnit.Framework;
using StringAssert = NUnit.Framework.Legacy.StringAssert;

namespace MindAttic.Chain.Tests
{
    /// <summary>
    /// The settings registry (UNI-T33): the shipped data files in Assets/StreamingAssets/Settings load and hold the
    /// original's figures in its own units, the one mapping turns them into the port's, and bad files are rejected
    /// with errors that name the file, the key and the line.
    /// </summary>
    public class GameSettingsTests
    {
        internal static string ShippedFolder
        {
            get
            {
                var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
                while (dir != null)
                {
                    var candidate = Path.Combine(dir.FullName, "Assets", "StreamingAssets", GameSettings.Folder);
                    if (Directory.Exists(candidate))
                    {
                        return candidate;
                    }

                    dir = dir.Parent;
                }

                throw new DirectoryNotFoundException("Assets/StreamingAssets/Settings not found above the test folder.");
            }
        }

        private static ISettingsSource Shipped => new DirectorySettingsSource(ShippedFolder);

        private static GameSettings Load() => GameSettings.Load(Shipped);

        /// <summary>The shipped files with one file's text changed.</summary>
        private static MemorySettingsSource Patched(string file, Func<string, string> change) =>
            new MemorySettingsSource(Shipped).Set(file, change(Shipped.Read(file)));

        /// <summary>The shipped files at another length scale (1 keeps the original's own figures).</summary>
        private static GameSettings AtScale(float scale) =>
            GameSettings.Load(Patched(GameSettings.SpaceFile, text => text.Replace("\"lengthScale\": 9", "\"lengthScale\": " + scale.ToString(System.Globalization.CultureInfo.InvariantCulture))));

        private static SettingsException LoadFails(ISettingsSource source) =>
            Assert.Throws<SettingsException>(() => GameSettings.Load(source));

        // --- The shipped files hold the original's figures --------------------------------------

        [Test]
        public void ShippedFiles_LoadWithoutErrors()
        {
            Assert.DoesNotThrow(() => Load());
        }

        [Test]
        public void EveryFile_IsShipped()
        {
            foreach (var file in GameSettings.Files)
            {
                Assert.IsTrue(File.Exists(Path.Combine(ShippedFolder, file)), file);
            }
        }

        [Test]
        public void Chain_HoldsChainVb()
        {
            var chain = Load().Chain;

            Assert.AreEqual(25, chain.MaxLinks);
            Assert.AreEqual(0.002f, chain.SegLength);
            Assert.AreEqual(0.1f, chain.SpringK);
            Assert.AreEqual(0.25f, chain.Resistance);
            Assert.AreEqual(new Vec2(0f, -0.0025f), chain.Gravity);
            Assert.AreEqual(1e-7f, chain.StopVel);
            Assert.AreEqual(1e-7f, chain.StopAcc);
            Assert.AreEqual(2f, chain.OverheadBoost);
            Assert.AreEqual(0.025f, chain.LinkSize);
            Assert.AreEqual(6, chain.Mace);
            Assert.AreEqual(1, chain.MaceMin);
            Assert.AreEqual(12, chain.MaceMax);
            Assert.IsTrue(chain.FollowMouse);
            Assert.AreEqual(ChainFollow.MaceScale, chain.MaceScale);
        }

        [Test]
        public void Angel_HoldsAngelVb()
        {
            var angel = Load().Angel;

            Assert.AreEqual(0.00171f, angel.Ratio3D);
            Assert.AreEqual(0.01f, angel.HoverMax);
            Assert.AreEqual(0.001f, angel.HoverRate);
            Assert.AreEqual(0.1f, angel.FlapRate);
            Assert.AreEqual(4, angel.FlapFrames);
            Assert.AreEqual(30, angel.MaxHP);
            Assert.AreEqual(0.25f, angel.HurtDelay);
        }

        [Test]
        public void Enemies_HoldEnemyVb()
        {
            var enemies = Load().Enemies;

            Assert.AreEqual(13, enemies.MaxEnemy);
            Assert.AreEqual(0.006f, enemies.SeekSpd);
            Assert.AreEqual(0.001f, enemies.ResistSPD);
            Assert.AreEqual(0.0005f, enemies.FlockSpd);
            Assert.AreEqual(1f, enemies.MassBase);
            Assert.AreEqual(1f, enemies.MassRandom);
            Assert.AreEqual(0.2f, enemies.LoadHP);
            Assert.AreEqual(1f, enemies.ResetHP);
            Assert.AreEqual(6f, enemies.CautiousSeekMultiple);
            Assert.AreEqual(4, enemies.Port.SeekCap, "UNI-T65: four seek at once, the other ten wait");
            Assert.AreEqual(0.3f, enemies.Port.HangBackNear);
            Assert.AreEqual(0.45f, enemies.Port.HangBackFar);
        }

        [Test]
        public void Item_HoldsItemVb()
        {
            var item = Load().Item;

            Assert.AreEqual(0.05f, item.ItemSize);
            Assert.AreEqual(0.001f, item.FallRate);
            Assert.AreEqual(0.001f, item.FloatMaxSPD);
            Assert.AreEqual(0.00001f, item.FloatAcc);
            Assert.AreEqual(0.002f, item.FloatSPD);
            Assert.AreEqual(0.001f, item.ResetFloatSPD);
            Assert.AreEqual(new Vec2(0f, 0.4f), item.Start);
            Assert.AreEqual(10, item.Heal);
        }

        [Test]
        public void Lighting_DayIsTheOriginalsClock()
        {
            var settings = Load();

            Assert.AreEqual(1440f, settings.Light.DayLength, "D6, UNI-T46");
            Assert.AreEqual(60f, settings.Light.TintRate, "1 a frame at 60 fps");
        }

        [Test]
        public void Audio_VolumesAreSoundVbs()
        {
            var settings = Load();

            Assert.AreEqual(-1000, settings.Audio.SfxVolume);
            Assert.AreEqual(0, settings.Audio.MusicVolume);
            Assert.AreEqual(0.316f, settings.AudioTuning.SfxVolume, 0.001f, "-10 dB");
            Assert.AreEqual(1f, settings.AudioTuning.MusicVolume);
            Assert.IsTrue(settings.AudioTuning.RandomMusicStart);
        }

        [Test]
        public void Input_HoldsInputVbsJoystick()
        {
            var settings = Load();

            Assert.IsTrue(settings.Input.BJoystick);
            Assert.AreEqual(1000f, settings.Input.JoyRange, "sfxVolume..+1000");
            Assert.AreEqual(250f, settings.Input.JoyOutsideDeadZone);
            Assert.IsTrue(settings.Steering.JoystickEnabled);
            Assert.AreEqual(0.25f, settings.Steering.DeadZone, 1e-6f, "250 of 1000");
            Assert.AreEqual(600f, settings.Steering.StickSpeed);
        }

        [Test]
        public void Input_DeadZoneMustBeInsideTheRange()
        {
            var e = LoadFails(Patched(GameSettings.InputFile, text => text.Replace("\"joyOutsideDeadZone\": 250", "\"joyOutsideDeadZone\": 1000")));

            StringAssert.Contains("input.json: 'joyOutsideDeadZone' must be below joyRange (1000)", e.Message);
        }

        // --- The mapping ------------------------------------------------------------------------

        [Test]
        public void Space_IsTheScreenHeightToTheArenaHeightAtSixtyFrames()
        {
            var mapping = Load().Mapping;

            Assert.AreEqual(9f, mapping.LengthScale, "screen height 1.0 is the 9 unit arena height");
            Assert.AreEqual(OriginalSpace.ArenaLengthScale, mapping.LengthScale);
            Assert.AreEqual(GameWorld.DefaultArena.Height, mapping.Length(1f), 1e-6f);
            Assert.AreEqual(60f, mapping.FramesPerSecond);
        }

        [Test]
        public void Mapping_ConvertsEachKindOfQuantity()
        {
            var m = new OriginalSpace(9f, 60f);

            Assert.AreEqual(0.45f, m.Length(0.05f), 1e-6f);
            Assert.AreEqual(0.54f, m.Speed(0.001f), 1e-6f);
            Assert.AreEqual(32.4f, m.Acceleration(0.001f), 1e-4f);
            Assert.AreEqual(0.36f, m.PerSecond(0.006f), 1e-6f);
            Assert.AreEqual(360f, m.PerSecondSquared(0.1f), 1e-3f);
            Assert.AreEqual(0.5f, m.Seconds(30f), 1e-6f);
        }

        [Test]
        public void Shipped_MapsToThePortsUnits()
        {
            var settings = AtScale(1f);
            var player = settings.Player;
            var world = settings.World;

            Assert.AreEqual(26, player.LinkCount, "links 0..maxLinks");
            Assert.AreEqual(0.002f, player.Chain.SegmentLength, 1e-7f);
            Assert.AreEqual(360f, player.Chain.SpringConstant, 1e-3f, "0.1 a frame squared");
            Assert.AreEqual(15f, player.Chain.Resistance, 1e-4f, "0.25 a frame");
            Assert.AreEqual(-9f, player.Chain.Gravity.Y, 1e-4f, "-0.0025 a frame squared");
            Assert.AreEqual(0.025f, player.Chain.OverheadMargin, 1e-7f, "one link size");
            Assert.AreEqual(2f, player.Chain.OverheadBoost);
            Assert.AreEqual(6, player.MaceIndex);
            Assert.AreEqual(0.00171f, player.Flight.Sensitivity, 1e-7f);
            Assert.AreEqual(0.01f, player.Flight.HoverAmplitude, 1e-7f);
            Assert.AreEqual(40f / 60f, player.Flight.HoverPeriod, 1e-5f, "+/-0.01 at 0.001 a frame is 40 frames");
            Assert.AreEqual(0.1f, player.Flight.FrameDuration);

            Assert.AreEqual(14, world.Enemies.Count, "enemies 0..maxEnemy");
            Assert.AreEqual(1f, world.Enemies.MassMin);
            Assert.AreEqual(2f, world.Enemies.MassMax);
            Assert.AreEqual(1f, world.Enemies.Hp);
            Assert.AreEqual(0.2f, world.Enemies.LoadHp, "loadEnemy's weaker first-wave HP, UNI-T70");
            Assert.AreEqual(0.36f, world.Enemies.Brain.SeekRate, 1e-6f);
            Assert.AreEqual(EnemyBrain.DefaultFlockSpeed, Load().World.Enemies.FlockSpeed, 1e-5f, "UNI-T35's 0.06 x 9");
            Assert.AreEqual(0.06f, world.Enemies.FlockSpeed, 1e-6f, "UNI-T35's 0.06 at scale 1");
            Assert.AreEqual(3.6f, world.Enemies.Brain.HurtDeceleration, 1e-4f, "resistSPD 0.001 a frame squared");
            Assert.AreEqual(0.06f, world.Enemies.Brain.RecoverSpeed, 1e-6f);
            Assert.AreEqual(2.16f, world.Enemies.Brain.CautiousRate, 1e-5f, "6 x seekSpd a frame");
            Assert.AreEqual(0.36f, world.Enemies.Brain.AlignSpeed, 1e-6f, "seekSpd a frame");
            Assert.AreEqual(0.3f, world.Enemies.Brain.HangBackNear, 1e-6f);
            Assert.AreEqual(0.45f, world.Enemies.Brain.HangBackFar, 1e-6f);
            Assert.AreEqual(4, world.Enemies.SeekCap);
            Assert.AreEqual(30, world.Session.MaxHp);
            Assert.AreEqual(0.25f, world.Session.HurtDelay);
            Assert.AreEqual(1000f, world.Session.PointsPerDamage);
        }

        [Test]
        public void Shipped_ShakeAmount_IsForm1sZeroKeyMapped()
        {
            Assert.AreEqual(0.02f, Load().Camera.ShakeAmount, 1e-7f, "Form1.vb: shakeScreen(0.02)");
            Assert.AreEqual(0.02f, AtScale(1f).ShakeAmount, 1e-7f);
            Assert.AreEqual(0.18f, Load().ShakeAmount, 1e-6f, "0.02 of the screen x 9");
        }

        [Test]
        public void Shipped_Camera_IsCameraVbsPerspectiveAndDebugKeys()
        {
            var camera = Load().Camera;
            Assert.AreEqual(System.MathF.PI / 4f, camera.Fov, 1e-6f, "Camera.vb: Fov = Math.PI / 4");
            Assert.AreEqual(4f / 3f, camera.Aspect, 1e-6f, "PerspectiveFovLH(Fov, 4 / 3, ...)");
            Assert.AreEqual(-1f, camera.CamZ, "cam = (0, 0, -1)");
            Assert.AreEqual(0.012f, camera.CamSpd, 1e-7f, "Chain.vb: camSPD");
            Assert.AreEqual(0.02f, camera.MoveStep, 1e-7f, "move_up / move_down");
            Assert.AreEqual(0.25f, camera.IsometricStep, 1e-7f, "isometric / deisometric");

            var debug = Load().DebugCameraSettings;
            Assert.AreEqual(-9f, debug.EyeZ, 1e-5f, "z -1 x 9");
            Assert.AreEqual(0.012f * 9f * 60f, debug.StepSpeed, 1e-4f, "per frame at 60 fps");
            Assert.AreEqual(0.02f * 9f * 60f, debug.LiftSpeed, 1e-4f);
            Assert.AreEqual(0.25f * 9f * 60f, debug.IsometricSpeed, 1e-3f);
            Assert.AreEqual(4f / 3f, debug.Aspect, 1e-6f);
        }

        [Test]
        public void Camera_EyeBehindThePlane_FailsToLoad()
        {
            var e = LoadFails(Patched(GameSettings.CameraFile, text => text.Replace("\"camZ\": -1", "\"camZ\": 1")));
            StringAssert.Contains("camZ", e.Message);
        }

        [Test]
        public void Shipped_HeartIsHeartFloatsDefault()
        {
            var heart = AtScale(1f).Heart;
            var expected = HeartFloat.Settings.Default;

            Assert.AreEqual(expected.ItemSize, heart.ItemSize, 1e-7f);
            Assert.AreEqual(expected.FallRate, heart.FallRate, 1e-7f);
            Assert.AreEqual(expected.StartFloatSpeed, heart.StartFloatSpeed, 1e-7f);
            Assert.AreEqual(expected.StartY, heart.StartY, 1e-7f);
            Assert.AreEqual(expected.WrapBelow, heart.WrapBelow, 1e-7f);
            Assert.AreEqual(expected.AngelSize, heart.AngelSize, 1e-7f);
            Assert.AreEqual(expected.RectWidth, heart.RectWidth, 1e-7f);
            Assert.AreEqual(expected.AngelRectWidth, heart.AngelRectWidth, 1e-7f);
        }

        [Test]
        public void LengthScale_MovesEveryLengthTogether()
        {
            var settings = Load();

            Assert.AreEqual(0.018f, settings.Player.Chain.SegmentLength, 1e-6f);
            Assert.AreEqual(-81f, settings.Player.Chain.Gravity.Y, 1e-3f);
            Assert.AreEqual(360f, settings.Player.Chain.SpringConstant, 1e-3f, "stiffness has no length in it");
            Assert.AreEqual(0.09f, settings.Player.Flight.HoverAmplitude, 1e-6f);
            Assert.AreEqual(3.6f, settings.Heart.StartY, 1e-6f);
            Assert.AreEqual(0.54f, settings.World.Enemies.FlockSpeed, 1e-5f);
            Assert.AreEqual(0.36f, settings.World.Enemies.Brain.SeekRate, 1e-6f, "a rate, not a length");
            Assert.AreEqual(2.16f, settings.World.Enemies.Brain.CautiousRate, 1e-5f, "Cautious's 6 x seekSpd is a rate too");
            Assert.AreEqual(3.24f, settings.World.Enemies.Brain.AlignSpeed, 1e-5f, "Align's seekSpd a frame is a speed: 0.006 x 9 x 60");
            Assert.AreEqual(2.7f, settings.World.Enemies.Brain.HangBackNear, 1e-5f);
            Assert.AreEqual(4.05f, settings.World.Enemies.Brain.HangBackFar, 1e-5f);
            Assert.AreEqual(AtScale(1f).Player.HitRadius, settings.Player.HitRadius, "port figures are never mapped");
        }

        [Test]
        public void Shipped_ScaledToTheArena()
        {
            var settings = Load();

            Assert.AreEqual(0.018f, settings.Player.Chain.SegmentLength, 1e-6f);
            Assert.AreEqual(3.6f, settings.Heart.StartY, 1e-6f, "start 0.4 of the screen height");
            Assert.AreEqual(0.36f, settings.World.Enemies.Brain.SeekRate, 1e-6f, "a rate, not a length");
        }

        [Test]
        public void Shipped_ResetFigures_AreRootVbs_ThroughTheOneMapping()
        {
            var settings = Load();

            Assert.AreEqual(1f, settings.World.GameSpeed, "gameSpeed = 1.0");
            Assert.AreEqual(30, settings.World.Session.MaxHp, "Angel(0).HP = maxHP");
            Assert.AreEqual(1f, settings.World.Enemies.Hp, "Enemy(n).HP = 1.0");
            Assert.AreEqual(0.2f, settings.World.Enemies.LoadHp, "loadEnemy's Enemy(n).HP = 0.2, UNI-T70");
            Assert.AreEqual(settings.Heart.StartY, settings.World.Heart.StartY, "the world's hearts are item.json's");
            Assert.AreEqual(0.4f * 9f, settings.World.Heart.StartY, 1e-5f, "item(n).Y = 0.4");
            Assert.AreEqual(0.001f * 9f, settings.World.Heart.ResetFloatSpeed, 1e-6f, "floatSPD = 0.001");
            Assert.AreEqual(0.1f * 3600f, settings.Player.Chain.SpringConstant, 1e-2f, "springK = 0.1 a frame");
            Assert.AreEqual(0.25f * 60f, settings.Player.Chain.Resistance, 1e-4f, "resistance = 0.25 a frame");
            Assert.AreEqual(-0.0025f * 9f * 3600f, settings.Player.Chain.Gravity.Y, 1e-2f, "gravity.Y = -0.0025");
        }

        [Test]
        public void Shipped_FeedsAWorld()
        {
            var settings = Load();
            var world = new GameWorld(settings.World, new Random(1));
            world.AddPlayer(new PlayerId(0), settings.Player);
            world.Reset();
            for (var i = 0; i < 60; i++)
            {
                world.Step(1f / 60f);
            }

            // The first wave spawns at loadEnemy's weaker HP (UNI-T70): one of the ten waiting enemies
            // takes enough incidental chain contact in this first second to die before ever seeking.
            Assert.AreEqual(13, world.Enemies.Count);
            Assert.AreEqual(4, world.SeekerCount(), "the shipped Seek cap");
            Assert.AreEqual(9, world.Enemies.Count(e => e.Brain.IsWaiting), "the rest hang back or stalk");
            Assert.AreEqual(6, world.Players[0].MaceIndex);
        }

        // --- Validation -------------------------------------------------------------------------

        [Test]
        public void MissingFile_IsNamed()
        {
            var e = LoadFails(new MemorySettingsSource(Shipped).Set(GameSettings.ChainFile, null));

            StringAssert.Contains("chain.json: not found", e.Message);
        }

        [Test]
        public void BadJson_GivesTheLine()
        {
            var e = LoadFails(Patched(GameSettings.AngelFile, text => text.Replace("\"maxHP\": 30,", "\"maxHP\": 30")));

            Assert.AreEqual(1, e.Errors.Count);
            StringAssert.Contains("angel.json: is not valid JSON", e.Errors[0]);
            StringAssert.Contains("line", e.Errors[0]);
        }

        [Test]
        public void OutOfRange_NamesTheKeyAndLine()
        {
            var e = LoadFails(Patched(GameSettings.ChainFile, text => text.Replace("\"springK\": 0.1", "\"springK\": -0.1")));

            Assert.AreEqual(1, e.Errors.Count);
            StringAssert.Contains("chain.json: 'springK' must be greater than 0 (is -0.1) (line", e.Errors[0]);
        }

        [Test]
        public void WrongType_IsReported()
        {
            var e = LoadFails(Patched(GameSettings.EnemiesFile, text => text.Replace("\"maxEnemy\": 13", "\"maxEnemy\": \"13\"")));

            StringAssert.Contains("enemies.json: 'maxEnemy' must be a whole number (is \"13\")", e.Message);
        }

        [Test]
        public void MissingKey_IsReported()
        {
            var e = LoadFails(Patched(GameSettings.ItemFile, text => text.Replace("\"fallRate\": 0.001,", string.Empty)));

            StringAssert.Contains("item.json: 'fallRate' is missing", e.Message);
        }

        [Test]
        public void UnknownKey_IsATypo()
        {
            var e = LoadFails(Patched(GameSettings.ChainFile, text => text.Replace("\"springK\"", "\"sprinK\"")));

            Assert.That(e.Errors, Has.Some.Contains("chain.json: unknown key 'sprinK'"));
            Assert.That(e.Errors, Has.Some.Contains("chain.json: 'springK' is missing"));
        }

        [Test]
        public void NestedKey_IsNamedWithItsSection()
        {
            var e = LoadFails(Patched(GameSettings.ChainFile, text => text.Replace("\"wallBounce\": 0.5", "\"wallBounce\": 1.5")));

            StringAssert.Contains("chain.json.port: 'wallBounce' must be between 0 and 1 (is 1.5)", e.Message);
        }

        [Test]
        public void HangBack_FarMustBeBeyondNear()
        {
            var e = LoadFails(Patched(GameSettings.EnemiesFile, text => text.Replace("\"hangBackFar\": 0.45", "\"hangBackFar\": 0.2")));

            StringAssert.Contains("hangBackFar", e.Message);
        }

        [Test]
        public void CrossChecks_AreReported()
        {
            var e = LoadFails(Patched(GameSettings.ChainFile, text => text.Replace("\"Mace\": 6", "\"Mace\": 13")));

            StringAssert.Contains("'Mace' must be between maceMin and maceMax (1-12)", e.Message);
        }

        [Test]
        public void BadChoice_ListsTheNames()
        {
            var e = LoadFails(Patched(GameSettings.CameraFile, text => text.Replace("\"FitLevel\"", "\"Fit\"")));

            StringAssert.Contains("'mode' must be one of FitLevel, Follow (is \"Fit\")", e.Message);
        }

        [Test]
        public void EveryProblem_IsReportedInOneLoad()
        {
            var source = new MemorySettingsSource(Shipped)
                .Set(GameSettings.ChainFile, Shipped.Read(GameSettings.ChainFile).Replace("\"springK\": 0.1", "\"springK\": 0"))
                .Set(GameSettings.AngelFile, Shipped.Read(GameSettings.AngelFile).Replace("\"maxHP\": 30", "\"maxHP\": 0"))
                .Set(GameSettings.GameFile, null);

            var e = LoadFails(source);

            Assert.AreEqual(3, e.Errors.Count, string.Join("\n", e.Errors));
            StringAssert.Contains("(3 problems)", e.Message);
        }

        [Test]
        public void NoteKeys_AreSkipped()
        {
            var source = Patched(GameSettings.GameFile, text => text.Replace("{", "{\n  \"_note\": [1, 2, 3],"));

            Assert.DoesNotThrow(() => GameSettings.Load(source));
        }

        // --- The JSON reader --------------------------------------------------------------------

        [Test]
        public void Json_ReadsEveryKind()
        {
            var value = Json.Parse("{ \"a\": [1, -2.5e1, true, false, null], \"b\": \"x\\n\\u0041\", \"c\": {} }");

            Assert.AreEqual(JsonKind.Object, value.Kind);
            var a = value.Get("a").Items;
            Assert.AreEqual(1d, a[0].Number);
            Assert.AreEqual(-25d, a[1].Number);
            Assert.IsTrue(a[2].Bool);
            Assert.IsFalse(a[3].Bool);
            Assert.AreEqual(JsonKind.Null, a[4].Kind);
            Assert.AreEqual("x\nA", value.Get("b").Text);
            Assert.AreEqual(0, value.Get("c").Members.Count);
        }

        [TestCase("{ \"a\": 1, }", 1)]
        [TestCase("{\n  \"a\": 1\n  \"b\": 2\n}", 3)]
        [TestCase("{ \"a\": 1, \"a\": 2 }", 1)]
        [TestCase("{ \"a\": 01x }", 1)]
        [TestCase("[1, 2", 1)]
        public void Json_RejectsWithTheLine(string text, int line)
        {
            var e = Assert.Throws<JsonException>(() => Json.Parse(text));

            Assert.AreEqual(line, e.Line);
        }

        [Test]
        public void Json_RemembersLines()
        {
            var value = Json.Parse("{\n\"a\": 1,\n\n\"b\": 2\n}");

            Assert.AreEqual(2, value.Get("a").Line);
            Assert.AreEqual(4, value.Get("b").Line);
        }

        [Test]
        public void Errors_AreNotDuplicatedAcrossFiles()
        {
            var e = LoadFails(Patched(GameSettings.SpaceFile, text => text.Replace("\"framesPerSecond\": 60", "\"framesPerSecond\": 0")));

            Assert.AreEqual(1, e.Errors.Count(x => x.Contains("framesPerSecond")));
        }
    }
}
