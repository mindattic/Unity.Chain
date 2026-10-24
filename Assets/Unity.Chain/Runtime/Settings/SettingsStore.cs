using System.IO;
using MindAttic.Chain.Core;
using UnityEngine;

namespace MindAttic.Chain
{
    /// <summary>
    /// The game's settings registry as loaded from the build's data files
    /// (<c>StreamingAssets/Settings/*.json</c>, read by Core's <see cref="GameSettings.Load"/> through a
    /// <see cref="DirectorySettingsSource"/>). Loaded once, on first use; a file that fails validation logs
    /// every problem and stops the game from starting rather than playing with a guess. The authority
    /// (<see cref="GameSession.Settings"/>) seats its players from it; components that draw or sound read
    /// their defaults from it until they are seated. Tests can set <see cref="Current"/> to their own registry.
    /// </summary>
    public static class SettingsStore
    {
        private static GameSettings s_current;

        /// <summary>The folder the data files are read from.</summary>
        public static string Folder => Path.Combine(Application.streamingAssetsPath, GameSettings.Folder);

        /// <summary>The loaded registry (loading it on first use). Set it to use another registry.</summary>
        public static GameSettings Current
        {
            get => s_current ?? (s_current = Load());
            set => s_current = value;
        }

        /// <summary>Loads the registry from <see cref="Folder"/>, logging and rethrowing a <see cref="SettingsException"/>.</summary>
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Clear() => s_current = null;
    }
}
