using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace AgeOfSakura.EditorTools
{
    /// <summary>Imports the TextMeshPro essential resources (default font asset, shaders, settings) exactly once.</summary>
    public static class TmpSetup
    {
        public const string SettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        [MenuItem("Age of Sakura/Import TextMeshPro Essentials")]
        public static void ImportEssentials()
        {
            if (File.Exists(SettingsPath))
            {
                Debug.Log("[SETUP] TextMeshPro essentials already imported.");
                return;
            }
            TMP_PackageResourceImporter.ImportResources(true, false, false);
        }

        /// <summary>
        /// Command line (do NOT pass -quit; the import finishes asynchronously):
        /// Unity -batchmode -nographics -projectPath . -executeMethod AgeOfSakura.EditorTools.TmpSetup.ImportAndExit
        /// </summary>
        public static void ImportAndExit()
        {
            ImportEssentials();
            double started = EditorApplication.timeSinceStartup;
            EditorApplication.update += () =>
            {
                if (File.Exists(SettingsPath) && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
                {
                    AssetDatabase.SaveAssets();
                    EditorApplication.Exit(0);
                }
                else if (EditorApplication.timeSinceStartup - started > 240)
                {
                    Debug.LogError("[SETUP] TextMeshPro import did not finish in time.");
                    EditorApplication.Exit(1);
                }
            };
        }
    }
}
