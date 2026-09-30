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

        private static void MergeGroup(Transform group)
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
