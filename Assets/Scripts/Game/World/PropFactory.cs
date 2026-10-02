using System;
using System.Collections.Generic;
using UnityEngine;

namespace AgeOfSakura.Game
{
    /// <summary>
    /// Vegetation, rocks and small props in the 3D-Toon style. Every prop's pivot sits on the ground at its centre.
    /// Trees have five species (oak, cherry, spruce, birch, old); crowns are clumps of smooth icospheres whose vertex colours are baked
    /// along the foliage ramp, plus camera-facing leaf cards on near scenery. Far scenery (built with <see cref="AnimateProps"/> = false)
    /// uses coarser clumps and no cards so static batching keeps it cheap.
    /// </summary>
    public sealed class PropFactory
    {
        private readonly GameArt art;
        private readonly Prim prim;
        private readonly WorldAnimator animator;
        private readonly VfxFactory vfx;

        public PropFactory(GameArt art, Prim prim, WorldAnimator animator, VfxFactory vfx)
        {
            this.art = art;
            this.prim = prim;
            this.animator = animator;
            this.vfx = vfx;
        }

        /// <summary>When false, props are built without sway and with coarse foliage so the caller can static-batch them (far scenery).</summary>
        public bool AnimateProps { get; set; } = true;

        /// <summary>
        /// Largest horizontal reach (world units from the prop's trunk) a tree, bamboo grove or boulder may have. Set by the world builder
        /// for scenery next to buildable land: buildings stay inside their footprint, so a crown that stays inside its own cell can never
        /// clip a neighbouring house. Crowns are squeezed sideways (and stay tall), never moved.
        /// </summary>
        public float MaxRadius { get; set; } = float.PositiveInfinity;

        /// <summary>Squeezes a prop horizontally until none of its renderers reaches further than <paramref name="limit"/> from its root.</summary>
        private void Fit(GameObject prop, float limit)
        {
            if (float.IsInfinity(limit)) return;
            var c = prop.transform.position;
            float reach = 0f;
            foreach (var r in prop.GetComponentsInChildren<MeshRenderer>())
            {
                var b = r.bounds;
                reach = Mathf.Max(reach, Mathf.Abs(b.min.x - c.x), Mathf.Abs(b.max.x - c.x), Mathf.Abs(b.min.z - c.z), Mathf.Abs(b.max.z - c.z));
            }
            if (reach <= limit) return;
            float k = limit / reach;
            var s = prop.transform.localScale;
            // the crown is squeezed sideways and only a little lower: a tall slim tree still reads as a tree, a needle does not
            prop.transform.localScale = new Vector3(s.x * k, s.y * Mathf.Lerp(1f, k, 0.45f), s.z * k);
        }

        /// <summary>The fixed camera's rotation; leaf cards are aligned to it.</summary>
        public Quaternion CameraRotation { get; set; } = Quaternion.identity;

        private bool Near => AnimateProps;

        private void Sway(Transform target, float amplitudeDegrees, float speed, float phase)
        {
            if (AnimateProps) animator.AddSway(target, amplitudeDegrees, speed, phase);
        }

        private static float R(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

        /// <summary>New prop root. Foliage props keep yaw 0: their vertex colours are baked for the sun direction of the fixed world.</summary>
        private GameObject NewProp(string name, Transform parent, Vector3 position, System.Random rng, bool randomYaw = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0f, randomYaw ? R(rng, 0f, 360f) : 0f, 0f);
            return go;
        }

        // ------------------------------------------------------------------ foliage building blocks

        private Transform NewCrown(GameObject tree, Vector3 localPosition)
        {
            var crown = new GameObject("Crown").transform;
            crown.SetParent(tree.transform, false);
            crown.localPosition = localPosition;
            return crown;
        }

        /// <summary>Group for the static woody parts of a tree (trunk, limbs, roots, moss, ground disc): merged into a few meshes at the end.</summary>
        private static Transform NewGroup(GameObject tree, string name)
        {
            var g = new GameObject(name).transform;
            g.SetParent(tree.transform, false);
            return g;
        }

        /// <summary>Centre and diameter of a crown clump: the leaf cards sit on the surfaces of these.</summary>
        private struct Spot
        {
            public Vector3 Pos;
            public float Size;
        }

        /// <summary>
        /// One crown clump. Big clumps near the camera get the finer icosphere; every clump gets a slight brightness/hue variation
        /// (in a few steps, so the materials stay shared) and a shade that follows its height in the crown.
        /// </summary>
        private GameObject Clump(Transform crown, FoliagePalette palette, Vector3 pos, Vector3 size, System.Random rng, float shade = 1f, bool outline = true)
        {
            int seed = rng.Next(6);
            int sub = Near && Mathf.Max(size.x, size.z) >= 0.36f ? ToonStyle.FoliageSubdivisionsNear : ToonStyle.FoliageSubdivisionsFar;
            float hue = 0.94f + rng.Next(3) * 0.03f;
            float v = Mathf.Round(shade * R(rng, 0.95f, 1.04f) * 16f) / 16f;
            var tint = new Color(v * hue, v, v * (hue + (1f - hue) * 0.5f), 1f);
            return prim.Add(crown, ToonMeshes.Blob(sub, seed, sub >= 2 ? 0.34f : 0.22f, palette), art.Foliage(tint, outline), pos, size, default, "Clump");
        }

        /// <summary>
        /// A crown made of many clumps: one big dark core that fills the volume, then clumps spiralling over an ellipsoid shell from the lower
        /// skirt to the top (big and dark low, small and bright high), so the crown is lumpy and layered instead of five smooth balls.
        /// </summary>
        private List<Spot> Crown(Transform crown, FoliagePalette palette, Vector3 center, Vector3 radii, int count, float sizeMax, float sizeMin, System.Random rng, bool core = true)
        {
            var spots = new List<Spot>(count + 1);
            if (core)
            {
                var size = new Vector3(radii.x * 1.5f, radii.y * 1.25f, radii.z * 1.5f);
                Clump(crown, palette, center, size, rng, 0.96f);
                spots.Add(new Spot { Pos = center, Size = Mathf.Max(size.x, size.z) });
            }
            for (int i = 0; i < count; i++)
            {
                float t = (i + 0.5f) / count;
                float yy = Mathf.Lerp(-0.6f, 0.92f, t);
                float ring = Mathf.Sqrt(Mathf.Max(0.05f, 1f - yy * yy));
                float ang = i * 2.39996f + R(rng, -0.35f, 0.35f);
                var pos = center + new Vector3(Mathf.Cos(ang) * ring * radii.x, yy * radii.y, Mathf.Sin(ang) * ring * radii.z);
                float size = Mathf.Lerp(sizeMax, sizeMin, Mathf.Abs(yy)) * R(rng, 0.85f, 1.15f);
                float shade = Mathf.Lerp(0.84f, 1.08f, (yy + 0.6f) / 1.52f) * R(rng, 0.96f, 1.03f);
                Clump(crown, palette, pos, new Vector3(size, size * R(rng, 0.78f, 0.95f), size), rng, shade);
                spots.Add(new Spot { Pos = pos, Size = size });
            }
            return spots;
        }

        /// <summary>
        /// Leaf (or blossom) cards scattered over the outer surfaces of the crown clumps, all facing the fixed camera. They break up the smooth
        /// silhouette into leafy edges; their tint follows the same light ramp as the clumps, in a few steps, and each card comes in one of three
        /// brightness variants. Far scenery (no sway, static batching) gets half of them.
        /// </summary>
        private void Cards(Transform crown, FoliagePalette palette, List<Spot> spots, Vector3 center, int count, float cardSize, PaintedTexture texture, System.Random rng, float protrude = 1.22f)
        {
            if (spots.Count == 0) return;
            int n = Near ? count : count * 2 / 3;
            var sun = ToonStyle.SunDirTowardsSun;
            for (int i = 0; i < n; i++)
            {
                var spot = spots[rng.Next(spots.Count)];
                var dir = new Vector3(R(rng, -1f, 1f), R(rng, -0.3f, 1f), R(rng, -1f, 1f));
                if (dir.sqrMagnitude < 0.05f) dir = Vector3.up;
                dir.Normalize();
                var outward = spot.Pos - center;
                outward.y *= 0.5f;
                if (outward.sqrMagnitude > 1e-4f && Vector3.Dot(dir, outward.normalized) < -0.2f) dir = -dir;
                dir.y = Mathf.Max(dir.y, -0.35f);
                dir.Normalize();
                float t = Mathf.Clamp01((Vector3.Dot(dir, sun) * 0.5f + 0.5f) * 0.65f + (dir.y * 0.5f + 0.5f) * 0.35f);
                t = Mathf.Round(Mathf.Clamp01((t - 0.18f) / 0.72f + R(rng, -0.28f, 0.3f)) * 4f) / 4f;   // dappled: some leaves in shade, some in the sun
                var pos = spot.Pos + dir * spot.Size * 0.5f * R(rng, 0.92f, protrude);
                var card = prim.Add(crown, ToonMeshes.Card(rng.Next(3)), art.LeafCards(ToonMeshes.FoliageColor(palette, t), texture), pos,
                    Vector3.one * cardSize * R(rng, 0.75f, 1.25f), default, "LeafCard", false);
                card.transform.rotation = CameraRotation * Quaternion.Euler(0f, 0f, R(rng, -50f, 50f));
            }
        }

        private void Roots(Transform tree, Material bark, int count, float radius, System.Random rng)
        {
            for (int i = 0; i < count; i++)
            {
                float a = (i + R(rng, 0f, 0.6f)) / count * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                // a root flare: a tapering tube that leaves the trunk and dives into the ground
                var path = new[] { dir * radius * 0.5f + Vector3.up * 0.09f, dir * radius * 1.5f + Vector3.up * 0.035f, dir * radius * 2.4f + Vector3.down * 0.01f };
                var radii = new[] { radius * 0.55f, radius * 0.4f, radius * 0.2f };
                prim.MeshObject(tree, ProceduralMeshes.Tube(path, radii, 6, 0.2f), bark, Vector3.zero);
            }
        }

        /// <summary>Tapering, gently bent trunk or limb as one smooth tube.</summary>
        private void Limb(Transform tree, Material bark, Vector3[] path, float[] radii, int sides = 8) =>
            prim.MeshObject(tree, ProceduralMeshes.Tube(path, radii, sides, 0.3f), bark, Vector3.zero);

        /// <summary>A branch from the trunk out to a crown clump: three points with a small upward bow, tapering.</summary>
        private void Branch(Transform tree, Material bark, Vector3 from, Vector3 to, float r0, float r1, System.Random rng)
        {
            var mid = (from + to) * 0.5f + new Vector3(R(rng, -0.03f, 0.03f), 0.05f, R(rng, -0.03f, 0.03f));
            Limb(tree, bark, new[] { from, mid, to }, new[] { r0, (r0 + r1) * 0.5f, r1 }, 6);
        }

        /// <summary>
        /// Limbs from the trunk to real crown clumps (never into thin air): picks <paramref name="count"/> lower, well-spread clumps and
        /// grows a branch into each, ending partway inside the clump.
        /// </summary>
        private void Boughs(Transform tree, Material bark, Vector3 from, Vector3 crownAt, List<Spot> spots, int count, float r0, float r1, System.Random rng)
        {
            if (spots.Count < 2) return;
            var picked = new List<int>();
            for (int i = 0; i < count; i++)
            {
                int best = -1;
                float bestScore = float.MinValue;
                for (int k = 1; k < spots.Count; k++)
                {
                    if (picked.Contains(k)) continue;
                    var flat = new Vector2(spots[k].Pos.x, spots[k].Pos.z);
                    float score = -spots[k].Pos.y + R(rng, 0f, 0.15f) + (flat.magnitude > 0.15f ? 0.2f : -0.5f);
                    foreach (int j in picked)
                    {
                        var other = new Vector2(spots[j].Pos.x, spots[j].Pos.z);
                        score -= Mathf.Max(0f, 0.6f - Vector2.Distance(flat, other)) * 0.9f; // spread the limbs around the trunk
                    }
                    if (score > bestScore) { bestScore = score; best = k; }
                }
                if (best < 0) break;
                picked.Add(best);
                var end = crownAt + Vector3.Lerp(Vector3.zero, spots[best].Pos, 0.72f);
                Branch(tree, bark, from, end, r0, r1, rng);
            }
        }

        /// <summary>Darker ground disc around a trunk (the style's "decals").</summary>
        private void BaseDecal(Transform tree, float radius, System.Random rng)
        {
            var decal = prim.Cylinder(tree, art.NoOutline(Color.Lerp(Palette.GrassDark, Palette.Mud, 0.35f)), new Vector3(0f, 0.004f, 0f), radius, 0.008f);
            decal.transform.localScale = new Vector3(1f, 1f, R(rng, 0.8f, 1f));
            decal.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------------ trees

        /// <summary>Spruce: tiers of drooping, lobed branch skirts that get smaller towards the top, on a slim trunk.</summary>
        public GameObject Pine(Transform parent, Vector3 pos, System.Random rng)
        {
            var go = NewProp("Spruce", parent, pos, rng);
            var wood = NewGroup(go, "Wood");
            float s = R(rng, 0.85f, 1.35f);
            go.transform.localScale = Vector3.one * s;
            var bark = art.Lit(Palette.Bark, PaintedTexture.Bark);
            Limb(wood, bark, new[] { new Vector3(0f, -0.02f, 0f), new Vector3(0.006f, 0.35f, 0f), new Vector3(0f, 0.9f, 0f), new Vector3(0f, 1.3f, 0f) },
                new[] { 0.085f, 0.06f, 0.035f, 0.015f }, 8);
            Roots(wood, bark, 3, 0.07f, rng);
            var crown = NewCrown(go, Vector3.zero);
            var foliageTones = new[] { art.Foliage(Color.white), art.Foliage(new Color(0.93f, 0.96f, 0.93f)), art.Foliage(new Color(0.86f, 0.92f, 0.88f)) };
            int tiers = Near ? 9 : 7;
            int segments = Near ? 18 : 12;
            for (int i = 0; i < tiers; i++)
            {
                float f = i / (float)(tiers - 1);
                float radius = Mathf.Lerp(0.4f, 0.09f, Mathf.Pow(f, 0.85f)) * R(rng, 0.94f, 1.06f);
                float height = Mathf.Lerp(0.46f, 0.27f, f);
                float y = 0.22f + f * 1.0f;
                var mesh = ToonMeshes.SpruceTier(radius, height, rng.Next(6, 9), segments, rng.Next(5));
                prim.Add(crown, mesh, foliageTones[i % 3], new Vector3(R(rng, -0.012f, 0.012f), y, R(rng, -0.012f, 0.012f)), Vector3.one,
                    new Vector3(R(rng, -3f, 3f), R(rng, 0f, 360f), R(rng, -3f, 3f)), "Tier");
            }
            Sway(crown, 1.4f, R(rng, 0.9f, 1.4f), R(rng, 0f, 6.28f));
            BaseDecal(wood, 0.3f, rng);
            ModelBatcher.MergeGroup(wood);
            ModelBatcher.MergeGroup(crown);
            Fit(go, MaxRadius);
            return go;
        }

        /// <summary>Oak: forked trunk with branches reaching into a layered crown of many clumps and leaf cards.</summary>
        public GameObject RoundTree(Transform parent, Vector3 pos, System.Random rng)
        {
            var go = NewProp("Oak", parent, pos, rng, false);
            var wood = NewGroup(go, "Wood");
            go.transform.localScale = Vector3.one * R(rng, 0.85f, 1.25f);
            var bark = art.Lit(Palette.Bark, PaintedTexture.Bark);
            Limb(wood, bark, new[] { new Vector3(0f, -0.04f, 0f), new Vector3(0.012f, 0.1f, 0f), new Vector3(0.03f, 0.3f, 0.01f), new Vector3(0.01f, 0.52f, 0f), new Vector3(0f, 0.66f, 0f) },
                new[] { 0.12f, 0.092f, 0.078f, 0.07f, 0.06f }, 9);
            Roots(wood, bark, 4, 0.085f, rng);
            var crownAt = new Vector3(0f, 0.55f, 0f);
            var crown = NewCrown(go, crownAt);
            var center = new Vector3(0f, 0.42f, 0f);
            var spots = Crown(crown, FoliagePalette.Leaf, center, new Vector3(0.52f, 0.38f, 0.52f), 13, 0.5f, 0.24f, rng);
            Cards(crown, FoliagePalette.Leaf, spots, center, 190, 0.18f, PaintedTexture.LeafCard, rng);
            Boughs(wood, bark, new Vector3(0f, 0.5f, 0f), crownAt, spots, 4, 0.04f, 0.016f, rng);
            Sway(crown, 1.1f, R(rng, 0.8f, 1.2f), R(rng, 0f, 6.28f));
            BaseDecal(wood, 0.32f, rng);
            ModelBatcher.MergeGroup(wood);
            ModelBatcher.MergeGroup(crown);
            Fit(go, MaxRadius);
            return go;
        }

        /// <summary>Cherry: short leaning trunk, wide spreading limbs, a cloud of blossom clumps and flower trusses.</summary>
        public GameObject Cherry(Transform parent, Vector3 pos, System.Random rng, float? maxRadius = null)
        {
            var go = NewProp("CherryBlossom", parent, pos, rng, false);
            var wood = NewGroup(go, "Wood");
            go.transform.localScale = Vector3.one * 1.25f;
            var bark = art.Lit(Palette.Bark, PaintedTexture.Bark);
            Limb(wood, bark, new[] { new Vector3(0f, -0.04f, 0f), new Vector3(0.02f, 0.14f, 0f), new Vector3(0.05f, 0.34f, 0.01f), new Vector3(0.02f, 0.6f, 0f) },
                new[] { 0.115f, 0.09f, 0.078f, 0.066f }, 9);
            Roots(wood, bark, 3, 0.09f, rng);
            var crownAt = new Vector3(0f, 0.7f, 0f);
            var crown = NewCrown(go, crownAt);
            var center = new Vector3(0f, 0.5f, 0f);
            var spots = Crown(crown, FoliagePalette.Blossom, center, new Vector3(0.6f, 0.38f, 0.6f), 14, 0.5f, 0.24f, rng);
            Cards(crown, FoliagePalette.Blossom, spots, center, 170, 0.16f, PaintedTexture.BlossomCard, rng);
            Boughs(wood, bark, new Vector3(0.03f, 0.56f, 0f), crownAt, spots, 5, 0.04f, 0.015f, rng);
            Sway(crown, 1.0f, 0.9f, R(rng, 0f, 6.28f));
            vfx.CreatePetals(go.transform, new Vector3(0f, 1.05f, 0f));
            BaseDecal(wood, 0.36f, rng);
            ModelBatcher.MergeGroup(wood);
            ModelBatcher.MergeGroup(crown);
            Fit(go, maxRadius ?? MaxRadius);
            return go;
        }

        /// <summary>Birch: slim, slightly bent white trunk with dark scars, thin limbs and a light, airy crown.</summary>
        public GameObject Birch(Transform parent, Vector3 pos, System.Random rng)
        {
            var go = NewProp("Birch", parent, pos, rng, false);
            var wood = NewGroup(go, "Wood");
            go.transform.localScale = Vector3.one * R(rng, 0.9f, 1.25f);
            var bark = art.Lit(Hex("#ece6d6"), PaintedTexture.BirchBark);
            float lean = R(rng, -0.05f, 0.05f);
            Limb(wood, bark, new[] { new Vector3(0f, -0.02f, 0f), new Vector3(lean * 0.5f, 0.3f, 0f), new Vector3(lean, 0.6f, 0.01f), new Vector3(lean * 1.3f, 0.86f, 0f) },
                new[] { 0.062f, 0.048f, 0.038f, 0.028f }, 8);
            var crownAt = new Vector3(lean * 1.3f, 0.85f, 0f);
            var crown = NewCrown(go, crownAt);
            var center = new Vector3(0f, 0.28f, 0f);
            var spots = Crown(crown, FoliagePalette.Birch, center, new Vector3(0.3f, 0.42f, 0.3f), 10, 0.34f, 0.18f, rng);
            Cards(crown, FoliagePalette.Birch, spots, center, 120, 0.15f, PaintedTexture.LeafCard, rng);
            Boughs(wood, bark, new Vector3(lean * 1.1f, 0.68f, 0f), crownAt, spots, 3, 0.02f, 0.008f, rng);
            Sway(crown, 1.6f, R(rng, 1f, 1.5f), R(rng, 0f, 6.28f));
            BaseDecal(wood, 0.26f, rng);
            ModelBatcher.MergeGroup(wood);
            ModelBatcher.MergeGroup(crown);
            Fit(go, MaxRadius);
            return go;
        }

        /// <summary>Old tree: thick, twisting trunk with heavy limbs, big roots, moss, and a wide dark crown.</summary>
        public GameObject OldTree(Transform parent, Vector3 pos, System.Random rng)
        {
            var go = NewProp("OldTree", parent, pos, rng, false);
            var wood = NewGroup(go, "Wood");
            go.transform.localScale = Vector3.one * R(rng, 1.0f, 1.3f);
            var bark = art.Lit(Hex("#5a3e26"), PaintedTexture.Bark);
            Limb(wood, bark, new[] { new Vector3(0f, -0.05f, 0f), new Vector3(0.02f, 0.12f, 0.01f), new Vector3(-0.03f, 0.3f, 0.02f), new Vector3(0.03f, 0.5f, -0.01f), new Vector3(0f, 0.72f, 0f) },
                new[] { 0.19f, 0.15f, 0.135f, 0.115f, 0.09f }, 10);
            Roots(wood, bark, 5, 0.15f, rng);
            var crownAt = new Vector3(0f, 0.72f, 0f);
            var moss = art.Foliage(Color.white, false);
            foreach (var m in new[] { new Vector3(0.15f, 0.28f, 0.05f), new Vector3(-0.1f, 0.5f, 0.1f) })
                prim.Add(wood, ToonMeshes.Blob(1, 3, 0.3f, FoliagePalette.Old), moss, m, new Vector3(0.14f, 0.09f, 0.12f), default, "Moss", false);
            var crown = NewCrown(go, crownAt);
            var center = new Vector3(0f, 0.36f, 0f);
            var spots = Crown(crown, FoliagePalette.Old, center, new Vector3(0.64f, 0.32f, 0.62f), 13, 0.56f, 0.28f, rng);
            Cards(crown, FoliagePalette.Old, spots, center, 160, 0.19f, PaintedTexture.LeafCard, rng);
            Boughs(wood, bark, new Vector3(0f, 0.62f, 0f), crownAt, spots, 4, 0.07f, 0.024f, rng);
            Sway(crown, 0.8f, R(rng, 0.7f, 1f), R(rng, 0f, 6.28f));
            BaseDecal(wood, 0.4f, rng);
            ModelBatcher.MergeGroup(wood);
            ModelBatcher.MergeGroup(crown);
            Fit(go, MaxRadius);
            return go;
        }

        public GameObject Bamboo(Transform parent, Vector3 pos, System.Random rng)
        {
            var go = NewProp("Bamboo", parent, pos, rng, false);
            int stalks = rng.Next(5, 8);
            var stem = art.Lit(Palette.Bamboo);
            var node = art.Lit(Color.Lerp(Palette.Bamboo, Palette.GrassDark, 0.5f));
            var leaf = art.Foliage(Color.white, false);
            const int sub = 1;
            for (int i = 0; i < stalks; i++)
            {
                var pivot = new GameObject("Stalk").transform;
                pivot.SetParent(go.transform, false);
                pivot.localPosition = new Vector3(R(rng, -0.28f, 0.28f), 0f, R(rng, -0.28f, 0.28f));
                pivot.localRotation = Quaternion.Euler(R(rng, -6f, 6f), 0f, R(rng, -6f, 6f));
                float h = R(rng, 1.4f, 2.2f);
                prim.Cylinder(pivot, stem, new Vector3(0f, h * 0.5f, 0f), 0.035f, h);
                for (int n = 1; n <= 3; n++)
                    prim.Cylinder(pivot, node, new Vector3(0f, h * n * 0.25f, 0f), 0.043f, 0.03f);
                // leaf fans: long narrow blobs radiating from the stalk top and from a lower node, drooping outwards
                for (int fan = 0; fan < 2; fan++)
                {
                    float y = h - 0.05f - fan * 0.28f;
                    for (int k = 0; k < 3; k++)
                    {
                        var leafBlob = prim.Add(pivot, ToonMeshes.Blob(sub, i + k, 0.3f, FoliagePalette.Birch), leaf, new Vector3(0f, y, 0f),
                            new Vector3(0.34f - fan * 0.08f, 0.06f, 0.1f), default, "Leaf", false);
                        leafBlob.transform.localRotation = Quaternion.Euler(0f, k * 120f + fan * 40f + i * 25f, -14f);
                        leafBlob.transform.localPosition = new Vector3(0f, y, 0f) + leafBlob.transform.localRotation * new Vector3(0.14f, 0f, 0f);
                    }
                }
                Sway(pivot, 2.6f, R(rng, 1.1f, 1.7f), R(rng, 0f, 6.28f));
            }
            Fit(go, MaxRadius);
            return go;
        }

        // ------------------------------------------------------------------ rocks, shrubs, ground detail

        public GameObject Rock(Transform parent, Vector3 pos, System.Random rng, bool big)
        {
            var go = NewProp(big ? "Boulder" : "Rock", parent, pos, rng);
            var mats = new[] { art.Lit(Palette.RockA, PaintedTexture.Stone), art.Lit(Palette.RockB, PaintedTexture.Stone), art.Lit(Palette.RockC, PaintedTexture.Stone) };
            int parts = big ? rng.Next(2, 4) : 1;
            for (int i = 0; i < parts; i++)
            {
                float size = big ? R(rng, 0.45f, 0.75f) : R(rng, 0.14f, 0.24f);
                var offset = big ? new Vector3(R(rng, -0.25f, 0.25f), 0f, R(rng, -0.25f, 0.25f)) : Vector3.zero;
                var scale = new Vector3(size * R(rng, 1f, 1.4f), size * R(rng, 0.6f, 0.85f), size * R(rng, 1f, 1.3f));
                var mesh = ToonMeshes.Blob(Near && big ? 2 : 1, 20 + rng.Next(4), 0.22f, FoliagePalette.None);
                var rock = prim.Add(go.transform, mesh, mats[rng.Next(mats.Length)], offset + new Vector3(0f, scale.y * 0.32f, 0f), scale, default, "Rock");
                rock.transform.localRotation = Quaternion.Euler(R(rng, -10f, 10f), R(rng, 0f, 360f), R(rng, -10f, 10f));
                if (big && i == 0)
                {
                    // moss on top
                    prim.Add(go.transform, ToonMeshes.Blob(1, 3, 0.3f, FoliagePalette.Leaf), art.Foliage(Color.white, false),
                        offset + new Vector3(0f, scale.y * 0.62f, 0f), new Vector3(scale.x * 0.62f, scale.y * 0.3f, scale.z * 0.62f), default, "Moss", false);
                }
            }
            if (big) Fit(go, MaxRadius);
            return go;
        }

        /// <summary>A mountain cell: a heap of large rock lumps (taller where more mountain lies around it) with a pale crest.</summary>
        public GameObject MountainRock(Transform parent, Vector3 pos, System.Random rng, float height)
        {
            var go = NewProp("Mountain", parent, pos, rng);
            var mats = new[] { art.Lit(Palette.RockA, PaintedTexture.Stone), art.Lit(Palette.RockB, PaintedTexture.Stone), art.Lit(Palette.RockC, PaintedTexture.Stone) };
            for (int i = 0; i < 3; i++)
            {
                float size = R(rng, 0.62f, 0.85f);
                float h = height * R(rng, 0.7f, 1.05f) * (i == 0 ? 1f : 0.7f);
                var offset = i == 0 ? Vector3.zero : new Vector3(R(rng, -0.3f, 0.3f), 0f, R(rng, -0.3f, 0.3f));
                var scale = new Vector3(size * 1.5f, h, size * 1.5f);
                var rock = prim.Add(go.transform, ToonMeshes.Blob(Near ? 2 : 1, 30 + rng.Next(5), 0.2f, FoliagePalette.None), mats[rng.Next(mats.Length)],
                    offset + new Vector3(0f, h * 0.3f, 0f), scale, default, "Lump");
                rock.transform.localRotation = Quaternion.Euler(R(rng, -6f, 6f), R(rng, 0f, 360f), R(rng, -6f, 6f));
            }
            prim.Add(go.transform, ToonMeshes.Blob(1, 5, 0.25f, FoliagePalette.None), art.Lit(Palette.RockC, PaintedTexture.Stone),
                new Vector3(0f, height * 0.78f, 0f), new Vector3(0.7f, height * 0.4f, 0.7f), default, "Crest");
            return go;
        }

        /// <summary>A bush: a dark core, a ring of smaller lumps at different heights, and leaf (or blossom) cards over the outside.</summary>
        private GameObject Bush(string name, FoliagePalette palette, PaintedTexture cardTexture, Transform parent, Vector3 pos, System.Random rng)
        {
            var go = NewProp(name, parent, pos, rng, false);
            var center = new Vector3(0f, 0.14f, 0f);
            var spots = Crown(go.transform, palette, center, new Vector3(0.11f, 0.09f, 0.11f), 7, 0.25f, 0.16f, rng);
            Cards(go.transform, palette, spots, center, 48, 0.1f, cardTexture, rng, 1.0f);   // bushes stand next to walls: cards stay on the clumps
            ModelBatcher.MergeGroup(go.transform);
            return go;
        }

        public GameObject Shrub(Transform parent, Vector3 pos, System.Random rng) =>
            Bush("Shrub", FoliagePalette.Leaf, PaintedTexture.LeafCard, parent, pos, rng);

        /// <summary>Low flowering bush in the sakura pinks (terrace corners, flower boxes).</summary>
        public GameObject BlossomShrub(Transform parent, Vector3 pos, System.Random rng) =>
            Bush("BlossomShrub", FoliagePalette.Blossom, PaintedTexture.BlossomCard, parent, pos, rng);

        public GameObject Flowers(Transform parent, Vector3 pos, System.Random rng)
        {
            var go = NewProp("Flowers", parent, pos, rng);
            var colors = new[] { Color.white, Palette.Hex("#ffe27a"), Palette.Hex("#f8a3c1"), Palette.Hex("#c2a8f0") };
            var stem = art.NoOutline(Palette.GrassDark);
            for (int i = 0; i < 7; i++)
            {
                var p = new Vector3(R(rng, -0.3f, 0.3f), 0f, R(rng, -0.3f, 0.3f));
                float h = R(rng, 0.08f, 0.16f);
                prim.Cylinder(go.transform, stem, p + new Vector3(0f, h * 0.5f, 0f), 0.01f, h);
                prim.Sphere(go.transform, art.NoOutline(colors[rng.Next(colors.Length)]), p + new Vector3(0f, h, 0f), new Vector3(0.07f, 0.04f, 0.07f), false);
            }
            Sway(go.transform, 2f, R(rng, 1.4f, 2f), R(rng, 0f, 6.28f));
            return go;
        }

        public GameObject Tuft(Transform parent, Vector3 pos, System.Random rng)
        {
            var go = NewProp("Tuft", parent, pos, rng);
            var m = art.NoOutline(rng.Next(2) == 0 ? Palette.GrassLight : Palette.GrassDark);
            for (int i = 0; i < 4; i++)
            {
                var blade = prim.Cone(go.transform, m, new Vector3(R(rng, -0.08f, 0.08f), 0f, R(rng, -0.08f, 0.08f)), 0.035f, R(rng, 0.14f, 0.26f));
                blade.transform.localRotation = Quaternion.Euler(R(rng, -18f, 18f), 0f, R(rng, -18f, 18f));
                blade.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            Sway(go.transform, 3f, R(rng, 1.6f, 2.4f), R(rng, 0f, 6.28f));
            return go;
        }

        /// <summary>Cattails at the water's edge.</summary>
        public GameObject Reeds(Transform parent, Vector3 pos, System.Random rng)
        {
            var go = NewProp("Reeds", parent, pos, rng, false);
            var stem = art.NoOutline(Color.Lerp(Palette.Bamboo, Palette.GrassDark, 0.3f));
            var head = art.NoOutline(Hex("#7a4a2a"));
            int n = rng.Next(4, 7);
            for (int i = 0; i < n; i++)
            {
                var pivot = new GameObject("Reed").transform;
                pivot.SetParent(go.transform, false);
                pivot.localPosition = new Vector3(R(rng, -0.14f, 0.14f), 0f, R(rng, -0.14f, 0.14f));
                pivot.localRotation = Quaternion.Euler(R(rng, -9f, 9f), 0f, R(rng, -9f, 9f));
                float h = R(rng, 0.45f, 0.85f);
                prim.Cylinder(pivot, stem, new Vector3(0f, h * 0.5f, 0f), 0.012f, h, default).GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                prim.Cylinder(pivot, head, new Vector3(0f, h * 0.86f, 0f), 0.024f, 0.1f).GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Sway(pivot, 3f, R(rng, 1.2f, 2f), R(rng, 0f, 6.28f));
            }
            return go;
        }

        /// <summary>Floating lily pad, sometimes with a pink blossom.</summary>
        public GameObject LilyPad(Transform parent, Vector3 pos, System.Random rng)
        {
            var go = NewProp("LilyPad", parent, pos, rng);
            var pad = art.NoOutline(Color.Lerp(Palette.LeafGreen, Palette.GrassDark, 0.2f));
            float r = R(rng, 0.1f, 0.18f);
            prim.Cylinder(go.transform, pad, new Vector3(0f, 0.065f, 0f), r, 0.012f).GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (rng.NextDouble() < 0.4)
                prim.Sphere(go.transform, art.NoOutline(Palette.Hex("#ffb7d0")), new Vector3(r * 0.3f, 0.095f, 0f), new Vector3(0.07f, 0.045f, 0.07f), false);
            return go;
        }

        /// <summary>A few small mushrooms (ground detail).</summary>
        public GameObject Mushrooms(Transform parent, Vector3 pos, System.Random rng)
        {
            var go = NewProp("Mushrooms", parent, pos, rng);
            var stem = art.NoOutline(Hex("#f1e6cf"));
            var caps = new[] { art.NoOutline(Hex("#d8463a")), art.NoOutline(Hex("#e0b068")) };
            int n = rng.Next(2, 4);
            for (int i = 0; i < n; i++)
            {
                var p = new Vector3(R(rng, -0.1f, 0.1f), 0f, R(rng, -0.1f, 0.1f));
                float s = R(rng, 0.7f, 1.2f);
                prim.Cylinder(go.transform, stem, p + new Vector3(0f, 0.04f * s, 0f), 0.014f * s, 0.08f * s);
                prim.Sphere(go.transform, caps[i % caps.Length], p + new Vector3(0f, 0.09f * s, 0f), new Vector3(0.1f * s, 0.06f * s, 0.1f * s), false);
            }
            return go;
        }

        // ------------------------------------------------------------------ deko objects

        public GameObject StoneLantern(Transform parent, Vector3 pos, float scale = 1.3f)
        {
            var go = new GameObject("StoneLantern");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var stone = art.Lit(Palette.Stone, PaintedTexture.Stone);
            var glow = art.Glow(ToonStyle.WindowGlow(false), ToonStyle.WindowGlow(false) * 0.7f);
            prim.Cylinder(go.transform, stone, new Vector3(0f, 0.04f * scale, 0f), 0.08f * scale, 0.08f * scale);
            prim.Cylinder(go.transform, stone, new Vector3(0f, 0.21f * scale, 0f), 0.038f * scale, 0.26f * scale);
            prim.Box(go.transform, stone, new Vector3(0f, 0.37f * scale, 0f), new Vector3(0.17f, 0.04f, 0.17f) * scale);
            prim.Box(go.transform, glow, new Vector3(0f, 0.45f * scale, 0f), new Vector3(0.1f, 0.12f, 0.1f) * scale);
            prim.Cone(go.transform, stone, new Vector3(0f, 0.51f * scale, 0f), 0.15f * scale, 0.12f * scale);
            return go;
        }

        public GameObject Torii(Transform parent, Vector3 pos, float yaw)
        {
            var go = new GameObject("Torii");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var red = art.Lit(Palette.BannerRed, PaintedTexture.Planks);
            var dark = art.Lit(Hex("#3a2a20"), PaintedTexture.Planks);
            foreach (var sx in new[] { -0.42f, 0.42f })
            {
                prim.Cylinder(go.transform, red, new Vector3(sx, 0.55f, 0f), 0.06f, 1.1f);
                prim.Cylinder(go.transform, dark, new Vector3(sx, 0.04f, 0f), 0.075f, 0.08f);
            }
            prim.Box(go.transform, red, new Vector3(0f, 0.86f, 0f), new Vector3(1.05f, 0.05f, 0.05f));
            prim.Box(go.transform, dark, new Vector3(0f, 1.14f, 0f), new Vector3(1.32f, 0.09f, 0.11f));
            prim.Box(go.transform, dark, new Vector3(-0.68f, 1.18f, 0f), new Vector3(0.16f, 0.06f, 0.11f), new Vector3(0f, 0f, 18f));
            prim.Box(go.transform, dark, new Vector3(0.68f, 1.18f, 0f), new Vector3(0.16f, 0.06f, 0.11f), new Vector3(0f, 0f, -18f));
            return go;
        }

        /// <summary>Fence with dark, round-topped posts and two rails (as in the style reference).</summary>
        public GameObject Fence(Transform parent, Vector3 pos, float length, float yaw)
        {
            var go = new GameObject("Fence");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var post = art.Lit(Palette.Timber, PaintedTexture.Planks);
            var rail = art.Lit(Palette.TimberLight, PaintedTexture.Planks);
            int posts = Mathf.Max(2, Mathf.CeilToInt(length / 0.4f) + 1);
            for (int i = 0; i < posts; i++)
            {
                float x = -length * 0.5f + length * i / (posts - 1);
                prim.BoxOnGround(go.transform, post, new Vector3(x, 0f, 0f), new Vector3(0.07f, 0.34f, 0.07f));
                prim.Sphere(go.transform, post, new Vector3(x, 0.35f, 0f), new Vector3(0.09f, 0.06f, 0.09f), false);
            }
            prim.Box(go.transform, rail, new Vector3(0f, 0.27f, 0f), new Vector3(length, 0.04f, 0.035f));
            prim.Box(go.transform, rail, new Vector3(0f, 0.15f, 0f), new Vector3(length, 0.04f, 0.035f));
            return go;
        }

        public GameObject LogPile(Transform parent, Vector3 pos, System.Random rng)
        {
            var go = NewProp("LogPile", parent, pos, rng);
            var bark = art.Lit(Palette.Bark, PaintedTexture.Bark);
            var cut = art.Lit(Palette.LogEnd, PaintedTexture.Planks);
            int[] rows = { 3, 2, 1 };
            for (int r = 0; r < rows.Length; r++)
            {
                for (int i = 0; i < rows[r]; i++)
                {
                    float x = (i - (rows[r] - 1) * 0.5f) * 0.2f;
                    prim.Cylinder(go.transform, bark, new Vector3(x, 0.09f + r * 0.16f, 0f), 0.09f, 0.6f, new Vector3(90f, 0f, 0f));
                    prim.Cylinder(go.transform, cut, new Vector3(x, 0.09f + r * 0.16f, -0.301f), 0.07f, 0.012f, new Vector3(90f, 0f, 0f));
                }
            }
            return go;
        }

        public GameObject Post(Transform parent, Vector3 pos)
        {
            var go = new GameObject("BoundaryPost");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            prim.BoxOnGround(go.transform, art.Lit(Palette.TimberLight, PaintedTexture.Planks), Vector3.zero, new Vector3(0.09f, 0.42f, 0.09f));
            prim.Box(go.transform, art.Lit(Palette.Straw, PaintedTexture.Thatch), new Vector3(0f, 0.36f, 0f), new Vector3(0.15f, 0.05f, 0.15f));
            return go;
        }

        public GameObject Rope(Transform parent, Vector3 from, Vector3 to)
        {
            var go = new GameObject("BoundaryRope");
            go.transform.SetParent(parent, false);
            var mid = (from + to) * 0.5f;
            var delta = to - from;
            go.transform.localPosition = mid;
            go.transform.localRotation = Quaternion.LookRotation(delta.normalized);
            prim.Box(go.transform, art.NoOutline(Palette.Straw), Vector3.zero, new Vector3(0.03f, 0.03f, delta.magnitude), Vector3.zero, false);
            return go;
        }

        /// <summary>Arched wooden bridge along +X spanning <paramref name="length"/> world units, on two stone abutments.</summary>
        public GameObject Bridge(Transform parent, Vector3 center, float length)
        {
            var go = new GameObject("Bridge");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            var plank = art.Lit(Palette.Plank, PaintedTexture.Planks);
            var plankLight = art.Lit(Palette.TimberLight, PaintedTexture.Planks);
            var dark = art.Lit(Palette.Timber, PaintedTexture.Planks);
            var stone = art.Lit(Palette.Stone, PaintedTexture.Stone);
            const float arch = 0.2f;
            int planks = Mathf.CeilToInt(length / 0.16f);
            for (int i = 0; i < planks; i++)
            {
                float u = (i + 0.5f) / planks;
                float x = -length * 0.5f + u * length;
                float y = 0.1f + arch * (1f - (2f * u - 1f) * (2f * u - 1f));
                float slope = Mathf.Atan(arch * -4f * (2f * u - 1f) / length) * Mathf.Rad2Deg;
                prim.Box(go.transform, i % 2 == 0 ? plank : plankLight, new Vector3(x, y, 0f), new Vector3(length / planks * 0.92f, 0.05f, 0.62f), new Vector3(0f, 0f, slope));
            }
            foreach (var sz in new[] { -0.33f, 0.33f })
            {
                int segs = Mathf.CeilToInt(length / 0.3f);
                for (int i = 0; i < segs; i++)
                {
                    float u = (i + 0.5f) / segs;
                    float x = -length * 0.5f + u * length;
                    float y = 0.1f + arch * (1f - (2f * u - 1f) * (2f * u - 1f));
                    float slope = Mathf.Atan(arch * -4f * (2f * u - 1f) / length) * Mathf.Rad2Deg;
                    prim.Box(go.transform, dark, new Vector3(x, y + 0.26f, sz), new Vector3(length / segs * 1.05f, 0.05f, 0.05f), new Vector3(0f, 0f, slope), false);
                }
                int posts = Mathf.CeilToInt(length / 0.6f) + 1;
                for (int i = 0; i < posts; i++)
                {
                    float u = (float)i / (posts - 1);
                    float y = 0.1f + arch * (1f - (2f * u - 1f) * (2f * u - 1f));
                    prim.BoxOnGround(go.transform, dark, new Vector3(-length * 0.5f + u * length, y - 0.03f, sz), new Vector3(0.07f, 0.34f, 0.07f));
                }
            }
            foreach (var sx in new[] { -1f, 1f })
                prim.BoxOnGround(go.transform, stone, new Vector3(sx * (length * 0.5f + 0.02f), -0.02f, 0f), new Vector3(0.34f, 0.2f, 0.92f));
            return go;
        }

        public GameObject Well(Transform parent, Vector3 pos, System.Random rng)
        {
            var go = NewProp("Well", parent, pos, rng);
            var stone = art.Lit(Palette.Stone, PaintedTexture.Stone);
            var wood = art.Lit(Palette.Timber, PaintedTexture.Planks);
            prim.Cylinder(go.transform, stone, new Vector3(0f, 0.17f, 0f), 0.26f, 0.34f);
            prim.Cylinder(go.transform, art.NoOutline(Palette.WaterDeep), new Vector3(0f, 0.325f, 0f), 0.2f, 0.02f);
            foreach (var sx in new[] { -0.24f, 0.24f }) prim.BoxOnGround(go.transform, wood, new Vector3(sx, 0.3f, 0f), new Vector3(0.06f, 0.5f, 0.06f));
            prim.MeshObject(go.transform, ProceduralMeshes.GableRoof(0.6f, 0.52f, 0.22f, 0.3f, 0.04f), art.Lit(Palette.RoofTile, PaintedTexture.Roof), new Vector3(0f, 0.78f, 0f), new Vector3(0f, 90f, 0f));
            prim.Cylinder(go.transform, wood, new Vector3(0f, 0.66f, 0f), 0.025f, 0.56f, new Vector3(0f, 0f, 90f));
            return go;
        }

        public GameObject Signboard(Transform parent, Vector3 pos, float yaw)
        {
            var go = new GameObject("Signboard");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var wood = art.Lit(Palette.Timber, PaintedTexture.Planks);
            var board = art.Lit(Palette.TimberLight, PaintedTexture.Planks);
            foreach (var sx in new[] { -0.3f, 0.3f }) prim.BoxOnGround(go.transform, wood, new Vector3(sx, 0f, 0f), new Vector3(0.07f, 0.72f, 0.07f));
            prim.Box(go.transform, board, new Vector3(0f, 0.52f, -0.05f), new Vector3(0.64f, 0.32f, 0.04f));
            prim.Box(go.transform, wood, new Vector3(0f, 0.7f, -0.04f), new Vector3(0.74f, 0.05f, 0.09f));
            return go;
        }

        public GameObject Barrel(Transform parent, Vector3 pos, System.Random rng)
        {
            var go = NewProp("Barrel", parent, pos, rng);
            var wood = art.Lit(Palette.TimberLight, PaintedTexture.Planks);
            var hoop = art.Lit(Palette.Timber);
            prim.Cylinder(go.transform, wood, new Vector3(0f, 0.17f, 0f), 0.14f, 0.34f);
            prim.Cylinder(go.transform, hoop, new Vector3(0f, 0.09f, 0f), 0.148f, 0.03f);
            prim.Cylinder(go.transform, hoop, new Vector3(0f, 0.25f, 0f), 0.148f, 0.03f);
            return go;
        }

        public GameObject Crate(Transform parent, Vector3 pos, System.Random rng)
        {
            var go = NewProp("Crate", parent, pos, rng);
            var wood = art.Lit(Palette.Plank, PaintedTexture.Planks);
            var frame = art.Lit(Palette.Timber, PaintedTexture.Planks);
            prim.BoxOnGround(go.transform, wood, Vector3.zero, new Vector3(0.34f, 0.26f, 0.26f));
            prim.BoxOnGround(go.transform, frame, new Vector3(0f, 0.22f, 0f), new Vector3(0.37f, 0.04f, 0.29f));
            prim.Add(go.transform, ToonMeshes.Blob(1, 2, 0.3f, FoliagePalette.Leaf), art.Foliage(Color.white, false), new Vector3(0f, 0.27f, 0f), new Vector3(0.28f, 0.12f, 0.2f), default, "Greens", false);
            return go;
        }

        /// <summary>Low rough stone wall segment along X.</summary>
        public GameObject StoneWall(Transform parent, Vector3 pos, float length, float yaw)
        {
            var go = new GameObject("StoneWall");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var stone = art.Lit(Palette.Stone, PaintedTexture.Stone);
            var stoneDark = art.Lit(Palette.StoneDark, PaintedTexture.Stone);
            prim.BoxOnGround(go.transform, stoneDark, Vector3.zero, new Vector3(length, 0.16f, 0.26f));
            prim.BoxOnGround(go.transform, stone, new Vector3(0f, 0.16f, 0f), new Vector3(length * 0.92f, 0.14f, 0.22f));
            return go;
        }

        public GameObject BannerPole(Transform parent, Vector3 pos, float yaw)
        {
            var go = new GameObject("Banner");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var wood = art.Lit(Palette.Timber, PaintedTexture.Planks);
            prim.Cylinder(go.transform, wood, new Vector3(0f, 0.75f, 0f), 0.03f, 1.5f);
            prim.Sphere(go.transform, art.Lit(Palette.Gold), new Vector3(0f, 1.52f, 0f), 0.05f);
            prim.Box(go.transform, wood, new Vector3(0.14f, 1.4f, 0f), new Vector3(0.3f, 0.03f, 0.03f));
            prim.Box(go.transform, art.Lit(Palette.Hex("#f1e8d4")), new Vector3(0.15f, 1.12f, 0f), new Vector3(0.24f, 0.56f, 0.02f), default, false);
            prim.Cylinder(go.transform, art.Lit(Palette.BannerRed), new Vector3(0.15f, 1.12f, -0.012f), 0.07f, 0.01f, new Vector3(90f, 0f, 0f));
            return go;
        }

        // ------------------------------------------------------------------ landscape

        public GameObject Hill(Transform parent, Vector3 pos, float radius, float height, System.Random rng)
        {
            var go = new GameObject("Hill");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var tint = Color.Lerp(Color.white, new Color(0.88f, 0.98f, 0.85f), (float)rng.NextDouble());
            prim.Add(go.transform, ToonMeshes.Blob(ToonStyle.FoliageSubdivisionsNear, rng.Next(6), 0.08f, FoliagePalette.Leaf), art.Foliage(tint, false),
                new Vector3(0f, -height * 0.15f, 0f), new Vector3(radius * 2f, height * 2f, radius * 2f), default, "HillBody", false);
            int trees = rng.Next(3, 6);
            for (int i = 0; i < trees; i++)
            {
                float a = R(rng, 0f, 6.28f);
                float r = R(rng, 0f, radius * 0.55f);
                float y = height * 0.82f * (1f - (r / radius) * (r / radius)) - height * 0.1f;
                var at = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                int kind = rng.Next(6);
                if (kind < 3) Pine(go.transform, at, rng);
                else if (kind < 5) RoundTree(go.transform, at, rng);
                else Birch(go.transform, at, rng);
            }
            return go;
        }

        public GameObject Butterfly(Transform parent, Vector3 center, System.Random rng)
        {
            var root = new GameObject("Butterfly");
            root.transform.SetParent(parent, false);
            var colors = new[] { Palette.Hex("#f5e36b"), Palette.Hex("#f7f7f0"), Palette.Hex("#e7a0d0") };
            var mat = art.Unlit(colors[rng.Next(colors.Length)]);
            var wingA = new GameObject("WingA").transform;
            wingA.SetParent(root.transform, false);
            var wingB = new GameObject("WingB").transform;
            wingB.SetParent(root.transform, false);
            prim.Box(wingA, mat, new Vector3(-0.045f, 0f, 0f), new Vector3(0.09f, 0.008f, 0.07f), Vector3.zero, false);
            prim.Box(wingB, mat, new Vector3(0.045f, 0f, 0f), new Vector3(0.09f, 0.008f, 0.07f), Vector3.zero, false);
            animator.AddButterfly(root.transform, wingA, wingB, center, R(rng, 0.8f, 1.6f), R(rng, 0.8f, 1.6f), R(rng, 0.5f, 0.9f), R(rng, 0f, 6.28f), R(rng, 0.45f, 0.8f));
            return root;
        }

        private static Color Hex(string s) => Palette.Hex(s);
    }
}
