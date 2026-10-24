using System;
using System.Collections.Generic;
using System.Globalization;

namespace MindAttic.Chain.Core
{
    /// <summary>
    /// Reads one JSON object of a data file and validates it as it goes. Every key must be present,
    /// of the right type and in range; a key nobody asked for is an error too (a typo would otherwise
    /// be silently ignored). Keys that start with an underscore are notes for people ("_source") and
    /// are skipped. Problems are collected, not thrown, so one load reports all of them, each as
    /// "file: key ... (line n)". A value that fails gives back the fallback so reading can go on.
    /// </summary>
    public sealed class SettingsReader
    {
        private readonly JsonValue _object;
        private readonly string _path;
        private readonly List<string> _errors;
        private readonly HashSet<string> _read = new HashSet<string>();

        internal SettingsReader(JsonValue value, string path, List<string> errors)
        {
            _object = value;
            _path = path;
            _errors = errors;
        }

        /// <summary>The nested object <paramref name="key"/> ("port").</summary>
        public SettingsReader Section(string key)
        {
            var value = Take(key, JsonKind.Object, "an object");
            return new SettingsReader(value, $"{_path}.{key}", _errors);
        }

        /// <summary>A finite number in [<paramref name="min"/>, <paramref name="max"/>].</summary>
        public float Number(string key, float min = float.MinValue, float max = float.MaxValue)
        {
            var value = Take(key, JsonKind.Number, "a number");
            if (value == null)
            {
                return 0f;
            }

            var number = value.Number;
            if (double.IsNaN(number) || double.IsInfinity(number) || Math.Abs(number) > float.MaxValue)
            {
                Fail(key, value, $"must be a finite number (is {Show(number)})");
                return 0f;
            }

            if (number < min || number > max)
            {
                Fail(key, value, $"must be {Range(min, max)} (is {Show(number)})");
                return Calc.Clamp((float)number, min, max);
            }

            return (float)number;
        }

        /// <summary>A number strictly greater than <paramref name="above"/>.</summary>
        public float Positive(string key, float above = 0f)
        {
            var value = _object?.Get(key);
            var number = Number(key);
            if (value != null && value.Kind == JsonKind.Number && !(number > above))
            {
                Fail(key, value, $"must be greater than {Show(above)} (is {Show(number)})");
            }

            return number;
        }

        /// <summary>A whole number in [<paramref name="min"/>, <paramref name="max"/>].</summary>
        public int Integer(string key, int min = int.MinValue, int max = int.MaxValue)
        {
            var value = Take(key, JsonKind.Number, "a whole number");
            if (value == null)
            {
                return min > 0 ? min : 0;
            }

            var number = value.Number;
            if (Math.Floor(number) != number || number < int.MinValue || number > int.MaxValue)
            {
                Fail(key, value, $"must be a whole number (is {Show(number)})");
                return min > 0 ? min : 0;
            }

            if (number < min || number > max)
            {
                Fail(key, value, $"must be {Range(min, max)} (is {Show(number)})");
                return Calc.Clamp((int)number, min, max);
            }

            return (int)number;
        }

        public bool Flag(string key)
        {
            var value = Take(key, JsonKind.Bool, "true or false");
            return value != null && value.Bool;
        }

        /// <summary>A two-number array [x, y], each in [<paramref name="min"/>, <paramref name="max"/>].</summary>
        public Vec2 Pair(string key, float min = float.MinValue, float max = float.MaxValue)
        {
            var value = Take(key, JsonKind.Array, "an [x, y] pair");
            if (value == null)
            {
                return Vec2.Zero;
            }

            if (value.Items.Count != 2 || value.Items[0].Kind != JsonKind.Number || value.Items[1].Kind != JsonKind.Number)
            {
                Fail(key, value, "must be an [x, y] pair of two numbers");
                return Vec2.Zero;
            }

            var x = value.Items[0].Number;
            var y = value.Items[1].Number;
            if (x < min || x > max || y < min || y > max)
            {
                Fail(key, value, $"must have both numbers {Range(min, max)} (is [{Show(x)}, {Show(y)}])");
            }

            return new Vec2((float)x, (float)y);
        }

        /// <summary>One of <typeparamref name="T"/>'s names, written as in the code ("FitLevel").</summary>
        public T Choice<T>(string key) where T : struct
        {
            var value = Take(key, JsonKind.String, "a name");
            if (value == null)
            {
                return default;
            }

            if (Enum.TryParse<T>(value.Text, false, out var parsed) && Enum.IsDefined(typeof(T), parsed))
            {
                return parsed;
            }

            Fail(key, value, $"must be one of {string.Join(", ", Enum.GetNames(typeof(T)))} (is \"{value.Text}\")");
            return default;
        }

        /// <summary>A cross-field check: records <paramref name="message"/> against <paramref name="key"/> when <paramref name="ok"/> is false.</summary>
        public void Check(bool ok, string key, string message)
        {
            if (!ok)
            {
                Fail(key, _object?.Get(key), message);
            }
        }

        /// <summary>Reports every key of this object that nothing read. Call once its keys have all been read.</summary>
        public void Done()
        {
            if (_object == null)
            {
                return;
            }

            foreach (var member in _object.Members)
            {
                if (!_read.Contains(member.Key) && !member.Key.StartsWith("_", StringComparison.Ordinal))
                {
                    _errors.Add($"{_path}: unknown key '{member.Key}' (line {member.Value.Line}); check its spelling");
                }
            }
        }

        private JsonValue Take(string key, JsonKind kind, string what)
        {
            _read.Add(key);
            if (_object == null)
            {
                return null; // the enclosing object is missing, already reported
            }

            var value = _object.Get(key);
            if (value == null)
            {
                _errors.Add($"{_path}: '{key}' is missing (expected {what})");
                return null;
            }

            if (value.Kind != kind)
            {
                Fail(key, value, $"must be {what} (is {Describe(value)})");
                return null;
            }

            return value;
        }

        private void Fail(string key, JsonValue value, string message)
        {
            var where = value != null ? $" (line {value.Line})" : string.Empty;
            _errors.Add($"{_path}: '{key}' {message}{where}");
        }

        private static string Describe(JsonValue value)
        {
            switch (value.Kind)
            {
                case JsonKind.Null: return "null";
                case JsonKind.Bool: return value.Bool ? "true" : "false";
                case JsonKind.Number: return Show(value.Number);
                case JsonKind.String: return $"\"{value.Text}\"";
                case JsonKind.Array: return "an array";
                default: return "an object";
            }
        }

        private static string Range(double min, double max)
        {
            var hasMin = min > float.MinValue && min > int.MinValue;
            var hasMax = max < float.MaxValue && max < int.MaxValue;
            if (hasMin && hasMax)
            {
                return $"between {Show(min)} and {Show(max)}";
            }

            return hasMin ? $"at least {Show(min)}" : $"at most {Show(max)}";
        }

        private static string Show(double number) => number.ToString("G6", CultureInfo.InvariantCulture);
    }
}
