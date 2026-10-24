using System;
using Unity.Multiplayer;
using UnityEngine;

namespace MindAttic.Chain.Online
{
    /// <summary>
    /// Which <see cref="GameSession"/> a network peer's game is (U.CHN-T26). On the server it is the one authority the
    /// online players' commands go to; on a client it is that screen's session, whose commands are forwarded to the
    /// server. A game runs one peer per process, so the default is the scene's session; a test that runs a server and
    /// clients in one process sets <see cref="Resolve"/> to give each peer its own (and <see cref="Reset"/> afterwards).
    /// </summary>
    public static class ChainAuthority
    {
        /// <summary>The game session of a peer's network session (null when it has none).</summary>
        public static Func<NetSession, GameSession> Resolve { get; set; } = SceneSession;

        /// <summary>Back to the scene's session.</summary>
        public static void Reset() => Resolve = SceneSession;

        /// <summary>The game session of <paramref name="session"/>'s peer.</summary>
        public static GameSession For(NetSession session) => Resolve?.Invoke(session);

        private static GameSession SceneSession(NetSession session) => UnityEngine.Object.FindAnyObjectByType<GameSession>();
    }
}
