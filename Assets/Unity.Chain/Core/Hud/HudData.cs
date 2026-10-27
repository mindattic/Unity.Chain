namespace MindAttic.Chain.Core
{
    /// <summary>A colour in the order the original's <c>Root.Write</c> takes it: alpha, red, green, blue (0-255 each).</summary>
    public readonly struct Argb
    {
        public Argb(byte a, byte r, byte g, byte b)
        {
            A = a;
            R = r;
            G = g;
            B = b;
        }

        public byte A { get; }
        public byte R { get; }
        public byte G { get; }
        public byte B { get; }

        public override string ToString() => $"[{A}, {R}, {G}, {B}]";
    }

    /// <summary>Which point of the original's window a HUD element's position is measured from.</summary>
    public enum HudAnchor
    {
        /// <summary>The position is the text's top-left corner, from the window's top-left (most of renderDisplay).</summary>
        TopLeft,

        /// <summary>The text's left edge is <c>x</c> pixels in from the right edge (<c>rTarget.Width - 50</c>), <c>y</c> down from the top.</summary>
        TopRight,

        /// <summary>The text is centred on the window's centre, moved by the position (the pause banner).</summary>
        Center,
    }

    /// <summary>One line (or block) of HUD text: where it sits, its colour, its font size and weight.</summary>
    public readonly struct HudTextStyle
    {
        public HudTextStyle(HudAnchor anchor, Vec2 position, Argb colour, float fontSize, bool bold)
        {
            Anchor = anchor;
            Position = position;
            Colour = colour;
            FontSize = fontSize;
            Bold = bold;
        }

        public HudAnchor Anchor { get; }

        /// <summary>Pixels of the original's window, y down (see <see cref="Anchor"/>).</summary>
        public Vec2 Position { get; }

        public Argb Colour { get; }

        /// <summary>The font's size in points (Gui.vb initFont), drawn at 96 dpi in the original's window.</summary>
        public float FontSize { get; }

        public bool Bold { get; }

        /// <summary>The font's height in the original window's pixels: points at 96 dpi.</summary>
        public float FontPixels => FontSize * 96f / 72f;

        internal static HudTextStyle Read(SettingsReader parent, string key)
        {
            var r = parent.Section(key);
            var style = new HudTextStyle(
                r.Choice<HudAnchor>("anchor"),
                r.Pair("position"),
                r.Colour("colour"),
                r.Positive("fontSize"),
                r.Flag("bold"));
            r.Done();
            return style;
        }
    }

    /// <summary>
    /// hud.json: Gui.vb's status box and FPS counter, Engine.vb's renderDisplay readouts and its pause banner, laid
    /// out in pixels of the original's 512x448 window (Form1.vb) with <c>Write</c>'s [alpha, red, green, blue]
    /// colours and Gui.vb's font sizes. The HUD scales the window to the screen's height.
    /// </summary>
    public sealed class HudData
    {
        /// <summary>The original's window, pixels (Form1.vb: 512 x 448; the pause banner's 512 / 2, 448 / 2).</summary>
        public Vec2 Screen { get; private set; }

        /// <summary>Gui.vb's statOffsetX/Y: the status box's top-left corner, pixels.</summary>
        public Vec2 StatusBoxPosition { get; private set; }

        /// <summary>Gui.vb's statWidth x statHeight: 96 x 48 pixels.</summary>
        public Vec2 StatusBoxSize { get; private set; }

        /// <summary><c>HP : </c> and one bar per hit point.</summary>
        public HudTextStyle Hp { get; private set; }

        public HudTextStyle Score { get; private set; }

        /// <summary>The <c>Res</c>/<c>Grav</c>/<c>Spring</c> block.</summary>
        public HudTextStyle Readout { get; private set; }

        /// <summary>The grappled enemy's HP, yellow.</summary>
        public HudTextStyle Grappled { get; private set; }

        /// <summary>Gui.vb's updateClock: the round's <c>m:ss</c> (<see cref="RoundClock"/>) at (300, 5), yellow, fontClock.</summary>
        public HudTextStyle Clock { get; private set; }

        /// <summary><c>FormatNumber(gameSpeed, 2)</c>.</summary>
        public HudTextStyle GameSpeed { get; private set; }

        /// <summary>Gui.vb's renderFPS: <c>iFrameRate &amp; " fps"</c>.</summary>
        public HudTextStyle Fps { get; private set; }

        /// <summary><c>- PAUSE -</c>, drawn half see-through in colorReticle (red).</summary>
        public HudTextStyle Pause { get; private set; }

        /// <summary>Gui.vb's iFPSProfileSpeed: the frame rate is sampled every 100 ms.</summary>
        public int FpsSampleMs { get; private set; }

        /// <summary>Root.vb's bDrawFPS: on at the start and again on every Reset; F4 flips it.</summary>
        public bool DrawFps { get; private set; }

        /// <summary>Gui.vb's renderMenu: the Tab menu panel and its lines.</summary>
        public HudMenu Menu { get; private set; }

        /// <summary>Zone.vb's hit box overlay (the E key), in the original's screen units (UNI-T56).</summary>
        public HudZone Zone { get; private set; }

        /// <summary>Form1.vb Startup's loading picture: how many <c>load640_&lt;n&gt;</c> it picks from (UNI-T58).</summary>
        public LoadingScreen Loading { get; private set; }

        /// <summary>frmConfig.vb's settings screen (F12 and the menu's Configure Video, UNI-T59).</summary>
        public ConfigScreenData Config { get; private set; }

        /// <summary>Script.vb's showScripter: the scripter console's line (the grave key, U.CHN-T61).</summary>
        public HudScripter Scripter { get; private set; }

        public HudPort Port { get; private set; }

        internal static HudData Read(SettingsReader r)
        {
            var data = new HudData
            {
                Screen = r.Pair("screen", 1f),
            };

            var box = r.Section("statusBox");
            data.StatusBoxPosition = box.Pair("position");
            data.StatusBoxSize = box.Pair("size", 1f);
            box.Done();

            data.Hp = HudTextStyle.Read(r, "hp");
            data.Score = HudTextStyle.Read(r, "score");
            data.Readout = HudTextStyle.Read(r, "readout");
            data.Grappled = HudTextStyle.Read(r, "grappled");
            data.Clock = HudTextStyle.Read(r, "clock");
            data.GameSpeed = HudTextStyle.Read(r, "gameSpeed");
            data.Fps = HudTextStyle.Read(r, "fps");
            data.Pause = HudTextStyle.Read(r, "pause");
            data.FpsSampleMs = r.Integer("fpsSampleMs", 1, 60000);
            data.DrawFps = r.Flag("drawFps");
            data.Menu = HudMenu.Read(r);
            data.Zone = HudZone.Read(r);
            data.Loading = LoadingScreen.Read(r);
            data.Config = ConfigScreenData.Read(r);
            data.Scripter = HudScripter.Read(r);

            var port = r.Section("port");
            data.Port = new HudPort(
                pauseDim: port.Number("pauseDim", 0f, 1f),
                gameOver: HudTextStyle.Read(port, "gameOver"),
                textShadow: port.Flag("textShadow"),
                gameOverBanner: port.Flag("gameOverBanner"));
            port.Done();
            return data;
        }
    }

    /// <summary>
    /// hud.json's <c>menu</c>: Gui.vb's loadMenu and renderMenu. The panel is <c>menu.jpg</c> on a 255 x 255 quad at
    /// (10, 10) whose v starts at 2 / 255 (the art's top two rows never show); every line is written in fontMenu
    /// (Arial 10 bold) in <c>Write</c>'s 255, 255, 255, 64, at its own spot in the window.
    /// </summary>
    public sealed class HudMenu
    {
        private readonly Vec2[] _lines;

        private HudMenu(Vec2 position, Vec2 size, Argb colour, float fontSize, bool bold, Vec2[] lines)
        {
            Position = position;
            Size = size;
            Colour = colour;
            FontSize = fontSize;
            Bold = bold;
            _lines = lines;
        }

        /// <summary>The panel's top-left corner, pixels (10, 10).</summary>
        public Vec2 Position { get; }

        /// <summary>menuWidth x menuHeight: 255 x 255 pixels.</summary>
        public Vec2 Size { get; }

        /// <summary>The lines' colour.</summary>
        public Argb Colour { get; }

        /// <summary>The lines' font size in points.</summary>
        public float FontSize { get; }

        public bool Bold { get; }

        /// <summary>The style of <paramref name="entry"/>'s line: its top-left corner in the window, the menu's colour and font.</summary>
        public HudTextStyle Line(MenuEntry entry) =>
            new HudTextStyle(HudAnchor.TopLeft, _lines[(int)entry], Colour, FontSize, Bold);

        internal static HudMenu Read(SettingsReader parent)
        {
            var r = parent.Section("menu");
            var position = r.Pair("position");
            var size = r.Pair("size", 1f);
            var colour = r.Colour("colour");
            var fontSize = r.Positive("fontSize");
            var bold = r.Flag("bold");

            var lines = r.Section("lines");
            var spots = new Vec2[GameMenu.Entries.Count];
            foreach (var entry in GameMenu.Entries)
            {
                spots[(int)entry] = lines.Pair(entry.ToString());
            }

            lines.Done();
            r.Done();
            return new HudMenu(position, size, colour, fontSize, bold, spots);
        }
    }

    /// <summary>
    /// hud.json's <c>zone</c>: Zone.vb's loadZone and renderZone, in the original's screen units (y up), which
    /// <see cref="GameSettings"/> maps through space.json into <see cref="HitZones.Settings"/>. loadZone builds one
    /// line-strip box, Left 0 to Right 0.05 and Top 0 to Bottom -0.05 from its corner; renderZone moves a copy of it to
    /// each enemy's <c>Enemy(n).X + enemySize / 2, Enemy(n).Y</c> (the enemy quad's centre: it spans 0..enemySize
    /// across and -enemySize / 2..enemySize / 2 up and down) and one to <c>rectMace.Left, rectMace.Top</c>
    /// (<c>link(Mace).X + 0.01, link(Mace).Y + maceSize / 2</c>), drawn in <c>texZone</c> (gfx/gui/event.png, a 2 x 2 of
    /// 0, 128, 255) while <c>bZone</c>, which Root.vb starts True and Form1.vb's E flips.
    /// </summary>
    public sealed class HudZone
    {
        /// <summary>Root.vb's <c>bZone = True</c>: the overlay is drawn from the start.</summary>
        public bool Shown { get; private set; }

        /// <summary>loadZone's <c>Left</c>, from the box's corner (0).</summary>
        public float Left { get; private set; }

        /// <summary>loadZone's <c>Right</c> (0.05).</summary>
        public float Right { get; private set; }

        /// <summary>loadZone's <c>Top</c> (0).</summary>
        public float Top { get; private set; }

        /// <summary>loadZone's <c>Bottom</c> (-0.05, y up).</summary>
        public float Bottom { get; private set; }

        /// <summary>Where renderZone puts an enemy's box corner, from the enemy's centre (0, 0).</summary>
        public Vec2 EnemyCorner { get; private set; }

        /// <summary>
        /// Where renderZone puts the mace's box corner (rectMace's top-left), from the mace's centre: the 3.5x mace quad
        /// spans <c>link(Mace).X</c>..<c>+ 3.5 x linkSize</c>, so its centre is 0.04375 right of the link, and
        /// <c>rectMace.Left</c> 0.01 right of it is -0.03375 from the centre; <c>rectMace.Top</c> is +0.0375.
        /// </summary>
        public Vec2 MaceCorner { get; private set; }

        /// <summary>texZone's colour (event.png: 0, 128, 255, opaque).</summary>
        public Argb Colour { get; private set; }

        /// <summary>Not in the original (a D3D line strip is one pixel wide): the line's width, world units.</summary>
        public float LineWidth { get; private set; }

        internal static HudZone Read(SettingsReader parent)
        {
            var r = parent.Section("zone");
            var zone = new HudZone
            {
                Shown = r.Flag("bZone"),
                Left = r.Number("left"),
                Right = r.Number("right"),
                Top = r.Number("top"),
                Bottom = r.Number("bottom"),
                EnemyCorner = r.Pair("enemyCorner"),
                MaceCorner = r.Pair("maceCorner"),
                Colour = r.Colour("colour"),
            };

            r.Check(zone.Right > zone.Left, "right", "must be greater than left");
            r.Check(zone.Top > zone.Bottom, "top", "must be greater than bottom");

            var port = r.Section("port");
            zone.LineWidth = port.Positive("lineWidth");
            port.Done();
            r.Done();
            return zone;
        }
    }

    /// <summary>
    /// hud.json's <c>scripter</c>: Script.vb's showScripter, which writes the console's line in fontMenu (Arial 10 bold)
    /// at (50, iHeight - 50) in <c>scriptColor</c> (opaque, starting <c>Color.FromArgb(0, 0, 255)</c>), its I-beam flipping
    /// once more than <c>beamSeconds</c> (0.5) have passed.
    /// </summary>
    public readonly struct HudScripter
    {
        public HudScripter(HudTextStyle style, float beamSeconds)
        {
            Style = style;
            BeamSeconds = beamSeconds;
        }

        /// <summary>Where the line sits and its font; its colour is the starting <c>scriptColor</c>.</summary>
        public HudTextStyle Style { get; }

        /// <summary>How long the I-beam stays on or off, seconds.</summary>
        public float BeamSeconds { get; }

        /// <summary>The starting script colour, as the runner holds it.</summary>
        public Rgb24 Colour => new Rgb24(Style.Colour.R, Style.Colour.G, Style.Colour.B);

        internal static HudScripter Read(SettingsReader parent)
        {
            var r = parent.Section("scripter");
            var scripter = new HudScripter(
                new HudTextStyle(
                    r.Choice<HudAnchor>("anchor"),
                    r.Pair("position"),
                    r.Colour("colour"),
                    r.Positive("fontSize"),
                    r.Flag("bold")),
                r.Positive("beamSeconds"));
            r.Done();
            return scripter;
        }
    }

    /// <summary>
    /// hud.json's port-only figures. The original had no game-over banner, no dimming and no text shadow, so the
    /// shipped file turns all three off (U.CHN-T79, BIBLE V9); each stays a data option.
    /// </summary>
    public readonly struct HudPort
    {
        public HudPort(float pauseDim, HudTextStyle gameOver, bool textShadow, bool gameOverBanner = false)
        {
            PauseDim = pauseDim;
            GameOver = gameOver;
            TextShadow = textShadow;
            GameOverBanner = gameOverBanner;
        }

        /// <summary>Whether a banner (styled by <see cref="GameOver"/>) is written over the game-over fade.</summary>
        public bool GameOverBanner { get; }

        /// <summary>How dark the overlay behind the pause banner is (0-1).</summary>
        public float PauseDim { get; }

        /// <summary>The game-over banner over the fade.</summary>
        public HudTextStyle GameOver { get; }

        /// <summary>Whether HUD text gets a thin dark drop shadow to stay readable on the port's brighter scenes.</summary>
        public bool TextShadow { get; }
    }
}
