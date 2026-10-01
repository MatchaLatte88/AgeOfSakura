using System.Collections.Generic;
using UnityEngine;

namespace AgeOfSakura.Game
{
    /// <summary>Roofs and flat quads generated in code (soft-shaded, see <see cref="ToonMeshes"/> for the other shapes).</summary>
    public static class ProceduralMeshes
    {
        /// <summary>
        /// Flat XZ quad from (0,0) to (width,height), facing up, UV (0..1). Used for terrain and overlays.
        /// </summary>
        public static Mesh GroundQuad(float width, float height)
        {
            var mesh = new Mesh { name = "GroundQuad" };
            mesh.vertices = new[]
            {
                new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, height),
                new Vector3(width, 0f, height), new Vector3(width, 0f, 0f)
            };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            return ToonMeshes.Finish(mesh);
        }

        /// <summary>
        /// Concave hip/gable roof built by lofting rectangles from the eaves (y=0) to a ridge line; corners are shared so the
        /// shading rolls softly over hips. Ridge half-extents default to a proper hip roof; pass ridgeHalfW = halfW for a gable.
        /// <paramref name="lip"/> flicks the outermost eave edge up and out, the typical upswept East-Asian eave (0 = none).
        /// </summary>
        public static Mesh Roof(float width, float depth, float height, float ridgeHalfW, float ridgeHalfD, float curve = 0.55f, float lip = 0.05f, bool openEnds = false)
        {
            float hw = width * 0.5f;
            float hd = depth * 0.5f;
            var rings = new List<Vector3>();   // (halfW, halfD, y)
            if (lip > 0f) rings.Add(new Vector3(hw + lip * 1.2f, hd + lip * 1.2f, lip * 0.9f));
            rings.Add(new Vector3(hw, hd, 0f));
            if (curve > 0f)
            {
                float hw1 = Mathf.Lerp(hw, ridgeHalfW, curve);
                float hd1 = Mathf.Lerp(hd, ridgeHalfD, curve);
                rings.Add(new Vector3(hw1, hd1, height * 0.38f));
            }
            rings.Add(new Vector3(ridgeHalfW, ridgeHalfD, height));

            var verts = new List<Vector3>();
            var tris = new List<int>();
            foreach (var ring in rings) verts.AddRange(Corners(ring));
            for (int r = 0; r + 1 < rings.Count; r++)
            {
                for (int s = 0; s < 4; s++)
                {
                    if (openEnds && (s == 1 || s == 3)) continue; // gable: the ends stay open for a wall triangle
                    int n = (s + 1) % 4;
                    int bl = r * 4 + s, br = r * 4 + n, tl = (r + 1) * 4 + s, tr = (r + 1) * 4 + n;
                    tris.Add(bl); tris.Add(tl); tris.Add(tr);
                    tris.Add(bl); tris.Add(tr); tris.Add(br);
                }
            }

            if (openEnds)
            {
                // a single open sheet has no back faces, so the inverted-hull outline never shows along its slope edges:
                // give the gable a thin closed shell (underside sheet + strips along the eave and the two open ends)
                int bottom = verts.Count;
                for (int r = 0; r < rings.Count; r++)
                    foreach (var c in Corners(rings[r])) verts.Add(c + Vector3.down * GableShellThickness);
                int sheetTris = tris.Count;
                for (int i = 0; i < sheetTris; i += 3)
                {
                    tris.Add(tris[i] + bottom); tris.Add(tris[i + 2] + bottom); tris.Add(tris[i + 1] + bottom);
                }
                for (int r = 0; r + 1 < rings.Count; r++)
                {
                    for (int corner = 0; corner < 4; corner++)
                        AddOutwardQuad(verts, tris, r * 4 + corner, (r + 1) * 4 + corner, bottom + (r + 1) * 4 + corner, bottom + r * 4 + corner,
                            corner == 0 || corner == 3 ? Vector3.left : Vector3.right);
                }
                AddOutwardQuad(verts, tris, 0, 1, bottom + 1, bottom, Vector3.back);        // eave, front
                AddOutwardQuad(verts, tris, 2, 3, bottom + 3, bottom + 2, Vector3.forward);  // eave, back
                AddOutwardQuad(verts, tris, 3, 0, bottom, bottom + 3, Vector3.left);         // lip edge across the open ends
                AddOutwardQuad(verts, tris, 1, 2, bottom + 2, bottom + 1, Vector3.right);
            }
            else
            {
                // underside: separate vertices so it stays flat and dark
                int cap = verts.Count;
                verts.AddRange(Corners(rings[0]));
                tris.Add(cap); tris.Add(cap + 1); tris.Add(cap + 2);
                tris.Add(cap); tris.Add(cap + 2); tris.Add(cap + 3);
            }

            var mesh = new Mesh { name = "Roof" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            return ToonMeshes.Finish(mesh);
        }

        /// <summary>Hip roof: hips meet at 45 degrees, so the ridge length is the difference of the two half extents.</summary>
        public static Mesh HipRoof(float width, float depth, float height, float curve = 0.55f, float lip = 0.05f)
        {
            float hw = width * 0.5f;
            float hd = depth * 0.5f;
            return Roof(width, depth, height, Mathf.Max(0f, hw - hd), Mathf.Max(0f, hd - hw), curve, lip);
        }

        /// <summary>Gable roof with the ridge running along X. The two ends are open; close them with <see cref="GableWall"/>.</summary>
        public static Mesh GableRoof(float width, float depth, float height, float curve = 0.35f, float lip = 0.05f)
        {
            return Roof(width, depth, height, width * 0.5f, 0f, curve, lip, true);
        }

        /// <summary>
        /// Triangular wall under a gable end: base <paramref name="width"/> along Z, apex <paramref name="height"/> up, extruded
        /// <paramref name="thickness"/> along X (centred). Flat faces; the outline pass still closes it through the baked direction.
        /// </summary>
        public static Mesh GableWall(float width, float height, float thickness)
        {
            float hw = width * 0.5f, ht = thickness * 0.5f;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
            {
                int i = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c);
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) >= 0f) { tris.Add(i); tris.Add(i + 1); tris.Add(i + 2); }
                else { tris.Add(i); tris.Add(i + 2); tris.Add(i + 1); }
            }
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward)
            {
                Tri(a, b, c, outward);
                Tri(a, c, d, outward);
            }
            var l0 = new Vector3(-ht, 0f, -hw); var l1 = new Vector3(-ht, 0f, hw); var l2 = new Vector3(-ht, height, 0f);
            var r0 = new Vector3(ht, 0f, -hw); var r1 = new Vector3(ht, 0f, hw); var r2 = new Vector3(ht, height, 0f);
            Tri(l0, l1, l2, Vector3.left);
            Tri(r0, r1, r2, Vector3.right);
            Quad(l0, l1, r1, r0, Vector3.down);
            Quad(l0, r0, r2, l2, new Vector3(0f, hw, -height));
            Quad(l1, l2, r2, r1, new Vector3(0f, hw, height));
            var mesh = new Mesh { name = "GableWall" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            return ToonMeshes.Finish(mesh);
        }

        // ------------------------------------------------------------------ pagoda roofs and ornaments

        /// <summary>
        /// Concave East-Asian roof: the slope sags (steeper towards the ridge) and the four eave corners swing up and out.
        /// Described by the outer eave rectangle, the rectangle at the top (a flat plateau under the next storey, or a short ridge)
        /// and the height. Ring parameter t: 0 = eave line, 1 = top, negative = the up-flicked lip beyond the eave.
        /// </summary>
        public readonly struct PagodaRoofSpec
        {
            public const int Segments = 6;
            public const float LipT = -0.06f;
            public readonly float EaveW, EaveD, TopW, TopD, Height, CornerLift;

            public PagodaRoofSpec(float eaveHalfW, float eaveHalfD, float topHalfW, float topHalfD, float height, float cornerLift)
            {
                EaveW = eaveHalfW; EaveD = eaveHalfD; TopW = topHalfW; TopD = topHalfD; Height = height; CornerLift = cornerLift;
            }

            /// <summary>Height of the slope above the eave line at ring parameter t (on the middle of a side, corner lift excluded).</summary>
            public float Profile(float t) => Height * Mathf.Pow(Mathf.Clamp01(t), 1.45f);

            /// <summary>Slope (rise per unit of horizontal run towards the top) at ring parameter t, measured along the depth axis.</summary>
            public float SlopeD(float t) => Height * 1.45f * Mathf.Pow(Mathf.Clamp01(t), 0.45f) / Mathf.Max(0.01f, EaveD - TopD);

            /// <summary>Point on ring t at perimeter index p (0 .. 4*Segments-1; corner c is index c*Segments, corners run -x-z, +x-z, +x+z, -x+z).</summary>
            public Vector3 Point(float t, int p)
            {
                float hx = Mathf.LerpUnclamped(EaveW, TopW, t);
                float hz = Mathf.LerpUnclamped(EaveD, TopD, t);
                int side = (p / Segments) % 4;
                float a = (p % Segments) / (float)Segments;
                float x, z;
                switch (side)
                {
                    case 0: x = Mathf.Lerp(-hx, hx, a); z = -hz; break;
                    case 1: x = hx; z = Mathf.Lerp(-hz, hz, a); break;
                    case 2: x = Mathf.Lerp(hx, -hx, a); z = hz; break;
                    default: x = -hx; z = Mathf.Lerp(hz, -hz, a); break;
                }
                float ux = hx > 1e-4f ? Mathf.Abs(x) / hx : 0f;
                float uz = hz > 1e-4f ? Mathf.Abs(z) / hz : 0f;
                float w = Mathf.Pow(Mathf.Clamp01(ux), 5f) * Mathf.Pow(Mathf.Clamp01(uz), 5f);
                float fade = 1f - Mathf.Max(0f, t);
                float lift = CornerLift * w * fade * fade;
                float push = lift * 0.6f;
                x += Mathf.Sign(x) * push * (Mathf.Abs(x) > 1e-4f ? 1f : 0f);
                z += Mathf.Sign(z) * push * (Mathf.Abs(z) > 1e-4f ? 1f : 0f);
                float y = Profile(t) + lift + (t < 0f ? 0.035f : 0f);
                return new Vector3(x, y, z);
            }

            /// <summary>Point on the hip line running into corner <paramref name="corner"/> at ring parameter t.</summary>
            public Vector3 Hip(float t, int corner) => Point(t, corner * Segments);
        }

        private static readonly float[] PagodaRings = { PagodaRoofSpec.LipT, 0f, 0.1f, 0.25f, 0.45f, 0.7f, 1f };

        /// <summary>World length of one uv unit along / up the slope (see <see cref="PaintedTexture.RoofTiles"/>: 4 columns, 7 rows).</summary>
        private const float TileU = 0.5f, TileV = 0.84f; // 4 columns of 0.125 and 7 rows of 0.12

        /// <summary>
        /// Roof shell of a <see cref="PagodaRoofSpec"/> with a visible edge board of thickness <paramref name="thick"/>; closed, so the outline pass closes it.
        /// Every slope has its own vertices (crisp hips) and uv0 for <see cref="PaintedTexture.RoofTiles"/>: u along the eave, v up the slope.
        /// </summary>
        public static Mesh PagodaRoof(PagodaRoofSpec spec, float thick = 0.07f)
        {
            int S = PagodaRoofSpec.Segments, N = 4 * S, per = S + 1;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            var down = new Vector3(0f, -thick, 0f);

            // top surface: four slopes, rings shared inside a slope
            var slopeV = new float[4 * per];
            for (int r = 0; r < PagodaRings.Length; r++)
            {
                for (int side = 0; side < 4; side++)
                    for (int i = 0; i < per; i++)
                    {
                        var pos = spec.Point(PagodaRings[r], (side * S + i) % N);
                        int column = side * per + i;
                        if (r > 0) slopeV[column] += (pos - verts[(r - 1) * 4 * per + column]).magnitude;
                        float along = side % 2 == 0 ? pos.x : pos.z;
                        verts.Add(pos);
                        uvs.Add(new Vector2(along / TileU + side * 0.37f, slopeV[column] / TileV));
                    }
            }
            for (int r = 0; r + 1 < PagodaRings.Length; r++)
                for (int side = 0; side < 4; side++)
                    for (int i = 0; i < S; i++)
                    {
                        int bl = r * 4 * per + side * per + i, br = bl + 1;
                        int tl = bl + 4 * per, tr = br + 4 * per;
                        tris.Add(bl); tris.Add(tl); tris.Add(tr);
                        tris.Add(bl); tris.Add(tr); tris.Add(br);
                    }

            // edge board: vertical band under the lip, own vertices so it stays a hard, dark face (its uv shows the tile ends)
            for (int p = 0; p < N; p++)
            {
                int n = (p + 1) % N;
                var a = spec.Point(PagodaRoofSpec.LipT, p); var b = spec.Point(PagodaRoofSpec.LipT, n);
                float ua = (a.x + a.z) / TileU, ub = (b.x + b.z) / TileU;
                Facing(verts, tris, a, b, b + down, a + down, (a + b) * 0.5f - new Vector3(0f, a.y, 0f), uvs, new Vector2(ua, 0.55f), new Vector2(ub, 0.55f), new Vector2(ub, 0.4f), new Vector2(ua, 0.4f));
            }

            // underside (a slightly lowered copy, three rings are enough: nobody sees it, the outline pass only needs a closed hull)
            float[] under = { PagodaRoofSpec.LipT, 0.45f, 1f };
            for (int r = 0; r + 1 < under.Length; r++)
                for (int p = 0; p < N; p++)
                {
                    int n = (p + 1) % N;
                    Facing(verts, tris,
                        spec.Point(under[r], p) + down, spec.Point(under[r], n) + down,
                        spec.Point(under[r + 1], n) + down, spec.Point(under[r + 1], p) + down, Vector3.down, uvs);
                }

            // top plateau, its rim down to the underside, and the underside plateau
            Vector3 c0 = spec.Point(1f, 0), c1 = spec.Point(1f, S), c2 = spec.Point(1f, 2 * S), c3 = spec.Point(1f, 3 * S);
            Facing(verts, tris, c0, c1, c2, c3, Vector3.up, uvs);
            Facing(verts, tris, c0 + down, c1 + down, c2 + down, c3 + down, Vector3.down, uvs);
            for (int p = 0; p < N; p++)
            {
                int n = (p + 1) % N;
                var a = spec.Point(1f, p); var b = spec.Point(1f, n);
                Facing(verts, tris, a, b, b + down, a + down, (a + b) * 0.5f - new Vector3(0f, a.y, 0f), uvs);
            }

            var mesh = new Mesh { name = "PagodaRoof" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            return ToonMeshes.Finish(mesh);
        }

        /// <summary>
        /// Adds a flat quad (own vertices) whose front faces along <paramref name="outward"/>. When <paramref name="uvs"/> is given it receives one
        /// uv per vertex (the four values, or zeros), so callers can keep a uv list parallel to the vertices.
        /// </summary>
        private static void Facing(List<Vector3> verts, List<int> tris, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward,
            List<Vector2> uvs = null, Vector2 uvA = default, Vector2 uvB = default, Vector2 uvC = default, Vector2 uvD = default)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            if (uvs != null) { uvs.Add(uvA); uvs.Add(uvB); uvs.Add(uvC); uvs.Add(uvD); }
            var n = Vector3.Cross(b - a, c - a) + Vector3.Cross(c - a, d - a);
            if (Vector3.Dot(n, outward) >= 0f) { tris.Add(i); tris.Add(i + 1); tris.Add(i + 2); tris.Add(i); tris.Add(i + 2); tris.Add(i + 3); }
            else { tris.Add(i); tris.Add(i + 2); tris.Add(i + 1); tris.Add(i); tris.Add(i + 3); tris.Add(i + 2); }
        }

        /// <summary>
        /// Round tube along a path with a radius per point (ridge caps, hip ridges, tapering tails). Ends are closed with a point.
        /// </summary>
        public static Mesh Tube(IList<Vector3> path, IList<float> radii, int sides = 5, float capReach = 0.9f)
        {
            int n = path.Count;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var frameN = Vector3.zero;
            for (int i = 0; i < n; i++)
            {
                var tangent = (path[Mathf.Min(i + 1, n - 1)] - path[Mathf.Max(i - 1, 0)]).normalized;
                if (i == 0)
                {
                    var reference = Mathf.Abs(tangent.y) > 0.9f ? Vector3.right : Vector3.up;
                    frameN = Vector3.Cross(reference, tangent).normalized;
                }
                else frameN = (frameN - tangent * Vector3.Dot(frameN, tangent)).normalized;
                var frameB = Vector3.Cross(tangent, frameN);
                for (int s = 0; s < sides; s++)
                {
                    float a = s * Mathf.PI * 2f / sides;
                    verts.Add(path[i] + (frameN * Mathf.Cos(a) + frameB * Mathf.Sin(a)) * radii[i]);
                }
            }
            for (int i = 0; i + 1 < n; i++)
                for (int s = 0; s < sides; s++)
                {
                    int t = (s + 1) % sides;
                    int a = i * sides + s, b = i * sides + t, c = (i + 1) * sides + t, d = (i + 1) * sides + s;
                    var hint = (verts[a] + verts[b] + verts[c] + verts[d]) * 0.25f - (path[i] + path[i + 1]) * 0.5f;
                    var face = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
                    if (Vector3.Dot(face, hint) >= 0f) { tris.Add(a); tris.Add(b); tris.Add(c); tris.Add(a); tris.Add(c); tris.Add(d); }
                    else { tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(a); tris.Add(d); tris.Add(c); }
                }
            // end caps: a point slightly beyond the last ring, so the tip is rounded off
            void Cap(int ring, Vector3 outward, float reach)
            {
                int apex = verts.Count;
                verts.Add(path[ring] + outward * reach * radii[ring]);
                for (int s = 0; s < sides; s++)
                {
                    int a = ring * sides + s, b = ring * sides + (s + 1) % sides;
                    var face = Vector3.Cross(verts[b] - verts[a], verts[apex] - verts[a]);
                    if (Vector3.Dot(face, outward) >= 0f) { tris.Add(a); tris.Add(b); tris.Add(apex); }
                    else { tris.Add(a); tris.Add(apex); tris.Add(b); }
                }
            }
            Cap(0, (path[0] - path[1]).normalized, capReach);
            Cap(n - 1, (path[n - 1] - path[n - 2]).normalized, capReach);
            var mesh = new Mesh { name = "Tube" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            return ToonMeshes.Finish(mesh);
        }

        /// <summary>Outline of a karahafu (cusped) gable: x across, y up; both ends at 0, an S-curve up to a gently peaked centre.</summary>
        public static Vector2[] KarahafuCurve(float width, float height, int samples = 12)
        {
            var pts = new Vector2[samples + 1];
            for (int i = 0; i <= samples; i++)
            {
                float x = -width * 0.5f + width * i / samples;
                float s = 1f - Mathf.Abs(x) / (width * 0.5f);
                float smooth = s * s * (3f - 2f * s);
                pts[i] = new Vector2(x, height * (0.86f * smooth + 0.14f * Mathf.Pow(s, 6f)));
            }
            return pts;
        }

        /// <summary>Front board of a gable: the region under <paramref name="curve"/> (down to -sink), thickness along +Z, front face towards -Z.</summary>
        public static Mesh GableBoard(Vector2[] curve, float sink, float thick)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i + 1 < curve.Length; i++)
            {
                Vector3 a = new Vector3(curve[i].x, curve[i].y, 0f), b = new Vector3(curve[i + 1].x, curve[i + 1].y, 0f);
                Vector3 a0 = new Vector3(a.x, -sink, 0f), b0 = new Vector3(b.x, -sink, 0f);
                var t = new Vector3(0f, 0f, thick);
                Facing(verts, tris, a0, b0, b, a, Vector3.back);
                Facing(verts, tris, a0 + t, b0 + t, b + t, a + t, Vector3.forward);
                Facing(verts, tris, a, b, b + t, a + t, Vector3.up);
                Facing(verts, tris, a0, b0, b0 + t, a0 + t, Vector3.down);
            }
            var first = curve[0]; var last = curve[curve.Length - 1];
            Facing(verts, tris, new Vector3(first.x, -sink, 0f), new Vector3(first.x, -sink, thick), new Vector3(first.x, first.y, thick), new Vector3(first.x, first.y, 0f), Vector3.left);
            Facing(verts, tris, new Vector3(last.x, -sink, 0f), new Vector3(last.x, -sink, thick), new Vector3(last.x, last.y, thick), new Vector3(last.x, last.y, 0f), Vector3.right);
            var mesh = new Mesh { name = "GableBoard" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            return ToonMeshes.Finish(mesh);
        }

        /// <summary>
        /// Roof over a karahafu gable: the outline (lifted by <paramref name="over"/>, pushed forward) swept along (0, rise, run),
        /// i.e. up the main roof and into the wall of the next storey. A thin closed shell.
        /// </summary>
        public static Mesh GableCap(Vector2[] curve, float over, float run, float rise, float thick)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            var sweep = new Vector3(0f, rise, run);
            float length = sweep.magnitude / TileV;
            var down = new Vector3(0f, -thick, 0f);
            Vector3 P(int i) => new Vector3(curve[i].x, curve[i].y + over, -over * 0.9f);
            float arc = 0f;
            for (int i = 0; i + 1 < curve.Length; i++)
            {
                Vector3 a = P(i), b = P(i + 1);
                float ua = arc / TileU;
                arc += (b - a).magnitude;
                float ub = arc / TileU;
                Facing(verts, tris, a, b, b + sweep, a + sweep, Vector3.up, uvs, new Vector2(ua, 0f), new Vector2(ub, 0f), new Vector2(ub, length), new Vector2(ua, length));
                Facing(verts, tris, a + down, b + down, b + sweep + down, a + sweep + down, Vector3.down, uvs);
                Facing(verts, tris, a, b, b + down, a + down, Vector3.back, uvs, new Vector2(ua, 0.55f), new Vector2(ub, 0.55f), new Vector2(ub, 0.4f), new Vector2(ua, 0.4f));
                Facing(verts, tris, a + sweep, b + sweep, b + sweep + down, a + sweep + down, Vector3.forward, uvs);
            }
            Vector3 f = P(0), l = P(curve.Length - 1);
            Facing(verts, tris, f, f + sweep, f + sweep + down, f + down, Vector3.left, uvs);
            Facing(verts, tris, l, l + sweep, l + sweep + down, l + down, Vector3.right, uvs);
            var mesh = new Mesh { name = "GableCap" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            return ToonMeshes.Finish(mesh);
        }

        private const float GableShellThickness = 0.025f;

        /// <summary>Adds the quad a-b-c-d as two triangles, winding chosen so its normal points along <paramref name="outward"/>.</summary>
        private static void AddOutwardQuad(List<Vector3> verts, List<int> tris, int a, int b, int c, int d, Vector3 outward)
        {
            bool flip = Vector3.Dot(Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]), outward) < 0f;
            if (flip) { int t = b; b = d; d = t; }
            tris.Add(a); tris.Add(b); tris.Add(c);
            tris.Add(a); tris.Add(c); tris.Add(d);
        }

        private static Vector3[] Corners(Vector3 ring)
        {
            // counter-clockwise when seen from above; side i runs from corner i to corner i+1
            return new[]
            {
                new Vector3(-ring.x, ring.z, -ring.y),
                new Vector3(ring.x, ring.z, -ring.y),
                new Vector3(ring.x, ring.z, ring.y),
                new Vector3(-ring.x, ring.z, ring.y)
            };
        }
    }
}
