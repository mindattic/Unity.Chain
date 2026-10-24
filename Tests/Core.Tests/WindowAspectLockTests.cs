using MindAttic.Chain.Core;
using NUnit.Framework;

namespace MindAttic.Chain.Tests
{
    /// <summary>
    /// UNI-T38: the rebuild's window defaults to the original's 640x480, 4:3 shape and stays 4:3 when the
    /// player resizes it, since Unity itself has no window aspect lock.
    /// </summary>
    public class WindowAspectLockTests
    {
        [TestCase(640, 480)]
        [TestCase(800, 600)]
        [TestCase(1024, 768)]
        public void IsLocked_IsTrue_ForThe4By3SizesTheOptionsScreenOffers(int width, int height)
        {
            Assert.IsTrue(WindowAspectLock.IsLocked(width, height));
        }

        [TestCase(1600, 900)]
        [TestCase(800, 450)]
        [TestCase(640, 481)]
        public void IsLocked_IsFalse_ForAnythingElse(int width, int height)
        {
            Assert.IsFalse(WindowAspectLock.IsLocked(width, height));
        }

        [Test]
        public void Resolve_DoesNothing_WhenTheNewSizeIsAlready4By3()
        {
            // DisplayPreferences.Apply() (UNI-T36) can itself change Screen size to one of the profile's
            // 4:3 choices; the lock must not fight that.
            var changed = WindowAspectLock.Resolve(640, 480, 800, 600, out var width, out var height);

            Assert.IsFalse(changed);
            Assert.AreEqual(800, width);
            Assert.AreEqual(600, height);
        }

        [Test]
        public void Resolve_KeepsTheWidth_WhenOnlyTheWidthChanged()
        {
            // The player drags the right edge: width moves, height does not.
            var changed = WindowAspectLock.Resolve(640, 480, 800, 480, out var width, out var height);

            Assert.IsTrue(changed);
            Assert.AreEqual(800, width);
            Assert.AreEqual(600, height);
        }

        [Test]
        public void Resolve_KeepsTheHeight_WhenOnlyTheHeightChanged()
        {
            // The player drags the bottom edge: height moves, width does not.
            var changed = WindowAspectLock.Resolve(640, 480, 640, 600, out var width, out var height);

            Assert.IsTrue(changed);
            Assert.AreEqual(800, width);
            Assert.AreEqual(600, height);
        }

        [Test]
        public void Resolve_KeepsTheWidth_WhenBothSidesChanged()
        {
            // A corner drag moves both; the width wins, as a Windows resize grip reports the horizontal
            // drag first.
            var changed = WindowAspectLock.Resolve(640, 480, 900, 500, out var width, out var height);

            Assert.IsTrue(changed);
            Assert.AreEqual(900, width);
            Assert.AreEqual(675, height);
        }

        [Test]
        public void Resolve_IgnoresNonsenseSizes()
        {
            Assert.IsFalse(WindowAspectLock.Resolve(640, 480, 0, 480, out _, out _));
            Assert.IsFalse(WindowAspectLock.Resolve(640, 480, 640, 0, out _, out _));
        }
    }
}
