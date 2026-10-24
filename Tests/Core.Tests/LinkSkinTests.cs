using System;
using System.Linq;
using MindAttic.Chain.Core;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace MindAttic.Chain.Tests
{
    /// <summary>
    /// Form1.vb's U (UNI-T41): links 1 to 5 put on aqua, emerald, teardrop, tablet and pearl, the rest keep link,
    /// the mace's link stays the mace; the set is chain.json's <c>skins</c>, and nothing takes the gems off again.
    /// </summary>
    public class LinkSkinTests
    {
        private static readonly PlayerId P0 = new PlayerId(0);

        private GameSettings _settings;
        private GameWorld _world;
        private PlayerEntity _player;

        [SetUp]
        public void SetUp()
        {
            _settings = GameSettings.Load(new DirectorySettingsSource(GameSettingsTests.ShippedFolder));
            _world = new GameWorld(_settings.World, new Random(1));
            _player = _world.AddPlayer(P0, _settings.Player);
            _world.Reset();
            _world.TakeEvents();
        }

        private static ISettingsSource Shipped => new DirectorySettingsSource(GameSettingsTests.ShippedFolder);

        private static SettingsException ChainFails(Func<string, string> change) =>
            Assert.Throws<SettingsException>(() => GameSettings.Load(
                new MemorySettingsSource(Shipped).Set(GameSettings.ChainFile, change(Shipped.Read(GameSettings.ChainFile)))));

        [Test]
        public void ChainJson_HoldsTexturesVbsPicturesAndForm1sUSet()
        {
            var skins = _settings.Chain.Skins;

            Assert.AreEqual("link", skins.Link, "loadChain: link(n).texture = texChain");
            CollectionAssert.AreEqual(new[] { "link", "aqua", "emerald", "teardrop", "tablet", "pearl" }, skins.Skins);
            foreach (var name in skins.Skins)
            {
                Assert.AreEqual(name + ".png", skins.File(name));
            }

            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5 }, skins.KeyU.Keys.OrderBy(k => k));
            Assert.AreEqual("aqua", skins.KeyU[1]);
            Assert.AreEqual("emerald", skins.KeyU[2]);
            Assert.AreEqual("teardrop", skins.KeyU[3]);
            Assert.AreEqual("tablet", skins.KeyU[4]);
            Assert.AreEqual("pearl", skins.KeyU[5]);
        }

        [Test]
        public void AtTheStart_EveryLinkIsLink()
        {
            Assert.IsFalse(_player.HasGemSkins);
            for (var i = 0; i < _player.MaceIndex; i++)
            {
                Assert.AreEqual("link", _player.LinkSkin(i), $"link {i}");
            }
        }

        [Test]
        public void U_PutsTheGemsOnLinksOneToFive_TheRestStayLink()
        {
            Assert.IsTrue(_world.Submit(new SkinLinks(P0)));

            Assert.AreEqual(P0, _world.TakeEvents().OfType<LinksSkinned>().Single().Player);
            Assert.IsTrue(_player.HasGemSkins);
            Assert.AreEqual("link", _player.LinkSkin(0), "the anchor keeps link");
            Assert.AreEqual("aqua", _player.LinkSkin(1));
            Assert.AreEqual("emerald", _player.LinkSkin(2));
            Assert.AreEqual("teardrop", _player.LinkSkin(3));
            Assert.AreEqual("tablet", _player.LinkSkin(4));
            Assert.AreEqual("pearl", _player.LinkSkin(5));

            _world.Submit(new ChangeChainLength(P0, 6)); // mace 6 -> 12
            for (var i = 6; i < _player.MaceIndex; i++)
            {
                Assert.AreEqual("link", _player.LinkSkin(i), $"link {i} keeps link.png");
            }
        }

        [Test]
        public void TheMaceLink_IsNotChanged_AndItsOwnSkinShowsOnceTheMaceMovesOff()
        {
            _world.Submit(new ChangeChainLength(P0, -3)); // mace 6 -> 3
            Assert.AreEqual(3, _player.MaceIndex);

            _world.Submit(new SkinLinks(P0));
            Assert.IsNull(_player.LinkSkin(3), "the mace's link is drawn as the mace, not teardrop");
            Assert.IsNull(_player.LinkSkin(4), "past the mace nothing is drawn");
            Assert.AreEqual("emerald", _player.LinkSkin(2));

            _world.Submit(new ChangeChainLength(P0, 3)); // back to 6
            Assert.AreEqual("teardrop", _player.LinkSkin(3));
            Assert.AreEqual("pearl", _player.LinkSkin(5));
            Assert.IsNull(_player.LinkSkin(6));
        }

        [Test]
        public void U_WorksPaused_AndASecondUChangesNothing()
        {
            _world.Submit(new Pause(P0, true));
            _world.TakeEvents();

            Assert.IsTrue(_world.Submit(new SkinLinks(P0)), "Form1.vb's key handler reads U paused too");
            Assert.IsFalse(_world.Submit(new SkinLinks(P0)));
            Assert.AreEqual(1, _world.TakeEvents().OfType<LinksSkinned>().Count());
            Assert.AreEqual("aqua", _player.LinkSkin(1));
        }

        [Test]
        public void NoResetOrRetune_TakesTheGemsOff()
        {
            _world.Submit(new SkinLinks(P0));

            _world.Submit(new ResetGame(P0));
            Assert.IsTrue(_player.HasGemSkins, "Root.vb's Reset never touches link(n).texture");
            Assert.AreEqual("pearl", _player.LinkSkin(5));

            var setup = _settings.Player;
            setup.LinkCount = 20;
            _world.Configure(P0, setup);
            Assert.AreEqual("tablet", _player.LinkSkin(4));
        }

        [Test]
        public void ASetupWithoutSkins_IsAllLink()
        {
            var world = new GameWorld(_settings.World, new Random(1));
            var setup = _settings.Player;
            setup.Skins = null;
            var player = world.AddPlayer(P0, setup);

            world.Submit(new SkinLinks(P0));
            Assert.AreEqual("link", player.LinkSkin(1));
        }

        [Test]
        public void KeyU_MustNameALinkAndAPicture()
        {
            var e = ChainFails(text => text.Replace("\"5\": \"pearl\"", "\"30\": \"pearl\""));
            StringAssert.Contains("chain.json.skins.keyU: '30' must be a link index between 1 and maxLinks (25)", e.Message);

            e = ChainFails(text => text.Replace("\"1\": \"aqua\"", "\"1\": \"ruby\""));
            StringAssert.Contains("chain.json.skins.keyU: '1' must name a skin in files (is \"ruby\")", e.Message);

            e = ChainFails(text => text.Replace("\"link\": \"link\"", "\"link\": \"rope\""));
            StringAssert.Contains("chain.json.skins: 'link' must name a skin in files (is \"rope\")", e.Message);
        }
    }
}
