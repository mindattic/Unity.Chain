using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MindAttic.Chain.Tests
{
    /// <summary>
    /// Play-mode smoke test for UNI-T16: the repo had no scene at all, so UNI-T12's end-to-end
    /// pass could not launch anything. Loads the registered Main scene, checks every piece it is
    /// supposed to wire together is present *and actually connected* (a component that is present
    /// but holds a null reference it guards with "if (x == null) return;" would otherwise pass
    /// this test while doing nothing), and runs a handful of physics steps to confirm the game's
    /// real update loop (chain stepping, flight, grapple, HUD) both runs without throwing and
    /// visibly does something.
    /// </summary>
    public sealed class MainSceneSmokeTests
    {
        [UnityTest]
        public IEnumerator MainScene_LoadsAndRunsWithoutErrors()
        {
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);

            var levelBounds = Object.FindAnyObjectByType<LevelBounds>();
            var rig = Object.FindAnyObjectByType<CameraRig>();
            var angel = Object.FindAnyObjectByType<AngelController>();
            var chain = Object.FindAnyObjectByType<ChainController>();
            var mace = Object.FindAnyObjectByType<MaceController>();
            var grapple = Object.FindAnyObjectByType<GrappleController>();
            var hitDetector = Object.FindAnyObjectByType<MaceHitDetector>();
            var chainAudio = Object.FindAnyObjectByType<ChainAudio>();
            var session = Object.FindAnyObjectByType<GameSession>();
            var hud = Object.FindAnyObjectByType<GameHud>();
            var music = Object.FindAnyObjectByType<MusicPlayer>();

            Assert.IsNotNull(levelBounds, "LevelBounds missing from Main scene.");
            Assert.IsNotNull(rig, "CameraRig missing from Main scene.");
            Assert.IsNotNull(angel, "AngelController missing from Main scene.");
            Assert.IsNotNull(chain, "ChainController missing from Main scene.");
            Assert.IsNotNull(mace, "MaceController missing from Main scene.");
            Assert.IsNotNull(grapple, "GrappleController missing from Main scene.");
            Assert.IsNotNull(hitDetector, "MaceHitDetector missing from Main scene.");
            Assert.IsNotNull(chainAudio, "ChainAudio missing from Main scene.");
            Assert.IsNotNull(session, "GameSession missing from Main scene.");
            Assert.IsNotNull(hud, "GameHud missing from Main scene.");
            Assert.IsNotNull(music, "MusicPlayer missing from Main scene.");

            // Presence is not enough: these are the references each component falls back to
            // silently skipping its work without, so a scene that merely contains the right
            // component types but never actually wires them would still pass the checks above.
            Assert.IsNotNull(GetPrivateField(angel, "spriteRenderer"), "Angel's SpriteRenderer is not wired into AngelController.");
            var handSocket = GetPrivateField(angel, "handSocketTransform") as Transform;
            Assert.IsNotNull(handSocket, "Angel's HandSocket transform is not wired into AngelController.");
            Assert.AreEqual(handSocket, chain.HandSocket, "ChainController is not pinned to the Angel's HandSocket.");
            Assert.AreEqual(levelBounds, chain.LevelBounds, "ChainController is not wired to the scene's LevelBounds.");
            Assert.AreEqual(levelBounds, rig.Level, "CameraRig is not wired to the scene's LevelBounds.");
            Assert.AreEqual(chain, grapple.Chain, "GrappleController is not wired to the Chain.");
            Assert.AreEqual(chain, hitDetector.Chain, "MaceHitDetector is not wired to the Chain.");
            Assert.AreEqual(grapple, hitDetector.Grapple, "MaceHitDetector is not wired to the GrappleController.");
            Assert.AreEqual(chain, chainAudio.Chain, "ChainAudio is not wired to the Chain.");
            Assert.AreEqual(grapple, chainAudio.Grapple, "ChainAudio is not wired to the GrappleController.");
            Assert.AreEqual(hitDetector, chainAudio.HitDetector, "ChainAudio is not wired to the MaceHitDetector.");
            Assert.IsNotNull(chainAudio.SfxSource, "ChainAudio has no AudioSource to play through.");
            Assert.AreEqual(angel, session.Angel, "GameSession is not wired to the AngelController.");
            Assert.AreEqual(hitDetector, session.MaceHits, "GameSession is not wired to the MaceHitDetector.");
            Assert.AreEqual(session, hud.Session, "GameHud is not wired to the GameSession.");
            Assert.AreEqual(grapple, hud.Grapple, "GameHud is not wired to the GrappleController.");

            var line = chain.GetComponent<LineRenderer>();
            Assert.IsNotNull(line, "Chain has no LineRenderer.");
            Assert.IsNotNull(line.sharedMaterial, "Chain's LineRenderer has no material.");

            var startingMacePosition = chain.GetLinkPosition(mace.MaceIndex);

            // The game's real loop: chain FixedUpdate stepping, grapple/hit-detector steps, HUD
            // OnGUI. None of this should throw across several physics steps.
            for (var i = 0; i < 10; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            yield return null;

            // Confirm the loop did not just run but actually simulated: link 0 tracks the hand
            // socket every step, and gravity should have pulled the mace link away from wherever
            // it started.
            Assert.Less(
                Vector2.Distance(handSocket.position, chain.GetLinkPosition(0)), 0.01f,
                "Link 0 did not track the Angel's HandSocket.");
            Assert.AreEqual(mace.MaceIndex + 1, line.positionCount, "LineRenderer is not drawing the active chain length.");
            Assert.Greater(
                Vector2.Distance(startingMacePosition, chain.GetLinkPosition(mace.MaceIndex)), 0.0001f,
                "The mace link did not move: the chain simulation does not appear to be running.");
        }

        private static object GetPrivateField(object target, string name)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"{target.GetType().Name} has no field named '{name}'.");
            return field.GetValue(target);
        }
    }
}
