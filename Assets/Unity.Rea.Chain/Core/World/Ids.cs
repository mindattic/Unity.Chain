using System;

namespace MindAttic.Chain.Core
{
    /// <summary>
    /// Which player a command comes from or an event is about. There is no "current player":
    /// every command carries its sender's id, and each player's presentation holds its own id
    /// (a seat; a networked player's NetEntity carries the same number to the authority).
    /// </summary>
    public readonly struct PlayerId : IEquatable<PlayerId>
    {
        public readonly int Value;

        public PlayerId(int value)
        {
            Value = value;
        }

        public bool Equals(PlayerId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is PlayerId other && Equals(other);
        public override int GetHashCode() => Value;
        public static bool operator ==(PlayerId a, PlayerId b) => a.Value == b.Value;
        public static bool operator !=(PlayerId a, PlayerId b) => a.Value != b.Value;
        public override string ToString() => $"P{Value}";
    }

    /// <summary>An enemy in the <see cref="GameWorld"/>. Ids are never reused within a world.</summary>
    public readonly struct EnemyId : IEquatable<EnemyId>
    {
        public readonly int Value;

        public EnemyId(int value)
        {
            Value = value;
        }

        public bool Equals(EnemyId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is EnemyId other && Equals(other);
        public override int GetHashCode() => Value;
        public static bool operator ==(EnemyId a, EnemyId b) => a.Value == b.Value;
        public static bool operator !=(EnemyId a, EnemyId b) => a.Value != b.Value;
        public override string ToString() => $"E{Value}";
    }
}
