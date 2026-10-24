using System.Collections.Generic;
using System.Linq;
using MindAttic.Chain.Core;
using NUnit.Framework;
using CollectionAssert = NUnit.Framework.Legacy.CollectionAssert;

namespace MindAttic.Chain.Tests
{
    /// <summary>
    /// UNI-T55: Gui.vb's renderMenu. hud.json holds loadMenu's panel and renderMenu's line spots; Root.vb's option
    /// flags (<see cref="ViewOptions"/>) start, flip and reset as Form1.vb's keys and Root.vb's Reset have them; each
    /// line names what its key does now (<see cref="GameMenu"/>); F12 flips the window mode as Startup's toggleScreen.
    /// </summary>
    public class MenuTests
    {
        private static HudData Hud => GameSettings.Load(new DirectorySettingsSource(GameSettingsTests.ShippedFolder)).Hud;

        [Test]
        public void HudJson_HoldsLoadMenuAndRenderMenu()
        {
            var menu = Hud.Menu;

            Assert.AreEqual(new Vec2(10f, 10f), menu.Position, "the quad's top-left corner");
            Assert.AreEqual(new Vec2(255f, 255f), menu.Size, "menuWidth x menuHeight");
            Assert.AreEqual(new Argb(255, 255, 255, 64).ToString(), menu.Colour.ToString(), "Write(..., 255, 255, 255, 64)");
            Assert.AreEqual(10f, menu.FontSize, "fontMenu, Arial 10");
            Assert.IsTrue(menu.Bold);

            var spots = GameMenu.Entries.Select(e => menu.Line(e).Position).ToArray();
            CollectionAssert.AreEqual(
                new[] { new Vec2(20f, 20f), new Vec2(20f, 40f), new Vec2(20f, 60f), new Vec2(20f, 80f), new Vec2(20f, 100f), new Vec2(20f, 240f) },
                spots,
                "F1..F4 every 20 px from (20, 20), F12 at (20, 100), ESC at (20, 240)");
            Assert.AreEqual(HudAnchor.TopLeft, menu.Line(MenuEntry.Quit).Anchor);
        }

        [Test]
        public void HudJson_RejectsAMissingMenuLine_NamingIt()
        {
            var shipped = new DirectorySettingsSource(GameSettingsTests.ShippedFolder);
            var source = new MemorySettingsSource(shipped).Set(
                GameSettings.HudFile,
                shipped.Read(GameSettings.HudFile).Replace("\"Quit\": [20, 240]", "\"Exit\": [20, 240]"));

            var e = Assert.Throws<SettingsException>(() => GameSettings.Load(source));

            Assert.IsTrue(e.Errors.Any(x => x.Contains("hud.json.menu.lines") && x.Contains("'Quit' is missing")), string.Join("\n", e.Errors));
            Assert.IsTrue(e.Errors.Any(x => x.Contains("unknown key 'Exit'")), string.Join("\n", e.Errors));
        }

        [Test]
        public void Options_StartAsRootVbHasThem()
        {
            var options = new ViewOptions();

            Assert.IsFalse(options.Wireframe, "bRenderWireframe = False");
            Assert.IsTrue(options.Textured, "bRenderTextures = True");
            Assert.IsFalse(options.BackFaceCulling, "bBackFaceCulling = False");
            Assert.IsTrue(options.DrawFps, "bDrawFPS = True");
            Assert.IsFalse(options.MenuOpen, "bMenu = False");
            Assert.IsFalse(new ViewOptions(drawFps: false).DrawFps, "hud.json's drawFps decides the frame rate's start");
        }

        [Test]
        public void F1ToF4_FlipTheirFlag_AndTab_TheMenu()
        {
            var options = new ViewOptions();
            var toggled = new List<(MenuEntry, bool)>();
            var menu = new List<bool>();
            options.Toggled += (entry, value) => toggled.Add((entry, value));
            options.MenuChanged += menu.Add;

            Assert.IsTrue(options.Toggle(MenuEntry.Wireframe));
            Assert.IsFalse(options.Toggle(MenuEntry.Textures));
            Assert.IsTrue(options.Toggle(MenuEntry.Culling));
            Assert.IsFalse(options.Toggle(MenuEntry.Fps));
            Assert.IsTrue(options.ToggleMenu());

            Assert.IsTrue(options[MenuEntry.Wireframe]);
            Assert.IsFalse(options[MenuEntry.Textures]);
            Assert.IsTrue(options[MenuEntry.Culling]);
            Assert.IsFalse(options[MenuEntry.Fps]);
            CollectionAssert.AreEqual(
                new[] { (MenuEntry.Wireframe, true), (MenuEntry.Textures, false), (MenuEntry.Culling, true), (MenuEntry.Fps, false) },
                toggled);
            CollectionAssert.AreEqual(new[] { true }, menu);

            Assert.IsFalse(options.ToggleMenu(), "Tab again closes it");
            Assert.IsFalse(options.Toggle(MenuEntry.Wireframe), "F1 again turns it back");
        }

        [Test]
        public void F12AndEscape_HoldNoFlag()
        {
            var options = new ViewOptions();
            Assert.IsTrue(ViewOptions.IsFlag(MenuEntry.Fps));
            Assert.IsFalse(ViewOptions.IsFlag(MenuEntry.Video));
            Assert.IsFalse(ViewOptions.IsFlag(MenuEntry.Quit));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => options.Toggle(MenuEntry.Video));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => options.Toggle(MenuEntry.Quit));
        }

        [Test]
        public void Reset_PutsEveryFlagBack_AndClosesTheMenu()
        {
            var options = new ViewOptions();
            foreach (var entry in GameMenu.Entries.Where(ViewOptions.IsFlag)) { options.Toggle(entry); }
            options.ToggleMenu();
            var menu = new List<bool>();
            options.MenuChanged += menu.Add;

            options.Reset();

            Assert.IsFalse(options.Wireframe);
            Assert.IsTrue(options.Textured);
            Assert.IsFalse(options.BackFaceCulling);
            Assert.IsTrue(options.DrawFps);
            Assert.IsFalse(options.MenuOpen, "Root.vb's Reset: bMenu = False");
            CollectionAssert.AreEqual(new[] { false }, menu);
        }

        [Test]
        public void Lines_NameWhatTheirKeyDoesNow()
        {
            var options = new ViewOptions();
            CollectionAssert.AreEqual(
                new[] { "F1 - Turn to Wireframe", "F2 - Use Shaded", "F3 - Enable Culling", "F4 - Hide FPS", "F12 - Configure Video", "ESC - Quit" },
                GameMenu.Entries.Select(e => GameMenu.Line(e, options)).ToArray(),
                "renderMenu at the start");

            foreach (var entry in GameMenu.Entries.Where(ViewOptions.IsFlag)) { options.Toggle(entry); }

            CollectionAssert.AreEqual(
                new[] { "F1 - Turn to Solid", "F2 - Use Textured", "F3 - Disable Culling", "F4 - Show FPS", "F12 - Configure Video", "ESC - Quit" },
                GameMenu.Entries.Select(e => GameMenu.Line(e, options)).ToArray(),
                "each line follows its flag; F3's is the right way round (BIBLE D22)");
        }

        // --- F12: frmConfig.vb (UNI-T59) ------------------------------------------------------------

        private static ConfigForm NewForm() => new ConfigForm(Hud.Config);

        [Test]
        public void HudJson_HoldsFrmConfig()
        {
            var config = Hud.Config;

            Assert.AreEqual(new Vec2(296f, 262f), config.Size, "ClientSize");
            CollectionAssert.AreEqual(new[] { "640 x 480", "800 x 600", "1024 x 768" }, config.Resolutions.Select(x => x.Label).ToArray(), "cbo_resolution's items");
            CollectionAssert.AreEqual(
                new[] { new DisplayResolution(640, 480), new DisplayResolution(800, 600), new DisplayResolution(1024, 768) },
                config.Resolutions.Select(x => x.Size).ToArray());
            Assert.AreEqual(WindowMode.Windowed, config.DefaultWindowMode, "btnDefault: opt_window");
            Assert.AreEqual(0, config.DefaultResolution, "btnDefault: Items.Item(0)");
        }

        [Test]
        public void HudJson_RejectsADefaultResolutionNotInTheList()
        {
            var shipped = new DirectorySettingsSource(GameSettingsTests.ShippedFolder);
            var source = new MemorySettingsSource(shipped).Set(
                GameSettings.HudFile,
                shipped.Read(GameSettings.HudFile).Replace("\"resolution\": \"640 x 480\"", "\"resolution\": \"320 x 200\"")
                    .Replace("\"windowMode\": \"Windowed\"", "\"windowMode\": \"Maximized\""));

            var e = Assert.Throws<SettingsException>(() => GameSettings.Load(source));

            Assert.IsTrue(e.Errors.Any(x => x.Contains("hud.json.config.default") && x.Contains("'resolution'")), string.Join("\n", e.Errors));
            Assert.IsTrue(e.Errors.Any(x => x.Contains("hud.json.config.default") && x.Contains("'windowMode'")), string.Join("\n", e.Errors));
        }

        [Test]
        public void Config_Load_ShowsTheProfile_AndOnlyFullscreenEnablesTheResolution()
        {
            var form = NewForm();
            var preferences = new PlayerPreferences
            {
                WindowMode = WindowMode.Fullscreen,
                Resolution = new DisplayResolution(1024, 768),
                MusicVolume = 0.5f,
                SfxVolume = 0.25f,
                AmbientVolume = 0.75f,
            };

            form.Load(preferences, fullScreenNow: false);

            Assert.IsTrue(form.Fullscreen);
            Assert.IsTrue(form.ResolutionEnabled, "fra_res is enabled in fullscreen");
            Assert.AreEqual("1024 x 768", form.Resolution.Label);
            Assert.AreEqual(0.5f, form.MusicVolume);
            Assert.AreEqual(0.25f, form.SfxVolume);
            Assert.AreEqual(0.75f, form.AmbientVolume);

            form.SetFullscreen(false);
            Assert.IsFalse(form.ResolutionEnabled, "opt_window: fra_res.Enabled = False");
            Assert.IsFalse(form.SelectResolution(1), "a disabled list cannot be picked");
            Assert.AreEqual(2, form.ResolutionIndex);

            // No window mode in the profile: the screen's own; a size not in the list: the default one.
            form.Load(new PlayerPreferences { Resolution = new DisplayResolution(1920, 1080) }, fullScreenNow: true);
            Assert.IsTrue(form.Fullscreen);
            Assert.AreEqual(0, form.ResolutionIndex);
            form.Load(new PlayerPreferences { WindowMode = WindowMode.Borderless }, fullScreenNow: false);
            Assert.IsTrue(form.Fullscreen, "borderless counts as fullscreen");
        }

        [Test]
        public void Config_Default_IsWindowed640x480_AndLeavesTheSoundAlone()
        {
            var form = NewForm();
            form.Load(new PlayerPreferences { WindowMode = WindowMode.Fullscreen, Resolution = new DisplayResolution(800, 600), MusicVolume = 0.4f }, false);

            form.Default();

            Assert.IsFalse(form.Fullscreen, "opt_window.Checked = True");
            Assert.AreEqual(new DisplayResolution(640, 480), form.Resolution.Size, "Items.Item(0)");
            Assert.AreEqual(0.4f, form.MusicVolume, "btnDefault never touched the Sound group");
        }

        [Test]
        public void Config_Submit_WritesTheModeResolutionAndAllThreeVolumes()
        {
            var form = NewForm();
            var preferences = new PlayerPreferences { SfxVolume = 0.5f };
            form.Load(preferences, fullScreenNow: false);
            var changes = 0;
            form.Changed += () => changes++;

            form.SetFullscreen(true);
            Assert.IsTrue(form.SelectResolution(1));
            form.SetMusicVolume(0.2f);
            form.SetSfxVolume(2f);
            form.SetAmbientVolume(0.6f);
            Assert.AreEqual(5, changes);

            form.Submit(preferences);

            Assert.AreEqual(WindowMode.Fullscreen, preferences.WindowMode);
            Assert.AreEqual(new DisplayResolution(800, 600), preferences.Resolution);
            Assert.AreEqual(0.2f, preferences.MusicVolume);
            Assert.AreEqual(1f, preferences.SfxVolume, "clamped to a gain");
            Assert.AreEqual(0.6f, preferences.AmbientVolume);

            form.SetFullscreen(false);
            form.Submit(preferences);
            Assert.AreEqual(WindowMode.Windowed, preferences.WindowMode);
        }
    }
}
