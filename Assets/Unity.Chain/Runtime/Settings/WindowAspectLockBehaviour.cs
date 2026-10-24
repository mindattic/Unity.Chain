using MindAttic.Chain.Core;
using UnityEngine;

namespace MindAttic.Chain
{
    /// <summary>
    /// Applies <see cref="WindowAspectLock"/> to the live window: while windowed, a resize that leaves 4:3
    /// (dragging a border or corner) is snapped back the same frame it happens. Fullscreen, borderless and
    /// maximized windows are left alone -- there is nothing for the player to drag, and <see cref="Screen.SetResolution"/>
    /// in those modes would itself change the mode (UNI-T38; RECONSTRUCTION.md "Window and options").
    /// </summary>
    public sealed class WindowAspectLockBehaviour : MonoBehaviour
    {
        private int _lastWidth;
        private int _lastHeight;

        private void OnEnable()
        {
            _lastWidth = Screen.width;
            _lastHeight = Screen.height;
        }

        private void Update()
        {
            if (Application.isEditor || Screen.fullScreenMode != FullScreenMode.Windowed)
            {
                return;
            }

            var width = Screen.width;
            var height = Screen.height;
            if (width == _lastWidth && height == _lastHeight)
            {
                return;
            }

            if (WindowAspectLock.Resolve(_lastWidth, _lastHeight, width, height, out var targetWidth, out var targetHeight))
            {
                Screen.SetResolution(targetWidth, targetHeight, Screen.fullScreenMode);
                width = targetWidth;
                height = targetHeight;
            }

            _lastWidth = width;
            _lastHeight = height;
        }
    }
}
