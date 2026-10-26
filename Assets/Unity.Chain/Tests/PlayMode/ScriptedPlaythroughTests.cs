using System.Collections;
using MindAttic.Chain.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MindAttic.Chain.Tests
{
    /// <summary>
    /// U.CHN-T62: the PlayMode half of "a built player played by a scripted run, captured" -- drives the exact
    /// path <see cref="ScriptedPlaythrough"/> plays for its capture (launch and the loading picture, fly and
    /// swing, grapple and throw, a kill, a few hits, a heart pickup, a game over fade and its automatic reset,
    /// pause, the Tab menu, the scripter console, the F12 settings screen) through the same public API
    /// (<see cref="PlaythroughActions"/>), so the path itself is asserted every run, not just watched once by eye.
    /// </summary>
    public sealed class ScriptedPlaythroughTests
    {
        [UnityTest]
        public IEnumerator ScriptedRun_PlaysLaunchThroughSettings_WithoutErrors()
        {
            // Launch and the loading picture (UNI-T58): the Splash scene the player opens on, which loads Main under it.
            // SplashSceneTests covers the picture itself; this only needs the path to actually reach Main.
            yield return SceneManager.LoadSceneAsync("Splash", LoadSceneMode.Single);
            var splash = Object.FindAnyObjectByType<SplashScreen>();
            Assert.IsNotNull(splash, "SplashScreen missing from the Splash scene.");
            Assert.That(splash.PictureNumber, Is.InRange(1, 12), "getRndNum(1, 12)");

            for (var frame = 0; frame < 1200 && SceneManager.GetActiveScene().name != "Main"; frame++)
            {
                yield return null;
            }

            Assert.AreEqual("Main", SceneManager.GetActiveScene().name, "The splash never loaded Main.");

            var session = Object.FindAnyObjectByType<GameSession>();
            var angel = Object.FindAnyObjectByType<AngelController>();
            var chain = Object.FindAnyObjectByType<ChainController>();
            var mace = Object.FindAnyObjectByType<MaceController>();
            var grapple = Object.FindAnyObjectByType<GrappleController>();
            var hud = Object.FindAnyObjectByType<GameHud>();
            var scripter = Object.FindAnyObjectByType<ScripterConsole>();
            var heart = Object.FindAnyObjectByType<HeartPickup>();

            Assert.IsNotNull(session, "GameSession missing from Main.");
            Assert.IsNotNull(angel, "AngelController missing from Main.");
            Assert.IsNotNull(chain, "ChainController missing from Main.");
            Assert.IsNotNull(mace, "MaceController missing from Main.");
            Assert.IsNotNull(grapple, "GrappleController missing from Main.");
            Assert.IsNotNull(hud, "GameHud missing from Main.");
            Assert.IsNotNull(scripter, "ScripterConsole missing from Main.");
            Assert.IsNotNull(heart, "HeartPickup missing from Main.");

            // Let the flock spawn and the chain hang from the hand before scripting anything, as MainSceneSmokeTests does.
            for (var i = 0; i < 10; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            var player = angel.Seat;

            // Fly and swing: circling mouse deltas really move the mace, not just the angel.
            var maceBeforeSwing = chain.GetLinkPosition(mace.MaceIndex);
            PlaythroughActions.FlyAndSwing(session, player, 40, 0.02f);
            Assert.Greater(
                Vector2.Distance(maceBeforeSwing, chain.GetLinkPosition(mace.MaceIndex)), 0.01f,
                "the mace did not swing");

            // Grapple and throw: a real catch while armed, then a real release.
            var caught = PlaythroughActions.GrappleCatchAndThrow(session, chain, grapple, mace, 0.02f);
            Assert.IsNotNull(caught, "the grapple did not catch the enemy put on the mace");
            Assert.IsFalse(grapple.IsArmed, "releasing the button did not disarm the grapple");

            // A kill: a one-hit-point enemy put right in the mace's path.
            var victim = PlaythroughActions.KillAnEnemyWithTheMace(session, chain, mace, 0.02f);
            Assert.IsFalse(victim.IsAlive, "the mace did not kill the enemy put in its way");

            // A couple of hits: real HP loss, not a game over.
            var hpBeforeHits = session.State.Hp;
            PlaythroughActions.TakeAHit(session, angel, 0.3f);
            PlaythroughActions.TakeAHit(session, angel, 0.3f);
            Assert.Less(session.State.Hp, hpBeforeHits, "no hit cost any HP");
            Assert.IsFalse(session.State.IsGameOver, "a couple of hits should not end the round");

            // A heart: picked up, and it heals.
            var hpBeforeHeart = session.State.Hp;
            PlaythroughActions.CollectTheHeart(session, angel, heart, 0.02f);
            Assert.IsTrue(heart.Rules.PickedUp, "the heart was not picked up");
            Assert.Greater(session.State.Hp, hpBeforeHeart, "picking up the heart did not heal");

            // Pause.
            session.Submit(new TogglePause(player));
            Assert.IsTrue(session.State.IsPaused, "TogglePause did not pause");
            session.Submit(new TogglePause(player));
            Assert.IsFalse(session.State.IsPaused, "TogglePause did not resume");

            // The Tab menu.
            Assert.IsTrue(hud.ToggleMenu(), "Tab did not open the menu");
            Assert.IsTrue(hud.MenuShown, "the menu panel is not shown while open");
            Assert.IsFalse(hud.ToggleMenu(), "Tab did not close the menu");

            // The scripter console: the grave key opens it, a real command runs and takes effect, and Enter closes it.
            Assert.IsFalse(scripter.IsOpen, "the scripter starts closed");
            scripter.Press(ScripterKey.Oemtilde);
            Assert.IsTrue(scripter.IsOpen, "the grave key did not open the scripter");
            scripter.Press(ScripterKey.C);
            scripter.Press(ScripterKey.C); // "CHANGE " then "CHANGE CAMERA "
            scripter.Press(ScripterKey.X);
            scripter.Press(ScripterKey.Space);
            scripter.Press(ScripterKey.D1);
            var result = scripter.Press(ScripterKey.Enter);
            Assert.IsNotNull(result, "Enter did not run 'CHANGE CAMERA X 1'");
            Assert.IsTrue(result.Ran, "'CHANGE CAMERA X 1' did not run");
            Assert.Greater(result.Effects.Count, 0, "the scripter command had no effect");
            Assert.IsFalse(scripter.IsOpen, "Enter did not close the scripter after running the line");

            // The F12 settings screen.
            Assert.IsTrue(hud.ToggleConfig(), "F12 did not open the settings screen");
            Assert.IsTrue(hud.ConfigShown, "the settings screen is not shown while open");
            Assert.IsFalse(hud.ToggleConfig(), "F12 did not close the settings screen");

            // Game over fade and its automatic reset.
            var steps = PlaythroughActions.ForceGameOverByContact(session, angel);
            Assert.Less(steps, 200, "the round never ended from repeated contact");
            Assert.IsTrue(session.State.IsGameOver, "HP running out did not end the round");
            Assert.LessOrEqual(session.State.Hp, 0, "the round ended before HP actually reached 0");

            PlaythroughActions.RunGameOverFadeToReset(session);
            Assert.IsFalse(session.State.IsGameOver, "the fade never finished and reset the round");
            Assert.AreEqual(session.State.MaxHp, session.State.Hp, "the automatic reset did not restore full HP");
            Assert.AreEqual(GameState.FullAmbient, session.State.Ambient, "the automatic reset did not restore full light");
            Assert.Greater(session.World.Enemies.Count, 0, "the automatic reset did not spawn a fresh flock");
        }
    }
}
