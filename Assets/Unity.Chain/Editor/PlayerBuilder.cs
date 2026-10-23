using System;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace MindAttic.Chain.EditorTools
{
    /// <summary>
    /// Command-line Windows build, so UNI-T16's fix (a Main scene registered in Build Settings)
    /// can be verified the same way a CI/build machine would:
    /// <c>-executeMethod MindAttic.Chain.EditorTools.PlayerBuilder.BuildWindows</c>. Output goes to
    /// the gitignored <c>Builds/</c> folder.
    /// </summary>
    public static class PlayerBuilder
    {
        private const string OutputPath = "Builds/Windows/Unity.Chain.exe";

        [MenuItem("MindAttic/Chain/Build Windows Player")]
        public static void BuildWindows()
        {
            var scenes = EditorBuildSettings.scenes;
            if (scenes.Length == 0)
            {
                throw new InvalidOperationException(
                    "No scenes in EditorBuildSettings; run SceneBuilder.BuildMainScene first.");
            }

            var scenePaths = new string[scenes.Length];
            for (var i = 0; i < scenes.Length; i++)
            {
                scenePaths[i] = scenes[i].path;
            }

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenePaths,
                locationPathName = OutputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new Exception(
                    $"Windows build failed: result={report.summary.result}, errors={report.summary.totalErrors}");
            }
        }
    }
}
