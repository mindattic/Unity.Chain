using System.Collections.Generic;
using MindAttic.Chain.Core;
using NUnit.Framework;

namespace MindAttic.Chain.Tests
{
    public class SwingSoundTriggerTests
    {
        private const float Dt = 1f / 50f;

        [Test]
        public void FiresOnce_WhenSpeedCrossesTheSwingSpeed()
        {
            var trigger = new SwingSoundTrigger(swingSpeed: 6f, rearmSpeed: 3f, cooldown: 0f, repeatInterval: 0f);

            Assert.IsFalse(trigger.Step(2f, Dt));
            Assert.IsTrue(trigger.Step(7f, Dt));
            Assert.IsTrue(trigger.IsSwinging);
            for (var i = 0; i < 50; i++)
            {
                Assert.IsFalse(trigger.Step(9f, Dt), "a held swing must not re-fire without a repeat interval");
            }
        }

        [Test]
        public void Rearms_OnlyAfterSlowingBelowTheRearmSpeed()
        {
            var trigger = new SwingSoundTrigger(6f, 3f, cooldown: 0f, repeatInterval: 0f);
            Assert.IsTrue(trigger.Step(7f, Dt));

            Assert.IsFalse(trigger.Step(4f, Dt)); // slowed, but not below the rearm speed
            Assert.IsFalse(trigger.Step(7f, Dt));

            Assert.IsFalse(trigger.Step(2f, Dt));
            Assert.IsFalse(trigger.IsSwinging);
            Assert.IsTrue(trigger.Step(7f, Dt));
        }

        [Test]
        public void Cooldown_BlocksAnImmediateSecondSwing()
        {
            var trigger = new SwingSoundTrigger(6f, 3f, cooldown: 0.2f, repeatInterval: 0f);
            Assert.IsTrue(trigger.Step(7f, Dt));
            Assert.IsFalse(trigger.Step(0f, Dt));
            Assert.IsFalse(trigger.Step(7f, Dt), "still inside the cooldown");

            for (var i = 0; i < 10; i++)
            {
                trigger.Step(0f, Dt);
            }

            Assert.IsTrue(trigger.Step(7f, Dt));
        }

        [Test]
        public void ContinuousWhirl_RepeatsAtTheRepeatInterval()
        {
            var trigger = new SwingSoundTrigger(6f, 3f, cooldown: 0f, repeatInterval: 0.5f);
            var fired = 0;
            for (var i = 0; i < 100; i++) // two seconds above the swing speed
            {
                if (trigger.Step(10f, Dt))
                {
                    fired++;
                }
            }

            Assert.AreEqual(4, fired);
        }

        [Test]
        public void VolumeForSpeed_RisesFromMinToFull()
        {
            Assert.AreEqual(0.25f, SwingSoundTrigger.VolumeForSpeed(6f, 6f, 18f, 0.25f), 1e-5f);
            Assert.AreEqual(0.625f, SwingSoundTrigger.VolumeForSpeed(12f, 6f, 18f, 0.25f), 1e-5f);
            Assert.AreEqual(1f, SwingSoundTrigger.VolumeForSpeed(40f, 6f, 18f, 0.25f), 1e-5f);
            Assert.AreEqual(1f, SwingSoundTrigger.VolumeForSpeed(6f, 6f, 6f, 0.25f), 1e-5f);
        }
    }

    public class ClipPickerTests
    {
        [Test]
        public void NeverRepeatsTheSameClipTwiceInARow_AndUsesThemAll()
        {
            var picker = new ClipPicker(seed: 1234);
            var seen = new HashSet<int>();
            var last = -1;
            for (var i = 0; i < 500; i++)
            {
                var index = picker.Next(7);
                Assert.That(index, Is.InRange(0, 6));
                Assert.AreNotEqual(last, index);
                seen.Add(index);
                last = index;
            }

            Assert.AreEqual(7, seen.Count);
        }

        [Test]
        public void SingleAndEmptySets()
        {
            var picker = new ClipPicker(seed: 1);
            Assert.AreEqual(-1, picker.Next(0));
            Assert.AreEqual(0, picker.Next(1));
            Assert.AreEqual(0, picker.Next(1));
        }
    }
}
