using System;
using MindAttic.Chain.Core;
using Unity.Shared.Core;
using Unity.Shared.Core.Json;

namespace MindAttic.Chain
{
    /// <summary>
    /// Core's <see cref="IPreferencesStore"/> over a shared MindAttic player profile's settings.json (the Unity.Shared
    /// package, format mindattic.settings v1): the volumes, mutes and reduce motion are its <c>common</c> block
    /// (<c>Profiles.Current.Settings.Common</c>, what every MindAttic game reads the same way), the resolution, window
    /// mode and tuning overrides Chain's own <c>game</c> block (keys in <see cref="PlayerPreferences.GameKeys"/>).
    /// Fields it does not know, in either block, are left as they are.
    /// </summary>
    public sealed class ProfilePreferencesStore : IPreferencesStore
    {
        private readonly Func<Profile> _profile;

        /// <param name="profile">The profile to use, asked for on every read and write so a profile switch is followed.</param>
        public ProfilePreferencesStore(Func<Profile> profile)
        {
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        }

        /// <summary>The store over the profile in use (<c>Profiles.Current</c>).</summary>
        public static ProfilePreferencesStore ForCurrentProfile() => new ProfilePreferencesStore(() => Profiles.Current);

        public string Describe => _profile().SettingsPath;

        public PlayerPreferences Read()
        {
            var settings = _profile().Settings;
            var common = settings.Common;
            var game = settings.Game;
            var preferences = new PlayerPreferences
            {
                MusicVolume = common.MusicVolume,
                SfxVolume = common.SfxVolume,
                MuteMusic = common.MuteMusic,
                MuteSfx = common.MuteSfx,
                ReduceMotion = common.ReduceMotion,
                StockVolumesApplied = game.GetBool(PlayerPreferences.GameKeys.StockVolumes),
                WindowMode = PlayerPreferences.ParseWindowMode(game.GetString(PlayerPreferences.GameKeys.WindowMode)),
            };

            if (game.ContainsKey(PlayerPreferences.GameKeys.AmbientVolume))
            {
                preferences.AmbientVolume = game.GetFloat(PlayerPreferences.GameKeys.AmbientVolume, 1f);
            }

            var resolution = game.GetObject(PlayerPreferences.GameKeys.Resolution);
            if (resolution != null)
            {
                var width = resolution.GetInt(PlayerPreferences.GameKeys.Width);
                var height = resolution.GetInt(PlayerPreferences.GameKeys.Height);
                if (width > 0 && height > 0)
                {
                    preferences.Resolution = new DisplayResolution(width, height);
                }
            }

            var tuning = game.GetObject(PlayerPreferences.GameKeys.Tuning);
            if (tuning != null)
            {
                foreach (var file in tuning)
                {
                    if (file.Value is JsonObject patch)
                    {
                        preferences.TuningOverrides[file.Key] = patch.ToJson();
                    }
                }
            }

            return preferences;
        }

        public bool Write(PlayerPreferences preferences)
        {
            if (preferences == null)
            {
                throw new ArgumentNullException(nameof(preferences));
            }

            var profile = _profile();
            var settings = profile.Settings;
            var common = settings.Common;
            common.MusicVolume = preferences.MusicVolume;
            common.SfxVolume = preferences.SfxVolume;
            common.MuteMusic = preferences.MuteMusic;
            common.MuteSfx = preferences.MuteSfx;
            common.ReduceMotion = preferences.ReduceMotion;

            var game = settings.Game;
            game.Set(PlayerPreferences.GameKeys.StockVolumes, preferences.StockVolumesApplied);
            if (preferences.HasAmbientVolume)
            {
                game.Set(PlayerPreferences.GameKeys.AmbientVolume, preferences.AmbientVolume);
            }

            if (preferences.Resolution is DisplayResolution size)
            {
                game.Set(PlayerPreferences.GameKeys.Resolution, new JsonObject()
                    .Set(PlayerPreferences.GameKeys.Width, size.Width)
                    .Set(PlayerPreferences.GameKeys.Height, size.Height));
            }
            else
            {
                game.Remove(PlayerPreferences.GameKeys.Resolution);
            }

            if (preferences.WindowMode is WindowMode mode)
            {
                game.Set(PlayerPreferences.GameKeys.WindowMode, PlayerPreferences.FormatWindowMode(mode));
            }
            else
            {
                game.Remove(PlayerPreferences.GameKeys.WindowMode);
            }

            if (preferences.TuningOverrides.Count > 0)
            {
                var tuning = new JsonObject();
                foreach (var pair in preferences.TuningOverrides)
                {
                    tuning.Set(pair.Key, JsonObject.Parse(pair.Value));
                }

                game.Set(PlayerPreferences.GameKeys.Tuning, tuning);
            }
            else
            {
                game.Remove(PlayerPreferences.GameKeys.Tuning);
            }

            return profile.TrySaveSettings();
        }
    }
}
