using System.Collections.Generic;
using System.Linq;
using MindAttic.Chain.Core;
using NUnit.Framework;
using StringAssert = NUnit.Framework.Legacy.StringAssert;

namespace MindAttic.Chain.Tests
{
    /// <summary>
    /// The player's preferences (UNI-T36): the profile's volumes, mutes and reduce motion, and Chain's resolution, window
    /// mode and tuning overrides. A fresh profile plays at the original's volumes (audio.json: full music, -10 dB sound
    /// effects), a profile's own figures win from then on, and tuning overrides lay over the stock data files and are
    /// validated like them.
    /// </summary>
    public class PlayerPreferencesTests
    {
        private static ISettingsSource Shipped => new DirectorySettingsSource(GameSettingsTests.ShippedFolder);

        private static AudioTuning StockAudio => GameSettings.Load(Shipped).AudioTuning;

        // --- Volumes ----------------------------------------------------------------------------

        [Test]
        public void AFreshProfile_PlaysAtTheOriginalsVolumes()
        {
            var preferences = PlayerPreferences.Load(new MemoryPreferencesStore(), StockAudio);

            Assert.AreEqual(1f, preferences.MusicVolume, 1e-6f, "Sound.vb's musicVolume 0 is full volume");
            Assert.AreEqual(0.3162f, preferences.SfxVolume, 1e-4f, "Sound.vb's sfxVolume -1000 is -10 dB");
            Assert.IsTrue(preferences.StockVolumesApplied);
            Assert.IsFalse(preferences.MuteMusic);
            Assert.IsFalse(preferences.MuteSfx);
            Assert.IsFalse(preferences.ReduceMotion);
        }

        [Test]
        public void TheStockVolumes_AreNotWritten_UntilThePlayerSaves()
        {
            var store = new MemoryPreferencesStore();
            PlayerPreferences.Load(store, StockAudio);
            Assert.AreEqual(0, store.Writes);
        }

        [Test]
        public void AProfilesOwnVolumes_OverrideTheStockOnes()
        {
            var store = new MemoryPreferencesStore();
            var preferences = PlayerPreferences.Load(store, StockAudio);
            preferences.MusicVolume = 0.25f;
            preferences.SfxVolume = 0.75f;
            preferences.MuteSfx = true;
            preferences.ReduceMotion = true;
            store.Write(preferences);

            var again = PlayerPreferences.Load(store, StockAudio);
            Assert.AreEqual(0.25f, again.MusicVolume);
            Assert.AreEqual(0.75f, again.SfxVolume);
            Assert.IsTrue(again.MuteSfx);
            Assert.IsTrue(again.ReduceMotion);
        }

        [Test]
        public void AProfileFromBeforeThisGame_TakesTheStockVolumes()
        {
            // Volumes some other tool wrote, never marked as this game's.
            var store = new MemoryPreferencesStore(new PlayerPreferences { MusicVolume = 0.6f, SfxVolume = 0.85f, MuteMusic = true });

            var preferences = PlayerPreferences.Load(store, StockAudio);
            Assert.AreEqual(1f, preferences.MusicVolume, 1e-6f);
            Assert.AreEqual(0.3162f, preferences.SfxVolume, 1e-4f);
            Assert.IsTrue(preferences.MuteMusic, "only the volumes take the stock figures");
        }

        [Test]
        public void Gains_AreZeroWhenMuted_AndVolumesClampToOne()
        {
            var preferences = new PlayerPreferences { MusicVolume = 3f, SfxVolume = -1f };
            Assert.AreEqual(1f, preferences.MusicVolume);
            Assert.AreEqual(0f, preferences.SfxVolume);

            preferences.SfxVolume = 0.5f;
            Assert.AreEqual(0.5f, preferences.SfxGain);
            preferences.MuteSfx = true;
            preferences.MuteMusic = true;
            Assert.AreEqual(0f, preferences.SfxGain);
            Assert.AreEqual(0f, preferences.MusicGain);
        }

        // --- Display ----------------------------------------------------------------------------

        [Test]
        public void WindowModes_RoundTripTheirSettingsNames()
        {
            foreach (WindowMode mode in System.Enum.GetValues(typeof(WindowMode)))
            {
                Assert.AreEqual(mode, PlayerPreferences.ParseWindowMode(PlayerPreferences.FormatWindowMode(mode)));
            }

            Assert.AreEqual(WindowMode.Borderless, PlayerPreferences.ParseWindowMode(" Borderless "));
            Assert.IsNull(PlayerPreferences.ParseWindowMode("theatre"));
            Assert.IsNull(PlayerPreferences.ParseWindowMode(null));
        }

        [Test]
        public void AResolution_IsKeptToASaneSize()
        {
            var tiny = new DisplayResolution(10, 99999);
            Assert.AreEqual(DisplayResolution.Min, tiny.Width);
            Assert.AreEqual(DisplayResolution.Max, tiny.Height);
            Assert.AreEqual("1920x1080", new DisplayResolution(1920, 1080).ToString());
        }

        [Test]
        public void Clone_SharesNothing()
        {
            var preferences = new PlayerPreferences { Resolution = new DisplayResolution(1280, 720), WindowMode = WindowMode.Windowed };
            preferences.TuningOverrides["chain.json"] = "{\"springK\": 0.3}";

            var copy = preferences.Clone();
            copy.TuningOverrides.Clear();
            copy.Resolution = null;

            Assert.AreEqual(1, preferences.TuningOverrides.Count);
            Assert.AreEqual(new DisplayResolution(1280, 720), preferences.Resolution);
            Assert.AreEqual(WindowMode.Windowed, copy.WindowMode);
        }

        // --- Tuning overrides -------------------------------------------------------------------

        private static GameSettings WithOverrides(params (string file, string json)[] overrides) =>
            GameSettings.Load(new TuningOverrideSource(Shipped, overrides.ToDictionary(o => o.file, o => o.json)));

        [Test]
        public void NoOverrides_ReadTheStockFiles()
        {
            var source = new TuningOverrideSource(Shipped, new Dictionary<string, string>());
            foreach (var file in GameSettings.Files)
            {
                Assert.AreEqual(Shipped.Read(file), source.Read(file), file);
            }
        }

        [Test]
        public void AnOverride_ChangesOnlyItsKeys_NestedSectionsMergedKeyByKey()
        {
            var stock = GameSettings.Load(Shipped);
            var tuned = WithOverrides(
                ("chain.json", "{\"springK\": 0.5, \"port\": {\"hitRadius\": 0.45}}"),
                ("audio.json", "{\"sfxVolume\": -2000}"));

            Assert.AreEqual(0.5f, tuned.Chain.SpringK);
            Assert.AreEqual(0.45f, tuned.Chain.Port.HitRadius);
            Assert.AreEqual(stock.Chain.Port.CatchRadius, tuned.Chain.Port.CatchRadius, "the rest of port is the stock one");
            Assert.AreEqual(stock.Chain.SegLength, tuned.Chain.SegLength);
            Assert.AreEqual(-2000, tuned.Audio.SfxVolume);
            Assert.AreEqual(stock.Angel.MaxHP, tuned.Angel.MaxHP);
        }

        [Test]
        public void ABadOverride_IsNamedLikeAnyOtherSettingsError()
        {
            var e = Assert.Throws<SettingsException>(() => WithOverrides(
                ("chain.json", "{\"springK\": -1, \"sprinK\": 2}"),
                ("angel.json", "{not json")));

            Assert.That(e.Errors, Has.Some.Contains("chain.json").And.Contains("springK"));
            Assert.That(e.Errors, Has.Some.Contains("chain.json").And.Contains("sprinK"));
            Assert.That(e.Errors, Has.Some.Contains("angel.json").And.Contains("not valid JSON"));
            StringAssert.Contains("tuning overrides", e.Message);
        }

        [Test]
        public void AnOverrideThatIsNotAnObject_IsRejected()
        {
            var e = Assert.Throws<SettingsException>(() => WithOverrides(("game.json", "[1, 2]")));
            Assert.That(e.Errors, Has.Some.Contains("game.json").And.Contains("one JSON object"));
        }

        // --- Json ------------------------------------------------------------------------------

        [Test]
        public void Write_ReadsBackToTheSameValue()
        {
            var text = "{\"a\": 1.25, \"b\": [true, false, null], \"c\": {\"d\": \"q\\\"uote\\n\", \"e\": []}, \"f\": {}, \"g\": -1000}";
            var once = Json.Write(Json.Parse(text));
            Assert.AreEqual(once, Json.Write(Json.Parse(once)));
            StringAssert.Contains("\"g\": -1000", once);
            StringAssert.Contains("\"a\": 1.25", once);
        }

        [Test]
        public void Merge_ReplacesNonObjects_AndAddsNewKeysAtTheEnd()
        {
            var merged = Json.Merge(Json.Parse("{\"a\": 1, \"b\": {\"x\": 1, \"y\": 2}}"), Json.Parse("{\"b\": {\"y\": 3}, \"c\": 4}"));
            Assert.AreEqual(new[] { "a", "b", "c" }, merged.Members.Select(m => m.Key).ToArray());
            Assert.AreEqual(1d, merged.Get("b").Get("x").Number);
            Assert.AreEqual(3d, merged.Get("b").Get("y").Number);
            Assert.AreEqual(JsonKind.Array, Json.Merge(Json.Parse("{\"a\": 1}"), Json.Parse("[1]")).Kind);
        }
    }
}
