using System;
using System.Collections.Generic;
using MindAttic.Chain.Core;
using NUnit.Framework;
using CollectionAssert = NUnit.Framework.Legacy.CollectionAssert;

namespace MindAttic.Chain.Tests
{
    /// <summary>
    /// U.CHN-T26: what an online peer sends the one authority (<see cref="CommandCodec"/>) and what the authority sends
    /// back for presentation to read (<see cref="WorldSnapshot"/>), plus the online menus' half of the input gate.
    /// </summary>
    public class NetWireTests
    {
        private static readonly PlayerId Sender = new PlayerId(3);

        private static IEnumerable<GameCommand> EveryCommand()
        {
            var spoof = new PlayerId(0); // the bytes never carry an id: the authority's seat for the sender wins
            yield return new SteerAngel(spoof, new Vec2(12.5f, -3f), 0.016f);
            yield return new ArmGrapple(spoof);
            yield return new ReleaseGrapple(spoof);
            yield return new ChangeChainLength(spoof, -2);
            yield return new ToggleFollow(spoof);
            yield return new SkinLinks(spoof);
            yield return new ToggleMaceMode(spoof);
            yield return new TuneChain(spoof, ChainKnob.Gravity, 4);
            yield return new ChangeGameSpeed(spoof, -1, 0.25f);
            yield return new Pause(spoof, true);
            yield return new TogglePause(spoof);
            yield return new ResetGame(spoof);
        }

        [Test]
        public void EverySentCommand_RoundTripsAsTheSendersSeat()
        {
            foreach (var command in EveryCommand())
            {
                Assert.IsTrue(CommandCodec.IsSent(command), command.ToString());
                var decoded = CommandCodec.Decode(CommandCodec.Encode(command), Sender);
                Assert.IsNotNull(decoded, command.ToString());
                Assert.AreEqual(command.GetType(), decoded.GetType());
                Assert.AreEqual(Sender, decoded.Player, "the seat the authority knows the sender by, never the id in the bytes");

                // Every payload survives: compare the text past the player id.
                Assert.AreEqual(Payload(command), Payload(decoded), command.ToString());
            }
        }

        [Test]
        public void QuitIsNeverSent()
        {
            var quit = new QuitGame(Sender);
            Assert.IsFalse(CommandCodec.IsSent(quit));
            Assert.IsFalse(CommandCodec.IsSent(null));
            Assert.Throws<ArgumentException>(() => CommandCodec.Encode(quit));
        }

        [Test]
        public void MalformedBytes_DecodeToNull()
        {
            var steer = CommandCodec.Encode(new SteerAngel(Sender, new Vec2(1f, 2f), 0.02f));

            Assert.IsNull(CommandCodec.Decode(null, Sender));
            Assert.IsNull(CommandCodec.Decode(new byte[0], Sender));
            Assert.IsNull(CommandCodec.Decode(new byte[] { CommandCodec.Version }, Sender));
            Assert.IsNull(CommandCodec.Decode(new byte[] { 99, 2 }, Sender), "another version");
            Assert.IsNull(CommandCodec.Decode(new byte[] { CommandCodec.Version, 200 }, Sender), "an unknown kind");
            Assert.IsNull(CommandCodec.Decode(steer[..^1], Sender), "truncated");

            var padded = new byte[steer.Length + 1];
            steer.CopyTo(padded, 0);
            Assert.IsNull(CommandCodec.Decode(padded, Sender), "trailing bytes");

            Assert.IsNull(CommandCodec.Decode(CommandCodec.Encode(new SteerAngel(Sender, new Vec2(float.NaN, 0f), 0.02f)), Sender), "NaN steer");
            Assert.IsNull(CommandCodec.Decode(CommandCodec.Encode(new SteerAngel(Sender, Vec2.Zero, -1f)), Sender), "negative time");
            Assert.IsNull(CommandCodec.Decode(CommandCodec.Encode(new ChangeGameSpeed(Sender, 1, float.PositiveInfinity)), Sender));

            var tune = CommandCodec.Encode(new TuneChain(Sender, ChainKnob.Spring, 1));
            tune[2] = 77; // no such knob
            Assert.IsNull(CommandCodec.Decode(tune, Sender));
        }

        [Test]
        public void ADecodedSteer_MovesTheSendersAngelInTheAuthoritysWorld()
        {
            var world = new GameWorld();
            world.AddPlayer(new PlayerId(0), PlayerSetup.Default);
            var remote = world.AddPlayer(Sender, PlayerSetup.Default);
            var start = remote.AngelPosition;

            var bytes = CommandCodec.Encode(new SteerAngel(new PlayerId(0), new Vec2(200f, 0f), 0.02f));
            Assert.IsTrue(world.Submit(CommandCodec.Decode(bytes, Sender)));

            Assert.Greater(remote.AngelPosition.X, start.X, "the sender's angel flew");
            Assert.AreEqual(start, world.FindPlayer(new PlayerId(0)).AngelPosition, "player 0 (whose id the client wrote) did not");
        }

        [Test]
        public void ASnapshot_CapturesWhatPresentationReads_AndRoundTrips()
        {
            var settings = GameWorld.Settings.Default;
            settings.Enemies.Count = 4;
            var world = new GameWorld(settings, new Random(7));
            var p0 = world.AddPlayer(new PlayerId(0), PlayerSetup.Default);
            world.AddPlayer(new PlayerId(2), PlayerSetup.Default);
            world.AddHeart();
            world.Reset();
            for (var i = 0; i < 20; i++)
            {
                world.Submit(new SteerAngel(new PlayerId(0), new Vec2(40f, 10f), 0.02f));
                world.Step(0.02f);
            }

            world.Submit(new ArmGrapple(new PlayerId(2)));
            world.Step(0.02f);
            world.TakeEvents();

            var snapshot = WorldSnapshot.Capture(world, sequence: 41);
            Assert.AreEqual(41, snapshot.Sequence);
            Assert.AreEqual(world.State.Hp, snapshot.Hp);
            Assert.AreEqual(world.State.Score, snapshot.Score);
            Assert.AreEqual(2, snapshot.Players.Count);
            Assert.IsTrue(snapshot.TryGetPlayer(new PlayerId(0), out var first));
            Assert.AreEqual(p0.AngelPosition, first.Angel);
            Assert.AreEqual(p0.MaceIndex + 1, first.Links.Length, "links from the hand to the mace");
            Assert.AreEqual(p0.MacePosition, first.Mace);
            Assert.IsTrue(snapshot.TryGetPlayer(new PlayerId(2), out var second));
            Assert.AreEqual(GrappleState.Armed, second.Grapple);
            Assert.AreEqual(world.Enemies.Count, snapshot.Enemies.Count);
            Assert.AreEqual(1, snapshot.Hearts.Count);
            Assert.IsFalse(snapshot.TryGetPlayer(new PlayerId(9), out _));

            var copy = WorldSnapshot.Decode(snapshot.Encode());
            Assert.IsNotNull(copy);
            Assert.AreEqual(snapshot.Sequence, copy.Sequence);
            Assert.AreEqual(snapshot.Time, copy.Time);
            Assert.AreEqual(snapshot.GameSpeed, copy.GameSpeed);
            Assert.AreEqual(snapshot.Hp, copy.Hp);
            Assert.AreEqual(snapshot.MaxHp, copy.MaxHp);
            Assert.AreEqual(snapshot.Score, copy.Score);
            Assert.AreEqual(snapshot.IsPaused, copy.IsPaused);
            Assert.AreEqual(snapshot.IsGameOver, copy.IsGameOver);
            Assert.AreEqual(snapshot.Ambient, copy.Ambient);
            for (var i = 0; i < snapshot.Players.Count; i++)
            {
                var a = snapshot.Players[i];
                var b = copy.Players[i];
                Assert.AreEqual(a.Id, b.Id);
                Assert.AreEqual(a.Angel, b.Angel);
                Assert.AreEqual(a.Facing, b.Facing);
                Assert.AreEqual(a.Frame, b.Frame);
                Assert.AreEqual(a.MaceIndex, b.MaceIndex);
                Assert.AreEqual(a.Grapple, b.Grapple);
                Assert.AreEqual(a.IsFollowing, b.IsFollowing);
                Assert.AreEqual(a.MaceMode, b.MaceMode);
                CollectionAssert.AreEqual(a.Links, b.Links);
            }

            for (var i = 0; i < snapshot.Enemies.Count; i++)
            {
                Assert.AreEqual(snapshot.Enemies[i].Id, copy.Enemies[i].Id);
                Assert.AreEqual(snapshot.Enemies[i].Position, copy.Enemies[i].Position);
                Assert.AreEqual(snapshot.Enemies[i].State, copy.Enemies[i].State);
                Assert.AreEqual(snapshot.Enemies[i].Hp, copy.Enemies[i].Hp);
                Assert.AreEqual(snapshot.Enemies[i].Frame, copy.Enemies[i].Frame);
            }

            Assert.AreEqual(snapshot.Hearts[0].X, copy.Hearts[0].X);
            Assert.AreEqual(snapshot.Hearts[0].Y, copy.Hearts[0].Y);
            Assert.AreEqual(snapshot.Hearts[0].PickedUp, copy.Hearts[0].PickedUp);
        }

        [Test]
        public void CapturingChangesNothing()
        {
            var world = new GameWorld();
            world.AddPlayer(new PlayerId(0), PlayerSetup.Default);
            world.Reset();
            world.TakeEvents();
            var before = WorldSnapshot.Capture(world).Encode();

            WorldSnapshot.Capture(world);

            CollectionAssert.AreEqual(before, WorldSnapshot.Capture(world).Encode());
            Assert.AreEqual(0, world.PendingEvents.Count);
        }

        [Test]
        public void MalformedSnapshots_DecodeToNull()
        {
            var world = new GameWorld();
            world.AddPlayer(new PlayerId(0), PlayerSetup.Default);
            var bytes = WorldSnapshot.Capture(world).Encode();

            Assert.IsNull(WorldSnapshot.Decode(null));
            Assert.IsNull(WorldSnapshot.Decode(new byte[0]));
            Assert.IsNull(WorldSnapshot.Decode(new byte[] { 42 }));
            Assert.IsNull(WorldSnapshot.Decode(bytes[..^1]), "truncated");

            var huge = (byte[])bytes.Clone();
            // The player count follows the 1-byte version and 30 bytes of header.
            BitConverter.GetBytes(int.MaxValue).CopyTo(huge, 31);
            Assert.IsNull(WorldSnapshot.Decode(huge), "an unbounded count");
        }

        [Test]
        public void TheMenusHalfOfTheGate_HoldsInputBack_WithoutTouchingTheScriptersHalf()
        {
            var gate = new InputGate();
            gate.SetMenuOpen(true);
            Assert.IsTrue(gate.MenuOpen);
            Assert.IsTrue(gate.KeysSuspended);
            Assert.IsTrue(gate.PlaySuspended);

            gate.Clear(); // the scripter closing does not open the menu's hold
            Assert.IsTrue(gate.KeysSuspended);
            Assert.IsTrue(gate.PlaySuspended);

            gate.SetMenuOpen(false);
            Assert.IsFalse(gate.KeysSuspended);
            Assert.IsFalse(gate.PlaySuspended);

            gate.Set(scripterOpen: false, scriptRunning: true);
            gate.SetMenuOpen(true);
            gate.SetMenuOpen(false);
            Assert.IsFalse(gate.KeysSuspended);
            Assert.IsTrue(gate.PlaySuspended, "a running script still holds play back");
        }

        private static string Payload(GameCommand command)
        {
            var text = command.ToString();
            var space = text.IndexOf(' ');
            var afterName = text.Substring(space + 1);
            var afterId = afterName.IndexOf(' ');
            return afterId < 0 ? string.Empty : afterName.Substring(afterId + 1);
        }
    }
}
