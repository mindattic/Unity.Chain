using System.Collections.Generic;
using MindAttic.Chain.Core;
using UnityEngine;

namespace MindAttic.Chain
{
    /// <summary>
    /// Zone.vb's renderZone (UNI-T56): while the screen's <see cref="ViewOptions.Zones"/> is on (Root.vb's
    /// <c>bZone</c>, True at the start, flipped by E through <see cref="GameHud.ToggleZones"/>), a line-strip box
    /// around every enemy and each following mace, where <see cref="HitZones"/> puts them, in texZone's colour
    /// (hud.json's <c>zone</c>). Each box is one world-space <see cref="LineRenderer"/> of loadZone's five points, made
    /// here in code and drawn over the sprites; the boxes take the scene's 2D light, as the original's lit line strip
    /// took its ambient, so the game-over fade darkens them too. The overlay reads the world and changes nothing.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(110)] // after the session, the views and the HUD have run for the frame
    public sealed class HitZoneOverlay : MonoBehaviour
    {
        /// <summary>The boxes' sorting order: over every sprite, as renderZone would draw after renderEnemy.</summary>
        public const int SortingOrder = 1000;

        private const string LitShader = "Universal Render Pipeline/2D/Sprite-Lit-Default";
        private const string UnlitShader = "Universal Render Pipeline/2D/Sprite-Unlit-Default";

        [Tooltip("Falls back to the scene's GameSession.")]
        [SerializeField] private GameSession session;
        [Tooltip("Holds the screen's view options (E). Falls back to the scene's GameHud.")]
        [SerializeField] private GameHud hud;
        [Tooltip("The boxes' material. Falls back to a 2D sprite material made at run time.")]
        [SerializeField] private Material lineMaterial;

        private readonly List<Rect2> _boxes = new List<Rect2>();
        private readonly List<LineRenderer> _lines = new List<LineRenderer>();
        private readonly Vector3[] _strip = new Vector3[5];
        private Material _ownMaterial;

        public GameSession Session
        {
            get => session;
            set => session = value;
        }

        public GameHud Hud
        {
            get => hud;
            set => hud = value;
        }

        public Material LineMaterial
        {
            get => lineMaterial;
            set
            {
                lineMaterial = value;
                foreach (var line in _lines)
                {
                    line.sharedMaterial = Material;
                }
            }
        }

        /// <summary>Whether the boxes are drawn: the HUD's <see cref="ViewOptions.Zones"/>, else hud.json's <c>bZone</c>.</summary>
        public bool Shown => hud != null ? hud.Options.Zones : Settings.Hud.Zone.Shown;

        /// <summary>How many boxes are drawn right now.</summary>
        public int BoxCount
        {
            get
            {
                var count = 0;
                foreach (var line in _lines)
                {
                    if (line.enabled)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>The boxes drawn at the last <see cref="Redraw"/>, in <see cref="HitZones.Collect"/>'s order (mace first).</summary>
        public IReadOnlyList<Rect2> Boxes => _boxes;

        /// <summary>The line drawing box <paramref name="index"/>.</summary>
        public LineRenderer Line(int index) => _lines[index];

        private GameSettings Settings => session != null ? session.Settings : SettingsStore.Current;

        /// <summary>Puts a box on every enemy and following mace, or hides them all while zones are off. Called every frame; public so tests can drive it.</summary>
        public void Redraw()
        {
            _boxes.Clear();
            if (session != null && Shown)
            {
                HitZones.Collect(session.World, Settings.Zones, _boxes);
            }

            var zone = Settings.Hud.Zone;
            var colour = GameHud.ToColor(zone.Colour);
            for (var i = 0; i < _boxes.Count; i++)
            {
                var line = i < _lines.Count ? _lines[i] : AddLine();
                var points = HitZones.Strip(_boxes[i]);
                for (var p = 0; p < points.Length; p++)
                {
                    _strip[p] = new Vector3(points[p].X, points[p].Y, 0f);
                }

                line.widthMultiplier = zone.LineWidth;
                line.startColor = colour;
                line.endColor = colour;
                line.SetPositions(_strip);
                line.enabled = true;
            }

            for (var i = _boxes.Count; i < _lines.Count; i++)
            {
                _lines[i].enabled = false;
            }
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
        }

        private void LateUpdate() => Redraw();

        private void OnDisable()
        {
            foreach (var line in _lines)
            {
                if (line != null)
                {
                    line.enabled = false;
                }
            }
        }

        private void OnDestroy()
        {
            if (_ownMaterial != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_ownMaterial);
                }
                else
                {
                    DestroyImmediate(_ownMaterial);
                }

                _ownMaterial = null;
            }
        }

        private LineRenderer AddLine()
        {
            var go = new GameObject($"Zone {_lines.Count}");
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = false;
            line.positionCount = _strip.Length;
            line.numCornerVertices = 0;
            line.numCapVertices = 0;
            line.sortingOrder = SortingOrder;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sharedMaterial = Material;
            _lines.Add(line);
            return line;
        }

        private Material Material
        {
            get
            {
                if (lineMaterial != null)
                {
                    return lineMaterial;
                }

                if (_ownMaterial == null)
                {
                    var shader = Shader.Find(LitShader) ?? Shader.Find(UnlitShader) ?? Shader.Find("Sprites/Default");
                    _ownMaterial = new Material(shader) { name = "HitZoneLine", hideFlags = HideFlags.DontSave };
                }

                return _ownMaterial;
            }
        }
    }
}
