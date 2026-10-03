using System;
using UnityEngine;

namespace AgeOfSakura.EditorTools.UiArt
{
    /// <summary>Signed distance in canvas pixels (negative inside). Origin bottom-left, y up.</summary>
    public delegate float Sdf(float x, float y);

    /// <summary>Colour at a point; <paramref name="d"/> is the shape's signed distance there (negative = inside).</summary>
    public delegate Color Shade(float x, float y, float d);

    /// <summary>
    /// Editor-time painter with supersampling. Shapes are signed-distance functions, fills are shade functions,
    /// so gradients, rim lights and inner shadows can be composed like layers in an art tool.
    /// Used by <see cref="UiArtGenerator"/> to bake the UI sprites into real PNG assets.
    /// </summary>
    public sealed class PaintCanvas
    {
        private readonly int ss;
        private readonly int sw;
        private readonly int sh;
        private readonly float[] r;
        private readonly float[] g;
        private readonly float[] b;
        private readonly float[] a;

        public int Width { get; }
        public int Height { get; }

        public PaintCanvas(int width, int height, int supersample = 2)
        {
            Width = width;
            Height = height;
            ss = supersample;
            sw = width * ss;
            sh = height * ss;
            int n = sw * sh;
            r = new float[n];
            g = new float[n];
            b = new float[n];
            a = new float[n];
        }

        /// <summary>Composites <paramref name="shade"/> over the canvas wherever <paramref name="shape"/> covers.</summary>
        public void Fill(Sdf shape, Shade shade, float opacity = 1f)
        {
            float inv = 1f / ss;
            for (int j = 0; j < sh; j++)
            {
                float y = (j + 0.5f) * inv;
                int row = j * sw;
                for (int i = 0; i < sw; i++)
                {
                    float x = (i + 0.5f) * inv;
                    float d = shape(x, y);
                    float cov = 0.5f - d * ss;
                    if (cov <= 0f) continue;
                    if (cov > 1f) cov = 1f;
                    var c = shade(x, y, d);
                    float sa = c.a * cov * opacity;
                    if (sa <= 0f) continue;
                    int k = row + i;
                    float keep = 1f - sa;
                    r[k] = c.r * sa + r[k] * keep;
                    g[k] = c.g * sa + g[k] * keep;
                    b[k] = c.b * sa + b[k] * keep;
                    a[k] = sa + a[k] * keep;
                }
            }
        }

        public void Fill(Sdf shape, Color color, float opacity = 1f) => Fill(shape, Sh.Solid(color), opacity);

        /// <summary>Soft drop shadow: an inflated copy of <paramref name="shape"/> that fades out over <paramref name="blur"/> px.</summary>
        public void Shadow(Sdf shape, float dx, float dy, float blur, float alpha, Color? tint = null)
        {
            var c = tint ?? new Color(0.09f, 0.05f, 0.02f, 1f);
            Fill(Sd.Inflate(Sd.Shift(shape, dx, dy), blur), Sh.Glow(new Color(c.r, c.g, c.b, alpha), blur));
        }

        /// <summary>Dark outline behind the body, then the body itself.</summary>
        public void Outlined(Sdf shape, Color outline, float outlineWidth, Shade body)
        {
            Fill(Sd.Inflate(shape, outlineWidth), Sh.Solid(outline));
            Fill(shape, body);
        }

        public Texture2D ToTexture()
        {
            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            var pixels = new Color[Width * Height];
            float norm = 1f / (ss * ss);
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    float sr = 0f, sg = 0f, sb = 0f, sa = 0f;
                    for (int j = 0; j < ss; j++)
                    {
                        int row = (y * ss + j) * sw + x * ss;
                        for (int i = 0; i < ss; i++)
                        {
                            sr += r[row + i];
                            sg += g[row + i];
                            sb += b[row + i];
                            sa += a[row + i];
                        }
                    }
                    sa *= norm;
                    if (sa <= 1e-4f) { pixels[y * Width + x] = new Color(0f, 0f, 0f, 0f); continue; }
                    pixels[y * Width + x] = new Color(sr * norm / sa, sg * norm / sa, sb * norm / sa, sa);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(false);
            return tex;
        }
    }

    /// <summary>Shape (signed distance) builders and combinators.</summary>
    public static class Sd
    {
        public static Sdf Circle(float cx, float cy, float radius) => (x, y) =>
        {
            float dx = x - cx, dy = y - cy;
            return Mathf.Sqrt(dx * dx + dy * dy) - radius;
        };

        /// <summary>Rounded box given by centre and half extents; <paramref name="corner"/> is the corner radius.</summary>
        public static Sdf Box(float cx, float cy, float halfW, float halfH, float corner = 0f) => (x, y) =>
        {
            float qx = Mathf.Abs(x - cx) - (halfW - corner);
            float qy = Mathf.Abs(y - cy) - (halfH - corner);
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - corner;
        };

        /// <summary>Box from corner to corner (x0,y0 bottom-left).</summary>
        public static Sdf Rect(float x0, float y0, float x1, float y1, float corner = 0f) =>
            Box((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (x1 - x0) * 0.5f, (y1 - y0) * 0.5f, corner);

        public static Sdf Capsule(float ax, float ay, float bx, float by, float radius) => (x, y) =>
        {
            float pax = x - ax, pay = y - ay, bax = bx - ax, bay = by - ay;
            float len2 = bax * bax + bay * bay;
            float h = len2 <= 0f ? 0f : Mathf.Clamp01((pax * bax + pay * bay) / len2);
            float dx = pax - bax * h, dy = pay - bay * h;
            return Mathf.Sqrt(dx * dx + dy * dy) - radius;
        };

        public static Sdf Ellipse(float cx, float cy, float rx, float ry) => (x, y) =>
        {
            float dx = (x - cx) / rx, dy = (y - cy) / ry;
            return (Mathf.Sqrt(dx * dx + dy * dy) - 1f) * Mathf.Min(rx, ry);
        };

        /// <summary>Filled polygon (even-odd). <paramref name="round"/> rounds the corners.</summary>
        public static Sdf Poly(Vector2[] p, float round = 0f) => (x, y) =>
        {
            float min = float.MaxValue;
            bool inside = false;
            for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
            {
                float ex = p[i].x - p[j].x, ey = p[i].y - p[j].y;
                float wx = x - p[j].x, wy = y - p[j].y;
                float len2 = ex * ex + ey * ey;
                float t = len2 <= 0f ? 0f : Mathf.Clamp01((wx * ex + wy * ey) / len2);
                float dx = wx - ex * t, dy = wy - ey * t;
                min = Mathf.Min(min, dx * dx + dy * dy);
                if ((p[i].y > y) != (p[j].y > y) && x < (p[j].x - p[i].x) * (y - p[i].y) / (p[j].y - p[i].y) + p[i].x) inside = !inside;
            }
            float d = Mathf.Sqrt(min);
            return (inside ? -d : d) - round;
        };

        public static Sdf Polyline(Vector2[] pts, float radius)
        {
            var caps = new Sdf[pts.Length - 1];
            for (int i = 0; i + 1 < pts.Length; i++) caps[i] = Capsule(pts[i].x, pts[i].y, pts[i + 1].x, pts[i + 1].y, radius);
            return Union(caps);
        }

        public static Sdf Arc(float cx, float cy, float radius, float thickness, float startDegrees, float endDegrees)
        {
            int steps = Mathf.Max(3, Mathf.CeilToInt(Mathf.Abs(endDegrees - startDegrees) / 6f));
            var pts = new Vector2[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float a = Mathf.Lerp(startDegrees, endDegrees, (float)i / steps) * Mathf.Deg2Rad;
                pts[i] = new Vector2(cx + Mathf.Cos(a) * radius, cy + Mathf.Sin(a) * radius);
            }
            return Polyline(pts, thickness * 0.5f);
        }

        public static Sdf Union(params Sdf[] shapes) => (x, y) =>
        {
            float d = float.MaxValue;
            for (int i = 0; i < shapes.Length; i++) d = Mathf.Min(d, shapes[i](x, y));
            return d;
        };

        public static Sdf Sub(Sdf a, Sdf b) => (x, y) => Mathf.Max(a(x, y), -b(x, y));
        public static Sdf Inter(Sdf a, Sdf b) => (x, y) => Mathf.Max(a(x, y), b(x, y));
        public static Sdf Inflate(Sdf s, float amount) => (x, y) => s(x, y) - amount;
        public static Sdf Shift(Sdf s, float dx, float dy) => (x, y) => s(x - dx, y - dy);

        /// <summary>Hollow outline of a shape, <paramref name="width"/> px wide, centred on its edge.</summary>
        public static Sdf Edge(Sdf s, float width) => (x, y) => Mathf.Abs(s(x, y)) - width * 0.5f;

        public static Sdf Rotate(Sdf s, float cx, float cy, float radians)
        {
            float cos = Mathf.Cos(-radians), sin = Mathf.Sin(-radians);
            return (x, y) =>
            {
                float dx = x - cx, dy = y - cy;
                return s(cx + dx * cos - dy * sin, cy + dx * sin + dy * cos);
            };
        }

        /// <summary>Everything above the horizontal line y = <paramref name="line"/>.</summary>
        public static Sdf HalfAbove(float line) => (x, y) => line - y;
        public static Sdf HalfBelow(float line) => (x, y) => y - line;
    }

    /// <summary>Shade builders: flat colours, gradients, glows and lit rims.</summary>
    public static class Sh
    {
        public static Shade Solid(Color c) => (x, y, d) => c;

        /// <summary>Vertical gradient from <paramref name="yTop"/> (colour top) to <paramref name="yBottom"/> (colour bottom).</summary>
        public static Shade V(float yTop, float yBottom, Color top, Color bottom) => (x, y, d) =>
        {
            float t = Mathf.Clamp01((yTop - y) / (yTop - yBottom));
            return Color.Lerp(top, bottom, t);
        };

        public static Shade Radial(float cx, float cy, float radius, Color inner, Color outer) => (x, y, d) =>
        {
            float dx = x - cx, dy = y - cy;
            float t = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / radius);
            return Color.Lerp(inner, outer, t);
        };

        /// <summary>Fades to transparent toward the edge of an inflated shape (for glows and soft shadows).</summary>
        public static Shade Glow(Color c, float radius) => (x, y, d) =>
        {
            float t = Mathf.Clamp01(-d / radius);
            t = t * t * (3f - 2f * t);
            return new Color(c.r, c.g, c.b, c.a * t);
        };

        /// <summary>
        /// A lit body: vertical gradient plus a light rim on the upper edge and a dark rim on the lower edge,
        /// as if lit from the top. <paramref name="mid"/> is the vertical centre used to decide upper/lower.
        /// </summary>
        public static Shade Lit(float yTop, float yBottom, Color top, Color bottom, Color rimLight, Color rimDark, float rimWidth)
        {
            float mid = (yTop + yBottom) * 0.5f;
            float half = Mathf.Max(1f, (yTop - yBottom) * 0.5f);
            return (x, y, d) =>
            {
                float t = Mathf.Clamp01((yTop - y) / (yTop - yBottom));
                var c = Color.Lerp(top, bottom, t);
                float rim = Mathf.Clamp01(1f + d / rimWidth);
                rim *= rim;
                float up = Mathf.Clamp01((y - mid) / half * 1.4f + 0.5f);
                var rimColor = Color.Lerp(rimDark, rimLight, up);
                return Color.Lerp(c, rimColor, rim * rimColor.a);
            };
        }

        /// <summary>Multiplies the alpha of another shade.</summary>
        public static Shade Fade(Shade s, float alpha) => (x, y, d) =>
        {
            var c = s(x, y, d);
            c.a *= alpha;
            return c;
        };

        /// <summary>Two-stop gradient along an arbitrary direction (degrees, 0 = to the right, 90 = upward).</summary>
        public static Shade Linear(float x0, float y0, float x1, float y1, Color from, Color to) => (x, y, d) =>
        {
            float dx = x1 - x0, dy = y1 - y0;
            float t = Mathf.Clamp01(((x - x0) * dx + (y - y0) * dy) / (dx * dx + dy * dy));
            return Color.Lerp(from, to, t);
        };

        /// <summary>Value-noise tint for paper fibres. Returns <paramref name="c"/> with alpha modulated by noise.</summary>
        public static Shade Noise(Color c, float scale, float strength, int seed = 1) => (x, y, d) =>
        {
            float n = Value(x * scale + seed * 17.3f, y * scale + seed * 9.1f) * 0.6f + Value(x * scale * 2.7f + 5f, y * scale * 2.7f + 11f) * 0.4f;
            return new Color(c.r, c.g, c.b, c.a * Mathf.Clamp01(n * strength));
        };

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

        public static float Value(float x, float y)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float tx = x - x0, ty = y - y0;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);
            return Mathf.Lerp(Mathf.Lerp(Hash(x0, y0), Hash(x0 + 1, y0), tx), Mathf.Lerp(Hash(x0, y0 + 1), Hash(x0 + 1, y0 + 1), tx), ty);
        }
    }

    /// <summary>Colours for the baked icons (glass surfaces and buttons use literal hex values in <see cref="UiArtRecipes"/>). Runtime code uses <c>Palette</c> for the same roles.</summary>
    public static class Pal
    {
        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        // gold (restrained inlay / coin)
        public static readonly Color GoldLight = Hex("#FFE9A6");
        public static readonly Color Gold = Hex("#F1C24F");
        public static readonly Color GoldDeep = Hex("#C48A24");
        public static readonly Color GoldDark = Hex("#7A4E12");

        // lacquer (dark warm brown-charcoal)
        public static readonly Color LacquerTop = Hex("#4B382D");
        public static readonly Color Lacquer = Hex("#33241C");
        public static readonly Color LacquerDeep = Hex("#1F1510");
        public static readonly Color LacquerRim = Hex("#7B6152");

        // washi paper
        public static readonly Color PaperLight = Hex("#FCF6E6");
        public static readonly Color Paper = Hex("#F3E8CE");
        public static readonly Color PaperDeep = Hex("#E4D4AF");
        public static readonly Color PaperEdge = Hex("#C9B58A");

        // matcha green
        public static readonly Color GreenLight = Hex("#A5D26E");
        public static readonly Color Green = Hex("#6FA04B");
        public static readonly Color GreenDeep = Hex("#446F31");
        public static readonly Color GreenDark = Hex("#26451D");

        // vermilion (torii red)
        public static readonly Color RedLight = Hex("#F0805F");
        public static readonly Color Red = Hex("#CB4630");
        public static readonly Color RedDeep = Hex("#8F2A1D");
        public static readonly Color RedDark = Hex("#511710");

        // diamond blue
        public static readonly Color BlueLight = Hex("#D2F7FF");
        public static readonly Color Blue = Hex("#45B4E6");
        public static readonly Color BlueDeep = Hex("#2378B8");
        public static readonly Color BlueDark = Hex("#133E6B");

        // wood
        public static readonly Color WoodLight = Hex("#F5D6A2");
        public static readonly Color Wood = Hex("#CC9A5F");
        public static readonly Color WoodDeep = Hex("#8A5A2E");
        public static readonly Color Bark = Hex("#5E3D24");
        public static readonly Color Outline = Hex("#3A2314");

        public static readonly Color White = Color.white;
        public static Color A(Color c, float alpha) => new Color(c.r, c.g, c.b, alpha);
    }
}
