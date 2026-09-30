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

            // underside: separate vertices so it stays flat and dark
            int cap = verts.Count;
            verts.AddRange(Corners(rings[0]));
            tris.Add(cap); tris.Add(cap + 1); tris.Add(cap + 2);
            tris.Add(cap); tris.Add(cap + 2); tris.Add(cap + 3);

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
