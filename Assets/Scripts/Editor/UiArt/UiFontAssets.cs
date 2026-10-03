using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace AgeOfSakura.EditorTools.UiArt
{
    /// <summary>
    /// Bakes the TextMeshPro font assets of the UI from the Nunito TTFs in Assets/Fonts (SemiBold for running text, ExtraBold for
    /// bold; Latin subset of the SIL OFL font, see NUNITO-LICENSE.txt). The atlases are static: every glyph the game's English and
    /// German texts need is rendered here, nothing is generated at runtime and the TTFs (outside Resources) do not ship in the build.
    /// </summary>
    public static class UiFontAssets
    {
        public const string SourceFolder = "Assets/Fonts";
        public const string Folder = "Assets/Resources/UI/Fonts";
        private static readonly string[] Faces = { "Nunito-SemiBold", "Nunito-ExtraBold" };

        public static void Generate()
        {
            ShaderUtilities.GetShaderPropertyIDs();
            Directory.CreateDirectory(Folder);
            foreach (var face in Faces) Bake(face);
            AssetDatabase.SaveAssets();
        }

        private static void Bake(string face)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>($"{SourceFolder}/{face}.ttf");
            if (font == null)
            {
                Debug.LogError($"[SETUP] {SourceFolder}/{face}.ttf not found (or not imported yet).");
                return;
            }

            string path = $"{Folder}/{face} SDF.asset";
            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);

            var asset = TMP_FontAsset.CreateFontAsset(font, 48, 5, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            asset.name = face + " SDF";
            AssetDatabase.CreateAsset(asset, path);
            asset.material.name = asset.name + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            for (int i = 0; i < asset.atlasTextures.Length; i++)
            {
                asset.atlasTextures[i].name = $"{asset.name} Atlas {i}";
                AssetDatabase.AddObjectToAsset(asset.atlasTextures[i], asset);
            }

            if (!asset.TryAddCharacters(Charset(), out string missing))
                Debug.LogError($"[SETUP] {face}: glyphs missing from the font or the atlas: '{missing}'");

            // static: the glyphs are baked, the source font is not needed (nor shipped) at runtime
            asset.atlasPopulationMode = AtlasPopulationMode.Static;
            EditorUtility.SetDirty(asset);
            Debug.Log($"[SETUP] {asset.name}: {asset.characterTable.Count} glyphs in {asset.atlasTextures.Length} atlas texture(s)");
        }

        /// <summary>Basic Latin, Latin-1 (German umlauts, ß, accents) and the typographic punctuation the texts use.</summary>
        private static string Charset()
        {
            var sb = new StringBuilder();
            for (int c = 0x20; c < 0x7F; c++) sb.Append((char)c);
            for (int c = 0xA0; c <= 0xFF; c++) sb.Append((char)c);
            sb.Append("–—‘’“”•…€");
            return sb.ToString();
        }
    }
}
