using System.Linq;
using MindAttic.Chain.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MindAttic.Chain.Tests
{
    /// <summary>
    /// UNI-T56: Zone.vb's hit boxes (<see cref="HitZoneOverlay"/>, E) and Root.vb's F1-F3 render states
    /// (<see cref="ViewRenderFeature"/> on the game's 2D renderer).
    /// </summary>
    public class ViewRenderTests
    {
        private TestRig _rig;

        [SetUp]
        public void SetUp() => _rig = new TestRig();

        [TearDown]
        public void TearDown()
        {
            _rig.Dispose();
            ViewRenderFeature.Options = null;
        }

        private (GameHud hud, HitZoneOverlay overlay) MakeOverlay()
        {
            var hud = _rig.Track(new GameObject("Hud")).AddComponent<GameHud>();
            hud.Session = _rig.Session;
            var overlay = _rig.Track(new GameObject("HitZones")).AddComponent<HitZoneOverlay>();
            overlay.Session = _rig.Session;
            overlay.Hud = hud;
            _rig.Session.Restart(); // Root.vb's Reset spawns the flock
            return (hud, overlay);
        }

        [Test]
        public void Overlay_BoxesEveryEnemyAndTheMace_FromTheStart()
        {
            var (_, overlay) = MakeOverlay();
            var world = _rig.Session.World;
            Assert.AreEqual(14, world.Enemies.Count, "the session spawned its flock");

            overlay.Redraw();

            Assert.IsTrue(overlay.Shown, "bZone = True");
            Assert.AreEqual(world.Enemies.Count + 1, overlay.BoxCount, "one per enemy (all 14, BIBLE D23) and the mace's");

            var zones = _rig.Session.Settings.Zones;
            var mace = HitZones.Box(_rig.Player.MacePosition + zones.MaceCorner, zones.Size);
            Assert.AreEqual(mace, overlay.Boxes[0], "Zone.vb's box 0: rectMace's corner");
            var enemy = world.Enemies[3];
            Assert.AreEqual(HitZones.Box(enemy.Position + zones.EnemyCorner, zones.Size), overlay.Boxes[4]);

            // Each box is loadZone's five-point line strip, in texZone's colour, over the sprites.
            var line = overlay.Line(0);
            Assert.AreEqual(5, line.positionCount);
            var strip = HitZones.Strip(mace);
            for (var i = 0; i < strip.Length; i++)
            {
                Assert.AreEqual(strip[i].X, line.GetPosition(i).x, 1e-5f, $"point {i}");
                Assert.AreEqual(strip[i].Y, line.GetPosition(i).y, 1e-5f, $"point {i}");
            }

            Assert.AreEqual((Color)new Color32(0, 128, 255, 255), line.startColor, "event.png: 0, 128, 255");
            Assert.AreEqual(_rig.Session.Settings.Hud.Zone.LineWidth, line.widthMultiplier, 1e-6f);
            Assert.AreEqual(HitZoneOverlay.SortingOrder, line.sortingOrder);
            Assert.IsNotNull(line.sharedMaterial);
        }

        [Test]
        public void Overlay_FollowsTheEnemies_AndEHidesIt_ThroughAReset()
        {
            var (hud, overlay) = MakeOverlay();
            var world = _rig.Session.World;
            overlay.Redraw();

            var enemy = world.Enemies[0];
            _rig.Step();
            overlay.Redraw();
            Assert.AreEqual(HitZones.Box(enemy.Position + _rig.Session.Settings.Zones.EnemyCorner, _rig.Session.Settings.Zones.Size), overlay.Boxes[1], "moved to the enemy every frame");

            Assert.IsFalse(hud.ToggleZones(), "E: bZone = Not bZone");
            overlay.Redraw();
            Assert.AreEqual(0, overlay.BoxCount);

            _rig.Session.Restart();
            overlay.Redraw();
            Assert.IsFalse(hud.Options.Zones, "Root.vb's Reset never sets bZone");
            Assert.AreEqual(0, overlay.BoxCount);

            Assert.IsTrue(hud.ToggleZones());
            overlay.Redraw();
            Assert.AreEqual(world.Enemies.Count + 1, overlay.BoxCount);
        }

        [Test]
        public void Overlay_DropsTheMaceBox_WhileTheChainDoesNotFollow()
        {
            var (_, overlay) = MakeOverlay();
            Assert.IsTrue(_rig.Session.Submit(new ToggleFollow(_rig.Id)));

            overlay.Redraw();

            Assert.AreEqual(_rig.Session.World.Enemies.Count, overlay.BoxCount, "with F off the mace hits nothing (BIBLE D17)");
        }

        [Test]
        public void Feature_RedrawsOnlyWhileF1ToF3AreOffTheirStart_AndSetsItsMaterial()
        {
            var feature = ScriptableObject.CreateInstance<ViewRenderFeature>();
            try
            {
                feature.Shader = Shader.Find(ViewRenderFeature.ShaderName);
                Assert.IsNotNull(feature.Shader, "the override shader imports");
                var material = feature.Material;
                Assert.IsNotNull(material);

                var options = new ViewOptions();
                Assert.IsFalse(ViewRenderFeature.Redraws(null), "no HUD: drawn as usual");
                Assert.IsFalse(ViewRenderFeature.Redraws(options), "solid, textured, no culling: drawn as usual");

                options.Toggle(MenuEntry.Textures);
                options.Toggle(MenuEntry.Culling);
                Assert.IsTrue(ViewRenderFeature.Redraws(options));
                ViewRenderFeature.Apply(material, options);
                Assert.AreEqual(0f, material.GetFloat("_Textured"), "F2: Use Shaded");
                Assert.AreEqual((float)CullMode.Back, material.GetFloat("_Cull"), "F3: back faces culled");

                options.Reset();
                options.Toggle(MenuEntry.Wireframe);
                Assert.IsTrue(ViewRenderFeature.Redraws(options), "F1");
                ViewRenderFeature.Apply(material, options);
                Assert.AreEqual(1f, material.GetFloat("_Textured"));
                Assert.AreEqual((float)CullMode.Off, material.GetFloat("_Cull"));
            }
            finally
            {
                Object.DestroyImmediate(feature);
            }
        }

        [Test]
        public void Pipeline_The2DRendererCarriesTheFeature_WithItsShader()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<Renderer2DData>("Assets/Unity.Rea.Chain/Rendering/Chain2DRenderer.asset");
            Assert.IsNotNull(renderer);

            var features = renderer.rendererFeatures.OfType<ViewRenderFeature>().ToArray();
            Assert.AreEqual(1, features.Length, "one ViewRenderFeature on the game's renderer");
            Assert.IsTrue(features[0].isActive);
            Assert.AreEqual(ViewRenderFeature.ShaderName, features[0].Shader != null ? features[0].Shader.name : null,
                "its override shader is referenced, so it ships in the build");
        }
    }
}
