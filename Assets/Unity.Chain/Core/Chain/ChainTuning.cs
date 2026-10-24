namespace MindAttic.Chain.Core
{
    /// <summary>The three chain figures Form1.vb's live-tuning keys change.</summary>
    public enum ChainKnob
    {
        /// <summary>Chain.vb's <c>resistance</c> (Insert +0.001, Delete -0.001: <c>changeResistance</c>).</summary>
        Resistance,

        /// <summary>Chain.vb's <c>gravity.Y</c> (Home -0.00001, End +0.00001: <c>changeGravity</c>).</summary>
        Gravity,

        /// <summary>Chain.vb's <c>springK</c> (PageUp +0.001, PageDown -0.001: <c>changeSpring</c>).</summary>
        Spring,
    }

    /// <summary>
    /// Form1.vb's live-tuning keys as a rule: each press moves one of a chain's <see cref="ChainKnob"/>s by its
    /// step (Insert/Delete resistance +/-0.001, Home/End gravity.Y -/+0.00001, PageUp/PageDown spring +/-0.001,
    /// chain.json's <c>tuning</c>), held between that knob's limits. The original had no limits (Chain.vb's
    /// <c>changeResistance</c>, <c>changeGravity</c> and <c>changeSpring</c> just add), so a spring or a
    /// resistance pushed below zero, or a stiff enough spring, blew the chain apart; the port stops at
    /// chain.json's limits (BIBLE V19). Figures are the port's (world units and seconds), mapped from the
    /// file's by <see cref="GameSettings"/>.
    /// </summary>
    public readonly struct ChainTuning
    {
        public ChainTuning(
            float resistanceStep, float resistanceMin, float resistanceMax,
            float gravityStep, float gravityMin, float gravityMax,
            float springStep, float springMin, float springMax)
        {
            ResistanceStep = resistanceStep;
            ResistanceMin = resistanceMin;
            ResistanceMax = resistanceMax;
            GravityStep = gravityStep;
            GravityMin = gravityMin;
            GravityMax = gravityMax;
            SpringStep = springStep;
            SpringMin = springMin;
            SpringMax = springMax;
        }

        public float ResistanceStep { get; }
        public float ResistanceMin { get; }
        public float ResistanceMax { get; }

        /// <summary>One step of gravity's y. A positive step (End) weakens the pull down; Home's is -1 step.</summary>
        public float GravityStep { get; }
        public float GravityMin { get; }
        public float GravityMax { get; }

        public float SpringStep { get; }
        public float SpringMin { get; }
        public float SpringMax { get; }

        /// <summary>chain.json's shipped tuning, mapped at the shipped scale (9) and 60 fps: for setups built without the data files.</summary>
        public static ChainTuning Default => From(
            new OriginalSpace(OriginalSpace.ArenaLengthScale, 60f),
            resistanceStep: 0.001f, resistanceMin: 0f, resistanceMax: 1f,
            gravityStep: 0.00001f, gravityMin: -0.01f, gravityMax: 0.0025f,
            springStep: 0.001f, springMin: 0.001f, springMax: 0.25f);

        /// <summary>The figures in the original's own units (per frame, screen units), mapped through <paramref name="space"/>.</summary>
        public static ChainTuning From(
            OriginalSpace space,
            float resistanceStep, float resistanceMin, float resistanceMax,
            float gravityStep, float gravityMin, float gravityMax,
            float springStep, float springMin, float springMax) => new ChainTuning(
            space.PerSecond(resistanceStep), space.PerSecond(resistanceMin), space.PerSecond(resistanceMax),
            space.Acceleration(gravityStep), space.Acceleration(gravityMin), space.Acceleration(gravityMax),
            space.PerSecondSquared(springStep), space.PerSecondSquared(springMin), space.PerSecondSquared(springMax));

        /// <summary>
        /// <paramref name="chain"/> with <paramref name="knob"/> moved by <paramref name="steps"/> of its step and
        /// held to its limits. Gravity moves only its y, as <c>changeGravity</c> does. A knob with no step (an
        /// unset tuning) never moves.
        /// </summary>
        public ChainSimulation.Settings Apply(ChainSimulation.Settings chain, ChainKnob knob, int steps)
        {
            if (steps == 0 || Step(knob) == 0f)
            {
                return chain;
            }

            var resistance = chain.Resistance;
            var gravity = chain.Gravity;
            var spring = chain.SpringConstant;
            switch (knob)
            {
                case ChainKnob.Resistance:
                    resistance = Move(resistance, steps * ResistanceStep, ResistanceMin, ResistanceMax);
                    break;
                case ChainKnob.Gravity:
                    gravity = gravity.WithY(Move(gravity.Y, steps * GravityStep, GravityMin, GravityMax));
                    break;
                case ChainKnob.Spring:
                    spring = Move(spring, steps * SpringStep, SpringMin, SpringMax);
                    break;
            }

            return chain.WithLiveTuning(resistance, gravity, spring);
        }

        /// <summary>
        /// <paramref name="value"/> moved by <paramref name="delta"/>, stopping at the limit it moves toward. A value
        /// already past that limit (a player's tuning override, say) stays where it is rather than jumping to it.
        /// </summary>
        private static float Move(float value, float delta, float min, float max)
        {
            if (delta > 0f)
            {
                return value >= max ? value : Calc.Min(value + delta, max);
            }

            return value <= min ? value : Calc.Max(value + delta, min);
        }

        /// <summary>One press's change to <paramref name="knob"/>.</summary>
        public float Step(ChainKnob knob)
        {
            switch (knob)
            {
                case ChainKnob.Resistance: return ResistanceStep;
                case ChainKnob.Gravity: return GravityStep;
                default: return SpringStep;
            }
        }

        /// <summary>The live value of <paramref name="knob"/> in <paramref name="chain"/>.</summary>
        public static float Read(ChainSimulation.Settings chain, ChainKnob knob)
        {
            switch (knob)
            {
                case ChainKnob.Resistance: return chain.Resistance;
                case ChainKnob.Gravity: return chain.Gravity.Y;
                default: return chain.SpringConstant;
            }
        }
    }
}
