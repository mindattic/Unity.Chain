using System.Collections.Generic;

namespace MindAttic.Chain.Core
{
    /// <summary>
    /// Puts a script's <see cref="ScriptEffect"/>s into the game's presentation state (U.CHN-T61): the camera moves or is
    /// placed on <see cref="DebugCamera"/> (in the original's screen units, mapped to world units by space.json's
    /// <see cref="OriginalSpace"/>), and the ambient colour, the tint channels and the day period go into the
    /// <see cref="LightingState"/>. The script colour and <c>bScriptRunning</c> are the runner's own (the console reads
    /// them from it), so they need nothing here. Either target may be null, and its effects are then dropped.
    /// </summary>
    public static class ScriptEffects
    {
        /// <summary>Applies every effect in order. Returns how many changed something.</summary>
        public static int Apply(IEnumerable<ScriptEffect> effects, LightingState lighting, DebugCamera camera, OriginalSpace space)
        {
            var applied = 0;
            if (effects == null)
            {
                return applied;
            }

            foreach (var effect in effects)
            {
                if (Apply(effect, lighting, camera, space))
                {
                    applied++;
                }
            }

            return applied;
        }

        /// <summary>Applies one effect. Returns false if it has no target here.</summary>
        public static bool Apply(ScriptEffect effect, LightingState lighting, DebugCamera camera, OriginalSpace space)
        {
            switch (effect)
            {
                case CameraMoved moved when camera != null:
                    var delta = space.Length(moved.Delta);
                    camera.Pan(moved.Axis == ScriptAxis.X ? new Vec2(delta, 0f) : new Vec2(0f, delta));
                    return true;

                case CameraPlaced placed when camera != null:
                    camera.Place(placed.Axis, space.Length(placed.Value));
                    return true;

                case AmbientChanged ambient when lighting != null:
                    lighting.SetAmbient(ambient.Colour);
                    return true;

                case TintChanged tint when lighting != null:
                    lighting.SetTint(tint.Channel, tint.Value);
                    return true;

                case DayPeriodChanged period when lighting != null:
                    lighting.SetPeriod(period.Period);
                    return true;

                default:
                    return false;
            }
        }
    }
}
