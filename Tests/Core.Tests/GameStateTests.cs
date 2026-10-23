using MindAttic.Chain.Core;
using NUnit.Framework;

namespace MindAttic.Chain.Tests
{
    public class GameStateTests
    {
        [Test]
        public void StartsAtFullHp_NoScore_Playing()
        {
            var state = new GameState();
            Assert.AreEqual(30, state.Hp, "Angel.vb maxHP");
            Assert.AreEqual(0, state.Score);
            Assert.IsTrue(state.IsPlaying);
        }

        [Test]
        public void TryHurt_RespectsHurtDelay()
        {
            var state = new GameState();
            Assert.IsTrue(state.TryHurt(1f));
            Assert.IsFalse(state.TryHurt(1.1f), "inside the 0.25 s hurt delay");
            Assert.AreEqual(29, state.Hp);
            Assert.IsTrue(state.TryHurt(1.25f));
            Assert.AreEqual(28, state.Hp);
        }

        [Test]
        public void HpRunsOut_GameOver_OnceAndFinal()
        {
            var state = new GameState(new GameState.Settings { MaxHp = 2, HurtDelay = 0f, ContactDamage = 1, PointsPerDamage = 1000f });
            var gameOvers = 0;
            state.GameOver += () => gameOvers++;

            state.TryHurt(0f);
            Assert.IsFalse(state.IsGameOver);
            state.TryHurt(1f);

            Assert.IsTrue(state.IsGameOver);
            Assert.AreEqual(0, state.Hp);
            Assert.AreEqual(1, gameOvers);
            Assert.IsFalse(state.TryHurt(2f));
            Assert.AreEqual(1, gameOvers);
            Assert.IsFalse(state.SetPaused(true), "a finished game cannot be paused");
            Assert.AreEqual(0, state.AddHitScore(1f), "no scoring after game over");
        }

        [Test]
        public void AddHitScore_IsDamageTimes1000()
        {
            var state = new GameState();
            Assert.AreEqual(250, state.AddHitScore(0.25f));
            Assert.AreEqual(1250, state.AddHitScore(1.25f));
            Assert.AreEqual(1500, state.Score);
        }

        [Test]
        public void Paused_IsNotHurt()
        {
            var state = new GameState();
            Assert.IsTrue(state.TogglePause());
            Assert.IsFalse(state.TryHurt(5f));
            Assert.AreEqual(30, state.Hp);
            Assert.IsFalse(state.TogglePause());
            Assert.IsTrue(state.TryHurt(5f));
        }

        [Test]
        public void Heal_CapsAtMax_AndReset_Restores()
        {
            var state = new GameState(new GameState.Settings { MaxHp = 10, HurtDelay = 0f, ContactDamage = 3, PointsPerDamage = 1000f });
            state.TryHurt(0f);
            Assert.AreEqual(7, state.Hp);
            Assert.AreEqual(3, state.Heal(20));
            Assert.AreEqual(10, state.Hp);

            state.AddHitScore(1f);
            state.TryHurt(1f);
            state.Reset();
            Assert.AreEqual(10, state.Hp);
            Assert.AreEqual(0, state.Score);
            Assert.IsTrue(state.TryHurt(0f), "hurt delay cleared by reset");
        }
    }
}
