namespace MindAttic.Chain.Core
{
    /// <summary>
    /// Engine.vb renderDisplay's <c>Res</c>/<c>Grav</c>/<c>Spring</c> readout: a chain's live resistance, gravity
    /// (its y) and spring constant, in the original's own units (per frame, screen units), so the HUD shows the
    /// figures chain.json holds (0.25, -0.0025, 0.1) and the live-tuning keys (UNI-T51) move.
    /// </summary>
    public readonly struct ChainReadout
    {
        public ChainReadout(float resistance, float gravity, float spring)
        {
            Resistance = resistance;
            Gravity = gravity;
            Spring = spring;
        }

        /// <summary>Chain.vb's <c>resistance</c>, per frame.</summary>
        public float Resistance { get; }

        /// <summary>Chain.vb's <c>gravity.Y</c>, screen units per frame squared.</summary>
        public float Gravity { get; }

        /// <summary>Chain.vb's <c>springK</c>, per frame squared.</summary>
        public float Spring { get; }

        /// <summary>A chain's settings (world units and seconds) mapped back through <paramref name="space"/>.</summary>
        public static ChainReadout From(ChainSimulation.Settings chain, OriginalSpace space) => new ChainReadout(
            space.ToPerFrame(chain.Resistance),
            space.ToOriginalAcceleration(chain.Gravity.Y),
            space.ToPerFrameSquared(chain.SpringConstant));
    }
}
