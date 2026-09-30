#if UNITY_EDITOR
using AgeOfSakura.Core;
using AgeOfSakura.Game;
using NUnit.Framework;
using UnityEngine;

namespace AgeOfSakura.Tests
{
    /// <summary>
    /// Guards the 3D-Toon style (Docs/grafikstil-3d-toon.md): generated meshes must be closed, smooth and outline-ready, the code's
    /// style constants must match the style document, and the camera data must not drift away from the fixed isometric view.
    /// Unity-only (meshes), so the file is compiled out of the plain `dotnet test` project.
    /// </summary>
    public class ToonStyleTests
    {
        private static void AssertOutlineReady(Mesh mesh, string what)
        {
            Assert.Greater(mesh.vertexCount, 0, what);
            var normals = mesh.normals;
            var outline = new System.Collections.Generic.List<Vector3>();
            mesh.GetUVs(3, outline);
            Assert.AreEqual(mesh.vertexCount, normals.Length, what + ": normals");
            Assert.AreEqual(mesh.vertexCount, outline.Count, what + ": outline directions (uv3)");
            Assert.AreEqual(mesh.vertexCount, mesh.colors32.Length, what + ": vertex colours");
            Assert.AreEqual(mesh.vertexCount, mesh.uv.Length, what + ": uv0");
            for (int i = 0; i < normals.Length; i++)
            {
                Assert.AreEqual(1f, normals[i].magnitude, 0.01f, $"{what}: normal {i} is not unit length");
                Assert.AreEqual(1f, outline[i].magnitude, 0.01f, $"{what}: outline direction {i} is not unit length");
            }
        }

        /// <summary>Every triangle must face the same way as its vertex normals (otherwise it is culled and the model has holes).</summary>
        private static void AssertWindingOutward(Mesh mesh, string what)
        {
            var v = mesh.vertices;
            var n = mesh.normals;
            var t = mesh.triangles;
            for (int i = 0; i < t.Length; i += 3)
            {
                var facing = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                if (facing.sqrMagnitude < 1e-12f) continue; // degenerate triangle at a cap centre
                var normal = n[t[i]] + n[t[i + 1]] + n[t[i + 2]];
                Assert.Greater(Vector3.Dot(facing, normal), 0f, $"{what}: triangle {i / 3} faces inwards");
            }
        }

        [Test]
        public void RoundedBox_HasTheRequestedSize_IsSmoothAndOutlineReady()
        {
            var size = new Vector3(2.3f, 0.78f, 1.4f);
            var mesh = ToonMeshes.RoundedBox(size);
            var b = mesh.bounds;
            Assert.AreEqual(size.x, b.size.x, 0.002f);
            Assert.AreEqual(size.y, b.size.y, 0.002f);
            Assert.AreEqual(size.z, b.size.z, 0.002f);
            Assert.AreEqual(0f, b.center.magnitude, 0.002f);
            AssertOutlineReady(mesh, "RoundedBox");
            AssertWindingOutward(mesh, "RoundedBox");
            Assert.AreSame(mesh, ToonMeshes.RoundedBox(size), "same size must reuse the cached mesh");
        }

        [Test]
        public void RoundedBox_NeverHasHardCubeCorners()
        {
            var mesh = ToonMeshes.RoundedBox(Vector3.one);
            // a hard cube corner would have a normal on a coordinate axis only at the corner vertex; rounded corners have 45 degree normals
            var normals = mesh.normals;
            bool hasDiagonal = false;
            foreach (var n in normals) if (Mathf.Abs(n.x) > 0.3f && Mathf.Abs(n.y) > 0.3f) hasDiagonal = true;
            Assert.IsTrue(hasDiagonal, "the box edges must carry diagonal (rounded) normals");
            foreach (var p in mesh.vertices)
                Assert.LessOrEqual(Mathf.Max(Mathf.Abs(p.x), Mathf.Max(Mathf.Abs(p.y), Mathf.Abs(p.z))), 0.5001f);
        }

        [Test]
        public void RoundedCylinder_HasTheRequestedSize_IsSmoothAndOutlineReady()
        {
            var mesh = ToonMeshes.RoundedCylinder(0.25f, 0.6f);
            Assert.AreEqual(0.5f, mesh.bounds.size.x, 0.01f);
            Assert.AreEqual(0.6f, mesh.bounds.size.y, 0.011f);
            AssertOutlineReady(mesh, "RoundedCylinder");
            AssertWindingOutward(mesh, "RoundedCylinder");
        }

        [Test]
        public void Cone_IsOutlineReadyAndFacesOutwards()
        {
            var mesh = ToonMeshes.Cone(8);
            AssertOutlineReady(mesh, "Cone");
            AssertWindingOutward(mesh, "Cone");
            Assert.AreEqual(1f, mesh.bounds.size.y, 0.001f);
        }

        [Test]
        public void Sphere_HasIcosphereCounts_AndSmoothNormals()
        {
            for (int n = 0; n <= 2; n++)
            {
                var mesh = ToonMeshes.Sphere(n);
                int faces = 20 * (int)Mathf.Pow(4, n);
                Assert.AreEqual(faces * 3, mesh.triangles.Length, "triangles at subdivision " + n);
                Assert.AreEqual(10 * (int)Mathf.Pow(4, n) + 2, mesh.vertexCount, "shared vertices at subdivision " + n);
                AssertOutlineReady(mesh, "Sphere" + n);
                AssertWindingOutward(mesh, "Sphere" + n);
            }
        }

        [Test]
        public void FoliageBlob_BakesTheSunIntoVertexColoursAlongTheRamp()
        {
            var mesh = ToonMeshes.Blob(2, 1, 0.3f, FoliagePalette.Leaf);
            AssertOutlineReady(mesh, "Blob");
            var colors = mesh.colors32;
            var normals = mesh.normals;
            var sun = ToonStyle.SunDirTowardsSun;
            int lit = 0, shade = 0;
            float litSum = 0f, shadeSum = 0f;
            for (int i = 0; i < colors.Length; i++)
            {
                float brightness = colors[i].g; // greens dominate the palette
                float facing = Vector3.Dot(normals[i], sun);
                if (facing > 0.6f) { litSum += brightness; lit++; }
                else if (facing < -0.3f) { shadeSum += brightness; shade++; }
            }
            Assert.Greater(lit, 0);
            Assert.Greater(shade, 0);
            Assert.Greater(litSum / lit, shadeSum / shade, "surfaces facing the sun must be baked lighter than surfaces facing away");
        }

        [Test]
        public void FoliageRamp_HitsItsEndpointsAndGetsLighter()
        {
            foreach (FoliagePalette palette in new[] { FoliagePalette.Leaf, FoliagePalette.Blossom, FoliagePalette.Birch, FoliagePalette.Spruce, FoliagePalette.Old })
            {
                var stops = ToonMeshes.FoliageStops(palette);
                Assert.AreEqual(4, stops.Length);
                AssertColor(stops[0], ToonMeshes.FoliageColor(palette, 0f));
                AssertColor(stops[3], ToonMeshes.FoliageColor(palette, 1f));
                AssertColor(stops[1], ToonMeshes.FoliageColor(palette, 0.38f));
                AssertColor(stops[2], ToonMeshes.FoliageColor(palette, 0.72f));
                float previous = -1f;
                for (float t = 0f; t <= 1.0001f; t += 0.1f)
                {
                    float luma = ToonMeshes.FoliageColor(palette, t).grayscale;
                    Assert.GreaterOrEqual(luma, previous - 0.002f, $"{palette}: ramp must not get darker towards the light end (t={t})");
                    previous = luma;
                }
            }
        }

        private static void AssertColor(Color expected, Color actual)
        {
            Assert.AreEqual(expected.r, actual.r, 0.002f);
            Assert.AreEqual(expected.g, actual.g, 0.002f);
            Assert.AreEqual(expected.b, actual.b, 0.002f);
        }

        [Test]
        public void Roofs_AreOutlineReady_AndGableEndsStayOpen()
        {
            var hip = ProceduralMeshes.HipRoof(2f, 2f, 0.6f);
            var gable = ProceduralMeshes.GableRoof(2f, 1.6f, 0.6f);
            AssertOutlineReady(hip, "HipRoof");
            AssertOutlineReady(gable, "GableRoof");
            AssertOutlineReady(ProceduralMeshes.GableWall(1.2f, 0.5f, 0.05f), "GableWall");
            Assert.Less(gable.triangles.Length, ProceduralMeshes.Roof(2f, 1.6f, 0.6f, 1f, 0f, 0.35f, 0.05f, false).triangles.Length,
                "an open-ended gable has fewer faces than a closed one");
            AssertWindingOutward(hip, "HipRoof");
        }

        [Test]
        public void StyleConstants_MatchTheStyleDocument()
        {
            Assert.AreEqual("1.0.0", ToonStyle.Version);
            Assert.AreEqual(3, ToonStyle.GradientSteps);
            Assert.AreEqual(ToonStyle.GradientSteps, ToonStyle.GradientValues.Length);
            Assert.AreEqual(0.0058f, ToonStyle.OutlineThickness, 1e-6f);
            var o = ToonStyle.OutlineColor;
            Assert.AreEqual(0.16f, o.r, 1e-4f);
            Assert.AreEqual(0.09f, o.g, 1e-4f);
            Assert.AreEqual(0.04f, o.b, 1e-4f);
            Assert.Greater(o.r + o.g + o.b, 0f, "the outline is dark brown, never pure black");
            Assert.AreEqual(30f, ToonStyle.CameraElevationDegrees);
            Assert.AreEqual(45f, ToonStyle.CameraAzimuthDegrees);
            Assert.AreEqual(4.4f, ToonStyle.BuildInSeconds, 1e-5f);
            CollectionAssert.AreEqual(new[] { "Ground", "Body", "Roof", "Trees", "Props" }, ToonStyle.BuildOrder);
            Assert.AreEqual(ToonStyle.BuildOrder.Length, ToonStyle.BuildWindows.Length);
            Assert.AreEqual(0.45f, ToonStyle.LeafCardAlphaTest, 1e-5f);
            Assert.AreEqual(new Color32(0xc4, 0xf0, 0x83, 255), (Color32)ToonStyle.LeafGreens[0]);
        }

        [Test]
        public void SunDirection_PointsUpAndFromTheRightOfTheFixedCamera()
        {
            var sun = ToonStyle.SunDirTowardsSun;
            Assert.AreEqual(1f, sun.magnitude, 1e-4f);
            Assert.Greater(sun.y, 0.5f, "the sun stands high");
            var cameraRight = Quaternion.Euler(ToonStyle.CameraElevationDegrees, ToonStyle.CameraAzimuthDegrees, 0f) * Vector3.right;
            Assert.Greater(Vector3.Dot(sun, cameraRight), 0f, "the style's sun comes from the camera's right");
        }

        [Test]
        public void CameraData_FollowsTheFixedIsometricStyleCamera()
        {
            var defs = TestData.Definitions();
            Assert.AreEqual(ToonStyle.CameraElevationDegrees, defs.Camera.PitchDegrees, "game_definitions.json camera.pitchDegrees");
            Assert.AreEqual(ToonStyle.CameraAzimuthDegrees, defs.Camera.YawDegrees, "game_definitions.json camera.yawDegrees");
            Assert.AreEqual(ToonStyle.CameraElevationDegrees, new CameraConfig().PitchDegrees, "CameraConfig default");
        }
    }
}
#endif
