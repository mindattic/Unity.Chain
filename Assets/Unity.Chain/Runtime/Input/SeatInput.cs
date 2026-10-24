using MindAttic.Chain.Core;
using UnityEngine;

namespace MindAttic.Chain
{
    /// <summary>
    /// One seat's input: it owns that seat's <see cref="ChainControls"/> (the Player map) and each frame turns
    /// what was pressed into the seat's commands, every one carrying the angel's player id: the mouse delta plus
    /// the gamepad stick (<see cref="SteerInput"/>, input.json) as <see cref="SteerAngel"/>, Pause as
    /// <see cref="TogglePause"/>, the grapple button as <see cref="ArmGrapple"/> / <see cref="ReleaseGrapple"/>,
    /// <c>[</c> and <c>]</c> as <see cref="ChangeChainLength"/>, F as <see cref="ToggleFollow"/>, Insert/Delete,
    /// Home/End and PageUp/PageDown as <see cref="TuneChain"/> (resistance, gravity, spring) and R as
    /// <see cref="ResetGame"/>. J turns the stick off and on (Form1.vb's joystick toggle), which is this seat's
    /// own input and no command. It never changes the world itself: it calls the seat's components, which submit.
    /// Sits beside the <see cref="AngelController"/>, which requires it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SeatInput : MonoBehaviour
    {
        /// <summary>One frame of a seat's input, as the actions read it (or as a test sets it).</summary>
        public struct Frame
        {
            public Vector2 MouseDelta;
            public Vector2 Stick;
            public bool GrappleHeld;
            public bool ChainShorter;
            public bool ChainLonger;
            public bool ToggleFollow;
            public bool Pause;
            public bool Reset;
            public bool ToggleJoystick;
            /// <summary>Insert and Delete.</summary>
            public bool ResistanceUp;
            public bool ResistanceDown;
            /// <summary>Home (gravity's y down a step) and End (up a step).</summary>
            public bool GravityDown;
            public bool GravityUp;
            /// <summary>PageUp and PageDown.</summary>
            public bool SpringUp;
            public bool SpringDown;
        }

        private AngelController _angel;
        private ChainControls _controls;
        private SteerInput _steering;

        public AngelController Angel => _angel != null ? _angel : _angel = GetComponent<AngelController>();

        /// <summary>This seat's actions (built on first use).</summary>
        public ChainControls Controls => _controls ?? (_controls = new ChainControls());

        /// <summary>How the stick adds to the mouse (input.json through the session's settings).</summary>
        public SteerInput Steering
        {
            get
            {
                if (_steering == null)
                {
                    var settings = Angel != null && Angel.Session != null ? Angel.Session.Settings : SettingsStore.Current;
                    _steering = new SteerInput(settings.Steering);
                }

                return _steering;
            }
        }

        /// <summary>Whether the gamepad stick steers (Input.vb's bJoystick; J toggles it).</summary>
        public bool JoystickEnabled => Steering.JoystickEnabled;

        /// <summary>Form1.vb's J. Returns the new state.</summary>
        public bool ToggleJoystick() => Steering.ToggleJoystick();

        /// <summary>What the Player map reads this frame.</summary>
        public Frame Read()
        {
            var c = Controls;
            return new Frame
            {
                MouseDelta = c.Steer.ReadValue<Vector2>(),
                Stick = c.SteerStick.ReadValue<Vector2>(),
                GrappleHeld = c.Grapple.IsPressed(),
                ChainShorter = c.ChainShorter.WasPressedThisFrame(),
                ChainLonger = c.ChainLonger.WasPressedThisFrame(),
                ToggleFollow = c.ToggleFollow.WasPressedThisFrame(),
                Pause = c.Pause.WasPressedThisFrame(),
                Reset = c.Reset.WasPressedThisFrame(),
                ToggleJoystick = c.ToggleJoystick.WasPressedThisFrame(),
                ResistanceUp = c.ResistanceUp.WasPressedThisFrame(),
                ResistanceDown = c.ResistanceDown.WasPressedThisFrame(),
                GravityDown = c.GravityDown.WasPressedThisFrame(),
                GravityUp = c.GravityUp.WasPressedThisFrame(),
                SpringUp = c.SpringUp.WasPressedThisFrame(),
                SpringDown = c.SpringDown.WasPressedThisFrame(),
            };
        }

        /// <summary>
        /// Sends one frame's input as this seat's commands. Called from <c>Update</c> with <see cref="Read"/>;
        /// public so tests can drive it. Does nothing before the angel is seated.
        /// </summary>
        public void Dispatch(Frame frame, float deltaTime)
        {
            var angel = Angel;
            if (angel == null || angel.Session == null)
            {
                return;
            }

            if (frame.ToggleJoystick)
            {
                ToggleJoystick();
            }

            var steer = Steering.Combine(frame.MouseDelta.ToCore(), frame.Stick.ToCore(), deltaTime);
            angel.SendInput(steer.ToUnity(), deltaTime, frame.Pause);

            var chain = angel.Chain;
            if (chain != null)
            {
                var grapple = chain.GetComponent<GrappleController>();
                if (grapple != null)
                {
                    grapple.SetButtonHeld(frame.GrappleHeld);
                }

                var mace = chain.GetComponent<MaceController>();
                if (mace != null)
                {
                    if (frame.ChainShorter)
                    {
                        mace.Shorten();
                    }

                    if (frame.ChainLonger)
                    {
                        mace.Lengthen();
                    }
                }

                if (frame.ToggleFollow)
                {
                    chain.ToggleFollow();
                }

                // Form1.vb's live-tuning keys, one step a press.
                Tune(chain, ChainKnob.Resistance, frame.ResistanceUp, frame.ResistanceDown);
                Tune(chain, ChainKnob.Gravity, frame.GravityUp, frame.GravityDown);
                Tune(chain, ChainKnob.Spring, frame.SpringUp, frame.SpringDown);
            }

            if (frame.Reset)
            {
                angel.Session.Submit(new ResetGame(angel.Seat));
            }
        }

        private static void Tune(ChainController chain, ChainKnob knob, bool up, bool down)
        {
            if (up)
            {
                chain.Tune(knob, 1);
            }

            if (down)
            {
                chain.Tune(knob, -1);
            }
        }

        private void OnEnable() => Controls.Player.Enable();

        private void OnDisable()
        {
            if (_controls != null)
            {
                _controls.Player.Disable();
            }

            // Never leave an enemy held by a button nobody can read any more.
            var grapple = Angel != null && Angel.Chain != null ? Angel.Chain.GetComponent<GrappleController>() : null;
            if (grapple != null)
            {
                grapple.SetButtonHeld(false);
            }
        }

        private void OnDestroy()
        {
            _controls?.Dispose();
            _controls = null;
        }

        private void Update() => Dispatch(Read(), Time.deltaTime);
    }
}
