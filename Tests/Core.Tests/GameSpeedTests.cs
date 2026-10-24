using System.Linq;
using MindAttic.Chain.Core;
using NUnit.Framework;

namespace MindAttic.Chain.Tests
{
    /// <summary>
    /// Input.vb's Minus and Equals (UNI-T52): <c>gameSpeed</c> -/+ 0.01 for every frame the key is held, never
    /// below 0, as <see cref="ChangeGameSpeed"/> commands on the shipped data; and, where the original only drew
    /// it, the world's time scale (<see cref="TimeScale"/>, BIBLE D13, V20).
    /// </summary>
    public class GameSpeedTests
    {
        private static readonly PlayerId P0 = new PlayerId(0);
        private const float Frame = 1f / 60f;
        private const float Dt = 1f / 50f;

        private GameSettings _settings;
        private GameWorld _world;

        [SetUp]
        public void SetUp()
        {
            _settings = GameSettings.Load(new DirectorySettingsSource(GameSettingsTests.ShippedFolder));
            _world = NewWorld(out _);
        }

        private GameWorld NewWorld(out PlayerEntity player)
        {
            var world = new GameWorld(_settings.World, new System.Random(1));
            player = world.AddPlayer(P0, _settings.Player);
            world.Reset();
            world.TakeEvents();
            return world;
        }

        private bool Hold(int direction, int frames = 1) => _world.Submit(new ChangeGameSpeed(P0, direction, frames * Frame));

        [Test]
        public void GameJson_HoldsInputVbsStep()
        {
            Assert.AreEqual(0.01f, _settings.Game.GameSpeedStep, "gameSpeed -/+ 0.01");
            Assert.AreEqual(0.6f, _settings.World.GameSpeedRate, 1e-6f, "0.01 a frame at 60 fps");
            Assert.AreEqual(1f, _world.GameSpeed);
        }

        [Test]
        public void Equals_RaisesAndMinus_LowersByAHundredthAFrame()
        {
            Assert.IsTrue(Hold(+1));
            Assert.AreEqual(1.01f, _world.GameSpeed, 1e-5f);
            var changed = _world.TakeEvents().OfType<GameSpeedChanged>().Single();
            Assert.AreEqual(P0, changed.Player);
            Assert.AreEqual(_world.GameSpeed, changed.Speed);

            Assert.IsTrue(Hold(-1, 3));
            Assert.AreEqual(0.98f, _world.GameSpeed, 1e-5f);
        }

        [Test]
        public void HeldForASecond_MovesItSixtyHundredths_AtAnyFrameRate()
        {
            for (var i = 0; i < 144; i++)
            {
                _world.Submit(new ChangeGameSpeed(P0, +1, 1f / 144f));
            }

            Assert.AreEqual(1.6f, _world.GameSpeed, 1e-4f);
        }

        [Test]
        public void NeverBelowZero()
        {
            Assert.IsTrue(Hold(-1, 1000));
            Assert.AreEqual(0f, _world.GameSpeed);
            _world.TakeEvents();

            Assert.IsFalse(Hold(-1), "at 0 Minus does nothing");
            Assert.IsEmpty(_world.TakeEvents().OfType<GameSpeedChanged>(), "and no event says it did");
            Assert.IsTrue(Hold(+1), "Equals still raises it");
        }

        [Test]
        public void WorksWhilePaused_AsInputVbPollsEveryFrame()
        {
            _world.Submit(new Pause(P0, true));

            Assert.IsTrue(Hold(+1, 10));
            Assert.AreEqual(1.1f, _world.GameSpeed, 1e-5f);
        }

        [Test]
        public void AnUnseatedPlayer_ChangesNothing()
        {
            Assert.IsFalse(_world.Submit(new ChangeGameSpeed(new PlayerId(7), +1, Frame)));
            Assert.AreEqual(1f, _world.GameSpeed);
        }

        [Test]
        public void Reset_PutsOnePointZeroBack()
        {
            Hold(+1, 50);

            _world.Submit(new ResetGame(P0));

            Assert.AreEqual(1f, _world.GameSpeed, "Root.vb Reset: gameSpeed = 1.0");
        }

        [Test]
        public void TheSpeed_ScalesGameTime()
        {
            // GameWorld.Step's fixed-step accumulator (BIBLE D9, U.CHN-T45) banks deltaTime x GameSpeed and
            // only advances RoundTime in whole 1/60 s sub-steps, carrying any leftover (including a float
            // rounding remainder right at a sub-step boundary) to the next call, so a single call does not
            // advance game time by an arbitrary fraction of a sub-step.
            const float fixedStep = 1f / 60f;

            _world.GameSpeed = 0.5f;
            _world.Step(Dt); // 0.02 * 0.5 = 0.01 s banked, under one sub-step: none runs yet
            Assert.AreEqual(0f, _world.RoundTime, 1e-7f);

            _world.GameSpeed = 2f;
            _world.Step(Dt); // + 0.02 * 2 = 0.04 s; 0.01 + 0.04 = 0.05 s, two whole sub-steps and change
            Assert.AreEqual(2 * fixedStep, _world.RoundTime, 1e-6f);
        }

        [Test]
        public void DoubleSpeed_PlaysTwoStepsInOne()
        {
            var fast = NewWorld(out var fastPlayer);
            var slow = NewWorld(out var slowPlayer);
            fast.GameSpeed = 2f;
            foreach (var world in new[] { fast, slow })
            {
                world.Submit(new SteerAngel(P0, new Vec2(40f, -20f), 0f));
            }

            for (var i = 0; i < 30; i++)
            {
                fast.Step(Dt);
                slow.Step(Dt);
                slow.Step(Dt);
            }

            Assert.AreEqual(slow.RoundTime, fast.RoundTime, 1e-5f);
            Assert.AreEqual(slowPlayer.MacePosition, fastPlayer.MacePosition, "the chain swings as two steps would swing it");
            Assert.That(fast.Enemies.Select(e => e.Position), Is.EqualTo(slow.Enemies.Select(e => e.Position)));
        }

        [Test]
        public void ZeroSpeed_StandsStill()
        {
            var enemies = _world.Enemies.Select(e => e.Position).ToArray();
            Hold(-1, 100);
            Assert.AreEqual(0f, _world.GameSpeed);
            var start = _world.Players[0].Flight.Position;

            _world.Step(Dt);

            Assert.AreEqual(0f, _world.RoundTime);
            Assert.That(_world.Enemies.Select(e => e.Position), Is.EqualTo(enemies));
            Assert.IsFalse(_world.Submit(new SteerAngel(P0, new Vec2(100f, 0f), Dt)), "the angel waits with everything else");
            Assert.AreEqual(start, _world.Players[0].Flight.Position);
        }

        [Test]
        public void HighSpeed_KeepsTheChainStable()
        {
            var world = NewWorld(out var player);
            world.GameSpeed = 8f;
            world.Submit(new SteerAngel(P0, new Vec2(200f, 0f), 0f));

            for (var i = 0; i < 300; i++)
            {
                world.Step(Dt);
            }

            var reach = (player.MacePosition - player.Chain.GetPosition(0)).Magnitude;
            Assert.IsFalse(float.IsNaN(reach) || float.IsInfinity(reach));
            Assert.Less(reach, 5f, "sub-steps no longer than the fixed step keep the springs together");
        }

        [Test]
        public void Substeps_KeepEachNoLongerThanTheFixedStep()
        {
            Assert.AreEqual(0, TimeScale.Substeps(0f));
            Assert.AreEqual(1, TimeScale.Substeps(0.3f));
            Assert.AreEqual(1, TimeScale.Substeps(1f));
            Assert.AreEqual(1, TimeScale.Substeps(1.0000001f), "0.01 steps that add up inexactly");
            Assert.AreEqual(2, TimeScale.Substeps(1.01f));
            Assert.AreEqual(3, TimeScale.Substeps(2.5f));
            Assert.AreEqual(TimeScale.MaxSubsteps, TimeScale.Substeps(1000f));
        }

        [Test]
        public void ABadGameSpeedStep_IsNamed()
        {
            var shipped = new DirectorySettingsSource(GameSettingsTests.ShippedFolder);
            var source = new MemorySettingsSource(shipped).Set(
                GameSettings.GameFile,
                shipped.Read(GameSettings.GameFile).Replace("\"gameSpeedStep\": 0.01", "\"gameSpeedStep\": 0"));

            var e = Assert.Throws<SettingsException>(() => GameSettings.Load(source));

            Assert.IsTrue(e.Errors.Any(x => x.Contains("'gameSpeedStep'")), string.Join("\n", e.Errors));
        }
    }
}
