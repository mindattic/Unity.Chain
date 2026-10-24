using System;
using System.Linq;
using MindAttic.Chain.Core;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace MindAttic.Chain.Tests
{
    /// <summary>
    /// Chain.vb's unreachable <c>theMaceMode</c> as a mace skin (UNI-T43, BIBLE D4): chain.json's <c>maceSkins</c>
    /// chooses the starting skin, the <see cref="ToggleMaceMode"/> debug key steps spike ball to magnet and back, and
    /// the hit and grapple rules are the same whatever the mace wears.
    /// </summary>
    public class MaceSkinTests
    {
        private static readonly PlayerId P0 = new PlayerId(0);

        private GameSettings _settings;
        private GameWorld _world;
        private PlayerEntity _player;

        [SetUp]
        public void SetUp()
        {
            _settings = GameSettings.Load(Shipped);
            _world = new GameWorld(_settings.World, new Random(1));
            _player = _world.AddPlayer(P0, _settings.Player);
            _world.Reset();
            _world.TakeEvents();
        }

        private static ISettingsSource Shipped => new DirectorySettingsSource(GameSettingsTests.ShippedFolder);

        private static GameSettings LoadChain(Func<string, string> change) => GameSettings.Load(
            new MemorySettingsSource(Shipped).Set(GameSettings.ChainFile, change(Shipped.Read(GameSettings.ChainFile))));

        [Test]
        public void TheModes_AreChainVbsTheMaceMode()
        {
            CollectionAssert.AreEqual(new[] { MaceMode.SpikeBall, MaceMode.Magnet }, MaceModes.All);
            Assert.AreEqual(MaceMode.Magnet, MaceModes.Next(MaceMode.SpikeBall));
            Assert.AreEqual(MaceMode.SpikeBall, MaceModes.Next(MaceMode.Magnet));
        }

        [Test]
        public void ChainJson_StartsOnTheSpikeBall_AndNamesEveryModesPicture()
        {
            var skins = _settings.Chain.MaceSkins;

            Assert.AreEqual(MaceMode.SpikeBall, skins.Mode, "Chain.vb: maceMode As theMaceMode = theMaceMode.SpikeBall");
            Assert.AreEqual("mace.png", skins.File(MaceMode.SpikeBall));
            Assert.AreEqual("magnet.png", skins.File(MaceMode.Magnet));
            Assert.AreEqual(MaceMode.SpikeBall, _settings.Player.MaceMode);
            Assert.AreEqual(MaceMode.SpikeBall, _player.MaceMode);
        }

        [Test]
        public void TheSetting_ChoosesTheMagnetFromTheStart()
        {
            var settings = LoadChain(text => text.Replace("\"mode\": \"SpikeBall\"", "\"mode\": \"Magnet\""));
            var world = new GameWorld(settings.World, new Random(1));

            Assert.AreEqual(MaceMode.Magnet, world.AddPlayer(P0, settings.Player).MaceMode);
        }

        [Test]
        public void TheDebugKey_StepsToTheMagnetAndBack_PausedToo()
        {
            _world.Submit(new Pause(P0, true));
            _world.TakeEvents();

            Assert.IsTrue(_world.Submit(new ToggleMaceMode(P0)));
            Assert.AreEqual(MaceMode.Magnet, _player.MaceMode);
            var changed = _world.TakeEvents().OfType<MaceModeChanged>().Single();
            Assert.AreEqual(P0, changed.Player);
            Assert.AreEqual(MaceMode.Magnet, changed.Mode);

            Assert.IsTrue(_world.Submit(new ToggleMaceMode(P0)));
            Assert.AreEqual(MaceMode.SpikeBall, _player.MaceMode);
        }

        [Test]
        public void AnUnseatedPlayer_ChangesNothing()
        {
            Assert.IsFalse(_world.Submit(new ToggleMaceMode(new PlayerId(7))));
            Assert.IsEmpty(_world.TakeEvents().OfType<MaceModeChanged>());
        }

        [Test]
        public void NoResetOrRetune_ChangesTheSkin()
        {
            _world.Submit(new ToggleMaceMode(P0));

            _world.Submit(new ResetGame(P0));
            Assert.AreEqual(MaceMode.Magnet, _player.MaceMode, "Root.vb's Reset never touches maceMode");

            var setup = _settings.Player;
            setup.LinkCount = 20;
            _world.Configure(P0, setup);
            Assert.AreEqual(MaceMode.Magnet, _player.MaceMode);
        }

        [Test]
        public void TheMagnet_HitsAndCatchesExactlyAsTheSpikeBall()
        {
            var ball = Swing(magnet: false);
            var magnet = Swing(magnet: true);

            StringAssert.Contains("EnemyHit", ball, "the swing must hit something for the comparison to mean anything");
            Assert.AreEqual(ball, magnet);
        }

        [Test]
        public void ChainJson_MustNameAKnownModeAndEveryPicture()
        {
            var e = Assert.Throws<SettingsException>(() => LoadChain(text => text.Replace("\"mode\": \"SpikeBall\"", "\"mode\": \"Hook\"")));
            StringAssert.Contains("chain.json.maceSkins: 'mode' must be one of SpikeBall, Magnet (is \"Hook\")", e.Message);

            e = Assert.Throws<SettingsException>(() => LoadChain(text => text.Replace("\"Magnet\": \"magnet.png\"", "\"Horseshoe\": \"magnet.png\"")));
            StringAssert.Contains("chain.json.maceSkins.files", e.Message);
            StringAssert.Contains("Magnet", e.Message);
        }

        /// <summary>
        /// Swings the mace through an enemy, then arms the grapple on another, in a fresh world, and returns what
        /// happened as text: every hit, knock, kill and catch with its figures, and the mace's path.
        /// </summary>
        private string Swing(bool magnet)
        {
            var world = new GameWorld(_settings.World, new Random(3));
            var player = world.AddPlayer(P0, _settings.Player);
            world.Reset();
            if (magnet)
            {
                world.Submit(new ToggleMaceMode(P0));
                Assert.AreEqual(MaceMode.Magnet, player.MaceMode);
            }

            world.TakeEvents();
            var log = new System.Text.StringBuilder();
            const float dt = 1f / 60f;
            for (var i = 0; i < 240; i++)
            {
                world.Submit(new SteerAngel(P0, new Vec2(i < 120 ? 6f : -6f, i % 40 < 20 ? 3f : -3f), dt));
                if (i == 150)
                {
                    world.Submit(new ArmGrapple(P0));
                }

                world.Step(dt);
                log.Append(player.MacePosition).Append(';');
                foreach (var e in world.TakeEvents().Where(e => !(e is MaceModeChanged)))
                {
                    log.Append(e).Append(';');
                }
            }

            log.Append(player.Grapple.State).Append(';');
            foreach (var enemy in world.Enemies)
            {
                log.Append(enemy.Hp).Append(',').Append(enemy.Position).Append(';');
            }

            return log.ToString();
        }
    }
}
