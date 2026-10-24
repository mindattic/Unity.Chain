using System;
using System.Collections.Generic;
using System.IO;

namespace MindAttic.Chain.Core
{
    /// <summary>
    /// What presentation reads of the <see cref="GameWorld"/>, captured from the one authority and sent to every online
    /// peer (U.CHN-T26): the shared HP, score, pause and game over, each seated player's angel, chain and mace, the
    /// enemies and the hearts. It is plain data the authority makes from its world (<see cref="Capture"/>); a peer reads
    /// it and never changes any world with it, so the authority stays the only thing that changes the game.
    /// </summary>
    public sealed class WorldSnapshot
    {
        /// <summary>The wire version; bytes with any other first byte are refused.</summary>
        public const byte Version = 1;

        /// <summary>The most chain links a player's entry carries (the original's chain is 25 links).</summary>
        public const int MaxLinks = 64;

        /// <summary>The most players, enemies or hearts a snapshot carries, so a damaged count cannot allocate without bound.</summary>
        public const int MaxEntries = 1024;

        public struct Player
        {
            public PlayerId Id;
            public Vec2 Angel;
            public Facing Facing;
            public int Frame;
            public int MaceIndex;
            public GrappleState Grapple;
            public bool IsFollowing;
            public MaceMode MaceMode;

            /// <summary>Links 0 (the hand) to the mace, so a peer can draw the chain as the authority has it.</summary>
            public Vec2[] Links;

            public Vec2 Mace => Links != null && Links.Length > 0 ? Links[Links.Length - 1] : Angel;
        }

        public struct Enemy
        {
            public EnemyId Id;
            public Vec2 Position;
            public EnemyState State;
            public float Hp;
            public int Frame;
        }

        public struct Heart
        {
            public float X;
            public float Y;
            public bool PickedUp;
        }

        /// <summary>Which snapshot this is; a peer keeps only the newest (unreliable delivery may reorder them).</summary>
        public int Sequence;

        public float Time;
        public float GameSpeed;
        public int Hp;
        public int MaxHp;
        public int Score;
        public bool IsPaused;
        public bool IsGameOver;
        public int Ambient;

        public readonly List<Player> Players = new List<Player>();
        public readonly List<Enemy> Enemies = new List<Enemy>();
        public readonly List<Heart> Hearts = new List<Heart>();

        /// <summary>The player entry for <paramref name="id"/>, if this snapshot has one.</summary>
        public bool TryGetPlayer(PlayerId id, out Player player)
        {
            foreach (var p in Players)
            {
                if (p.Id == id)
                {
                    player = p;
                    return true;
                }
            }

            player = default;
            return false;
        }

        /// <summary>Reads <paramref name="world"/> as it is now. Changes nothing in it.</summary>
        public static WorldSnapshot Capture(GameWorld world, int sequence = 0)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            var state = world.State;
            var snapshot = new WorldSnapshot
            {
                Sequence = sequence,
                Time = world.Time,
                GameSpeed = world.GameSpeed,
                Hp = state.Hp,
                MaxHp = state.MaxHp,
                Score = state.Score,
                IsPaused = state.IsPaused,
                IsGameOver = state.IsGameOver,
                Ambient = state.Ambient,
            };

            foreach (var p in world.Players)
            {
                var mace = Calc.Clamp(p.MaceIndex, 0, Calc.Min(p.Chain.LinkCount - 1, MaxLinks - 1));
                var links = new Vec2[mace + 1];
                for (var i = 0; i <= mace; i++)
                {
                    links[i] = p.Chain.GetPosition(i);
                }

                snapshot.Players.Add(new Player
                {
                    Id = p.Id,
                    Angel = p.AngelPosition,
                    Facing = p.Flight.Facing,
                    Frame = p.Flight.Frame,
                    MaceIndex = p.MaceIndex,
                    Grapple = p.Grapple.State,
                    IsFollowing = p.IsFollowing,
                    MaceMode = p.MaceMode,
                    Links = links,
                });
            }

            foreach (var e in world.Enemies)
            {
                if (!e.InWorld)
                {
                    continue;
                }

                snapshot.Enemies.Add(new Enemy { Id = e.Id, Position = e.Position, State = e.State, Hp = e.Hp, Frame = e.Frame });
            }

            foreach (var h in world.Hearts)
            {
                snapshot.Hearts.Add(new Heart { X = h.X, Y = h.Y, PickedUp = h.PickedUp });
            }

            return snapshot;
        }

        /// <summary>This snapshot as bytes.</summary>
        public byte[] Encode()
        {
            using (var stream = new MemoryStream(256))
            using (var w = new BinaryWriter(stream))
            {
                w.Write(Version);
                w.Write(Sequence);
                w.Write(Time);
                w.Write(GameSpeed);
                w.Write(Hp);
                w.Write(MaxHp);
                w.Write(Score);
                w.Write(IsPaused);
                w.Write(IsGameOver);
                w.Write(Ambient);

                w.Write(Players.Count);
                foreach (var p in Players)
                {
                    w.Write(p.Id.Value);
                    Write(w, p.Angel);
                    w.Write((byte)p.Facing);
                    w.Write(p.Frame);
                    w.Write(p.MaceIndex);
                    w.Write((byte)p.Grapple);
                    w.Write(p.IsFollowing);
                    w.Write((byte)p.MaceMode);
                    var links = p.Links ?? Array.Empty<Vec2>();
                    w.Write((byte)Calc.Min(links.Length, MaxLinks));
                    for (var i = 0; i < links.Length && i < MaxLinks; i++)
                    {
                        Write(w, links[i]);
                    }
                }

                w.Write(Enemies.Count);
                foreach (var e in Enemies)
                {
                    w.Write(e.Id.Value);
                    Write(w, e.Position);
                    w.Write((byte)e.State);
                    w.Write(e.Hp);
                    w.Write(e.Frame);
                }

                w.Write(Hearts.Count);
                foreach (var h in Hearts)
                {
                    w.Write(h.X);
                    w.Write(h.Y);
                    w.Write(h.PickedUp);
                }

                w.Flush();
                return stream.ToArray();
            }
        }

        /// <summary>The snapshot <paramref name="bytes"/> hold; null for anything malformed, truncated or of another version.</summary>
        public static WorldSnapshot Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 1 || bytes[0] != Version)
            {
                return null;
            }

            try
            {
                using (var r = new BinaryReader(new MemoryStream(bytes, 1, bytes.Length - 1)))
                {
                    var s = new WorldSnapshot
                    {
                        Sequence = r.ReadInt32(),
                        Time = r.ReadSingle(),
                        GameSpeed = r.ReadSingle(),
                        Hp = r.ReadInt32(),
                        MaxHp = r.ReadInt32(),
                        Score = r.ReadInt32(),
                        IsPaused = r.ReadBoolean(),
                        IsGameOver = r.ReadBoolean(),
                        Ambient = r.ReadInt32(),
                    };

                    var players = Count(r);
                    for (var i = 0; i < players; i++)
                    {
                        var p = new Player
                        {
                            Id = new PlayerId(r.ReadInt32()),
                            Angel = ReadVec(r),
                            Facing = (Facing)r.ReadByte(),
                            Frame = r.ReadInt32(),
                            MaceIndex = r.ReadInt32(),
                            Grapple = (GrappleState)r.ReadByte(),
                            IsFollowing = r.ReadBoolean(),
                            MaceMode = (MaceMode)r.ReadByte(),
                        };
                        int links = r.ReadByte();
                        if (links > MaxLinks)
                        {
                            return null;
                        }

                        p.Links = new Vec2[links];
                        for (var l = 0; l < links; l++)
                        {
                            p.Links[l] = ReadVec(r);
                        }

                        s.Players.Add(p);
                    }

                    var enemies = Count(r);
                    for (var i = 0; i < enemies; i++)
                    {
                        s.Enemies.Add(new Enemy
                        {
                            Id = new EnemyId(r.ReadInt32()),
                            Position = ReadVec(r),
                            State = (EnemyState)r.ReadByte(),
                            Hp = r.ReadSingle(),
                            Frame = r.ReadInt32(),
                        });
                    }

                    var hearts = Count(r);
                    for (var i = 0; i < hearts; i++)
                    {
                        s.Hearts.Add(new Heart { X = r.ReadSingle(), Y = r.ReadSingle(), PickedUp = r.ReadBoolean() });
                    }

                    return r.BaseStream.Position == r.BaseStream.Length ? s : null;
                }
            }
            catch (EndOfStreamException)
            {
                return null;
            }
            catch (InvalidDataException)
            {
                return null;
            }
        }

        private static int Count(BinaryReader r)
        {
            var count = r.ReadInt32();
            if (count < 0 || count > MaxEntries)
            {
                throw new InvalidDataException("count out of range");
            }

            return count;
        }

        private static void Write(BinaryWriter w, Vec2 v)
        {
            w.Write(v.X);
            w.Write(v.Y);
        }

        private static Vec2 ReadVec(BinaryReader r) => new Vec2(r.ReadSingle(), r.ReadSingle());
    }
}
