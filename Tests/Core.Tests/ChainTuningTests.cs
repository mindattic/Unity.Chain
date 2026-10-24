using System.Linq;
using MindAttic.Chain.Core;
using NUnit.Framework;

namespace MindAttic.Chain.Tests
{
    /// <summary>
    /// Form1.vb's live-tuning keys (UNI-T51): Insert/Delete resistance +/-0.001, Home/End gravity -/+0.00001,
    /// PageUp/PageDown spring +/-0.001, as <see cref="TuneChain"/> commands on the shipped data, shown in the
    /// Res/Grav/Spring readout, held to chain.json's limits and put back by a reset.
    /// </summary>
    public class ChainTuningTests
    {
        private static readonly PlayerId P0 = new PlayerId(0);

        private GameSettings _settings;
        private GameWorld _world;
        private PlayerEntity _player;

        [SetUp]
        public void SetUp()
        {
            _settings = GameSettings.Load(new DirectorySettingsSource(GameSettingsTests.ShippedFolder));
            _world = new GameWorld(_settings.World, new System.Random(1));
            _player = _world.AddPlayer(P0, _settings.Player);
            _world.Reset();
            _world.TakeEvents();
        }

        private ChainReadout Readout => ChainReadout.From(_player.Chain.Config, _settings.Mapping);

        [Test]
        public void ChainJson_HoldsForm1sStepsAndThePortsLimits()
        {
            var tuning = _settings.Chain.Tuning;

            Assert.AreEqual(0.001f, tuning.ResistanceStep, "Insert/Delete changeResistance(+/-0.001)");
            Assert.AreEqual(0.00001f, tuning.GravityStep, "Home/End changeGravity(-/+0.00001)");
            Assert.AreEqual(0.001f, tuning.SpringStep, "PageUp/PageDown changeSpring(+/-0.001)");
            Assert.Less(tuning.ResistanceMin, _settings.Chain.Resistance);
            Assert.Greater(tuning.ResistanceMax, _settings.Chain.Resistance);
            Assert.Less(tuning.GravityMin, _settings.Chain.Gravity.Y);
            Assert.Greater(tuning.GravityMax, _settings.Chain.Gravity.Y);
            Assert.Less(tuning.SpringMin, _settings.Chain.SpringK);
            Assert.Greater(tuning.SpringMax, _settings.Chain.SpringK);
        }

        [Test]
        public void InsertAndDelete_MoveResistanceByAThousandth()
        {
            Assert.IsTrue(_world.Submit(new TuneChain(P0, ChainKnob.Resistance, 1)));
            Assert.AreEqual(0.251f, Readout.Resistance, 1e-6f);

            var tuned = _world.TakeEvents().OfType<ChainTuned>().Single();
            Assert.AreEqual(P0, tuned.Player);
            Assert.AreEqual(ChainKnob.Resistance, tuned.Knob);
            Assert.AreEqual(_player.Chain.Config.Resistance, tuned.Value);

            _world.Submit(new TuneChain(P0, ChainKnob.Resistance, -1));
            _world.Submit(new TuneChain(P0, ChainKnob.Resistance, -1));
            Assert.AreEqual(0.249f, Readout.Resistance, 1e-6f);
            Assert.AreEqual(-0.0025f, Readout.Gravity, 1e-9f, "only the resistance moves");
            Assert.AreEqual(0.1f, Readout.Spring, 1e-7f, "only the resistance moves");
        }

        [Test]
        public void HomeAndEnd_MoveGravitysYByAHundredThousandth()
        {
            var x = _player.Chain.Config.Gravity.X;

            _world.Submit(new TuneChain(P0, ChainKnob.Gravity, -1)); // Home
            Assert.AreEqual(-0.00251f, Readout.Gravity, 1e-9f);

            _world.Submit(new TuneChain(P0, ChainKnob.Gravity, 1)); // End
            _world.Submit(new TuneChain(P0, ChainKnob.Gravity, 1));
            Assert.AreEqual(-0.00249f, Readout.Gravity, 1e-9f);
            Assert.AreEqual(x, _player.Chain.Config.Gravity.X, "changeGravity moves only gravity.Y");
        }

        [Test]
        public void PageUpAndPageDown_MoveTheSpringByAThousandth()
        {
            _world.Submit(new TuneChain(P0, ChainKnob.Spring, 1));
            Assert.AreEqual(0.101f, Readout.Spring, 1e-7f);

            _world.Submit(new TuneChain(P0, ChainKnob.Spring, -1));
            _world.Submit(new TuneChain(P0, ChainKnob.Spring, -1));
            Assert.AreEqual(0.099f, Readout.Spring, 1e-7f);
        }

        [Test]
        public void TheKnobsStopAtChainJsonsLimits()
        {
            var tuning = _settings.Chain.Tuning;

            for (var i = 0; i < 1000; i++)
            {
                _world.Submit(new TuneChain(P0, ChainKnob.Resistance, -1));
                _world.Submit(new TuneChain(P0, ChainKnob.Spring, -1));
                _world.Submit(new TuneChain(P0, ChainKnob.Gravity, 1));
            }

            Assert.AreEqual(tuning.ResistanceMin, Readout.Resistance, 1e-6f);
            Assert.AreEqual(tuning.SpringMin, Readout.Spring, 1e-6f);
            Assert.AreEqual(tuning.GravityMax, Readout.Gravity, 1e-8f);
            _world.TakeEvents();

            Assert.IsFalse(_world.Submit(new TuneChain(P0, ChainKnob.Resistance, -1)), "at the limit nothing moves");
            Assert.IsFalse(_world.Submit(new TuneChain(P0, ChainKnob.Spring, -1)));
            Assert.IsFalse(_world.Submit(new TuneChain(P0, ChainKnob.Gravity, 1)));
            Assert.IsEmpty(_world.TakeEvents().OfType<ChainTuned>(), "and no event says it did");

            Assert.IsTrue(_world.Submit(new TuneChain(P0, ChainKnob.Spring, 1)), "it moves back off the limit");
        }

        [Test]
        public void AValueAlreadyPastALimit_StaysPut_RatherThanJumpingToIt()
        {
            var tuning = new ChainTuning(1f, 0f, 2f, 1f, -2f, 0f, 1f, 1f, 2f);
            var chain = new ChainSimulation.Settings(0.1f, 5f, 3f, new Vec2(0f, -1f), 0f, 0f);

            var up = tuning.Apply(chain, ChainKnob.Spring, 1);
            Assert.AreEqual(5f, up.SpringConstant, "above springMax: up does nothing");

            var down = tuning.Apply(chain, ChainKnob.Spring, -1);
            Assert.AreEqual(4f, down.SpringConstant, "down moves a step");
        }

        [Test]
        public void TuningWorksWhilePaused_AsForm1sKeyHandlerDoes()
        {
            _world.Submit(new Pause(P0, true));

            Assert.IsTrue(_world.Submit(new TuneChain(P0, ChainKnob.Spring, 1)));
            Assert.AreEqual(0.101f, Readout.Spring, 1e-7f);
        }

        [Test]
        public void AnUnseatedPlayer_TunesNothing()
        {
            Assert.IsFalse(_world.Submit(new TuneChain(new PlayerId(7), ChainKnob.Spring, 1)));
        }

        [Test]
        public void Reset_PutsTheDataFilesFiguresBack()
        {
            _world.Submit(new TuneChain(P0, ChainKnob.Resistance, 1));
            _world.Submit(new TuneChain(P0, ChainKnob.Gravity, -1));
            _world.Submit(new TuneChain(P0, ChainKnob.Spring, 1));

            _world.Submit(new ResetGame(P0));

            Assert.AreEqual(0.25f, Readout.Resistance, 1e-7f);
            Assert.AreEqual(-0.0025f, Readout.Gravity, 1e-9f);
            Assert.AreEqual(0.1f, Readout.Spring, 1e-7f);
        }

        [Test]
        public void ARetune_KeepsTheLiveFigures()
        {
            _world.Submit(new TuneChain(P0, ChainKnob.Spring, 1));

            var setup = _settings.Player;
            setup.WallBounce = 0.25f;
            _world.Configure(P0, setup);

            Assert.AreEqual(0.101f, Readout.Spring, 1e-7f, "the keys' figures are play state; only a reset restores them");
            Assert.AreEqual(0.25f, _player.Chain.WallBounce);
        }

        [Test]
        public void ATunedSpring_ChangesHowTheChainHangs()
        {
            var stiff = new GameWorld(_settings.World, new System.Random(1));
            var stiffPlayer = stiff.AddPlayer(P0, _settings.Player);
            stiff.Reset();
            for (var i = 0; i < 100; i++)
            {
                stiff.Submit(new TuneChain(P0, ChainKnob.Spring, 1));
            }

            for (var i = 0; i < 120; i++)
            {
                _world.Step(1f / 60f);
                stiff.Step(1f / 60f);
            }

            var soft = (_player.MacePosition - _player.Chain.GetPosition(0)).Magnitude;
            var tight = (stiffPlayer.MacePosition - stiffPlayer.Chain.GetPosition(0)).Magnitude;
            Assert.Less(tight, soft, "a stiffer spring stretches less under gravity");
        }

        [Test]
        public void ABadTuningSection_IsNamed()
        {
            var shipped = new DirectorySettingsSource(GameSettingsTests.ShippedFolder);
            var source = new MemorySettingsSource(shipped).Set(
                GameSettings.ChainFile,
                shipped.Read(GameSettings.ChainFile).Replace("\"springMax\": 0.25", "\"springMax\": 0.0005"));

            var e = Assert.Throws<SettingsException>(() => GameSettings.Load(source));

            Assert.IsTrue(e.Errors.Any(x => x.Contains("chain.json.tuning") && x.Contains("'springMax'")), string.Join("\n", e.Errors));
        }
    }
}
