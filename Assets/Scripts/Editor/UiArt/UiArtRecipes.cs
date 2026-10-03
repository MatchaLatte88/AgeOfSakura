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
    /// Recipes for every baked UI sprite. Art direction: compact frosted glass (same family as the VoxelHaven HUD) - translucent
    /// cream and dark-brown surfaces with a light upper edge, flat gradient buttons, chunky outlined icons for the content.
    /// Authored at 2x (PPU 200); pills and panels are 9-sliced and rounded to size at runtime (<c>UiKit.Round</c>).
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
            yield return Icon("tools", Tools);
            yield return Icon("iron", Iron);
            yield return Icon("mine", Mine);
            yield return Icon("fish", Fish);
            yield return Icon("blacksmith", Blacksmith);
            yield return Icon("fisher_dock", FisherDock);
            yield return Icon("road", Road);
            yield return Icon("rice_paddy", RicePaddy);
            yield return Icon("garden", Garden);
            yield return Icon("shrine", Shrine);
            yield return Icon("beauty", Beauty);
            yield return Icon("noise", Noise);
            yield return Icon("faith", Faith);
            yield return Icon("upgrade", Upgrade);
            yield return Icon("lock", Lock);
            yield return Icon("clock", Clock);
            yield return Icon("bubble", Bubble);
            yield return Icon("alert", Alert);

            // ---- effects
            yield return Icon("glow", Glow);
            yield return Icon("sparkle", Sparkle);

            // ---- surfaces, chips, bars (9-slice; the corner radius in pixels is the border, see UiKit.Round)
            yield return new ArtEntry { Id = "panel_light", Make = () => GlassLight(256, 256, 80), Border = new Vector4(80, 80, 80, 80), Mipmaps = false };
            yield return new ArtEntry { Id = "pill_light", Make = () => GlassLight(256, 128, 64), Border = new Vector4(64, 64, 64, 64), Mipmaps = false };
            yield return new ArtEntry { Id = "pill_dark", Make = () => GlassDark(256, 128, 64), Border = new Vector4(64, 64, 64, 64), Mipmaps = false };
            yield return new ArtEntry { Id = "pill_flat", Make = FlatPill, Border = new Vector4(64, 64, 64, 64), Mipmaps = false };
            yield return new ArtEntry { Id = "card_light", Make = () => Card(false), Border = new Vector4(56, 56, 56, 56), Mipmaps = false };
            yield return new ArtEntry { Id = "card_locked", Make = () => Card(true), Border = new Vector4(56, 56, 56, 56), Mipmaps = false };
            yield return new ArtEntry { Id = "trough", Make = Trough, Border = new Vector4(36, 32, 36, 32), Mipmaps = false };
            yield return new ArtEntry { Id = "bar_green", Make = () => BarFill(H("#86C868"), H("#5E9E4A")), Border = new Vector4(36, 32, 36, 32), Mipmaps = false };
            yield return new ArtEntry { Id = "bar_gold", Make = () => BarFill(H("#F6D472"), H("#E2B23C")), Border = new Vector4(36, 32, 36, 32), Mipmaps = false };

            // ---- buttons: one flat face per style
            foreach (var style in ButtonStyles())
            {
                var s = style;
                yield return new ArtEntry { Id = "btn_" + s.Name, Make = () => ButtonFace(s), Border = new Vector4(64, 64, 64, 64), Mipmaps = false };
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

        /// <summary>Flat white glyph; the UI tints it (ink on paper, white on colour), so it carries no shadow of its own.</summary>
        private static Texture2D Glyph(Func<Sdf> shape)
        {
            var c = new PaintCanvas(256, 256, 3);
            c.Fill(shape(), Sh.Solid(Color.white));
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

        /// <summary>Currency "Tools": a hammer and a spanner crossed.</summary>
        private static Texture2D Tools()
        {
            var c = new PaintCanvas(256, 256, 3);
            // spanner from the lower left to an open-jawed head at the upper right
            var spannerHandle = Sd.Capsule(62, 60, 158, 156, 14);
            var jawRing = Sd.Circle(180, 178, 40);
            var jawSlot = Sd.Rotate(Sd.Box(206, 204, 26, 15, 3), 180, 178, Mathf.PI * 0.25f);
            var spannerHead = Sd.Sub(jawRing, jawSlot);
            // hammer from the lower right to a head at the upper left
            var hammerHandle = Sd.Capsule(196, 62, 104, 152, 13);
            var hammerHead = Sd.Rotate(Sd.Box(94, 164, 50, 25, 8), 94, 164, Mathf.PI * 0.25f);
            c.Shadow(Sd.Union(spannerHandle, spannerHead, hammerHandle, hammerHead), 3, -9, 14, 0.38f);
            c.Outlined(spannerHandle, Pal.Outline, 7, Sh.Linear(60, 60, 160, 160, H("#F6F9FB"), H("#7B8996")));
            c.Outlined(spannerHead, Pal.Outline, 7, Sh.Linear(150, 140, 212, 214, H("#F6F9FB"), H("#75838F")));
            c.Outlined(hammerHandle, Pal.Outline, 7, Sh.Linear(196, 62, 104, 152, H("#EDBE80"), H("#8A5A2E")));
            c.Outlined(hammerHead, Pal.Outline, 7, Sh.Linear(60, 200, 130, 130, H("#8E9AA6"), H("#3F4B57")));
            c.Fill(Sd.Capsule(70, 82, 140, 148, 2.6f), Sh.Solid(new Color(1, 1, 1, 0.5f)));
            Sparkle(c, 214, 70, 20, 0.9f);
            return c.ToTexture();
        }

        /// <summary>Currency "Iron": a small stack of ingots.</summary>
        private static Texture2D Iron()
        {
            var c = new PaintCanvas(256, 256, 3);
            Sdf Ingot(float cx, float cy) => Sd.Poly(P(cx - 74, cy - 28, cx + 74, cy - 28, cx + 56, cy + 28, cx - 56, cy + 28), 8);
            var spots = new[] { new Vector2(78, 70), new Vector2(178, 70), new Vector2(128, 132) };
            var all = new Sdf[spots.Length];
            for (int i = 0; i < spots.Length; i++) all[i] = Ingot(spots[i].x, spots[i].y);
            c.Shadow(Sd.Union(all), 3, -9, 14, 0.38f);
            for (int i = 0; i < spots.Length; i++)
            {
                var s = spots[i];
                c.Outlined(all[i], Pal.Outline, 7, Sh.V(s.y + 28, s.y - 28, H("#C9D3DC"), H("#5F6C78")));
                c.Fill(Sd.Capsule(s.x - 44, s.y + 16, s.x + 40, s.y + 16, 3), Sh.Solid(new Color(1, 1, 1, 0.7f)));
                c.Fill(Sd.Capsule(s.x - 56, s.y - 14, s.x + 56, s.y - 14, 2.4f), Sh.Solid(Pal.A(H("#37424D"), 0.45f)));
            }
            Sparkle(c, 214, 190, 24, 0.95f);
            return c.ToTexture();
        }

        /// <summary>Building "Mine": a timbered tunnel mouth in a rocky mountain, with an ore cart.</summary>
        private static Texture2D Mine()
        {
            var c = new PaintCanvas(256, 256, 3);
            var mountain = Sd.Poly(P(10, 28, 246, 28, 200, 150, 150, 232, 106, 176, 60, 196), 14);
            c.Shadow(mountain, 3, -9, 14, 0.38f);
            c.Outlined(mountain, Pal.Outline, 7, Sh.V(232, 28, H("#C3C8CE"), H("#6E7681")));
            c.Fill(Sd.Capsule(76, 170, 146, 214, 3), Sh.Solid(new Color(1, 1, 1, 0.45f)));
            c.Fill(Sd.Capsule(60, 120, 100, 150, 3), Sh.Solid(Pal.A(Color.black, 0.15f)));
            var hole = Sd.Poly(P(86, 38, 170, 38, 170, 108, 128, 136, 86, 108), 6);
            c.Outlined(hole, Pal.Outline, 6, Sh.V(136, 38, H("#2B2420"), H("#0E0A08")));
            foreach (float px in new[] { 82f, 174f }) c.Outlined(Sd.Capsule(px, 36, px, 112, 7), Pal.Outline, 5, Sh.Linear(px - 7, 70, px + 7, 70, H("#C28A4F"), H("#6B431F")));
            c.Outlined(Sd.Box(128, 118, 56, 8, 3), Pal.Outline, 5, Sh.V(126, 110, H("#C28A4F"), H("#6B431F")));
            // ore cart on rails
            c.Fill(Sd.Capsule(30, 30, 226, 30, 3), Sh.Solid(Pal.A(Pal.Outline, 0.8f)));
            var cart = Sd.Poly(P(104, 70, 152, 70, 146, 44, 110, 44), 4);
            c.Outlined(cart, Pal.Outline, 5, Sh.V(70, 44, H("#9A6A40"), H("#4C2F19")));
            c.Fill(Sd.Ellipse(128, 72, 24, 8), Sh.V(80, 64, H("#E3A33A"), H("#B46A20")));
            foreach (float wx in new[] { 114f, 142f }) c.Outlined(Sd.Circle(wx, 38, 8), Pal.Outline, 3, Sh.Solid(H("#6D7782")));
            Sparkle(c, 200, 190, 18, 0.9f);
            return c.ToTexture();
        }

        /// <summary>Currency "Fish": a plump river fish with a forked tail.</summary>
        private static Texture2D Fish()
        {
            var c = new PaintCanvas(256, 256, 3);
            var body = Sd.Ellipse(112, 128, 88, 54);
            var tail = Sd.Poly(P(176, 128, 242, 186, 226, 128, 242, 70), 6);
            var topFin = Sd.Poly(P(86, 168, 124, 214, 150, 168), 5);
            var lowFin = Sd.Poly(P(110, 92, 134, 54, 150, 96), 5);
            c.Shadow(Sd.Union(body, tail, topFin, lowFin), 3, -9, 14, 0.38f);
            c.Outlined(topFin, Pal.Outline, 7, Sh.V(214, 168, H("#F0805F"), H("#C4472F")));
            c.Outlined(lowFin, Pal.Outline, 7, Sh.V(96, 54, H("#F0805F"), H("#C4472F")));
            c.Outlined(tail, Pal.Outline, 7, Sh.V(186, 70, H("#F7A07A"), H("#CB4630")));
            c.Outlined(body, Pal.Outline, 7, Sh.Lit(182, 74, H("#9ED8EE"), H("#3A83B2"), new Color(1, 1, 1, 0.5f), Pal.A(Pal.BlueDark, 0.5f), 12));
            c.Fill(Sd.Inter(Sd.Ellipse(112, 104, 78, 30), body), Sh.Solid(Pal.A(Color.white, 0.55f)));          // pale belly
            for (int row = 0; row < 3; row++)
                for (int i = 0; i < 4; i++)
                    c.Fill(Sd.Arc(70 + i * 30 + (row % 2) * 15, 140 - row * 22, 13, 3.2f, 200, 340), Sh.Solid(Pal.A(Pal.BlueDark, 0.45f)));   // scales
            c.Fill(Sd.Capsule(168, 100, 172, 156, 3), Sh.Solid(Pal.A(Pal.BlueDark, 0.4f)));                    // gill line
            c.Fill(Sd.Circle(60, 142, 13), Sh.Solid(Pal.Outline));
            c.Fill(Sd.Circle(60, 142, 10), Sh.Solid(Color.white));
            c.Fill(Sd.Circle(58, 141, 5.5f), Sh.Solid(Pal.Outline));
            c.Fill(Sd.Circle(56, 144, 2f), Sh.Solid(Color.white));
            c.Fill(Sd.Capsule(30, 118, 48, 112, 2.4f), Sh.Solid(Pal.A(Pal.Outline, 0.8f)));                    // mouth
            c.Fill(Sd.Arc(112, 128, 80, 5, 105, 160), Sh.Solid(new Color(1, 1, 1, 0.55f)));
            return c.ToTexture();
        }

        /// <summary>Building "Blacksmith": an anvil on a stump with flying sparks.</summary>
        private static Texture2D Blacksmith()
        {
            var c = new PaintCanvas(256, 256, 3);
            var stump = Sd.Box(128, 48, 66, 40, 10);
            var face = Sd.Box(132, 140, 86, 21, 6);
            var horn = Sd.Poly(P(50, 158, 6, 140, 50, 122), 4);
            var waist = Sd.Poly(P(92, 124, 172, 124, 160, 84, 104, 84), 4);
            var foot = Sd.Box(132, 78, 62, 14, 5);
            c.Shadow(Sd.Union(stump, face, horn, waist, foot), 3, -9, 14, 0.38f);
            c.Outlined(stump, Pal.Outline, 7, Sh.Lit(88, 8, H("#A9784A"), H("#4E3018"), new Color(1, 1, 1, 0.3f), Pal.A(Color.black, 0.45f), 10));
            for (int i = 0; i < 4; i++) c.Fill(Sd.Capsule(80 + i * 32, 18, 84 + i * 32, 74, 2.2f), Sh.Solid(Pal.A(Color.black, 0.2f)));
            c.Outlined(foot, Pal.Outline, 6, Sh.V(92, 64, H("#7E8995"), H("#3B444E")));
            c.Outlined(waist, Pal.Outline, 6, Sh.V(124, 84, H("#6F7B87"), H("#343D47")));
            c.Outlined(horn, Pal.Outline, 6, Sh.V(158, 122, H("#9AA6B2"), H("#4A5561")));
            c.Outlined(face, Pal.Outline, 6, Sh.V(161, 119, H("#C5D0DA"), H("#69757F")));
            c.Fill(Sd.Capsule(60, 154, 196, 154, 2.4f), Sh.Solid(new Color(1, 1, 1, 0.75f)));
            // glowing iron on the face and sparks
            c.Fill(Sd.Box(128, 168, 34, 8, 4), Sh.V(176, 160, H("#FFB347"), H("#E2531F")));
            Sparkle(c, 98, 214, 20, 0.95f);
            Sparkle(c, 162, 226, 16, 0.9f);
            Sparkle(c, 196, 196, 14, 0.8f);
            return c.ToTexture();
        }

        /// <summary>Building "Fisher Dock": a small boat with a sail beside a jetty on the water.</summary>
        private static Texture2D FisherDock()
        {
            var c = new PaintCanvas(256, 256, 3);
            var water = Sd.Ellipse(128, 52, 118, 40);
            c.Shadow(water, 2, -6, 10, 0.3f);
            c.Outlined(water, H("#1F5C8A"), 6, Sh.V(92, 12, H("#6CC7EC"), H("#2C86C2")));
            c.Fill(Sd.Arc(70, 56, 20, 4, 200, 340), Sh.Solid(new Color(1, 1, 1, 0.55f)));
            c.Fill(Sd.Arc(188, 44, 22, 4, 200, 340), Sh.Solid(new Color(1, 1, 1, 0.55f)));
            // jetty: posts and deck
            foreach (float px in new[] { 36f, 74f, 112f })
                c.Outlined(Sd.Capsule(px, 40, px, 100, 7), Pal.Outline, 5, Sh.Linear(px - 7, 70, px + 7, 70, H("#9A6A40"), H("#4C2F19")));
            var deck = Sd.Box(74, 106, 56, 9, 3);
            c.Outlined(deck, Pal.Outline, 6, Sh.V(115, 97, H("#E0AE70"), H("#8A5A2E")));
            for (int i = 0; i < 4; i++) c.Fill(Sd.Capsule(34 + i * 26, 100, 34 + i * 26, 112, 1.6f), Sh.Solid(Pal.A(Pal.Outline, 0.5f)));
            // boat with a sail
            var hull = Sd.Poly(P(124, 86, 232, 86, 212, 48, 148, 48), 8);
            var mast = Sd.Capsule(176, 86, 176, 214, 5);
            var sail = Sd.Poly(P(182, 206, 182, 100, 232, 100), 4);
            var jib = Sd.Poly(P(170, 200, 170, 104, 130, 104), 4);
            c.Shadow(Sd.Union(hull, mast, sail, jib), 2, -7, 10, 0.32f);
            c.Outlined(jib, Pal.Outline, 5, Sh.V(200, 104, H("#FFF6DC"), H("#E3CFA0")));
            c.Outlined(sail, Pal.Outline, 5, Sh.V(206, 100, H("#FFFFFF"), H("#EBDDBA")));
            c.Outlined(mast, Pal.Outline, 4, Sh.Solid(H("#8A5A2E")));
            c.Outlined(hull, Pal.Outline, 6, Sh.V(86, 48, H("#D78A4C"), H("#8F4B24")));
            c.Fill(Sd.Capsule(138, 70, 224, 70, 3), Sh.Solid(Pal.A(Pal.Outline, 0.55f)));
            c.Fill(Sd.Circle(208, 214, 9), Sh.Solid(Pal.RedLight));
            return c.ToTexture();
        }

        /// <summary>Building "Road": cobbled path running away into the distance.</summary>
        private static Texture2D Road()
        {
            var c = new PaintCanvas(256, 256, 3);
            var verge = Sd.Poly(P(8, 24, 248, 24, 168, 238, 88, 238), 6);
            var path = Sd.Poly(P(34, 34, 222, 34, 160, 226, 96, 226), 5);
            c.Shadow(verge, 3, -9, 14, 0.38f);
            c.Outlined(verge, Pal.Outline, 7, Sh.V(238, 24, H("#8FCB5C"), H("#4E9B3E")));
            c.Outlined(path, Pal.Outline, 6, Sh.V(226, 34, H("#D9CDB3"), H("#A79B84")));
            // cobbles in rows that shrink with distance
            for (int row = 0; row < 7; row++)
            {
                float t = row / 6f;
                float y = Mathf.Lerp(54, 206, t * t * 0.4f + t * 0.6f);
                float half = Mathf.Lerp(88, 34, (y - 34) / 192f);
                int n = row % 2 == 0 ? 4 : 3;
                float sz = Mathf.Lerp(24, 11, (y - 34) / 192f);
                for (int i = 0; i < n; i++)
                {
                    float x = 128 + (i - (n - 1) * 0.5f) * (2f * half / (n + 0.2f));
                    var stone = Sd.Ellipse(x, y, sz * 1.15f, sz * 0.62f);
                    c.Outlined(stone, Pal.A(Pal.Outline, 0.75f), 3, Sh.V(y + sz * 0.6f, y - sz * 0.6f, H("#F2EBD9"), H("#B4A78D")));
                }
            }
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

        /// <summary>Need/effect icon "Noise": a speaker with two sound waves. Coloured like the other content icons (the flat glyphs are tinted by the UI and would vanish on a cream card).</summary>
        private static Texture2D Noise()
        {
            var c = new PaintCanvas(256, 256, 3);
            var body = Sd.Poly(P(34, 100, 84, 100, 134, 62, 134, 194, 84, 156, 34, 156), 6);
            var near = Sd.Arc(134, 128, 52, 18, -50, 50);
            var far = Sd.Arc(134, 128, 92, 18, -50, 50);
            c.Shadow(Sd.Union(body, near, far), 3, -9, 14, 0.38f);
            c.Outlined(far, Pal.Outline, 5, Sh.V(200, 56, H("#FFC77A"), H("#E88A2E")));
            c.Outlined(near, Pal.Outline, 5, Sh.V(180, 76, H("#FFC77A"), H("#E88A2E")));
            c.Outlined(body, Pal.Outline, 7, Sh.V(194, 62, H("#E7DCC0"), H("#A99A78")));
            return c.ToTexture();
        }

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

        /// <summary>World marker disc above a building (ready / upgrade / producing): a clean cream disc with a soft rim.</summary>
        private static Texture2D Bubble()
        {
            var c = new PaintCanvas(256, 256, 3);
            var disc = Sd.Circle(128, 128, 112);
            c.Shadow(disc, 0, -7, 12, 0.3f);
            c.Fill(disc, Sh.Solid(H("#D9CBAE")));
            c.Fill(Sd.Inflate(disc, -5), Sh.V(240, 16, H("#FFFFFF"), H("#F3EAD6")));
            return c.ToTexture();
        }

        private static Texture2D Alert()
        {
            var c = new PaintCanvas(128, 128, 3);
            c.Fill(Sd.Circle(64, 64, 56), Sh.V(120, 8, H("#E77A64"), H("#D9604C")));
            c.Fill(Sd.Capsule(64, 52, 64, 84, 8), Sh.Solid(Color.white));
            c.Fill(Sd.Circle(64, 32, 8.5f), Sh.Solid(Color.white));
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

        // ===================================================================== surfaces (9-slice)

        /// <summary>Translucent cream glass: slightly lighter at the top, bright upper edge. <paramref name="radius"/> is in pixels.</summary>
        private static Texture2D GlassLight(int w, int h, float radius)
        {
            var c = new PaintCanvas(w, h, 2);
            var shape = Sd.Box(w * 0.5f, h * 0.5f, w * 0.5f - 1f, h * 0.5f - 1f, radius);
            c.Fill(shape, Sh.V(h, 0, Pal.A(H("#FFFCF6"), 0.975f), Pal.A(H("#FAF1E0"), 0.955f)));
            c.Fill(Sd.Edge(Sd.Inflate(shape, -1.5f), 3f), Sh.V(h, 0, new Color(1, 1, 1, 0.95f), new Color(1, 1, 1, 0.35f)));
            return c.ToTexture();
        }

        /// <summary>Translucent dark-brown glass for what floats over the world (resource bar, toast, pop-ups).</summary>
        private static Texture2D GlassDark(int w, int h, float radius)
        {
            var c = new PaintCanvas(w, h, 2);
            var shape = Sd.Box(w * 0.5f, h * 0.5f, w * 0.5f - 1f, h * 0.5f - 1f, radius);
            c.Fill(shape, Sh.V(h, 0, Pal.A(H("#40332A"), 0.80f), Pal.A(H("#33271F"), 0.86f)));
            c.Fill(Sd.Edge(Sd.Inflate(shape, -1.5f), 3f), Sh.V(h, 0, new Color(1, 1, 1, 0.24f), new Color(1, 1, 1, 0.05f)));
            return c.ToTexture();
        }

        /// <summary>Plain white pill that the UI tints (chips, tracks, flashes).</summary>
        private static Texture2D FlatPill()
        {
            var c = new PaintCanvas(256, 128, 2);
            c.Fill(Sd.Box(128, 64, 127, 63, 63), Sh.Solid(Color.white));
            return c.ToTexture();
        }

        /// <summary>Tappable tile on a light panel: flat cream with a fine edge. The locked variant is greyer.</summary>
        private static Texture2D Card(bool locked)
        {
            var c = new PaintCanvas(192, 192, 2);
            c.Fill(Sd.Box(96, 96, 95, 95, 56), Sh.Solid(locked ? H("#D8CFBA") : H("#E6DAC0")));
            c.Fill(Sd.Box(96, 96, 93, 93, 54), locked ? Sh.V(192, 0, H("#E9E2D2"), H("#E0D8C6")) : Sh.V(192, 0, H("#F8F0DE"), H("#F0E5CC")));
            return c.ToTexture();
        }

        /// <summary>Dark track of the in-world progress bar.</summary>
        private static Texture2D Trough()
        {
            var c = new PaintCanvas(256, 64, 3);
            var cap = Sd.Box(128, 32, 127, 31, 31);
            c.Fill(cap, Sh.Solid(Pal.A(H("#33271F"), 0.85f)));
            c.Fill(Sd.Edge(Sd.Inflate(cap, -1f), 2f), Sh.Solid(new Color(1, 1, 1, 0.18f)));
            return c.ToTexture();
        }

        private static Texture2D BarFill(Color top, Color bottom)
        {
            var c = new PaintCanvas(256, 64, 3);
            c.Fill(Sd.Box(128, 32, 127, 31, 31), Sh.V(64, 0, top, bottom));
            return c.ToTexture();
        }

        // ===================================================================== buttons

        public sealed class ButtonStyleDef
        {
            public string Name;
            public Color Top, Bottom, Edge;
        }

        private static IEnumerable<ButtonStyleDef> ButtonStyles()
        {
            var light = new Color(1, 1, 1, 0.28f);
            yield return new ButtonStyleDef { Name = "green", Top = H("#74B45C"), Bottom = H("#5E9E4A"), Edge = light };
            yield return new ButtonStyleDef { Name = "gold", Top = H("#F6D472"), Bottom = H("#E2B23C"), Edge = light };
            yield return new ButtonStyleDef { Name = "red", Top = H("#E77A64"), Bottom = H("#D9604C"), Edge = light };
            yield return new ButtonStyleDef { Name = "paper", Top = H("#FFFFFF"), Bottom = H("#F3EAD6"), Edge = H("#E6DAC0") };
            yield return new ButtonStyleDef { Name = "disabled", Top = H("#E8E0CE"), Bottom = H("#DDD3BC"), Edge = H("#D0C5AB") };
            yield return new ButtonStyleDef { Name = "dark", Top = H("#4A3B31"), Bottom = H("#3A2E26"), Edge = new Color(1, 1, 1, 0.14f) };
        }

        private static Texture2D ButtonFace(ButtonStyleDef s)
        {
            var c = new PaintCanvas(256, 128, 2);
            var shape = Sd.Box(128, 64, 127, 63, 63);
            c.Fill(shape, Sh.V(128, 0, s.Top, s.Bottom));
            c.Fill(Sd.Edge(Sd.Inflate(shape, -1f), 2f), Sh.Solid(s.Edge));
            return c.ToTexture();
        }
    }
}
