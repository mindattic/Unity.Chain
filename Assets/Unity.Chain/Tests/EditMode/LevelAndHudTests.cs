using System.Collections.Generic;
using MindAttic.Chain;
using MindAttic.Chain.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace MindAttic.Chain.Tests
{
    public class LevelComponentTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _objects)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }

            _objects.Clear();
        }

        private GameObject Track(GameObject go)
        {
            _objects.Add(go);
            return go;
        }

        private LevelBounds MakeLevel(Vector2 center, Vector2 size)
        {
            var go = Track(new GameObject("Level"));
            go.transform.position = center;
            var level = go.AddComponent<LevelBounds>();
            level.Size = size;
            return level;
        }

        [Test]
        public void LevelBounds_IsCenteredOnTransformPlusOffset()
        {
            var level = MakeLevel(new Vector2(10f, 5f), new Vector2(20f, 10f));
            level.Offset = new Vector2(1f, -1f);

            Assert.AreEqual(new Rect(1f, -1f, 20f, 10f), level.Bounds);
            Assert.AreEqual(new Rect(2f, 0f, 18f, 8f), level.Inset(new Vector2(1f, 1f)));
            Assert.IsTrue(level.Contains(new Vector2(11f, 4f)));
            Assert.AreEqual(new Vector2(21f, -1f), level.Clamp(new Vector2(99f, -99f)));
        }

        [Test]
        public void AngelController_ClampsToLevelInsetByMargin()
        {
            var level = MakeLevel(Vector2.zero, new Vector2(30f, 12f));
            var angel = Track(new GameObject("Angel")).AddComponent<AngelController>();
            angel.LevelBounds = level;

            // Default margin is (0.5, 0.6).
            var b = angel.Bounds;
            Assert.AreEqual(-14.5f, b.xMin, 1e-4f);
            Assert.AreEqual(14.5f, b.xMax, 1e-4f);
            Assert.AreEqual(-5.4f, b.yMin, 1e-4f);
            Assert.AreEqual(5.4f, b.yMax, 1e-4f);
        }

        [Test]
        public void Chain_BouncesLinksOffTheLevel_UnlessKeepInLevelIsOff()
        {
            using (var rig = new TestRig(levelSize: new Vector2(4f, 2f)))
            {
                rig.MoveAngelTo(new Vector2(0f, 0.9f));
                // The longest chain (12 links) hangs about two units at chain.json's figures, past the floor.
                while (rig.Mace.Lengthen())
                {
                }

                for (var i = 0; i < 200; i++)
                {
                    rig.Step();
                }

                for (var i = 1; i < rig.Chain.LinkCount; i++)
                {
                    Assert.GreaterOrEqual(rig.Chain.GetLinkPosition(i).y, -1f - 1e-4f, $"link {i} fell through the floor");
                }

                rig.Chain.KeepInLevel = false;
                for (var i = 0; i < 200; i++)
                {
                    rig.Step();
                }

                Assert.Less(rig.Mace.MacePosition.y, -1f, "with bounds off the chain hangs past the floor");
            }
        }

        private CameraRig MakeCamera(float aspect)
        {
            var go = Track(new GameObject("Camera"));
            go.transform.position = new Vector3(0f, 0f, -10f);
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.aspect = aspect;
            return go.AddComponent<CameraRig>();
        }

        [Test]
        public void CameraRig_FitLevel_ShowsWholeLevelCentered()
        {
            var level = MakeLevel(new Vector2(3f, 2f), new Vector2(16f, 9f));
            var rig = MakeCamera(4f / 3f);
            rig.Level = level;
            rig.Mode = CameraFramingMode.FitLevel;

            rig.Snap();

            Assert.AreEqual(6f, rig.Camera.orthographicSize, 1e-4f);
            Assert.AreEqual(new Vector3(3f, 2f, -10f), rig.transform.position, "z is kept");
            var view = rig.ViewRect;
            Assert.LessOrEqual(view.xMin, level.Bounds.xMin + 1e-4f);
            Assert.GreaterOrEqual(view.xMax, level.Bounds.xMax - 1e-4f);
        }

        [Test]
        public void CameraRig_Follow_TracksTarget_ButNeverShowsPastTheLevel()
        {
            var level = MakeLevel(Vector2.zero, new Vector2(40f, 20f));
            var rig = MakeCamera(16f / 9f);
            var target = Track(new GameObject("Target")).transform;
            rig.Level = level;
            rig.Target = target;
            rig.Mode = CameraFramingMode.Follow;
            rig.FollowSize = 4.5f;
            rig.DeadZoneHalf = new Vector2(1f, 1f);
            rig.Sharpness = 0f;

            target.position = new Vector3(5f, 0.5f, 0f);
            rig.Frame(1f / 60f);
            Assert.AreEqual(4f, rig.transform.position.x, 1e-4f, "dragged to keep the target at the dead-zone edge");
            Assert.AreEqual(0f, rig.transform.position.y, 1e-4f, "inside the dead zone vertically");

            target.position = new Vector3(100f, 100f, 0f);
            rig.Frame(1f / 60f);
            var view = rig.ViewRect;
            Assert.AreEqual(level.Bounds.xMax, view.xMax, 1e-3f);
            Assert.AreEqual(level.Bounds.yMax, view.yMax, 1e-3f);
        }

        [Test]
        public void CameraRig_Follow_SmoothsTowardTheGoal()
        {
            var level = MakeLevel(Vector2.zero, new Vector2(40f, 20f));
            var rig = MakeCamera(16f / 9f);
            var target = Track(new GameObject("Target")).transform;
            rig.Level = level;
            rig.Target = target;
            rig.Mode = CameraFramingMode.Follow;
            rig.DeadZoneHalf = Vector2.zero;
            rig.Sharpness = 5f;

            target.position = new Vector3(6f, 0f, 0f);
            rig.Frame(0.1f);
            var x = rig.transform.position.x;
            Assert.Greater(x, 0f);
            Assert.Less(x, 6f, "eases in rather than snapping");
        }

        [Test]
        public void CameraRig_SessionReset_RecentresIt()
        {
            using (var testRig = new TestRig())
            {
                var rig = MakeCamera(16f / 9f);
                rig.Level = testRig.Level;
                rig.Target = testRig.Angel.transform;
                rig.Mode = CameraFramingMode.Follow;
                rig.Session = testRig.Session;
                rig.transform.position = new Vector3(6f, -3f, -10f); // a shake or a debug pan

                testRig.Session.Submit(new ResetGame(testRig.Id));

                Assert.AreEqual(new Vector3(0f, 0f, -10f), rig.transform.position, "Root.vb's Reset: cam and target at the origin");
                rig.Session = null;
            }
        }
    }

    public class HudTextTests
    {
        [Test]
        public void Hp_IsOneBarPerHitPoint()
        {
            Assert.AreEqual("HP : |||", HudText.Hp(3));
            Assert.AreEqual("HP : ", HudText.Hp(0));
            Assert.AreEqual("HP : ", HudText.Hp(-4));
        }

        [Test]
        public void Score_And_Grappled_UseInvariantFormatting()
        {
            Assert.AreEqual("Score : 12345", HudText.Score(12345));
            Assert.AreEqual("Grappled: 0.75", HudText.Grappled(0.75f));
            Assert.AreEqual("Grappled: 2", HudText.Grappled(2f));
            Assert.AreEqual("Grappled: 0", HudText.Grappled(-1f));
        }
    }

    public class GameSessionTests
    {
        private TestRig _rig;
        private readonly List<GameEvent> _events = new List<GameEvent>();

        [SetUp]
        public void SetUp()
        {
            _rig = new TestRig();
            _rig.Session.EventRaised += _events.Add;
        }

        [TearDown]
        public void TearDown()
        {
            _rig.Dispose();
            _events.Clear();
        }

        private GameSession Session => _rig.Session;

        [Test]
        public void Join_SeatsTheAngel_WhichDrawsWhereTheWorldFliesIt()
        {
            Assert.AreSame(Session, _rig.Angel.Session);
            Assert.AreEqual(new PlayerId(0), _rig.Angel.Seat);
            Assert.IsNotNull(Session.World.FindPlayer(_rig.Angel.Seat));

            Assert.IsTrue(_rig.Angel.SendInput(new Vector2(100f, -50f), 0f));

            var flown = new Vector2(100f, -50f) * _rig.Player.Setup.Flight.Sensitivity; // angel.json's Ratio3D
            Assert.AreEqual(flown.x, _rig.Player.Flight.Position.X, 1e-5f);
            Assert.AreEqual(flown.y, _rig.Player.Flight.Position.Y, 1e-5f);
            Assert.AreEqual(flown, (Vector2)_rig.Angel.transform.position);
            Assert.Throws<System.ArgumentException>(() => Session.Join(_rig.Angel), "a seat is taken once");
        }

        [Test]
        public void EnemyTouchingTheAngel_CostsHp_ThroughTheHurtDelay()
        {
            _rig.SpawnEnemy(new Vector2(-0.5f, 0f));

            _rig.Step(0.1f);
            Assert.AreEqual(29, Session.State.Hp);
            _rig.Step(0.1f);
            _rig.Step(0.1f);
            Assert.AreEqual(29, Session.State.Hp);
            _rig.Step(0.1f);
            Assert.AreEqual(28, Session.State.Hp);

            var hurt = _events.FindAll(e => e is AngelHurt);
            Assert.AreEqual(2, hurt.Count);
            Assert.AreEqual(_rig.Id, ((AngelHurt)hurt[0]).Player);
        }

        [Test]
        public void MaceHit_IsScored()
        {
            _rig.Swing();
            _rig.SpawnEnemy(_rig.Player.MacePosition.ToUnity());
            _rig.Step();

            var hit = _events.Find(e => e is EnemyHit) as EnemyHit;
            Assert.IsNotNull(hit);
            Assert.Greater(hit.Points, 0);
            Assert.AreEqual(hit.Points, Session.State.Score);
        }

        [Test]
        public void PauseKey_PausesThroughACommand_AndFreezesTheWorld()
        {
            _rig.Angel.SendInput(Vector2.zero, 0f, pausePressed: true);
            Assert.IsTrue(Session.State.IsPaused);
            var paused = _events.Find(e => e is PauseChanged) as PauseChanged;
            Assert.IsNotNull(paused);
            Assert.AreEqual(_rig.Id, paused.Player);

            var time = Session.World.Time;
            Assert.IsFalse(_rig.Angel.SendInput(new Vector2(100f, 0f), 0.1f), "the angel does not fly while paused");
            _rig.Step();
            Assert.AreEqual(time, Session.World.Time);

            _rig.Angel.SendInput(Vector2.zero, 0f, pausePressed: true);
            Assert.IsFalse(Session.State.IsPaused);
        }

        [Test]
        public void Restart_ResetsInPlace()
        {
            Session.State.AddHitScore(2f);
            _rig.MoveAngelTo(new Vector2(2f, 2f));
            Session.Submit(new Pause(_rig.Id, true));

            Session.Restart();

            Assert.AreEqual(0, Session.State.Score);
            Assert.IsTrue(Session.State.IsPlaying);
            Assert.AreEqual(Vector2.zero, _rig.Player.Flight.Position.ToUnity());
            Assert.IsTrue(_events.Exists(e => e is WorldReset));
        }

        [Test]
        public void GameOver_FadesFiveAFrame_ThenResetsItself()
        {
            var state = new GameState();
            state.AddHitScore(2f);
            Assert.IsFalse(state.AdvanceGameOver(), "nothing happens while playing");
            Assert.AreEqual(GameState.FullAmbient, state.Ambient);

            for (var i = 0; i < state.MaxHp; i++) { state.TryHurt(i); }
            Assert.IsTrue(state.IsGameOver);

            Assert.IsFalse(state.AdvanceGameOver());
            Assert.AreEqual(250, state.Ambient);

            var frames = 1;
            while (!state.AdvanceGameOver())
            {
                frames++;
                Assert.Less(frames, 100);
            }

            Assert.AreEqual(50, frames, "255 down by 5 while above 6, then the finished frame");
            Assert.IsTrue(state.IsGameOver, "the fade only reports done; the world resets");
            Assert.AreEqual(GameState.FadeFloor - 1, state.Ambient);
            state.Reset();
            Assert.IsFalse(state.IsGameOver);
            Assert.AreEqual(state.MaxHp, state.Hp);
            Assert.AreEqual(0, state.Score);
            Assert.AreEqual(GameState.FullAmbient, state.Ambient);
        }

        [Test]
        public void Session_GameOver_RestartsWithoutAClick()
        {
            Session.State.Config.HurtDelay = 0f;
            _rig.SpawnEnemy(new Vector2(-0.5f, 0f));
            for (var i = 0; i < 300 && !Session.State.IsGameOver; i++) { _rig.Step(); }
            Assert.IsTrue(Session.State.IsGameOver);
            Assert.IsTrue(_events.Exists(e => e is GameOver));

            for (var i = 0; i < 60 && Session.State.IsGameOver; i++) { _rig.Step(); }

            Assert.IsTrue(_events.Exists(e => e is WorldReset));
            Assert.IsTrue(Session.State.IsPlaying);
            Assert.AreEqual(Session.State.MaxHp, Session.State.Hp);
        }

        [Test]
        public void Hud_ShowsGrappledEnemyHp_OnlyWhileHeld()
        {
            var hud = _rig.Track(new GameObject("Hud")).AddComponent<GameHud>();
            hud.Session = Session;
            hud.Grapple = _rig.Grapple;
            Assert.IsNull(hud.GrappledHp);

            _rig.SpawnEnemy(new Vector2(5f, 5f), hp: 1f);
            _rig.Grapple.SetButtonHeld(true);
            _rig.Step(); // arms; the enemy is far from the mace, so nothing is caught yet
            Assert.IsNull(hud.GrappledHp);

            _rig.SpawnEnemy(_rig.Player.MacePosition.ToUnity(), hp: 0.75f);
            _rig.Step();
            Assert.AreEqual(0.75f, hud.GrappledHp.Value, 1e-5f);
            hud.Refresh();
            Assert.AreEqual(HudText.Grappled(0.75f), hud.GrappledText);
        }

        [Test]
        public void Hud_IsAUiToolkitTree_ShowingHpAndScore_FromTheWorldsEvents()
        {
            var hud = _rig.Track(new GameObject("Hud")).AddComponent<GameHud>();
            hud.Session = Session;
            Assert.IsNotNull(hud.Root, "the HUD root is built in code");
            Assert.AreEqual(PickingMode.Ignore, hud.Root.pickingMode, "the HUD lets clicks through");
            Assert.AreEqual(HudText.Hp(Session.State.Hp), hud.HpText);
            Assert.AreEqual(HudText.Score(0), hud.ScoreText);
            Assert.IsNull(hud.GrappledText, "nothing is grappled");
            Assert.IsNull(hud.BannerText, "no banner while playing");
            Assert.AreEqual(0f, hud.OverlayDim);

            Session.State.Config.HurtDelay = 0f;
            Session.State.TryHurt(0);
            hud.Refresh();
            Assert.AreEqual(HudText.Hp(Session.State.Hp), hud.HpText, "the HP line follows the world");

            var panel = hud.AddPanel(new VisualElement { name = "later-panel" });
            Assert.AreSame(hud.Root, panel.parent, "later panels go on the HUD root");
            Assert.AreEqual(hud.Root.childCount - 1, hud.Root.IndexOf(panel), "and draw over the HUD's own layers");
        }

        [Test]
        public void Hud_ShowsThePauseBanner_OverADimOverlay()
        {
            var hud = _rig.Track(new GameObject("Hud")).AddComponent<GameHud>();
            hud.Session = Session;
            _ = hud.Root;

            Assert.IsTrue(Session.Submit(new Pause(_rig.Id, true)));
            Assert.AreEqual(HudText.Paused, hud.BannerText, "the pause event refreshes the HUD");
            Assert.AreEqual(GameHud.PauseDim, hud.OverlayDim, 1e-5f);

            Assert.IsTrue(Session.Submit(new Pause(_rig.Id, false)));
            Assert.IsNull(hud.BannerText);
            Assert.AreEqual(0f, hud.OverlayDim);
        }

        [Test]
        public void Hud_GameOver_DarkensByTheFade_WithoutThePipelineLight()
        {
            var hud = _rig.Track(new GameObject("Hud")).AddComponent<GameHud>();
            hud.Session = Session;
            _ = hud.Root;
            Session.State.Config.HurtDelay = 0f;
            _rig.SpawnEnemy(new Vector2(-0.5f, 0f));
            for (var i = 0; i < 300 && !Session.State.IsGameOver; i++) { _rig.Step(); }
            Assert.IsTrue(Session.State.IsGameOver);
            _rig.Step();
            hud.Refresh();

            Assert.AreEqual(HudText.GameOver, hud.BannerText);
            Assert.Greater(hud.GameOverDim, 0f, "the fade has begun");
            Assert.AreEqual(hud.GameOverDim, hud.OverlayDim, 1e-5f, "with no scene light the HUD shows the fade");
        }
    }
}
