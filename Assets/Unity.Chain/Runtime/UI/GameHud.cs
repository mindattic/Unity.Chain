using MindAttic.Chain.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace MindAttic.Chain
{
    /// <summary>
    /// The screen-space HUD, a UI Toolkit <see cref="UIDocument"/> built entirely in code (its
    /// <see cref="PanelSettings"/>, theme and visual tree; nothing to wire in the Inspector), laid out
    /// like Engine.vb's <c>renderDisplay</c>: the HP bar and score top-left, the grappled enemy's HP
    /// under them while the grapple holds one, a centered "- PAUSE -" banner while paused, and a
    /// game-over banner over the ambient fade (dimmed here unless <see cref="SceneLight"/> shows it).
    /// The panel scales with the screen (<see cref="ReferenceResolution"/>, matched on height).
    ///
    /// <see cref="Root"/> is the HUD root the later HUD, menu, scripter and settings panels are added
    /// to (<see cref="AddPanel"/>): the status and overlay layers sit at its bottom, so a panel added
    /// later draws over them. The HUD reads everything from the world through
    /// <see cref="GameSession"/> (refreshing on its events and following the fade each frame) and the
    /// player's <see cref="GrappleController"/>, and changes nothing; the strings come from
    /// <see cref="HudText"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)] // after the session and the views have run for the frame
    public sealed class GameHud : MonoBehaviour
    {
        /// <summary>The panel's design resolution; it scales to the screen matching on height.</summary>
        public static readonly Vector2Int ReferenceResolution = new Vector2Int(1280, 720);

        /// <summary>How dark the pause banner's overlay is.</summary>
        public const float PauseDim = 0.45f;

        private const string ThemePath = "UI/ChainTheme";

        [Tooltip("Falls back to the scene's GameSession.")]
        [SerializeField] private GameSession session;
        [Tooltip("Shows the grappled enemy's HP while it holds one. Falls back to the scene's GrappleController.")]
        [SerializeField] private GrappleController grapple;
        [Tooltip("While this lights the scene, its light shows the game-over fade and the HUD does not dim. Falls back to the scene's SceneLight.")]
        [SerializeField] private SceneLight sceneLight;

        [Header("Look")]
        [Tooltip("HUD text height as a fraction of the screen height.")]
        [Range(0.01f, 0.1f)]
        [SerializeField] private float textScale = 0.03f;
        [Tooltip("Inset of the HUD from the screen's top-left corner, as a fraction of the screen height.")]
        [Range(0f, 0.2f)]
        [SerializeField] private float margin = 0.02f;
        [SerializeField] private Color statusColor = new Color(1f, 0.2f, 0.2f);
        [SerializeField] private Color grappleColor = new Color(1f, 1f, 0.2f);
        [SerializeField] private Color bannerColor = Color.white;

        private UIDocument _document;
        private PanelSettings _panelSettings;
        private GameSession _subscribed;

        private VisualElement _root;
        private VisualElement _overlay;
        private Label _hp;
        private Label _score;
        private Label _grappled;
        private Label _banner;

        public GameSession Session
        {
            get => session;
            set
            {
                session = value;
                if (isActiveAndEnabled)
                {
                    Subscribe();
                }

                Refresh();
            }
        }

        public GrappleController Grapple
        {
            get => grapple;
            set
            {
                grapple = value;
                Refresh();
            }
        }

        public SceneLight SceneLight
        {
            get => sceneLight;
            set
            {
                sceneLight = value;
                Refresh();
            }
        }

        /// <summary>
        /// The HUD root: a full-screen element that lets clicks through, holding the status and
        /// overlay layers. Later panels go on it with <see cref="AddPanel"/>. Built on first use, so it
        /// can be read (and tested) before the document's panel exists.
        /// </summary>
        public VisualElement Root
        {
            get
            {
                Build();
                return _root;
            }
        }

        /// <summary>The document the HUD is shown through (null until the component wakes).</summary>
        public UIDocument Document => _document;

        /// <summary>The HP line as it is shown.</summary>
        public string HpText => Root != null ? _hp.text : null;

        /// <summary>The score line as it is shown.</summary>
        public string ScoreText => Root != null ? _score.text : null;

        /// <summary>The grappled enemy's HP line, or null while it is hidden.</summary>
        public string GrappledText => Root != null && Shown(_grappled) ? _grappled.text : null;

        /// <summary>The centered banner (pause or game over), or null while it is hidden.</summary>
        public string BannerText => Root != null && Shown(_banner) ? _banner.text : null;

        /// <summary>How dark the overlay is drawn right now (0 while hidden).</summary>
        public float OverlayDim => Root != null && Shown(_overlay) ? _overlay.style.backgroundColor.value.a : 0f;

        /// <summary>
        /// How dark the HUD's game-over overlay is: the game-over fade, unless the scene's 2D light
        /// already shows it (then 0, so the fade is not applied twice).
        /// </summary>
        public float GameOverDim => session == null || (sceneLight != null && sceneLight.LightsTheScene)
            ? 0f
            : 1f - session.State.AmbientLevel;

        /// <summary>The HP of the enemy the grapple holds, or null when it holds none.</summary>
        public float? GrappledHp => grapple != null && grapple.HeldEnemy is EnemyEntity enemy && enemy.IsAlive
            ? enemy.Hp
            : (float?)null;

        /// <summary>
        /// Adds a panel over the HUD's own layers (the later HUD, menu, scripter and settings
        /// panels). Returns the panel.
        /// </summary>
        public T AddPanel<T>(T panel) where T : VisualElement
        {
            Root.Add(panel);
            return panel;
        }

        /// <summary>Shows the world's state: HP, score, grappled HP, and the pause or game-over banner.</summary>
        public void Refresh()
        {
            if (_root == null)
            {
                return;
            }

            if (session == null)
            {
                SetShown(_hp, false);
                SetShown(_score, false);
                SetShown(_grappled, false);
                SetShown(_banner, false);
                SetShown(_overlay, false);
                return;
            }

            var state = session.State;
            SetShown(_hp, true);
            SetShown(_score, true);
            SetText(_hp, HudText.Hp(state.Hp));
            SetText(_score, HudText.Score(state.Score));

            var grappled = GrappledHp;
            SetShown(_grappled, grappled.HasValue);
            if (grappled.HasValue)
            {
                SetText(_grappled, HudText.Grappled(grappled.Value));
            }

            if (state.IsGameOver)
            {
                ShowBanner(HudText.GameOver, GameOverDim);
            }
            else if (state.IsPaused)
            {
                ShowBanner(HudText.Paused, PauseDim);
            }
            else
            {
                SetShown(_banner, false);
                SetShown(_overlay, false);
            }
        }

        private void Awake()
        {
            if (session == null)
            {
                session = FindAnyObjectByType<GameSession>();
            }

            if (grapple == null)
            {
                grapple = FindAnyObjectByType<GrappleController>();
            }

            if (sceneLight == null)
            {
                sceneLight = FindAnyObjectByType<SceneLight>();
            }

            EnsureDocument();
        }

        private void OnEnable()
        {
            Subscribe();
            Attach();
            Refresh();
        }

        private void OnDisable() => Unsubscribe();

        private void OnDestroy()
        {
            Unsubscribe();
            if (_panelSettings != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_panelSettings);
                }
                else
                {
                    DestroyImmediate(_panelSettings);
                }

                _panelSettings = null;
            }
        }

        // The fade and the grappled enemy's HP change every step without an event of their own.
        private void LateUpdate()
        {
            Attach();
            Refresh();
        }

        private void OnSessionEvent(GameEvent gameEvent) => Refresh();

        private void Subscribe()
        {
            if (_subscribed == session)
            {
                return;
            }

            Unsubscribe();
            if (session != null)
            {
                session.EventRaised += OnSessionEvent;
                _subscribed = session;
            }
        }

        private void Unsubscribe()
        {
            if (_subscribed != null)
            {
                _subscribed.EventRaised -= OnSessionEvent;
                _subscribed = null;
            }
        }

        /// <summary>The document and its panel settings, made here rather than as assets.</summary>
        private void EnsureDocument()
        {
            if (_document == null)
            {
                _document = GetComponent<UIDocument>();
                if (_document == null)
                {
                    _document = gameObject.AddComponent<UIDocument>();
                }
            }

            if (_document.panelSettings == null)
            {
                _panelSettings = CreatePanelSettings();
                _document.panelSettings = _panelSettings;
            }
        }

        private static PanelSettings CreatePanelSettings()
        {
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.name = "ChainHudPanel";
            settings.hideFlags = HideFlags.DontSave;
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>(ThemePath);
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = ReferenceResolution;
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 1f; // text height is a fraction of the screen height
            settings.clearColor = false;
            return settings;
        }

        /// <summary>Puts the HUD root into the document's panel once the panel exists.</summary>
        private void Attach()
        {
            Build();
            var panelRoot = _document != null ? _document.rootVisualElement : null;
            if (panelRoot != null && _root.parent != panelRoot)
            {
                panelRoot.Add(_root);
            }
        }

        private void Build()
        {
            if (_root != null)
            {
                return;
            }

            var lineHeight = ReferenceResolution.y * textScale;
            var inset = ReferenceResolution.y * margin;

            _root = new VisualElement { name = "hud-root", pickingMode = PickingMode.Ignore };
            _root.style.position = Position.Absolute;
            _root.style.left = 0f;
            _root.style.top = 0f;
            _root.style.right = 0f;
            _root.style.bottom = 0f;

            _overlay = new VisualElement { name = "hud-overlay", pickingMode = PickingMode.Ignore };
            Fill(_overlay);
            _overlay.style.justifyContent = Justify.Center;
            _overlay.style.alignItems = Align.Center;
            _root.Add(_overlay);

            _banner = MakeLabel("hud-banner", lineHeight * 2f, bannerColor);
            _banner.style.unityTextAlign = TextAnchor.MiddleCenter;
            _overlay.Add(_banner);

            var status = new VisualElement { name = "hud-status", pickingMode = PickingMode.Ignore };
            status.style.position = Position.Absolute;
            status.style.left = inset;
            status.style.top = inset;
            status.style.flexDirection = FlexDirection.Column;
            _root.Add(status);

            _hp = MakeLabel("hud-hp", lineHeight, statusColor);
            _score = MakeLabel("hud-score", lineHeight, statusColor);
            _grappled = MakeLabel("hud-grappled", lineHeight, grappleColor);
            _grappled.style.marginTop = lineHeight * 0.5f;
            status.Add(_hp);
            status.Add(_score);
            status.Add(_grappled);

            SetShown(_overlay, false);
            SetShown(_banner, false);
            SetShown(_grappled, false);
            Refresh();
        }

        private static Label MakeLabel(string name, float fontSize, Color color)
        {
            var label = new Label { name = name, pickingMode = PickingMode.Ignore };
            label.style.fontSize = fontSize;
            label.style.color = color;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.marginLeft = 0f;
            label.style.marginRight = 0f;
            label.style.marginTop = 0f;
            label.style.marginBottom = fontSize * 0.3f;
            label.style.paddingLeft = 0f;
            label.style.paddingRight = 0f;
            label.style.paddingTop = 0f;
            label.style.paddingBottom = 0f;
            label.style.textShadow = new TextShadow
            {
                offset = new Vector2(Mathf.Max(1f, fontSize / 12f), Mathf.Max(1f, fontSize / 12f)),
                blurRadius = 0f,
                color = new Color(0f, 0f, 0f, 0.75f),
            };
            return label;
        }

        private static void Fill(VisualElement element)
        {
            element.style.position = Position.Absolute;
            element.style.left = 0f;
            element.style.top = 0f;
            element.style.right = 0f;
            element.style.bottom = 0f;
        }

        private void ShowBanner(string title, float dim)
        {
            SetText(_banner, title);
            SetShown(_banner, true);
            SetShown(_overlay, true);
            _overlay.style.backgroundColor = new Color(0f, 0f, 0f, Mathf.Clamp01(dim));
        }

        private static void SetText(Label label, string text)
        {
            if (label.text != text)
            {
                label.text = text;
            }
        }

        private static bool Shown(VisualElement element) => element.style.display.value != DisplayStyle.None;

        private static void SetShown(VisualElement element, bool shown) =>
            element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
