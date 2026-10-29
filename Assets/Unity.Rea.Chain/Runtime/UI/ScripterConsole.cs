using System.Collections.Generic;
using MindAttic.Chain.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace MindAttic.Chain
{
    /// <summary>
    /// Form1.vb's scripter mode and Script.vb's showScripter (U.CHN-T61): the grave key (the Debug map's ToggleScripter)
    /// opens a Core <see cref="Core.Scripter"/>, and while it is open this reads the keyboard itself, key by key, into
    /// it (<see cref="KeyMap"/>): letters, digits, Space and punctuation type, the C shortcuts expand, Back deletes,
    /// Delete clears, Insert recalls the last script, Escape, F11 and the grave key close, and Enter runs the line
    /// through the scripter's <see cref="ScriptRunner"/> and puts its <see cref="ScriptEffect"/>s into the camera
    /// (<see cref="CameraRig.View"/>) and the light (<see cref="SceneLight.State"/>) with <see cref="ScriptEffects"/>.
    ///
    /// The line is a label on the HUD (<see cref="GameHud.AddPanel"/>), laid out from hud.json's <c>scripter</c>
    /// (50, iHeight - 50, fontMenu) and drawn in the script colour, its I-beam blinking every half second, while open and
    /// not paused (oneFrameRender calls showScripter only unpaused); the keys work paused too, as OnKeyDown does.
    ///
    /// Each frame it sets the session's <see cref="InputGate"/> before any other reader runs: while it is open (and on
    /// the frame it closes, so the Escape that closed it does not also quit) Form1.vb's normal keys are held back, and
    /// while it is open or a script runs (<c>CHANGE SCRIPT RUNNING ON</c>, <c>bScriptRunning</c>) Input.vb's getInput is.
    /// It changes nothing in the world.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-150)] // sets the input gate before the session, the seat and the views read input
    public sealed class ScripterConsole : MonoBehaviour
    {
        /// <summary>
        /// The keyboard keys the scripter reads while open, as Form1.vb's <c>Keys</c> (US layout positions: the Input
        /// System names a key by where it sits, as WinForms' key codes do). The 102nd key's <c>Keys.OemBackslash</c> has no
        /// fixed Input System key and is left out.
        /// </summary>
        public static readonly IReadOnlyList<(Key key, ScripterKey scripter)> KeyMap = BuildKeyMap();

        [Tooltip("Whose input the scripter holds back, and whose settings lay it out. Falls back to the scene's GameSession.")]
        [SerializeField] private GameSession session;
        [Tooltip("The HUD the line is drawn on. Falls back to the scene's GameHud.")]
        [SerializeField] private GameHud hud;
        [Tooltip("Whose light CHANGE LIGHT and CHANGE TIME change. Falls back to the scene's SceneLight.")]
        [SerializeField] private SceneLight sceneLight;
        [Tooltip("Whose camera CHANGE CAMERA moves. Falls back to the scene's CameraRig.")]
        [SerializeField] private CameraRig rig;

        private Core.Scripter _scripter;
        private ChainControls _controls;
        private Label _line;
        private GameHud _attachedTo;

        public GameSession Session
        {
            get => session;
            set => session = value;
        }

        public GameHud Hud
        {
            get => hud;
            set
            {
                hud = value;
                Attach();
            }
        }

        public SceneLight SceneLight
        {
            get => sceneLight;
            set => sceneLight = value;
        }

        public CameraRig Rig
        {
            get => rig;
            set => rig = value;
        }

        /// <summary>The console's state: the line, the last script, open or closed, the runner (built on first use from hud.json).</summary>
        public Core.Scripter Scripter
        {
            get
            {
                if (_scripter == null)
                {
                    var layout = Settings.Hud.Scripter;
                    _scripter = new Core.Scripter(new ScriptRunner(layout.Colour), layout.BeamSeconds);
                }

                return _scripter;
            }
        }

        /// <summary>Root.vb's <c>bScripter</c>.</summary>
        public bool IsOpen => Scripter.IsOpen;

        /// <summary>The line's label on the HUD (null until it has a HUD).</summary>
        public Label LineLabel
        {
            get
            {
                Attach();
                return _line;
            }
        }

        /// <summary>Whether the line is drawn: open and not paused.</summary>
        public bool LineShown => LineLabel != null && LineLabel.style.display.value != DisplayStyle.None;

        /// <summary>The line as it is drawn, or null while it is hidden.</summary>
        public string LineText => LineShown ? _line.text : null;

        /// <summary>The last script Enter ran, and what came of it.</summary>
        public ScriptResult LastResult { get; private set; }

        private GameSettings Settings => session != null ? session.Settings : SettingsStore.Current;

        /// <summary>
        /// One key down, as Form1.vb's OnKeyDown takes it: closed, only the grave key opens the console; open, it edits the
        /// line, and Enter runs it (<see cref="Run"/>). Returns the result when a script ran, else null. Public so tests
        /// can type.
        /// </summary>
        public ScriptResult Press(ScripterKey key)
        {
            var script = Scripter.Press(key);
            var result = script != null ? Run(script) : null;
            SetGate(Scripter.IsOpen);
            Refresh(Time.realtimeSinceStartup);
            return result;
        }

        /// <summary>
        /// Form1.vb's Enter: <c>Script.Execute(theScript)</c> now (real seconds), against the light as it is, its effects put
        /// into the camera and the light. Returns what came of it.
        /// </summary>
        public ScriptResult Run(string script)
        {
            var lighting = sceneLight != null ? sceneLight.State : null;
            var view = lighting != null ? ScriptView.Of(lighting.Ambient, lighting) : ScriptView.Default;
            var result = Scripter.Run(script, Time.realtimeSinceStartup, view);
            ScriptEffects.Apply(result.Effects, lighting, rig != null ? rig.View : null, Settings.Mapping);
            if (sceneLight != null)
            {
                sceneLight.Apply();
            }

            LastResult = result;
            return result;
        }

        /// <summary>Shows the line as it is at <paramref name="now"/> real seconds (blinking the beam while it is drawn).</summary>
        public void Refresh(float now)
        {
            var label = LineLabel;
            if (label == null)
            {
                return;
            }

            var paused = session != null && session.State.IsPaused && !session.State.IsGameOver;
            var shown = Scripter.IsOpen && !paused;
            label.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            if (!shown)
            {
                return;
            }

            Scripter.Tick(now);
            var line = Scripter.Line;
            if (label.text != line)
            {
                label.text = line;
            }

            var colour = Scripter.Colour;
            label.style.color = (Color)new Color32((byte)colour.R, (byte)colour.G, (byte)colour.B, 255);
        }

        private void SetGate(bool keysHeld)
        {
            if (session != null)
            {
                session.InputGate.Set(keysHeld, Scripter.Runner.Running);
            }
        }

        private void Attach()
        {
            if (hud == null || _attachedTo == hud)
            {
                return;
            }

            if (_line != null)
            {
                _line.RemoveFromHierarchy();
            }

            _line = hud.CreateLabel("hud-scripter", Settings.Hud.Scripter.Style);
            _line.style.display = DisplayStyle.None;
            hud.AddPanel(_line);
            _attachedTo = hud;
        }

        private void Awake()
        {
            if (session == null)
            {
                session = FindAnyObjectByType<GameSession>();
            }

            if (hud == null)
            {
                hud = FindAnyObjectByType<GameHud>();
            }

            if (sceneLight == null)
            {
                sceneLight = FindAnyObjectByType<SceneLight>();
            }

            if (rig == null)
            {
                rig = FindAnyObjectByType<CameraRig>();
            }
        }

        private void OnEnable() => (_controls ?? (_controls = new ChainControls())).Debug.Enable();

        private void OnDisable()
        {
            _controls?.Debug.Disable();
            Scripter.Close();
            if (session != null)
            {
                session.InputGate.Clear();
            }

            Refresh(Time.realtimeSinceStartup);
        }

        private void OnDestroy()
        {
            _controls?.Dispose();
            _controls = null;
        }

        private void Update()
        {
            var wasOpen = Scripter.IsOpen;
            if (!wasOpen)
            {
                // Outside scripter mode Form1.vb's only scripter key is the grave key.
                if (_controls != null && _controls.ToggleScripter.WasPressedThisFrame())
                {
                    Scripter.Press(ScripterKey.Oemtilde);
                }
            }
            else
            {
                var keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    foreach (var (key, scripterKey) in KeyMap)
                    {
                        if (!Scripter.IsOpen)
                        {
                            break; // Escape, F11, the grave key or Enter closed it: the rest of the frame's keys are OnKeyDown's own
                        }

                        if (keyboard[key].wasPressedThisFrame)
                        {
                            var script = Scripter.Press(scripterKey);
                            if (script != null)
                            {
                                Run(script);
                            }
                        }
                    }
                }
            }

            // Held for the whole frame it was open in, so the key that closed it does nothing else.
            SetGate(wasOpen || Scripter.IsOpen);
        }

        private void LateUpdate()
        {
            Attach();
            Refresh(Time.realtimeSinceStartup);
        }

        private static IReadOnlyList<(Key, ScripterKey)> BuildKeyMap()
        {
            var map = new List<(Key, ScripterKey)>();
            for (var i = 0; i < 26; i++)
            {
                map.Add((Key.A + i, ScripterKey.A + i));
            }

            map.Add((Key.Digit0, ScripterKey.D0));
            for (var i = 1; i <= 9; i++)
            {
                map.Add((Key.Digit1 + (i - 1), ScripterKey.D0 + i));
            }

            map.Add((Key.Space, ScripterKey.Space));
            map.Add((Key.Semicolon, ScripterKey.OemSemicolon));
            map.Add((Key.Backslash, ScripterKey.OemPipe));
            map.Add((Key.Period, ScripterKey.OemPeriod));
            map.Add((Key.Slash, ScripterKey.OemQuestion));
            map.Add((Key.Equals, ScripterKey.Oemplus));
            map.Add((Key.Minus, ScripterKey.OemMinus));
            map.Add((Key.Comma, ScripterKey.Oemcomma));
            map.Add((Key.Quote, ScripterKey.OemQuotes));
            map.Add((Key.Backquote, ScripterKey.Oemtilde));
            map.Add((Key.Backspace, ScripterKey.Back));
            map.Add((Key.Delete, ScripterKey.Delete));
            map.Add((Key.Enter, ScripterKey.Enter));
            map.Add((Key.Insert, ScripterKey.Insert));
            map.Add((Key.Escape, ScripterKey.Escape));
            map.Add((Key.F11, ScripterKey.F11));
            return map;
        }
    }
}
