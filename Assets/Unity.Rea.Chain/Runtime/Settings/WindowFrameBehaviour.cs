using System;
using System.Collections;
using System.Runtime.InteropServices;
using MindAttic.Chain.Core;
using UnityEngine;

namespace MindAttic.Chain
{
    /// <summary>
    /// Form1.vb's window is a borderless 640x480 form, so by default (game.json's <c>windowFrame</c>:
    /// <see cref="WindowFrame.Borderless"/>, BIBLE V12, U.CHN-T76) the windowed player strips the OS title bar
    /// and border once its window exists; <see cref="WindowFrame.OsFrame"/> leaves the OS frame alone. Windows
    /// standalone only (the editor, other platforms and fullscreen play have no frame to strip). Created by
    /// <see cref="RuntimeInitializeOnLoadMethodAttribute"/> so no scene needs rebuilding for it.
    /// </summary>
    public sealed class WindowFrameBehaviour : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (Application.isEditor)
            {
                return;
            }

            var go = new GameObject("WindowFrame");
            DontDestroyOnLoad(go);
            go.AddComponent<WindowFrameBehaviour>();
        }

        private IEnumerator Start()
        {
            // The native window is only reliably there after the first frames have been presented.
            yield return null;
            yield return null;

            if (SettingsStore.Current.Game.WindowFrame == WindowFrame.Borderless)
            {
                NativeFrame.Strip();
            }
        }
    }

    /// <summary>The one native bit <see cref="WindowFrameBehaviour"/> needs that Unity's player settings do not expose.</summary>
    internal static class NativeFrame
    {
#if UNITY_STANDALONE_WIN
        private const int GwlStyle = -16;
        private const long WsCaption = 0x00C00000L;
        private const long WsThickFrame = 0x00040000L;
        private const uint SwpNoMove = 0x0002, SwpNoSize = 0x0001, SwpNoZOrder = 0x0004, SwpFrameChanged = 0x0020;

        [DllImport("user32.dll")] private static extern IntPtr GetActiveWindow();
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int index, IntPtr value);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

        /// <summary>Removes the caption and sizing border of a windowed player's window; the client size is kept.</summary>
        public static void Strip()
        {
            if (Screen.fullScreenMode != FullScreenMode.Windowed)
            {
                return;
            }

            var hwnd = GetActiveWindow();
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            var style = GetWindowLongPtr(hwnd, GwlStyle).ToInt64();
            style &= ~(WsCaption | WsThickFrame);
            SetWindowLongPtr(hwnd, GwlStyle, new IntPtr(style));
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoZOrder | SwpFrameChanged);
        }
#else
        /// <summary>Not a Windows standalone player: no frame to strip.</summary>
        public static void Strip() { }
#endif
    }
}
