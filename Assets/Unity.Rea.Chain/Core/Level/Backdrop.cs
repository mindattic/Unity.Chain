using System;
using System.Collections.Generic;

namespace MindAttic.Chain.Core
{
    /// <summary>Form1.vb's two background keys.</summary>
    public enum AreaKey
    {
        /// <summary>B: <c>texBackground = areaBeach</c>.</summary>
        B,

        /// <summary>N: <c>texBackground = areaUtopia</c>.</summary>
        N,
    }

    /// <summary>
    /// area.json: Background.vb's quad, Textures.vb's area pictures and Form1.vb's B and N, and Cube.vb's brick room, in
    /// the original's own units (screen space, y up). Every name the file uses (<see cref="Start"/>, <see cref="KeyB"/>,
    /// <see cref="KeyN"/>) must be one of its <see cref="Areas"/>.
    /// </summary>
    public sealed class AreaData
    {
        private readonly Dictionary<string, string> _files = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly List<string> _areas = new List<string>();

        /// <summary>Background.vb's <c>bgWidth</c> (1.2): the quad's width from its left edge.</summary>
        public float BgWidth { get; private set; }

        /// <summary>Background.vb's <c>bgHeight</c> (0.415): the quad's half height, above and below its centre line.</summary>
        public float BgHeight { get; private set; }

        /// <summary>renderBackground's <c>Matrix.Translation(-0.56, 0, 0)</c>: where the quad's left edge and centre line sit.</summary>
        public Vec2 BgOffset { get; private set; }

        /// <summary>Textures.vb's <c>texBackground = areaBeach</c>: the area shown from the start.</summary>
        public string Start { get; private set; }

        /// <summary>The area Form1.vb's B shows (beach).</summary>
        public string KeyB { get; private set; }

        /// <summary>The area Form1.vb's N shows (utopia).</summary>
        public string KeyN { get; private set; }

        /// <summary>Every area's name, in the file's order.</summary>
        public IReadOnlyList<string> Areas => _areas;

        /// <summary>Cube.vb's <c>Matrix.Scaling(67, 55, 38)</c>: the room's width and height.</summary>
        public Vec2 CubeSize { get; private set; }

        /// <summary>Cube.vb's scaling on z (38): the room's depth, centred on the play plane.</summary>
        public float CubeDepth { get; private set; }

        /// <summary>Textures.vb's <c>texCube1</c> (brick.jpg).</summary>
        public string CubeTexture { get; private set; }

        /// <summary>The picture file of area <paramref name="name"/> (beach.png), or null if there is no such area.</summary>
        public string File(string name) => name != null && _files.TryGetValue(name, out var file) ? file : null;

        internal static AreaData Read(SettingsReader r)
        {
            var data = new AreaData
            {
                BgWidth = r.Positive("bgWidth"),
                BgHeight = r.Positive("bgHeight"),
                BgOffset = r.Pair("bgOffset"),
                Start = r.Text("texBackground"),
                KeyB = r.Text("keyB"),
                KeyN = r.Text("keyN"),
            };

            var areas = r.Section("areas");
            foreach (var name in areas.Names())
            {
                data._areas.Add(name);
                data._files[name] = areas.Text(name);
            }

            areas.Check(data._areas.Count > 0, "areas", "must name at least one area");
            areas.Done();

            foreach (var (key, name) in new[] { ("texBackground", data.Start), ("keyB", data.KeyB), ("keyN", data.KeyN) })
            {
                r.Check(name.Length == 0 || data._files.ContainsKey(name), key, $"must name one of the areas ({string.Join(", ", data._areas)}), not \"{name}\"");
            }

            var cube = r.Section("cube");
            data.CubeSize = cube.Pair("size", 0.0001f);
            data.CubeDepth = cube.Positive("depth");
            data.CubeTexture = cube.Text("texture");
            cube.Done();
            return data;
        }
    }

    /// <summary>
    /// Background.vb and Cube.vb, the scenery behind the play field (UNI-T40). The backdrop is one quad on the play
    /// plane showing the current area's picture stretched over it; the brick room is the cube the camera sits inside,
    /// drawn first, so wherever the backdrop does not reach, the room's far wall shows. The port draws orthographically
    /// (BIBLE V1), so the room is drawn as the original's perspective eye sees its far wall: projected onto the play
    /// plane (<see cref="FarWall"/>), following the eye at the parallax that distance gives (BIBLE V24).
    /// </summary>
    public static class Backdrop
    {
        /// <summary>area.json in world units (space.json's mapping).</summary>
        public readonly struct Settings
        {
            public Settings(Rect2 quad, Vec2 roomSize, float roomDepth)
            {
                Quad = quad;
                RoomSize = roomSize;
                RoomDepth = roomDepth;
            }

            /// <summary>The backdrop's quad on the play plane: left edge at bgOffset.X, bgWidth across, bgHeight above and below bgOffset.Y.</summary>
            public Rect2 Quad { get; }

            /// <summary>The brick room's width and height.</summary>
            public Vec2 RoomSize { get; }

            /// <summary>The brick room's depth; its far wall is half of it beyond the play plane.</summary>
            public float RoomDepth { get; }

            /// <summary>Maps <paramref name="area"/> (screen units) through <paramref name="mapping"/>.</summary>
            public static Settings From(AreaData area, OriginalSpace mapping)
            {
                if (area == null)
                {
                    throw new ArgumentNullException(nameof(area));
                }

                return new Settings(
                    Quad(mapping.Length(area.BgOffset), mapping.Length(area.BgWidth), mapping.Length(area.BgHeight)),
                    mapping.Length(area.CubeSize),
                    mapping.Length(area.CubeDepth));
            }
        }

        /// <summary>
        /// Root.vb's setupVertex quad as Background.vb places it: x from <paramref name="offset"/>.X to + <paramref name="width"/>,
        /// y <paramref name="halfHeight"/> either side of <paramref name="offset"/>.Y.
        /// </summary>
        public static Rect2 Quad(Vec2 offset, float width, float halfHeight) =>
            new Rect2(offset.X, offset.Y - halfHeight, width, halfHeight * 2f);

        /// <summary>
        /// Where the room's far wall (the cube's z + depth / 2 face, the one the camera looks at) falls on the play plane
        /// as an eye at <paramref name="eye"/>, <paramref name="eyeZ"/> (below 0: in front of the plane) sees it: every
        /// point of the wall scaled towards the eye by distance to the plane over distance to the wall. From Camera.vb's
        /// eye (z -1) the 38-deep room's wall is 20 times as far as the plane, so it shows at a twentieth of its size
        /// (3.35 x 2.75 screen units), centred at 0.95 of the eye's offset. An eye on or behind the plane sees no wall.
        /// </summary>
        public static Rect2 FarWall(Vec2 eye, float eyeZ, Vec2 roomSize, float roomDepth)
        {
            var toPlane = -eyeZ;
            var toWall = roomDepth * 0.5f - eyeZ;
            if (toPlane <= 0f || toWall <= 0f)
            {
                return new Rect2(eye.X, eye.Y, 0f, 0f);
            }

            var t = toPlane / toWall;
            var size = roomSize * t;
            var centre = eye * (1f - t);
            return new Rect2(centre.X - size.X * 0.5f, centre.Y - size.Y * 0.5f, size.X, size.Y);
        }
    }

    /// <summary>
    /// Textures.vb's <c>texBackground</c>: which area the backdrop shows, the screen's own view state (it belongs to no
    /// player and changes nothing in the world). It starts on area.json's <c>texBackground</c> (beach); Form1.vb's B and N
    /// show <c>keyB</c> (beach) and <c>keyN</c> (utopia), paused too, as Form1.vb's key handler runs while paused and
    /// renderBackground draws then. Root.vb's Reset never touches it, so an area chosen stays through every reset.
    /// Form1.vb's second <c>Case Keys.B</c>, <c>bBackground = Not bBackground</c>, can never run (the first B case takes
    /// the key) and would toggle a flag nothing reads; it is not ported (BIBLE D15).
    /// </summary>
    public sealed class AreaSelection
    {
        private readonly AreaData _data;

        public AreaSelection(AreaData data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            Current = data.Start;
        }

        /// <summary>The area shown now.</summary>
        public string Current { get; private set; }

        /// <summary>The picture file of the area shown now.</summary>
        public string CurrentFile => _data.File(Current);

        /// <summary>Raised after the area changes, with the new one.</summary>
        public event Action<string> Changed;

        /// <summary>The area <paramref name="key"/> shows.</summary>
        public string AreaFor(AreaKey key) => key == AreaKey.B ? _data.KeyB : _data.KeyN;

        /// <summary>Form1.vb's B or N. Returns whether the area changed (pressing B on the beach does nothing).</summary>
        public bool Press(AreaKey key) => Show(AreaFor(key));

        /// <summary>Shows area <paramref name="name"/>, one of area.json's areas. Returns whether it changed.</summary>
        public bool Show(string name)
        {
            if (_data.File(name) == null)
            {
                throw new ArgumentException($"\"{name}\" is not one of area.json's areas ({string.Join(", ", _data.Areas)})", nameof(name));
            }

            if (name == Current)
            {
                return false;
            }

            Current = name;
            Changed?.Invoke(name);
            return true;
        }
    }
}
