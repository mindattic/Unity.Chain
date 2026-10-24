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
    /// <c>[</c> and <c>]</c>), ToggleFollow (Form1.vb's F), SkinLinks (Form1.vb's U, the gem link skins), Pause (Form1.vb's Pause key, P too), Quit (Form1.vb's Escape), Reset (Form1.vb's R), ToggleJoystick
    /// (Form1.vb's J), the live-tuning keys ResistanceUp/ResistanceDown (Insert/Delete), GravityDown/GravityUp
    /// (Home/End) and SpringUp/SpringDown (PageUp/PageDown), and GameSpeedDown/GameSpeedUp (Input.vb's Minus and
    /// Equals, read while held).</item>
    /// <item><b>Debug</b>, the scene's toggles that belong to no player: ToggleLighting (Form1.vb's L, read by
    /// <see cref="SceneLight"/>), ShakeScreen (Form1.vb's 0, read by <see cref="CameraRig"/>), and, read by
    /// <see cref="GameHud"/>, ToggleFps (F4), ToggleMenu (Tab), ToggleWireframe (F1), ToggleTextures (F2),
    /// ToggleCulling (F3) and ConfigureVideo (F12), and ToggleZones (Form1.vb's E, Zone.vb's hit boxes, flipped by
    /// <see cref="GameHud"/> and drawn by <see cref="HitZoneOverlay"/>), and, read while held by <see cref="CameraRig"/>,
    /// Input.vb's camera keys (UNI-T57): CameraForward and CameraBackward (numpad plus and Enter), CameraLeft and
    /// CameraRight (numpad 4 and 6), CameraUp and CameraDown (numpad 8 and 2), Isometric and Deisometric (X and Z), and
    /// AreaB and AreaN (Form1.vb's B and N, the backdrop's beach and utopia, read by <see cref="AreaBackdrop"/>).
    /// Later debug keys join this map.</item>
    /// </list>
    /// Input.vb's and Form1.vb's keys keep their bindings, as the original has them.
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
        /// <summary>Form1.vb's U: links 1 to 5 put on aqua, emerald, teardrop, tablet and pearl.</summary>
        public InputAction SkinLinks => Player["SkinLinks"];
        /// <summary>Form1.vb's Pause key: <c>Pause = Not Pause</c>. P is the rebuild's extra (BIBLE V8).</summary>
        public InputAction Pause => Player["Pause"];
        /// <summary>Form1.vb's Escape: <c>bRunning = False</c>, the game ends.</summary>
        public InputAction Quit => Player["Quit"];
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
        /// <summary>Form1.vb's Tab: <c>showMenu = Not showMenu</c>, Gui.vb's menu panel.</summary>
        public InputAction ToggleMenu => Debug["ToggleMenu"];
        /// <summary>Form1.vb's F1: <c>wireframe = Not wireframe</c>.</summary>
        public InputAction ToggleWireframe => Debug["ToggleWireframe"];
        /// <summary>Form1.vb's F2: <c>useTextures = Not useTextures</c>.</summary>
        public InputAction ToggleTextures => Debug["ToggleTextures"];
        /// <summary>Form1.vb's F3: <c>backFaceCulling = Not backFaceCulling</c>.</summary>
        public InputAction ToggleCulling => Debug["ToggleCulling"];
        /// <summary>Form1.vb's F12: <c>toggleScreen</c>, windowed to fullscreen and back.</summary>
        public InputAction ConfigureVideo => Debug["ConfigureVideo"];
        /// <summary>Form1.vb's E: <c>bZone = Not bZone</c>, Zone.vb's hit box overlay.</summary>
        public InputAction ToggleZones => Debug["ToggleZones"];
        /// <summary>Input.vb's numpad plus: <c>move_forward</c>, <c>cam.Z += camSPD</c> every frame it is held.</summary>
        public InputAction CameraForward => Debug["CameraForward"];
        /// <summary>Input.vb's numpad Enter: <c>move_backward</c>, <c>cam.Z -= camSPD</c>.</summary>
        public InputAction CameraBackward => Debug["CameraBackward"];
        /// <summary>Input.vb's numpad 4: <c>strafe_right</c>, which moves <c>cam.X</c> and <c>target.X</c> by -camSPD (the view goes left).</summary>
        public InputAction CameraLeft => Debug["CameraLeft"];
        /// <summary>Input.vb's numpad 6: <c>strafe_left</c>, <c>cam.X</c> and <c>target.X</c> by +camSPD (the view goes right).</summary>
        public InputAction CameraRight => Debug["CameraRight"];
        /// <summary>Input.vb's numpad 8: <c>move_up</c>, <c>cam.Y</c> and <c>target.Y</c> by +0.02.</summary>
        public InputAction CameraUp => Debug["CameraUp"];
        /// <summary>Input.vb's numpad 2: <c>move_down</c>, by -0.02.</summary>
        public InputAction CameraDown => Debug["CameraDown"];
        /// <summary>Input.vb's X: <c>isometric</c>, <c>cam.X</c> and <c>cam.Y</c> by +0.25, the target left alone.</summary>
        public InputAction Isometric => Debug["Isometric"];
        /// <summary>Input.vb's Z: <c>deisometric</c>, by -0.25.</summary>
        public InputAction Deisometric => Debug["Deisometric"];
        /// <summary>Form1.vb's B: <c>texBackground = areaBeach</c> (area.json's keyB).</summary>
        public InputAction AreaB => Debug["AreaB"];
        /// <summary>Form1.vb's N: <c>texBackground = areaUtopia</c> (area.json's keyN).</summary>
        public InputAction AreaN => Debug["AreaN"];

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
            // Form1.vb's U (UNI-T41): the gem link skins of this seat's chain.
            Button(player, "SkinLinks", "<Keyboard>/u");
            // Form1.vb's Pause key, P as the modern extra and the gamepad's Start (BIBLE V8, UNI-T54).
            Button(player, "Pause", "<Keyboard>/pause", "<Keyboard>/p", "<Gamepad>/start");
            // Form1.vb's Escape quits the game.
            Button(player, "Quit", "<Keyboard>/escape");
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
            // Gui.vb's menu (UNI-T55): Tab opens it; F1-F3 flip the render flags it lists, F12 the screen mode.
            Button(debug, "ToggleMenu", "<Keyboard>/tab");
            Button(debug, "ToggleWireframe", "<Keyboard>/f1");
            Button(debug, "ToggleTextures", "<Keyboard>/f2");
            Button(debug, "ToggleCulling", "<Keyboard>/f3");
            Button(debug, "ConfigureVideo", "<Keyboard>/f12");
            // Form1.vb's E (UNI-T56): Zone.vb's hit boxes.
            Button(debug, "ToggleZones", "<Keyboard>/e");
            // Input.vb's camera keys (UNI-T57), read while held: Camera.vb's move, strafe and isometric.
            Button(debug, "CameraForward", "<Keyboard>/numpadPlus");
            Button(debug, "CameraBackward", "<Keyboard>/numpadEnter");
            Button(debug, "CameraLeft", "<Keyboard>/numpad4");
            Button(debug, "CameraRight", "<Keyboard>/numpad6");
            Button(debug, "CameraUp", "<Keyboard>/numpad8");
            Button(debug, "CameraDown", "<Keyboard>/numpad2");
            Button(debug, "Isometric", "<Keyboard>/x");
            Button(debug, "Deisometric", "<Keyboard>/z");
            // Form1.vb's B and N (UNI-T40): the backdrop's area. Its second B case (bBackground) is unreachable, not ported.
            Button(debug, "AreaB", "<Keyboard>/b");
            Button(debug, "AreaN", "<Keyboard>/n");

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
