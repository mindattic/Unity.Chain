using System.IO;
using System.Linq;
using MindAttic.Chain.Core;
using NUnit.Framework;
using CollectionAssert = NUnit.Framework.Legacy.CollectionAssert;

namespace MindAttic.Chain.Tests
{
    /// <summary>
    /// UNI-T40: Background.vb's quad, Textures.vb's areas, Form1.vb's B and N and Cube.vb's brick room. area.json holds
    /// the original's figures, <see cref="Backdrop"/> places the quad and projects the room's far wall, and
    /// <see cref="AreaSelection"/> starts on the beach, B and N showing beach and utopia, nothing resetting it.
    /// </summary>
    public class BackdropTests
    {
        private static DirectorySettingsSource ShippedSource => new DirectorySettingsSource(GameSettingsTests.ShippedFolder);

        private static GameSettings Shipped => GameSettings.Load(ShippedSource);

        private static SettingsException LoadFails(string from, string to)
        {
            var shipped = ShippedSource;
            var text = shipped.Read(GameSettings.AreaFile);
            Assert.That(text, Does.Contain(from), "the patch must apply");
            var source = new MemorySettingsSource(shipped).Set(GameSettings.AreaFile, text.Replace(from, to));
            return Assert.Throws<SettingsException>(() => GameSettings.Load(source));
        }

        [Test]
        public void AreaJson_HoldsBackgroundVbTexturesVbAndCubeVb()
        {
            var area = Shipped.Area;

            Assert.AreEqual(1.2f, area.BgWidth, "Background.vb: bgWidth = 1.2");
            Assert.AreEqual(0.415f, area.BgHeight, "Background.vb: bgHeight = 0.415");
            Assert.AreEqual(new Vec2(-0.56f, 0f), area.BgOffset, "renderBackground: Matrix.Translation(-0.56, 0, 0)");
            Assert.AreEqual("beach", area.Start, "Textures.vb: texBackground = areaBeach");
            Assert.AreEqual("beach.png", area.File(area.KeyB), "Form1.vb B: areaBeach, gfx/area/beach.png");
            Assert.AreEqual("utopia.png", area.File(area.KeyN), "Form1.vb N: areaUtopia, gfx/area/utopia.png");
            Assert.AreEqual(new Vec2(67f, 55f), area.CubeSize, "Cube.vb: Matrix.Scaling(67, 55, 38)");
            Assert.AreEqual(38f, area.CubeDepth);
            Assert.AreEqual("brick.jpg", area.CubeTexture, "Textures.vb: texCube1 = gfx/brick.jpg");
        }

        [Test]
        public void AreaJson_ListsEveryPictureOfGfxArea_TheUnusedOnesToo()
        {
            var area = Shipped.Area;

            CollectionAssert.AreEquivalent(
                new[] { "beach", "utopia", "landscape", "landscape2", "01", "02", "512" },
                area.Areas.ToArray());
            CollectionAssert.AreEquivalent(
                new[] { "beach.png", "utopia.png", "landscape.jpg", "landscape2.jpg", "01.png", "02.png", "512.jpg" },
                area.Areas.Select(area.File).ToArray());
        }

        [Test]
        public void EveryListedPicture_AndTheBrick_IsImported()
        {
            var assets = Path.GetFullPath(Path.Combine(GameSettingsTests.ShippedFolder, "..", ".."));
            var area = Shipped.Area;
            foreach (var name in area.Areas)
            {
                Assert.IsTrue(File.Exists(Path.Combine(assets, "Unity.Rea.Chain", "Art", "Background", area.File(name))), name);
            }

            Assert.IsTrue(File.Exists(Path.Combine(assets, "Unity.Rea.Chain", "Art", "Room", area.CubeTexture)), area.CubeTexture);
        }

        [Test]
        public void Settings_MapTheQuadAndTheRoomIntoWorldUnits()
        {
            var settings = Shipped;
            var scale = settings.Mapping.LengthScale;
            var quad = settings.Backdrop.Quad;

            Assert.AreEqual(-0.56f * scale, quad.XMin, 1e-4f, "setupVertex's left edge (x 0) at the translation");
            Assert.AreEqual(0.64f * scale, quad.XMax, 1e-4f, "bgWidth across from it");
            Assert.AreEqual(-0.415f * scale, quad.YMin, 1e-4f, "bgHeight below the centre line");
            Assert.AreEqual(0.415f * scale, quad.YMax, 1e-4f, "and above it");
            Assert.AreEqual(new Vec2(67f * scale, 55f * scale), settings.Backdrop.RoomSize);
            Assert.AreEqual(38f * scale, settings.Backdrop.RoomDepth, 1e-4f);
        }

        [Test]
        public void FarWall_FromCameraVbsEye_IsATwentiethOfTheWall()
        {
            // cam (0, 0, -1): the plane is 1 away, the far wall (z 19) 20.
            var wall = Backdrop.FarWall(Vec2.Zero, -1f, new Vec2(67f, 55f), 38f);

            Assert.AreEqual(67f / 20f, wall.Width, 1e-5f);
            Assert.AreEqual(55f / 20f, wall.Height, 1e-5f);
            Assert.AreEqual(0f, wall.Center.X, 1e-6f);
            Assert.AreEqual(0f, wall.Center.Y, 1e-6f);
        }

        [Test]
        public void FarWall_FollowsTheEyeAtItsParallax()
        {
            var wall = Backdrop.FarWall(new Vec2(1f, -0.5f), -1f, new Vec2(67f, 55f), 38f);

            Assert.AreEqual(0.95f, wall.Center.X, 1e-5f, "the wall's centre (x 0) seen from x 1 lands at 1 - 1/20");
            Assert.AreEqual(-0.475f, wall.Center.Y, 1e-5f);
        }

        [Test]
        public void FarWall_ScalesWithTheMapping()
        {
            var original = Backdrop.FarWall(new Vec2(0.2f, 0.1f), -1f, new Vec2(67f, 55f), 38f);
            var mapped = Backdrop.FarWall(new Vec2(1.8f, 0.9f), -9f, new Vec2(603f, 495f), 342f);

            Assert.AreEqual(original.Width * 9f, mapped.Width, 1e-3f);
            Assert.AreEqual(original.Center.X * 9f, mapped.Center.X, 1e-4f);
        }

        [Test]
        public void FarWall_IsNothing_FromAnEyeOnThePlane()
        {
            var wall = Backdrop.FarWall(Vec2.Zero, 0f, new Vec2(67f, 55f), 38f);

            Assert.AreEqual(0f, wall.Width);
            Assert.AreEqual(0f, wall.Height);
        }

        [Test]
        public void Selection_StartsOnTheBeach()
        {
            var selection = new AreaSelection(Shipped.Area);

            Assert.AreEqual("beach", selection.Current);
            Assert.AreEqual("beach.png", selection.CurrentFile);
        }

        [Test]
        public void N_ShowsUtopia_AndB_TheBeachAgain()
        {
            var selection = new AreaSelection(Shipped.Area);
            string raised = null;
            selection.Changed += area => raised = area;

            Assert.IsTrue(selection.Press(AreaKey.N));
            Assert.AreEqual("utopia", selection.Current);
            Assert.AreEqual("utopia", raised);

            Assert.IsTrue(selection.Press(AreaKey.B));
            Assert.AreEqual("beach", selection.Current);
            Assert.AreEqual("beach", raised);
        }

        [Test]
        public void B_OnTheBeach_ChangesNothing()
        {
            // Form1.vb's B only ever sets areaBeach: its second Case Keys.B (bBackground) never runs, so B is no toggle.
            var selection = new AreaSelection(Shipped.Area);
            var raised = 0;
            selection.Changed += _ => raised++;

            Assert.IsFalse(selection.Press(AreaKey.B));
            Assert.IsFalse(selection.Press(AreaKey.B));
            Assert.AreEqual("beach", selection.Current);
            Assert.AreEqual(0, raised);
        }

        [Test]
        public void Show_RefusesAnAreaTheFileDoesNotList()
        {
            var selection = new AreaSelection(Shipped.Area);

            Assert.Throws<System.ArgumentException>(() => selection.Show("moon"));
            Assert.AreEqual("beach", selection.Current);
        }

        [Test]
        public void AreaJson_RejectsAStartThatIsNoArea_NamingIt()
        {
            var e = LoadFails("\"texBackground\": \"beach\"", "\"texBackground\": \"moon\"");

            Assert.IsTrue(e.Errors.Any(x => x.Contains("area.json") && x.Contains("texBackground") && x.Contains("moon")), string.Join("\n", e.Errors));
        }

        [Test]
        public void AreaJson_RejectsAKeyThatIsNoArea()
        {
            var e = LoadFails("\"keyN\": \"utopia\"", "\"keyN\": \"utopa\"");

            Assert.IsTrue(e.Errors.Any(x => x.Contains("keyN")), string.Join("\n", e.Errors));
        }

        [Test]
        public void AreaJson_RejectsAnEmptyFileName()
        {
            var e = LoadFails("\"utopia\": \"utopia.png\"", "\"utopia\": \"\"");

            Assert.IsTrue(e.Errors.Any(x => x.Contains("area.json.areas") && x.Contains("utopia")), string.Join("\n", e.Errors));
        }

        [Test]
        public void AreaJson_RejectsAFlatRoom()
        {
            var e = LoadFails("\"depth\": 38", "\"depth\": 0");

            Assert.IsTrue(e.Errors.Any(x => x.Contains("area.json.cube") && x.Contains("depth")), string.Join("\n", e.Errors));
        }
    }
}
