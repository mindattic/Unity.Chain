using System.Collections.Generic;
using MindAttic.Chain;
using MindAttic.Chain.Core;
using NUnit.Framework;
using UnityEngine;

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
        public void ChainController_BouncesLinksOffTheLevel()
        {
            var level = MakeLevel(Vector2.zero, new Vector2(4f, 2f));
            var go = Track(new GameObject("Chain"));
            var chain = go.AddComponent<ChainController>();
            chain.LevelBounds = level;
            chain.Rebuild();

            go.transform.position = new Vector3(0f, 0.9f, 0f);
            for (var i = 0; i < 200; i++)
            {
                chain.Simulate(1f / 50f);
            }

            for (var i = 1; i < chain.LinkCount; i++)
            {
                Assert.GreaterOrEqual(chain.GetLinkPosition(i).y, -1f - 1e-4f, $"link {i} fell through the floor");
            }

            chain.KeepInLevel = false;
            for (var i = 0; i < 200; i++)
            {
                chain.Simulate(1f / 50f);
            }

            Assert.Less(chain.GetLinkPosition(chain.LinkCount - 1).y, -1f, "with bounds off the chain hangs past the floor");
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
        private readonly List<GameObject> _objects = new List<GameObject>();
        private GameSession _session;
        private AngelController _angel;

        [SetUp]
        public void SetUp()
        {
            _angel = Track(new GameObject("Angel")).AddComponent<AngelController>();
            _session = Track(new GameObject("Session")).AddComponent<GameSession>();
            _session.Angel = _angel;
            _session.ContactRadius = 0.35f;
        }

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

        private Enemy SpawnEnemy(Vector2 position)
        {
            var go = Track(new GameObject("Enemy"));
            go.transform.position = position;
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            go.AddComponent<CircleCollider2D>().radius = 0.2f;
            var enemy = go.AddComponent<Enemy>();
            enemy.StartingHp = 1f;
            enemy.FixedMass = 1f;
            Physics2D.SyncTransforms();
            return enemy;
        }

        [Test]
        public void EnemyTouchingAngel_CostsHp_ThroughHurtDelay()
        {
            SpawnEnemy(new Vector2(0.3f, 0f));

            Assert.IsTrue(_session.CheckContact(1f));
            Assert.IsFalse(_session.CheckContact(1.1f));
            Assert.IsTrue(_session.CheckContact(1.3f));
            Assert.AreEqual(28, _session.State.Hp);
        }

        [Test]
        public void FarEnemy_DoesNotHurt()
        {
            SpawnEnemy(new Vector2(3f, 0f));
            Assert.IsFalse(_session.CheckContact(1f));
            Assert.AreEqual(30, _session.State.Hp);
        }

        [Test]
        public void GrabbedEnemy_DoesNotHurt()
        {
            var enemy = SpawnEnemy(new Vector2(0.3f, 0f));
            enemy.OnGrappled();
            Assert.AreEqual(EnemyState.Grabbed, enemy.State);

            Assert.IsFalse(_session.CheckContact(1f));
        }

        [Test]
        public void MaceHit_IsScored()
        {
            var enemy = SpawnEnemy(new Vector2(5f, 5f));
            _session.RegisterHit(enemy, 0.4f);
            Assert.AreEqual(400, _session.State.Score);
        }

        [Test]
        public void Restart_OutsidePlayMode_ResetsInPlace()
        {
            _session.State.AddHitScore(2f);
            _session.SetPaused(true);
            var restarted = 0;
            _session.Restarted += () => restarted++;

            _session.Restart();

            Assert.AreEqual(0, _session.State.Score);
            Assert.IsTrue(_session.State.IsPlaying);
            Assert.AreEqual(1, restarted);
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
            Assert.IsTrue(state.IsGameOver, "the fade only reports done; the session resets");
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
            var restarted = 0;
            _session.Restarted += () => restarted++;
            for (var i = 0; i < 31; i++) { _session.State.TryHurt(i); }
            Assert.IsTrue(_session.State.IsGameOver);

            for (var i = 0; i < 60 && restarted == 0; i++) { _session.StepGameOver(); }

            Assert.AreEqual(1, restarted);
            Assert.IsTrue(_session.State.IsPlaying);
        }

        [Test]
        public void Hud_ShowsGrappledEnemyHp_OnlyWhileHeld()
        {
            var chainGo = Track(new GameObject("Chain"));
            var chain = chainGo.AddComponent<ChainController>();
            chain.Rebuild();
            var grapple = chainGo.AddComponent<GrappleController>();
            grapple.Chain = chain;
            var hud = Track(new GameObject("Hud")).AddComponent<GameHud>();
            hud.Session = _session;
            hud.Grapple = grapple;

            Assert.IsNull(hud.GrappledHp);

            var enemy = SpawnEnemy(new Vector2(5f, 5f));
            grapple.SetButtonHeld(true);
            grapple.Step(); // arms; the enemy is far from the mace, so nothing is caught yet
            Assert.IsNull(hud.GrappledHp);
            Assert.IsTrue(grapple.TryCatch(enemy));
            Assert.AreEqual(1f, hud.GrappledHp.Value, 1e-5f);
        }
    }
}
