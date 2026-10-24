using System.Linq;
using MindAttic.Chain.Core;
using NUnit.Framework;

namespace MindAttic.Chain.Tests
{
    /// <summary>
    /// The HUD's data and rules (UNI-T50): hud.json holds Gui.vb's and renderDisplay's layout in the original
    /// window's pixels and Write's colours, the frame counter samples like renderFPS, and the Res/Grav/Spring readout
    /// maps the chain's live tuning back to the original's own figures.
    /// </summary>
    public class HudTests
    {
        private static ISettingsSource Shipped => new DirectorySettingsSource(GameSettingsTests.ShippedFolder);

        private static GameSettings Load() => GameSettings.Load(Shipped);

        private static void AreEqual(Argb expected, Argb actual, string what)
        {
            Assert.AreEqual(expected.ToString(), actual.ToString(), what);
        }

        [Test]
        public void HudJson_HoldsGuiVbAndRenderDisplay()
        {
            var hud = Load().Hud;

            Assert.AreEqual(new Vec2(512f, 448f), hud.Screen);
            Assert.AreEqual(new Vec2(0f, 0f), hud.StatusBoxPosition, "statOffsetX/Y");
            Assert.AreEqual(new Vec2(96f, 48f), hud.StatusBoxSize, "statWidth x statHeight");

            Assert.AreEqual(new Vec2(20f, 10f), hud.Hp.Position);
            Assert.AreEqual(new Vec2(10f, 30f), hud.Score.Position);
            Assert.AreEqual(new Vec2(50f, 50f), hud.Readout.Position);
            Assert.AreEqual(new Vec2(20f, 120f), hud.Grappled.Position);
            Assert.AreEqual(HudAnchor.TopRight, hud.GameSpeed.Anchor);
            Assert.AreEqual(new Vec2(50f, 20f), hud.GameSpeed.Position, "rTarget.Width - 50, 20");
            Assert.AreEqual(HudAnchor.TopRight, hud.Fps.Anchor);
            Assert.AreEqual(new Vec2(50f, 4f), hud.Fps.Position, "rTarget.Width - 50, 4");
            Assert.AreEqual(HudAnchor.Center, hud.Pause.Anchor, "512 / 2, 448 / 2");

            AreEqual(new Argb(255, 255, 0, 0), hud.Hp.Colour, "HP red");
            AreEqual(new Argb(255, 255, 0, 0), hud.Score.Colour, "score red");
            AreEqual(new Argb(255, 0, 0, 255), hud.Readout.Colour, "readout blue");
            AreEqual(new Argb(255, 255, 255, 0), hud.Grappled.Colour, "grappled yellow");
            AreEqual(new Argb(255, 255, 0, 0), hud.GameSpeed.Colour, "game speed in colorReticle, red");
            AreEqual(new Argb(255, 255, 0, 25), hud.Fps.Colour, "renderFPS's 255, 255, 0, 25");
            AreEqual(new Argb(128, 255, 0, 0), hud.Pause.Colour, "pause half see-through in colorReticle");

            Assert.AreEqual(10f, hud.Hp.FontSize, "fontMenu, Arial 10");
            Assert.IsTrue(hud.Hp.Bold);
            Assert.AreEqual(24f, hud.Pause.FontSize, "fontPause, Arial Narrow 24");
            Assert.IsFalse(hud.Pause.Bold);
            Assert.AreEqual(10f * 96f / 72f, hud.Hp.FontPixels, 1e-5f, "points at 96 dpi");

            Assert.AreEqual(100, hud.FpsSampleMs, "iFPSProfileSpeed");
            Assert.IsTrue(hud.DrawFps, "bDrawFPS starts True");
        }

        [Test]
        public void HudJson_RejectsABadColour_NamingTheKey()
        {
            var source = new MemorySettingsSource(Shipped).Set(
                GameSettings.HudFile,
                Shipped.Read(GameSettings.HudFile).Replace("\"colour\": [255, 0, 0, 255]", "\"colour\": [255, 0, 300]"));

            var e = Assert.Throws<SettingsException>(() => GameSettings.Load(source));

            Assert.IsTrue(e.Errors.Any(x => x.Contains("hud.json.readout") && x.Contains("'colour'")), string.Join("\n", e.Errors));
        }

        [Test]
        public void FrameRateCounter_SamplesEvery100Ms_InStepsOf10()
        {
            var counter = new FrameRateCounter(100);
            Assert.AreEqual(0, counter.FrameRate);

            counter.Frame(100); // the first sample (of nothing), then this frame counts
            for (var i = 1; i <= 5; i++)
            {
                counter.Frame(100 + i * 16);
                Assert.AreEqual(0, counter.FrameRate, "the rate holds between samples");
            }

            counter.Frame(200);
            Assert.AreEqual(60, counter.FrameRate, "6 frames in 100 ms x (1000 / 100)");

            counter.Frame(300);
            Assert.AreEqual(10, counter.FrameRate, "one frame since: 10 fps");
        }

        [Test]
        public void ChainReadout_ShowsChainJsonsOwnFigures()
        {
            var settings = Load();
            var readout = ChainReadout.From(settings.Player.Chain, settings.Mapping);

            Assert.AreEqual(0.25f, readout.Resistance, 1e-7f);
            Assert.AreEqual(-0.0025f, readout.Gravity, 1e-9f);
            Assert.AreEqual(0.1f, readout.Spring, 1e-7f);
        }
    }
}
