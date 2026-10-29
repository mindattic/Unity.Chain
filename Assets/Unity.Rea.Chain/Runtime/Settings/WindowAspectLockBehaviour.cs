using System;
using System.Runtime.InteropServices;
using MindAttic.Chain.Core;
using UnityEngine;

namespace MindAttic.Chain
{
    /// <summary>
    /// Applies <see cref="WindowAspectLock"/> to the live window. Unity has no window-message hook to tell a
    /// live border drag apart from any other resize, so this cannot correct a drag as it happens: Windows keeps
    /// driving the drag from the mouse delta regardless of what <see cref="Screen.SetResolution"/> says mid-drag,
    /// so trying to would just fight the player's own hand (it only ever fired on a changed frame, before
    /// UNI-T38's review found it fighting edge drags this way) and the size is only known to have settled once
    /// it stops changing for a few frames in a row -- which is also after the player lets go of the border.
    /// A maximized window is left alone (<see cref="NativeWindow.IsMaximized"/>): it still reports
    /// <see cref="FullScreenMode.Windowed"/> on Windows (<see cref="FullScreenMode.MaximizedWindow"/> is
    /// macOS-only), so without this check a maximize would itself count as a non-4:3 resize and get undone.
    /// </summary>
    public sealed class WindowAspectLockBehaviour : MonoBehaviour
    {
        /// <summary>How many consecutive frames the size must hold before it counts as settled, not mid-drag.</summary>
        private const int FramesToSettle = 3;

        private int _lastWidth;
        private int _lastHeight;
        private int _pendingWidth;
        private int _pendingHeight;
        private int _stableFrames;

        private void OnEnable()
        {
            _lastWidth = Screen.width;
            _lastHeight = Screen.height;
            _pendingWidth = _lastWidth;
            _pendingHeight = _lastHeight;
            _stableFrames = 0;
        }

        private void Update()
        {
            if (Application.isEditor || Screen.fullScreenMode != FullScreenMode.Windowed || NativeWindow.IsMaximized())
            {
                // Nothing to correct here: remember the size so a later frame does not read it as a drag.
                _lastWidth = Screen.width;
                _lastHeight = Screen.height;
                _stableFrames = 0;
                return;
            }

            var width = Screen.width;
            var height = Screen.height;

            if (width == _pendingWidth && height == _pendingHeight)
            {
                _stableFrames++;
            }
            else
            {
                _pendingWidth = width;
                _pendingHeight = height;
                _stableFrames = 0;
            }

            if (width == _lastWidth && height == _lastHeight)
            {
                return; // nothing has changed since the lock last ran.
            }

            if (_stableFrames < FramesToSettle)
            {
                return; // still moving: Windows is still delivering the drag, let it finish.
            }

            if (WindowAspectLock.Resolve(_lastWidth, _lastHeight, width, height, out var targetWidth, out var targetHeight))
            {
                Screen.SetResolution(targetWidth, targetHeight, Screen.fullScreenMode);
                width = targetWidth;
                height = targetHeight;
            }

            _lastWidth = width;
            _lastHeight = height;
            _pendingWidth = width;
            _pendingHeight = height;
            _stableFrames = 0;
        }
    }

    /// <summary>The one native bit <see cref="WindowAspectLockBehaviour"/> needs that <see cref="Screen"/> does not expose.</summary>
    internal static class NativeWindow
    {
#if UNITY_STANDALONE_WIN
        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll")]
        private static extern bool IsZoomed(IntPtr hWnd);

        /// <summary>True if the foreground window is maximized (Windows' "Zoomed").</summary>
        public static bool IsMaximized()
        {
            var hwnd = GetActiveWindow();
            return hwnd != IntPtr.Zero && IsZoomed(hwnd);
        }
#else
        /// <summary>Not a Windows standalone player: nothing to ask, so never maximized by this check.</summary>
        public static bool IsMaximized() => false;
#endif
    }
}
