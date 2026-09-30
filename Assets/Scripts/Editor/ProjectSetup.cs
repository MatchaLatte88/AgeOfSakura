using System.IO;
using AgeOfSakura.Game;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace AgeOfSakura.EditorTools
{
    /// <summary>
    /// One-time project configuration that cannot live in plain files: URP asset with mobile-friendly quality,
    /// landscape orientation, shaders kept in builds, Input System backend, and the Game scene.
    /// Runs automatically when the project is opened and only changes what is still unset.
    /// Also available as "Age of Sakura / Run Project Setup".
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        private const string SettingsFolder = "Assets/Settings";
        private const string RendererPath = SettingsFolder + "/AgeOfSakura_URP_Renderer.asset";
        private const string PipelinePath = SettingsFolder + "/AgeOfSakura_URP.asset";
        public const string ScenePath = "Assets/Scenes/Game.unity";

        static ProjectSetup()
        {
            EditorApplication.delayCall += () => Run(false);
        }

        [MenuItem("Age of Sakura/Run Project Setup")]
        private static void RunFromMenu() => Run(true);

        /// <summary>Command line: Unity -batchmode -quit -executeMethod AgeOfSakura.EditorTools.ProjectSetup.RunBatch</summary>
        public static void RunBatch()
        {
            Run(true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void Run(bool verbose)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;

            bool changed = false;
            changed |= EnsurePlayerSettings();
            changed |= EnsureRenderPipeline();
            changed |= EnsureShadowRange();
            changed |= RemoveAlwaysIncludedShaders();
            changed |= EnsureShaderVariantMaterials();
            changed |= EnsureInputBackend();
            changed |= EnsureScene();

            if (changed)
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[SETUP] Age of Sakura project setup applied.");
            }
            else if (verbose)
            {
                Debug.Log("[SETUP] Project already configured.");
            }
        }

        // ------------------------------------------------------------------ player settings

        private static bool EnsurePlayerSettings()
        {
            bool changed = false;
            if (PlayerSettings.defaultInterfaceOrientation != UIOrientation.AutoRotation
                || PlayerSettings.allowedAutorotateToPortrait
                || PlayerSettings.allowedAutorotateToPortraitUpsideDown
                || !PlayerSettings.allowedAutorotateToLandscapeLeft
                || !PlayerSettings.allowedAutorotateToLandscapeRight)
            {
                // Landscape only (both landscape sides). Auto-rotation is limited to those two.
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
                PlayerSettings.allowedAutorotateToPortrait = false;
                PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
                PlayerSettings.allowedAutorotateToLandscapeLeft = true;
                PlayerSettings.allowedAutorotateToLandscapeRight = true;
                changed = true;
            }

            if (PlayerSettings.companyName == "DefaultCompany")
            {
                PlayerSettings.companyName = "AgeOfSakura";
                PlayerSettings.productName = "Age of Sakura";
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.ageofsakura.prototype");
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.ageofsakura.prototype");
                changed = true;
            }
            return changed;
        }

        // ------------------------------------------------------------------ URP

        private static bool EnsureRenderPipeline()
        {
            if (GraphicsSettings.defaultRenderPipeline != null) return false;

            var existing = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (existing == null)
            {
                if (!AssetDatabase.IsValidFolder(SettingsFolder)) AssetDatabase.CreateFolder("Assets", "Settings");

                var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, RendererPath);
                existing = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(existing, PipelinePath);
                TuneForMobile(existing);
                EditorUtility.SetDirty(existing);
            }

            GraphicsSettings.defaultRenderPipeline = existing;
            int previous = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = existing;
            }
            QualitySettings.SetQualityLevel(previous, false);
            Debug.Log("[SETUP] Created and assigned the URP asset " + PipelinePath);
            return true;
        }

        /// <summary>
        /// The fixed camera sits 80 units from the ground and shadow distance is measured from the camera, so the former 40 units meant
        /// that no shadow was ever drawn. The style's soft sun shadow needs the whole valley inside the range; one cascade, 2048 map.
        /// </summary>
        private static bool EnsureShadowRange()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (asset == null) return false;
            var so = new SerializedObject(asset);
            var distance = so.FindProperty("m_ShadowDistance");
            var resolution = so.FindProperty("m_MainLightShadowmapResolution");
            if (distance == null || resolution == null) return false;
            if (distance.floatValue >= 105f && resolution.intValue >= 2048) return false;
            distance.floatValue = Mathf.Max(distance.floatValue, 105f);
            resolution.intValue = Mathf.Max(resolution.intValue, 2048);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            Debug.Log("[SETUP] Shadow distance raised to 105 and shadow map to 2048 so the sun shadow is visible from the fixed camera.");
            return true;
        }

        /// <summary>Mobile defaults: no HDR, no MSAA, one shadow cascade, short shadow distance, moderate shadow map.</summary>
        private static void TuneForMobile(UniversalRenderPipelineAsset asset)
        {
            var so = new SerializedObject(asset);
            SetInt(so, "m_MSAA", 1);                       // MSAA disabled
            SetBool(so, "m_SupportsHDR", false);
            SetFloat(so, "m_ShadowDistance", 40f);
            SetInt(so, "m_ShadowCascadeCount", 1);
            SetInt(so, "m_MainLightShadowmapResolution", 1024);
            SetBool(so, "m_SoftShadowsSupported", true);
            SetBool(so, "m_SupportsCameraDepthTexture", false);
            SetBool(so, "m_SupportsCameraOpaqueTexture", false);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetInt(SerializedObject so, string name, int value) { var p = Find(so, name); if (p != null) p.intValue = value; }
        private static void SetBool(SerializedObject so, string name, bool value) { var p = Find(so, name); if (p != null) p.boolValue = value; }
        private static void SetFloat(SerializedObject so, string name, float value) { var p = Find(so, name); if (p != null) p.floatValue = value; }

        private static SerializedProperty Find(SerializedObject so, string name)
        {
            var p = so.FindProperty(name);
            if (p == null) Debug.LogWarning($"[SETUP] URP asset has no property '{name}' in this URP version; adjust it manually in {PipelinePath}.");
            return p;
        }

        // ------------------------------------------------------------------ shaders

        private const string VariantFolder = "Assets/Resources/ShaderVariants";

        /// <summary>
        /// The game creates its materials at runtime (Shader.Find), so builds must keep those shaders. Listing them under
        /// "Always Included Shaders" makes Unity compile EVERY shader_feature combination (hundreds of thousands of variants,
        /// hours of build time). Instead we keep one small template material per keyword combination the game really uses;
        /// Unity then builds only those variants. If GameArt starts using a new keyword combination, add a template here.
        /// </summary>
        private static bool EnsureShaderVariantMaterials()
        {
            bool changed = false;
            changed |= EnsureTemplate("Lit_Opaque", "Universal Render Pipeline/Lit", false, "_SPECULARHIGHLIGHTS_OFF", "_ENVIRONMENTREFLECTIONS_OFF");
            changed |= EnsureTemplate("Lit_Transparent", "Universal Render Pipeline/Lit", true);
            changed |= EnsureTemplate("Unlit_Opaque", "Universal Render Pipeline/Unlit", false);
            changed |= EnsureTemplate("Unlit_Transparent", "Universal Render Pipeline/Unlit", true);
            changed |= EnsureTemplate("Particles_Transparent", "Universal Render Pipeline/Particles/Unlit", true);
            return changed;
        }

        private static bool EnsureTemplate(string name, string shaderName, bool transparent, params string[] keywords)
        {
            string path = VariantFolder + "/" + name + ".mat";
            if (File.Exists(path)) return false;
            var shader = Shader.Find(shaderName);
            if (shader == null) return false; // URP not resolved yet; runs again after the next reload

            Directory.CreateDirectory(VariantFolder);
            var material = new Material(shader) { name = name };
            foreach (string keyword in keywords) material.EnableKeyword(keyword);
            if (transparent) GameArt.MakeTransparent(material);
            AssetDatabase.CreateAsset(material, path);
            return true;
        }

        /// <summary>Removes the entries earlier setup versions added (see EnsureShaderVariantMaterials for why).</summary>
        private static bool RemoveAlwaysIncludedShaders()
        {
            var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (asset == null || asset.Length == 0) return false;
            var so = new SerializedObject(asset[0]);
            var list = so.FindProperty("m_AlwaysIncludedShaders");
            if (list == null) return false;

            bool changed = false;
            for (int i = list.arraySize - 1; i >= 0; i--)
            {
                var shader = list.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
                if (shader == null || System.Array.IndexOf(GameArt.RequiredShaderNames, shader.name) < 0) continue;
                list.GetArrayElementAtIndex(i).objectReferenceValue = null; // Unity needs the slot cleared before deletion
                list.DeleteArrayElementAtIndex(i);
                changed = true;
            }
            if (changed) so.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }

        // ------------------------------------------------------------------ input

        private static bool EnsureInputBackend()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0) return false;
            var so = new SerializedObject(assets[0]);
            var handler = so.FindProperty("activeInputHandler");
            if (handler == null || handler.intValue != 0) return false;

            // 0 = old Input Manager only, 1 = Input System only, 2 = both.
            handler.intValue = 2;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.LogWarning("[SETUP] Enabled the Input System backend. Restart the Unity Editor once for it to take effect.");
            return true;
        }

        // ------------------------------------------------------------------ scene

        private static bool EnsureScene()
        {
            if (File.Exists(ScenePath))
            {
                EnsureSceneInBuild();
                return false;
            }

            var active = SceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(active.path) || active.isDirty)
            {
                // The user is working in another scene: do not touch it. The game still starts in any scene (GameBootstrap).
                Debug.LogWarning("[SETUP] Skipped creating " + ScenePath + " because another scene is open; run 'Age of Sakura > Run Project Setup' from an empty scene.");
                return false;
            }

            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            EnsureSceneInBuild();
            Debug.Log("[SETUP] Created " + ScenePath);
            return true;
        }

        private static void EnsureSceneInBuild()
        {
            var scenes = EditorBuildSettings.scenes;
            foreach (var s in scenes) if (s.path == ScenePath) return;
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(scenes) { new EditorBuildSettingsScene(ScenePath, true) };
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
