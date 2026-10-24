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
    /// </summary>
    public static class RenderPipelineBuilder
    {
        public const string Folder = "Assets/Unity.Chain/Rendering";
        public const string PipelinePath = Folder + "/ChainURP.asset";
        public const string RendererPath = Folder + "/Chain2DRenderer.asset";
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
            AssetDatabase.SaveAssets();
            return pipeline;
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
