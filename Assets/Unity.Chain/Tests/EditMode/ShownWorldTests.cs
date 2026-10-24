using MindAttic.Chain.Core;
using NUnit.Framework;
using UnityEngine;

namespace MindAttic.Chain.Tests
{
    /// <summary>
    /// U.CHN-T73: online as a client, this screen's views draw the remote authority's world (<see cref="GameSession.Shown"/>,
    /// a <see cref="WorldSnapshot"/>) instead of the frozen local one: the angel, chain and mace show the screen's own seat
    /// there, the enemies and hearts are the authority's, and the HUD shows its HP, score, clock, pause and game over.
    /// </summary>
    public class ShownWorldTests
    {
        private static readonly PlayerId Seat = new PlayerId(3);

        private TestRig _rig;
        private GameWorld _authority;

        private GameSession Session => _rig.Session;

        [SetUp]
        public void SetUp()
        {
            _rig = new TestRig();
            Session.Restart();

            // The remote authority: its own world, with the host at seat 0 and this screen's player at seat 3.
            var settings = GameWorld.Settings.Default;
            settings.Enemies.Count = 3;
            _authority = new GameWorld(settings, new System.Random(5));
            _authority.AddPlayer(new PlayerId(0), PlayerSetup.Default);
            _authority.AddPlayer(Seat, PlayerSetup.Default);
            _authority.AddHeart();
            _authority.Reset();
            for (var i = 0; i < 30; i++)
            {
                _authority.Submit(new SteerAngel(Seat, new Vec2(-50f, 20f), 0.02f));
                _authority.Step(0.02f);
            }

            _authority.TakeEvents();
        }

        [TearDown]
        public void TearDown() => _rig.Dispose();

        /// <summary>This screen joins online: its commands are forwarded and its views get the authority's world.</summary>
        private WorldSnapshot JoinAndShow()
        {
            Session.Forward = command => true;
            var snapshot = WorldSnapshot.Capture(_authority, sequence: 1);
            Session.ShowAuthority(snapshot, Seat);
            return snapshot;
        }

        [Test]
        public void NothingIsShown_InLocalPlay_EvenIfASnapshotWasHanded()
        {
            Session.ShowAuthority(WorldSnapshot.Capture(_authority), Seat);
            Assert.IsNull(Session.Shown, "only a screen that forwards its commands shows a remote world");
            Assert.IsFalse(Session.TryGetShownPlayer(out _));

            JoinAndShow();
            Assert.IsNotNull(Session.Shown);
            Session.Forward = null;
            Assert.IsNull(Session.Shown, "dropped as soon as the screen plays locally again");
        }

        [Test]
        public void TheAngelChainAndMace_DrawTheScreensSeat_AsTheAuthorityHasIt()
        {
            var mace = _rig.Track(new GameObject("Mace visual")).transform;
            _rig.Mace.MaceVisual = mace;
            _authority.Submit(new ArmGrapple(Seat));
            _authority.Step(0.02f);
            _authority.TakeEvents();

            var snapshot = JoinAndShow();
            Assert.IsTrue(snapshot.TryGetPlayer(Seat, out var shown));

            _rig.Angel.Apply();
            _rig.Chain.RenderChain(1f);
            _rig.Mace.RenderMace();

            Assert.AreEqual(shown.Angel.X, _rig.Angel.transform.position.x, 1e-5f, "the angel stands where the authority has this seat's");
            Assert.AreEqual(shown.Angel.Y, _rig.Angel.transform.position.y, 1e-5f);
            Assert.AreNotEqual(_rig.Player.AngelPosition.X, _rig.Angel.transform.position.x, "not where the frozen local world has it");

            var line = _rig.Chain.GetComponent<LineRenderer>();
            Assert.AreEqual(shown.Links.Length, line.positionCount, "the chain is drawn hand to mace");
            Assert.AreEqual(shown.Links.Length, _rig.Chain.ActiveLinkCount);
            for (var i = 0; i < shown.Links.Length; i++)
            {
                Assert.AreEqual(shown.Links[i].X, line.GetPosition(i).x, 1e-5f, "link " + i);
                Assert.AreEqual(shown.Links[i].Y, line.GetPosition(i).y, 1e-5f, "link " + i);
            }

            Assert.AreEqual(shown.MaceIndex, _rig.Mace.MaceIndex);
            Assert.AreEqual(shown.Mace.X, mace.position.x, 1e-5f, "the mace sits on the shown chain's last link");
            Assert.AreEqual(shown.Mace.Y, mace.position.y, 1e-5f);
            Assert.IsTrue(_rig.Mace.IsGrappleArmed, "the authority's grapple is armed for this seat");
            Assert.IsFalse(_rig.Player.Grapple.IsArmed, "though the local world's is not");
        }

        [Test]
        public void AScreenSeatMissingFromTheSnapshot_DrawsNoChain()
        {
            Session.Forward = command => true;
            Session.ShowAuthority(WorldSnapshot.Capture(_authority), new PlayerId(9));

            _rig.Chain.RenderChain(1f);
            Assert.AreEqual(0, _rig.Chain.GetComponent<LineRenderer>().positionCount);
        }

        [Test]
        public void TheEnemies_AreTheAuthoritys_WhileShown_AndTheLocalFlockComesBackAfter()
        {
            var spawner = _rig.Track(new GameObject("Spawner")).AddComponent<EnemySpawner>();
            spawner.Session = Session;
            var local = spawner.Alive;
            Assert.Greater(local, 0, "the screen's own world has its flock");
            var localView = spawner.ViewOf(Session.World.Enemies[0].Id);

            var snapshot = JoinAndShow();
            spawner.ShowAuthority();
            Assert.IsTrue(spawner.IsShowingAuthority);
            Assert.AreEqual(snapshot.Enemies.Count, spawner.Alive, "one view for each of the authority's enemies");
            Assert.IsFalse(localView.gameObject.activeSelf, "the frozen local flock is hidden");
            var first = snapshot.Enemies[0];
            var view = spawner.ViewOf(first.Id);
            Assert.IsNotNull(view);
            Assert.AreEqual(first.Position.X, view.transform.position.x, 1e-5f);
            Assert.AreEqual(first.Position.Y, view.transform.position.y, 1e-5f);

            // An enemy gone from the next snapshot loses its view.
            var next = WorldSnapshot.Capture(_authority, sequence: 2);
            next.Enemies.RemoveAt(0);
            Session.ShowAuthority(next, Seat);
            spawner.ShowAuthority();
            Assert.AreEqual(next.Enemies.Count, spawner.Alive);
            Assert.IsNull(spawner.ViewOf(first.Id));

            Session.Forward = null;
            spawner.ShowAuthority();
            Assert.IsFalse(spawner.IsShowingAuthority);
            Assert.AreEqual(local, spawner.Alive, "back to the screen's own flock");
            Assert.IsTrue(localView.gameObject.activeSelf);
        }

        [Test]
        public void AHeart_DrawsTheAuthoritysHeart_InTheSamePlaceInTheList()
        {
            var go = _rig.Track(new GameObject("Heart"));
            var sprite = go.AddComponent<SpriteRenderer>();
            var heart = go.AddComponent<HeartPickup>();
            Session.AddHeart(heart);

            var snapshot = JoinAndShow();
            var picked = snapshot.Hearts[0];
            picked.X += 1.5f;
            picked.PickedUp = true;
            snapshot.Hearts[0] = picked;

            heart.Refresh();
            Assert.AreEqual(picked.X, heart.transform.position.x, 1e-5f);
            Assert.IsFalse(sprite.enabled, "the authority's heart was picked up");
            Assert.IsFalse(heart.IsCollected, "the local world's was not");

            Session.Forward = null;
            heart.Refresh();
            Assert.IsTrue(sprite.enabled, "back to the screen's own heart");
        }

        [Test]
        public void TheHud_ShowsTheAuthoritysGame()
        {
            var hud = _rig.Track(new GameObject("Hud")).AddComponent<GameHud>();
            hud.Session = Session;
            _ = hud.Root;

            var snapshot = JoinAndShow();
            snapshot.Hp = 37;
            snapshot.Score = 1234;
            hud.Refresh();
            Assert.AreEqual(HudText.Hp(37), hud.HpText);
            Assert.AreEqual(HudText.Score(1234), hud.ScoreText);
            Assert.AreEqual(HudText.Clock(snapshot.RoundTime), hud.ClockText);
            Assert.AreEqual(HudText.GameSpeed(snapshot.GameSpeed), hud.GameSpeedText);
            Assert.IsNull(hud.BannerText);

            snapshot.IsPaused = true;
            hud.Refresh();
            Assert.AreEqual(HudText.Paused, hud.BannerText, "the authority paused");
            Assert.IsFalse(Session.State.IsPaused, "this screen's world did not");

            snapshot.IsGameOver = true;
            hud.Refresh();
            Assert.AreEqual(HudText.GameOver, hud.BannerText);
        }
    }
}
