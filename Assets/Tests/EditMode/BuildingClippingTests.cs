#if UNITY_EDITOR
using System.Collections.Generic;
using AgeOfSakura.Game;
using NUnit.Framework;
using UnityEngine;

namespace AgeOfSakura.Tests
{
    /// <summary>
    /// Clipping guards for the 3D-Toon world: trees and shrubs must never grow through buildings (neither the ones standing inside a model nor
    /// the sakura, oaks and boulders of the landscape next to buildable land), and every model must stay inside its footprint so that two
    /// neighbouring buildings cannot penetrate each other. Unity-only (physics queries, renderers), compiled out of `dotnet test`.
    /// </summary>
    public class BuildingClippingTests
    {
        private readonly List<GameObject> created = new List<GameObject>();
        private GameArt art;
        private Prim prim;
        private ProceduralBuildingModels models;
        private PropFactory props;

        [SetUp]
        public void SetUp()
        {
            art = new GameArt();
            prim = new Prim(art);
            var vfx = new VfxFactory(art);
            var host = new GameObject("TestHost");
            created.Add(host);
            props = new PropFactory(art, prim, host.AddComponent<WorldAnimator>(), vfx);
            models = new ProceduralBuildingModels(art, prim, vfx) { Props = props };
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in created) if (go != null) Object.DestroyImmediate(go);
            created.Clear();
        }

        private static (int w, int d) Footprint(string id) => id == "town_hall" ? (3, 3) : (2, 2);

        private GameObject Model(string id, int level)
        {
            var (w, d) = Footprint(id);
            var model = models.Create(id, level, w, d);
            created.Add(model);
            return model;
        }

        private static string GroupOf(Transform model, Transform t)
        {
            while (t != null && t.parent != model) t = t.parent;
            return t != null ? t.name : "?";
        }

        [Test]
        public void EveryModel_StaysInsideItsFootprint()
        {
            foreach (var id in ProceduralBuildingModels.KnownVisualIds)
            {
                for (int level = 1; level <= 3; level++)
                {
                    var (w, d) = Footprint(id);
                    var model = Model(id, level);
                    foreach (var r in model.GetComponentsInChildren<MeshRenderer>())
                    {
                        string group = GroupOf(model.transform, r.transform);
                        float tolerance = group == "Trees" ? 0.16f : 0.06f; // shrubs may lean a little over the edge: neighbours' roofs are inset by more
                        var b = r.bounds;
                        Assert.LessOrEqual(Mathf.Max(-b.min.x, b.max.x), w * 0.5f + tolerance, $"{id} level {level}: {group}/{r.name} leaves the footprint along X ({b.min.x:0.00}..{b.max.x:0.00})");
                        Assert.LessOrEqual(Mathf.Max(-b.min.z, b.max.z), d * 0.5f + tolerance, $"{id} level {level}: {group}/{r.name} leaves the footprint along Z ({b.min.z:0.00}..{b.max.z:0.00})");
                    }
                }
            }
        }

        [Test]
        public void TreesAndShrubsInsideModels_DoNotClipBuildingParts()
        {
            foreach (var id in ProceduralBuildingModels.KnownVisualIds)
            {
                for (int level = 1; level <= 3; level++)
                {
                    var model = Model(id, level);
                    var solids = new List<Collider>();
                    foreach (var r in model.GetComponentsInChildren<MeshRenderer>())
                    {
                        string group = GroupOf(model.transform, r.transform);
                        if (group == "Trees" || group == "Ground") continue; // trees stand in the ground, that is no clipping
                        var collider = r.gameObject.AddComponent<MeshCollider>();
                        collider.sharedMesh = r.GetComponent<MeshFilter>().sharedMesh;
                        solids.Add(collider);
                    }
                    Physics.SyncTransforms();

                    var trees = model.transform.Find("Trees");
                    if (trees == null) { DestroyModel(model); continue; }
                    foreach (var filter in trees.GetComponentsInChildren<MeshFilter>())
                    {
                        if (filter.GetComponent<MeshRenderer>().sharedMaterial.name.StartsWith("LeafCards")) continue; // flat cards hug the crown
                        var mesh = filter.sharedMesh;
                        var centre = mesh.bounds.center;
                        foreach (var v in mesh.vertices)
                        {
                            foreach (float k in new[] { 0.35f, 0.7f, 0.98f })
                            {
                                var world = filter.transform.TransformPoint(centre + (v - centre) * k);
                                var hits = Physics.OverlapSphere(world, 0.02f);
                                if (hits.Length == 0) continue;
                                var hitRenderer = hits[0].GetComponent<MeshRenderer>();
                                Assert.Fail($"{id} level {level}: {filter.transform.parent.name}/{filter.name} clips {GroupOf(model.transform, hits[0].transform)}/{hitRenderer.sharedMaterial.name} near {world.x:0.00},{world.y:0.00},{world.z:0.00}");
                            }
                        }
                    }
                    DestroyModel(model); // all models sit at the origin: leftover colliders would hit the next one
                }
            }
        }

        private void DestroyModel(GameObject model)
        {
            created.Remove(model);
            Object.DestroyImmediate(model);
            Physics.SyncTransforms();
        }

        [Test]
        public void SceneryNextToBuildableLand_StaysInsideItsCell()
        {
            props.MaxRadius = 0.47f;
            var rng = new System.Random(5);
            var parent = new GameObject("Scenery").transform;
            created.Add(parent.gameObject);
            var makers = new Dictionary<string, System.Func<GameObject>>
            {
                { "Spruce", () => props.Pine(parent, Vector3.zero, rng) },
                { "Oak", () => props.RoundTree(parent, Vector3.zero, rng) },
                { "Sakura", () => props.Cherry(parent, Vector3.zero, rng) },
                { "Birch", () => props.Birch(parent, Vector3.zero, rng) },
                { "Old tree", () => props.OldTree(parent, Vector3.zero, rng) },
                { "Bamboo", () => props.Bamboo(parent, Vector3.zero, rng) },
                { "Boulder", () => props.Rock(parent, Vector3.zero, rng, true) }
            };
            foreach (var pair in makers)
            {
                for (int i = 0; i < 25; i++)
                {
                    var prop = pair.Value();
                    float reach = 0f;
                    foreach (var r in prop.GetComponentsInChildren<MeshRenderer>())
                    {
                        var b = r.bounds;
                        reach = Mathf.Max(reach, Mathf.Abs(b.min.x), Mathf.Abs(b.max.x), Mathf.Abs(b.min.z), Mathf.Abs(b.max.z));
                    }
                    Assert.LessOrEqual(reach, 0.475f, $"{pair.Key} #{i} reaches {reach:0.00} from its trunk and could clip a neighbouring house");
                }
            }
        }

        [Test]
        public void TownHall_StaysWithinATriangleBudget()
        {
            for (int level = 1; level <= 3; level++)
            {
                var model = Model("town_hall", level);
                int triangles = 0, renderers = 0;
                foreach (var f in model.GetComponentsInChildren<MeshFilter>())
                {
                    if (!f.gameObject.activeInHierarchy || f.sharedMesh == null) continue;
                    triangles += f.sharedMesh.triangles.Length / 3;
                    renderers++;
                }
                TestContext.Out.WriteLine($"town_hall level {level}: {triangles} triangles in {renderers} renderers");
                Assert.LessOrEqual(triangles, 45000, $"town_hall level {level} is too heavy for a phone ({triangles} triangles)");
            }
        }

        [Test]
        public void Vegetation_StaysWithinTriangleAndRendererBudgets()
        {
            var rng = new System.Random(11);
            var parent = new GameObject("Vegetation").transform;
            created.Add(parent.gameObject);
            var makers = new Dictionary<string, System.Func<GameObject>>
            {
                { "Spruce", () => props.Pine(parent, Vector3.zero, rng) },
                { "Oak", () => props.RoundTree(parent, Vector3.zero, rng) },
                { "Sakura", () => props.Cherry(parent, Vector3.zero, rng) },
                { "Birch", () => props.Birch(parent, Vector3.zero, rng) },
                { "Old tree", () => props.OldTree(parent, Vector3.zero, rng) },
                { "Shrub", () => props.Shrub(parent, Vector3.zero, rng) },
                { "Blossom shrub", () => props.BlossomShrub(parent, Vector3.zero, rng) }
            };
            foreach (bool near in new[] { true, false })
            {
                props.AnimateProps = near;
                foreach (var pair in makers)
                {
                    var prop = pair.Value();
                    int triangles = 0, renderers = 0;
                    foreach (var f in prop.GetComponentsInChildren<MeshFilter>())
                    {
                        if (!f.gameObject.activeInHierarchy || f.sharedMesh == null) continue;
                        triangles += f.sharedMesh.triangles.Length / 3;
                        renderers++;
                    }
                    TestContext.Out.WriteLine($"{(near ? "near" : "far ")} {pair.Key}: {triangles} triangles in {renderers} renderers");
                    bool shrub = pair.Key.EndsWith("hrub");
                    Assert.LessOrEqual(triangles, near ? (shrub ? 2500 : 6000) : (shrub ? 1500 : 3500), $"{(near ? "near" : "far")} {pair.Key} is too heavy ({triangles} triangles)");
                    Assert.LessOrEqual(renderers, 40, $"{pair.Key} is not merged into few renderers ({renderers})");
                }
            }
            props.AnimateProps = true;
        }

        [Test]
        public void Actors_AreMergedIntoFewRenderers_AndStayWithinABudget()
        {
            var actors = new ActorFactory(art, prim);
            for (int variant = 0; variant < ActorFactory.VillagerVariants; variant++)
                CountActor($"villager {variant}", actors.Villager(variant), 6000, 60);
            CountActor("dog", actors.Dog(), 4000, 40);
            CountActor("hen", actors.Chicken(true), 3000, 40);
        }

        private void CountActor(string name, ActorVisual actor, int maxTriangles, int maxRenderers)
        {
            created.Add(actor.Root);
            int triangles = 0, renderers = 0;
            foreach (var f in actor.Root.GetComponentsInChildren<MeshFilter>())
            {
                if (!f.gameObject.activeInHierarchy || f.sharedMesh == null) continue;
                triangles += f.sharedMesh.triangles.Length / 3;
                renderers++;
            }
            TestContext.Out.WriteLine($"{name}: {triangles} triangles in {renderers} renderers");
            Assert.LessOrEqual(triangles, maxTriangles, $"{name} is too heavy ({triangles} triangles)");
            Assert.LessOrEqual(renderers, maxRenderers, $"{name} is not merged into few renderers ({renderers})");
            // every pose must run without throwing (missing joints would show up as a NullReferenceException here)
            actor.Animate(NpcState.Idle, 1f, 0f);
            actor.Animate(NpcState.Walking, 1f, 0.7f);
        }

        [Test]
        public void ModelsAreMergedIntoFewRenderers()
        {
            foreach (var id in ProceduralBuildingModels.KnownVisualIds)
            {
                var model = Model(id, 3);
                int staticRenderers = 0;
                foreach (var r in model.GetComponentsInChildren<MeshRenderer>())
                    if (GroupOf(model.transform, r.transform) != "Trees") staticRenderers++;
                Assert.LessOrEqual(staticRenderers, 70, $"{id}: the static groups should be merged per material (got {staticRenderers} renderers)");
            }
        }
    }
}
#endif
