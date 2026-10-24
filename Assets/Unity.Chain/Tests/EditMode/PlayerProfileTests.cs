using System;
using System.IO;
using MindAttic.Chain;
using MindAttic.Chain.Core;
using NUnit.Framework;
using Unity.Shared.Core;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Keeps every EditMode test off the developer's real profile: the preferences are read from (and saved to) memory,
/// at the stock volumes, for the whole run.
/// </summary>
[SetUpFixture]
public class PlayerProfileIsolation
{
    [OneTimeSetUp]
    public void UseMemory()
    {
        SettingsStore.Reload();
        PlayerProfile.Store = new MemoryPreferencesStore();
    }

    [OneTimeTearDown]
    public void Restore()
    {
        PlayerProfile.Reset();
        SettingsStore.Reload();
    }
}

namespace MindAttic.Chain.Tests
{
    /// <summary>
    /// UNI-T36: the player's preferences live in the shared MindAttic profile (Unity.Shared's settings.json): the volumes,
    /// mutes and reduce motion in its common block, Chain's resolution, window mode and tuning overrides in its game
    /// block; the music and sound effects play at them.
    /// </summary>
    public class PlayerProfileTests
    {
        private string _root;
        private ProfileStore _profiles;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "ChainProfiles-" + Guid.NewGuid().ToString("N"));
            _profiles = new ProfileStore(_root);
            PlayerProfile.Store = new MemoryPreferencesStore();
        }

        [TearDown]
        public void TearDown()
        {
            PlayerProfile.Store = new MemoryPreferencesStore();
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        private ProfilePreferencesStore StoreOver(Profile profile) => new ProfilePreferencesStore(() => profile);

        [Test]
        public void TheCommonBlock_HoldsTheVolumesMutesAndReduceMotion()
        {
            var profile = _profiles.Current;
            var store = StoreOver(profile);
            var preferences = PlayerPreferences.Load(store, SettingsStore.Stock.AudioTuning);
            preferences.MusicVolume = 0.4f;
            preferences.SfxVolume = 0.2f;
            preferences.MuteMusic = true;
            preferences.ReduceMotion = true;
            Assert.IsTrue(store.Write(preferences));

            var common = profile.ReloadSettings().Common;
            Assert.AreEqual(0.4f, common.MusicVolume, 1e-6f);
            Assert.AreEqual(0.2f, common.SfxVolume, 1e-6f);
            Assert.IsTrue(common.MuteMusic);
            Assert.IsFalse(common.MuteSfx);
            Assert.IsTrue(common.ReduceMotion);
            Assert.IsTrue(profile.Settings.Game.GetBool(PlayerPreferences.GameKeys.StockVolumes));
        }

        [Test]
        public void AFreshProfile_ReadsTheOriginalsVolumes_NotTheSharedDefaults()
        {
            var preferences = PlayerPreferences.Load(StoreOver(_profiles.Current), SettingsStore.Stock.AudioTuning);
            Assert.AreEqual(1f, preferences.MusicVolume, 1e-6f);
            Assert.AreEqual(0.3162f, preferences.SfxVolume, 1e-4f);
        }

        [Test]
        public void TheGameBlock_HoldsResolutionWindowModeAndTuning_AndRoundTrips()
        {
            var profile = _profiles.Current;
            var store = StoreOver(profile);
            var preferences = store.Read();
            preferences.Resolution = new DisplayResolution(1600, 900);
            preferences.WindowMode = WindowMode.Borderless;
            Assert.IsFalse(preferences.HasAmbientVolume, "a profile with no ambient volume reads none");
            preferences.AmbientVolume = 0.3f; // frmConfig's Ambient slider (UNI-T59): Chain's own key, not the common block
            preferences.TuningOverrides["chain.json"] = "{\"springK\": 0.5, \"port\": {\"hitRadius\": 0.45}}";
            profile.Settings.Game.Set("somethingElse", 7); // a field this game does not know survives
            Assert.IsTrue(store.Write(preferences));

            var game = profile.ReloadSettings().Game;
            Assert.AreEqual("borderless", game.GetString(PlayerPreferences.GameKeys.WindowMode));
            Assert.AreEqual(1600, game.GetObject(PlayerPreferences.GameKeys.Resolution).GetInt("width"));
            Assert.AreEqual(0.45f, game.GetObject("tuning").GetObject("chain.json").GetObject("port").GetFloat("hitRadius"));
            Assert.AreEqual(7, game.GetInt("somethingElse"));
            Assert.AreEqual(0.3f, game.GetFloat(PlayerPreferences.GameKeys.AmbientVolume), 1e-6f);

            var again = store.Read();
            Assert.AreEqual(0.3f, again.AmbientVolume, 1e-6f);
            Assert.AreEqual(new DisplayResolution(1600, 900), again.Resolution);
            Assert.AreEqual(WindowMode.Borderless, again.WindowMode);
            var tuned = GameSettings.Load(new TuningOverrideSource(new DirectorySettingsSource(SettingsStore.Folder), again.TuningOverrides));
            Assert.AreEqual(0.5f, tuned.Chain.SpringK);
            Assert.AreEqual(0.45f, tuned.Chain.Port.HitRadius);

            again.Resolution = null;
            again.WindowMode = null;
            again.TuningOverrides.Clear();
            store.Write(again);
            game = profile.ReloadSettings().Game;
            Assert.IsFalse(game.ContainsKey(PlayerPreferences.GameKeys.Resolution));
            Assert.IsFalse(game.ContainsKey(PlayerPreferences.GameKeys.WindowMode));
            Assert.IsFalse(game.ContainsKey(PlayerPreferences.GameKeys.Tuning));
        }

        [Test]
        public void SettingsStore_LaysTheProfilesTuningOverTheStockFiles()
        {
            var preferences = new PlayerPreferences();
            preferences.TuningOverrides["chain.json"] = "{\"springK\": 0.5}";
            PlayerProfile.Store = new MemoryPreferencesStore(preferences);
            SettingsStore.Reload();
            try
            {
                Assert.AreEqual(0.5f, SettingsStore.Current.Chain.SpringK);
                Assert.AreNotEqual(0.5f, SettingsStore.Stock.Chain.SpringK);
            }
            finally
            {
                PlayerProfile.Store = new MemoryPreferencesStore();
                SettingsStore.Reload();
            }
        }

        [Test]
        public void Display_ResolvesOnlyWhatThePlayerChose()
        {
            var none = new PlayerPreferences();
            Assert.IsFalse(DisplayPreferences.Resolve(none, 800, 600, FullScreenMode.Windowed, out var w, out var h, out var mode));
            Assert.AreEqual((800, 600, FullScreenMode.Windowed), (w, h, mode));

            var borderless = new PlayerPreferences { WindowMode = WindowMode.Borderless };
            Assert.IsTrue(DisplayPreferences.Resolve(borderless, 800, 600, FullScreenMode.Windowed, out w, out h, out mode));
            Assert.AreEqual((800, 600, FullScreenMode.FullScreenWindow), (w, h, mode));

            var sized = new PlayerPreferences { Resolution = new DisplayResolution(1280, 720) };
            Assert.IsTrue(DisplayPreferences.Resolve(sized, 800, 600, FullScreenMode.ExclusiveFullScreen, out w, out h, out mode));
            Assert.AreEqual((1280, 720, FullScreenMode.ExclusiveFullScreen), (w, h, mode));
        }

        [Test]
        public void MusicPlayer_PlaysAtTheProfilesMusicVolumeAndMute()
        {
            var preferences = new PlayerPreferences { MusicVolume = 0.3f, StockVolumesApplied = true };
            PlayerProfile.Store = new MemoryPreferencesStore(preferences);
            var go = new GameObject("Music");
            try
            {
                var player = go.AddComponent<MusicPlayer>();
                player.Apply(); // Awake and OnEnable do not run outside Play mode
                var source = go.GetComponent<AudioSource>();
                Assert.AreEqual(0.3f, source.volume, 1e-6f);
                Assert.IsFalse(source.mute);

                // The player's own volume and mute are the profile's.
                player.Volume = 0.8f;
                player.Muted = true;
                Assert.AreEqual(0.8f, PlayerProfile.Preferences.MusicVolume, 1e-6f);
                Assert.IsTrue(PlayerProfile.Preferences.MuteMusic);
                Assert.AreEqual(0.8f, source.volume, 1e-6f);
                Assert.IsTrue(source.mute);

                // A change made elsewhere reaches it on the next apply (an enabled player listens for PlayerProfile.Changed).
                PlayerProfile.Preferences.MusicVolume = 0.1f;
                player.Apply();
                Assert.AreEqual(0.1f, source.volume, 1e-6f);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ChainAudio_PlaysAtTheProfilesSfxVolume_AndIsSilentWhenMuted()
        {
            var preferences = new PlayerPreferences { SfxVolume = 0.5f, StockVolumesApplied = true };
            PlayerProfile.Store = new MemoryPreferencesStore(preferences);
            var go = new GameObject("Sfx");
            var clip = AudioClip.Create("hit", 4410, 1, 44100, false);
            try
            {
                var audio = go.AddComponent<ChainAudio>();
                audio.SetClips(ChainSound.Impact, new[] { clip });
                var volume = -1f;
                audio.Played += (s, c, v) => volume = v;

                audio.Play(ChainSound.Impact, 0.5f);
                Assert.AreEqual(0.25f, volume, 1e-6f);
                Assert.AreEqual(0.5f, audio.Volume);

                PlayerProfile.Preferences.MuteSfx = true;
                audio.Play(ChainSound.Impact);
                Assert.AreEqual(0f, volume);
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void TheStockVolumes_AreAudioJsons()
        {
            PlayerProfile.Store = new MemoryPreferencesStore();
            Assert.AreEqual(SettingsStore.Stock.AudioTuning.MusicVolume, PlayerProfile.Preferences.MusicVolume, 1e-6f);
            Assert.AreEqual(SettingsStore.Stock.AudioTuning.SfxVolume, PlayerProfile.Preferences.SfxVolume, 1e-6f);
        }
    }
}
