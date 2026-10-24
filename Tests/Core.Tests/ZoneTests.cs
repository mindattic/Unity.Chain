using System.Collections.Generic;
using System.Linq;
using MindAttic.Chain.Core;
using NUnit.Framework;
using CollectionAssert = NUnit.Framework.Legacy.CollectionAssert;

namespace MindAttic.Chain.Tests
{
    /// <summary>
    /// UNI-T56: Zone.vb's hit box overlay. hud.json holds loadZone's box, renderZone's corners and texZone's colour;
    /// <see cref="HitZones"/> puts one box on every enemy and one on each following mace (BIBLE D23); E flips
    /// <see cref="ViewOptions.Zones"/>, which starts on (bZone = True) and no reset touches.
    /// </summary>
    public class ZoneTests
    {
        private static readonly PlayerId P0 = new PlayerId(0);

        private static GameSettings Shipped => GameSettings.Load(new DirectorySettingsSource(GameSettingsTests.ShippedFolder));

        [Test]
        public void HudJson_HoldsLoadZoneAndRenderZone()
        {
            var zone = Shipped.Hud.Zone;

            Assert.IsTrue(zone.Shown, "Root.vb: bZone = True");
            Assert.AreEqual(0f, zone.Left, "theZone(n).Left = 0");
            Assert.AreEqual(0.05f, zone.Right, "theZone(n).Right = 0.05");
            Assert.AreEqual(0f, zone.Top, "theZone(n).Top = 0");
            Assert.AreEqual(-0.05f, zone.Bottom, "theZone(n).Bottom = -0.05");
            Assert.AreEqual(Vec2.Zero, zone.EnemyCorner, "Enemy(n).X + enemySize / 2, Enemy(n).Y: the enemy's centre");
            Assert.AreEqual(0.01f - 3.5f * 0.025f / 2f, zone.MaceCorner.X, 1e-6f, "rectMace.Left from the mace's centre");
            Assert.AreEqual(0.075f / 2f, zone.MaceCorner.Y, 1e-6f, "rectMace.Top = link(Mace).Y + maceSize / 2");
            Assert.AreEqual(new Argb(255, 0, 128, 255).ToString(), zone.Colour.ToString(), "event.png: 0, 128, 255");
            Assert.Greater(zone.LineWidth, 0f);
        }

        [Test]
        public void Settings_MapTheBoxIntoWorldUnits()
        {
            var settings = Shipped;
            var scale = settings.Mapping.LengthScale;

            Assert.AreEqual(0.05f * scale, settings.Zones.Size.X, 1e-5f);
            Assert.AreEqual(0.05f * scale, settings.Zones.Size.Y, 1e-5f);
            Assert.AreEqual(Vec2.Zero, settings.Zones.EnemyCorner);
            Assert.AreEqual(-0.03375f * scale, settings.Zones.MaceCorner.X, 1e-5f);
            Assert.AreEqual(0.0375f * scale, settings.Zones.MaceCorner.Y, 1e-5f);
        }

        [Test]
        public void HudJson_RejectsAnInsideOutBox_NamingIt()
        {
            var shipped = new DirectorySettingsSource(GameSettingsTests.ShippedFolder);
            var source = new MemorySettingsSource(shipped).Set(
                GameSettings.HudFile,
                shipped.Read(GameSettings.HudFile).Replace("\"right\": 0.05", "\"right\": -0.1"));

            var e = Assert.Throws<SettingsException>(() => GameSettings.Load(source));

            Assert.IsTrue(e.Errors.Any(x => x.Contains("hud.json.zone") && x.Contains("right")), string.Join("\n", e.Errors));
        }

        [Test]
        public void Box_HangsBelowAndRightOfItsCorner()
        {
            var box = HitZones.Box(new Vec2(1f, 2f), new Vec2(0.45f, 0.45f));

            Assert.AreEqual(1f, box.XMin, 1e-6f);
            Assert.AreEqual(1.45f, box.XMax, 1e-6f);
            Assert.AreEqual(2f, box.YMax, 1e-6f, "the corner is the top: loadZone's Top 0, Bottom -0.05");
            Assert.AreEqual(1.55f, box.YMin, 1e-6f);
        }

        [Test]
        public void Strip_RunsUpperRightUpperLeftLowerLeftLowerRight_AndCloses()
        {
            var strip = HitZones.Strip(new Rect2(0f, 0f, 2f, 1f));

            CollectionAssert.AreEqual(
                new[] { new Vec2(2f, 1f), new Vec2(0f, 1f), new Vec2(0f, 0f), new Vec2(2f, 0f), new Vec2(2f, 1f) },
                strip,
                "vZone(0..4): UR, UL, LL, LR, UR, drawn as a four-line strip");
        }

        [Test]
        public void Collect_BoxesTheMaceFirst_ThenEveryEnemy()
        {
            var settings = GameWorld.Settings.Default;
            settings.Enemies.Count = 0;
            var world = new GameWorld(settings, new System.Random(1));
            var player = world.AddPlayer(P0, PlayerSetup.Default);

            // 14 enemies: the original boxes only maxZone + 1 = 6 and never enemy 0 (BIBLE D23); here every one.
            var enemies = Enumerable.Range(0, 14).Select(i => world.SpawnEnemy(new Vec2(i - 7f, 2f))).ToList();
            var zones = new HitZones.Settings(new Vec2(0.45f, 0.45f), new Vec2(0.1f, 0f), new Vec2(-0.3f, 0.3f));
            var boxes = new List<Rect2>();

            Assert.AreEqual(15, HitZones.Collect(world, zones, boxes));
            Assert.AreEqual(HitZones.Box(player.MacePosition + new Vec2(-0.3f, 0.3f), zones.Size), boxes[0], "Zone.vb's box 0 is the mace's");
            for (var i = 0; i < enemies.Count; i++)
            {
                Assert.AreEqual(HitZones.Box(enemies[i].Position + new Vec2(0.1f, 0f), zones.Size), boxes[i + 1], $"enemy {i}");
            }

            // A killed enemy leaves the world and its box goes with it.
            world.ClearEnemies();
            Assert.AreEqual(1, HitZones.Collect(world, zones, boxes));
        }

        [Test]
        public void Collect_DropsTheMaceBox_WhileTheChainDoesNotFollow()
        {
            var settings = GameWorld.Settings.Default;
            settings.Enemies.Count = 0;
            var world = new GameWorld(settings, new System.Random(1));
            world.AddPlayer(P0, PlayerSetup.Default);
            world.SpawnEnemy(new Vec2(1f, 1f));
            var zones = new HitZones.Settings(new Vec2(0.45f, 0.45f), Vec2.Zero, Vec2.Zero);
            var boxes = new List<Rect2>();

            Assert.IsTrue(world.Submit(new ToggleFollow(P0)));

            // With F off the mace hits nothing (BIBLE D17), so it has no hit box to show; the enemy keeps its own.
            Assert.AreEqual(1, HitZones.Collect(world, zones, boxes));
            Assert.AreEqual(HitZones.Box(new Vec2(1f, 1f), zones.Size), boxes[0]);
        }

        [Test]
        public void Zones_StartOn_EFlipsThem_AndNoResetTouchesThem()
        {
            var options = new ViewOptions();
            var changes = new List<bool>();
            options.ZonesChanged += changes.Add;

            Assert.IsTrue(options.Zones, "bZone = True");
            Assert.IsFalse(options.ToggleZones(), "E: bZone = Not bZone");
            Assert.IsFalse(options.Zones);

            options.Reset();
            Assert.IsFalse(options.Zones, "Root.vb's Reset never sets bZone");
            Assert.IsTrue(options.ToggleZones());
            CollectionAssert.AreEqual(new[] { false, true }, changes);

            Assert.IsFalse(new ViewOptions(zones: false).Zones, "hud.json's bZone false starts it off");
        }

        [Test]
        public void AltersRendering_OnlyWhileARenderFlagIsOffItsStart()
        {
            var options = new ViewOptions();
            Assert.IsFalse(options.AltersRendering, "solid, textured, no culling: drawn plainly");

            options.Toggle(MenuEntry.Wireframe);
            Assert.IsTrue(options.AltersRendering, "F1");
            options.Toggle(MenuEntry.Wireframe);

            options.Toggle(MenuEntry.Textures);
            Assert.IsTrue(options.AltersRendering, "F2");
            options.Toggle(MenuEntry.Textures);

            options.Toggle(MenuEntry.Culling);
            Assert.IsTrue(options.AltersRendering, "F3");
            options.Reset();

            options.Toggle(MenuEntry.Fps);
            options.ToggleMenu();
            options.ToggleZones();
            Assert.IsFalse(options.AltersRendering, "F4, Tab and E change nothing the renderer draws");
        }
    }
}
