using System;
using System.Collections.Generic;
using System.Linq;
using MindAttic.Chain.Core;
using NUnit.Framework;

namespace MindAttic.Chain.Tests
{
    /// <summary>The world as the one authority runs it: commands in (each with a player id), steps, events out.</summary>
    public class GameWorldTests
    {
        private const float Dt = 1f / 50f;
        private static readonly PlayerId P0 = new PlayerId(0);
        private static readonly PlayerId P1 = new PlayerId(1);

        private GameWorld _world;
        private PlayerEntity _player;

        private static GameWorld NewWorld(int enemies = 0, float enemyRadius = 0.15f, int seed = 1)
        {
            var settings = GameWorld.Settings.Default;
            settings.Enemies.Count = enemies;
            settings.Enemies.Radius = enemyRadius;
            return new GameWorld(settings, new Random(seed));
        }

        [SetUp]
        public void SetUp()
        {
            _world = NewWorld();
            _player = _world.AddPlayer(P0, PlayerSetup.Default);
        }

        /// <summary>Drags the angel sideways <paramref name="stride"/> units a step, so the mace picks up speed.</summary>
        private void Swing(GameWorld world, PlayerId player, int steps = 10, float stride = 0.3f)
        {
            for (var i = 0; i < steps; i++)
            {
                world.Submit(new SteerAngel(player, new Vec2(stride / 0.01f, 0f), 0f));
                world.Step(Dt);
            }
        }

        private void Swing(int steps = 10, float stride = 0.3f) => Swing(_world, P0, steps, stride);

        private List<GameEvent> Events() => _world.TakeEvents().ToList();

        // --- Commands -----------------------------------------------------------------------

        [Test]
        public void Commands_FromAPlayerWhoIsNotSeated_ChangeNothing()
        {
            Assert.IsFalse(_world.Submit(new SteerAngel(P1, new Vec2(100f, 0f), Dt)));
            Assert.IsFalse(_world.Submit(new ArmGrapple(P1)));
            Assert.IsFalse(_world.Submit(new ChangeChainLength(P1, 1)));
            Assert.AreEqual(Vec2.Zero, _player.Flight.Position);
        }

        [Test]
        public void SteerAngel_FliesThatPlayersAngel()
        {
            Assert.IsTrue(_world.Submit(new SteerAngel(P0, new Vec2(100f, -50f), Dt)));
            Assert.AreEqual(new Vec2(1f, -0.5f), _player.Flight.Position);
        }

        [Test]
        public void WhilePaused_NothingMoves_AndSteeringIsIgnored()
        {
            Swing(3);
            Assert.IsTrue(_world.Submit(new Pause(P0, true)));
            var events = Events();
            Assert.IsTrue(events.OfType<PauseChanged>().Single().Paused);
            Assert.AreEqual(P0, events.OfType<PauseChanged>().Single().Player);

            var angel = _player.Flight.Position;
            var mace = _player.MacePosition;
            var time = _world.Time;
            Assert.IsFalse(_world.Submit(new SteerAngel(P0, new Vec2(100f, 0f), Dt)));
            _world.Step(Dt);

            Assert.AreEqual(angel, _player.Flight.Position);
            Assert.AreEqual(mace, _player.MacePosition);
            Assert.AreEqual(time, _world.Time);

            Assert.IsTrue(_world.Submit(new TogglePause(P0)));
            Assert.IsFalse(_world.State.IsPaused);
        }

        [Test]
        public void Pause_ThatChangesNothing_ProducesNoEvent()
        {
            Assert.IsFalse(_world.Submit(new Pause(P0, false)));
            Assert.IsEmpty(Events());
        }

        [Test]
        public void Step_PinsLinkZeroToTheAngelsHand()
        {
            _world.Submit(new SteerAngel(P0, new Vec2(400f, -300f), 0f));
            _world.Step(Dt);

            Assert.AreEqual(_player.Flight.HandSocket, _player.Chain.GetPosition(0));
        }

        // --- Chain length -------------------------------------------------------------------

        [Test]
        public void ChangeChainLength_MovesTheMaceAndResizesTheChain()
        {
            Assert.AreEqual(6, _player.MaceIndex);
            Assert.AreEqual(7, _player.Chain.ActiveLinkCount);

            Assert.IsTrue(_world.Submit(new ChangeChainLength(P0, -1)));
            Assert.AreEqual(5, _player.MaceIndex);
            Assert.AreEqual(6, _player.Chain.ActiveLinkCount);
            Assert.AreEqual(5, Events().OfType<ChainLengthChanged>().Single().MaceIndex);

            _world.Submit(new ChangeChainLength(P0, 1));
            _world.Submit(new ChangeChainLength(P0, 1));
            Assert.AreEqual(7, _player.MaceIndex);
            Assert.AreEqual(8, _player.Chain.ActiveLinkCount);
        }

        [Test]
        public void ChainLength_StopsAtOneAndTwelveLinks()
        {
            for (var i = 0; i < 20; i++) { _world.Submit(new ChangeChainLength(P0, -1)); }
            Assert.AreEqual(1, _player.MaceIndex);
            Assert.IsFalse(_world.Submit(new ChangeChainLength(P0, -1)));

            for (var i = 0; i < 20; i++) { _world.Submit(new ChangeChainLength(P0, 1)); }
            Assert.AreEqual(12, _player.MaceIndex);
            Assert.IsFalse(_world.Submit(new ChangeChainLength(P0, 1)));
        }

        [Test]
        public void MaxMaceIndex_IsCappedByAShortChain()
        {
            var setup = PlayerSetup.Default;
            setup.LinkCount = 5;
            var player = _world.AddPlayer(P1, setup);

            Assert.AreEqual(4, player.MaxMaceIndex);
            Assert.AreEqual(4, player.MaceIndex);
        }

        [Test]
        public void MaceMass_FollowsTheMaceWhenItMoves()
        {
            var setup = PlayerSetup.Default;
            setup.MaceMass = 3f;
            _world.Configure(P0, setup);
            Assert.AreEqual(3f, _player.Chain.GetMass(6));
            Assert.AreEqual(1f, _player.Chain.GetMass(5));

            _world.Submit(new ChangeChainLength(P0, -1));
            Assert.AreEqual(3f, _player.Chain.GetMass(5));
            Assert.AreEqual(1f, _player.Chain.GetMass(6));
        }

        [Test]
        public void ShortenedChain_StillHangsFromTheHand_WithTheTailOnTheMace()
        {
            _world.Submit(new ChangeChainLength(P0, -3));
            for (var i = 0; i < 50; i++) { _world.Step(Dt); }

            Assert.Less(_player.MacePosition.Y, _player.Flight.HandSocket.Y);
            Assert.AreEqual(_player.MacePosition, _player.Chain.GetPosition(_player.Chain.LinkCount - 1));
        }

        [Test]
        public void Chain_BouncesOffTheArena_WhenKeptInIt()
        {
            _world.Arena = new Rect2(-2f, -1f, 4f, 2f);
            _world.Submit(new SteerAngel(P0, new Vec2(0f, 90f), 0f));
            for (var i = 0; i < 200; i++) { _world.Step(Dt); }

            for (var i = 1; i < _player.Chain.LinkCount; i++)
            {
                Assert.GreaterOrEqual(_player.Chain.GetPosition(i).Y, -1f - 1e-4f, $"link {i} fell through the floor");
            }

            var setup = _player.Setup;
            setup.KeepChainInArena = false;
            _world.Configure(P0, setup);
            for (var i = 0; i < 200; i++) { _world.Step(Dt); }

            Assert.Less(_player.MacePosition.Y, -1f, "with the arena off the chain hangs past the floor");
        }

        // --- The mace -----------------------------------------------------------------------

        [Test]
        public void EnemyAtTheMace_IsHitWithTheMaceVelocity_AndScored()
        {
            Swing();
            var enemy = _world.SpawnEnemy(_player.MacePosition, mass: 1f, hp: 100f);
            Events();

            // The step moves the mace on; predict the velocity it will hit with from a twin world.
            var velocity = Twin(w => w.SpawnEnemy(w.FindPlayer(P0).MacePosition + new Vec2(50f, 50f), 1f, 100f));
            _world.Step(Dt);

            var hit = Events().OfType<EnemyHit>().Single();
            Assert.AreEqual(P0, hit.Player);
            Assert.AreEqual(enemy.Id, hit.Enemy);
            Assert.AreEqual(velocity, hit.MaceVelocity);
            Assert.AreEqual(velocity.Magnitude * 0.05f, hit.Damage, 1e-4f);
            Assert.AreEqual(EnemyState.Hurt, enemy.State);
            Assert.AreEqual(velocity, enemy.Velocity);
            Assert.AreEqual(100f - hit.Damage, enemy.Hp, 1e-4f);
            Assert.AreEqual(hit.Points, _world.State.Score);
            Assert.AreEqual((int)Math.Round(hit.Damage * 1000f), hit.Points);
            Assert.IsFalse(hit.Killed);
        }

        /// <summary>Runs one more step of a world that has had the same commands as this one, and returns its mace velocity.</summary>
        private Vec2 Twin(Action<GameWorld> setup)
        {
            var twin = NewWorld();
            twin.AddPlayer(P0, PlayerSetup.Default);
            Swing(twin, P0);
            setup(twin);
            twin.Step(Dt);
            return twin.FindPlayer(P0).MaceVelocity;
        }

        [Test]
        public void EnemyAwayFromTheMace_IsNotHit()
        {
            Swing();
            var enemy = _world.SpawnEnemy(_player.MacePosition + new Vec2(5f, 5f), 1f, 100f);
            _world.Step(Dt);

            Assert.IsEmpty(Events().OfType<EnemyHit>());
            Assert.AreEqual(EnemyState.Seek, enemy.State);
        }

        [Test]
        public void FastSwing_SweepsThroughAnEnemyBetweenSteps()
        {
            var setup = PlayerSetup.Default;
            setup.HitRadius = 0.003f;

            GameWorld Build(out PlayerEntity player)
            {
                var world = NewWorld(enemyRadius: 0.003f);
                var settings = world.Config;
                settings.Enemies.Brain.SeekRate = 0f; // the enemy stands still, so only the mace's path matters
                world.Config = settings;
                player = world.AddPlayer(P0, setup);
                Swing(world, P0);
                return world;
            }

            // Where the whirling mace goes in one step.
            var probe = Build(out var probed);
            var from = probed.MacePosition;
            probe.Submit(new SteerAngel(P0, new Vec2(30f, 0f), 0f));
            probe.Step(Dt);
            var to = probed.MacePosition;
            Assert.Greater((to - from).Magnitude, 0.012f, "the mace moves further in a step than the two circles span");

            // An enemy halfway along that path, which neither end of the move touches.
            var world2 = Build(out _);
            var middle = (from + to) * 0.5f;
            var enemy = world2.SpawnEnemy(middle, 1f, 100f);
            Assert.IsFalse(enemy.Overlaps(to, setup.HitRadius), "the end position alone should miss");
            Assert.IsFalse(enemy.Overlaps(from, setup.HitRadius), "the start position alone should miss");

            world2.Submit(new SteerAngel(P0, new Vec2(30f, 0f), 0f));
            world2.Step(Dt);

            Assert.AreEqual(1, world2.TakeEvents().OfType<EnemyHit>().Count());
            Assert.AreEqual(EnemyState.Hurt, enemy.State);
        }

        [Test]
        public void HurtEnemy_IsNotHitTwice()
        {
            Swing();
            var enemy = _world.SpawnEnemy(_player.MacePosition, 1f, 100f);
            _world.Step(Dt);
            Assert.AreEqual(1, Events().OfType<EnemyHit>().Count());

            var hp = enemy.Hp;
            _world.Step(Dt);
            Assert.IsEmpty(Events().OfType<EnemyHit>());
            Assert.AreEqual(hp, enemy.Hp);
        }

        [Test]
        public void LethalHit_KillsTheEnemy_AndTakesItOutOfTheWorld()
        {
            Swing();
            var enemy = _world.SpawnEnemy(_player.MacePosition, 1f, 0.0001f);
            _world.Step(Dt);

            var events = Events();
            Assert.IsTrue(events.OfType<EnemyHit>().Single().Killed);
            var killed = events.OfType<EnemyKilled>().Single();
            Assert.AreEqual(enemy.Id, killed.Enemy);
            Assert.AreEqual(P0, killed.Player);
            Assert.IsFalse(enemy.IsAlive);
            Assert.IsFalse(enemy.InWorld);
            Assert.IsNull(_world.FindEnemy(enemy.Id));
        }

        // --- The grapple --------------------------------------------------------------------

        [Test]
        public void ArmGrapple_ArmsOnTheNextStep()
        {
            Assert.IsTrue(_world.Submit(new ArmGrapple(P0)));
            Assert.AreEqual(GrappleState.Idle, _player.Grapple.State, "commands are applied by the step");
            _world.Step(Dt);

            Assert.AreEqual(GrappleState.Armed, _player.Grapple.State);
            Assert.AreEqual(GrappleState.Armed, Events().OfType<GrappleChanged>().Single().State);
        }

        [Test]
        public void QuickTapBetweenSteps_StillArmsForOneStep()
        {
            _world.Submit(new ArmGrapple(P0));
            _world.Submit(new ReleaseGrapple(P0));
            _world.Step(Dt);
            Assert.AreEqual(GrappleState.Armed, _player.Grapple.State);

            _world.Step(Dt);
            Assert.AreEqual(GrappleState.Idle, _player.Grapple.State);
        }

        [Test]
        public void ArmedGrapple_CatchesInsteadOfHitting_TheHeldEnemySharesLaterDamage_AndReleaseThrowsIt()
        {
            var setup = PlayerSetup.Default;
            setup.CatchRadius = 0f;
            setup.ThrowMultiplier = 1.5f;
            _world.Configure(P0, setup);

            Swing();
            _world.Submit(new ArmGrapple(P0));
            _world.Step(Dt);
            Assert.AreEqual(GrappleState.Armed, _player.Grapple.State);
            Events();

            var caught = _world.SpawnEnemy(_player.MacePosition, 1f, 100f);
            _world.Step(Dt);
            var events = Events();
            Assert.AreEqual(caught.Id, events.OfType<Grabbed>().Single().Enemy);
            Assert.IsEmpty(events.OfType<EnemyHit>());
            Assert.AreSame(caught, _player.HeldEnemy);
            Assert.AreEqual(EnemyState.Grabbed, caught.State);
            Assert.AreEqual(100f, caught.Hp);
            Assert.AreEqual(_player.MacePosition, caught.Position, "carried on the mace");

            Swing(1);
            Events();
            var victim = _world.SpawnEnemy(_player.MacePosition + new Vec2(0.05f, 0f), 1f, 100f);
            Swing(1);
            Assert.AreEqual(1, Events().OfType<EnemyHit>().Count());
            Assert.AreEqual(EnemyState.Hurt, victim.State);
            Assert.AreEqual(100f - victim.Hp, 100f - caught.Hp, 1e-4f);

            _world.Submit(new ReleaseGrapple(P0));
            _world.Step(Dt);
            var thrown = Events().OfType<Thrown>().Single();
            Assert.AreEqual(caught.Id, thrown.Enemy);
            Assert.AreEqual(EnemyState.Hurt, caught.State);
            Assert.IsTrue(caught.Brain.IsThrown);
            Assert.AreEqual(GrappleState.Idle, _player.Grapple.State);
            Assert.AreEqual(_player.MaceVelocity * 1.5f, thrown.Velocity);
        }

        [Test]
        public void Release_ThrowsWithTheMaceVelocityTimesTheMultiplier()
        {
            var setup = PlayerSetup.Default;
            setup.ThrowMultiplier = 2f;
            _world.Configure(P0, setup);
            _world.Submit(new ArmGrapple(P0));
            _world.Step(Dt);
            var enemy = _world.SpawnEnemy(_player.MacePosition, 1f, 100f);
            _world.Step(Dt);
            Assert.AreSame(enemy, _player.HeldEnemy);

            Swing(5);
            _world.Submit(new ReleaseGrapple(P0));
            _world.Submit(new SteerAngel(P0, new Vec2(30f, 0f), 0f));
            _world.Step(Dt);

            var thrown = Events().OfType<Thrown>().Single();
            Assert.AreEqual(_player.MaceVelocity * 2f, thrown.Velocity);
            Assert.AreEqual(thrown.Velocity, enemy.Velocity);
        }

        [Test]
        public void HeldEnemyKilled_IsDropped_AndTheGrappleStaysArmed()
        {
            var setup = PlayerSetup.Default;
            setup.CatchRadius = 0f;
            _world.Configure(P0, setup);
            Swing();
            _world.Submit(new ArmGrapple(P0));
            _world.Step(Dt);
            var held = _world.SpawnEnemy(_player.MacePosition, 1f, 0.0001f);
            _world.Step(Dt);
            Assert.AreSame(held, _player.HeldEnemy);

            Swing(1);
            _world.SpawnEnemy(_player.MacePosition + new Vec2(0.05f, 0f), 1f, 100f);
            Events();
            Swing(1);

            var events = Events();
            Assert.IsTrue(events.OfType<EnemyKilled>().Any(k => k.Enemy == held.Id));
            Assert.IsNull(_player.HeldEnemy);
            Assert.AreEqual(GrappleState.Armed, _player.Grapple.State);

            _world.Submit(new ReleaseGrapple(P0));
            _world.Step(Dt);
            Assert.IsEmpty(Events().OfType<Thrown>());
            Assert.AreEqual(GrappleState.Idle, _player.Grapple.State);
        }

        // --- Enemies ------------------------------------------------------------------------

        [Test]
        public void ThrownEnemy_KnocksEnemiesItTouches_WithoutDamage()
        {
            var setup = PlayerSetup.Default;
            setup.HitRadius = 0f;
            _world.Configure(P0, setup);

            var thrown = _world.SpawnEnemy(new Vec2(5f, 3f), 1f, 100f);
            var bystander = _world.SpawnEnemy(new Vec2(5.2f, 3f), 2f, 100f);
            IGrappleTarget target = thrown;
            target.OnGrappled();
            target.Throw(new Vec2(4f, 0f));

            _world.Step(Dt);

            Assert.AreEqual(EnemyState.Hurt, bystander.State);
            Assert.Greater(bystander.Velocity.X, 0f, "knocked the way the thrown enemy flies");
            Assert.AreEqual(100f, bystander.Hp);
            Assert.AreEqual(bystander.Id, Events().OfType<EnemyKnocked>().Single().Enemy);
        }

        [Test]
        public void OverlappingEnemies_SeparateOverTime()
        {
            // Seek is off, so only flocking moves them.
            var settings = _world.Config;
            settings.Enemies.Brain.SeekRate = 0f;
            _world.Config = settings;
            var a = _world.SpawnEnemy(new Vec2(5f, 3f), 1f, 100f);
            var b = _world.SpawnEnemy(new Vec2(5.1f, 3f), 1f, 100f);
            for (var i = 0; i < 50; i++) { _world.Step(Dt); }

            Assert.Less(a.Position.X, 5f);
            Assert.Greater(b.Position.X, 5.1f);
        }

        [Test]
        public void GrabbedEnemy_IsNotPushed_AndDoesNotPush()
        {
            var settings = _world.Config;
            settings.Enemies.Brain.SeekRate = 0f;
            _world.Config = settings;
            var free = _world.SpawnEnemy(new Vec2(5f, 3f), 1f, 100f);
            var held = _world.SpawnEnemy(new Vec2(5.1f, 3f), 1f, 100f);
            ((IGrappleTarget)held).OnGrappled();

            _world.Step(Dt);

            Assert.AreEqual(5f, free.Position.X, 1e-6f);
            Assert.AreEqual(5.1f, held.Position.X, 1e-6f);
        }

        [Test]
        public void Enemies_SeekTheNearestAngel()
        {
            _world.Submit(new SteerAngel(P0, new Vec2(-500f, 0f), 0f));
            var second = _world.AddPlayer(P1, PlayerSetup.Default);
            _world.Submit(new SteerAngel(P1, new Vec2(500f, 0f), 0f));

            var nearP1 = _world.SpawnEnemy(new Vec2(4f, 3f), 1f, 100f);
            var nearP0 = _world.SpawnEnemy(new Vec2(-4f, 3f), 1f, 100f);
            _world.Step(Dt);

            Assert.Greater(nearP1.Position.X, 4f);
            Assert.Less(nearP0.Position.X, -4f);
            Assert.Less(nearP1.Position.Y, 3f);
            Assert.AreEqual(new Vec2(5f, 0f), second.Flight.Position);
        }

        [Test]
        public void SpawnWave_PutsTheConfiguredCountInTheArena_WithMassInRange()
        {
            var world = NewWorld(enemies: 14);
            world.Arena = new Rect2(-3f, -2f, 6f, 4f);
            world.SpawnWave();

            Assert.AreEqual(14, world.Enemies.Count);
            Assert.AreEqual(14, world.TakeEvents().OfType<EnemySpawned>().Count());
            Assert.AreEqual(14, world.Enemies.Select(e => e.Id).Distinct().Count());
            foreach (var enemy in world.Enemies)
            {
                Assert.IsTrue(enemy.Position.X >= -3f && enemy.Position.X <= 3f && enemy.Position.Y >= -2f && enemy.Position.Y <= 2f);
                Assert.IsTrue(enemy.Brain.Mass >= 1f && enemy.Brain.Mass <= 2f);
                Assert.AreEqual(1f, enemy.Hp);
            }
        }

        [Test]
        public void RandomPositions_AreInsideTheArea()
        {
            var area = new Rect2(-8f, -4.5f, 16f, 9f);
            var points = GameWorld.RandomPositions(area, 200, new Random(1));

            Assert.AreEqual(200, points.Length);
            foreach (var p in points)
            {
                Assert.IsTrue(p.X >= area.XMin && p.X <= area.XMax && p.Y >= area.YMin && p.Y <= area.YMax);
            }
        }

        // --- The angel ----------------------------------------------------------------------

        private GameWorld ContactWorld(out PlayerEntity player)
        {
            var world = NewWorld(enemyRadius: 0.2f);
            player = world.AddPlayer(P0, PlayerSetup.Default);
            var settings = world.Config;
            settings.Enemies.Brain.SeekRate = 0f;
            world.Config = settings;
            return world;
        }

        [Test]
        public void EnemyTouchingTheAngel_CostsHp_ThroughTheHurtDelay()
        {
            var world = ContactWorld(out _);
            world.SpawnEnemy(new Vec2(-0.4f, 0f), 1f, 100f);

            world.Step(0.1f);
            Assert.AreEqual(29, world.State.Hp);
            var hurt = world.TakeEvents().OfType<AngelHurt>().Single();
            Assert.AreEqual(P0, hurt.Player);
            Assert.AreEqual(29, hurt.Hp);

            world.Step(0.1f);
            world.Step(0.1f);
            Assert.AreEqual(29, world.State.Hp, "inside the 0.25 s hurt delay");
            world.Step(0.1f);
            Assert.AreEqual(28, world.State.Hp);
        }

        [Test]
        public void FarEnemy_DoesNotHurt()
        {
            var world = ContactWorld(out _);
            world.SpawnEnemy(new Vec2(-3f, 0f), 1f, 100f);
            world.Step(0.1f);

            Assert.AreEqual(30, world.State.Hp);
        }

        [Test]
        public void GrabbedEnemy_DoesNotHurt()
        {
            var world = ContactWorld(out _);
            var enemy = world.SpawnEnemy(new Vec2(-0.4f, 0f), 1f, 100f);
            ((IGrappleTarget)enemy).OnGrappled();
            world.Step(0.1f);

            Assert.AreEqual(30, world.State.Hp);
        }

        [Test]
        public void HpRunningOut_IsGameOver_ThenTheLightFades_AndTheWorldResetsItself()
        {
            var world = ContactWorld(out var player);
            var settings = world.Config;
            settings.Enemies.Count = 3;
            world.Config = settings;
            world.State.Config.HurtDelay = 0f;
            world.SpawnEnemy(new Vec2(-0.4f, 0f), 1f, 100f);
            world.Submit(new SteerAngel(P0, new Vec2(10f, 0f), 0f));
            world.Submit(new ChangeChainLength(P0, 2));
            world.AddHitScoreForTest(1f);

            for (var i = 0; i < 30; i++) { world.Step(Dt); }
            Assert.IsTrue(world.State.IsGameOver);
            var events = world.TakeEvents();
            Assert.AreEqual(1, events.OfType<GameOver>().Count());
            Assert.IsFalse(world.Submit(new Pause(P0, true)), "a finished game cannot be paused");

            var steps = 0;
            while (world.State.IsGameOver)
            {
                world.Step(Dt);
                Assert.Less(++steps, 100);
            }

            events = world.TakeEvents();
            Assert.AreEqual(1, events.OfType<WorldReset>().Count());
            Assert.AreEqual(3, events.OfType<EnemySpawned>().Count());
            Assert.AreEqual(1, events.OfType<EnemyRemoved>().Count());
            Assert.AreEqual(30, world.State.Hp);
            Assert.AreEqual(0, world.State.Score);
            Assert.AreEqual(GameState.FullAmbient, world.State.Ambient);
            Assert.AreEqual(Vec2.Zero, player.Flight.Position, "the angel is home (Root.vb Reset)");
            Assert.AreEqual(8, player.MaceIndex, "the chain length is kept");
            Assert.AreEqual(3, world.Enemies.Count);
        }

        [Test]
        public void CollectHeart_Heals_UpToMax()
        {
            var world = ContactWorld(out _);
            world.SpawnEnemy(new Vec2(-0.4f, 0f), 1f, 100f);
            world.Step(0.1f);
            world.TakeEvents();

            Assert.AreEqual(1, world.CollectHeart(P0, 10));
            Assert.AreEqual(30, world.State.Hp);
            var healed = world.TakeEvents().OfType<AngelHealed>().Single();
            Assert.AreEqual(P0, healed.Player);
            Assert.AreEqual(1, healed.Gained);
            Assert.AreEqual(0, world.CollectHeart(P1, 10), "only a seated player collects");
        }

        [Test]
        public void ResetGame_StartsOver()
        {
            Swing(3);
            Assert.IsTrue(_world.Submit(new ResetGame(P0)));

            Assert.AreEqual(Vec2.Zero, _player.Flight.Position);
            Assert.AreEqual(_player.Flight.HandSocket, _player.MacePosition, "the chain collapses on the hand");
            Assert.AreEqual(1, Events().OfType<WorldReset>().Count());
        }

        // --- Follow (F key) -----------------------------------------------------------------

        [Test]
        public void ToggleFollow_SlackensThatPlayersChain_AndSaysSo()
        {
            Assert.IsTrue(_player.IsFollowing);
            Assert.IsTrue(_world.Submit(new ToggleFollow(P0)));

            Assert.IsFalse(_player.IsFollowing);
            var changed = Events().OfType<FollowChanged>().Single();
            Assert.AreEqual(P0, changed.Player);
            Assert.IsFalse(changed.IsFollowing);

            _world.Submit(new ToggleFollow(P0));
            Assert.IsTrue(_player.IsFollowing);
        }

        [Test]
        public void NotFollowing_TheMaceNeitherHitsNorCatches_UntilFollowIsBackOn()
        {
            Swing();
            _world.Submit(new ToggleFollow(P0));
            _world.Submit(new ArmGrapple(P0));
            var settings = _world.Config;
            settings.Enemies.Brain.SeekRate = 0f;
            _world.Config = settings;
            var enemy = _world.SpawnEnemy(_player.MacePosition, 1f, 100f);
            _world.Step(Dt);

            Assert.AreEqual(EnemyState.Seek, enemy.State);
            Assert.IsNull(_player.HeldEnemy);

            _world.Submit(new ReleaseGrapple(P0));
            _world.Step(Dt);
            _world.Submit(new ToggleFollow(P0));
            enemy.Position = _player.MacePosition;
            _world.Step(Dt);
            Assert.AreEqual(EnemyState.Hurt, enemy.State);
        }

        [Test]
        public void TheFollowFlag_SurvivesAReset()
        {
            _world.Submit(new ToggleFollow(P0));
            _world.Reset();

            Assert.IsFalse(_player.IsFollowing, "Root.vb's Reset leaves followMouse alone");
        }

        // --- Several players ----------------------------------------------------------------

        [Test]
        public void EachPlayer_HasTheirOwnAngelChainAndGrapple()
        {
            var second = _world.AddPlayer(P1, PlayerSetup.Default);
            _world.Submit(new SteerAngel(P1, new Vec2(300f, 0f), 0f));
            _world.Submit(new ArmGrapple(P1));
            _world.Submit(new ChangeChainLength(P1, -2));
            _world.Step(Dt);

            Assert.AreEqual(Vec2.Zero, _player.Flight.Position);
            Assert.AreEqual(GrappleState.Idle, _player.Grapple.State);
            Assert.AreEqual(6, _player.MaceIndex);
            Assert.AreEqual(new Vec2(3f, 0f), second.Flight.Position);
            Assert.AreEqual(GrappleState.Armed, second.Grapple.State);
            Assert.AreEqual(4, second.MaceIndex);
            Assert.AreEqual(second.Flight.HandSocket, second.Chain.GetPosition(0));
            Assert.Throws<ArgumentException>(() => _world.AddPlayer(P1, PlayerSetup.Default));
        }

        [Test]
        public void AHit_IsCreditedToThePlayerWhoseMaceLandedIt()
        {
            var world = NewWorld();
            var left = PlayerSetup.Default;
            left.Start = new Vec2(-5f, 0f);
            var right = PlayerSetup.Default;
            right.Start = new Vec2(5f, 0f);
            world.AddPlayer(P0, left);
            var second = world.AddPlayer(P1, right);
            Swing(world, P1);
            var enemy = world.SpawnEnemy(second.MacePosition, 1f, 100f);
            world.TakeEvents();
            world.Step(Dt);

            var hit = world.TakeEvents().OfType<EnemyHit>().Single();
            Assert.AreEqual(P1, hit.Player);
            Assert.AreEqual(enemy.Id, hit.Enemy);
        }

        // --- The Seek cap (UNI-T65) ------------------------------------------------------------

        /// <summary>A world with no flocking and the given Seek cap, the player's angel at the origin.</summary>
        private static GameWorld CappedWorld(int cap, out PlayerEntity player)
        {
            var settings = GameWorld.Settings.Default;
            settings.Enemies.Count = 0;
            settings.Enemies.FlockSpeed = 0f;
            settings.Enemies.SeekCap = cap;
            var world = new GameWorld(settings, new Random(1));
            player = world.AddPlayer(P0, PlayerSetup.Default);
            return world;
        }

        [Test]
        public void SeekCap_OnlyThatManySeek_TheRestWait()
        {
            var world = CappedWorld(2, out var player);
            for (var i = 0; i < 6; i++)
            {
                world.SpawnEnemy(player.AngelPosition + new Vec2(6f, -3f + i), 1f, 100f);
            }

            world.Step(Dt);

            Assert.AreEqual(2, world.SeekerCount());
            Assert.AreEqual(4, world.Enemies.Count(e => e.Brain.IsWaiting));
        }

        [Test]
        public void SeekCap_OfTheWholeWave_IsTheOriginal_EveryoneSeeks()
        {
            var world = CappedWorld(14, out var player);
            for (var i = 0; i < 14; i++)
            {
                world.SpawnEnemy(player.AngelPosition + new Vec2(6f, -3f + i * 0.4f), 1f, 100f);
            }

            world.Step(Dt);

            Assert.AreEqual(14, world.SeekerCount());
        }

        [Test]
        public void SeekSlot_FreedByAKill_GoesToTheNearestWaitingEnemy()
        {
            var world = CappedWorld(1, out var player);
            var seeker = world.SpawnEnemy(player.AngelPosition + new Vec2(5f, 0f), 1f, 100f);
            var far = world.SpawnEnemy(player.AngelPosition + new Vec2(-7f, 0f), 1f, 100f);
            var near = world.SpawnEnemy(player.AngelPosition + new Vec2(0f, 4f), 1f, 100f);
            Assert.AreEqual(EnemyState.Seek, seeker.State);
            Assert.IsTrue(far.Brain.IsWaiting && near.Brain.IsWaiting);

            seeker.Brain.Damage(1000f);
            world.Step(Dt);

            Assert.AreEqual(EnemyState.Seek, near.State, "the nearest waiting enemy takes the slot");
            Assert.IsTrue(far.Brain.IsWaiting);
        }

        [Test]
        public void SeekSlot_FreedByAHurtSeeker_IsTaken_AndTheRecoveredOneMayWaitAgain()
        {
            var world = CappedWorld(1, out var player);
            var seeker = world.SpawnEnemy(player.AngelPosition + new Vec2(5f, 0f), 1f, 100f);
            var waiting = world.SpawnEnemy(player.AngelPosition + new Vec2(0f, 4f), 1f, 100f);

            seeker.Brain.Hit(new Vec2(0.5f, 0f), 0f);
            world.Step(Dt);

            Assert.AreEqual(EnemyState.Hurt, seeker.State);
            Assert.AreEqual(EnemyState.Seek, waiting.State);

            for (var i = 0; i < 200 && seeker.State == EnemyState.Hurt; i++)
            {
                world.Step(Dt);
            }

            world.Step(Dt);
            Assert.AreEqual(1, world.SeekerCount(), "never more than the cap");
        }

        [Test]
        public void WaitingEnemies_HoldTheHangBack_NeitherReachingNorLeavingTheAngel()
        {
            var world = CappedWorld(0, out var player);
            var config = world.Config.Enemies.Brain;
            var enemies = new[]
            {
                world.SpawnEnemy(player.AngelPosition + new Vec2(7f, 0f), 1f, 100f),
                world.SpawnEnemy(player.AngelPosition + new Vec2(-1f, 0.5f), 2f, 100f),
                world.SpawnEnemy(player.AngelPosition + new Vec2(3f, -4f), 1.5f, 100f),
            };

            for (var i = 0; i < 500; i++)
            {
                world.Step(Dt);
            }

            var sawCautious = false;
            var sawAlign = false;
            for (var i = 0; i < 300; i++)
            {
                world.Step(Dt);
                foreach (var enemy in enemies)
                {
                    var distance = (enemy.Position - player.AngelPosition).Magnitude;
                    Assert.Greater(distance, config.HangBackNear * 0.8f, "never closes on the angel");
                    Assert.Less(distance, config.HangBackFar * 1.4f, "never runs off");
                    sawCautious |= enemy.State == EnemyState.Cautious;
                    sawAlign |= enemy.State == EnemyState.Align;
                }
            }

            Assert.IsTrue(sawCautious && sawAlign, "they back off and stalk in turn");
            Assert.AreEqual(0, world.SeekerCount());
        }

        [Test]
        public void WaitingEnemy_CanStillBeHit()
        {
            var world = CappedWorld(0, out var player);
            Swing(world, P0);
            var enemy = world.SpawnEnemy(player.MacePosition, 1f, 100f);
            Assert.IsTrue(enemy.Brain.IsWaiting);
            world.TakeEvents();
            world.Step(Dt);

            Assert.AreEqual(enemy.Id, world.TakeEvents().OfType<EnemyHit>().Single().Enemy);
        }

        [Test]
        public void RemovedPlayer_IsGone_AndTheirCommandsAreIgnored()
        {
            Assert.IsTrue(_world.RemovePlayer(P0));
            Assert.IsNull(_world.FindPlayer(P0));
            Assert.IsFalse(_world.Submit(new SteerAngel(P0, Vec2.One, Dt)));
            Assert.IsFalse(_world.RemovePlayer(P0));
        }
    }

    internal static class GameWorldTestExtensions
    {
        public static void AddHitScoreForTest(this GameWorld world, float damage) => world.State.AddHitScore(damage);
    }
}
