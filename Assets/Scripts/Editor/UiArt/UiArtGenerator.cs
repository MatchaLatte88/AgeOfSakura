using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AgeOfSakura.EditorTools.UiArt
{
    /// <summary>
    /// Bakes all UI sprites from <see cref="UiArtRecipes"/> into real PNG assets under Assets/Resources/UI, with the right
    /// import settings (sprite, 2x pixels-per-unit, 9-slice borders). Artists can replace any PNG later without touching code:
    /// the game loads sprites by file name. Also writes a contact sheet for quick visual review.
    /// Command line: Unity -batchmode -nographics -quit -projectPath . -executeMethod AgeOfSakura.EditorTools.UiArt.UiArtGenerator.GenerateAll
    /// </summary>
    public static class UiArtGenerator
    {
        public const string Folder = "Assets/Resources/UI";
        public const float PixelsPerUnit = 200f;
        private const string ContactSheetPath = "Builds/UiShots/art/contact_sheet.png";

        [MenuItem("Age of Sakura/Generate UI Art")]
        public static void GenerateAll()
        {
            Directory.CreateDirectory(Folder);
            var entries = new List<ArtEntry>(UiArtRecipes.All());
            var sheet = new List<KeyValuePair<string, Texture2D>>();

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var entry in entries)
                {
                    var texture = entry.Make();
                    File.WriteAllBytes($"{Folder}/{entry.Id}.png", texture.EncodeToPNG());
                    sheet.Add(new KeyValuePair<string, Texture2D>(entry.Id, texture));
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            AssetDatabase.Refresh();

            foreach (var entry in entries) ConfigureImporter(entry);
            UiFontMaterials.Generate();
            AssetDatabase.SaveAssets();

            WriteContactSheet(sheet);
            foreach (var pair in sheet) Object.DestroyImmediate(pair.Value);
            Debug.Log($"[SETUP] Generated {entries.Count} UI sprites in {Folder}; contact sheet: {ContactSheetPath}");
        }

        private static void ConfigureImporter(ArtEntry entry)
        {
            string path = $"{Folder}/{entry.Id}.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null)
            {
                Debug.LogError($"[SETUP] Could not import {path}");
                return;
            }
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.mipmapEnabled = entry.Mipmaps;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = entry.Repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 1024;
            importer.spriteBorder = entry.Border;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteExtrude = 0;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        private static void WriteContactSheet(List<KeyValuePair<string, Texture2D>> items)
        {
            const int cell = 240;
            const int columns = 8;
            int rows = (items.Count + columns - 1) / columns;
            var sheet = new Texture2D(columns * cell, rows * cell, TextureFormat.RGBA32, false);
            var bg = new Color[sheet.width * sheet.height];
            for (int y = 0; y < sheet.height; y++)
            {
                for (int x = 0; x < sheet.width; x++)
                {
                    bool darkHalf = (x / cell) % 2 == 1;
                    float checker = ((x / 16 + y / 16) % 2 == 0) ? 0f : 0.03f;
                    bg[y * sheet.width + x] = darkHalf ? new Color(0.26f + checker, 0.30f + checker, 0.34f + checker, 1f) : new Color(0.62f + checker, 0.66f + checker, 0.60f + checker, 1f);
                }
            }
            sheet.SetPixels(bg);

            for (int i = 0; i < items.Count; i++)
            {
                var tex = items[i].Value;
                int col = i % columns;
                int row = rows - 1 - i / columns;
                float scale = Mathf.Min((cell - 16f) / tex.width, (cell - 16f) / tex.height);
                int w = Mathf.Max(1, Mathf.RoundToInt(tex.width * scale));
                int h = Mathf.Max(1, Mathf.RoundToInt(tex.height * scale));
                int ox = col * cell + (cell - w) / 2;
                int oy = row * cell + (cell - h) / 2;
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        var src = tex.GetPixelBilinear((x + 0.5f) / w, (y + 0.5f) / h);
                        if (src.a <= 0f) continue;
                        var dst = sheet.GetPixel(ox + x, oy + y);
                        sheet.SetPixel(ox + x, oy + y, Color.Lerp(dst, new Color(src.r, src.g, src.b, 1f), src.a));
                    }
                }
            }
            sheet.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(ContactSheetPath));
            File.WriteAllBytes(ContactSheetPath, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
        }
    }
}
