using System;
using System.Collections.Generic;
using UnityEngine;

namespace AgeOfSakura.Game
{
    public enum FoliagePalette
    {
        None,
        Leaf,
        Blossom,
        Birch,
        Spruce,
        Old
    }

    /// <summary>
    /// Generated meshes for the 3D-Toon style: rounded boxes and cylinders (never hard cube edges), smooth icosphere blobs for foliage,
    /// camera-facing leaf cards. Every mesh carries the same vertex layout (position, normal, uv0, colour, uv3 = smoothed outline
    /// direction) so static batching can merge them and the outline pass never tears open at hard edges.
    /// </summary>
    public static class ToonMeshes
    {
        private static readonly Dictionary<long, Mesh> Cache = new Dictionary<long, Mesh>();

        // ------------------------------------------------------------------ vertex layout

        /// <summary>
        /// Completes a mesh: fills missing uv/colour channels and bakes the smoothed outline direction into uv3
        /// (normals of vertices sharing a position are averaged, so flat-shaded hard edges still extrude as one closed hull).
        /// </summary>
        public static Mesh Finish(Mesh mesh)
        {
            var verts = mesh.vertices;
            var normals = mesh.normals;
            if (normals == null || normals.Length != verts.Length)
            {
                mesh.RecalculateNormals();
                normals = mesh.normals;
            }

            var sums = new Dictionary<Vector3Int, Vector3>(verts.Length);
            for (int i = 0; i < verts.Length; i++)
            {
                var key = Quantize(verts[i]);
                sums.TryGetValue(key, out var sum);
                sums[key] = sum + normals[i];
            }
            var outline = new List<Vector3>(verts.Length);
            for (int i = 0; i < verts.Length; i++)
            {
                var sum = sums[Quantize(verts[i])];
                outline.Add(sum.sqrMagnitude > 1e-8f ? sum.normalized : normals[i]);
            }
            mesh.SetUVs(3, outline);

            if (mesh.uv == null || mesh.uv.Length != verts.Length) mesh.uv = new Vector2[verts.Length];
            if (mesh.colors32 == null || mesh.colors32.Length != verts.Length)
            {
                var white = new Color32[verts.Length];
                for (int i = 0; i < white.Length; i++) white[i] = new Color32(255, 255, 255, 255);
                mesh.colors32 = white;
            }
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3Int Quantize(Vector3 p) =>
            new Vector3Int(Mathf.RoundToInt(p.x * 2000f), Mathf.RoundToInt(p.y * 2000f), Mathf.RoundToInt(p.z * 2000f));

        private static long Key(int kind, int a, int b, int c)
        {
            unchecked { return (((long)kind * 1000003L + a) * 1000003L + b) * 1000003L + c; }
        }

        // ------------------------------------------------------------------ rounded box

        /// <summary>
        /// Box of exactly <paramref name="size"/>, centred on the origin, with rounded edges and corners (analytic smooth normals).
        /// The bevel radius follows the thinnest side so thin timber keeps a soft but visible edge.
        /// </summary>
        public static Mesh RoundedBox(Vector3 size)
        {
            int kx = Mathf.Max(1, Mathf.RoundToInt(size.x * 1000f));
            int ky = Mathf.Max(1, Mathf.RoundToInt(size.y * 1000f));
            int kz = Mathf.Max(1, Mathf.RoundToInt(size.z * 1000f));
            long key = Key(1, kx, ky, kz);
            if (Cache.TryGetValue(key, out var cached)) return cached;

            var half = new Vector3(kx, ky, kz) * 0.0005f;
            float radius = Mathf.Min(0.07f, Mathf.Min(half.x, Mathf.Min(half.y, half.z)) * 0.8f);
            var mesh = BuildRoundedBox(half, radius);
            mesh.name = $"RoundedBox_{kx}x{ky}x{kz}";
            Cache[key] = mesh;
            return mesh;
        }

        private static Mesh BuildRoundedBox(Vector3 half, float r)
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            var inner = new Vector3(half.x - r, half.y - r, half.z - r);

            for (int axis = 0; axis < 3; axis++)
            {
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    int u = (axis + 1) % 3;
                    int v = (axis + 2) % 3;
                    int start = verts.Count;
                    for (int j = 0; j < 4; j++)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            var raw = Vector3.zero;
                            raw[axis] = sign * half[axis];
                            raw[u] = Tick(i, half[u], inner[u]);
                            raw[v] = Tick(j, half[v], inner[v]);
                            var c = new Vector3(
                                Mathf.Clamp(raw.x, -inner.x, inner.x),
                                Mathf.Clamp(raw.y, -inner.y, inner.y),
                                Mathf.Clamp(raw.z, -inner.z, inner.z));
                            var n = (raw - c).normalized;
                            verts.Add(c + n * r);
                            normals.Add(n);
                        }
                    }
                    for (int j = 0; j < 3; j++)
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            int a = start + j * 4 + i;
                            AddOutwardQuad(verts, normals, tris, a, a + 1, a + 5, a + 4);
                        }
                    }
                }
            }

            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            return Finish(mesh);
        }

        private static float Tick(int i, float half, float inner)
        {
            switch (i)
            {
                case 0: return -half;
                case 1: return -inner;
                case 2: return inner;
                default: return half;
            }
        }

        /// <summary>Adds a quad (a,b,c,d in loop order), flipping the winding so it faces along the vertex normals.</summary>
        private static void AddOutwardQuad(List<Vector3> verts, List<Vector3> normals, List<int> tris, int a, int b, int c, int d)
        {
            // a quad collapsed to a line at one side (cap centre rings) has one degenerate triangle: judge by the larger of the two
            var facing = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
            var facing2 = Vector3.Cross(verts[c] - verts[a], verts[d] - verts[a]);
            if (facing2.sqrMagnitude > facing.sqrMagnitude) facing = facing2;
            var n = normals[a] + normals[b] + normals[c] + normals[d];
            if (Vector3.Dot(facing, n) >= 0f)
            {
                tris.Add(a); tris.Add(b); tris.Add(c);
                tris.Add(a); tris.Add(c); tris.Add(d);
            }
            else
            {
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(a); tris.Add(d); tris.Add(c);
            }
        }

        // ------------------------------------------------------------------ rounded cylinder

        /// <summary>Upright cylinder of exactly the given radius and height, centred on the origin, with rounded rims.</summary>
        public static Mesh RoundedCylinder(float radius, float height)
        {
            int kr = Mathf.Max(1, Mathf.RoundToInt(radius * 200f));   // 5 mm steps
            int kh = Mathf.Max(1, Mathf.RoundToInt(height * 100f));   // 1 cm steps
            long key = Key(2, kr, kh, 0);
            if (Cache.TryGetValue(key, out var cached)) return cached;

            float rr = kr * 0.005f;
            float hh = kh * 0.01f;
            var mesh = BuildRoundedCylinder(rr, hh, rr > 0.05f ? 12 : 8);
            mesh.name = $"RoundedCylinder_{kr}x{kh}";
            Cache[key] = mesh;
            return mesh;
        }

        private static Mesh BuildRoundedCylinder(float radius, float height, int sides)
        {
            float half = height * 0.5f;
            float b = Mathf.Min(0.035f, Mathf.Min(radius, half) * 0.45f);

            // profile from the bottom centre around the rim to the top centre: (radius, y, normal radius, normal y)
            var profile = new List<Vector4> { new Vector4(0f, -half, 0f, -1f) };
            for (int k = 0; k <= 2; k++)
            {
                float a = k * Mathf.PI * 0.25f;
                float s = Mathf.Sin(a), c = Mathf.Cos(a);
                profile.Add(new Vector4(radius - b + b * s, -half + b - b * c, s, -c));
            }
            for (int k = 2; k >= 0; k--)
            {
                float a = k * Mathf.PI * 0.25f;
                float s = Mathf.Sin(a), c = Mathf.Cos(a);
                profile.Add(new Vector4(radius - b + b * s, half - b + b * c, s, c));
            }
            profile.Add(new Vector4(0f, half, 0f, 1f));

            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            for (int p = 0; p < profile.Count; p++)
            {
                var pr = profile[p];
                for (int s = 0; s <= sides; s++)
                {
                    float ang = (float)s / sides * Mathf.PI * 2f;
                    float x = Mathf.Cos(ang), z = Mathf.Sin(ang);
                    verts.Add(new Vector3(x * pr.x, pr.y, z * pr.x));
                    normals.Add(new Vector3(x * pr.z, pr.w, z * pr.z).normalized);
                }
            }
            int ring = sides + 1;
            for (int p = 0; p + 1 < profile.Count; p++)
            {
                for (int s = 0; s < sides; s++)
                {
                    int a = p * ring + s;
                    AddOutwardQuad(verts, normals, tris, a, a + 1, a + ring + 1, a + ring);
                }
            }

            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            // degenerate triangles at the cap centres are harmless; drop them so the mesh stays clean
            return Finish(mesh);
        }

        // ------------------------------------------------------------------ cone

        /// <summary>Cone with base radius 0.5 at y=0 and tip at y=1 (scale it to size), smooth side normals.</summary>
        public static Mesh Cone(int sides)
        {
            long key = Key(3, sides, 0, 0);
            if (Cache.TryGetValue(key, out var cached)) return cached;

            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            // slope: the side normal leans up by atan(0.5 / 1)
            const float lean = 0.4472136f; // sin of the lean angle for radius 0.5, height 1
            float flat = Mathf.Sqrt(1f - lean * lean);
            for (int s = 0; s <= sides; s++)
            {
                float a = (float)s / sides * Mathf.PI * 2f;
                float x = Mathf.Cos(a), z = Mathf.Sin(a);
                verts.Add(new Vector3(x * 0.5f, 0f, z * 0.5f));
                normals.Add(new Vector3(x * flat, lean, z * flat));
                verts.Add(new Vector3(0f, 1f, 0f));
                normals.Add(new Vector3(x * flat, lean, z * flat));
            }
            for (int s = 0; s < sides; s++)
            {
                int a = s * 2;
                // a: base s, a+1: tip s, a+2: base s+1, a+3: tip s+1
                AddOutwardTri(verts, normals, tris, a, a + 1, a + 2);
            }
            int capStart = verts.Count;
            verts.Add(Vector3.zero);
            normals.Add(Vector3.down);
            for (int s = 0; s <= sides; s++)
            {
                float a = (float)s / sides * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(a) * 0.5f, 0f, Mathf.Sin(a) * 0.5f));
                normals.Add(Vector3.down);
            }
            for (int s = 0; s < sides; s++) AddOutwardTri(verts, normals, tris, capStart, capStart + 1 + s, capStart + 2 + s);

            var mesh = new Mesh { name = "ToonCone" + sides };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            Finish(mesh);
            Cache[key] = mesh;
            return mesh;
        }

        private static void AddOutwardTri(List<Vector3> verts, List<Vector3> normals, List<int> tris, int a, int b, int c)
        {
            var facing = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
            var n = normals[a] + normals[b] + normals[c];
            if (Vector3.Dot(facing, n) >= 0f) { tris.Add(a); tris.Add(b); tris.Add(c); }
            else { tris.Add(a); tris.Add(c); tris.Add(b); }
        }

        // ------------------------------------------------------------------ icosphere / foliage blobs

        /// <summary>Smooth sphere of diameter 1 (icosahedron subdivided <paramref name="subdivisions"/> times, shared vertices).</summary>
        public static Mesh Sphere(int subdivisions)
        {
            long key = Key(4, subdivisions, 0, 0);
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var mesh = Blob(subdivisions, 0, 0f, FoliagePalette.None);
            mesh.name = "ToonSphere" + subdivisions;
            Cache[key] = mesh;
            return mesh;
        }

        private static readonly Dictionary<long, Mesh> BlobCache = new Dictionary<long, Mesh>();

        /// <summary>
        /// Smooth lumpy sphere of diameter ~1. Seeded radial bumps give every clump its own silhouette; when a palette is given the
        /// vertex colours are baked from the sun direction along the style's foliage ramp, so foliage needs no realtime light.
        /// Cached per (subdivisions, seed, bumpiness, palette): all clumps with the same key share one mesh.
        /// </summary>
        public static Mesh Blob(int subdivisions, int seed, float bumpiness, FoliagePalette palette)
        {
            long key = Key(5, subdivisions, seed * 100 + (int)palette, Mathf.RoundToInt(bumpiness * 1000f));
            if (BlobCache.TryGetValue(key, out var cached)) return cached;

            var verts = new List<Vector3>();
            var tris = new List<int>();
            BuildIcosphere(subdivisions, verts, tris);

            var rng = new System.Random(seed * 7919 + 13);
            var phase = new Vector3((float)rng.NextDouble() * 10f, (float)rng.NextDouble() * 10f, (float)rng.NextDouble() * 10f);
            for (int i = 0; i < verts.Count; i++)
            {
                var d = verts[i].normalized;
                float bump = 0f;
                if (bumpiness > 0f)
                {
                    bump = Mathf.Sin(d.x * 3.1f + phase.x) * Mathf.Sin(d.y * 2.7f + phase.y) * 0.6f
                           + Mathf.Sin(d.z * 4.3f + phase.z + d.x * 1.7f) * 0.4f;
                }
                verts[i] = d * (0.5f * (1f + bump * bumpiness));
            }

            var mesh = new Mesh { name = $"Blob_{subdivisions}_{seed}_{palette}" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals(); // shared vertices: smooth, never facetted

            if (palette != FoliagePalette.None)
            {
                var normals = mesh.normals;
                var colors = new Color32[verts.Count];
                var sun = ToonStyle.SunDirTowardsSun;
                for (int i = 0; i < colors.Length; i++)
                {
                    var n = normals[i];
                    float lambert = Mathf.Clamp01(Vector3.Dot(n, sun) * 0.5f + 0.5f);
                    float sky = Mathf.Clamp01(n.y * 0.5f + 0.5f);
                    float t = Mathf.Clamp01(lambert * 0.65f + sky * 0.35f);
                    t = Mathf.Clamp01((t - 0.18f) / 0.72f);
                    colors[i] = ToonStyle.ToVertexColor(FoliageColor(palette, t));
                }
                mesh.colors32 = colors;
            }
            Finish(mesh);
            BlobCache[key] = mesh;
            return mesh;
        }

        private static void BuildIcosphere(int subdivisions, List<Vector3> verts, List<int> tris)
        {
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            verts.AddRange(new[]
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
            });
            for (int i = 0; i < verts.Count; i++) verts[i] = verts[i].normalized * 0.5f;
            var faces = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
                1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
                4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
            };
            for (int s = 0; s < subdivisions; s++)
            {
                var midpoints = new Dictionary<long, int>();
                var next = new List<int>(faces.Count * 4);
                for (int f = 0; f < faces.Count; f += 3)
                {
                    int a = faces[f], b = faces[f + 1], c = faces[f + 2];
                    int ab = Mid(verts, midpoints, a, b);
                    int bc = Mid(verts, midpoints, b, c);
                    int ca = Mid(verts, midpoints, c, a);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                faces = next;
            }
            // the table above winds clockwise seen from outside in a right-handed layout; Unity is left-handed, so flip when needed
            for (int f = 0; f < faces.Count; f += 3)
            {
                var n = Vector3.Cross(verts[faces[f + 1]] - verts[faces[f]], verts[faces[f + 2]] - verts[faces[f]]);
                var centre = verts[faces[f]] + verts[faces[f + 1]] + verts[faces[f + 2]];
                if (Vector3.Dot(n, centre) >= 0f) { tris.Add(faces[f]); tris.Add(faces[f + 1]); tris.Add(faces[f + 2]); }
                else { tris.Add(faces[f]); tris.Add(faces[f + 2]); tris.Add(faces[f + 1]); }
            }
        }

        private static int Mid(List<Vector3> verts, Dictionary<long, int> cache, int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (cache.TryGetValue(key, out int index)) return index;
            verts.Add(((verts[a] + verts[b]) * 0.5f).normalized * 0.5f);
            index = verts.Count - 1;
            cache[key] = index;
            return index;
        }

        // ------------------------------------------------------------------ foliage colour ramp

        /// <summary>Colour along the style's foliage ramp (stops 0, 0.38, 0.72, 1) for a bake value t in 0..1 (0 = deepest shade).</summary>
        public static Color FoliageColor(FoliagePalette palette, float t)
        {
            var stops = FoliageStops(palette);
            var ramp = ToonStyle.FoliageRamp;
            t = Mathf.Clamp01(t);
            for (int i = 0; i + 1 < ramp.Length; i++)
            {
                if (t <= ramp[i + 1])
                {
                    float k = Mathf.InverseLerp(ramp[i], ramp[i + 1], t);
                    return Color.Lerp(stops[i], stops[i + 1], k);
                }
            }
            return stops[stops.Length - 1];
        }

        /// <summary>
        /// Four colours, dark to light. Leaf/Blossom use the style palette directly (shade = the darkest palette colour darkened);
        /// Birch, Spruce and Old are derived from the same greens (allowed by the review checklist: "begründete Ableitung").
        /// </summary>
        public static Color[] FoliageStops(FoliagePalette palette)
        {
            var g = ToonStyle.LeafGreens;
            var p = ToonStyle.BlossomPinks;
            switch (palette)
            {
                case FoliagePalette.Blossom:
                    return new[] { Dark(p[1], 0.78f), p[1], p[2], p[0] };
                case FoliagePalette.Birch:
                    return new[] { Dark(g[1], 0.85f), g[1], Color.Lerp(g[1], g[0], 0.6f), g[0] };
                case FoliagePalette.Spruce:
                    return new[] { Dark(g[2], 0.5f), Dark(g[2], 0.72f), g[2], Color.Lerp(g[2], g[1], 0.55f) };
                case FoliagePalette.Old:
                    return new[] { Dark(ToonStyle.Moss, 0.62f), Dark(ToonStyle.Moss, 0.85f), ToonStyle.Moss, g[1] };
                default:
                    return new[] { Dark(g[2], 0.74f), g[2], g[1], g[0] };
            }
        }

        private static Color Dark(Color c, float f) => new Color(c.r * f, c.g * f, c.b * f, 1f);

        // ------------------------------------------------------------------ flat helpers

        /// <summary>Unit quad in the XY plane facing -Z (towards a camera that looks along +Z), uv 0..1, for leaf cards.</summary>
        public static Mesh Card()
        {
            long key = Key(6, 0, 0, 0);
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var mesh = new Mesh { name = "LeafCard" };
            mesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(0.5f, -0.5f, 0f) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            Finish(mesh);
            Cache[key] = mesh;
            return mesh;
        }

        /// <summary>Completes a procedurally built mesh (used by roofs and the ground quad).</summary>
        public static Mesh Prepare(Mesh mesh) => Finish(mesh);
    }
}
