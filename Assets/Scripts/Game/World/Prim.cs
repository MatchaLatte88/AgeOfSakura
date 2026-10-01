using UnityEngine;

namespace AgeOfSakura.Game
{
    /// <summary>Convenience builders that assemble models out of the toon style's rounded primitives (no colliders).</summary>
    public sealed class Prim
    {
        private readonly GameArt art;

        /// <summary>Smallest side below which <see cref="ThinBoxes"/> swaps the rounded box for a flat one (a bevel that small is invisible).</summary>
        public const float ThinLimit = 0.065f;

        /// <summary>
        /// While set, boxes with a side under <see cref="ThinLimit"/> use the 12-triangle flat box instead of the 108-triangle rounded box.
        /// Detailed models (the Town Hall has hundreds of bars and slats) switch it on while they build.
        /// </summary>
        public bool ThinBoxes { get; set; }

        public Prim(GameArt art)
        {
            this.art = art;
        }

        private Mesh BoxMesh(Vector3 size) =>
            ThinBoxes && Mathf.Min(size.x, Mathf.Min(size.y, size.z)) < ThinLimit ? ToonMeshes.SharpBox(size) : ToonMeshes.RoundedBox(size);

        public GameObject Add(Transform parent, Mesh mesh, Material material, Vector3 localPosition, Vector3 localScale, Vector3 localEuler = default, string name = null, bool castShadows = true)
        {
            var go = new GameObject(name ?? mesh.name);
            var t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            t.localRotation = Quaternion.Euler(localEuler);
            t.localScale = localScale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>Rounded box of exactly <paramref name="size"/> centred on <paramref name="center"/> (never a hard cube edge).</summary>
        public GameObject Box(Transform parent, Material m, Vector3 center, Vector3 size, Vector3 euler = default, bool castShadows = true) =>
            Add(parent, BoxMesh(size), m, center, Vector3.one, euler, "Box", castShadows);

        /// <summary>Rounded box whose bottom face sits at <paramref name="baseCenter"/>.y.</summary>
        public GameObject BoxOnGround(Transform parent, Material m, Vector3 baseCenter, Vector3 size, Vector3 euler = default) =>
            Add(parent, BoxMesh(size), m, baseCenter + new Vector3(0f, size.y * 0.5f, 0f), Vector3.one, euler, "Box");

        /// <summary>Vertical cylinder with rounded rims centred on <paramref name="center"/>.</summary>
        public GameObject Cylinder(Transform parent, Material m, Vector3 center, float radius, float height, Vector3 euler = default, bool castShadows = true) =>
            Add(parent, ToonMeshes.RoundedCylinder(radius, height), m, center, Vector3.one, euler, "Cylinder", castShadows);

        /// <summary>Smooth sphere/ellipsoid; small ones use the cheaper 80-triangle mesh.</summary>
        public GameObject Sphere(Transform parent, Material m, Vector3 center, Vector3 diameters, bool castShadows = true) =>
            Add(parent, Mathf.Max(diameters.x, Mathf.Max(diameters.y, diameters.z)) < 0.2f ? art.SphereLow : art.Sphere, m, center, diameters, Vector3.zero, "Sphere", castShadows);

        public GameObject Sphere(Transform parent, Material m, Vector3 center, float radius) =>
            Sphere(parent, m, center, Vector3.one * radius * 2f);

        /// <summary>Cone with its base at <paramref name="baseCenter"/>.</summary>
        public GameObject Cone(Transform parent, Material m, Vector3 baseCenter, float radius, float height, bool eightSides = false) =>
            Add(parent, eightSides ? art.Cone8 : art.Cone6, m, baseCenter, new Vector3(radius * 2f, height, radius * 2f), Vector3.zero, "Cone");

        public GameObject MeshObject(Transform parent, Mesh mesh, Material m, Vector3 localPosition, Vector3 euler = default) =>
            Add(parent, mesh, m, localPosition, Vector3.one, euler, mesh.name);
    }
}
