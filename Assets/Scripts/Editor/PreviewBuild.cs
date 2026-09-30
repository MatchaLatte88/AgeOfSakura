using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AgeOfSakura.EditorTools
{
    /// <summary>
    /// Fast Windows development build used only for UI review with the UI showcase (see UiShowcase.cs).
    /// Uses the Mono backend so it builds in about a minute and needs no C++ toolchain.
    /// Command line: Unity -batchmode -nographics -quit -buildTarget StandaloneWindows64 -executeMethod AgeOfSakura.EditorTools.PreviewBuild.BuildWindowsPreview
    /// </summary>
    public static class PreviewBuild
    {
        public const string OutputPath = "Builds/Preview/AgeOfSakuraPreview.exe";

        [MenuItem("Age of Sakura/Build Windows UI Preview")]
        public static void BuildWindowsPreview()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            var target = NamedBuildTarget.Standalone;
            var previous = PlayerSettings.GetScriptingBackend(target);
            PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.Mono2x);
            try
            {
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ProjectSetup.ScenePath },
                    locationPathName = OutputPath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development
                };
                var report = BuildPipeline.BuildPlayer(options);
                Debug.Log($"[BUILD] Windows preview: {report.summary.result}, {report.summary.totalErrors} error(s)");
                if (report.summary.result != BuildResult.Succeeded) throw new Exception("Windows preview build failed, see the log above.");
            }
            finally
            {
                PlayerSettings.SetScriptingBackend(target, previous);
            }
        }
    }
}
