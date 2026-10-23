using System;

namespace MindAttic.Chain.Core
{
    /// <summary>
    /// The scalar helpers the rules use, with the semantics of <c>UnityEngine.Mathf</c> they replace
    /// (notably <see cref="Sign"/> is +1 at zero and <see cref="Epsilon"/> is the smallest positive float).
    /// </summary>
    public static class Calc
    {
        public const float Epsilon = float.Epsilon;
        public const float PI = MathF.PI;

        public static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;
        public static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
        public static float Clamp01(float value) => Clamp(value, 0f, 1f);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float Max(float a, float b) => a > b ? a : b;
        public static float Min(float a, float b) => a < b ? a : b;
        public static int Max(int a, int b) => a > b ? a : b;
        public static int Min(int a, int b) => a < b ? a : b;
        public static float Abs(float value) => MathF.Abs(value);
        public static float Sin(float radians) => MathF.Sin(radians);
        public static float Exp(float power) => MathF.Exp(power);

        /// <summary>+1 for zero and positive, -1 for negative.</summary>
        public static float Sign(float value) => value >= 0f ? 1f : -1f;

        /// <summary>Rounds to the nearest whole number, halves to even.</summary>
        public static int RoundToInt(float value) => (int)MathF.Round(value);

        public static float MoveTowards(float current, float target, float maxDelta) =>
            MathF.Abs(target - current) <= maxDelta ? target : current + MathF.Sign(target - current) * maxDelta;
    }
}
