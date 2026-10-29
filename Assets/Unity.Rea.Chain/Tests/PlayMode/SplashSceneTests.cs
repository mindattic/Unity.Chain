using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace MindAttic.Chain.Tests
{
    /// <summary>
    /// UNI-T58: Form1.vb Startup's loading picture. The Splash scene (the one a build opens on) shows one of the twelve
    /// load640_&lt;n&gt; pictures over the whole window, loads Main under it, and is gone once Main is in.
    /// </summary>
    public sealed class SplashSceneTests
    {
        [Test]
        public void EveryLoadingPicture_LoadsAt640x480()
        {
            var screen = SettingsStore.Current.Hud.Loading;
            Assert.AreEqual(12, screen.Pictures, "getRndNum(1, 12)");
            for (var n = 1; n <= screen.Pictures; n++)
            {
                var picture = SplashScreen.LoadPicture(screen, n);
                Assert.IsNotNull(picture, screen.PictureName(n));
                Assert.AreEqual(640, picture.width, screen.PictureName(n));
                Assert.AreEqual(480, picture.height, screen.PictureName(n));
            }
        }

        [UnityTest]
        public IEnumerator SplashScene_ShowsARandomPicture_ThenLoadsMain()
        {
            yield return SceneManager.LoadSceneAsync("Splash", LoadSceneMode.Single);

            var splash = Object.FindAnyObjectByType<SplashScreen>();
            Assert.IsNotNull(splash, "SplashScreen missing from the Splash scene.");
            Assert.AreEqual("Main", splash.NextScene);
            Assert.That(splash.PictureNumber, Is.InRange(1, 12), "getRndNum(1, 12)");
            Assert.IsNotNull(splash.Picture, "The chosen picture did not load.");

            yield return null;

            // The picture fills the window on a panel of its own, over everything.
            var element = splash.PictureElement;
            Assert.IsNotNull(element.panel, "The picture is not in a panel.");
            Assert.AreEqual(splash.Picture, element.resolvedStyle.backgroundImage.texture, "The panel does not draw the chosen picture.");
            Assert.AreEqual(Position.Absolute, element.resolvedStyle.position);
            Assert.AreEqual(element.panel.visualTree.layout.width, element.layout.width, 0.5f, "The picture does not fill the window's width.");
            Assert.AreEqual(element.panel.visualTree.layout.height, element.layout.height, 0.5f, "The picture does not fill the window's height.");

            // Main loads under it; the picture is gone once Main is in.
            for (var frame = 0; frame < 1200 && splash != null; frame++)
            {
                yield return null;
            }

            Assert.IsTrue(splash == null, "The splash was never taken down.");
            Assert.AreEqual("Main", SceneManager.GetActiveScene().name, "The splash did not load Main.");
            Assert.IsNotNull(Object.FindAnyObjectByType<GameSession>(), "Main's GameSession is missing.");
            Assert.IsNull(Object.FindAnyObjectByType<SplashScreen>(), "A SplashScreen outlived the load.");
        }
    }
}
