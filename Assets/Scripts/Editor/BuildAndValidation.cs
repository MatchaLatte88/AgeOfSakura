using System;
using System.IO;
using AgeOfSakura.Core;
using AgeOfSakura.Game;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AgeOfSakura.EditorTools
{
    /// <summary>Menu items and CLI entry points for building and validating data.</summary>
    public static class BuildScripts
    {
        private const string DefinitionsPath = "Assets/Resources/Definitions/game_definitions.json";

        /// <summary>Command line: Unity -batchmode -quit -executeMethod AgeOfSakura.EditorTools.BuildScripts.BuildAndroidDevelopment</summary>
        [MenuItem("Age of Sakura/Build Android Development APK")]
        public static void BuildAndroidDevelopment()
        {
            ValidateDefinitions(throwOnError: true);
            Directory.CreateDirectory("Builds");
            EditorUserBuildSettings.buildAppBundle = false;

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ProjectSetup.ScenePath },
                locationPathName = "Builds/AgeOfSakura-dev.apk",
                target = BuildTarget.Android,
                options = BuildOptions.Development
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[BUILD] Android development build: {report.summary.result}, {report.summary.totalSize / (1024 * 1024)} MB, {report.summary.totalErrors} error(s)");
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Android build failed, see the log above.");
        }

        /// <summary>Command line: ... -executeMethod AgeOfSakura.EditorTools.BuildScripts.BuildAndroidRelease (AAB for Google Play).</summary>
        [MenuItem("Age of Sakura/Build Android Release AAB")]
        public static void BuildAndroidRelease()
        {
            ValidateDefinitions(throwOnError: true);
            Directory.CreateDirectory("Builds");
            EditorUserBuildSettings.buildAppBundle = true;
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ProjectSetup.ScenePath },
                locationPathName = "Builds/AgeOfSakura.aab",
                target = BuildTarget.Android,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[BUILD] Android release build: {report.summary.result}");
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Android build failed, see the log above.");
        }

        [MenuItem("Age of Sakura/Validate Definitions")]
        private static void ValidateFromMenu() => ValidateDefinitions(throwOnError: false);

        public static bool ValidateDefinitions(bool throwOnError)
        {
            string json = File.ReadAllText(DefinitionsPath);
            try
            {
                DefinitionLoader.LoadValidated(json, id => Array.IndexOf(ProceduralBuildingModels.KnownVisualIds, id) >= 0);
                Debug.Log("[SETUP] game_definitions.json is valid.");
                return true;
            }
            catch (DefinitionException e)
            {
                Debug.LogError("[SETUP] " + e.Message);
                if (throwOnError) throw;
                return false;
            }
        }
    }

    /// <summary>Re-validates the definitions whenever the JSON is (re)imported, so mistakes show up immediately in the editor.</summary>
    public sealed class DefinitionsPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (var path in imported)
            {
                if (path.EndsWith("Definitions/game_definitions.json", StringComparison.OrdinalIgnoreCase))
                {
                    BuildScripts.ValidateDefinitions(throwOnError: false);
                    return;
                }
            }
        }
    }
}
