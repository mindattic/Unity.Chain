using System;

namespace MindAttic.Chain.Core
{
    /// <summary>
    /// Gui.vb's renderFPS counter: frames are counted, and every <see cref="SampleMs"/> (iFPSProfileSpeed, 100 ms)
    /// of real time the count becomes the frame rate, <c>iCurrCnt * (1000 / iFPSProfileSpeed)</c>, and starts over.
    /// The readout so moves in steps of 10 fps and is one sample old.
    ///
    /// The original called renderFPS twice a frame (once at the top of oneFrameRender and once more at the end),
    /// so it counted every frame twice and showed double the real rate; here a frame is counted once (BIBLE D20).
    /// </summary>
    public sealed class FrameRateCounter
    {
        private long _lastCheck;
        private int _count;

        public FrameRateCounter(int sampleMs)
        {
            if (sampleMs <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleMs), "must be above 0");
            }

            SampleMs = sampleMs;
        }

        /// <summary>How often, in milliseconds of real time, the count becomes the frame rate.</summary>
        public int SampleMs { get; }

        /// <summary>iFrameRate: frames a second at the last sample (0 before the first).</summary>
        public int FrameRate { get; private set; }

        /// <summary>
        /// One drawn frame at <paramref name="nowMs"/> (a millisecond clock, like Environment.TickCount): samples
        /// the rate once <see cref="SampleMs"/> has passed since the last sample, then counts this frame.
        /// </summary>
        public void Frame(long nowMs)
        {
            if (nowMs - _lastCheck >= SampleMs)
            {
                _lastCheck = nowMs;
                FrameRate = (int)Math.Round(_count * (1000d / SampleMs), MidpointRounding.ToEven);
                _count = 0;
            }

            _count++;
        }
    }
}
