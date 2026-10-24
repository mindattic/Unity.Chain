using MindAttic.Chain.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace MindAttic.Chain
{
    /// <summary>
    /// The screen-space HUD, a UI Toolkit <see cref="UIDocument"/> built entirely in code (its
    /// <see cref="PanelSettings"/>, theme and visual tree; nothing to wire in the Inspector), the port of Gui.vb and
    /// Engine.vb's <c>renderDisplay</c> (UNI-T50):
    /// <list type="bullet">
    /// <item>the status box art (Gui.vb renderStatus: <c>status_box.png</c>, 96 x 48 at the top left);</item>
    /// <item>HP as one <c>|</c> per point and the score, red; the grappled enemy's HP in yellow while the grapple
    /// holds one; the chain's <c>Res</c>/<c>Grav</c>/<c>Spring</c> in blue; the game speed, red, top right;</item>
    /// <item>the round clock (Gui.vb updateClock, UNI-T53): <c>m:ss</c> of <see cref="GameWorld.RoundTime"/> at
    /// (300, 5) in yellow, back to 0:00 on every reset, hidden with the rest of the status layer while paused;</item>
    /// <item>the frame rate top right (Gui.vb renderFPS), sampled every 100 ms by a <see cref="FrameRateCounter"/>,
    /// shown from the start, flipped by F4 (the Debug map's ToggleFps) and shown again by every reset (Root.vb's
    /// <c>bDrawFPS = True</c>);</item>
    /// <item>Gui.vb's renderMenu (UNI-T55): Tab opens <c>menu.jpg</c>, 255 x 255 at (10, 10), listing F1 solid or
    /// wireframe, F2 textured or shaded, F3 culling, F4 the frame rate, F12 configure video and ESC quit, each line
    /// naming what its key does now (<see cref="GameMenu"/>); the flags are the screen's <see cref="Options"/>
    /// (Root.vb's, all put back and the menu closed by every reset); F12 switches windowed and fullscreen
    /// (<see cref="DisplayPreferences.ToggleScreen"/>) and ESC is the seat's own Quit, the world's
    /// <see cref="QuitGame"/>; drawn only while not paused, as oneFrameRender draws it;</item>
    /// <item>while paused only the half see-through red <c>- PAUSE -</c> banner (oneFrameRender draws none of the
    /// HUD while paused), over the port's dim; at game over the port's banner over the fade (dimmed here unless
    /// <see cref="SceneLight"/> shows it).</item>
    /// </list>
    /// Every position, colour, font size and the sample rate come from hud.json (<see cref="HudData"/>), in pixels of
    /// the original's 512 x 448 window, which the panel scales to the screen's height
    /// (<see cref="ReferenceResolution"/>, matched on height); <c>TopRight</c> text keeps its distance from the right
    /// edge on any aspect.
    ///
    /// <see cref="Root"/> is the HUD root the later HUD, menu, scripter and settings panels are added to
    /// (<see cref="AddPanel"/>): the HUD's own layers sit at its bottom, so a panel added later draws over them. The
    /// HUD reads everything from the world through <see cref="GameSession"/> (refreshing on its events and every
    /// frame) and the player's <see cref="GrappleController"/>, and changes nothing; the strings come from
    /// <see cref="HudText"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)] // after the session and the views have run for the frame
    public sealed class GameHud : MonoBehaviour
    {
        /// <summary>The panel's design resolution; it scales to the screen matching on height.</summary>
        public static readonly Vector2Int ReferenceResolution = new Vector2Int(1280, 720);

        /// <summary>The status box art, under Resources (Gui.vb's gfx/gui/status_box.png, magenta keyed to alpha).</summary>
        public const string StatusBoxPath = "UI/status_box";

        /// <summary>The menu art, under Resources (Gui.vb's gfx/gui/menu.jpg, its top two rows cropped as loadMenu's v does).</summary>
        public const string MenuPath = "UI/menu";

        private const string ThemePath = "UI/ChainTheme";

        [Tooltip("Falls back to the scene's GameSession.")]
        [SerializeField] private GameSession session;
        [Tooltip("Shows the grappled enemy's HP while it holds one. Falls back to the scene's GrappleController.")]
        [SerializeField] private GrappleController grapple;
        [Tooltip("While this lights the scene, its light shows the game-over fade and the HUD does not dim. Falls back to the scene's SceneLight.")]
        [SerializeField] private SceneLight sceneLight;

        private UIDocument _document;
        private PanelSettings _panelSettings;
        private GameSession _subscribed;
        private ChainControls _controls;
        private HudData _layout;
        private FrameRateCounter _frames;
        private ViewOptions _options;

        private VisualElement _root;
        private VisualElement _overlay;
        private VisualElement _status;
        private VisualElement _statusBox;
        private Label _hp;
        private Label _score;
        private Label _readout;
        private Label _grappled;
        private Label _clock;
        private Label _gameSpeed;
        private Label _fps;
        private Label _pause;
        private Label _gameOver;
        private VisualElement _menu;
        private readonly Label[] _menuLines = new Label[GameMenu.Entries.Count];

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

        /// <summary>hud.json: the layout the HUD is built from (the session's registry, else the data files').</summary>
        public HudData Layout => _layout ?? (_layout = (session != null ? session.Settings : SettingsStore.Current).Hud);

        /// <summary>Original-window pixels to panel pixels: the panel's height over the original's (720 / 448).</summary>
        public float Scale => ReferenceResolution.y / Layout.Screen.Y;

        /// <summary>
        /// The HUD root: a full-screen element that lets clicks through, holding the HUD's own layers. Later panels go
        /// on it with <see cref="AddPanel"/>. Built on first use, so it can be read (and tested) before the document's
        /// panel exists.
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

        /// <summary>The status box art's element.</summary>
        public VisualElement StatusBox => Root != null ? _statusBox : null;

        /// <summary>Whether the status layer (box, HP, score, readouts) is drawn: not while paused, as in oneFrameRender.</summary>
        public bool StatusShown => Root != null && Shown(_status);

        /// <summary>The HP line as it is shown.</summary>
        public string HpText => Root != null ? _hp.text : null;

        /// <summary>The score line as it is shown.</summary>
        public string ScoreText => Root != null ? _score.text : null;

        /// <summary>The Res/Grav/Spring block as it is shown.</summary>
        public string ReadoutText => Root != null ? _readout.text : null;

        /// <summary>The round clock (<c>m:ss</c>) as it is shown, or null while it is hidden (paused).</summary>
        public string ClockText => Root != null && Shown(_status) ? _clock.text : null;

        /// <summary>The game speed as it is shown.</summary>
        public string GameSpeedText => Root != null ? _gameSpeed.text : null;

        /// <summary>The grappled enemy's HP line, or null while it is hidden.</summary>
        public string GrappledText => Root != null && Shown(_grappled) ? _grappled.text : null;

        /// <summary>The frame rate as it is shown, or null while it is hidden (F4 off, or paused).</summary>
        public string FpsText => Root != null && Shown(_fps) ? _fps.text : null;

        /// <summary>The centered banner (pause or game over), or null while neither is shown.</summary>
        public string BannerText => Root == null ? null : Shown(_gameOver) ? _gameOver.text : Shown(_pause) ? _pause.text : null;

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
        /// Root.vb's option flags (wireframe, textures, culling, the frame rate, the menu), the screen's own: F1-F4 and
        /// Tab flip them, every reset puts them back. The renderer reads the first three (UNI-T56).
        /// </summary>
        public ViewOptions Options => _options ?? (_options = new ViewOptions(Layout.DrawFps));

        /// <summary>Root.vb's bDrawFPS: whether the frame rate is drawn. F4 flips it; every reset turns it back on.</summary>
        public bool ShowFps
        {
            get => Options.DrawFps;
            set
            {
                Options.DrawFps = value;
                Refresh();
            }
        }

        /// <summary>Whether the menu panel is drawn: open (Tab) and not paused, as oneFrameRender draws renderMenu.</summary>
        public bool MenuShown => Root != null && Shown(_menu);

        /// <summary>The menu panel's element.</summary>
        public VisualElement MenuPanel => Root != null ? _menu : null;

        /// <summary><paramref name="entry"/>'s menu line as it reads now.</summary>
        public string MenuLineText(MenuEntry entry) => Root != null ? _menuLines[(int)entry].text : null;

        /// <summary>Form1.vb's Tab: opens or closes the menu (it works paused, and shows once play resumes). Returns whether it is open.</summary>
        public bool ToggleMenu()
        {
            var open = Options.ToggleMenu();
            Refresh();
            return open;
        }

        /// <summary>
        /// What a menu line's key does: F1-F4 flip their flag (returning its new state), F12 switches windowed and
        /// fullscreen (true when it went fullscreen), ESC sends the world <see cref="QuitGame"/> for the seated player,
        /// as the seat's own Escape does (true when the world took it).
        /// </summary>
        public bool Act(MenuEntry entry)
        {
            bool result;
            switch (entry)
            {
                case MenuEntry.Video:
                    result = DisplayPreferences.ToggleScreen() != WindowMode.Windowed;
                    break;
                case MenuEntry.Quit:
                    var player = session != null ? FindPlayer() : null;
                    result = player != null && session.Submit(new QuitGame(player.Id));
                    break;
                default:
                    result = Options.Toggle(entry);
                    break;
            }

            Refresh();
            return result;
        }

        /// <summary>Gui.vb's frame counter (sampled every hud.json <c>fpsSampleMs</c>).</summary>
        public FrameRateCounter Frames => _frames ?? (_frames = new FrameRateCounter(Layout.FpsSampleMs));

        /// <summary>Form1.vb's F4: flips <see cref="ShowFps"/>. Returns the new state.</summary>
        public bool ToggleFps() => Act(MenuEntry.Fps);

        /// <summary>
        /// One drawn frame at <paramref name="nowMs"/> of real time: counts it while the frame rate is drawn and the
        /// game is not paused (renderFPS runs only then), and shows the latest sample. Called every frame; public so
        /// tests can drive it.
        /// </summary>
        public void TickFps(long nowMs)
        {
            if (Options.DrawFps && (session == null || !session.State.IsPaused))
            {
                Frames.Frame(nowMs);
            }

            if (_root != null)
            {
                SetText(_fps, HudText.Fps(Frames.FrameRate));
            }
        }

        /// <summary>
        /// Adds a panel over the HUD's own layers (the later HUD, menu, scripter and settings
        /// panels). Returns the panel.
        /// </summary>
        public T AddPanel<T>(T panel) where T : VisualElement
        {
            Root.Add(panel);
            return panel;
        }

        /// <summary>Shows the world's state: the status layer, the frame rate, and the pause or game-over banner.</summary>
        public void Refresh()
        {
            if (_root == null)
            {
                return;
            }

            RefreshMenu();
            if (session == null)
            {
                SetShown(_status, false);
                SetShown(_fps, false);
                SetShown(_pause, false);
                SetShown(_gameOver, false);
                SetShown(_overlay, false);
                return;
            }

            var state = session.State;
            var paused = state.IsPaused && !state.IsGameOver;

            // oneFrameRender draws the status box, renderDisplay and renderFPS only while not paused.
            SetShown(_status, !paused);
            SetShown(_fps, !paused && Options.DrawFps);

            SetText(_hp, HudText.Hp(state.Hp));
            SetText(_score, HudText.Score(state.Score));
            SetText(_clock, HudText.Clock(session.World.RoundTime));
            SetText(_gameSpeed, HudText.GameSpeed(session.World.GameSpeed));

            var player = FindPlayer();
            SetShown(_readout, player != null);
            if (player != null)
            {
                SetText(_readout, HudText.Readout(ChainReadout.From(player.Chain.Config, session.Settings.Mapping)));
            }

            var grappled = GrappledHp;
            SetShown(_grappled, grappled.HasValue);
            if (grappled.HasValue)
            {
                SetText(_grappled, HudText.Grappled(grappled.Value));
            }

            SetShown(_gameOver, state.IsGameOver);
            SetShown(_pause, paused);
            if (state.IsGameOver)
            {
                ShowOverlay(GameOverDim);
            }
            else if (paused)
            {
                ShowOverlay(Layout.Port.PauseDim);
            }
            else
            {
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
            (_controls ?? (_controls = new ChainControls())).Debug.Enable();
        }

        private void OnDisable()
        {
            Unsubscribe();
            _controls?.Debug.Disable();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            _controls?.Dispose();
            _controls = null;
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

        private void Update()
        {
            if (_controls == null)
            {
                return;
            }

            // Form1.vb's OnKeyDown. Escape is the seat's own Quit (SeatInput), so the menu's ESC line needs nothing here.
            if (_controls.ToggleMenu.WasPressedThisFrame()) { ToggleMenu(); }
            if (_controls.ToggleWireframe.WasPressedThisFrame()) { Act(MenuEntry.Wireframe); }
            if (_controls.ToggleTextures.WasPressedThisFrame()) { Act(MenuEntry.Textures); }
            if (_controls.ToggleCulling.WasPressedThisFrame()) { Act(MenuEntry.Culling); }
            if (_controls.ToggleFps.WasPressedThisFrame()) { Act(MenuEntry.Fps); }
            if (_controls.ConfigureVideo.WasPressedThisFrame()) { Act(MenuEntry.Video); }
        }

        // The fade, the grappled enemy's HP and the live tuning change every step without an event of their own.
        private void LateUpdate()
        {
            Attach();
            TickFps((long)(Time.realtimeSinceStartupAsDouble * 1000d));
            Refresh();
        }

        private void OnSessionEvent(GameEvent gameEvent) => Refresh();

        // Root.vb's Reset: bRenderWireframe = False, bRenderTextures = True, bBackFaceCulling = False, bDrawFPS = True,
        // bMenu = False.
        private void OnRestarted()
        {
            Options.Reset();
            Refresh();
        }

        private PlayerEntity FindPlayer()
        {
            var world = session.World;
            var player = session.Angel != null ? world.FindPlayer(session.Angel.Seat) : null;
            return player ?? (world.Players.Count > 0 ? world.Players[0] : null);
        }

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
                session.Restarted += OnRestarted;
                _subscribed = session;
            }
        }

        private void Unsubscribe()
        {
            if (_subscribed != null)
            {
                _subscribed.EventRaised -= OnSessionEvent;
                _subscribed.Restarted -= OnRestarted;
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
            settings.match = 1f; // the original window's height is the screen's height
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

            var layout = Layout;

            _root = new VisualElement { name = "hud-root", pickingMode = PickingMode.Ignore };
            Fill(_root);

            _overlay = new VisualElement { name = "hud-overlay", pickingMode = PickingMode.Ignore };
            Fill(_overlay);
            _root.Add(_overlay);

            // Gui.vb renderStatus draws the box first; renderDisplay writes over it.
            _status = new VisualElement { name = "hud-status", pickingMode = PickingMode.Ignore };
            Fill(_status);
            _root.Add(_status);

            _statusBox = new VisualElement { name = "hud-status-box", pickingMode = PickingMode.Ignore };
            _statusBox.style.position = Position.Absolute;
            _statusBox.style.left = layout.StatusBoxPosition.X * Scale;
            _statusBox.style.top = layout.StatusBoxPosition.Y * Scale;
            _statusBox.style.width = layout.StatusBoxSize.X * Scale;
            _statusBox.style.height = layout.StatusBoxSize.Y * Scale;
            var art = Resources.Load<Texture2D>(StatusBoxPath);
            if (art != null)
            {
                _statusBox.style.backgroundImage = new StyleBackground(art);
                _statusBox.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            }

            _status.Add(_statusBox);

            _hp = AddLabel(_status, "hud-hp", layout.Hp);
            _score = AddLabel(_status, "hud-score", layout.Score);
            _readout = AddLabel(_status, "hud-readout", layout.Readout);
            _grappled = AddLabel(_status, "hud-grappled", layout.Grappled);
            _clock = AddLabel(_status, "hud-clock", layout.Clock);
            _gameSpeed = AddLabel(_status, "hud-game-speed", layout.GameSpeed);

            _fps = AddLabel(_root, "hud-fps", layout.Fps);
            _fps.text = HudText.Fps(Frames.FrameRate);
            _pause = AddLabel(_root, "hud-pause", layout.Pause);
            _pause.text = HudText.Paused;
            _gameOver = AddLabel(_root, "hud-game-over", layout.Port.GameOver);
            _gameOver.text = HudText.GameOver;

            BuildMenu(layout.Menu);

            SetShown(_overlay, false);
            SetShown(_pause, false);
            SetShown(_gameOver, false);
            SetShown(_grappled, false);
            Refresh();
        }

        /// <summary>
        /// Gui.vb's loadMenu and renderMenu: menu.jpg on its 255 x 255 quad at (10, 10), and one line per entry at its
        /// own spot in the window. It sits over the HUD's own layers (a panel like the later ones) and lets clicks
        /// through; the keys act on it, as in the original.
        /// </summary>
        private void BuildMenu(HudMenu menu)
        {
            var scale = Scale;
            _menu = new VisualElement { name = "hud-menu", pickingMode = PickingMode.Ignore };
            Fill(_menu);

            var art = new VisualElement { name = "hud-menu-art", pickingMode = PickingMode.Ignore };
            art.style.position = Position.Absolute;
            art.style.left = menu.Position.X * scale;
            art.style.top = menu.Position.Y * scale;
            art.style.width = menu.Size.X * scale;
            art.style.height = menu.Size.Y * scale;
            var texture = Resources.Load<Texture2D>(MenuPath);
            if (texture != null)
            {
                art.style.backgroundImage = new StyleBackground(texture);
                art.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            }

            _menu.Add(art);
            foreach (var entry in GameMenu.Entries)
            {
                _menuLines[(int)entry] = AddLabel(_menu, "hud-menu-" + entry.ToString().ToLowerInvariant(), menu.Line(entry));
            }

            _root.Add(_menu);
        }

        private void RefreshMenu()
        {
            var paused = session != null && session.State.IsPaused && !session.State.IsGameOver;
            var shown = Options.MenuOpen && !paused;
            SetShown(_menu, shown);
            foreach (var entry in GameMenu.Entries)
            {
                SetText(_menuLines[(int)entry], GameMenu.Line(entry, Options));
            }
        }

        /// <summary>A label placed and coloured from hud.json (<see cref="HudTextStyle"/>), in panel pixels.</summary>
        private Label AddLabel(VisualElement parent, string name, HudTextStyle style)
        {
            var scale = Scale;
            var fontSize = style.FontPixels * scale;
            var label = new Label { name = name, pickingMode = PickingMode.Ignore };
            label.style.position = Position.Absolute;
            label.style.fontSize = fontSize;
            label.style.color = ToColor(style.Colour);
            label.style.unityFontStyleAndWeight = style.Bold ? FontStyle.Bold : FontStyle.Normal;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.marginLeft = 0f;
            label.style.marginRight = 0f;
            label.style.marginTop = 0f;
            label.style.marginBottom = 0f;
            label.style.paddingLeft = 0f;
            label.style.paddingRight = 0f;
            label.style.paddingTop = 0f;
            label.style.paddingBottom = 0f;

            var x = style.Position.X * scale;
            var y = style.Position.Y * scale;
            switch (style.Anchor)
            {
                case HudAnchor.TopRight:
                    // rTarget.Width - x: the left edge sits x in from the right edge, on any aspect.
                    label.style.left = Length.Percent(100);
                    label.style.marginLeft = -x;
                    label.style.top = y;
                    break;
                case HudAnchor.Center:
                    label.style.left = Length.Percent(50);
                    label.style.top = Length.Percent(50);
                    label.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
                    label.style.marginLeft = x;
                    label.style.marginTop = y;
                    label.style.unityTextAlign = TextAnchor.MiddleCenter;
                    break;
                default:
                    label.style.left = x;
                    label.style.top = y;
                    break;
            }

            if (Layout.Port.TextShadow)
            {
                var offset = Mathf.Max(1f, fontSize / 12f);
                label.style.textShadow = new TextShadow
                {
                    offset = new Vector2(offset, offset),
                    blurRadius = 0f,
                    color = new Color(0f, 0f, 0f, 0.75f * style.Colour.A / 255f),
                };
            }

            parent.Add(label);
            return label;
        }

        /// <summary>A hud.json colour ([alpha, red, green, blue], 0-255) as a Unity colour.</summary>
        public static Color ToColor(Argb colour) => new Color32(colour.R, colour.G, colour.B, colour.A);

        private static void Fill(VisualElement element)
        {
            element.style.position = Position.Absolute;
            element.style.left = 0f;
            element.style.top = 0f;
            element.style.right = 0f;
            element.style.bottom = 0f;
        }

        private void ShowOverlay(float dim)
        {
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
