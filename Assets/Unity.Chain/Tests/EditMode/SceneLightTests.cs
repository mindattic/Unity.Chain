using System.Collections.Generic;
using MindAttic.Chain.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MindAttic.Chain.Tests
{
    public class SceneLightTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private GameSession _session;
        private SceneLight _light;
        private Light2D _light2D;

        [SetUp]
        public void SetUp()
        {
            _session = Track(new GameObject("Session")).AddComponent<GameSession>();
            var go = Track(new GameObject("SceneLight"));
            _light2D = go.AddComponent<Light2D>();
            _light2D.lightType = Light2D.LightType.Global;
            _light = go.AddComponent<SceneLight>();
            _light.Session = _session;
            _light.GlobalLight = _light2D;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _objects)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }

            _objects.Clear();
        }

        private GameObject Track(GameObject go)
        {
            _objects.Add(go);
            return go;
        }

        [Test]
        public void Day_LightsWhite()
        {
            _light.Step(1f / 60f);
            Assert.AreEqual(Color.white, _light2D.color);
        }

        [Test]
        public void Night_LightsBlue()
        {
            _light.State.SetPeriod(DayPeriod.Night);
            for (var i = 0; i < 300; i++)
            {
                _light.Step(1f / 60f);
            }

            Assert.AreEqual(new Color(0f, 0f, 1f, 1f), _light2D.color);
        }

        [Test]
        public void GameOverFade_DimsTheLight()
        {
            for (var i = 0; i < _session.State.MaxHp; i++)
            {
                _session.State.TryHurt(i);
            }

            for (var i = 0; i < 10; i++)
            {
                _session.Step(1f / 50f); // the world fades a step at a time
            }

            _light.Step(1f / 60f);
            Assert.AreEqual(_session.State.AmbientLevel, _light2D.color.r, 1e-4f);
            Assert.Less(_light2D.color.r, 1f);
        }

        [Test]
        public void ToggleLighting_LightsUnlit_AndPauseHoldsTheCycle()
        {
            _light.State.SetPeriod(DayPeriod.Dusk);
            _light.Step(1f);
            Assert.AreNotEqual(Color.white, _light2D.color, "dusk tints the light");

            Assert.IsFalse(_light.ToggleLighting());
            Assert.AreEqual(Color.white, _light2D.color, "lighting off is unlit");
            Assert.IsTrue(_light.ToggleLighting());

            var clock = _light.State.Clock;
            _session.Submit(new Pause(new PlayerId(0), true));
            _light.Step(1f);
            Assert.AreEqual(clock, _light.State.Clock, "pause skipped renderLight");
        }

        [Test]
        public void Project_RendersWithTheCommitted2DPipeline_OnEveryQualityLevel()
        {
            // UNI-T18: the pipeline the settings name exists, draws with the 2D renderer, and is the
            // one in GraphicsSettings and on every quality level (no level falls back to built-in).
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Unity.Chain/Rendering/ChainURP.asset");
            Assert.IsNotNull(pipeline, "the URP pipeline asset is committed");
            var renderers = new SerializedObject(pipeline).FindProperty("m_RendererDataList");
            Assert.IsInstanceOf<Renderer2DData>(renderers.GetArrayElementAtIndex(0).objectReferenceValue, "its renderer is the 2D renderer");
            Assert.AreEqual(pipeline, GraphicsSettings.defaultRenderPipeline);
            var current = QualitySettings.GetQualityLevel();
            try
            {
                for (var level = 0; level < QualitySettings.count; level++)
                {
                    Assert.AreEqual(pipeline, QualitySettings.GetRenderPipelineAssetAt(level), QualitySettings.names[level]);
                    // URP writes its MSAA into the active level when it renders; a level that
                    // disagrees would be rewritten (and churn in git) on every play.
                    QualitySettings.SetQualityLevel(level, false);
                    Assert.AreEqual(pipeline.msaaSampleCount > 1 ? pipeline.msaaSampleCount : 0, QualitySettings.antiAliasing, QualitySettings.names[level]);
                }
            }
            finally
            {
                QualitySettings.SetQualityLevel(current, false);
            }

            Assert.IsNotNull(GraphicsSettings.GetSettingsForRenderPipeline<UniversalRenderPipeline>(), "URP global settings are mapped");
        }

        [Test]
        public void GameOverFade_UnderThe2DPipeline_IsShownByTheLightNotTheHud()
        {
            var hud = Track(new GameObject("Hud")).AddComponent<GameHud>();
            hud.Session = _session;
            hud.SceneLight = _light;
            for (var i = 0; i < _session.State.MaxHp; i++)
            {
                _session.State.TryHurt(i);
            }

            for (var i = 0; i < 10; i++)
            {
                _session.Step(1f / 50f);
            }

            _light.Step(1f / 60f);
            Assert.IsTrue(_light.LightsTheScene, "the URP 2D light lights the scene");
            Assert.Less(_light2D.color.r, 1f, "the light shows the fade");
            Assert.AreEqual(0f, hud.GameOverDim, "so the HUD does not dim it a second time");

            _light.ToggleLighting();
            Assert.IsTrue(_light.LightsTheScene, "lighting off is unlit, and the light still shows the fade");
            Assert.AreEqual(0f, hud.GameOverDim);
        }

        [Test]
        public void InPlaceRestart_TurnsLightingBackOn()
        {
            _light.ToggleLighting();
            Assert.IsFalse(_light.State.Enabled);

            _session.Restart();

            Assert.IsTrue(_light.State.Enabled, "Root.vb Reset: bLighting = True");
        }
    }
}
