using System.Collections.Generic;
using UnityEngine;

namespace AgeOfSakura.Game
{
    /// <summary>
    /// The paving of one road cell: a stone-edged cobble patch that grows an arm toward every neighbouring road (or bridge, or the Town Hall),
    /// so a row of single cells reads as one continuous road. There are only 16 shapes (one per neighbour mask); the meshes are built once and shared.
    /// The paint is projected from world space, so the stones run on across cell borders without seams.
    /// </summary>
    public sealed class RoadSurface : MonoBehaviour
    {
        public const int East = 1;
        public const int North = 2;
        public const int West = 4;
        public const int South = 8;

        private static readonly Dictionary<int, Mesh> Cache = new Dictionary<int, Mesh>();

        private MeshFilter filter;

        public int Mask { get; private set; } = -1;

        public void Setup(Material border, Material top)
        {
            filter = GetComponent<MeshFilter>();
            GetComponent<MeshRenderer>().sharedMaterials = new[] { border, top };
        }

        public void SetMask(int mask)
        {
            if (mask == Mask) return;
            Mask = mask;
            filter.sharedMesh = MeshFor(mask);
        }

        public static Mesh MeshFor(int mask)
        {
            if (Cache.TryGetValue(mask, out var cached) && cached != null) return cached;

            var verts = new List<Vector3>();
            var borderTris = new List<int>();
            var topTris = new List<int>();
            AddLayer(verts, borderTris, 0.012f, 0.37f, 0.14f, 0.35f, mask);
            AddLayer(verts, topTris, 0.024f, 0.31f, 0.12f, 0.29f, mask);

            var mesh = new Mesh { name = "RoadSurface_" + mask, subMeshCount = 2 };
            mesh.SetVertices(verts);
            mesh.SetTriangles(borderTris, 0);
            mesh.SetTriangles(topTris, 1);
            var normals = new Vector3[verts.Count];
            for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
            mesh.normals = normals;
            ToonMeshes.Finish(mesh);
            Cache[mask] = mesh;
            return mesh;
        }

        /// <summary>One layer: a chamfered square in the middle and a straight arm to the edge of the cell for every bit of the mask.</summary>
        private static void AddLayer(List<Vector3> verts, List<int> tris, float y, float half, float cut, float armHalfWidth, int mask)
        {
            AddPolygon(verts, tris, y, new[]
            {
                new Vector2(-half + cut, half), new Vector2(half - cut, half), new Vector2(half, half - cut), new Vector2(half, -half + cut),
                new Vector2(half - cut, -half), new Vector2(-half + cut, -half), new Vector2(-half, -half + cut), new Vector2(-half, half - cut)
            });
            const float edge = 0.5f;
            float inner = half - 0.06f;
            if ((mask & East) != 0) AddPolygon(verts, tris, y, Rect(inner, edge, -armHalfWidth, armHalfWidth));
            if ((mask & West) != 0) AddPolygon(verts, tris, y, Rect(-edge, -inner, -armHalfWidth, armHalfWidth));
            if ((mask & North) != 0) AddPolygon(verts, tris, y, Rect(-armHalfWidth, armHalfWidth, inner, edge));
            if ((mask & South) != 0) AddPolygon(verts, tris, y, Rect(-armHalfWidth, armHalfWidth, -edge, -inner));
        }

        private static Vector2[] Rect(float x0, float x1, float z0, float z1) =>
            new[] { new Vector2(x0, z1), new Vector2(x1, z1), new Vector2(x1, z0), new Vector2(x0, z0) };

        /// <summary>Fan-triangulates a convex polygon so that it faces up, whichever way round its corners were given.</summary>
        private static void AddPolygon(List<Vector3> verts, List<int> tris, float y, Vector2[] points)
        {
            float area = 0f;
            for (int i = 0; i < points.Length; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % points.Length];
                area += a.x * b.y - b.x * a.y;
            }
            if (area > 0f) System.Array.Reverse(points); // counter-clockwise seen from above would face down

            int first = verts.Count;
            foreach (var p in points) verts.Add(new Vector3(p.x, y, p.y));
            for (int i = 1; i + 1 < points.Length; i++)
            {
                tris.Add(first);
                tris.Add(first + i);
                tris.Add(first + i + 1);
            }
        }
    }
}
