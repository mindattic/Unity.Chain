using System.Collections;
using System.Reflection;
using MindAttic.Chain.Core;
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
    /// real update loop (the authority stepping the world, flight, grapple, HUD) both runs without throwing and
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
            var spawner = Object.FindAnyObjectByType<EnemySpawner>();
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
            Assert.IsNotNull(spawner, "EnemySpawner missing from Main scene.");
            Assert.IsNotNull(chainAudio, "ChainAudio missing from Main scene.");
            Assert.IsNotNull(session, "GameSession missing from Main scene.");
            Assert.IsNotNull(hud, "GameHud missing from Main scene.");
            Assert.IsNotNull(music, "MusicPlayer missing from Main scene.");
            Assert.IsNotNull(music.Music, "MusicPlayer has no track assigned (UNI-T20: music.wma converted to music.ogg).");

            // Presence is not enough: these are the references each component falls back to
            // silently skipping its work without, so a scene that merely contains the right
            // component types but never actually wires them would still pass the checks above.
            Assert.IsNotNull(GetPrivateField(angel, "spriteRenderer"), "Angel's SpriteRenderer is not wired into AngelController.");
            var handSocket = GetPrivateField(angel, "handSocketTransform") as Transform;
            Assert.IsNotNull(handSocket, "Angel's HandSocket transform is not wired into AngelController.");
            Assert.AreEqual(chain, angel.Chain, "AngelController is not wired to its Chain.");
            Assert.AreEqual(levelBounds, session.Level, "GameSession is not wired to the scene's LevelBounds.");
            Assert.AreEqual(levelBounds, rig.Level, "CameraRig is not wired to the scene's LevelBounds.");
            Assert.AreEqual(chain, grapple.Chain, "GrappleController is not beside the Chain.");
            Assert.AreEqual(chain, chainAudio.Chain, "ChainAudio is not wired to the Chain.");
            Assert.IsNotNull(chainAudio.SfxSource, "ChainAudio has no AudioSource to play through.");

            // UNI-T20: the exact imported counts, not just "non-empty" -- a renamed or missing sfx
            // file would otherwise still pass with a silently shorter clip set.
            Assert.AreEqual(7, chainAudio.GetClips(ChainSound.Swing).Length, "ChainAudio does not have all 7 swing clips (sword_swing_1-7).");
            Assert.AreEqual(3, chainAudio.GetClips(ChainSound.Impact).Length, "ChainAudio does not have all 3 impact clips (sword_hit_1-3).");
            Assert.AreEqual(4, chainAudio.GetClips(ChainSound.EnemyDeath).Length, "ChainAudio does not have all 4 death clips (death_1-4).");
            Assert.AreEqual(1, chainAudio.GetClips(ChainSound.GrappleCatch).Length, "ChainAudio has no grapple-catch clip (defend.wav).");
            Assert.AreEqual(1, chainAudio.GetClips(ChainSound.GrappleThrow).Length, "ChainAudio has no grapple-throw clip (throw.wav, UNI-T71).");
            Assert.AreEqual(angel, session.Angel, "GameSession is not wired to the AngelController.");
            Assert.AreEqual(session, spawner.Session, "EnemySpawner is not wired to the GameSession.");
            Assert.AreEqual(session, hud.Session, "GameHud is not wired to the GameSession.");
            Assert.AreEqual(grapple, hud.Grapple, "GameHud is not wired to the GrappleController.");

            // U.CHN-T61: Form1.vb's scripter, drawn on the HUD and wired to what its scripts change.
            var scripter = Object.FindAnyObjectByType<ScripterConsole>();
            Assert.IsNotNull(scripter, "ScripterConsole missing from Main scene.");
            Assert.AreEqual(session, scripter.Session, "ScripterConsole is not wired to the GameSession.");
            Assert.AreEqual(hud, scripter.Hud, "ScripterConsole is not wired to the GameHud.");
            Assert.AreEqual(rig, scripter.Rig, "ScripterConsole is not wired to the CameraRig.");
            Assert.IsNotNull(scripter.SceneLight, "ScripterConsole is not wired to the SceneLight.");
            Assert.IsFalse(scripter.IsOpen, "bScripter starts False.");

            // UNI-T40: Background.vb's quad on the beach, Cube.vb's brick room behind it, every gfx/area picture wired.
            var backdrop = Object.FindAnyObjectByType<AreaBackdrop>();
            Assert.IsNotNull(backdrop, "AreaBackdrop missing from Main scene.");
            Assert.AreEqual(rig, backdrop.Rig, "AreaBackdrop is not wired to the CameraRig.");
            Assert.AreEqual("beach", backdrop.Current, "Textures.vb: texBackground = areaBeach at the start.");
            Assert.IsNotNull(backdrop.Backdrop, "AreaBackdrop has no quad to draw the area on.");
            Assert.AreEqual(backdrop.SpriteFor("beach"), backdrop.Backdrop.sprite, "The backdrop does not show beach.png.");
            Assert.IsNotNull(backdrop.Room, "AreaBackdrop has no brick room.");
            Assert.IsNotNull(backdrop.Room.sprite, "The brick room has no brick.jpg.");
            foreach (var name in session.Settings.Area.Areas)
            {
                Assert.IsNotNull(backdrop.SpriteFor(name), $"area.json's {name} has no picture wired.");
            }

            // UNI-T41: Form1.vb's U gems, every gfx/chain link picture wired under its chain.json name.
            var skins = chain.GetComponent<LinkSkinView>();
            Assert.IsNotNull(skins, "LinkSkinView missing from the Chain.");
            foreach (var name in session.Settings.Chain.Skins.Skins)
            {
                Assert.IsNotNull(skins.SpriteFor(name), $"chain.json's {name} skin has no picture wired.");
            }

            // UNI-T43: Chain.vb's theMaceMode as a skin, every mode's picture wired, the visual starting in chain.json's mode.
            var maceSkins = chain.GetComponent<MaceController>();
            foreach (var mode in MaceModes.All)
            {
                Assert.IsNotNull(maceSkins.SpriteFor(mode), $"chain.json's {mode} mace skin has no picture wired.");
            }

            Assert.AreSame(
                maceSkins.SpriteFor(session.Settings.Chain.MaceSkins.Mode),
                maceSkins.MaceVisual.GetComponent<SpriteRenderer>().sprite,
                "the mace starts in chain.json's maceSkins.mode");

            var line = chain.GetComponent<LineRenderer>();
            Assert.IsNotNull(line, "Chain has no LineRenderer.");
            Assert.IsNotNull(line.sharedMaterial, "Chain's LineRenderer has no material.");

            // The one authority seated the angel and its chain as player 0, and started the world.
            Assert.AreEqual(session, angel.Session, "The angel was not seated by the GameSession.");
            Assert.AreEqual(session, chain.Session, "The chain was not seated by the GameSession.");
            Assert.AreEqual(angel.Seat, chain.Player, "The chain is not the angel's player's chain.");

            var startingMacePosition = chain.GetLinkPosition(mace.MaceIndex);

            // The game's real loop: the session stepping the world, the chain view, the HUD
            // refreshing its UI Toolkit tree. None of this should throw across several physics steps.
            for (var i = 0; i < 10; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            yield return null;

            // Start began the world: a fresh flock, each enemy shown by the spawner (some may already
            // have been hit by the falling mace, so count what the world and the views agree on).
            Assert.Greater(session.World.Enemies.Count, 0, "The world did not spawn the flock.");
            Assert.AreEqual(session.World.Enemies.Count, spawner.Alive, "The spawner does not show the world's enemies.");

            // Confirm the loop did not just run but actually simulated: link 0 tracks the hand
            // every step, and gravity should have pulled the mace link away from wherever
            // it started.
            Assert.Less(
                Vector2.Distance(angel.HandSocket, chain.GetLinkPosition(0)), 0.01f,
                "Link 0 did not track the Angel's hand.");
            Assert.Less(
                Vector2.Distance(handSocket.position, angel.HandSocket), 0.01f,
                "The HandSocket transform does not follow the Angel's hand.");
            Assert.AreEqual(mace.MaceIndex + 1, line.positionCount, "LineRenderer is not drawing the active chain length.");

            // The HUD is a UI Toolkit document built in code, its root in the document's panel,
            // showing the world's HP and score.
            Assert.IsNotNull(hud.Document, "GameHud built no UIDocument.");
            Assert.IsNotNull(hud.Document.panelSettings, "GameHud's UIDocument has no PanelSettings.");
            Assert.IsNotNull(hud.Root.panel, "The HUD root is not in a panel.");
            Assert.AreEqual(HudText.Hp(session.State.Hp), hud.HpText, "The HUD does not show the world's HP.");
            Assert.AreEqual(HudText.Score(session.State.Score), hud.ScoreText, "The HUD does not show the world's score.");
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
