using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace MindAttic.Chain
{
    /// <summary>
    /// Every input of the game as one <see cref="InputActionAsset"/>, built here in code (nothing to wire in the
    /// Editor), with keyboard, mouse and gamepad bindings. Two maps:
    /// <list type="bullet">
    /// <item><b>Player</b>, one seat's controls, read by that seat's <see cref="SeatInput"/> and sent to the
    /// authority as commands carrying its player id: Steer (mouse delta), SteerStick (gamepad left stick, steering
    /// like the mouse, BIBLE D14), Grapple (left mouse button), ChainShorter and ChainLonger (Input.vb's
    /// <c>[</c> and <c>]</c>), ToggleFollow (Form1.vb's F), Pause, Reset (Form1.vb's R), ToggleJoystick
    /// (Form1.vb's J), the live-tuning keys ResistanceUp/ResistanceDown (Insert/Delete), GravityDown/GravityUp
    /// (Home/End) and SpringUp/SpringDown (PageUp/PageDown), and GameSpeedDown/GameSpeedUp (Input.vb's Minus and
    /// Equals, read while held).</item>
    /// <item><b>Debug</b>, the scene's toggles that belong to no player: ToggleLighting (Form1.vb's L, read by
    /// <see cref="SceneLight"/>), ShakeScreen (Form1.vb's 0, read by <see cref="CameraRig"/>) and ToggleFps
    /// (Form1.vb's F4, read by <see cref="GameHud"/>). Later debug keys join this map.</item>
    /// </list>
    /// Input.vb's and Form1.vb's keys keep their bindings; Minus and Equals stay free for game speed (UNI-T52)
    /// and the numpad for the debug camera (UNI-T57), as the original has them.
    /// </summary>
    public sealed class ChainControls : IDisposable
    {
        public const string PlayerMapName = "Player";
        public const string DebugMapName = "Debug";

        public ChainControls()
        {
            Asset = BuildAsset();
            Player = Asset.FindActionMap(PlayerMapName, throwIfNotFound: true);
            Debug = Asset.FindActionMap(DebugMapName, throwIfNotFound: true);
        }

        /// <summary>The asset these controls own.</summary>
        public InputActionAsset Asset { get; private set; }

        public InputActionMap Player { get; }
        public InputActionMap Debug { get; }

        public InputAction Steer => Player["Steer"];
        public InputAction SteerStick => Player["SteerStick"];
        public InputAction Grapple => Player["Grapple"];
        public InputAction ChainShorter => Player["ChainShorter"];
        public InputAction ChainLonger => Player["ChainLonger"];
        public InputAction ToggleFollow => Player["ToggleFollow"];
        public InputAction Pause => Player["Pause"];
        public InputAction Reset => Player["Reset"];
        public InputAction ToggleJoystick => Player["ToggleJoystick"];
        /// <summary>Insert: <c>changeResistance(0.001)</c>.</summary>
        public InputAction ResistanceUp => Player["ResistanceUp"];
        /// <summary>Delete: <c>changeResistance(-0.001)</c>.</summary>
        public InputAction ResistanceDown => Player["ResistanceDown"];
        /// <summary>Home: <c>changeGravity(-0.00001)</c>, gravity's y one step lower (a stronger pull down).</summary>
        public InputAction GravityDown => Player["GravityDown"];
        /// <summary>End: <c>changeGravity(0.00001)</c>.</summary>
        public InputAction GravityUp => Player["GravityUp"];
        /// <summary>PageUp: <c>changeSpring(0.001)</c>.</summary>
        public InputAction SpringUp => Player["SpringUp"];
        /// <summary>PageDown: <c>changeSpring(-0.001)</c>.</summary>
        public InputAction SpringDown => Player["SpringDown"];
        /// <summary>Minus: <c>gameSpeed -= 0.01</c> every frame it is held (Input.vb).</summary>
        public InputAction GameSpeedDown => Player["GameSpeedDown"];
        /// <summary>Equals: <c>gameSpeed += 0.01</c> every frame it is held (Input.vb).</summary>
        public InputAction GameSpeedUp => Player["GameSpeedUp"];
        public InputAction ToggleLighting => Debug["ToggleLighting"];
        public InputAction ShakeScreen => Debug["ShakeScreen"];
        public InputAction ToggleFps => Debug["ToggleFps"];

        /// <summary>A new asset with the game's two maps and every binding.</summary>
        public static InputActionAsset BuildAsset()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "ChainControls";

            var player = asset.AddActionMap(PlayerMapName);
            // A delta is a per-frame count, not a state: pass every change straight through.
            player.AddAction("Steer", InputActionType.PassThrough, "<Mouse>/delta", expectedControlLayout: "Vector2");
            player.AddAction("SteerStick", InputActionType.Value, "<Gamepad>/leftStick", expectedControlLayout: "Vector2");
            Button(player, "Grapple", "<Mouse>/leftButton", "<Gamepad>/rightTrigger", "<Gamepad>/buttonSouth");
            Button(player, "ChainShorter", "<Keyboard>/leftBracket", "<Gamepad>/leftShoulder");
            Button(player, "ChainLonger", "<Keyboard>/rightBracket", "<Gamepad>/rightShoulder");
            Button(player, "ToggleFollow", "<Keyboard>/f", "<Gamepad>/buttonNorth");
            // Form1.vb's Pause key; P and Escape until UNI-T54 settles the keys (BIBLE V8).
            Button(player, "Pause", "<Keyboard>/pause", "<Keyboard>/p", "<Keyboard>/escape", "<Gamepad>/start");
            Button(player, "Reset", "<Keyboard>/r", "<Gamepad>/select");
            Button(player, "ToggleJoystick", "<Keyboard>/j");
            // Form1.vb's live-tuning keys (UNI-T51): resistance, gravity and the spring of this seat's chain.
            Button(player, "ResistanceUp", "<Keyboard>/insert");
            Button(player, "ResistanceDown", "<Keyboard>/delete");
            Button(player, "GravityDown", "<Keyboard>/home");
            Button(player, "GravityUp", "<Keyboard>/end");
            Button(player, "SpringUp", "<Keyboard>/pageUp");
            Button(player, "SpringDown", "<Keyboard>/pageDown");
            // Input.vb's Minus and Equals (UNI-T52): the game speed, while held.
            Button(player, "GameSpeedDown", "<Keyboard>/minus");
            Button(player, "GameSpeedUp", "<Keyboard>/equals");

            var debug = asset.AddActionMap(DebugMapName);
            Button(debug, "ToggleLighting", "<Keyboard>/l");
            Button(debug, "ShakeScreen", "<Keyboard>/digit0");
            Button(debug, "ToggleFps", "<Keyboard>/f4");

            return asset;
        }

        private static void Button(InputActionMap map, string name, params string[] bindings)
        {
            var action = map.AddAction(name, InputActionType.Button);
            foreach (var binding in bindings)
            {
                action.AddBinding(binding);
            }
        }

        public void Dispose()
        {
            if (Asset == null)
            {
                return;
            }

            Asset.Disable();
            if (Application.isPlaying)
            {
                Object.Destroy(Asset);
            }
            else
            {
                Object.DestroyImmediate(Asset);
            }

            Asset = null;
        }
    }
}
