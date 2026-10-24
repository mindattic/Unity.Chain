using System;
using MindAttic.Chain.Core;
using NUnit.Framework;

namespace MindAttic.Chain.Tests
{
    public class ScreenShakeTests
    {
        [Test]
        public void FirstCall_Restores_AndArms()
        {
            var shake = new ScreenShake(0.18f, new Random(1));

            Assert.IsFalse(shake.Armed, "Root.vb: shake starts False");
            Assert.AreEqual(Vec2.Zero, shake.Shake());
            Assert.IsTrue(shake.Armed);
        }

        [Test]
        public void AlternateCalls_JumpRightAndDown_ThenRestore()
        {
            const float amount = 0.18f;
            var shake = new ScreenShake(amount, new Random(7));
            shake.Shake();

            for (var i = 0; i < 200; i++)
            {
                var offset = shake.Shake();
                Assert.IsFalse(shake.Armed);
                Assert.That(offset.X, Is.InRange(0f, amount * 2f), "two draws of 0..amount to the right");
                Assert.That(offset.Y, Is.InRange(-amount * 2f, 0f), "two draws of 0..amount down");

                Assert.AreEqual(Vec2.Zero, shake.Shake(), "target = cam");
                Assert.IsTrue(shake.Armed);
            }
        }

        [Test]
        public void TheDraws_AreReallyRandom_BibleD19()
        {
            // The original's getRndNum(0, 0.02) was Random.Next(0, 1) on integers: always 0, so nothing moved.
            var shake = new ScreenShake(0.18f, new Random(3));
            var moved = 0;
            shake.Shake();
            for (var i = 0; i < 50; i++)
            {
                if (shake.Shake() != Vec2.Zero)
                {
                    moved++;
                }

                shake.Shake();
            }

            Assert.AreEqual(50, moved);
        }

        [Test]
        public void Recenter_ClearsTheOffset_AndKeepsTheFlag()
        {
            var shake = new ScreenShake(0.18f, new Random(5));
            shake.Shake();
            shake.Shake();

            shake.Recenter();

            Assert.AreEqual(Vec2.Zero, shake.Offset);
            Assert.IsFalse(shake.Armed, "Reset never touches shake");
        }

        [Test]
        public void ZeroOrNegativeAmount_NeverMoves()
        {
            var shake = new ScreenShake(-1f, new Random(2));
            Assert.AreEqual(0f, shake.Amount);
            shake.Shake();
            Assert.AreEqual(Vec2.Zero, shake.Shake());
        }
    }
}
