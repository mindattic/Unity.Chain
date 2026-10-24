using System.IO;
using MindAttic.Chain.Core;
using UnityEngine;

namespace MindAttic.Chain
{
    /// <summary>
    /// The game's settings registry as loaded from the build's data files
    /// (<c>StreamingAssets/Settings/*.json</c>, read by Core's <see cref="GameSettings.Load"/> through a
    /// <see cref="DirectorySettingsSource"/>), with the player profile's tuning overrides
    /// (<see cref="PlayerPreferences.TuningOverrides"/>) laid over them by a <see cref="TuningOverrideSource"/>.
    /// Loaded once, on first use; a stock file that fails validation logs every problem and stops the game from
    /// starting rather than playing with a guess, while overrides that fail are logged and the stock figures used.
    /// The authority (<see cref="GameSession.Settings"/>) seats its players from it; components that draw or sound
    /// read their defaults from it until they are seated. Tests can set <see cref="Current"/> to their own registry.
    /// </summary>
    public static class SettingsStore
    {
        private static GameSettings s_stock;
        private static GameSettings s_current;

        /// <summary>The folder the data files are read from.</summary>
        public static string Folder => Path.Combine(Application.streamingAssetsPath, GameSettings.Folder);

        /// <summary>The data files exactly as shipped, no profile overrides (the stock volumes come from here).</summary>
        public static GameSettings Stock => s_stock ?? (s_stock = Load());

        /// <summary>The registry the game plays with: <see cref="Stock"/> with the profile's tuning overrides. Set it to use another registry.</summary>
        public static GameSettings Current
        {
            get => s_current ?? (s_current = WithOverrides());
            set => s_current = value;
        }

        /// <summary>Loads the stock registry from <see cref="Folder"/>, logging and rethrowing a <see cref="SettingsException"/>.</summary>
        public static GameSettings Load()
        {
            try
            {
                return GameSettings.Load(new DirectorySettingsSource(Folder));
            }
            catch (SettingsException e)
            {
                Debug.LogError(e.Message);
                throw;
            }
        }

        private static GameSettings WithOverrides()
        {
            var stock = Stock;
            var overrides = PlayerProfile.Preferences.TuningOverrides;
            if (overrides.Count == 0)
            {
                return stock;
            }

            try
            {
                return GameSettings.Load(new TuningOverrideSource(new DirectorySettingsSource(Folder), overrides));
            }
            catch (SettingsException e)
            {
                Debug.LogWarning("[Chain] The profile's tuning overrides were not used: " + e.Message);
                return stock;
            }
        }

        /// <summary>Forgets both registries, so the next use reads the files (and the profile's overrides) again.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reload()
        {
            s_stock = null;
            s_current = null;
        }
    }
}
