using System;
using System.Collections.Generic;
using UnityEngine;

namespace AgeOfSakura.EditorTools.UiArt
{
    public sealed class ArtEntry
    {
        public string Id;
        public Func<Texture2D> Make;
        /// <summary>9-slice border (left, bottom, right, top) in texture pixels; zero = not sliced.</summary>
        public Vector4 Border;
        public bool Repeat;
        public bool Mipmaps = true;
    }

    /// <summary>
    /// Recipes for every baked UI sprite. Art direction: "Lacquer and Washi" - dark warm lacquer frames with a thin gold inlay,
    /// washi paper surfaces, chunky lit icons with dark outlines, everything lit from the top. Authored at 2x (PPU 200).
    /// </summary>
    public static class UiArtRecipes
    {
        private static Color H(string hex) => Pal.Hex(hex);

        public static IEnumerable<ArtEntry> All()
        {
            // ---- icons (256)
            yield return Icon("coin", Coin);
            yield return Icon("wood", Wood);
            yield return Icon("diamond", Diamond);
            yield return Icon("hammer", Hammer);
            yield return Icon("check", Check);
            yield return Icon("cross", Cross);
            yield return Icon("rotate", Rotate);
            yield return Icon("gear", Gear);
            yield return Icon("house", House);
            yield return Icon("woodcutter", Woodcutter);
            yield return Icon("town_hall", TownHall);
            yield return Icon("rice", Rice);
            yield return Icon("rice_paddy", RicePaddy);
            yield return Icon("garden", Garden);
            yield return Icon("shrine", Shrine);
            yield return Icon("beauty", Beauty);
            yield return Icon("noise", Noise);
            yield return Icon("faith", Faith);
            yield return Icon("upgrade", Upgrade);
            yield return Icon("lock", Lock);
            yield return Icon("clock", Clock);
            yield return Icon("bubble", () => Bubble(false));
            yield return Icon("bubble_tail", () => Bubble(true));
            yield return Icon("badge", Badge);
            yield return Icon("alert", Alert);
            yield return new ArtEntry { Id = "tail_paper", Make = TailPaper, Mipmaps = false };
            yield return Icon("medallion", Medallion);

            // ---- effects
            yield return Icon("glow", Glow);
            yield return Icon("sparkle", Sparkle);
            yield return Icon("rays", Rays);
            yield return Icon("shine", Shine);
            yield return new ArtEntry { Id = "vignette", Make = Vignette, Mipmaps = false };
            yield return new ArtEntry { Id = "fade_top", Make = () => EdgeFade(true), Mipmaps = false };
            yield return new ArtEntry { Id = "fade_bottom", Make = () => EdgeFade(false), Mipmaps = false };
            yield return new ArtEntry { Id = "paper_noise", Make = PaperNoise, Repeat = true, Mipmaps = true };

            // ---- panels, chips, bars (9-slice)
            yield return new ArtEntry { Id = "panel_paper", Make = () => Panel(false), Border = new Vector4(110, 110, 110, 110), Mipmaps = false };
            yield return new ArtEntry { Id = "panel_lacquer", Make = () => Panel(true), Border = new Vector4(110, 110, 110, 110), Mipmaps = false };
            yield return new ArtEntry { Id = "plaque", Make = Plaque, Border = new Vector4(120, 80, 120, 80), Mipmaps = false };
            yield return new ArtEntry { Id = "pill_lacquer", Make = () => Pill(true), Border = new Vector4(64, 64, 64, 64), Mipmaps = false };
            yield return new ArtEntry { Id = "pill_paper", Make = () => Pill(false), Border = new Vector4(64, 64, 64, 64), Mipmaps = false };
            yield return new ArtEntry { Id = "card_paper", Make = () => Card(0), Border = new Vector4(80, 80, 80, 80), Mipmaps = false };
            yield return new ArtEntry { Id = "card_locked", Make = () => Card(1), Border = new Vector4(80, 80, 80, 80), Mipmaps = false };
            yield return new ArtEntry { Id = "trough", Make = Trough, Border = new Vector4(36, 32, 36, 32), Mipmaps = false };
            yield return new ArtEntry { Id = "bar_green", Make = () => BarFill(Pal.GreenLight, Pal.Green, Pal.GreenDeep), Border = new Vector4(36, 32, 36, 32), Mipmaps = false };
            yield return new ArtEntry { Id = "bar_gold", Make = () => BarFill(Pal.GoldLight, Pal.Gold, Pal.GoldDeep), Border = new Vector4(36, 32, 36, 32), Mipmaps = false };
            yield return new ArtEntry { Id = "divider", Make = Divider, Border = new Vector4(200, 0, 200, 0), Mipmaps = false };

            // ---- buttons: face + depth per style
            foreach (var style in ButtonStyles())
            {
                var s = style;
                yield return new ArtEntry { Id = "btn_" + s.Name, Make = () => ButtonFace(s), Border = new Vector4(84, 84, 84, 84), Mipmaps = false };
                yield return new ArtEntry { Id = "btn_" + s.Name + "_depth", Make = () => ButtonDepth(s), Border = new Vector4(84, 84, 84, 84), Mipmaps = false };
            }
        }

        private static ArtEntry Icon(string id, Func<Texture2D> make) => new ArtEntry { Id = id, Make = make };

        // ===================================================================== helpers

        private static void Sparkle(PaintCanvas c, float cx, float cy, float size, float alpha = 1f)
        {
            var v = Sd.Poly(new[] { new Vector2(cx, cy + size), new Vector2(cx + size * 0.16f, cy), new Vector2(cx, cy - size), new Vector2(cx - size * 0.16f, cy) });
            var hz = Sd.Poly(new[] { new Vector2(cx + size, cy), new Vector2(cx, cy + size * 0.16f), new Vector2(cx - size, cy), new Vector2(cx, cy - size * 0.16f) });
            c.Fill(Sd.Inflate(Sd.Circle(cx, cy, size * 0.35f), 6), Sh.Glow(new Color(1f, 1f, 0.9f, 0.55f * alpha), 12));
            c.Fill(Sd.Union(v, hz), Sh.Solid(new Color(1f, 1f, 1f, alpha)));
        }

        /// <summary>Five-petal cherry blossom, lit from above. Used as a corner ornament so the UI carries the game's theme.</summary>
        private static void Blossom(PaintCanvas c, float cx, float cy, float r, float degrees)
        {
            var petals = new List<Sdf>();
            for (int i = 0; i < 5; i++)
            {
                float a = (degrees + i * 72f) * Mathf.Deg2Rad;
                float px = cx + Mathf.Cos(a) * r * 0.5f, py = cy + Mathf.Sin(a) * r * 0.5f;
                petals.Add(Sd.Circle(px, py, r * 0.46f));
            }
            var flower = Sd.Union(petals.ToArray());
            c.Shadow(flower, 1.5f, -3f, 6f, 0.35f);
            c.Fill(Sd.Inflate(flower, 2.5f), Sh.Solid(H("#B9607E")));
            c.Fill(flower, Sh.Radial(cx, cy, r * 1.05f, H("#F0A3BC"), H("#FFE9F0")));
            for (int i = 0; i < 5; i++)
            {
                float a = (degrees + i * 72f + 36f) * Mathf.Deg2Rad;
                c.Fill(Sd.Capsule(cx + Mathf.Cos(a) * r * 0.2f, cy + Mathf.Sin(a) * r * 0.2f, cx + Mathf.Cos(a) * r * 0.62f, cy + Mathf.Sin(a) * r * 0.62f, 1.1f), Sh.Solid(Pal.A(H("#C9668A"), 0.55f)));
            }
            c.Fill(Sd.Circle(cx, cy, r * 0.2f), Sh.Radial(cx, cy, r * 0.2f, H("#FFE49A"), H("#E0A63A")));
            c.Fill(Sd.Circle(cx - r * 0.06f, cy + r * 0.08f, r * 0.07f), Sh.Solid(new Color(1, 1, 1, 0.6f)));
        }

        private static Vector2[] P(params float[] xy)
        {
            var pts = new Vector2[xy.Length / 2];
            for (int i = 0; i < pts.Length; i++) pts[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
            return pts;
        }

        // ===================================================================== icons

        private static Texture2D Coin()
        {
            var c = new PaintCanvas(256, 256, 3);
            var disc = Sd.Circle(128, 128, 110);
            c.Shadow(disc, 3, -10, 14, 0.38f);
            c.Outlined(disc, Pal.GoldDark, 9, Sh.Lit(238, 18, Pal.GoldLight, Pal.GoldDeep, new Color(1, 1, 1, 0.95f), Pal.A(Pal.GoldDark, 0.8f), 13));
            c.Fill(Sd.Edge(Sd.Circle(128, 128, 84), 8), Sh.Solid(Pal.A(Pal.GoldDark, 0.5f)));
            c.Fill(Sd.Edge(Sd.Circle(128, 128, 76), 3), Sh.Solid(new Color(1, 1, 1, 0.4f)));
            c.Fill(Sd.Circle(128, 128, 72), Sh.V(200, 56, H("#FFE58F"), H("#E9AE40")), 0.6f);
            var hole = Sd.Box(128, 128, 25, 25, 6);
            c.Outlined(hole, Pal.GoldDark, 6, Sh.V(153, 103, H("#2B1A0B"), H("#57381A")));
            c.Fill(Sd.Rect(106, 104, 150, 108, 2), Sh.Solid(new Color(1, 1, 1, 0.18f)));
            var crescent = Sd.Inter(Sd.Sub(Sd.Circle(100, 158, 64), Sd.Circle(116, 144, 64)), Sd.Circle(128, 128, 102));
            c.Fill(crescent, Sh.Solid(new Color(1, 1, 1, 0.62f)));
            Sparkle(c, 186, 192, 22, 0.95f);
            return c.ToTexture();
        }

        private static void LogEnd(PaintCanvas c, float cx, float cy, float r)
        {
            var circle = Sd.Circle(cx, cy, r);
            c.Outlined(circle, Pal.Outline, 6, Sh.Lit(cy + r, cy - r, H("#93613A"), H("#4E3018"), new Color(1, 1, 1, 0.35f), Pal.A(Color.black, 0.5f), 10));
            var face = Sd.Circle(cx, cy, r * 0.74f);
            c.Fill(face, Sh.Radial(cx + r * 0.12f, cy + r * 0.14f, r * 0.85f, Pal.WoodLight, H("#D9A566")));
            c.Fill(Sd.Edge(Sd.Circle(cx, cy, r * 0.56f), 4), Sh.Solid(Pal.A(Pal.WoodDeep, 0.5f)));
            c.Fill(Sd.Edge(Sd.Circle(cx, cy, r * 0.34f), 4), Sh.Solid(Pal.A(Pal.WoodDeep, 0.5f)));
            c.Fill(Sd.Circle(cx, cy, r * 0.1f), Sh.Solid(Pal.A(Pal.WoodDeep, 0.65f)));
            c.Fill(Sd.Capsule(cx + 2, cy, cx + r * 0.62f, cy - r * 0.28f, 1.8f), Sh.Solid(Pal.A(Pal.WoodDeep, 0.45f)));
            c.Fill(Sd.Arc(cx, cy, r * 0.9f, 6, 112, 165), Sh.Solid(new Color(1, 1, 1, 0.3f)));
        }

        private static Texture2D Wood()
        {
            var c = new PaintCanvas(256, 256, 3);
            var all = Sd.Union(Sd.Circle(78, 84, 62), Sd.Circle(178, 84, 62), Sd.Circle(128, 168, 62));
            c.Shadow(all, 3, -9, 14, 0.38f);
            LogEnd(c, 78, 84, 62);
            LogEnd(c, 178, 84, 62);
            LogEnd(c, 128, 168, 62);
            return c.ToTexture();
        }

        private static Texture2D Diamond()
        {
            var c = new PaintCanvas(256, 256, 3);
            var outline = Sd.Poly(P(22, 150, 70, 212, 186, 212, 234, 150, 128, 28), 5);
            c.Shadow(outline, 3, -9, 14, 0.38f);
            c.Fill(Sd.Inflate(outline, 8), Sh.Solid(Pal.BlueDark));
            c.Fill(outline, Sh.V(212, 28, Pal.BlueLight, Pal.BlueDeep));
            void Facet(Vector2[] pts, Color a, Color b, float y0, float y1) => c.Fill(Sd.Poly(pts), Sh.V(y0, y1, a, b));
            Facet(P(22, 150, 70, 212, 100, 150), H("#9CE3F8"), H("#6FCDF0"), 212, 150);
            Facet(P(70, 212, 186, 212, 156, 150, 100, 150), H("#F4FDFF"), H("#BDEFFF"), 212, 150);
            Facet(P(186, 212, 234, 150, 156, 150), H("#62C8EE"), H("#3BB0E3"), 212, 150);
            Facet(P(22, 150, 100, 150, 128, 28), H("#3BA9E2"), H("#2A86C6"), 150, 28);
            Facet(P(100, 150, 156, 150, 128, 28), H("#8CDAF7"), H("#3DA6DE"), 150, 28);
            Facet(P(156, 150, 234, 150, 128, 28), H("#2A86C6"), H("#1C64A3"), 150, 28);
            var edges = Sd.Union(
                Sd.Polyline(P(22, 150, 234, 150), 1.6f), Sd.Polyline(P(70, 212, 100, 150, 128, 28), 1.6f),
                Sd.Polyline(P(186, 212, 156, 150, 128, 28), 1.6f), Sd.Polyline(P(70, 212, 22, 150), 1.6f), Sd.Polyline(P(186, 212, 234, 150), 1.6f));
            c.Fill(edges, Sh.Solid(new Color(1, 1, 1, 0.55f)));
            Sparkle(c, 196, 196, 26, 1f);
            return c.ToTexture();
        }

        private static Texture2D Hammer()
        {
            var c = new PaintCanvas(256, 256, 3);
            const float cx = 168f, cy = 168f;
            float tilt = -Mathf.PI * 0.25f; // the head runs across the handle
            var handle = Sd.Capsule(58, 56, cx, cy, 19);
            var headBase = Sd.Box(cx, cy, 74, 42, 13);
            var head = Sd.Rotate(headBase, cx, cy, tilt);
            c.Shadow(Sd.Union(handle, head), 3, -9, 14, 0.38f);
            c.Outlined(handle, Pal.Outline, 7, Sh.Linear(50, 50, cx, cy, H("#EDBE80"), H("#8A5A2E")));
            c.Fill(Sd.Capsule(52, 70, cx - 10, cy + 4, 3.5f), Sh.Solid(new Color(1, 1, 1, 0.3f)));
            c.Outlined(head, Pal.Outline, 7, Sh.Linear(cx - 60, cy + 60, cx + 60, cy - 60, H("#F6F9FB"), H("#75838F")));
            var capA = Sd.Inter(Sd.Rotate(Sd.Box(cx - 56, cy, 20, 42, 5), cx, cy, tilt), head);
            var capB = Sd.Inter(Sd.Rotate(Sd.Box(cx + 56, cy, 20, 42, 5), cx, cy, tilt), head);
            c.Fill(capA, Sh.Solid(Pal.A(H("#56636F"), 0.6f)));
            c.Fill(capB, Sh.Solid(Pal.A(H("#56636F"), 0.6f)));
            c.Fill(Sd.Rotate(Sd.Box(cx, cy + 25, 60, 5, 2.5f), cx, cy, tilt), Sh.Solid(new Color(1, 1, 1, 0.8f)));
            return c.ToTexture();
        }

        private static Texture2D Glyph(Func<Sdf> shape, float shadow = 0.42f)
        {
            var c = new PaintCanvas(256, 256, 3);
            var s = shape();
            c.Shadow(s, 0, -9, 10, shadow);
            c.Fill(s, Sh.V(230, 26, Color.white, H("#E7EEDC")));
            return c.ToTexture();
        }

        private static Texture2D Check() => Glyph(() => Sd.Polyline(P(54, 132, 106, 80, 202, 188), 23));

        private static Texture2D Cross() => Glyph(() => Sd.Union(Sd.Capsule(64, 64, 192, 192, 23), Sd.Capsule(64, 192, 192, 64, 23)));

        private static Texture2D Rotate() => Glyph(() =>
        {
            var arc = Sd.Arc(128, 128, 66, 30, 125, 395);
            float a = 395f * Mathf.Deg2Rad;
            var end = new Vector2(128 + Mathf.Cos(a) * 66, 128 + Mathf.Sin(a) * 66);
            var dir = new Vector2(-Mathf.Sin(a), Mathf.Cos(a));
            var nrm = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            var head = Sd.Poly(new[] { end + nrm * 48 - dir * 8, end - nrm * 48 - dir * 8, end + dir * 58 }, 5);
            return Sd.Union(arc, head);
        });

        private static Texture2D Gear() => Glyph(() =>
        {
            var parts = new List<Sdf> { Sd.Circle(128, 128, 68) };
            for (int i = 0; i < 8; i++) parts.Add(Sd.Rotate(Sd.Box(128, 128 + 78, 21, 27, 6), 128, 128, i * Mathf.PI / 4f));
            return Sd.Sub(Sd.Union(parts.ToArray()), Sd.Circle(128, 128, 30));
        });

        private static Texture2D House()
        {
            var c = new PaintCanvas(256, 256, 3);
            var walls = Sd.Box(128, 92, 76, 54, 5);
            var roof = Sd.Poly(P(10, 120, 128, 234, 246, 120, 228, 102, 128, 190, 28, 102), 5);
            c.Shadow(Sd.Union(walls, roof), 3, -9, 14, 0.38f);
            c.Outlined(walls, Pal.Outline, 7, Sh.Lit(146, 38, H("#FFF6DC"), H("#E4CC9C"), new Color(1, 1, 1, 0.6f), Pal.A(Pal.WoodDeep, 0.4f), 10));
            c.Fill(Sd.Rect(56, 132, 200, 140, 0), Sh.Solid(Pal.A(Pal.Bark, 0.85f)));
            c.Fill(Sd.Rect(110, 40, 146, 104, 6), Sh.Solid(Pal.Outline));
            c.Fill(Sd.Rect(114, 44, 142, 102, 4), Sh.V(102, 44, H("#B2794A"), H("#7A4A26")));
            c.Fill(Sd.Circle(136, 74, 4), Sh.Solid(Pal.GoldLight));
            foreach (float wx in new[] { 76f, 180f })
            {
                c.Fill(Sd.Box(wx, 92, 18, 18, 4), Sh.Solid(Pal.Outline));
                c.Fill(Sd.Box(wx, 92, 14, 14, 3), Sh.V(106, 78, H("#9FD0EA"), H("#4E86AC")));
                c.Fill(Sd.Rect(wx - 1.5f, 78, wx + 1.5f, 106, 0), Sh.Solid(Pal.A(Pal.Outline, 0.8f)));
            }
            c.Outlined(roof, Pal.Outline, 8, Sh.V(234, 100, H("#F8DE8E"), H("#C5943F")));
            for (int i = 0; i < 6; i++)
            {
                float t = 0.16f + i * 0.14f;
                float xl = Mathf.Lerp(28, 128, t), xr = Mathf.Lerp(228, 128, t);
                float y = Mathf.Lerp(108, 200, t);
                c.Fill(Sd.Capsule(xl, y, xr, y, 1.7f), Sh.Solid(Pal.A(Pal.WoodDeep, 0.28f)));
            }
            c.Fill(Sd.Polyline(P(24, 116, 128, 218), 3), Sh.Solid(new Color(1, 1, 1, 0.35f)));
            return c.ToTexture();
        }

        private static Texture2D Woodcutter()
        {
            var c = new PaintCanvas(256, 256, 3);
            var stump = Sd.Union(Sd.Box(104, 80, 62, 46, 10), Sd.Ellipse(104, 118, 62, 24));
            var handle = Sd.Capsule(120, 122, 206, 214, 12);
            var headPoly = Sd.Poly(P(184, 226, 236, 244, 250, 196, 218, 174, 194, 196), 5);
            c.Shadow(Sd.Union(stump, handle, headPoly), 3, -9, 14, 0.38f);
            c.Outlined(Sd.Box(104, 80, 62, 46, 10), Pal.Outline, 7, Sh.Lit(126, 34, H("#9A6A40"), H("#4C2F19"), new Color(1, 1, 1, 0.3f), Pal.A(Color.black, 0.45f), 10));
            for (int i = 0; i < 4; i++) c.Fill(Sd.Capsule(62 + i * 28, 44, 66 + i * 28, 110, 2.2f), Sh.Solid(Pal.A(Color.black, 0.2f)));
            c.Outlined(Sd.Ellipse(104, 118, 62, 24), Pal.Outline, 7, Sh.Radial(112, 122, 70, Pal.WoodLight, H("#D9A566")));
            c.Fill(Sd.Edge(Sd.Ellipse(104, 118, 40, 15), 4), Sh.Solid(Pal.A(Pal.WoodDeep, 0.5f)));
            c.Fill(Sd.Edge(Sd.Ellipse(104, 118, 20, 7), 3), Sh.Solid(Pal.A(Pal.WoodDeep, 0.5f)));
            c.Outlined(handle, Pal.Outline, 6, Sh.Linear(120, 122, 206, 214, H("#E0AE70"), H("#8A5A2E")));
            c.Outlined(headPoly, Pal.Outline, 7, Sh.Linear(190, 240, 246, 180, H("#F6F9FB"), H("#7B8996")));
            c.Fill(Sd.Polyline(P(196, 228, 240, 240), 2.4f), Sh.Solid(new Color(1, 1, 1, 0.8f)));
            return c.ToTexture();
        }

        private static Texture2D TownHall()
        {
            var c = new PaintCanvas(256, 256, 3);
            var lower = Sd.Box(128, 62, 68, 44, 4);
            var roof1 = Sd.Poly(P(12, 108, 68, 144, 188, 144, 244, 108, 214, 92, 42, 92), 4);
            var upper = Sd.Box(128, 158, 44, 26, 4);
            var roof2 = Sd.Poly(P(50, 182, 92, 214, 164, 214, 206, 182, 180, 168, 76, 168), 4);
            var spire = Sd.Capsule(128, 208, 128, 246, 4);
            c.Shadow(Sd.Union(lower, roof1, upper, roof2, spire), 3, -9, 14, 0.38f);
            c.Outlined(lower, Pal.Outline, 7, Sh.V(106, 18, H("#FFF6DC"), H("#DDC594")));
            c.Fill(Sd.Rect(120, 18, 136, 70, 3), Sh.Solid(Pal.A(Pal.Outline, 0.95f)));
            foreach (float px in new[] { 78f, 178f }) c.Fill(Sd.Rect(px - 4, 22, px + 4, 100, 0), Sh.Solid(Pal.A(Pal.Bark, 0.9f)));
            c.Outlined(roof1, Pal.Outline, 7, Sh.V(144, 92, H("#7B90AB"), H("#33455A")));
            c.Fill(Sd.Polyline(P(24, 102, 68, 134, 188, 134, 232, 102), 3), Sh.Solid(Pal.A(Pal.GoldLight, 0.9f)));
            c.Outlined(upper, Pal.Outline, 6, Sh.V(184, 132, H("#FFF6DC"), H("#DDC594")));
            c.Fill(Sd.Rect(120, 134, 136, 168, 3), Sh.Solid(Pal.A(Pal.Outline, 0.9f)));
            c.Outlined(roof2, Pal.Outline, 7, Sh.V(214, 168, H("#8CA1BC"), H("#3A4D63")));
            c.Fill(Sd.Polyline(P(62, 176, 96, 204, 160, 204, 194, 176), 2.6f), Sh.Solid(Pal.A(Pal.GoldLight, 0.9f)));
            c.Outlined(spire, Pal.GoldDark, 4, Sh.V(246, 208, Pal.GoldLight, Pal.GoldDeep));
            c.Fill(Sd.Circle(128, 232, 8), Sh.Solid(Pal.GoldLight));
            return c.ToTexture();
        }

        private static Texture2D Rice()
        {
            var c = new PaintCanvas(256, 256, 3);
            var bowl = Sd.Sub(Sd.Ellipse(128, 118, 100, 84), Sd.HalfAbove(118));
            var mound = Sd.Inter(Sd.Ellipse(128, 118, 80, 78), Sd.HalfAbove(112));
            c.Shadow(Sd.Union(bowl, mound), 3, -9, 14, 0.38f);
            c.Outlined(mound, Pal.Outline, 7, Sh.V(196, 112, H("#FFFFFF"), H("#E9E1CC")));
            foreach (var g in new[] { new Vector2(92, 150), new Vector2(128, 176), new Vector2(160, 150), new Vector2(118, 140), new Vector2(146, 126) })
                c.Fill(Sd.Capsule(g.x - 6, g.y - 3, g.x + 6, g.y + 3, 3.4f), Sh.Solid(Pal.A(H("#B9AE96"), 0.75f)));
            c.Outlined(bowl, Pal.Outline, 8, Sh.V(118, 34, H("#D9573C"), H("#8F2A1D")));
            c.Fill(Sd.Rect(30, 108, 226, 120, 5), Sh.V(120, 108, Pal.GoldLight, Pal.GoldDeep));
            c.Fill(Sd.Capsule(60, 84, 110, 60, 4), Sh.Solid(new Color(1, 1, 1, 0.3f)));
            return c.ToTexture();
        }

        private static Texture2D RicePaddy()
        {
            var c = new PaintCanvas(256, 256, 3);
            var field = Sd.Poly(P(128, 40, 240, 100, 128, 160, 16, 100), 10);
            c.Shadow(field, 3, -9, 14, 0.38f);
            c.Outlined(field, Pal.Outline, 8, Sh.V(160, 40, H("#B9E4EC"), H("#4A9CBA")));
            c.Fill(Sd.Inflate(Sd.Edge(Sd.Poly(P(128, 52, 220, 100, 128, 148, 36, 100), 8), 6), 0), Sh.Solid(Pal.A(H("#8A6A3E"), 0.85f)));
            foreach (var t in new[] { new Vector2(96, 100), new Vector2(128, 116), new Vector2(160, 100), new Vector2(128, 84), new Vector2(72, 108), new Vector2(184, 108) })
            {
                c.Fill(Sd.Capsule(t.x, t.y, t.x - 8, t.y + 46, 4.5f), Sh.V(t.y + 46, t.y, Pal.GreenLight, Pal.GreenDeep));
                c.Fill(Sd.Capsule(t.x, t.y, t.x + 9, t.y + 40, 4.5f), Sh.V(t.y + 40, t.y, Pal.GreenLight, Pal.Green));
                c.Fill(Sd.Capsule(t.x, t.y, t.x + 1, t.y + 52, 4f), Sh.V(t.y + 52, t.y, H("#D8E88A"), Pal.Green));
            }
            return c.ToTexture();
        }

        private static Texture2D Garden()
        {
            var c = new PaintCanvas(256, 256, 3);
            var sand = Sd.Ellipse(128, 92, 112, 66);
            var rock1 = Sd.Ellipse(96, 120, 38, 30);
            var rock2 = Sd.Ellipse(152, 108, 24, 20);
            var foliage = Sd.Union(Sd.Circle(178, 176, 30), Sd.Circle(206, 162, 22), Sd.Circle(154, 160, 20));
            c.Shadow(Sd.Union(sand, rock1, foliage), 3, -9, 14, 0.38f);
            c.Outlined(sand, Pal.Outline, 8, Sh.V(158, 26, H("#FBF1D3"), H("#D9C79A")));
            c.Fill(Sd.Edge(Sd.Ellipse(128, 88, 88, 46), 4), Sh.Solid(Pal.A(Pal.WoodDeep, 0.35f)));
            c.Fill(Sd.Edge(Sd.Ellipse(128, 86, 62, 30), 4), Sh.Solid(Pal.A(Pal.WoodDeep, 0.35f)));
            c.Outlined(rock2, Pal.Outline, 6, Sh.V(128, 88, H("#B5B3AA"), H("#7C7A74")));
            c.Outlined(rock1, Pal.Outline, 7, Sh.V(150, 90, H("#C4C2B9"), H("#77756F")));
            c.Fill(Sd.Capsule(182, 112, 178, 160, 5), Sh.Solid(Pal.Bark));
            c.Outlined(foliage, Pal.Outline, 6, Sh.V(206, 140, Pal.GreenLight, Pal.GreenDeep));
            return c.ToTexture();
        }

        private static Texture2D Shrine()
        {
            var c = new PaintCanvas(256, 256, 3);
            var postL = Sd.Rect(70, 26, 96, 196, 3);
            var postR = Sd.Rect(160, 26, 186, 196, 3);
            var kasagi = Sd.Poly(P(14, 206, 242, 206, 222, 240, 34, 240), 3);
            var nuki = Sd.Rect(50, 148, 206, 170, 3);
            c.Shadow(Sd.Union(postL, postR, kasagi, nuki), 3, -9, 14, 0.38f);
            c.Outlined(postL, Pal.Outline, 7, Sh.Linear(70, 0, 96, 0, Pal.RedLight, Pal.RedDeep));
            c.Outlined(postR, Pal.Outline, 7, Sh.Linear(160, 0, 186, 0, Pal.RedLight, Pal.RedDeep));
            c.Outlined(nuki, Pal.Outline, 7, Sh.V(170, 148, Pal.RedLight, Pal.Red));
            c.Outlined(kasagi, Pal.Outline, 7, Sh.V(240, 206, H("#5A4A44"), Pal.Outline));
            c.Fill(Sd.Rect(70, 26, 96, 40, 0), Sh.Solid(Pal.A(Pal.Outline, 0.9f)));
            c.Fill(Sd.Rect(160, 26, 186, 40, 0), Sh.Solid(Pal.A(Pal.Outline, 0.9f)));
            return c.ToTexture();
        }

        private static Texture2D Beauty()
        {
            var c = new PaintCanvas(256, 256, 3);
            c.Shadow(Sd.Circle(128, 128, 96), 3, -9, 14, 0.3f);
            Blossom(c, 128, 128, 104, 90f);
            return c.ToTexture();
        }

        private static Texture2D Noise() => Glyph(() =>
        {
            var body = Sd.Poly(P(34, 100, 84, 100, 134, 62, 134, 194, 84, 156, 34, 156), 6);
            return Sd.Union(body, Sd.Arc(134, 128, 52, 16, -50, 50), Sd.Arc(134, 128, 92, 16, -50, 50));
        });

        private static Texture2D Faith()
        {
            var c = new PaintCanvas(256, 256, 3);
            var baseS = Sd.Box(128, 34, 52, 18, 4);
            var post = Sd.Rect(112, 50, 144, 116, 2);
            var lamp = Sd.Box(128, 140, 46, 26, 4);
            var roof = Sd.Poly(P(64, 166, 192, 166, 172, 208, 84, 208), 4);
            var cap = Sd.Circle(128, 222, 14);
            c.Shadow(Sd.Union(baseS, post, lamp, roof, cap), 3, -9, 14, 0.38f);
            c.Outlined(baseS, Pal.Outline, 7, Sh.V(52, 16, H("#C4C2B9"), H("#77756F")));
            c.Outlined(post, Pal.Outline, 6, Sh.V(116, 50, H("#C4C2B9"), H("#8E8C86")));
            c.Outlined(lamp, Pal.Outline, 6, Sh.V(166, 114, H("#C4C2B9"), H("#8E8C86")));
            c.Fill(Sd.Box(128, 140, 24, 14, 3), Sh.V(154, 126, H("#FFF3B0"), H("#F2B84A")));
            c.Outlined(roof, Pal.Outline, 7, Sh.V(208, 166, H("#D3D1C8"), H("#77756F")));
            c.Outlined(cap, Pal.Outline, 5, Sh.V(236, 208, H("#C4C2B9"), H("#8E8C86")));
            return c.ToTexture();
        }

        private static Texture2D Upgrade() => Glyph(() => Sd.Poly(P(128, 238, 214, 140, 160, 140, 160, 26, 96, 26, 96, 140, 42, 140), 8));

        private static Texture2D Lock()
        {
            var c = new PaintCanvas(256, 256, 3);
            var shackle = Sd.Union(Sd.Arc(128, 156, 42, 22, 0, 180), Sd.Capsule(86, 156, 86, 118, 11), Sd.Capsule(170, 156, 170, 118, 11));
            var body = Sd.Box(128, 96, 76, 58, 18);
            c.Shadow(Sd.Union(shackle, body), 3, -9, 14, 0.38f);
            c.Outlined(shackle, Pal.Outline, 6, Sh.V(200, 116, H("#F3F6F8"), H("#8391A0")));
            c.Outlined(body, Pal.Outline, 8, Sh.Lit(154, 38, H("#F6D57C"), H("#C48A24"), new Color(1, 1, 1, 0.7f), Pal.A(Pal.GoldDark, 0.6f), 12));
            c.Fill(Sd.Circle(128, 104, 15), Sh.Solid(Pal.Outline));
            c.Fill(Sd.Poly(P(120, 100, 136, 100, 141, 62, 115, 62), 2), Sh.Solid(Pal.Outline));
            return c.ToTexture();
        }

        private static Texture2D Clock()
        {
            var c = new PaintCanvas(256, 256, 3);
            var disc = Sd.Circle(128, 128, 108);
            c.Shadow(disc, 3, -8, 12, 0.35f);
            c.Outlined(disc, Pal.GoldDark, 8, Sh.Lit(236, 20, Pal.GoldLight, Pal.GoldDeep, new Color(1, 1, 1, 0.9f), Pal.A(Pal.GoldDark, 0.7f), 12));
            c.Fill(Sd.Circle(128, 128, 84), Sh.V(212, 44, Pal.PaperLight, Pal.PaperDeep));
            c.Fill(Sd.Edge(Sd.Circle(128, 128, 84), 4), Sh.Solid(Pal.A(Pal.GoldDark, 0.55f)));
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6f;
                var p0 = new Vector2(128 + Mathf.Cos(a) * 70, 128 + Mathf.Sin(a) * 70);
                var p1 = new Vector2(128 + Mathf.Cos(a) * 78, 128 + Mathf.Sin(a) * 78);
                c.Fill(Sd.Capsule(p0.x, p0.y, p1.x, p1.y, 2.6f), Sh.Solid(Pal.A(Pal.Outline, 0.7f)));
            }
            c.Fill(Sd.Capsule(128, 128, 128, 188, 6), Sh.Solid(Pal.Outline));
            c.Fill(Sd.Capsule(128, 128, 170, 108, 6), Sh.Solid(Pal.Outline));
            c.Fill(Sd.Circle(128, 128, 10), Sh.Solid(Pal.GoldDeep));
            return c.ToTexture();
        }

        private static Texture2D Bubble(bool withTail)
        {
            int h = withTail ? 300 : 256;
            var c = new PaintCanvas(256, h, 3);
            float cy = withTail ? 172f : 128f;
            var round = Sd.Circle(128, cy, 112);
            Sdf shape = round;
            if (withTail) shape = Sd.Union(round, Sd.Poly(P(92, 84, 164, 84, 128, 22), 6));
            c.Shadow(shape, 3, -9, 14, 0.4f);
            c.Fill(Sd.Inflate(shape, 0), Sh.Solid(Pal.Outline));
            Sdf ring = Sd.Inflate(shape, -8);
            c.Fill(ring, Sh.Lit(cy + 104, cy - 104, Pal.GoldLight, Pal.GoldDeep, new Color(1, 1, 1, 0.9f), Pal.A(Pal.GoldDark, 0.7f), 10));
            Sdf inner = Sd.Inflate(shape, -22);
            c.Fill(inner, Sh.V(cy + 90, cy - 90, H("#FFFBEC"), H("#F1E3C0")));
            c.Fill(inner, (x, y, d) => new Color(0.4f, 0.25f, 0.1f, 0.2f * Mathf.Clamp01(1 + d / 16f) * Mathf.Clamp01((y - cy) / 120f + 0.4f)));
            return c.ToTexture();
        }

        private static Texture2D Badge()
        {
            var c = new PaintCanvas(128, 128, 3);
            var disc = Sd.Circle(64, 64, 52);
            c.Shadow(disc, 1, -4, 6, 0.4f);
            c.Fill(Sd.Inflate(disc, 6), Sh.Solid(Color.white));
            c.Fill(disc, Sh.Lit(116, 12, Pal.RedLight, Pal.RedDeep, new Color(1, 1, 1, 0.5f), Pal.A(Pal.RedDark, 0.7f), 8));
            c.Fill(Sd.Inter(Sd.Sub(Sd.Circle(50, 82, 32), Sd.Circle(58, 74, 32)), disc), Sh.Solid(new Color(1, 1, 1, 0.5f)));
            return c.ToTexture();
        }

        private static Texture2D Alert()
        {
            var c = new PaintCanvas(128, 128, 3);
            var disc = Sd.Circle(64, 64, 54);
            c.Shadow(disc, 1, -4, 6, 0.4f);
            c.Outlined(disc, Pal.RedDark, 5, Sh.Lit(118, 10, Pal.RedLight, Pal.RedDeep, new Color(1, 1, 1, 0.5f), Pal.A(Pal.RedDark, 0.7f), 8));
            c.Fill(Sd.Capsule(64, 50, 64, 82, 8), Sh.Solid(Color.white));
            c.Fill(Sd.Circle(64, 32, 8.5f), Sh.Solid(Color.white));
            return c.ToTexture();
        }

        private static Texture2D TailPaper()
        {
            var c = new PaintCanvas(128, 96, 3);
            var tri = Sd.Poly(P(14, 96, 114, 96, 64, 10), 8);
            c.Shadow(tri, 1, -4, 6, 0.3f);
            c.Fill(Sd.Inflate(tri, 0), Sh.Solid(Pal.LacquerDeep));
            c.Fill(Sd.Inflate(tri, -7), Sh.V(96, 10, Pal.PaperLight, Pal.PaperDeep));
            return c.ToTexture();
        }

        private static Texture2D Medallion()
        {
            var c = new PaintCanvas(256, 256, 3);
            var disc = Sd.Circle(128, 128, 116);
            c.Shadow(disc, 2, -8, 12, 0.4f);
            c.Fill(disc, Sh.Solid(Pal.Outline));
            c.Fill(Sd.Circle(128, 128, 108), Sh.Lit(236, 20, Pal.GoldLight, Pal.GoldDeep, new Color(1, 1, 1, 0.9f), Pal.A(Pal.GoldDark, 0.7f), 12));
            var inner = Sd.Circle(128, 128, 90);
            c.Fill(inner, Sh.V(218, 38, H("#FFFBEC"), H("#EBDCB6")));
            c.Fill(inner, (x, y, d) => new Color(0.4f, 0.25f, 0.1f, 0.24f * Mathf.Clamp01(1 + d / 18f) * Mathf.Clamp01((y - 128) / 130f + 0.45f)));
            return c.ToTexture();
        }

        // ===================================================================== effects

        private static Texture2D Glow()
        {
            var c = new PaintCanvas(128, 128, 1);
            c.Fill(Sd.Circle(64, 64, 64), (x, y, d) =>
            {
                float t = Mathf.Clamp01(-d / 64f);
                t = t * t * (3f - 2f * t);
                return new Color(1, 1, 1, t);
            });
            return c.ToTexture();
        }

        private static Texture2D Sparkle()
        {
            var c = new PaintCanvas(128, 128, 3);
            Sparkle(c, 64, 64, 58);
            return c.ToTexture();
        }

        private static Texture2D Rays()
        {
            var c = new PaintCanvas(256, 256, 2);
            var parts = new List<Sdf>();
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6f;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var n = new Vector2(-d.y, d.x);
                parts.Add(Sd.Poly(new[] { new Vector2(128, 128) + n * 5f, new Vector2(128, 128) - n * 5f, new Vector2(128, 128) + d * 128f - n * 14f, new Vector2(128, 128) + d * 128f + n * 14f }));
            }
            c.Fill(Sd.Union(parts.ToArray()), (x, y, d) =>
            {
                float r = Mathf.Sqrt((x - 128) * (x - 128) + (y - 128) * (y - 128)) / 128f;
                return new Color(1, 1, 1, Mathf.Clamp01(1f - r) * Mathf.Clamp01(r * 6f) * 0.9f);
            });
            return c.ToTexture();
        }

        private static Texture2D Shine()
        {
            var c = new PaintCanvas(128, 128, 2);
            c.Fill(Sd.Poly(P(40, 0, 92, 0, 128, 128, 76, 128)), (x, y, d) => new Color(1, 1, 1, 0.55f * Mathf.Clamp01(1f + d / 10f)));
            return c.ToTexture();
        }

        private static Texture2D Vignette()
        {
            var tex = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            var px = new Color[128 * 128];
            for (int y = 0; y < 128; y++)
            {
                for (int x = 0; x < 128; x++)
                {
                    float dx = (x + 0.5f) / 128f * 2f - 1f, dy = (y + 0.5f) / 128f * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx * 0.9f + dy * dy * 1.15f);
                    float a = Mathf.Clamp01((r - 0.62f) / 0.75f);
                    px[y * 128 + x] = new Color(0.08f, 0.05f, 0.03f, a * a * 0.55f);
                }
            }
            tex.SetPixels(px);
            tex.Apply(false);
            return tex;
        }

        private static Texture2D EdgeFade(bool top)
        {
            var tex = new Texture2D(4, 128, TextureFormat.RGBA32, false);
            var px = new Color[4 * 128];
            for (int y = 0; y < 128; y++)
            {
                float t = top ? (y / 127f) : 1f - y / 127f; // 1 at the screen edge
                float a = t * t * 0.55f;
                for (int x = 0; x < 4; x++) px[y * 4 + x] = new Color(0.1f, 0.06f, 0.03f, a);
            }
            tex.SetPixels(px);
            tex.Apply(false);
            return tex;
        }

        private static Texture2D PaperNoise()
        {
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float v = Tile(x / (float)n * 8f, y / (float)n * 8f, 8) * 0.55f + Tile(x / (float)n * 24f, y / (float)n * 24f, 24) * 0.45f;
                    float fibre = Tile(x / (float)n * 3f, y / (float)n * 40f, 3, 40);
                    px[y * n + x] = new Color(0.45f, 0.3f, 0.12f, Mathf.Clamp01(v * 0.10f + fibre * 0.05f));
                }
            }
            tex.SetPixels(px);
            tex.Apply(false);
            return tex;
        }

        private static float Tile(float x, float y, int period, int periodY = -1)
        {
            if (periodY < 0) periodY = period;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float tx = x - x0, ty = y - y0;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);
            float H2(int ix, int iy)
            {
                ix = ((ix % period) + period) % period;
                iy = ((iy % periodY) + periodY) % periodY;
                return Sh.Value(ix * 3.7f + 0.5f, iy * 5.1f + 0.5f);
            }
            return Mathf.Lerp(Mathf.Lerp(H2(x0, y0), H2(x0 + 1, y0), tx), Mathf.Lerp(H2(x0, y0 + 1), H2(x0 + 1, y0 + 1), tx), ty);
        }

        // ===================================================================== panels / chips / bars (9-slice)

        private static Texture2D Panel(bool dark)
        {
            var c = new PaintCanvas(512, 512, 2);
            var outer = Sd.Box(256, 256, 254, 254, 70);
            c.Fill(outer, Sh.Lit(510, 2, Pal.LacquerTop, Pal.LacquerDeep, Pal.A(Pal.LacquerRim, 0.95f), Pal.A(Color.black, 0.5f), 7));
            c.Fill(Sd.Edge(Sd.Box(256, 256, 238, 238, 56), 4), Sh.V(494, 18, Pal.GoldLight, Pal.GoldDeep), 0.95f);
            c.Fill(Sd.Edge(Sd.Box(256, 256, 232, 232, 52), 1.6f), Sh.Solid(Pal.A(Color.black, 0.35f)));
            var inner = Sd.Box(256, 256, 224, 224, 44);
            if (dark)
            {
                c.Fill(inner, Sh.V(480, 32, H("#3E2E25"), H("#221812")));
                c.Fill(inner, (x, y, d) => new Color(1, 1, 1, 0.06f * Mathf.Clamp01((y - 256) / 240f) * Mathf.Clamp01(1 + d / 40f)));
            }
            else
            {
                c.Fill(inner, Sh.V(480, 32, Pal.PaperLight, Pal.PaperDeep));
                c.Fill(inner, Sh.Noise(Pal.A(Pal.PaperEdge, 1f), 0.05f, 0.5f), 0.32f);
                c.Fill(inner, (x, y, d) => new Color(0.36f, 0.22f, 0.08f, 0.26f * Mathf.Clamp01(1 + d / 22f) * Mathf.Clamp01((y - 200) / 250f + 0.25f)));
                c.Fill(Sd.Edge(Sd.Box(256, 256, 224, 224, 44), 3), Sh.Solid(Pal.A(Pal.PaperEdge, 0.7f)), 0.8f);
            }
            if (dark)
            {
                foreach (var corner in new[] { new Vector2(52, 52), new Vector2(460, 52), new Vector2(52, 460), new Vector2(460, 460) })
                    c.Fill(Sd.Circle(corner.x, corner.y, 5), Sh.V(corner.y + 5, corner.y - 5, Pal.GoldLight, Pal.GoldDeep), 0.9f);
            }
            else
            {
                // blossoms tucked into two opposite corners
                // only inside the 110 px corner regions, otherwise the 9-slice stretches them into streaks
                Blossom(c, 58, 454, 46, 12);
                Blossom(c, 454, 58, 46, 50);
                foreach (var corner in new[] { new Vector2(460, 460), new Vector2(52, 52) })
                    c.Fill(Sd.Circle(corner.x, corner.y, 5), Sh.V(corner.y + 5, corner.y - 5, Pal.GoldLight, Pal.GoldDeep), 0.9f);
            }
            return c.ToTexture();
        }

        /// <summary>Title plaque that overhangs the top edge of the bottom sheet: dark lacquer, gold inlay, gilded end lugs.</summary>
        private static Texture2D Plaque()
        {
            var c = new PaintCanvas(512, 192, 2);
            var outer = Sd.Box(256, 96, 254, 94, 64);
            c.Shadow(outer, 0, -6, 8, 0.25f);
            c.Fill(outer, Sh.Lit(190, 2, Pal.LacquerTop, Pal.LacquerDeep, Pal.A(Pal.LacquerRim, 0.95f), Pal.A(Color.black, 0.5f), 7));
            c.Fill(Sd.Edge(Sd.Box(256, 96, 240, 80, 52), 4), Sh.V(180, 12, Pal.GoldLight, Pal.GoldDeep), 0.95f);
            var body = Sd.Box(256, 96, 232, 72, 46);
            c.Fill(body, Sh.V(168, 24, H("#3F2F26"), H("#1F1610")));
            c.Fill(Sd.Inter(body, Sd.HalfAbove(100)), Sh.V(168, 100, new Color(1, 1, 1, 0.12f), new Color(1, 1, 1, 0.01f)));
            foreach (float x in new[] { 40f, 472f }) c.Fill(Sd.Circle(x, 96, 8), Sh.V(104, 88, Pal.GoldLight, Pal.GoldDeep));
            return c.ToTexture();
        }

        private static Texture2D Pill(bool dark)
        {
            var c = new PaintCanvas(384, 128, 2);
            var cap = Sd.Box(192, 64, 190, 62, 62);
            if (dark)
            {
                c.Fill(cap, Sh.Lit(126, 2, Pal.LacquerTop, Pal.LacquerDeep, Pal.A(Pal.LacquerRim, 0.9f), Pal.A(Color.black, 0.5f), 6));
                c.Fill(Sd.Edge(Sd.Box(192, 64, 184, 56, 56), 3.2f), Sh.V(120, 8, Pal.GoldLight, Pal.GoldDeep), 0.95f);
                var body = Sd.Box(192, 64, 178, 50, 50);
                c.Fill(body, Sh.V(114, 14, H("#3B2B22"), H("#1D140F")));
                c.Fill(Sd.Inter(body, Sd.HalfAbove(66)), Sh.V(114, 66, new Color(1, 1, 1, 0.13f), new Color(1, 1, 1, 0.02f)));
                c.Fill(body, (x, y, d) => new Color(0, 0, 0, 0.35f * Mathf.Clamp01(1 + d / 12f) * Mathf.Clamp01((64 - y) / 60f + 0.2f)));
            }
            else
            {
                c.Fill(cap, Sh.Lit(126, 2, H("#5A4437"), Pal.LacquerDeep, Pal.A(Pal.LacquerRim, 0.9f), Pal.A(Color.black, 0.5f), 6));
                c.Fill(Sd.Edge(Sd.Box(192, 64, 184, 56, 56), 3f), Sh.V(120, 8, Pal.GoldLight, Pal.GoldDeep), 0.9f);
                var body = Sd.Box(192, 64, 178, 50, 50);
                c.Fill(body, Sh.V(114, 14, Pal.PaperLight, Pal.PaperDeep));
                c.Fill(body, (x, y, d) => new Color(0.36f, 0.22f, 0.08f, 0.22f * Mathf.Clamp01(1 + d / 14f) * Mathf.Clamp01((y - 40) / 80f + 0.3f)));
            }
            return c.ToTexture();
        }

        private static Texture2D Card(int variant)
        {
            var c = new PaintCanvas(256, 256, 2);
            var outer = Sd.Box(128, 128, 126, 126, 56);
            bool locked = variant == 1;
            c.Fill(outer, Sh.Solid(locked ? H("#8F8672") : Pal.PaperEdge));
            var body = Sd.Box(128, 128, 120, 120, 50);
            c.Fill(body, locked ? Sh.V(248, 8, H("#DCD3BE"), H("#BDB299")) : Sh.V(248, 8, H("#FFF9EA"), H("#EEDFBB")));
            if (!locked) c.Fill(body, Sh.Noise(Pal.A(Pal.PaperEdge, 1f), 0.06f, 0.5f), 0.3f);
            c.Fill(body, (x, y, d) => new Color(0.36f, 0.22f, 0.08f, 0.2f * Mathf.Clamp01(1 + d / 16f) * Mathf.Clamp01((y - 90) / 150f + 0.15f)));
            c.Fill(Sd.Inter(Sd.Edge(Sd.Box(128, 128, 116, 116, 46), 3), Sd.HalfAbove(150)), Sh.Solid(new Color(1, 1, 1, 0.55f)));
            return c.ToTexture();
        }

        private static Texture2D Trough()
        {
            var c = new PaintCanvas(256, 64, 3);
            var cap = Sd.Box(128, 32, 127, 31, 31);
            c.Fill(cap, Sh.Solid(Pal.A(Pal.LacquerDeep, 1f)));
            c.Fill(Sd.Box(128, 31, 123, 27, 27), Sh.V(58, 4, H("#221510"), H("#3A2A21")));
            c.Fill(Sd.Box(128, 31, 123, 27, 27), (x, y, d) => new Color(0, 0, 0, 0.5f * Mathf.Clamp01(1 + d / 9f) * Mathf.Clamp01((y - 20) / 30f)));
            c.Fill(Sd.Inter(Sd.Edge(Sd.Box(128, 32, 125, 29, 29), 2.4f), Sd.HalfBelow(32)), Sh.Solid(Pal.A(Pal.GoldLight, 0.35f)));
            return c.ToTexture();
        }

        private static Texture2D BarFill(Color light, Color mid, Color deep)
        {
            var c = new PaintCanvas(256, 64, 3);
            var cap = Sd.Box(128, 32, 127, 31, 31);
            c.Fill(cap, Sh.Solid(Pal.A(deep, 1f)));
            var body = Sd.Box(128, 32, 124, 28, 28);
            c.Fill(body, Sh.Lit(62, 2, light, deep, new Color(1, 1, 1, 0.55f), Pal.A(Color.black, 0.25f), 7));
            c.Fill(Sd.Inter(Sd.Box(128, 40, 112, 14, 14), Sd.HalfAbove(34)), Sh.V(56, 34, new Color(1, 1, 1, 0.5f), new Color(1, 1, 1, 0.08f)));
            for (int i = -2; i < 12; i++)
            {
                float x = i * 26f;
                c.Fill(Sd.Inter(Sd.Poly(P(x, 4, x + 12, 4, x + 30, 60, x + 18, 60)), body), Sh.Solid(new Color(1, 1, 1, 0.07f)));
            }
            return c.ToTexture();
        }

        private static Texture2D Divider()
        {
            var c = new PaintCanvas(512, 24, 3);
            c.Fill(Sd.Rect(30, 10, 482, 14, 2), (x, y, d) =>
            {
                float t = Mathf.Abs(x - 256f) / 226f;
                return new Color(Pal.Gold.r, Pal.Gold.g, Pal.Gold.b, Mathf.Clamp01(1.4f - t * 1.5f));
            });
            var diamond = Sd.Poly(P(256, 22, 270, 12, 256, 2, 242, 12));
            c.Fill(Sd.Inflate(diamond, 2), Sh.Solid(Pal.GoldDark));
            c.Fill(diamond, Sh.V(22, 2, Pal.GoldLight, Pal.GoldDeep));
            return c.ToTexture();
        }

        // ===================================================================== buttons

        public sealed class ButtonStyleDef
        {
            public string Name;
            public Color Light, Mid, Deep, Outline;
        }

        private static IEnumerable<ButtonStyleDef> ButtonStyles()
        {
            yield return new ButtonStyleDef { Name = "green", Light = H("#B3DE7A"), Mid = Pal.Green, Deep = Pal.GreenDeep, Outline = Pal.GreenDark };
            yield return new ButtonStyleDef { Name = "gold", Light = H("#FFE9A6"), Mid = Pal.Gold, Deep = Pal.GoldDeep, Outline = Pal.GoldDark };
            yield return new ButtonStyleDef { Name = "red", Light = H("#F58F70"), Mid = Pal.Red, Deep = Pal.RedDeep, Outline = Pal.RedDark };
            yield return new ButtonStyleDef { Name = "paper", Light = H("#FFFBEC"), Mid = H("#F1E4C4"), Deep = H("#DCC898"), Outline = H("#8A7350") };
            yield return new ButtonStyleDef { Name = "disabled", Light = H("#D9D1BD"), Mid = H("#B9B098"), Deep = H("#958C74"), Outline = H("#5F5844") };
            yield return new ButtonStyleDef { Name = "dark", Light = H("#5B4739"), Mid = Pal.Lacquer, Deep = Pal.LacquerDeep, Outline = H("#120B08") };
        }

        private static Texture2D ButtonFace(ButtonStyleDef s)
        {
            var c = new PaintCanvas(384, 192, 2);
            var outer = Sd.Box(192, 96, 190, 94, 60);
            c.Fill(outer, Sh.Solid(s.Outline));
            var body = Sd.Box(192, 96, 183, 87, 54);
            c.Fill(body, Sh.Lit(184, 8, s.Light, s.Deep, new Color(1, 1, 1, 0.55f), Pal.A(Color.black, 0.28f), 9));
            var gloss = Sd.Inter(Sd.Box(192, 130, 160, 46, 40), Sd.HalfAbove(104));
            c.Fill(gloss, Sh.V(176, 104, new Color(1, 1, 1, 0.42f), new Color(1, 1, 1, 0.04f)));
            c.Fill(Sd.Inter(Sd.Edge(Sd.Box(192, 96, 176, 80, 48), 2), Sd.HalfAbove(96)), Sh.Solid(new Color(1, 1, 1, 0.3f)));
            return c.ToTexture();
        }

        private static Texture2D ButtonDepth(ButtonStyleDef s)
        {
            var c = new PaintCanvas(384, 192, 2);
            var outer = Sd.Box(192, 96, 190, 94, 60);
            c.Fill(outer, Sh.V(188, 4, Color.Lerp(s.Outline, s.Deep, 0.35f), s.Outline));
            return c.ToTexture();
        }
    }
}
