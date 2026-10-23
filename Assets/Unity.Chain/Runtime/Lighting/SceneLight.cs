using MindAttic.Chain.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MindAttic.Chain
{
    /// <summary>
    /// Plays the Core <see cref="LightingState"/> (Lights.vb's <c>renderLight</c>) on a URP 2D global
    /// light: each fixed step it walks the Day, Dusk, Night, Morning tint and sets the light's colour to
    /// the session's ambient (the game-over fade, <see cref="GameState.AmbientLevel"/>) times that tint.
    /// L toggles lighting (<c>bLighting</c>). It only reads the session; it never changes the game.
    ///
    /// The time of day carries over the scene reload a new round makes, as the original's Reset left
    /// the cycle running; lighting is switched back on each round (Root.vb's Reset).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(130)] // after GameSession (120), so this step's fade is shown
    public sealed class SceneLight : MonoBehaviour
    {
        [Tooltip("Falls back to the scene's GameSession.")]
        [SerializeField] private GameSession session;
        [Tooltip("The global 2D light this drives. Falls back to one on this GameObject, added if missing.")]
        [SerializeField] private Light2D globalLight;

        [Header("Day cycle")]
        [Tooltip("Seconds in one Day, Dusk, Night, Morning cycle. The original's 1e10 s never moved (BIBLE D6); 1440 s is its own clock's 24-minute day.")]
        [Min(1f)]
        [SerializeField] private float dayLength = 1440f;
        [Tooltip("Tint units (of 255) a channel moves per second (Lights.vb: 1 a frame, 60 at 60 fps).")]
        [Min(0f)]
        [SerializeField] private float tintRate = 60f;

        // The time of day survives the scene reload a new round makes (Root.vb's Reset left it running).
        private static LightingState s_carried;

        private LightingState _state;
        private GameSession _subscribed;

        public LightingState State => _state ?? (_state = TakeState());

        public GameSession Session
        {
            get => session;
            set
            {
                Unsubscribe();
                session = value;
                if (isActiveAndEnabled)
                {
                    Subscribe();
                }
            }
        }

        public Light2D GlobalLight
        {
            get => globalLight;
            set => globalLight = value;
        }

        /// <summary>
        /// True when the 2D light is what the player sees (a URP pipeline is active and the light is
        /// on), so the game-over fade is shown by the light; otherwise the HUD shows it.
        /// </summary>
        public bool LightsTheScene => globalLight != null && globalLight.isActiveAndEnabled
            && GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset;

        /// <summary>The colour the light is set to this step.</summary>
        public Color CurrentColour
        {
            get
            {
                var ambient = session != null ? session.State.AmbientLevel : 1f;
                var lit = State.LightColour(ambient);
                return new Color(lit.R, lit.G, lit.B, 1f);
            }
        }

        /// <summary>One frame of renderLight: walks the cycle and lights the scene. Called from FixedUpdate; public so tests can drive it.</summary>
        public void Step(float deltaTime)
        {
            if (session == null || !session.State.IsPaused) // pause skipped renderLight
            {
                State.Step(deltaTime);
            }

            Apply();
        }

        /// <summary>L: lighting on or off. Returns the new state.</summary>
        public bool ToggleLighting()
        {
            var on = State.ToggleEnabled();
            Apply();
            return on;
        }

        /// <summary>Sets the light's colour from the state.</summary>
        public void Apply()
        {
            if (globalLight != null)
            {
                globalLight.color = CurrentColour;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearCarriedState() => s_carried = null;

        private LightingState TakeState()
        {
            var settings = new LightingState.Settings { DayLength = dayLength, TintRate = tintRate };
            if (!Application.isPlaying)
            {
                return new LightingState(settings);
            }

            if (s_carried == null)
            {
                s_carried = new LightingState(settings);
            }

            s_carried.Config = settings;
            s_carried.NewRound();
            return s_carried;
        }

        private void Awake()
        {
            if (session == null)
            {
                session = FindAnyObjectByType<GameSession>();
            }

            if (globalLight == null)
            {
                globalLight = GetComponent<Light2D>();
            }

            if (globalLight == null)
            {
                globalLight = gameObject.AddComponent<Light2D>();
                globalLight.lightType = Light2D.LightType.Global;
            }
        }

        private void OnEnable()
        {
            Subscribe();
            Apply();
        }

        private void OnDisable() => Unsubscribe();

        private void Subscribe()
        {
            if (_subscribed == session)
            {
                return;
            }

            Unsubscribe();
            if (session != null)
            {
                session.Restarted += OnRestarted;
                _subscribed = session;
            }
        }

        private void Unsubscribe()
        {
            if (_subscribed != null)
            {
                _subscribed.Restarted -= OnRestarted;
                _subscribed = null;
            }
        }

        private void OnRestarted()
        {
            State.NewRound();
            Apply();
        }

        private void FixedUpdate() => Step(Time.fixedDeltaTime);

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.lKey.wasPressedThisFrame)
            {
                ToggleLighting();
            }
        }

        private void OnValidate()
        {
            if (_state != null)
            {
                _state.Config = new LightingState.Settings { DayLength = dayLength, TintRate = tintRate };
            }
        }
    }
}
