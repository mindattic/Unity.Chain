using System;
using System.Collections.Generic;
using MindAttic.Chain.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MindAttic.Chain.Tests
{
    /// <summary>
    /// A seated player for component tests: a <see cref="GameSession"/> (the authority), an angel and a
    /// chain (with a mace and a grapple unless asked not to), joined as player 0 in a level big enough
    /// that no wall gets in the way. Edit mode runs no Awake or FixedUpdate, so tests drive the
    /// session's step themselves.
    /// </summary>
    internal sealed class TestRig : IDisposable
    {
        public const float Dt = 1f / 50f;

        private readonly List<Object> _objects = new List<Object>();

        public readonly GameSession Session;
        public readonly AngelController Angel;
        public readonly ChainController Chain;
        public readonly MaceController Mace;
        public readonly GrappleController Grapple;
        public readonly LevelBounds Level;
        public readonly PlayerEntity Player;

        public TestRig(bool mace = true, bool grapple = true, Vector2? levelSize = null, Action<TestRig> beforeJoin = null)
        {
            Level = Track(new GameObject("Level")).AddComponent<LevelBounds>();
            Level.Size = levelSize ?? new Vector2(200f, 200f);

            Angel = Track(new GameObject("Angel")).AddComponent<AngelController>();
            Angel.LevelBounds = Level;

            var chainGo = Track(new GameObject("Chain"));
            Chain = chainGo.AddComponent<ChainController>();
            if (mace)
            {
                Mace = chainGo.AddComponent<MaceController>();
            }

            if (grapple)
            {
                Grapple = chainGo.AddComponent<GrappleController>();
                Grapple.UseMouseButton = false;
            }

            Angel.Chain = Chain;

            Session = Track(new GameObject("Session")).AddComponent<GameSession>();
            Session.Angel = Angel;
            Session.Level = Level;
            beforeJoin?.Invoke(this);
            Player = Session.Join(Angel);
        }

        public PlayerId Id => Player.Id;

        public T Track<T>(T o) where T : Object
        {
            _objects.Add(o);
            return o;
        }

        /// <summary>Flies the angel straight to <paramref name="position"/> (the world clamps it to the level).</summary>
        public void MoveAngelTo(Vector2 position)
        {
            var delta = (position - Player.Flight.Position.ToUnity()) / Player.Setup.Flight.Sensitivity;
            Angel.SendInput(delta, 0f);
        }

        /// <summary>One authority step, then the chain view remembers it (as FixedUpdate would).</summary>
        public void Step(float deltaTime = Dt)
        {
            Session.Step(deltaTime);
            Chain.CaptureStep();
        }

        /// <summary>Drags the angel sideways <paramref name="stride"/> a step from x = 0, so the mace picks up speed.</summary>
        public void Swing(int steps = 10, float stride = 0.3f)
        {
            for (var i = 0; i < steps; i++)
            {
                MoveAngelTo(new Vector2(i * stride, 0f));
                Step();
            }
        }

        /// <summary>Puts an enemy in the world (as the authority's own setup, not a player command).</summary>
        public EnemyEntity SpawnEnemy(Vector2 position, float hp = 100f, float mass = 1f) =>
            Session.World.SpawnEnemy(position.ToCore(), mass, hp);

        public void Dispose()
        {
            foreach (var o in _objects)
            {
                if (o != null)
                {
                    Object.DestroyImmediate(o);
                }
            }

            _objects.Clear();
        }
    }
}
