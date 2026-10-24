using System;
using System.IO;
using System.Linq;
using MindAttic.Chain.Core;
using NUnit.Framework;
using CollectionAssert = NUnit.Framework.Legacy.CollectionAssert;

namespace MindAttic.Chain.Tests
{
    /// <summary>
    /// UNI-T58: Form1.vb Startup's loading picture. hud.json's <c>loading</c> holds how many <c>load640_&lt;n&gt;</c>
    /// there are; <see cref="LoadingScreen.Pick"/> is getRndNum(1, 12), every number 1 to 12 inclusive and none other.
    /// </summary>
    public class LoadingScreenTests
    {
        private static LoadingScreen Shipped =>
            GameSettings.Load(new DirectorySettingsSource(GameSettingsTests.ShippedFolder)).Hud.Loading;

        [Test]
        public void HudJson_HoldsStartupsTwelvePictures()
        {
            Assert.AreEqual(12, Shipped.Pictures, "getRndNum(1, 12)");
        }

        [Test]
        public void EveryPicture_IsShippedUnderResources()
        {
            // StreamingAssets/Settings -> Assets
            var assets = Path.GetFullPath(Path.Combine(GameSettingsTests.ShippedFolder, "..", ".."));
            var folder = Path.Combine(assets, "Unity.Chain", "Resources", "UI", "Load");
            var screen = Shipped;
            for (var n = 1; n <= screen.Pictures; n++)
            {
                Assert.IsTrue(File.Exists(Path.Combine(folder, screen.PictureName(n) + ".jpg")), screen.PictureName(n));
            }

            Assert.AreEqual(screen.Pictures, Directory.GetFiles(folder, "*.jpg").Length, "no picture the pick can never reach");
        }

        [Test]
        public void Pick_IsGetRndNum_OneToTwelveInclusive()
        {
            var screen = LoadingScreen.Of(12);
            var random = new Random(58);
            var seen = Enumerable.Range(0, 5000).Select(_ => screen.Pick(random)).ToArray();

            Assert.IsTrue(seen.All(n => n >= 1 && n <= 12), "rndNum.Next(1, 12 + 1)");
            CollectionAssert.AreEquivalent(Enumerable.Range(1, 12), seen.Distinct(), "every picture can come up, 12 included");
        }

        [Test]
        public void Pick_MatchesRootVbsRandomDraw()
        {
            var screen = LoadingScreen.Of(12);

            Assert.AreEqual(new Random(7).Next(1, 13), screen.Pick(new Random(7)));
        }

        [Test]
        public void PictureName_IsTheOriginalsFileStem()
        {
            var screen = LoadingScreen.Of(12);

            Assert.AreEqual("load640_1", screen.PictureName(1));
            Assert.AreEqual("load640_12", screen.PictureName(12));
            Assert.Throws<ArgumentOutOfRangeException>(() => screen.PictureName(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => screen.PictureName(13));
        }

        [Test]
        public void HudJson_RejectsNoPictures()
        {
            var shipped = new DirectorySettingsSource(GameSettingsTests.ShippedFolder);
            var source = new MemorySettingsSource(shipped).Set(
                GameSettings.HudFile,
                shipped.Read(GameSettings.HudFile).Replace("\"pictures\": 12", "\"pictures\": 0"));

            var e = Assert.Throws<SettingsException>(() => GameSettings.Load(source));

            Assert.IsTrue(e.Errors.Any(x => x.Contains("hud.json.loading") && x.Contains("'pictures' must be between 1")), string.Join("\n", e.Errors));
        }
    }
}
