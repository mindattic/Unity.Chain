// U.CHN-T26: online play on the Unity.Multiplayer kit's test fixtures (Unity.Multiplayer.Testing). Every test gets a
// real host or dedicated server and real clients over Unity Transport on a free loopback port. Each peer has its own
// GameSession (ChainAuthority.Resolve), as each would in its own process: the server's is the one authority, a client's
// is its screen, whose commands are forwarded instead of changing its own world. The wire formats themselves are tested
// without Unity: dotnet test Tests/Core.Tests (NetWireTests).
using System.Collections;
using MindAttic.Chain.Core;
using NUnit.Framework;
using Unity.Multiplayer;
using Unity.Multiplayer.Core;
using Unity.Multiplayer.Testing;
using UnityEngine;
using UnityEngine.TestTools;

namespace MindAttic.Chain.Online.Tests
{
    internal static class OnlinePrefabs
    {
        // The Resources paths OnlineBuilder writes the prefabs to (Editor/OnlineBuilder.cs).
        public static GameObject Player => Load("Online/ChainPlayer");
        public static GameObject WorldSync => Load("Online/ChainWorldSync");

        private static GameObject Load(string path)
        {
            var prefab = Resources.Load<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, "Resources/" + path + ".prefab is missing: run MindAttic > Chain > Build Main Scene");
            return prefab;
        }

        /// <summary>A peer's GameSession with no flock, so nothing but the test's commands moves an angel.</summary>
        public static GameSession NewGame(string name)
        {
            var game = new GameObject(name).AddComponent<GameSession>();
            game.World.Config.Enemies.Count = 0;
            return game;
        }
    }

    /// <summary>A host and one client: the client's commands reach the host's world as its own seat, and come back.</summary>
    public class ChainPlayerTests : NetSessionTest
    {
        private GameSession _hostGame;
        private GameSession _clientGame;

        public ChainPlayerTests() : base(Topology.Host, clients: 1) { }

        // No seat held for a reconnect, so a player who leaves is gone at once (the leave test).
        protected override NetConfig Config()
        {
            var config = base.Config();
            config.ReconnectGraceSeconds = 0;
            return config;
        }

        protected override void OnSessionsCreated()
        {
            SetPlayerPrefab(OnlinePrefabs.Player);
            RegisterPrefab(OnlinePrefabs.WorldSync);
            Client.Identity = new PlayerIdentity("ann-token", "Ann", new PlayerColor(230, 80, 60));
            _hostGame = OnlinePrefabs.NewGame("Host game");
            _clientGame = OnlinePrefabs.NewGame("Client game");
            ChainAuthority.Resolve = session => session == Server ? _hostGame : session == Client ? _clientGame : null;
        }

        protected override IEnumerator OnSessionsTearDown()
        {
            ChainAuthority.Reset();
            Object.Destroy(_hostGame.gameObject);
            Object.Destroy(_clientGame.gameObject);
            yield break;
        }

        private IEnumerator Seated(System.Action<ChainPlayer, ChainPlayer> found)
        {
            yield return StartAndJoinAll();
            var annOnHerClient = WaitForSpawn<ChainPlayer>(Client, owner: Client.LocalClientId);
            yield return annOnHerClient;
            var annOnHost = WaitForSpawn<ChainPlayer>(Server, owner: Client.LocalClientId);
            yield return annOnHost;
            yield return WaitUntil(() => annOnHerClient.Result.IsSeated, "Ann's client never learnt her seat");
            found(annOnHerClient.Result, annOnHost.Result);
        }

        [UnityTest]
        public IEnumerator EachConnection_GetsAPlayerCarryingItsSeat_AndTheAuthoritySeatsIt()
        {
            ChainPlayer ann = null, annOnHost = null;
            yield return Seated((c, h) => { ann = c; annOnHost = h; });

            var hostOwn = WaitForSpawn<ChainPlayer>(Server, owner: Server.LocalClientId);
            yield return hostOwn;

            Assert.That(ann.Seat, Is.EqualTo(annOnHost.Seat), "every peer reads the server's seat");
            Assert.That(ann.Seat, Is.Not.EqualTo(hostOwn.Result.Seat), "two connections, two seats");
            Assert.That(_hostGame.World.FindPlayer(ann.PlayerId), Is.Not.Null, "the authority seated Ann in its world");
            Assert.That(_hostGame.World.FindPlayer(hostOwn.Result.PlayerId), Is.Not.Null, "and the host's own seat");
            Assert.That(_hostGame.IsRemote, Is.False, "the host is the authority: its commands apply to its own world");
            Assert.That(_clientGame.IsRemote, Is.True, "Ann's screen forwards its commands");
            Assert.That(_clientGame.World.Players.Count, Is.EqualTo(0), "and its own world seats nobody");
            yield return WaitUntil(() => annOnHost.DisplayName == "Ann", () => "the host sees her as " + annOnHost.DisplayName);
        }

        [UnityTest]
        public IEnumerator TheClientsCommands_GoToTheAuthority_AsItsOwnSeat_AndTheAngelComesBackSynced()
        {
            ChainPlayer ann = null, annOnHost = null;
            yield return Seated((c, h) => { ann = c; annOnHost = h; });
            var hostSeat = new PlayerId(Server.Roster.Local.Seat);

            var hostAngel = _hostGame.World.FindPlayer(hostSeat).AngelPosition;
            var start = _hostGame.World.FindPlayer(ann.PlayerId).AngelPosition;
            var clientTimeBefore = _clientGame.World.Time;

            // What Ann's mouse does: her screen's input readers submit, naming whatever id; it is forwarded, not applied.
            for (var i = 0; i < 10; i++)
            {
                Assert.That(_clientGame.Submit(new SteerAngel(hostSeat, new Vec2(60f, 0f), 0.02f)), Is.True, "sent");
                yield return null;
            }

            yield return WaitUntil(() => _hostGame.World.FindPlayer(ann.PlayerId).AngelPosition.X > start.X + 0.2f,
                () => "the authority never flew Ann's angel; it is at " + _hostGame.World.FindPlayer(ann.PlayerId).AngelPosition);
            Assert.That(_hostGame.World.FindPlayer(hostSeat).AngelPosition.X, Is.EqualTo(hostAngel.X).Within(1e-4f),
                "the host's angel, whose id Ann's commands named, did not move");
            Assert.That(annOnHost.CommandsApplied, Is.GreaterThanOrEqualTo(10));

            yield return WaitUntil(() => ann.Angel.x > start.X + 0.2f, () => "Ann's client never saw her angel move; it is at " + ann.Angel);
            Assert.That(_clientGame.World.Time, Is.EqualTo(clientTimeBefore), "her screen's own world is not stepped while it is online");

            // Quit never leaves her screen: Escape is the kit's in-game menu online, or the application's in local play.
            Assert.That(CommandCodec.IsSent(new QuitGame(ann.PlayerId)), Is.False);
        }

        [UnityTest]
        public IEnumerator BytesThatAreNoCommand_AreRefusedByTheAuthority()
        {
            ChainPlayer ann = null, annOnHost = null;
            yield return Seated((c, h) => { ann = c; annOnHost = h; });
            var start = _hostGame.World.FindPlayer(ann.PlayerId).AngelPosition;

            ann.Send(new byte[] { 99, 1, 2, 3 });
            ann.Send(new byte[0]);
            yield return WaitUntil(() => annOnHost.CommandsRefused == 2, () => "refused " + annOnHost.CommandsRefused);
            Assert.That(annOnHost.CommandsApplied, Is.EqualTo(0));
            Assert.That(_hostGame.World.FindPlayer(ann.PlayerId).AngelPosition, Is.EqualTo(start));
        }

        [UnityTest]
        public IEnumerator TheAuthoritysWorld_ReachesTheClient_AsSnapshots()
        {
            ChainPlayer ann = null, annOnHost = null;
            yield return Seated((c, h) => { ann = c; annOnHost = h; });

            var onHost = ChainWorldSync.Serve(Server, OnlinePrefabs.WorldSync);
            Assert.That(ChainWorldSync.For(Server), Is.SameAs(onHost), "found by its key on the server");
            Assert.That(ChainWorldSync.Serve(Server, OnlinePrefabs.WorldSync), Is.SameAs(onHost), "one per session");

            ChainWorldSync onClient = null;
            yield return WaitUntil(() => (onClient = ChainWorldSync.For(Client)) != null, "the client never got the world sync by its key");
            yield return WaitUntil(() => onClient.Latest != null && onClient.Latest.Players.Count == 2,
                () => "the client's snapshot has " + (onClient.Latest == null ? "nothing" : onClient.Latest.Players.Count + " players"));

            var snapshot = onClient.Latest;
            Assert.That(snapshot.Hp, Is.EqualTo(_hostGame.State.Hp));
            Assert.That(snapshot.Score, Is.EqualTo(_hostGame.State.Score));
            Assert.That(snapshot.TryGetPlayer(ann.PlayerId, out var annInSnapshot), Is.True, "Ann's seat is in the snapshot");
            Assert.That(annInSnapshot.Links.Length, Is.EqualTo(_hostGame.World.FindPlayer(ann.PlayerId).MaceIndex + 1));

            // A pause on the authority reaches the client in the next snapshots, though the host's game time stops.
            _hostGame.Submit(new Pause(ann.PlayerId, true));
            yield return WaitUntil(() => onClient.Latest.IsPaused, "the client never saw the authority's pause");
            _hostGame.Submit(new Pause(ann.PlayerId, false));
            yield return WaitUntil(() => !onClient.Latest.IsPaused, "the client never saw the authority resume");
        }

        [UnityTest]
        public IEnumerator APlayerWhoLeaves_IsTakenOutOfTheAuthoritysWorld_AndTheirScreenPlaysLocallyAgain()
        {
            ChainPlayer ann = null, annOnHost = null;
            yield return Seated((c, h) => { ann = c; annOnHost = h; });
            var seat = ann.PlayerId;

            Client.Leave();
            yield return WaitUntil(() => _hostGame.World.FindPlayer(seat) == null, "Ann's seat stayed in the authority's world");
            yield return WaitUntil(() => !_clientGame.IsRemote, "Ann's screen still forwards its commands");
        }
    }

    /// <summary>The nightly smoke without Docker: a dedicated server and two ChainBots that join by code and fly.</summary>
    public class ChainBotTests : NetBotTest
    {
        private GameSession _serverGame;

        public ChainBotTests() : base(bots: 2) { }

        protected override float BotTimeoutSeconds => 60f;

        protected override void OnSessionsCreated()
        {
            SetPlayerPrefab(OnlinePrefabs.Player);
            RegisterPrefab(OnlinePrefabs.WorldSync);
            _serverGame = OnlinePrefabs.NewGame("Server game");
            // Only the server has a game: a bot has no screen, and plays through its ChainPlayer's SendCommand.
            ChainAuthority.Resolve = session => session == Server ? _serverGame : null;
        }

        protected override IEnumerator OnSessionsTearDown()
        {
            ChainAuthority.Reset();
            Object.Destroy(_serverGame.gameObject);
            yield return base.OnSessionsTearDown();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator TwoBotsJoinByCode_AndTheAuthorityFliesEachOnesAngel()
        {
            yield return RunBots(role => new ChainBot(), "A", "B");

            Assert.That(_serverGame.World.Players.Count, Is.EqualTo(2), "a dedicated server seats only its two bots");
            foreach (var bot in Bots)
            {
                Assert.That(bot.PassMessage, Does.Contain("flew"));
            }
        }
    }
}
