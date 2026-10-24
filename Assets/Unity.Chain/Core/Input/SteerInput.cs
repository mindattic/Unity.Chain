namespace MindAttic.Chain.Core
{
    /// <summary>
    /// How one player's steering input becomes the mouse delta a <see cref="SteerAngel"/> command carries:
    /// the mouse delta as it is, plus Input.vb's joystick. The original read the stick (every axis set to the
    /// range -1000..1000, pushed once past <c>joyOutsideDeadZone</c> 250) and bound it to nothing (BIBLE D14);
    /// here a pushed stick steers like the mouse, at <see cref="Settings.StickSpeed"/> mouse counts a second at
    /// full deflection, so the angel's <c>Ratio3D</c> turns both into distance the same way. Form1.vb's J
    /// (<see cref="ToggleJoystick"/>) switches the stick off and on; it starts on, as Input.vb's <c>bJoystick</c>
    /// does. This is the player's own input, not game state, so it changes nothing in the world by itself.
    /// </summary>
    public sealed class SteerInput
    {
        /// <summary>input.json, mapped (<see cref="GameSettings.Steering"/>).</summary>
        public readonly struct Settings
        {
            public Settings(bool joystickEnabled, float deadZone, float stickSpeed)
            {
                JoystickEnabled = joystickEnabled;
                DeadZone = Calc.Clamp(deadZone, 0f, 0.999f);
                StickSpeed = Calc.Max(0f, stickSpeed);
            }

            /// <summary>Input.vb's <c>bJoystick</c>: whether the stick steers to begin with.</summary>
            public bool JoystickEnabled { get; }

            /// <summary>An axis must be pushed past this fraction of full deflection to count (joyOutsideDeadZone / range: 0.25).</summary>
            public float DeadZone { get; }

            /// <summary>Mouse counts a second a fully pushed stick steers by.</summary>
            public float StickSpeed { get; }
        }

        public SteerInput(Settings settings)
        {
            Config = settings;
            JoystickEnabled = settings.JoystickEnabled;
        }

        public Settings Config { get; }

        /// <summary>Whether the stick steers now.</summary>
        public bool JoystickEnabled { get; private set; }

        /// <summary>Form1.vb's J: the stick off if it was on, on if it was off. Returns the new state.</summary>
        public bool ToggleJoystick() => JoystickEnabled = !JoystickEnabled;

        /// <summary>
        /// One stick axis (-1..1) with Input.vb's dead zone applied: zero inside it, then rising from zero at its edge
        /// to full at full deflection, so the stick has no jump where it starts to count.
        /// </summary>
        public static float Axis(float value, float deadZone)
        {
            var push = Calc.Abs(value);
            if (push <= deadZone)
            {
                return 0f;
            }

            return Calc.Sign(value) * Calc.Clamp01((push - deadZone) / (1f - deadZone));
        }

        /// <summary>The mouse counts the stick steers by over <paramref name="deltaTime"/> seconds (zero while the joystick is off).</summary>
        public Vec2 StickDelta(Vec2 stick, float deltaTime)
        {
            if (!JoystickEnabled || deltaTime <= 0f)
            {
                return Vec2.Zero;
            }

            var pushed = new Vec2(Axis(stick.X, Config.DeadZone), Axis(stick.Y, Config.DeadZone));
            return pushed * (Config.StickSpeed * deltaTime);
        }

        /// <summary>This frame's steer: the mouse delta plus whatever the stick adds.</summary>
        public Vec2 Combine(Vec2 mouseDelta, Vec2 stick, float deltaTime) => mouseDelta + StickDelta(stick, deltaTime);
    }
}
