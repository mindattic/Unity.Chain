using System;
using System.IO;

namespace MindAttic.Chain.Core
{
    /// <summary>
    /// A <see cref="GameCommand"/> on the wire (U.CHN-T26): an online client's input readers make the same commands as
    /// local play, and the client's <c>GameSession</c> hands each one to its networked player, which sends these bytes
    /// to the one authority. The authority decodes them for the sender's own seat (<see cref="Decode"/>), never for
    /// whatever player id the bytes name, so a client can only ever move its own angel.
    /// <para><see cref="QuitGame"/> is not sent: Escape ends the local application (or, online, opens the kit's in-game
    /// menu), never the authority's world.</para>
    /// </summary>
    public static class CommandCodec
    {
        /// <summary>The wire version; bytes with any other first byte are refused.</summary>
        public const byte Version = 1;

        private enum Kind : byte
        {
            SteerAngel = 1,
            ArmGrapple = 2,
            ReleaseGrapple = 3,
            ChangeChainLength = 4,
            ToggleFollow = 5,
            SkinLinks = 6,
            ToggleMaceMode = 7,
            TuneChain = 8,
            ChangeGameSpeed = 9,
            Pause = 10,
            TogglePause = 11,
            ResetGame = 12,
        }

        /// <summary>Whether <paramref name="command"/> travels to the authority (every command but <see cref="QuitGame"/>).</summary>
        public static bool IsSent(GameCommand command) => command != null && !(command is QuitGame);

        /// <summary>The command as bytes. The player id is left out: the authority puts the sender's seat back.</summary>
        /// <exception cref="ArgumentException">The command is not one that is sent (<see cref="IsSent"/>).</exception>
        public static byte[] Encode(GameCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            using (var stream = new MemoryStream(16))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Version);
                switch (command)
                {
                    case SteerAngel steer:
                        writer.Write((byte)Kind.SteerAngel);
                        writer.Write(steer.MouseDelta.X);
                        writer.Write(steer.MouseDelta.Y);
                        writer.Write(steer.DeltaTime);
                        break;
                    case ArmGrapple _:
                        writer.Write((byte)Kind.ArmGrapple);
                        break;
                    case ReleaseGrapple _:
                        writer.Write((byte)Kind.ReleaseGrapple);
                        break;
                    case ChangeChainLength length:
                        writer.Write((byte)Kind.ChangeChainLength);
                        writer.Write(length.Links);
                        break;
                    case ToggleFollow _:
                        writer.Write((byte)Kind.ToggleFollow);
                        break;
                    case SkinLinks _:
                        writer.Write((byte)Kind.SkinLinks);
                        break;
                    case ToggleMaceMode _:
                        writer.Write((byte)Kind.ToggleMaceMode);
                        break;
                    case TuneChain tune:
                        writer.Write((byte)Kind.TuneChain);
                        writer.Write((byte)tune.Knob);
                        writer.Write(tune.Steps);
                        break;
                    case ChangeGameSpeed speed:
                        writer.Write((byte)Kind.ChangeGameSpeed);
                        writer.Write(speed.Direction);
                        writer.Write(speed.HeldSeconds);
                        break;
                    case Pause pause:
                        writer.Write((byte)Kind.Pause);
                        writer.Write(pause.Paused);
                        break;
                    case TogglePause _:
                        writer.Write((byte)Kind.TogglePause);
                        break;
                    case ResetGame _:
                        writer.Write((byte)Kind.ResetGame);
                        break;
                    default:
                        throw new ArgumentException(command.GetType().Name + " is not sent to the authority.", nameof(command));
                }

                writer.Flush();
                return stream.ToArray();
            }
        }

        /// <summary>
        /// The command <paramref name="bytes"/> hold, as <paramref name="sender"/>'s (the seat the authority knows the
        /// connection by). Null for anything malformed, truncated, of another version or carrying a number that is not
        /// finite, so a bad client is ignored rather than throwing on the authority.
        /// </summary>
        public static GameCommand Decode(byte[] bytes, PlayerId sender)
        {
            if (bytes == null || bytes.Length < 2 || bytes[0] != Version)
            {
                return null;
            }

            try
            {
                using (var reader = new BinaryReader(new MemoryStream(bytes, 1, bytes.Length - 1)))
                {
                    GameCommand command = Read(reader, (Kind)reader.ReadByte(), sender);
                    return command != null && reader.BaseStream.Position == reader.BaseStream.Length ? command : null;
                }
            }
            catch (EndOfStreamException)
            {
                return null;
            }
        }

        private static GameCommand Read(BinaryReader reader, Kind kind, PlayerId sender)
        {
            switch (kind)
            {
                case Kind.SteerAngel:
                {
                    var delta = new Vec2(reader.ReadSingle(), reader.ReadSingle());
                    var dt = reader.ReadSingle();
                    return Finite(delta.X) && Finite(delta.Y) && Finite(dt) && dt >= 0f ? new SteerAngel(sender, delta, dt) : null;
                }

                case Kind.ArmGrapple: return new ArmGrapple(sender);
                case Kind.ReleaseGrapple: return new ReleaseGrapple(sender);
                case Kind.ChangeChainLength: return new ChangeChainLength(sender, reader.ReadInt32());
                case Kind.ToggleFollow: return new ToggleFollow(sender);
                case Kind.SkinLinks: return new SkinLinks(sender);
                case Kind.ToggleMaceMode: return new ToggleMaceMode(sender);
                case Kind.TuneChain:
                {
                    var knob = (ChainKnob)reader.ReadByte();
                    var steps = reader.ReadInt32();
                    return Enum.IsDefined(typeof(ChainKnob), knob) ? new TuneChain(sender, knob, steps) : null;
                }

                case Kind.ChangeGameSpeed:
                {
                    var direction = reader.ReadInt32();
                    var held = reader.ReadSingle();
                    return Finite(held) && held >= 0f ? new ChangeGameSpeed(sender, direction, held) : null;
                }

                case Kind.Pause: return new Pause(sender, reader.ReadBoolean());
                case Kind.TogglePause: return new TogglePause(sender);
                case Kind.ResetGame: return new ResetGame(sender);
                default: return null;
            }
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
