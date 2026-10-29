using System;

namespace MindAttic.Chain.Core
{
    /// <summary>
    /// The pixel math behind the rebuild's aspect-locked window (UNI-T38): the original was a fixed 640x480,
    /// 4:3 form (RECONSTRUCTION.md "Window and options"); the rebuild's window is resizable (UNI-T36/UNI-T59's
    /// resolution picker needs it to be), so nothing in Unity itself stops a player dragging it to another
    /// shape. This works out, in pixels only, whether a resize left 4:3 and what to snap it back to; the
    /// Unity side (<c>Screen</c>) applies it every frame the size changed.
    /// </summary>
    public static class WindowAspectLock
    {
        /// <summary>The original's shape: width divided by height.</summary>
        public const float Aspect = 4f / 3f;

        /// <summary>True if <paramref name="width"/> x <paramref name="height"/> is already 4:3.</summary>
        public static bool IsLocked(int width, int height)
        {
            return width > 0 && height > 0 && HeightFor(width) == height;
        }

        /// <summary>The 4:3 height for <paramref name="width"/>.</summary>
        public static int HeightFor(int width) => (int)Math.Round(width / Aspect, MidpointRounding.AwayFromZero);

        /// <summary>The 4:3 width for <paramref name="height"/>.</summary>
        public static int WidthFor(int height) => (int)Math.Round(height * Aspect, MidpointRounding.AwayFromZero);

        /// <summary>
        /// A window resize from (<paramref name="lastWidth"/>, <paramref name="lastHeight"/>) to
        /// (<paramref name="width"/>, <paramref name="height"/>). False (and the size unchanged) if the new
        /// size is already 4:3 or not a real size; otherwise <paramref name="targetWidth"/>/
        /// <paramref name="targetHeight"/> hold the 4:3 size to snap back to, keeping whichever side the
        /// player actually moved (a corner drag, where both moved, keeps the width).
        /// </summary>
        public static bool Resolve(int lastWidth, int lastHeight, int width, int height, out int targetWidth, out int targetHeight)
        {
            targetWidth = width;
            targetHeight = height;

            if (width <= 0 || height <= 0 || IsLocked(width, height))
            {
                return false;
            }

            var onlyHeightChanged = height != lastHeight && width == lastWidth;
            if (onlyHeightChanged)
            {
                targetWidth = WidthFor(height);
                targetHeight = height;
            }
            else
            {
                targetWidth = width;
                targetHeight = HeightFor(width);
            }

            return true;
        }
    }
}
