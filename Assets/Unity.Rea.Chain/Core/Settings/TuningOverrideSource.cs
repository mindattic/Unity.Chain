using System;
using System.Collections.Generic;

namespace MindAttic.Chain.Core
{
    /// <summary>
    /// The stock data files with a player profile's tuning overrides laid over them
    /// (<see cref="PlayerPreferences.TuningOverrides"/>: file name to a JSON object of the keys it changes, nested
    /// sections such as <c>"port"</c> merged key by key, see <see cref="Json.Merge"/>). A file without an override
    /// reads as the stock one, so <see cref="GameSettings.Load"/> validates the merged result exactly as it does the
    /// stock files: an override with an unknown key or a value out of range is named like any other settings error.
    /// </summary>
    public sealed class TuningOverrideSource : ISettingsSource
    {
        private readonly ISettingsSource _stock;
        private readonly IReadOnlyDictionary<string, string> _overrides;

        public TuningOverrideSource(ISettingsSource stock, IReadOnlyDictionary<string, string> overrides)
        {
            _stock = stock ?? throw new ArgumentNullException(nameof(stock));
            _overrides = overrides ?? new Dictionary<string, string>();
        }

        public string Describe => $"{_stock.Describe} with the profile's tuning overrides";

        public string Read(string name)
        {
            var stock = _stock.Read(name);
            if (stock == null || !_overrides.TryGetValue(name, out var patch) || string.IsNullOrWhiteSpace(patch))
            {
                return stock;
            }

            JsonValue overlay;
            try
            {
                overlay = Json.Parse(patch);
            }
            catch (JsonException e)
            {
                throw new SettingsException(new[] { $"{name}: the profile's tuning override is not valid JSON ({e.Message})" }, Describe);
            }

            if (overlay.Kind != JsonKind.Object)
            {
                throw new SettingsException(new[] { $"{name}: the profile's tuning override must be one JSON object {{ ... }}" }, Describe);
            }

            JsonValue root;
            try
            {
                root = Json.Parse(stock);
            }
            catch (JsonException)
            {
                return stock; // the stock file's own error is reported when it is read
            }

            return Json.Write(Json.Merge(root, overlay));
        }
    }
}
