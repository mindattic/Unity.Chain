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

        [Test]
        public void F12_FlipsTheWindowMode()
        {
            var preferences = new PlayerPreferences();

            Assert.AreEqual(WindowMode.Fullscreen, preferences.ToggleScreen(fullScreen: false));
            Assert.AreEqual(WindowMode.Fullscreen, preferences.WindowMode);
            Assert.AreEqual(WindowMode.Windowed, preferences.ToggleScreen(fullScreen: true));
            Assert.AreEqual(WindowMode.Windowed, preferences.WindowMode);
        }
    }
}
