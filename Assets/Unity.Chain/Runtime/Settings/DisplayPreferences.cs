using MindAttic.Chain.Core;
using UnityEngine;

namespace MindAttic.Chain
{
    /// <summary>
    /// Puts the profile's resolution and window mode (<see cref="PlayerPreferences.Resolution"/>,
    /// <see cref="PlayerPreferences.WindowMode"/>) into effect in a player build, before the first scene loads. Either
    /// left unset keeps what the build would have done; the Editor's Game view is never resized.
    /// </summary>
    public static class DisplayPreferences
    {
        /// <summary>The Unity mode for <paramref name="mode"/>.</summary>
        public static FullScreenMode ToUnity(WindowMode mode)
        {
            switch (mode)
            {
                case WindowMode.Borderless: return FullScreenMode.FullScreenWindow;
                case WindowMode.Fullscreen: return FullScreenMode.ExclusiveFullScreen;
                case WindowMode.Maximized: return FullScreenMode.MaximizedWindow;
                default: return FullScreenMode.Windowed;
            }
        }

        /// <summary>The size and mode <paramref name="preferences"/> ask for, given the screen's current ones; false if they ask for nothing.</summary>
        public static bool Resolve(PlayerPreferences preferences, int width, int height, FullScreenMode current,
            out int targetWidth, out int targetHeight, out FullScreenMode targetMode)
        {
            targetWidth = preferences.Resolution?.Width ?? width;
            targetHeight = preferences.Resolution?.Height ?? height;
            targetMode = preferences.WindowMode.HasValue ? ToUnity(preferences.WindowMode.Value) : current;
            return preferences.Resolution.HasValue || preferences.WindowMode.HasValue;
        }

        /// <summary>Applies <see cref="PlayerProfile.Preferences"/>' display settings to the screen (player builds only).</summary>
        public static void Apply()
        {
            if (Application.isEditor)
            {
                return;
            }

            if (Resolve(PlayerProfile.Preferences, Screen.width, Screen.height, Screen.fullScreenMode,
                    out var width, out var height, out var mode))
            {
                Screen.SetResolution(width, height, mode);
            }
        }

        /// <summary>
        /// Form1.vb's F12 and the menu's <c>F12 - Configure Video</c>: windowed becomes fullscreen and fullscreen
        /// windowed (<see cref="PlayerPreferences.ToggleScreen"/>), kept in the profile as <c>saveOptions</c> kept it in
        /// the registry, and put into effect in a player build. The original restarted its engine to rebuild the D3D
        /// device; Unity switches in place, so the round goes on (BIBLE V21). Returns the new mode.
        /// </summary>
        public static WindowMode ToggleScreen()
        {
            var mode = PlayerProfile.Preferences.ToggleScreen(Screen.fullScreenMode != FullScreenMode.Windowed);
            PlayerProfile.Save();
            Apply();
            return mode;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyOnStart() => Apply();
    }
}
