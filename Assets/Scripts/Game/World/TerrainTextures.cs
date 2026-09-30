using AgeOfSakura.Core;
using UnityEngine;

namespace AgeOfSakura.Game
{
    /// <summary>
    /// Paints the hand-painted-looking ground texture from the logical grid (grass variation, dirt patches,
    /// muddy banks, stream bed). Generated once at startup; no imported art.
    /// </summary>
    public static class TerrainTextures
    {
        public const int PixelsPerCell = 24;

        public static Texture2D BuildGround(GridMap grid)
        {
            int w = grid.Width;
            int h = grid.Height;
            var dirt = new float[w, h];
            var water = new float[w, h];
            var forest = new float[w, h];
            for (int x = 0; x < w; x++)
            {
                for (int z = 0; z < h; z++)
                {
                    var t = grid.GetCell(x, z).Terrain;
                    dirt[x, z] = t == TerrainType.Dirt ? 1f : 0f;
                    water[x, z] = (t == TerrainType.Water || t == TerrainType.Bridge) ? 1f : 0f;
                    forest[x, z] = (t == TerrainType.Tree || t == TerrainType.Bamboo || t == TerrainType.Rock || t == TerrainType.Cherry) ? 1f : 0f;
                }
            }

            int texW = w * PixelsPerCell;
            int texH = h * PixelsPerCell;
            var pixels = new Color32[texW * texH];
            for (int py = 0; py < texH; py++)
            {
                for (int px = 0; px < texW; px++)
                {
                    float u = (px + 0.5f) / PixelsPerCell;
                    float v = (py + 0.5f) / PixelsPerCell;
                    float n = ValueNoise(u * 1.7f, v * 1.7f) * 0.6f + ValueNoise(u * 5.3f + 11f, v * 5.3f + 3f) * 0.4f;
                    float speck = ValueNoise(u * 19f + 5f, v * 19f + 9f);

                    var color = Color.Lerp(Palette.GrassDark, Palette.GrassLight, n);
                    color = Color.Lerp(color, Palette.GrassLight, Mathf.Clamp01((speck - 0.72f) * 2.2f) * 0.35f);

                    // painted grass strokes: short light and dark blades, plus sparse tiny blossoms
                    float blade = ValueNoise(u * 26f + 3f, v * 8f + 7f);
                    float shade = ValueNoise(u * 22f + 19f, v * 7f + 1f);
                    color = Color.Lerp(color, Palette.GrassLight, Smooth(0.66f, 0.92f, blade) * 0.4f);
                    color = Color.Lerp(color, Palette.GrassDark, Smooth(0.7f, 0.95f, shade) * 0.3f);
                    float dot = ValueNoise(u * 41f + 71f, v * 41f + 13f);
                    if (dot > 0.93f) color = Color.Lerp(color, (Hash(Mathf.FloorToInt(u * 41f), Mathf.FloorToInt(v * 41f)) < 0.5f) ? Palette.Hex("#fff3b0") : Palette.Hex("#ffd0e0"), 0.75f);

                    float f = Sample(forest, w, h, u, v);
                    color = Color.Lerp(color, Palette.GrassDark * 0.85f, f * 0.55f);

                    float d = Smooth(0.42f, 0.58f, Sample(dirt, w, h, u, v) + (n - 0.5f) * 0.55f);
                    color = Color.Lerp(color, Color.Lerp(Palette.Dirt, Palette.DirtDark, n), d);

                    float wv = Sample(water, w, h, u, v);
                    float bank = Smooth(0.04f, 0.4f, wv);
                    color = Color.Lerp(color, Palette.Mud, bank * 0.6f);
                    float wet = Smooth(0.44f, 0.56f, wv + (n - 0.5f) * 0.14f);
                    color = Color.Lerp(color, Color.Lerp(Palette.WaterShallow, Palette.WaterDeep, Mathf.Clamp01((wv - 0.5f) * 2f + n * 0.3f)), wet);

                    color.a = 1f;
                    pixels[py * texW + px] = color;
                }
            }

            var tex = new Texture2D(texW, texH, TextureFormat.RGBA32, true)
            {
                name = "GroundTexture",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 2
            };
            tex.SetPixels32(pixels);
            tex.Apply(true, true);
            return tex;
        }

        /// <summary>Tileable streak texture used for the animated water surface. Different seeds give layers that shimmer against each other.</summary>
        public static Texture2D BuildRipples(int seed, int size = 64)
        {
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float v = (float)y / size;
                    float n = TileableNoise(u * 4f + seed, v * 8f + seed * 0.5f, 4, 8);
                    float line = 1f - Mathf.Clamp01(Mathf.Abs(n - 0.5f) / 0.06f);
                    float a = line * 0.55f;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "RippleTexture",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            tex.SetPixels32(pixels);
            tex.Apply(true, true);
            return tex;
        }

        /// <summary>HLSL-style smoothstep (Unity's Mathf.SmoothStep is an interpolation between two values, not an edge function).</summary>
        private static float Smooth(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        private static float Sample(float[,] cells, int w, int h, float u, float v)
        {
            float fx = u - 0.5f;
            float fy = v - 0.5f;
            int x0 = Mathf.FloorToInt(fx);
            int y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0;
            float ty = fy - y0;
            float a = At(cells, w, h, x0, y0);
            float b = At(cells, w, h, x0 + 1, y0);
            float c = At(cells, w, h, x0, y0 + 1);
            float d = At(cells, w, h, x0 + 1, y0 + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        private static float At(float[,] cells, int w, int h, int x, int y) =>
            cells[Mathf.Clamp(x, 0, w - 1), Mathf.Clamp(y, 0, h - 1)];

        private static float Hash(int x, int y)
        {
            unchecked
            {
                int n = x * 374761393 + y * 668265263;
                n = (n ^ (n >> 13)) * 1274126177;
                n ^= n >> 16;
                return (n & 0x7fffffff) / (float)0x7fffffff;
            }
        }

        private static float ValueNoise(float x, float y)
        {
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            float tx = x - x0;
            float ty = y - y0;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);
            return Mathf.Lerp(
                Mathf.Lerp(Hash(x0, y0), Hash(x0 + 1, y0), tx),
                Mathf.Lerp(Hash(x0, y0 + 1), Hash(x0 + 1, y0 + 1), tx), ty);
        }

        private static float TileableNoise(float x, float y, int periodX, int periodY)
        {
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            float tx = x - x0;
            float ty = y - y0;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);
            int xa = Mod(x0, periodX), xb = Mod(x0 + 1, periodX);
            int ya = Mod(y0, periodY), yb = Mod(y0 + 1, periodY);
            return Mathf.Lerp(Mathf.Lerp(Hash(xa, ya), Hash(xb, ya), tx), Mathf.Lerp(Hash(xa, yb), Hash(xb, yb), tx), ty);
        }

        private static int Mod(int a, int m) => ((a % m) + m) % m;
    }
}
