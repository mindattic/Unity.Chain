namespace MindAttic.Chain.Core
{
    /// <summary>
    /// Camera.vb's camera and Input.vb's debug keys for it, as a pure rule. The original looks from <c>cam</c>
    /// (0, 0, -1) at <c>target</c> (0, 0, 0) through a 45 degree, 4:3 perspective (<c>initCamera</c>, rebuilt every
    /// frame). Every frame a key is held:
    /// <list type="bullet">
    /// <item>numpad plus and numpad Enter: <c>move_forward</c> / <c>move_backward</c>, <c>cam.Z</c> +/- <c>camSPD</c>
    /// (Chain.vb's 0.012), the eye nearer or further from the plane it looks at (<see cref="MoveForward"/>);</item>
    /// <item>numpad 6 and 4: <c>strafe_left</c> / <c>strafe_right</c>, <c>cam.X</c> and <c>target.X</c> +/- <c>camSPD</c>
    /// (<see cref="Strafe"/>);</item>
    /// <item>numpad 8 and 2: <c>move_up</c> / <c>move_down</c>, <c>cam.Y</c> and <c>target.Y</c> +/- 0.02 (<see cref="Lift"/>);</item>
    /// <item>X and Z: <c>isometric</c> / <c>deisometric</c>, <c>cam.X</c> and <c>cam.Y</c> +/- 0.25 with the target left
    /// where it is, so the eye swings off the axis and looks at the plane at a slant (<see cref="Isometric"/>).</item>
    /// </list>
    /// Root.vb's <c>Reset</c> puts both back (<see cref="Recenter"/>); <c>shakeScreen</c>'s restore puts the target
    /// back on the eye (<c>target.X = cam.X</c>, <c>target.Y = cam.Y</c>, <see cref="LookStraight"/>).
    ///
    /// Positions are world units relative to the rig's framed centre (the original's origin), the eye's z towards the
    /// viewer negative and the target always on z 0; speeds are per second (the original's per-frame steps at
    /// space.json's frame rate), so a key held for a second moves as far as 60 of the original's frames. One fix: the
    /// eye stops <see cref="Settings.MinDistance"/> in front of the plane it looks at, where the original walked through it (on it
    /// <c>LookAtLH</c> has no direction; past it the camera turns round and culls every sprite, BIBLE D26).
    /// </summary>
    public sealed class DebugCamera
    {
        /// <summary>camera.json, mapped to world units and seconds.</summary>
        public readonly struct Settings
        {
            public Settings(float fov, float aspect, float eyeZ, float stepSpeed, float liftSpeed, float isometricSpeed, float minDistance)
            {
                Fov = fov;
                Aspect = aspect;
                EyeZ = eyeZ;
                StepSpeed = stepSpeed;
                LiftSpeed = liftSpeed;
                IsometricSpeed = isometricSpeed;
                MinDistance = minDistance;
            }

            /// <summary>Camera.vb's <c>Fov</c>: the vertical field of view, radians (pi / 4).</summary>
            public float Fov { get; }

            /// <summary>Camera.vb's <c>PerspectiveFovLH</c> aspect, width over height (4 / 3).</summary>
            public float Aspect { get; }

            /// <summary>Where the eye starts on z (Camera.vb's <c>cam.Z</c> -1), world units, negative.</summary>
            public float EyeZ { get; }

            /// <summary><c>camSPD</c> (0.012 a frame): forward, back and strafe, world units a second.</summary>
            public float StepSpeed { get; }

            /// <summary><c>move_up</c> / <c>move_down</c>'s 0.02 a frame, world units a second.</summary>
            public float LiftSpeed { get; }

            /// <summary><c>isometric</c> / <c>deisometric</c>'s 0.25 a frame on x and y, world units a second.</summary>
            public float IsometricSpeed { get; }

            /// <summary>The nearest the eye comes to the plane it looks at, world units (the port's, BIBLE D26).</summary>
            public float MinDistance { get; }

            /// <summary>Camera.vb's own figures at a length scale of 1 and one frame a second.</summary>
            public static Settings Original => new Settings(0.7853982f, 4f / 3f, -1f, 0.012f, 0.02f, 0.25f, 0.012f);
        }

        public DebugCamera(Settings settings)
        {
            Config = settings;
            Recenter();
        }

        public Settings Config { get; }

        /// <summary>Camera.vb's <c>cam</c> x and y, world units from the framed centre.</summary>
        public Vec2 Eye { get; private set; }

        /// <summary>Camera.vb's <c>cam.Z</c>, world units, negative (in front of the plane).</summary>
        public float EyeZ { get; private set; }

        /// <summary>Camera.vb's <c>target</c> x and y, world units from the framed centre (its z is always 0).</summary>
        public Vec2 Target { get; private set; }

        /// <summary>How far the eye is from its target now, world units.</summary>
        public float Distance => Length(Target - Eye, EyeZ);

        /// <summary>How far it was at the start (the original's 1 screen unit, mapped).</summary>
        public float StartDistance => Calc.Abs(Config.EyeZ);

        /// <summary>The view's size now over its size at the start: below 1 nearer (zoomed in), above 1 further.</summary>
        public float Zoom => StartDistance > 0f ? Distance / StartDistance : 1f;

        /// <summary>Which way the eye looks, x and y (the z part is <see cref="EyeZ"/> negated); zero when it looks straight at the plane.</summary>
        public Vec2 Slant => Target - Eye;

        /// <summary>Whether anything has moved off the original's start.</summary>
        public bool IsMoved => Eye != Vec2.Zero || Target != Vec2.Zero || EyeZ != Config.EyeZ;

        /// <summary>Numpad plus (<paramref name="direction"/> +1, <c>move_forward</c>) or numpad Enter (-1, <c>move_backward</c>), held <paramref name="seconds"/>.</summary>
        public void MoveForward(int direction, float seconds)
        {
            var z = EyeZ + Sign(direction) * Config.StepSpeed * seconds;
            // The eye stays MinDistance in front of the plane it looks at (the original walked through it, BIBLE D26).
            EyeZ = Calc.Min(z, -Config.MinDistance);
        }

        /// <summary>
        /// Numpad 6 (<paramref name="direction"/> +1, <c>strafe_left</c>, the view moves right) or numpad 4 (-1, <c>strafe_right</c>),
        /// held <paramref name="seconds"/>: eye and target together on x. Returns how far they moved. Camera.vb also moved
        /// <c>link(0).X</c> by as much, but <c>renderChain</c> puts link 0 back on the hand before anything reads it, so the chain
        /// never moved (BIBLE V22): the port leaves the chain alone.
        /// </summary>
        public float Strafe(int direction, float seconds)
        {
            var dx = Sign(direction) * Config.StepSpeed * seconds;
            Pan(new Vec2(dx, 0f));
            return dx;
        }

        /// <summary>Numpad 8 (<paramref name="direction"/> +1, <c>move_up</c>) or numpad 2 (-1, <c>move_down</c>), held <paramref name="seconds"/>.</summary>
        public void Lift(int direction, float seconds) => Pan(new Vec2(0f, Sign(direction) * Config.LiftSpeed * seconds));

        /// <summary>X (<paramref name="direction"/> +1, <c>isometric</c>) or Z (-1, <c>deisometric</c>), held <paramref name="seconds"/>: the eye alone, on x and y.</summary>
        public void Isometric(int direction, float seconds)
        {
            var d = Sign(direction) * Config.IsometricSpeed * seconds;
            Eye += new Vec2(d, d);
        }

        /// <summary>Eye and target together (Script.vb's <c>CHANGE CAMERA</c> moves both too).</summary>
        public void Pan(Vec2 delta)
        {
            Eye += delta;
            Target += delta;
        }

        /// <summary>
        /// Script.vb's <c>CHANGE CAMERA X n</c> (<paramref name="axis"/> X) or <c>Y n</c>: eye and target both set to
        /// <paramref name="value"/> on that axis (<c>cam.X = Parameter</c>, <c>target.X = Parameter</c>), world units.
        /// </summary>
        public void Place(ScriptAxis axis, float value)
        {
            if (axis == ScriptAxis.X)
            {
                Eye = new Vec2(value, Eye.Y);
                Target = new Vec2(value, Target.Y);
            }
            else
            {
                Eye = new Vec2(Eye.X, value);
                Target = new Vec2(Target.X, value);
            }
        }

        /// <summary><c>shakeScreen</c>'s restore: the target back under the eye, so the view looks straight again.</summary>
        public void LookStraight() => Target = Eye;

        /// <summary>Root.vb's <c>Reset</c>: <c>cam</c> (0, 0, -1) and <c>target</c> (0, 0, 0).</summary>
        public void Recenter()
        {
            Eye = Vec2.Zero;
            Target = Vec2.Zero;
            EyeZ = Config.EyeZ;
        }

        private static float Sign(int direction) => direction > 0 ? 1f : direction < 0 ? -1f : 0f;

        private static float Length(Vec2 lateral, float z) => Calc.Sqrt(lateral.SqrMagnitude + z * z);
    }
}
