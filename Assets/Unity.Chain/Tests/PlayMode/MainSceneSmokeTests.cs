using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MindAttic.Chain.Tests
{
    /// <summary>
    /// Play-mode smoke test for UNI-T16: the repo had no scene at all, so UNI-T12's end-to-end
    /// pass could not launch anything. Loads the registered Main scene, checks every piece it is
    /// supposed to wire together is present, and runs a handful of physics steps to make sure the
    /// game's actual update loop (chain stepping, flight, grapple, HUD) does not throw.
    /// </summary>
    public sealed class MainSceneSmokeTests
    {
        [UnityTest]
        public IEnumerator MainScene_LoadsAndRunsWithoutErrors()
        {
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);

            Assert.IsNotNull(Object.FindAnyObjectByType<LevelBounds>(), "LevelBounds missing from Main scene.");
            Assert.IsNotNull(Object.FindAnyObjectByType<CameraRig>(), "CameraRig missing from Main scene.");
            Assert.IsNotNull(Object.FindAnyObjectByType<AngelController>(), "AngelController missing from Main scene.");
            Assert.IsNotNull(Object.FindAnyObjectByType<ChainController>(), "ChainController missing from Main scene.");
            Assert.IsNotNull(Object.FindAnyObjectByType<MaceController>(), "MaceController missing from Main scene.");
            Assert.IsNotNull(Object.FindAnyObjectByType<GrappleController>(), "GrappleController missing from Main scene.");
            Assert.IsNotNull(Object.FindAnyObjectByType<MaceHitDetector>(), "MaceHitDetector missing from Main scene.");
            Assert.IsNotNull(Object.FindAnyObjectByType<ChainAudio>(), "ChainAudio missing from Main scene.");
            Assert.IsNotNull(Object.FindAnyObjectByType<GameSession>(), "GameSession missing from Main scene.");
            Assert.IsNotNull(Object.FindAnyObjectByType<GameHud>(), "GameHud missing from Main scene.");
            Assert.IsNotNull(Object.FindAnyObjectByType<MusicPlayer>(), "MusicPlayer missing from Main scene.");

            // The game's real loop: chain FixedUpdate stepping, grapple/hit-detector steps, HUD
            // OnGUI. None of this should throw across several physics steps.
            for (var i = 0; i < 10; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            yield return null;
        }
    }
}
