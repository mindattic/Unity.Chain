

namespace MindAttic.Chain.Core
{
    /// <summary>
    /// Decides when the mace's swing "whoosh" plays, free of Unity audio so it can be tested.
    /// A swing fires once when the mace speed rises past <see cref="SwingSpeed"/>, and cannot fire
    /// again until the speed has dropped back under <see cref="RearmSpeed"/> (hysteresis, so a
    /// mace hovering around the threshold does not stutter) and <see cref="Cooldown"/> seconds
    /// have passed since the last one. A flail circled fast stays above the threshold, so it also
    /// re-fires every <see cref="RepeatInterval"/> seconds while it keeps whirling (0 = never).
    /// </summary>
    public sealed class SwingSoundTrigger
    {
        public float SwingSpeed;
        public float RearmSpeed;
        public float Cooldown;
        public float RepeatInterval;

        private bool _armed = true;
        private float _sinceLast = float.PositiveInfinity;

        public SwingSoundTrigger(float swingSpeed = 6f, float rearmSpeed = 3f, float cooldown = 0.15f, float repeatInterval = 0.6f)
        {
            SwingSpeed = swingSpeed;
            RearmSpeed = rearmSpeed;
            Cooldown = cooldown;
            RepeatInterval = repeatInterval;
        }

        /// <summary>True while a swing has fired and the mace has not yet slowed below <see cref="RearmSpeed"/>.</summary>
        public bool IsSwinging => !_armed;

        /// <summary>Feeds this step's mace speed (world units per second). Returns true when a swing sound should play.</summary>
        public bool Step(float speed, float deltaTime)
        {
            _sinceLast += Calc.Max(0f, deltaTime);

            if (!_armed && speed < Calc.Min(RearmSpeed, SwingSpeed))
            {
                _armed = true;
            }

            if (speed < SwingSpeed || _sinceLast < Cooldown)
            {
                return false;
            }

            if (_armed || (RepeatInterval > 0f && _sinceLast >= RepeatInterval))
            {
                _armed = false;
                _sinceLast = 0f;
                return true;
            }

            return false;
        }

        /// <summary>Forgets the current swing (the chain was teleported or rebuilt).</summary>
        public void Reset()
        {
            _armed = true;
            _sinceLast = float.PositiveInfinity;
        }

        /// <summary>
        /// Volume for a speed-driven sound: <paramref name="minVolume"/> at <paramref name="startSpeed"/>
        /// rising linearly to 1 at <paramref name="fullSpeed"/>.
        /// </summary>
        public static float VolumeForSpeed(float speed, float startSpeed, float fullSpeed, float minVolume)
        {
            if (fullSpeed <= startSpeed)
            {
                return speed >= startSpeed ? 1f : Calc.Clamp01(minVolume);
            }

            var t = Calc.Clamp01((speed - startSpeed) / (fullSpeed - startSpeed));
            return Calc.Lerp(Calc.Clamp01(minVolume), 1f, t);
        }
    }

    /// <summary>
    /// Picks a random clip index from a set (Sound.vb kept 7 sword swings, 3 hits, 4 deaths),
    /// never the same one twice in a row when there is more than one.
    /// </summary>
    public sealed class ClipPicker
    {
        private readonly System.Random _random;
        private int _last = -1;

        public ClipPicker(int seed) => _random = new System.Random(seed);

        public ClipPicker() => _random = new System.Random();

        /// <summary>A clip index in [0, count), or -1 when the set is empty.</summary>
        public int Next(int count)
        {
            if (count <= 0)
            {
                return -1;
            }

            if (count == 1)
            {
                return _last = 0;
            }

            var index = _random.Next(count - 1);
            if (_last >= 0 && _last < count && index >= _last)
            {
                index++;
            }

            return _last = index;
        }
    }
}
