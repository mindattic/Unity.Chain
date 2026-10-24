using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MindAttic.Chain.EditorTools
{
    /// <summary>
    /// Builds the game's URP 2D pipeline (UNI-T18): the URP package was installed and GraphicsSettings
    /// and every quality level pointed at a pipeline asset that was never committed, so the game drew
    /// with the built-in pipeline and the 2D global light (<see cref="SceneLight"/>) lit nothing.
    /// This creates <see cref="PipelinePath"/> with its 2D renderer (<see cref="RendererPath"/>) and
    /// assigns it as the default pipeline and on every quality level. It only creates what is missing
    /// and only assigns what differs, so rerunning it (and every import after it) leaves the committed
    /// assets and settings untouched. Run from the menu or in batch mode via
    /// <c>-executeMethod MindAttic.Chain.EditorTools.RenderPipelineBuilder.BuildPipeline</c>;
    /// <see cref="SceneBuilder"/> runs it first so the scene's sprites take the 2D renderer's lit material.
    /// It also puts <see cref="ViewRenderFeature"/> (UNI-T56: F1-F3's wireframe, untextured and culled drawing) on the
    /// 2D renderer, with its override shader (<see cref="ViewShaderPath"/>) so the shader ships in the build.
    /// </summary>
    public static class RenderPipelineBuilder
    {
        public const string Folder = "Assets/Unity.Chain/Rendering";
        public const string PipelinePath = Folder + "/ChainURP.asset";
        public const string RendererPath = Folder + "/Chain2DRenderer.asset";
        public const string ViewShaderPath = Folder + "/ViewOptions.shader";
        private const string PostProcessDataPath = "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";

        [MenuItem("MindAttic/Chain/Build Render Pipeline")]
        public static void BuildPipeline() => Ensure();

        /// <summary>Creates the pipeline and renderer if missing and makes them the active pipeline everywhere.</summary>
        public static UniversalRenderPipelineAsset Ensure()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = CreatePipeline();
            }

            if (GraphicsSettings.defaultRenderPipeline != pipeline)
            {
                GraphicsSettings.defaultRenderPipeline = pipeline;
            }

            // URP writes its own MSAA count into the active quality level whenever it renders, so a
            // level whose antiAliasing differs is rewritten by every play or render; matching them
            // here keeps QualitySettings stable (the pipeline's 1 sample is antiAliasing 0).
            var antiAliasing = pipeline.msaaSampleCount > 1 ? pipeline.msaaSampleCount : 0;
            var current = QualitySettings.GetQualityLevel();
            for (var level = 0; level < QualitySettings.count; level++)
            {
                // QualitySettings only exposes the active level for writing.
                QualitySettings.SetQualityLevel(level, false);
                if (QualitySettings.renderPipeline != pipeline)
                {
                    QualitySettings.renderPipeline = pipeline;
                }

                if (QualitySettings.antiAliasing != antiAliasing)
                {
                    QualitySettings.antiAliasing = antiAliasing;
                }
            }

            QualitySettings.SetQualityLevel(current, false);
            EnsureViewFeature(AssetDatabase.LoadAssetAtPath<Renderer2DData>(RendererPath));
            AssetDatabase.SaveAssets();
            return pipeline;
        }

        /// <summary>
        /// Adds <see cref="ViewRenderFeature"/> to <paramref name="renderer"/> as a sub-asset if it has none, and points it
        /// at the override shader. Returns the feature (null without a renderer).
        /// </summary>
        public static ViewRenderFeature EnsureViewFeature(Renderer2DData renderer)
        {
            if (renderer == null)
            {
                return null;
            }

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ViewShaderPath);
            var feature = renderer.rendererFeatures.Find(f => f is ViewRenderFeature) as ViewRenderFeature;
            if (feature == null)
            {
                // As the renderer's own "Add Renderer Feature" does: a sub-asset, listed, with its local id in the map.
                feature = ScriptableObject.CreateInstance<ViewRenderFeature>();
                feature.name = nameof(ViewRenderFeature);
                feature.Shader = shader;
                AssetDatabase.AddObjectToAsset(feature, renderer);
                renderer.rendererFeatures.Add(feature);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);

                var serialized = new SerializedObject(renderer);
                var map = serialized.FindProperty("m_RendererFeatureMap");
                map.arraySize = renderer.rendererFeatures.Count;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                renderer.SetDirty();
                EditorUtility.SetDirty(renderer);
            }
            else if (feature.Shader != shader)
            {
                feature.Shader = shader;
                EditorUtility.SetDirty(feature);
            }

            return feature;
        }

        private static UniversalRenderPipelineAsset CreatePipeline()
        {
            Directory.CreateDirectory(Folder);

            var renderer = AssetDatabase.LoadAssetAtPath<Renderer2DData>(RendererPath);
            if (renderer == null)
            {
                // As URP's own "2D Renderer" create menu does: the default post-process data, then
                // the package's shaders and materials filled into every null resource slot.
                renderer = ScriptableObject.CreateInstance<Renderer2DData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
                var serialized = new SerializedObject(renderer);
                serialized.FindProperty("m_PostProcessData").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<PostProcessData>(PostProcessDataPath);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                ResourceReloader.ReloadAllNullIn(renderer, UniversalRenderPipelineAsset.packagePath);
                EditorUtility.SetDirty(renderer);
            }

            var pipeline = UniversalRenderPipelineAsset.Create(renderer);
            AssetDatabase.CreateAsset(pipeline, PipelinePath);
            AssetDatabase.SaveAssets();
            return pipeline;
        }
    }
}
