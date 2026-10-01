using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AgeOfSakura.Game
{
    /// <summary>
    /// Merges the many small parts of a building model into one mesh per (group, material, shadow mode). A detailed building has a few
    /// hundred rounded boxes, cylinders and blobs; merged they cost a handful of draw calls each. Only static parts are merged:
    /// the Trees group keeps its separate objects because crowns sway and cast different shadows.
    /// </summary>
    public static class ModelBatcher
    {
        private static readonly string[] MergedGroups = { "Ground", "Body", "Roof", "Props" };

        private struct Key
        {
            public Material Material;
            public ShadowCastingMode Shadows;
        }

        public static void Merge(GameObject model)
        {
            foreach (var groupName in MergedGroups)
            {
                var group = model.transform.Find(groupName);
                if (group != null) MergeGroup(group);
            }
        }

        /// <summary>
        /// Merges only the meshes of the direct children of <paramref name="pivot"/> (one mesh per material and shadow mode). Child pivots, which
        /// animate on their own, are left alone: for jointed characters, where every bone keeps a few merged meshes instead of dozens of renderers.
        /// </summary>
        public static void MergeDirect(Transform pivot)
        {
            var buckets = new Dictionary<(int, int), List<CombineInstance>>();
            var materials = new Dictionary<(int, int), Key>();
            var sources = new List<GameObject>();
            var toLocal = pivot.worldToLocalMatrix;
            for (int i = 0; i < pivot.childCount; i++)
            {
                var child = pivot.GetChild(i);
                var filter = child.GetComponent<MeshFilter>();
                var renderer = child.GetComponent<MeshRenderer>();
                if (filter == null || renderer == null || filter.sharedMesh == null || renderer.sharedMaterial == null || !child.gameObject.activeSelf) continue;
                var key = (renderer.sharedMaterial.GetInstanceID(), (int)renderer.shadowCastingMode);
                if (!buckets.TryGetValue(key, out var list))
                {
                    buckets[key] = list = new List<CombineInstance>();
                    materials[key] = new Key { Material = renderer.sharedMaterial, Shadows = renderer.shadowCastingMode };
                }
                list.Add(new CombineInstance { mesh = filter.sharedMesh, transform = toLocal * child.localToWorldMatrix });
                sources.Add(child.gameObject);
            }
            Emit(pivot, buckets, materials, sources);
        }

        /// <summary>Merges every active mesh under <paramref name="group"/> into one mesh per (material, shadow mode); the sources are destroyed.</summary>
        public static void MergeGroup(Transform group)
        {
            var buckets = new Dictionary<(int, int), List<CombineInstance>>();
            var materials = new Dictionary<(int, int), Key>();
            var sources = new List<GameObject>();
            var toLocal = group.worldToLocalMatrix;

            foreach (var filter in group.GetComponentsInChildren<MeshFilter>(false))
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || filter.sharedMesh == null || renderer.sharedMaterial == null) continue;
                var key = (renderer.sharedMaterial.GetInstanceID(), (int)renderer.shadowCastingMode);
                if (!buckets.TryGetValue(key, out var list))
                {
                    buckets[key] = list = new List<CombineInstance>();
                    materials[key] = new Key { Material = renderer.sharedMaterial, Shadows = renderer.shadowCastingMode };
                }
                list.Add(new CombineInstance { mesh = filter.sharedMesh, transform = toLocal * filter.transform.localToWorldMatrix });
                sources.Add(filter.gameObject);
            }

            Emit(group, buckets, materials, sources);
        }

        private static void Emit(Transform group, Dictionary<(int, int), List<CombineInstance>> buckets, Dictionary<(int, int), Key> materials, List<GameObject> sources)
        {
            foreach (var pair in buckets)
            {
                var info = materials[pair.Key];
                var mesh = new Mesh { name = $"Merged_{group.name}_{info.Material.name}" };
                long vertexCount = 0;
                foreach (var c in pair.Value) vertexCount += c.mesh.vertexCount;
                if (vertexCount > 65000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.CombineMeshes(pair.Value.ToArray(), true, true);
                mesh.RecalculateBounds();

                var go = new GameObject("Merged_" + info.Material.name);
                go.transform.SetParent(group, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = info.Material;
                mr.shadowCastingMode = info.Shadows;
            }

            foreach (var source in sources)
            {
                source.SetActive(false); // Destroy is deferred to the end of the frame in play mode
                if (Application.isPlaying) Object.Destroy(source);
                else Object.DestroyImmediate(source);
            }
        }
    }
}
