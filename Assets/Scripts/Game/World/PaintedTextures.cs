using System;
using System.Collections.Generic;
using UnityEngine;

namespace AgeOfSakura.Game
{
    public enum PaintedTexture
    {
        Bark,
        BirchBark,
        Plaster,
        Thatch,
        Stone,
        Earth,
        Door,
        Roof,
        Planks,
        LeafCard,
        LogEnd,
        Shoji
    }

    /// <summary>
    /// "Canvas-gemalte" procedural textures of the style (bark, plaster, thatch, stone, earth, door, roof tiles, planks, leaf cards).
    /// They are soft luminance maps around 0.7..1.0, tileable, and get their colour from the material, so one texture serves every tint.
    /// Nothing is imported: the textures are painted in code at startup (a few milliseconds).
    /// </summary>
    public static class PaintedTextures
    {
        private const int Size = 128;
        private static readonly Dictionary<PaintedTexture, Texture2D> Cache = new Dictionary<PaintedTexture, Texture2D>();

        public static Texture2D Get(PaintedTexture kind)
        {
            if (Cache.TryGetValue(kind, out var t) && t != null) return t;
            t = Paint(kind);
            Cache[kind] = t;
            return t;
        }

        /// <summary>Texture tiles per world unit that looks right for each kind (used by GameArt).</summary>
        public static float DefaultScale(PaintedTexture kind)
        {
            switch (kind)
            {
                case PaintedTexture.Bark: return 2.2f;
                case PaintedTexture.BirchBark: return 2.4f;
                case PaintedTexture.Plaster: return 1.4f;
                case PaintedTexture.Thatch: return 1.6f;
                case PaintedTexture.Stone: return 1.6f;
                case PaintedTexture.Earth: return 2.0f;
                case PaintedTexture.Door: return 2.4f;
                case PaintedTexture.Roof: return 1.8f;
                case PaintedTexture.Planks: return 2.0f;
                case PaintedTexture.LogEnd: return 6.0f;
                case PaintedTexture.Shoji: return 6.5f;
                default: return 1f;
            }
        }

        private static Texture2D Paint(PaintedTexture kind)
        {
            int size = Size;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    float v = (y + 0.5f) / size;
                    Color c = Sample(kind, u, v);
                    pixels[y * size + x] = c;
                }
            }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "Painted_" + kind,
                wrapMode = kind == PaintedTexture.LeafCard ? TextureWrapMode.Clamp : TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 1
            };
            tex.SetPixels32(pixels);
            tex.Apply(true, true);
            return tex;
        }

        private static Color Sample(PaintedTexture kind, float u, float v)
        {
            switch (kind)
            {
                case PaintedTexture.Bark: return Bark(u, v, false);
                case PaintedTexture.BirchBark: return Bark(u, v, true);
                case PaintedTexture.Plaster: return Plaster(u, v);
                case PaintedTexture.Thatch: return Thatch(u, v);
                case PaintedTexture.Stone: return Stone(u, v);
                case PaintedTexture.Earth: return Earth(u, v);
                case PaintedTexture.Door: return Door(u, v);
                case PaintedTexture.Roof: return RoofTile(u, v);
                case PaintedTexture.Planks: return Planks(u, v);
                case PaintedTexture.LogEnd: return LogEnd(u, v);
                case PaintedTexture.Shoji: return Shoji(u, v);
                default: return LeafCard(u, v);
            }
        }

        private static Color Grey(float g, float a = 1f) => new Color(g, g, g, a);

        // ------------------------------------------------------------------ painters

        private static Color Bark(float u, float v, bool birch)
        {
            float streak = Noise(u * 9f, v * 2f, 9, 2);
            float fine = Noise(u * 26f, v * 5f, 26, 5);
            float g = 0.82f + (streak - 0.5f) * 0.3f + (fine - 0.5f) * 0.12f;
            float crack = 1f - Mathf.Clamp01(Mathf.Abs(Noise(u * 7f + 3f, v * 1.5f, 7, 1) - 0.5f) / 0.045f);
            g -= crack * 0.3f;
            if (birch)
            {
                // pale trunk with dark horizontal bark scars
                float scar = Noise(u * 5f, v * 14f, 5, 14);
                g = 0.92f - Mathf.Clamp01((scar - 0.66f) * 5f) * 0.55f + (fine - 0.5f) * 0.06f;
            }
            return Grey(Mathf.Clamp01(g));
        }

        private static Color Plaster(float u, float v)
        {
            float blotch = Noise(u * 5f, v * 5f, 5, 5);
            float grain = Noise(u * 34f, v * 34f, 34, 34);
            float g = 0.93f + (blotch - 0.5f) * 0.12f + (grain - 0.5f) * 0.06f;
            float speck = Noise(u * 21f + 7f, v * 21f + 2f, 21, 21);
            if (speck > 0.9f) g -= 0.1f;
            return Grey(g);
        }

        private static Color Thatch(float u, float v)
        {
            // long straw strands with bundled courses
            float strand = Noise(u * 46f, v * 5f, 46, 5);
            float g = 0.78f + strand * 0.26f;
            float course = v * 7f;
            float edge = course - Mathf.Floor(course);
            g -= Mathf.Clamp01(1f - edge / 0.09f) * 0.16f;
            g += Mathf.Clamp01(edge - 0.85f) * 0.12f;
            return Grey(Mathf.Clamp01(g));
        }

        private static Color Stone(float u, float v)
        {
            // rough blocks: rows of unequal stones with mortar lines
            const int rows = 4;
            float rv = v * rows;
            int row = Mathf.FloorToInt(rv);
            float fy = rv - row;
            float offset = Hash(row, 5) * 0.5f;
            float cols = 3f;
            float cu = (u + offset) * cols;
            int col = Mathf.FloorToInt(cu);
            float fx = cu - col;
            float shade = 0.82f + Hash(row * 7 + 3, Mathf.FloorToInt(cu) % 3 + 11) * 0.22f;
            float mortar = Mathf.Min(Mathf.Min(fx, 1f - fx) * 0.45f, Mathf.Min(fy, 1f - fy));
            float line = 1f - Mathf.Clamp01(mortar / 0.07f);
            float g = shade + (Noise(u * 22f, v * 22f, 22, 22) - 0.5f) * 0.1f - line * 0.3f;
            // a soft lit top edge on every stone keeps the painted look
            g += Mathf.Clamp01(1f - fy / 0.25f) * 0.04f;
            return Grey(Mathf.Clamp01(g));
        }

        private static Color Earth(float u, float v)
        {
            float a = Noise(u * 8f, v * 8f, 8, 8);
            float b = Noise(u * 30f, v * 30f, 30, 30);
            float g = 0.82f + (a - 0.5f) * 0.22f + (b - 0.5f) * 0.14f;
            if (b > 0.86f) g += 0.1f;
            return Grey(g);
        }

        private static Color Door(float u, float v)
        {
            // vertical planks, a dark gap between them and two cross braces
            float p = u * 4f;
            float fx = p - Mathf.Floor(p);
            float gap = 1f - Mathf.Clamp01(Mathf.Min(fx, 1f - fx) / 0.05f);
            float grain = Noise(u * 12f, v * 38f, 12, 38);
            float g = 0.84f + (grain - 0.5f) * 0.18f - gap * 0.35f;
            float brace = Mathf.Abs(v - 0.25f) < 0.05f || Mathf.Abs(v - 0.75f) < 0.05f ? -0.12f : 0f;
            return Grey(g + brace);
        }

        private static Color RoofTile(float u, float v)
        {
            // overlapping tiles, each row shifted by half a tile
            const float rows = 6f;
            float rv = v * rows;
            int row = Mathf.FloorToInt(rv);
            float fy = rv - row;
            float cu = u * 6f + (row % 2) * 0.5f;
            int col = Mathf.FloorToInt(cu);
            float fx = cu - col;
            float shade = 0.84f + Hash(row * 13 + col % 6, 3) * 0.16f;
            float curve = 1f - Mathf.Abs(fx - 0.5f) * 0.7f;
            float g = shade * (0.9f + curve * 0.1f);
            g -= Mathf.Clamp01(1f - fy / 0.12f) * 0.22f;          // shadow under the tile above
            g -= Mathf.Clamp01(1f - Mathf.Min(fx, 1f - fx) / 0.04f) * 0.14f;
            g += Mathf.Clamp01((fy - 0.75f) / 0.25f) * 0.06f;
            return Grey(Mathf.Clamp01(g));
        }

        private static Color Planks(float u, float v)
        {
            float p = u * 3f;
            float fx = p - Mathf.Floor(p);
            int plank = Mathf.FloorToInt(p);
            float gap = 1f - Mathf.Clamp01(Mathf.Min(fx, 1f - fx) / 0.04f);
            float grain = Noise(u * 10f + plank * 3f, v * 30f, 10, 30);
            float g = 0.82f + Hash(plank, 17) * 0.12f + (grain - 0.5f) * 0.16f - gap * 0.28f;
            return Grey(Mathf.Clamp01(g));
        }

        /// <summary>
        /// Growth rings around the lattice corner (0,0): a projected texture has its origin at the object's origin, so a cut disc
        /// modelled around its own origin gets concentric rings centred on it.
        /// </summary>
        private static Color LogEnd(float u, float v)
        {
            float dx = Mathf.Min(u, 1f - u), dy = Mathf.Min(v, 1f - v);
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            float wobble = (Noise(u * 6f, v * 6f, 6, 6) - 0.5f) * 0.05f;
            float ring = Mathf.Sin((r + wobble) * 62f) * 0.5f + 0.5f;
            float g = 0.9f - ring * 0.16f;
            if (r < 0.04f) g -= 0.22f;                                   // pith
            g -= Mathf.Clamp01(1f - Mathf.Abs(Mathf.Atan2(dy, dx) - 0.6f) / 0.05f) * Mathf.Clamp01(r * 3f) * 0.2f; // one radial crack
            return Grey(Mathf.Clamp01(g));
        }

        /// <summary>Paper screen: pale paper cells divided by a thin wooden lattice (one cell per tile).</summary>
        private static Color Shoji(float u, float v)
        {
            float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
            float lattice = 1f - Mathf.Clamp01(edge / 0.07f);
            float paper = 0.97f + (Noise(u * 16f, v * 16f, 16, 16) - 0.5f) * 0.05f;
            return Grey(Mathf.Lerp(paper, 0.42f, lattice));
        }

        private static readonly Vector3[] Leaves =
        {
            new Vector3(0.50f, 0.50f, 0f), new Vector3(0.30f, 0.66f, 35f), new Vector3(0.70f, 0.68f, -30f),
            new Vector3(0.28f, 0.34f, -40f), new Vector3(0.72f, 0.32f, 40f), new Vector3(0.50f, 0.80f, 5f),
            new Vector3(0.50f, 0.22f, -8f)
        };

        private static Color LeafCard(float u, float v)
        {
            // a cluster of pointed leaves; outside the leaves the alpha is 0 (cut out by the shader at 0.45)
            float best = 0f;
            float shade = 0.8f;
            for (int i = 0; i < Leaves.Length; i++)
            {
                var l = Leaves[i];
                float a = l.z * Mathf.Deg2Rad;
                float dx = u - l.x, dy = v - l.y;
                float rx = dx * Mathf.Cos(a) + dy * Mathf.Sin(a);
                float ry = -dx * Mathf.Sin(a) + dy * Mathf.Cos(a);
                float e = (rx * rx) / (0.085f * 0.085f) + (ry * ry) / (0.2f * 0.2f);
                // pointed tip: squeeze the width towards both ends
                e += Mathf.Abs(ry) * 6f * Mathf.Abs(rx) / 0.085f;
                float inside = 1f - e;
                if (inside > best)
                {
                    best = inside;
                    float vein = Mathf.Abs(rx) < 0.008f ? 0.08f : 0f;
                    shade = 0.86f + Hash(i, 29) * 0.14f - vein;
                }
            }
            float alpha = best > 0f ? 1f : 0f;
            return new Color(shade, shade, shade, alpha);
        }

        // ------------------------------------------------------------------ noise

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

        /// <summary>Tileable value noise; periodX / periodY are the lattice periods that make the texture wrap seamlessly.</summary>
        private static float Noise(float x, float y, int periodX, int periodY)
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
