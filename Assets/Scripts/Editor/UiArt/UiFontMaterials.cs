using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace AgeOfSakura.EditorTools.UiArt
{
    /// <summary>
    /// Bakes the TextMeshPro material presets used by the UI (letterpress, outlined, chunky numbers, light-on-dark) as assets.
    /// They must exist as assets (not be created at runtime) so the shader variants with OUTLINE_ON / UNDERLAY_ON are kept in builds.
    /// </summary>
    public static class UiFontMaterials
    {
        public const string Folder = "Assets/Resources/UI/Fonts";
        private const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        public static void Generate()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null)
            {
                Debug.LogError("[SETUP] TMP LiberationSans SDF not found; run 'Age of Sakura > Import TextMeshPro Essentials' first.");
                return;
            }
            ShaderUtilities.GetShaderPropertyIDs();
            Directory.CreateDirectory(Folder);

            // dark text on paper: subtle light "letterpress" edge below the glyphs
            Make("UI_Plain", font, m =>
            {
                Embolden(m, 0.06f);
                Underlay(m, new Color(1f, 0.97f, 0.88f, 0.75f), 0f, -0.5f, 0f, 0.05f);
            });
            // cream text on coloured buttons and over the world: dark outline plus soft drop shadow
            Make("UI_Outlined", font, m =>
            {
                Embolden(m, 0.16f);
                Outline(m, Pal.Outline, 0.34f);
                Underlay(m, new Color(0.08f, 0.04f, 0.02f, 0.5f), 0f, -0.8f, 0.2f, 0.35f);
            });
            // currency numbers and pop-ups: chunkier outline
            Make("UI_Number", font, m =>
            {
                Embolden(m, 0.2f);
                Outline(m, Pal.Outline, 0.4f);
                Underlay(m, new Color(0.08f, 0.04f, 0.02f, 0.55f), 0f, -0.9f, 0.25f, 0.4f);
            });
            // light text on dark lacquer: soft shadow only
            Make("UI_Light", font, m =>
            {
                Embolden(m, 0.1f);
                Underlay(m, new Color(0f, 0f, 0f, 0.6f), 0f, -0.7f, 0.1f, 0.35f);
            });
            AssetDatabase.SaveAssets();
        }

        private static void Make(string name, TMP_FontAsset font, Action<Material> setup)
        {
            string path = $"{Folder}/{name}.mat";
            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
            var material = new Material(font.material) { name = name };
            setup(material);
            AssetDatabase.CreateAsset(material, path);
        }

        /// <summary>Thickens the glyph face (the default font looks thin at game-UI sizes).</summary>
        private static void Embolden(Material m, float dilate) => m.SetFloat(ShaderUtilities.ID_FaceDilate, dilate);

        private static void Outline(Material m, Color color, float width)
        {
            m.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
            m.SetFloat(ShaderUtilities.ID_OutlineSoftness, 0.05f);
            m.SetColor(ShaderUtilities.ID_OutlineColor, color);
            m.EnableKeyword(ShaderUtilities.Keyword_Outline);
        }

        private static void Underlay(Material m, Color color, float offsetX, float offsetY, float dilate, float softness)
        {
            m.SetColor(ShaderUtilities.ID_UnderlayColor, color);
            m.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, offsetX);
            m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, offsetY);
            m.SetFloat(ShaderUtilities.ID_UnderlayDilate, dilate);
            m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, softness);
            m.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        }
    }
}
