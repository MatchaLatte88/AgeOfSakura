using System;
using System.Collections.Generic;
using UnityEngine;

namespace AgeOfSakura.Game
{
    /// <summary>
    /// Metadata a model exposes to the (gameplay-independent) building view. A replacement prefab only needs
    /// this component; footprint, selection, production and save data never read the mesh.
    /// </summary>
    public sealed class BuildingModel : MonoBehaviour
    {
        /// <summary>Height above ground where world indicators float.</summary>
        public float IndicatorHeight = 2.4f;
        /// <summary>Height of the tap collider.</summary>
        public float ColliderHeight = 1.8f;
        /// <summary>Optional chimney anchor; the view adds smoke there.</summary>
        public Transform SmokeAnchor;
        /// <summary>
        /// A camera-facing sprite has only one authored angle. When true, placement rotation still changes the
        /// logical footprint but does not turn the visual away from the fixed isometric camera.
        /// </summary>
        public bool IgnorePlacementRotation;
    }

    /// <summary>
    /// Produces the model for a building's visualId. The default implementation builds the 3D-Toon placeholders;
    /// a prefab-based provider can replace it without touching gameplay.
    /// Model space: origin at the footprint centre on the ground, footprint spans width x depth cells, the front (door) faces -Z.
    /// </summary>
    public interface IBuildingModelProvider
    {
        bool Has(string visualId);
        GameObject Create(string visualId, int visualLevel, int footprintWidth, int footprintHeight);
    }

    /// <summary>
    /// 3D-Toon buildings: rounded timber/plaster/thatch bodies with painted textures, glowing windows, up-swept roofs, and three looks per
    /// building (visual level 1..3). Children are grouped in the style's build order (Ground, Body, Roof, Trees, Props) so the
    /// construction animation (<see cref="BuildInAnimation"/>) can drop them in one after the other; the static groups are merged into
    /// a few meshes (<see cref="ModelBatcher"/>). Everything stays inside the footprint (plus a small margin for shrubs), so neighbouring
    /// buildings and the sakura trees of the world can never clip into a model.
    /// </summary>
    public sealed class ProceduralBuildingModels : IBuildingModelProvider
    {
        private readonly GameArt art;
        private readonly Prim prim;
        private readonly VfxFactory vfx;
        private readonly Dictionary<string, Func<int, int, int, GameObject>> builders;

        /// <summary>Set once the world's prop factory exists; buildings then grow their own small trees and shrubs.</summary>
        public PropFactory Props { get; set; }

        public ProceduralBuildingModels(GameArt art, Prim prim, VfxFactory vfx)
        {
            this.art = art;
            this.prim = prim;
            this.vfx = vfx;
            builders = new Dictionary<string, Func<int, int, int, GameObject>>
            {
                { "town_hall", TownHall },
                { "house", House },
                { "woodcutter", Woodcutter },
                { "rice_paddy", RicePaddy },
                { "garden", Garden },
                { "shrine", Shrine }
            };
        }

        /// <summary>Every visualId this provider can build (used by editor-time validation without creating materials).</summary>
        public static readonly string[] KnownVisualIds = { "town_hall", "house", "woodcutter", "rice_paddy", "garden", "shrine" };

        public bool Has(string visualId) => builders.ContainsKey(visualId);

        public GameObject Create(string visualId, int visualLevel, int footprintWidth, int footprintHeight)
        {
            if (!builders.TryGetValue(visualId, out var build)) throw new KeyNotFoundException($"No model for visualId '{visualId}'.");
            var model = build(Mathf.Clamp(visualLevel, 1, 3), footprintWidth, footprintHeight);
            ModelBatcher.Merge(model);
            model.AddComponent<BuildInAnimation>();
            return model;
        }

        // ------------------------------------------------------------------ helpers

        private GameObject NewRoot(string name, float indicatorHeight, float colliderHeight)
        {
            var root = new GameObject(name);
            var model = root.AddComponent<BuildingModel>();
            model.IndicatorHeight = indicatorHeight;
            model.ColliderHeight = colliderHeight;
            return root;
        }

        private static Transform Group(GameObject root, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(root.transform, false);
            return t;
        }

        private static System.Random Rng(string id)
        {
            int h = 17;
            foreach (char c in id) h = h * 31 + c;
            return new System.Random(h);
        }

        private static Color Hex(string s) => Palette.Hex(s);

        private Material StoneMat => art.Lit(Palette.Stone, PaintedTexture.Stone);
        private Material StoneDarkMat => art.Lit(Palette.StoneDark, PaintedTexture.Stone);
        private Material PlasterMat => art.Lit(Palette.Plaster, PaintedTexture.Plaster);
        private Material FrameMat => art.Lit(Palette.Timber, PaintedTexture.Planks);
        private Material WoodMat => art.Lit(Palette.TimberLight, PaintedTexture.Planks);
        private Material BeamMat => art.Lit(Palette.Plank, PaintedTexture.Planks);
        private Material DoorMat => art.Lit(Palette.TimberLight, PaintedTexture.Door);
        private Material ShojiMat => art.Lit(Color.white, PaintedTexture.Shoji);
        private Material ThatchMat => art.Lit(Palette.Straw, PaintedTexture.Thatch);
        private Material ThatchDarkMat => art.Lit(Palette.StrawDark, PaintedTexture.Thatch);
        private Material SlateMat => art.Lit(Palette.RoofTile, PaintedTexture.Roof);
        private Material SlateLightMat => art.Lit(Palette.RoofTileLight, PaintedTexture.Roof);
        /// <summary>Tile roof whose uv follows the slope (columns of tiles flow down every side), for the lofted pagoda roofs.</summary>
        private Material SlateUvMat => art.LitUv(Palette.RoofTile, PaintedTexture.RoofTiles);
        private Material RedMat => art.Lit(Palette.BannerRed, PaintedTexture.Planks);
        private Material GoldMat => art.Lit(Palette.Gold);
        private Material LogEndMat => art.Lit(Palette.LogEnd, PaintedTexture.LogEnd);
        private Material BarkMat => art.Lit(Palette.Bark, PaintedTexture.Bark);
        private Material SteelMat => art.Lit(Hex("#9aa3ad"));
        private Material GlowMat => art.Glow(ToonStyle.WindowGlow(false), ToonStyle.WindowGlow(false) * 0.55f);
        private Material LanternGlowMat => art.Glow(Hex("#d8452f"), Hex("#ff9a52") * 0.55f);

        /// <summary>Height of the lofted roof surface above the eaves at horizontal fraction f (0 eave .. 1 ridge); mirrors ProceduralMeshes.Roof.</summary>
        private static float RoofSurface(float height, float curve, float f)
        {
            f = Mathf.Clamp01(f);
            return f <= curve ? height * 0.38f * f / curve : height * 0.38f + height * 0.62f * (f - curve) / (1f - curve);
        }

        /// <summary>Runs <paramref name="make"/> with the prop factory's crown limit set, so a tree inside a model cannot grow past it.</summary>
        private GameObject Limited(float radius, Func<GameObject> make)
        {
            float previous = Props.MaxRadius;
            Props.MaxRadius = radius;
            var go = make();
            Props.MaxRadius = previous;
            return go;
        }

        /// <summary>Glowing window with a dark frame and cross bars. <paramref name="alongX"/>: the wall's normal points along X (the window lies in the ZY plane).</summary>
        private void Window(Transform t, Vector3 center, float width, float height, bool alongX)
        {
            var frame = FrameMat;
            float bar = 0.028f, depth = 0.04f;
            Vector3 glow = alongX ? new Vector3(0.03f, height, width) : new Vector3(width, height, 0.03f);
            prim.Box(t, GlowMat, center, glow);
            Vector3 H = alongX ? new Vector3(depth, bar, width + bar * 2f) : new Vector3(width + bar * 2f, bar, depth);
            Vector3 V = alongX ? new Vector3(depth, height + bar * 2f, bar) : new Vector3(bar, height + bar * 2f, depth);
            prim.Box(t, frame, center + new Vector3(0f, height * 0.5f + bar * 0.5f, 0f), H, default, false);
            prim.Box(t, frame, center - new Vector3(0f, height * 0.5f + bar * 0.5f, 0f), H, default, false);
            prim.Box(t, frame, center, H, default, false);
            var side = alongX ? new Vector3(0f, 0f, width * 0.5f + bar * 0.5f) : new Vector3(width * 0.5f + bar * 0.5f, 0f, 0f);
            prim.Box(t, frame, center + side, V, default, false);
            prim.Box(t, frame, center - side, V, default, false);
            prim.Box(t, frame, center, V, default, false);
        }

        /// <summary>A few rounded moss patches lying on a roof slope. Slope facing -Z (sign -1) or +Z.</summary>
        private void MossPatches(Transform t, float roofBase, float hd, float height, float curve, float zSign, params Vector2[] xf)
        {
            var moss = art.Lit(Color.Lerp(Color.Lerp(ToonStyle.Moss, Palette.Straw, 0.4f), Palette.GrassDark, 0.3f)); // olive: a green that sits in thatch
            foreach (var p in xf)
            {
                float f = p.y;
                float z = zSign * (hd * (1f - f));
                float y = roofBase + RoofSurface(height, curve, f) + 0.01f;
                float slope = Mathf.Atan2(RoofSurface(height, curve, f + 0.05f) - RoofSurface(height, curve, f - 0.05f), hd * 0.1f) * Mathf.Rad2Deg;
                var patch = prim.Sphere(t, moss, new Vector3(p.x, y, z), new Vector3(0.2f, 0.05f, 0.14f), false);
                patch.transform.localRotation = Quaternion.Euler(slope * zSign, 0f, 0f);
            }
        }

        private Transform SmokeAnchorAt(GameObject root, Transform parent, Vector3 pos)
        {
            var anchor = new GameObject("SmokeAnchor").transform;
            anchor.SetParent(parent, false);
            anchor.localPosition = pos;
            root.GetComponent<BuildingModel>().SmokeAnchor = anchor;
            return anchor;
        }

        /// <summary>Stone chimney with a banded shaft and a capped top; the smoke anchor sits above it.</summary>
        private void Chimney(GameObject root, Transform parent, Vector3 basePos, float height)
        {
            var stone = StoneMat;
            prim.BoxOnGround(parent, stone, basePos, new Vector3(0.2f, height, 0.2f));
            prim.BoxOnGround(parent, StoneDarkMat, basePos + new Vector3(0f, height * 0.45f, 0f), new Vector3(0.225f, 0.035f, 0.225f));
            prim.BoxOnGround(parent, StoneDarkMat, basePos + new Vector3(0f, height - 0.04f, 0f), new Vector3(0.27f, 0.07f, 0.27f));
            prim.BoxOnGround(parent, StoneDarkMat, basePos + new Vector3(0f, height + 0.03f, 0f), new Vector3(0.23f, 0.03f, 0.23f));
            SmokeAnchorAt(root, parent, basePos + new Vector3(0f, height + 0.1f, 0f));
        }

        /// <summary>Red paper lantern on a short cord; glows like the windows.</summary>
        private void PaperLantern(Transform t, Vector3 pos, float scale)
        {
            var dark = art.Lit(Hex("#3a2a20"));
            prim.Cylinder(t, dark, pos + new Vector3(0f, 0.085f * scale, 0f), 0.006f * scale, 0.09f * scale);
            prim.Cylinder(t, dark, pos + new Vector3(0f, 0.07f * scale, 0f), 0.045f * scale, 0.02f * scale);
            prim.Sphere(t, LanternGlowMat, pos, new Vector3(0.11f, 0.14f, 0.11f) * scale, false);
            prim.Cylinder(t, dark, pos - new Vector3(0f, 0.07f * scale, 0f), 0.04f * scale, 0.02f * scale);
        }

        /// <summary>Thick thatch edge along both eaves of a gable roof (the roof surface alone is paper thin).</summary>
        private void EaveBand(Transform roof, float roofY, float roofW, float roofD, Material m)
        {
            foreach (var s in new[] { -1f, 1f })
                prim.Box(roof, m, new Vector3(0f, roofY + 0.045f, s * (roofD * 0.5f + 0.055f)), new Vector3(roofW + 0.12f, 0.06f, 0.055f), default, false);
        }

        /// <summary>Wrapped bands around a ridge roll (thatched ridges are tied every few hand widths).</summary>
        private void RidgeBands(Transform roof, float y, float length, float radius, Material m)
        {
            int count = Mathf.Max(3, Mathf.RoundToInt(length / 0.25f));
            for (int i = 0; i < count; i++)
            {
                float x = -length * 0.5f + length * (i + 0.5f) / count;
                prim.Cylinder(roof, m, new Vector3(x, y, 0f), radius, 0.024f, new Vector3(0f, 0f, 90f), false); // thin: a wide disc would read as a dark spot
            }
        }

        private void Bush(Transform parent, Vector3 pos, float scale, System.Random rng)
        {
            if (Props == null) return;
            var b = Props.Shrub(parent, pos, rng);
            b.transform.localScale = Vector3.one * scale;
        }

        private void BlossomBush(Transform parent, Vector3 pos, float scale, System.Random rng)
        {
            if (Props == null) return;
            var b = Props.BlossomShrub(parent, pos, rng);
            b.transform.localScale = Vector3.one * scale;
        }

        /// <summary>Flattened stones set along a line (foundation skirts, paths).</summary>
        private void StoneLine(Transform t, Vector3 from, Vector3 to, int count, float size, System.Random rng, Material m)
        {
            for (int i = 0; i < count; i++)
            {
                var p = Vector3.Lerp(from, to, (i + 0.5f) / count);
                float k = size * (0.85f + (float)rng.NextDouble() * 0.3f);
                var stone = prim.Sphere(t, m, p, new Vector3(k * 1.3f, k * 0.7f, k), false);
                stone.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 180f, 0f);
            }
        }

        // ---------------------------------------------------------------- Town Hall (3x3), levels 1..3 = 2..4 storeys of a castle-like tower
        //
        // Modelled after the reference art: stone terrace with a stair on each of the two faces the camera sees, red-pillared veranda around a
        // white plaster ground floor, upper storeys in dark timber and plaster, dark blue-grey tile roofs that sag and swing up at the corners
        // (hip ridges in lighter tile, gold edge lines and corner tips), a cusped karahafu gable in front of every roof, golden shachihoko
        // on the top ridge and nobori banners on the terrace. Everything is built once for the -Z face and mirrored onto the -X face by a
        // 90 degree pivot, so the detail cost and the look are the same on both sides.

        private static Transform Pivot(Transform parent, float yaw)
        {
            var t = new GameObject("Face").transform;
            t.SetParent(parent, false);
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return t;
        }

        private Material GoldTrimMat => art.NoOutline(Palette.Gold);

        /// <summary>Ring parameter of the golden tip at each eave corner (just inside the up-flicked lip).</summary>
        private const float PagodaTip = -0.03f;

        /// <summary>Golden shachihoko (fish with a tail curled over its head) on a ridge end; sign +1 for the +X end, -1 for the -X end.</summary>
        private void Shachihoko(Transform t, Vector3 pos, float sign, float s)
        {
            var gold = GoldMat;
            var pts = new[]
            {
                new Vector3(0f, 0.02f, 0f), new Vector3(0.012f, 0.10f, 0f), new Vector3(0f, 0.19f, 0f),
                new Vector3(-0.06f, 0.26f, 0f), new Vector3(-0.13f, 0.28f, 0f), new Vector3(-0.19f, 0.24f, 0f)
            };
            var radii = new[] { 0.05f, 0.047f, 0.04f, 0.03f, 0.021f, 0.011f };
            for (int i = 0; i < pts.Length; i++)
            {
                pts[i] = new Vector3(pts[i].x * sign, pts[i].y, pts[i].z) * s;
                radii[i] *= s;
            }
            prim.MeshObject(t, ProceduralMeshes.Tube(pts, radii, 6), gold, pos);
            prim.Sphere(t, gold, pos + new Vector3(sign * 0.045f, 0.05f, 0f) * s, new Vector3(0.13f, 0.1f, 0.1f) * s);
            var snout = prim.Cone(t, gold, pos + new Vector3(sign * 0.09f, 0.055f, 0f) * s, 0.03f * s, 0.07f * s);
            snout.transform.localRotation = Quaternion.Euler(0f, 0f, -sign * 70f);
            for (int k = 1; k <= 3; k++)
            {
                var spike = prim.Cone(t, gold, pos + pts[k] + new Vector3(sign * radii[k] * 0.9f, 0f, 0f), 0.014f * s, 0.055f * s);
                spike.transform.localRotation = Quaternion.Euler(0f, 0f, -sign * (35f + k * 15f));
            }
            prim.Sphere(t, gold, pos + pts[5] + new Vector3(-sign * 0.02f, 0.012f, 0f) * s, new Vector3(0.07f, 0.02f, 0.05f) * s, false);
        }

        /// <summary>Tall golden finial (rings and a flame tip) on the very top of the tower.</summary>
        private void Finial(Transform t, Vector3 pos, float s)
        {
            var gold = GoldMat;
            prim.Cylinder(t, gold, pos + new Vector3(0f, 0.02f * s, 0f), 0.075f * s, 0.04f * s);
            prim.Cylinder(t, gold, pos + new Vector3(0f, 0.07f * s, 0f), 0.045f * s, 0.06f * s);
            prim.Sphere(t, gold, pos + new Vector3(0f, 0.13f * s, 0f), 0.055f * s);
            for (int k = 0; k < 4; k++)
                prim.Cylinder(t, gold, pos + new Vector3(0f, (0.19f + 0.06f * k) * s, 0f), (0.062f - 0.009f * k) * s, 0.024f * s);
            prim.Cone(t, gold, pos + new Vector3(0f, 0.2f * s, 0f), 0.02f * s, 0.34f * s);
            prim.Sphere(t, gold, pos + new Vector3(0f, 0.5f * s, 0f), 0.034f * s);
            prim.Cone(t, gold, pos + new Vector3(0f, 0.51f * s, 0f), 0.02f * s, 0.09f * s);
        }

        /// <summary>
        /// Cusped gable sitting on a roof slope: plaster board with a gold-edged round window, slate cap swept back up the slope.
        /// Local frame: origin on the roof surface, -Z outwards, the cap runs along +Z (and rises by <paramref name="rise"/>).
        /// </summary>
        private void HallGable(Transform roof, Vector3 origin, float width, float height, float run, float rise)
        {
            var curve = ProceduralMeshes.KarahafuCurve(width, height);
            prim.MeshObject(roof, ProceduralMeshes.GableBoard(curve, 0.17f, 0.05f), PlasterMat, origin);
            prim.MeshObject(roof, ProceduralMeshes.GableCap(curve, 0.045f, run, rise, 0.04f), SlateUvMat, origin);

            // gold bargeboard edge and end tips
            var edge = new Vector3[curve.Length];
            var radii = new float[curve.Length];
            for (int i = 0; i < curve.Length; i++)
            {
                edge[i] = new Vector3(curve[i].x, curve[i].y + 0.045f + 0.012f, -0.045f * 0.9f - 0.012f);
                radii[i] = 0.017f;
            }
            prim.MeshObject(roof, ProceduralMeshes.Tube(edge, radii, 5), GoldTrimMat, origin, default);
            foreach (var end in new[] { edge[0], edge[edge.Length - 1] }) prim.Sphere(roof, GoldTrimMat, origin + end, 0.03f);

            // gold crest (mon) with a red heart on the plaster, timber slats either side
            var c = origin + new Vector3(0f, height * 0.4f, -0.012f);
            float cr = Mathf.Min(0.065f, width * 0.11f);
            prim.Cylinder(roof, GoldMat, c, cr, 0.026f, new Vector3(90f, 0f, 0f), false);
            prim.Cylinder(roof, RedMat, c + new Vector3(0f, 0f, -0.006f), cr * 0.55f, 0.028f, new Vector3(90f, 0f, 0f), false);
            foreach (var sx in new[] { -1f, 1f })
                for (int k = 1; k <= 2; k++)
                    prim.Box(roof, FrameMat, origin + new Vector3(sx * width * (0.12f + 0.1f * k), height * 0.16f, -0.012f), new Vector3(0.026f, height * 0.3f, 0.02f), default, false);
        }

        /// <summary>Stone stair from the lower slab up to the terrace, with cheek walls; built for the -Z face.</summary>
        private void HallStairs(Transform t, float slabH, float deckY)
        {
            var stone = StoneMat;
            var dark = StoneDarkMat;
            prim.BoxOnGround(t, stone, new Vector3(0f, slabH, -1.32f), new Vector3(0.96f, 0.075f, 0.34f));
            prim.BoxOnGround(t, stone, new Vector3(0f, slabH, -1.26f), new Vector3(0.96f, 0.15f, 0.22f));
            foreach (var sx in new[] { -0.53f, 0.53f })
            {
                prim.BoxOnGround(t, dark, new Vector3(sx, slabH, -1.41f), new Vector3(0.1f, 0.12f, 0.16f));
                prim.BoxOnGround(t, dark, new Vector3(sx, slabH, -1.24f), new Vector3(0.1f, deckY - slabH, 0.18f));
                prim.Sphere(t, stone, new Vector3(sx, slabH + 0.13f, -1.47f), new Vector3(0.11f, 0.08f, 0.11f), false);
            }
        }

        /// <summary>Ground floor of one face: shoji doors under a gold plaque, latticed windows, red pillars with bracket sets, rail, lanterns.</summary>
        private void HallGroundFloorFace(Transform b, float y, float half, float wallH, Material pillar, float deckY)
        {
            var frame = FrameMat;
            var gold = GoldMat;
            float z0 = -half - 0.005f;
            float top = y + wallH;

            // entrance
            prim.Box(b, ShojiMat, new Vector3(-0.15f, y + 0.31f, z0), new Vector3(0.28f, 0.6f, 0.04f));
            prim.Box(b, ShojiMat, new Vector3(0.15f, y + 0.31f, z0), new Vector3(0.28f, 0.6f, 0.04f));
            prim.Box(b, frame, new Vector3(0f, y + 0.63f, z0 - 0.015f), new Vector3(0.8f, 0.06f, 0.06f));
            prim.Box(b, frame, new Vector3(0f, y + 0.31f, z0 - 0.012f), new Vector3(0.03f, 0.6f, 0.05f), default, false);
            foreach (var sx in new[] { -0.38f, 0.38f }) prim.BoxOnGround(b, frame, new Vector3(sx, y, z0 - 0.015f), new Vector3(0.06f, 0.66f, 0.06f));
            prim.Sphere(b, gold, new Vector3(0.05f, y + 0.31f, z0 - 0.04f), 0.028f);
            prim.Sphere(b, gold, new Vector3(-0.05f, y + 0.31f, z0 - 0.04f), 0.028f);
            prim.Box(b, gold, new Vector3(0f, y + 0.72f, z0 - 0.03f), new Vector3(0.34f, 0.085f, 0.03f), default, false);
            prim.Box(b, frame, new Vector3(0f, y + 0.72f, z0 - 0.018f), new Vector3(0.38f, 0.115f, 0.02f), default, false);

            // windows either side of the door, on a sill board
            foreach (var sx in new[] { -0.6f, 0.6f })
            {
                Window(b, new Vector3(sx, y + 0.47f, z0), 0.26f, 0.36f, false);
                prim.Box(b, frame, new Vector3(sx, y + 0.265f, z0 - 0.025f), new Vector3(0.36f, 0.04f, 0.07f), default, false);
                foreach (var dx in new[] { -0.09f, 0.09f })
                    prim.Box(b, frame, new Vector3(sx + dx, y + 0.47f, z0 - 0.012f), new Vector3(0.018f, 0.36f, 0.03f), default, false);
            }

            // pillar ring: base stone, gold band, capital and a bracket set carrying the eaves
            const float pz = -1.04f;
            float pillarTop = top + 0.05f;
            foreach (var px in new[] { -1.04f, -0.52f, 0.52f })
            {
                prim.Cylinder(b, StoneDarkMat, new Vector3(px, deckY + 0.04f, pz), 0.085f, 0.07f);
                prim.Cylinder(b, pillar, new Vector3(px, (deckY + pillarTop) * 0.5f + 0.03f, pz), 0.052f, pillarTop - deckY - 0.03f);
                prim.Cylinder(b, GoldMat, new Vector3(px, deckY + 0.11f, pz), 0.06f, 0.022f);
                prim.Cylinder(b, GoldMat, new Vector3(px, pillarTop - 0.14f, pz), 0.06f, 0.022f);
                prim.Box(b, frame, new Vector3(px, pillarTop - 0.045f, pz), new Vector3(0.14f, 0.08f, 0.14f));
                prim.Box(b, frame, new Vector3(px, pillarTop - 0.005f, pz), new Vector3(0.32f, 0.045f, 0.11f));
                prim.Box(b, frame, new Vector3(px, pillarTop - 0.04f, pz - 0.1f), new Vector3(0.09f, 0.045f, 0.17f));
                prim.Sphere(b, gold, new Vector3(px, pillarTop - 0.04f, pz - 0.19f), Vector3.one * 0.05f, false);
            }
            prim.Box(b, pillar, new Vector3(0f, top - 0.03f, pz), new Vector3(2.16f, 0.07f, 0.07f));
            prim.Box(b, pillar, new Vector3(0f, y + 0.07f, pz), new Vector3(2.16f, 0.05f, 0.05f), default, false);

            // low red rail (koran) either side of the stair, gold-capped posts
            foreach (var cx in new[] { -0.83f, 0.83f })
            {
                prim.Box(b, pillar, new Vector3(cx, deckY + 0.26f, -1.11f), new Vector3(0.54f, 0.04f, 0.05f), default, false);
                prim.Box(b, pillar, new Vector3(cx, deckY + 0.14f, -1.11f), new Vector3(0.54f, 0.03f, 0.04f), default, false);
                for (int k = -2; k <= 2; k++) prim.BoxOnGround(b, pillar, new Vector3(cx + k * 0.115f, deckY, -1.11f), new Vector3(0.026f, 0.24f, 0.026f));
                foreach (var e in new[] { -0.27f, 0.27f })
                {
                    prim.BoxOnGround(b, pillar, new Vector3(cx + e, deckY, -1.11f), new Vector3(0.05f, 0.27f, 0.05f));
                    prim.Sphere(b, gold, new Vector3(cx + e, deckY + 0.3f, -1.11f), Vector3.one * 0.064f, false);
                }
            }

            // hanging paper lanterns between the pillars
            foreach (var lx in new[] { -0.78f, 0.78f }) PaperLantern(b, new Vector3(lx, top - 0.13f, pz - 0.02f), 0.85f);
        }

        /// <summary>Upper storey of one face: two latticed windows on sills, timber studs, gold fittings on the corner posts.</summary>
        private void HallUpperFace(Transform b, float y, float half, float wallH, int storey, int level)
        {
            var frame = FrameMat;
            float z0 = -half - 0.005f;
            // the eave of the storey above hides the upper part of the wall from the fixed camera: the windows sit low
            float wy = y + wallH * 0.36f;
            float ww = Mathf.Max(0.16f, half * 0.34f);
            float wh = wallH * 0.34f;
            foreach (var sx in new[] { -1f, 1f })
            {
                float x = sx * half * 0.56f;
                Window(b, new Vector3(x, wy, z0), ww, wh, false);
                prim.Box(b, frame, new Vector3(x, wy - wh * 0.5f - 0.05f, z0 - 0.02f), new Vector3(ww + 0.1f, 0.035f, 0.065f), default, false);
                prim.BoxOnGround(b, frame, new Vector3(sx * half * 0.24f, y + 0.03f, z0 - 0.005f), new Vector3(0.04f, wallH * 0.55f, 0.04f));
                prim.Box(b, GoldMat, new Vector3(sx * half, y + wallH * 0.42f, z0 - 0.03f), new Vector3(0.075f, 0.1f, 0.02f), default, false);
            }
            if (storey == 1 && level >= 2) PaperLantern(b, new Vector3(half * 0.3f, y + wallH * 0.5f, -half - 0.13f), 0.7f);
        }

        private GameObject TownHall(int level, int w, int d)
        {
            prim.ThinBoxes = true; // hundreds of bars and slats: flat boxes for everything hair-thin
            try { return BuildTownHall(level); }
            finally { prim.ThinBoxes = false; }
        }

        private GameObject BuildTownHall(int level)
        {
            int tiers = level + 1;
            var rng = Rng("town_hall");
            var root = NewRoot("TownHall_Model", 3.2f + 0.45f * level, 2.7f + 0.5f * level);
            var ground = Group(root, "Ground");
            var body = Group(root, "Body");
            var roof = Group(root, "Roof");
            var trees = Group(root, "Trees");
            var props = Group(root, "Props");
            bool gilded = level >= 3;
            var pillar = RedMat; // lacquered pillars from the first look on, like the reference
            var frame = FrameMat;
            var gold = GoldMat;
            var slateLight = SlateLightMat;
            const float slabH = 0.08f, deckY = 0.3f;
            var faces = new[] { 0f, 90f };

            // ---- terrace: stone slab, raised platform with coping and a wooden veranda floor, corner stones, a stair on each visible face
            prim.BoxOnGround(ground, StoneMat, Vector3.zero, new Vector3(2.96f, slabH, 2.96f));
            prim.BoxOnGround(ground, StoneDarkMat, new Vector3(0f, slabH, 0f), new Vector3(2.3f, deckY - slabH, 2.3f));
            prim.BoxOnGround(ground, StoneMat, new Vector3(0f, deckY - 0.03f, 0f), new Vector3(2.36f, 0.05f, 2.36f));
            prim.BoxOnGround(ground, WoodMat, new Vector3(0f, deckY + 0.015f, 0f), new Vector3(2.2f, 0.02f, 2.2f));
            foreach (var sx in new[] { -1f, 1f })
                foreach (var sz in new[] { -1f, 1f })
                    prim.BoxOnGround(ground, StoneDarkMat, new Vector3(sx * 1.42f, slabH, sz * 1.42f), new Vector3(0.14f, 0.13f, 0.14f));
            foreach (var yaw in faces)
            {
                var g = Pivot(ground, yaw);
                HallStairs(g, slabH, deckY);
            }

            // ---- storeys: plaster box in a timber frame, ring of red pillars around the first, sagging tile roof on each
            float y = deckY + 0.01f;
            float half = 0.84f;
            float ridgeY = y;
            ProceduralMeshes.PagodaRoofSpec lastSpec = default;
            for (int i = 0; i < tiers; i++)
            {
                bool top = i == tiers - 1;
                float wallH = i == 0 ? 0.86f : 0.66f - 0.06f * (i - 1);
                float nextHalf = half * 0.72f;
                float eave = half + (i == 0 ? 0.52f : 0.29f * Mathf.Pow(0.9f, i - 1));
                float roofH = i == 0 ? 0.5f : (top ? 0.5f : 0.38f);
                float topW = top ? Mathf.Max(0.16f, eave * 0.36f) : nextHalf + 0.04f;
                float topD = top ? 0.03f : nextHalf + 0.04f;
                float roofY = y + wallH - 0.02f;
                var spec = new ProceduralMeshes.PagodaRoofSpec(eave, eave, topW, topD, roofH, 0.13f * Mathf.Pow(0.85f, i));

                // walls, framing, beams
                float size = half * 2f;
                prim.BoxOnGround(body, PlasterMat, new Vector3(0f, y, 0f), new Vector3(size - 0.02f, wallH, size - 0.02f));
                prim.BoxOnGround(body, frame, new Vector3(0f, y, 0f), new Vector3(size + 0.08f, 0.07f, size + 0.08f));
                var beam = i == 0 || gilded ? pillar : frame;
                prim.BoxOnGround(body, beam, new Vector3(0f, y + wallH - 0.1f, 0f), new Vector3(size + 0.1f, 0.09f, size + 0.1f));
                foreach (var sx in new[] { -1f, 1f })
                    foreach (var sz in new[] { -1f, 1f })
                    {
                        prim.BoxOnGround(body, frame, new Vector3(sx * half, y, sz * half), new Vector3(0.12f, wallH + 0.01f, 0.12f));
                        prim.BoxOnGround(body, StoneDarkMat, new Vector3(sx * half, y, sz * half), new Vector3(0.17f, 0.05f, 0.17f));
                    }

                foreach (var yaw in faces)
                {
                    var b = Pivot(body, yaw);
                    if (i == 0) HallGroundFloorFace(b, y, half, wallH, pillar, deckY);
                    else HallUpperFace(b, y, half, wallH, i, level);
                }
                if (i == 0)
                {
                    // the two far corner pillars close the ring under the eaves
                    foreach (var p in new[] { new Vector2(1.04f, -1.04f), new Vector2(1.04f, 1.04f) })
                    {
                        float pillarTop = y + wallH + 0.05f;
                        prim.Cylinder(body, pillar, new Vector3(p.x, (deckY + pillarTop) * 0.5f + 0.03f, p.y), 0.052f, pillarTop - deckY - 0.03f);
                        prim.Box(body, frame, new Vector3(p.x, pillarTop - 0.045f, p.y), new Vector3(0.14f, 0.08f, 0.14f));
                    }
                }

                // roof: shell, hip ridges in light tile, gold edge line and corner tips
                prim.MeshObject(roof, ProceduralMeshes.PagodaRoof(spec), SlateUvMat, new Vector3(0f, roofY, 0f));
                for (int c = 0; c < 4; c++)
                {
                    const int steps = 8;
                    var path = new Vector3[steps];
                    var radii = new float[steps];
                    for (int k = 0; k < steps; k++)
                    {
                        float t = 0.02f + 0.96f * k / (steps - 1);
                        path[k] = spec.Hip(t, c) + new Vector3(0f, 0.022f, 0f);
                        radii[k] = Mathf.Lerp(0.032f, 0.022f, t);
                    }
                    prim.MeshObject(roof, ProceduralMeshes.Tube(path, radii, 5), slateLight, new Vector3(0f, roofY, 0f));
                    var tip = spec.Hip(PagodaTip, c);
                    var outward = new Vector3(Mathf.Sign(tip.x), 0f, Mathf.Sign(tip.z)).normalized;
                    var cone = prim.Cone(roof, gold, new Vector3(0f, roofY, 0f) + tip + new Vector3(0f, 0.01f, 0f), 0.035f, 0.13f);
                    cone.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (outward * 0.8f + Vector3.up * 0.6f).normalized);
                }
                {
                    int n = 4 * ProceduralMeshes.PagodaRoofSpec.Segments;
                    var edge = new Vector3[n + 1];
                    var radii = new float[n + 1];
                    for (int p = 0; p <= n; p++)
                    {
                        edge[p] = spec.Point(ProceduralMeshes.PagodaRoofSpec.LipT, p % n) + new Vector3(0f, 0.03f, 0f);
                        radii[p] = 0.019f;
                    }
                    prim.MeshObject(roof, ProceduralMeshes.Tube(edge, radii, 4), GoldTrimMat, new Vector3(0f, roofY, 0f));
                }

                // karahafu gable in front of the roof on both visible faces
                float k0 = eave / 1.36f;
                float td = top ? 0.42f : 0.36f;
                float hz = Mathf.Lerp(spec.EaveD, spec.TopD, td);
                float run = hz - (top ? 0.06f : nextHalf - 0.02f);
                float rise = spec.SlopeD(td) * run;
                foreach (var yaw in faces)
                {
                    var r = Pivot(roof, yaw);
                    HallGable(r, new Vector3(0f, roofY + spec.Profile(td) - 0.02f, -hz), Mathf.Max(0.5f, 0.92f * k0), Mathf.Max(0.22f, 0.36f * k0), run, rise);
                }

                lastSpec = spec;
                ridgeY = roofY + roofH;
                y = roofY + roofH - 0.03f;
                half = nextHalf;
            }

            // ridge of the top roof: light tile cap, golden shachihoko on both ends, finial in the middle
            {
                float tw = lastSpec.TopW;
                var ridge = new[] { new Vector3(-tw, 0.02f, 0f), new Vector3(-tw * 0.5f, 0.03f, 0f), new Vector3(0f, 0.035f, 0f), new Vector3(tw * 0.5f, 0.03f, 0f), new Vector3(tw, 0.02f, 0f) };
                var radii = new[] { 0.04f, 0.04f, 0.04f, 0.04f, 0.04f };
                prim.MeshObject(roof, ProceduralMeshes.Tube(ridge, radii, 5), slateLight, new Vector3(0f, ridgeY, 0f));
                float s = tiers >= 4 ? 1.1f : 1f;
                Shachihoko(roof, new Vector3(tw, ridgeY + 0.02f, 0f), 1f, s);
                Shachihoko(roof, new Vector3(-tw, ridgeY + 0.02f, 0f), -1f, s);
                Finial(roof, new Vector3(0f, ridgeY + 0.03f, 0f), 1f);
            }
            float topY = ridgeY + 0.5f;
            var model = root.GetComponent<BuildingModel>();
            model.IndicatorHeight = topY + 0.7f;
            model.ColliderHeight = topY;

            // ---- props: nobori banners flanking the stair, stone lanterns on both faces
            foreach (var sx in new[] { -1f, 1f })
            {
                float x = sx * 1.02f;
                const float bz = -1.44f;
                prim.Cylinder(props, frame, new Vector3(x, slabH + 0.74f, bz), 0.028f, 1.48f);
                prim.Sphere(props, gold, new Vector3(x, slabH + 1.5f, bz), 0.05f);
                prim.Box(props, frame, new Vector3(x - sx * 0.14f, slabH + 1.4f, bz), new Vector3(0.3f, 0.028f, 0.028f), default, false);
                prim.Box(props, RedMat, new Vector3(x - sx * 0.14f, slabH + 1.0f, bz), new Vector3(0.24f, 0.78f, 0.022f), default, false);
                prim.Box(props, gold, new Vector3(x - sx * 0.14f, slabH + 1.36f, bz - 0.005f), new Vector3(0.26f, 0.04f, 0.026f), default, false);
                prim.Box(props, gold, new Vector3(x - sx * 0.14f, slabH + 0.63f, bz - 0.005f), new Vector3(0.26f, 0.03f, 0.026f), default, false);
                prim.Cylinder(props, gold, new Vector3(x - sx * 0.14f, slabH + 1.03f, bz - 0.015f), 0.06f, 0.02f, new Vector3(90f, 0f, 0f), false);
                prim.Cylinder(props, RedMat, new Vector3(x - sx * 0.14f, slabH + 1.03f, bz - 0.02f), 0.032f, 0.02f, new Vector3(90f, 0f, 0f), false);
            }
            foreach (var yaw in faces)
            {
                var lanterns = Pivot(props, yaw);
                foreach (var sx in new[] { -0.8f, 0.8f }) StoneLantern(lanterns, new Vector3(sx, slabH, -1.3f), 0.8f);
            }

            // ---- shrubs and blossom bushes on the terrace corners: low enough to sit under the eaves, clear of the pillars (no tree can stand in
            // a 3x3 footprint without growing through the roofs, so the sakura trees of the world stand outside, in their own cells)
            Bush(trees, new Vector3(-1.34f, slabH, -1.34f), 0.8f, rng);
            BlossomBush(trees, new Vector3(1.34f, slabH, -1.34f), 0.75f, rng);
            BlossomBush(trees, new Vector3(-1.34f, slabH, 1.26f), 0.8f, rng);
            BlossomBush(trees, new Vector3(1.36f, slabH, 1.3f), 0.8f, rng);
            Bush(trees, new Vector3(0f, slabH, 1.4f), 0.75f, rng);
            return root;
        }

        // ---------------------------------------------------------------- House (2x2): thatched half-timber cottage, levels 1..3

        private GameObject House(int level, int w, int d)
        {
            var rng = Rng("house");
            var root = NewRoot("House_Model", 1.95f + (level >= 3 ? 0.3f : 0f), 1.45f + (level >= 3 ? 0.3f : 0f));
            var ground = Group(root, "Ground");
            var body = Group(root, "Body");
            var roof = Group(root, "Roof");
            var trees = Group(root, "Trees");
            var props = Group(root, "Props");

            const float hw = 0.7f, hd = 0.64f, wallBase = 0.13f, wallH = 0.6f;
            float roofY = wallBase + wallH + 0.02f;
            const float roofH = 0.78f, curve = 0.35f;
            float roofW = hw * 2f + 0.28f, roofD = hd * 2f + 0.3f;

            // plinth of dark stone with a skirt of individual stones, path stones in front
            prim.BoxOnGround(ground, StoneDarkMat, Vector3.zero, new Vector3(1.62f, 0.13f, 1.5f));
            var stoneMat = StoneMat;
            StoneLine(ground, new Vector3(-0.78f, 0.05f, -0.78f), new Vector3(0.78f, 0.05f, -0.78f), 9, 0.15f, rng, stoneMat);
            StoneLine(ground, new Vector3(-0.83f, 0.05f, -0.66f), new Vector3(-0.83f, 0.05f, 0.66f), 8, 0.15f, rng, stoneMat);
            StoneLine(ground, new Vector3(0.83f, 0.05f, -0.66f), new Vector3(0.83f, 0.05f, 0.66f), 8, 0.15f, rng, stoneMat);
            var cobble = art.Lit(Palette.Plaster, PaintedTexture.Stone);
            prim.BoxOnGround(ground, cobble, new Vector3(-0.1f, 0f, -0.98f), new Vector3(0.36f, 0.06f, 0.14f));

            // plaster walls in a timber frame
            prim.BoxOnGround(body, PlasterMat, new Vector3(0f, wallBase, 0f), new Vector3(hw * 2f - 0.04f, wallH, hd * 2f - 0.04f));
            prim.BoxOnGround(body, FrameMat, new Vector3(0f, wallBase, 0f), new Vector3(hw * 2f + 0.06f, 0.06f, hd * 2f + 0.06f));
            prim.BoxOnGround(body, FrameMat, new Vector3(0f, wallBase + wallH - 0.05f, 0f), new Vector3(hw * 2f + 0.08f, 0.08f, hd * 2f + 0.08f));
            prim.BoxOnGround(body, FrameMat, new Vector3(0f, wallBase + wallH * 0.52f, 0f), new Vector3(hw * 2f + 0.04f, 0.035f, hd * 2f + 0.04f));
            foreach (var sx in new[] { -1f, 1f })
                foreach (var sz in new[] { -1f, 1f })
                    prim.BoxOnGround(body, FrameMat, new Vector3(sx * hw, wallBase, sz * hd), new Vector3(0.1f, wallH + 0.01f, 0.1f));
            foreach (var x in new[] { -0.4f, 0.3f }) prim.BoxOnGround(body, FrameMat, new Vector3(x, wallBase, -hd), new Vector3(0.05f, wallH - 0.05f, 0.05f));
            // X bracing on the left wall, between the two windows, like the reference cottage
            prim.Box(body, FrameMat, new Vector3(-hw, wallBase + wallH * 0.5f, 0f), new Vector3(0.045f, 0.6f, 0.04f), new Vector3(38f, 0f, 0f), false);
            prim.Box(body, FrameMat, new Vector3(-hw, wallBase + wallH * 0.5f, 0f), new Vector3(0.045f, 0.6f, 0.04f), new Vector3(-38f, 0f, 0f), false);
            prim.BoxOnGround(body, FrameMat, new Vector3(-hw, wallBase, 0f), new Vector3(0.06f, wallH - 0.05f, 0.05f));

            // veranda (engawa) in front of a recessed shoji door
            float deckTop = wallBase + 0.05f;
            prim.BoxOnGround(body, WoodMat, new Vector3(-0.1f, wallBase - 0.01f, -hd - 0.13f), new Vector3(1.04f, 0.06f, 0.26f));
            foreach (var x in new[] { -0.58f, 0.38f })
            {
                prim.BoxOnGround(body, FrameMat, new Vector3(x, wallBase, -hd - 0.22f), new Vector3(0.055f, 0.56f, 0.055f));
                prim.Box(body, BeamMat, new Vector3(x, wallBase + 0.56f, -hd - 0.21f), new Vector3(0.09f, 0.04f, 0.1f), default, false);
            }
            prim.Box(body, ShojiMat, new Vector3(-0.12f, deckTop + 0.22f, -hd - 0.005f), new Vector3(0.34f, 0.44f, 0.035f));
            foreach (var x in new[] { -0.3f, 0.06f }) prim.BoxOnGround(body, FrameMat, new Vector3(x, deckTop, -hd - 0.015f), new Vector3(0.05f, 0.5f, 0.05f));
            prim.Box(body, FrameMat, new Vector3(-0.12f, deckTop + 0.48f, -hd - 0.015f), new Vector3(0.44f, 0.05f, 0.05f));
            prim.Box(body, FrameMat, new Vector3(-0.12f, deckTop + 0.22f, -hd - 0.012f), new Vector3(0.34f, 0.03f, 0.03f), default, false);

            // windows with swung-open shutters, chimney
            Window(body, new Vector3(0.36f, wallBase + 0.36f, -hd - 0.005f), 0.26f, 0.22f, false);
            foreach (var z in new[] { -0.44f, 0.44f })
            {
                Window(body, new Vector3(-hw - 0.005f, wallBase + 0.36f, z), 0.22f, 0.2f, true);
                foreach (var s in new[] { -1f, 1f })
                {
                    var shutter = prim.Box(body, WoodMat, new Vector3(-hw - 0.05f, wallBase + 0.36f, z + s * 0.17f), new Vector3(0.022f, 0.21f, 0.11f), default, false);
                    shutter.transform.localRotation = Quaternion.Euler(0f, s * 55f, 0f);
                }
            }
            Chimney(root, body, new Vector3(0.42f, wallBase + 0.45f, 0.28f), 0.88f + (level >= 3 ? 0.12f : 0f));

            // thatched gable roof: thick edge, rounded ridge rolls with tied bands, moss, timbered gable ends
            float ridgeY = roofY + roofH;
            prim.MeshObject(roof, ProceduralMeshes.GableRoof(roofW, roofD, roofH, curve, 0.06f), ThatchMat, new Vector3(0f, roofY, 0f));
            EaveBand(roof, roofY, roofW, roofD, ThatchDarkMat);
            foreach (var sx in new[] { -1f, 1f })
            {
                prim.Add(roof, ProceduralMeshes.GableWall(roofD - 0.3f, roofH * 0.8f, 0.05f), PlasterMat, new Vector3(sx * (hw - 0.02f), roofY, 0f), Vector3.one, default, "GableWall");
                float bx = sx * (hw + 0.025f);
                prim.BoxOnGround(roof, FrameMat, new Vector3(bx, roofY, 0f), new Vector3(0.04f, roofH * 0.72f, 0.04f));
                prim.Box(roof, FrameMat, new Vector3(bx, roofY + 0.16f, -0.17f), new Vector3(0.04f, 0.32f, 0.035f), new Vector3(-36f, 0f, 0f), false);
                prim.Box(roof, FrameMat, new Vector3(bx, roofY + 0.16f, 0.17f), new Vector3(0.04f, 0.32f, 0.035f), new Vector3(36f, 0f, 0f), false);
            }
            prim.Cylinder(roof, ThatchDarkMat, new Vector3(0f, ridgeY + 0.025f, 0f), 0.085f, roofW + 0.06f, new Vector3(0f, 0f, 90f));
            prim.Cylinder(roof, ThatchMat, new Vector3(0f, ridgeY + 0.085f, 0f), 0.05f, roofW - 0.05f, new Vector3(0f, 0f, 90f));
            RidgeBands(roof, ridgeY + 0.025f, roofW, 0.095f, FrameMat);
            foreach (var sx in new[] { -1f, 1f })
                prim.Sphere(roof, ThatchDarkMat, new Vector3(sx * (roofW * 0.5f + 0.03f), ridgeY + 0.025f, 0f), new Vector3(0.14f, 0.17f, 0.17f), false);
            MossPatches(roof, roofY, roofD * 0.5f, roofH, curve, -1f, new Vector2(-0.35f, 0.5f), new Vector2(0.2f, 0.3f), new Vector2(0.55f, 0.6f));
            MossPatches(roof, roofY, roofD * 0.5f, roofH, curve, 1f, new Vector2(-0.1f, 0.45f));

            if (level >= 2)
            {
                // flower boxes, a hanging paper lantern under the veranda eave, a second tier of thatch along the ridge
                foreach (var z in new[] { -0.44f, 0.44f })
                {
                    prim.Box(body, WoodMat, new Vector3(-hw - 0.09f, wallBase + 0.21f, z), new Vector3(0.09f, 0.07f, 0.26f), default, false);
                    BlossomBush(body, new Vector3(-hw - 0.09f, wallBase + 0.24f, z), 0.3f, rng);
                }
                prim.Box(body, WoodMat, new Vector3(0.36f, wallBase + 0.2f, -hd - 0.04f), new Vector3(0.3f, 0.07f, 0.08f));
                Bush(body, new Vector3(0.36f, wallBase + 0.24f, -hd - 0.04f), 0.3f, rng);
                PaperLantern(body, new Vector3(-0.35f, wallBase + 0.44f, -hd - 0.22f), 0.8f);
            }
            if (level >= 3)
            {
                // blue noren over the door, a dormer with its own glowing window
                var blue = art.Lit(Hex("#3e72b8"));
                for (int i = 0; i < 3; i++)
                    prim.Box(body, blue, new Vector3(-0.12f + (i - 1) * 0.095f, deckTop + 0.3f, -hd - 0.1f), new Vector3(0.085f, 0.3f, 0.02f), default, false);
                prim.Box(body, WoodMat, new Vector3(-0.12f, deckTop + 0.46f, -hd - 0.1f), new Vector3(0.34f, 0.03f, 0.03f), default, false);
                float f = 0.5f;
                float dz = -(roofD * 0.5f) * (1f - f);
                float dy = roofY + RoofSurface(roofH, curve, f);
                prim.BoxOnGround(roof, PlasterMat, new Vector3(0.3f, dy - 0.05f, dz), new Vector3(0.4f, 0.26f, 0.22f));
                Window(roof, new Vector3(0.3f, dy + 0.08f, dz - 0.115f), 0.2f, 0.15f, false);
                prim.Add(roof, ProceduralMeshes.GableRoof(0.3f, 0.5f, 0.2f, 0.3f, 0.04f), ThatchMat, new Vector3(0.3f, dy + 0.21f, dz), Vector3.one, new Vector3(0f, 90f, 0f), "DormerRoof");
                prim.Cylinder(roof, ThatchDarkMat, new Vector3(0.3f, dy + 0.4f, dz), 0.045f, 0.44f, new Vector3(90f, 0f, 0f), false);
            }

            // front fence with a small gate, rain barrel with lid and bamboo gutter, drying daikon, firewood, cabbages at the back
            var fence = FrameMat;
            foreach (var x in new[] { -0.9f, -0.72f, -0.54f, -0.36f })
                prim.BoxOnGround(props, fence, new Vector3(x, 0f, -0.9f), new Vector3(0.06f, 0.27f, 0.06f));
            prim.Box(props, WoodMat, new Vector3(-0.63f, 0.2f, -0.9f), new Vector3(0.6f, 0.035f, 0.03f));
            prim.Box(props, WoodMat, new Vector3(-0.63f, 0.1f, -0.9f), new Vector3(0.6f, 0.035f, 0.03f));
            prim.Cylinder(props, art.Lit(Palette.Bark, PaintedTexture.Planks), new Vector3(0.92f, 0.16f, -0.42f), 0.13f, 0.32f);
            prim.Cylinder(props, FrameMat, new Vector3(0.92f, 0.09f, -0.42f), 0.137f, 0.03f);
            prim.Cylinder(props, FrameMat, new Vector3(0.92f, 0.22f, -0.42f), 0.137f, 0.03f);
            prim.Cylinder(props, WoodMat, new Vector3(0.92f, 0.335f, -0.42f), 0.12f, 0.03f);
            prim.Cylinder(props, art.Lit(Palette.Bamboo), new Vector3(0.83f, 0.52f, -0.42f), 0.022f, 0.4f, new Vector3(0f, 0f, 62f));
            foreach (var z in new[] { -0.2f, 0.12f }) prim.BoxOnGround(props, fence, new Vector3(-0.93f, 0f, z), new Vector3(0.045f, 0.52f, 0.045f));
            prim.Box(props, WoodMat, new Vector3(-0.93f, 0.5f, -0.04f), new Vector3(0.035f, 0.035f, 0.42f), default, false);
            for (int i = 0; i < 5; i++)
                prim.Cylinder(props, i % 2 == 0 ? art.Lit(Hex("#f3ead6")) : art.Lit(Hex("#e7a04a")), new Vector3(-0.93f, 0.4f, -0.17f + i * 0.075f), 0.02f, 0.17f, default, false);
            var logBark = BarkMat;
            for (int i = 0; i < 3; i++)
            {
                prim.Cylinder(props, logBark, new Vector3(0.66f + i * 0.095f, 0.06f, 0.8f), 0.045f, 0.4f, new Vector3(90f, 0f, 0f));
                prim.Cylinder(props, LogEndMat, new Vector3(0.66f + i * 0.095f, 0.06f, 0.598f), 0.04f, 0.012f, new Vector3(90f, 0f, 0f), false);
            }
            prim.Cylinder(props, logBark, new Vector3(0.705f, 0.14f, 0.8f), 0.045f, 0.4f, new Vector3(90f, 0f, 0f));
            prim.Cylinder(props, LogEndMat, new Vector3(0.705f, 0.14f, 0.598f), 0.04f, 0.012f, new Vector3(90f, 0f, 0f), false);
            prim.Cylinder(props, ThatchDarkMat, new Vector3(-0.86f, 0.07f, -0.68f), 0.12f, 0.14f);
            prim.BoxOnGround(props, art.Lit(Palette.DirtDark, PaintedTexture.Earth), new Vector3(-0.15f, 0f, 0.86f), new Vector3(0.95f, 0.05f, 0.22f));
            var cabbage = art.Foliage(Color.white, false);
            for (int i = 0; i < 5; i++)
                prim.Add(props, ToonMeshes.Blob(1, i, 0.25f, FoliagePalette.Leaf), cabbage, new Vector3(-0.52f + i * 0.2f, 0.11f, 0.86f), new Vector3(0.13f, 0.1f, 0.13f), default, "Cabbage", false);

            if (Props != null)
            {
                Bush(trees, new Vector3(-0.92f, 0f, 0.86f), 0.7f, rng);
                Bush(trees, new Vector3(0.92f, 0f, 0.2f), 0.7f, rng);
                var fl = Props.Flowers(trees, new Vector3(0.64f, 0f, -0.86f), rng);
                fl.transform.localScale = Vector3.one * 0.45f;
            }
            return root;
        }

        // ---------------------------------------------------------------- Woodcutter (2x2): open log workshop, levels 1..3

        private GameObject Woodcutter(int level, int w, int d)
        {
            prim.ThinBoxes = true; // rafter tips, slats and courses: flat boxes for everything hair-thin
            try { return BuildWoodcutter(level); }
            finally { prim.ThinBoxes = false; }
        }

        private GameObject BuildWoodcutter(int level)
        {
            var rng = Rng("woodcutter");
            var root = NewRoot("Woodcutter_Model", 1.75f + 0.15f * level, 1.3f + 0.15f * level);
            var ground = Group(root, "Ground");
            var body = Group(root, "Body");
            var roof = Group(root, "Roof");
            var trees = Group(root, "Trees");
            var props = Group(root, "Props");

            var plank = BeamMat;
            var plankDark = FrameMat;
            var roofMat = level >= 3 ? SlateMat : art.Lit(Hex("#b98a45"), PaintedTexture.Thatch);
            var roofDark = level >= 3 ? SlateLightMat : ThatchDarkMat;
            var bark = BarkMat;
            var cut = LogEndMat;
            var steel = SteelMat;

            // yard: a trodden earth patch with a plank floor under the hut, wood chips
            prim.BoxOnGround(ground, art.Lit(Palette.DirtDark, PaintedTexture.Earth), new Vector3(0f, 0f, 0f), new Vector3(1.9f, 0.05f, 1.9f));
            prim.BoxOnGround(ground, WoodMat, new Vector3(-0.3f, 0.05f, 0.25f), new Vector3(1.1f, 0.06f, 0.95f));
            for (int i = 0; i < 14; i++)
            {
                var chip = prim.Box(ground, art.Lit(Palette.LogEnd), new Vector3(-0.2f + (float)rng.NextDouble() * 1.1f, 0.055f, -0.95f + (float)rng.NextDouble() * 0.7f),
                    new Vector3(0.06f, 0.012f, 0.03f), new Vector3(0f, (float)rng.NextDouble() * 180f, 0f), false);
            }

            // the hut: back and left walls with planks battens, corner posts, open front
            float roofY = 0.92f;
            prim.BoxOnGround(body, plank, new Vector3(-0.3f, 0.11f, 0.68f), new Vector3(1.1f, 0.78f, 0.08f));
            prim.BoxOnGround(body, plank, new Vector3(-0.83f, 0.11f, 0.25f), new Vector3(0.08f, 0.78f, 0.92f));
            for (int i = 0; i < 5; i++) // battens over the planks
            {
                prim.BoxOnGround(body, plankDark, new Vector3(-0.83f - 0.05f, 0.11f, -0.15f + i * 0.2f), new Vector3(0.025f, 0.76f, 0.04f));
            }
            foreach (var p in new[] { new Vector2(-0.83f, -0.2f), new Vector2(0.23f, -0.2f), new Vector2(0.23f, 0.68f), new Vector2(-0.83f, 0.68f) })
                prim.BoxOnGround(body, plankDark, new Vector3(p.x, 0.11f, p.y), new Vector3(0.11f, 0.85f, 0.11f));
            prim.BoxOnGround(body, plankDark, new Vector3(-0.3f, 0.11f, -0.2f), new Vector3(1.06f, 0.06f, 0.06f)); // front sill beam
            prim.Box(body, plankDark, new Vector3(-0.3f, 0.92f - 0.03f, -0.2f), new Vector3(1.14f, 0.07f, 0.07f), default, false);
            Window(body, new Vector3(-0.875f, 0.55f, 0.25f), 0.26f, 0.2f, true);
            // a workbench with a saw blade and vice inside
            prim.BoxOnGround(body, WoodMat, new Vector3(-0.3f, 0.11f, 0.5f), new Vector3(0.7f, 0.22f, 0.22f));
            prim.Box(body, steel, new Vector3(-0.3f, 0.45f, 0.6f), new Vector3(0.36f, 0.12f, 0.02f), new Vector3(0f, 0f, 6f), false);
            prim.Box(body, plankDark, new Vector3(-0.6f, 0.27f, 0.5f), new Vector3(0.08f, 0.06f, 0.1f), default, false);
            // tools hung on the outside of the left wall: big two-man saw and two axes
            prim.Box(body, steel, new Vector3(-0.92f, 0.56f, -0.05f), new Vector3(0.012f, 0.08f, 0.44f), default, false);
            prim.Box(body, plankDark, new Vector3(-0.92f, 0.56f, -0.29f), new Vector3(0.02f, 0.1f, 0.03f), default, false);
            prim.Box(body, plankDark, new Vector3(-0.92f, 0.56f, 0.19f), new Vector3(0.02f, 0.1f, 0.03f), default, false);
            foreach (var z in new[] { 0.52f, 0.66f })
            {
                prim.Box(body, WoodMat, new Vector3(-0.92f, 0.42f, z), new Vector3(0.02f, 0.3f, 0.025f), new Vector3(0f, 0f, 0f), false);
                prim.Box(body, steel, new Vector3(-0.92f, 0.55f, z + 0.03f), new Vector3(0.022f, 0.07f, 0.09f), default, false);
            }

            // gable roof (thatch; slate at level 3) with gable ends, a thick edge and a chimney
            float ridgeY = roofY + 0.54f;
            prim.MeshObject(roof, ProceduralMeshes.GableRoof(1.35f, 1.45f, 0.52f, 0.3f, 0.06f), roofMat, new Vector3(-0.3f, roofY, 0.25f));
            foreach (var z in new[] { -1f, 1f })
                prim.Box(roof, roofDark, new Vector3(-0.3f, roofY + 0.045f, 0.25f + z * (0.725f + 0.055f)), new Vector3(1.47f, 0.06f, 0.055f), default, false);
            foreach (var sx in new[] { -0.3f - 0.61f, -0.3f + 0.61f })
            {
                prim.Add(roof, ProceduralMeshes.GableWall(1.15f, 0.42f, 0.05f), PlasterMat, new Vector3(sx, roofY, 0.25f), Vector3.one, default, "GableWall");
                prim.BoxOnGround(roof, plankDark, new Vector3(sx + Mathf.Sign(sx + 0.3f) * 0.03f, roofY, 0.25f), new Vector3(0.04f, 0.4f, 0.04f));
            }
            prim.Cylinder(roof, roofDark, new Vector3(-0.3f, ridgeY, 0.25f), 0.07f, 1.4f, new Vector3(0f, 0f, 90f));
            var bands = new GameObject("RidgeBands").transform; // RidgeBands centres on the origin; this roof is centred on (-0.3, 0.25)
            bands.SetParent(roof, false);
            bands.localPosition = new Vector3(-0.3f, 0f, 0.25f);
            RidgeBands(bands, ridgeY, 1.4f, 0.078f, art.Lit(Hex("#7a5a2a"), PaintedTexture.Thatch));
            Chimney(root, roof, new Vector3(-0.6f, roofY + 0.05f, 0.5f), 0.55f);
            PaperLantern(body, new Vector3(0.23f, 0.72f, -0.3f), 0.75f);

            if (level >= 2)
            {
                // a lean-to for the firewood on the right side
                prim.Box(roof, WoodMat, new Vector3(0.6f, 0.62f, -0.05f), new Vector3(0.85f, 0.05f, 0.85f), new Vector3(0f, 0f, -14f));
                foreach (var p in new[] { new Vector2(0.98f, -0.4f), new Vector2(0.98f, 0.3f) })
                    prim.BoxOnGround(body, plankDark, new Vector3(p.x, 0f, p.y), new Vector3(0.08f, 0.66f, 0.08f));
            }
            if (level >= 3)
            {
                // porch roof along the front, hanging sign
                prim.Box(roof, WoodMat, new Vector3(-0.3f, 0.74f, -0.3f), new Vector3(1.3f, 0.05f, 0.55f), new Vector3(10f, 0f, 0f));
                prim.Box(body, art.Lit(Palette.BannerRed), new Vector3(-0.3f, 0.52f, -0.36f), new Vector3(0.3f, 0.16f, 0.02f), default, false);
            }

            // stacked logs (cut ends with growth rings face the camera side), bigger piles with level
            int[] rowCounts = level >= 2 ? new[] { 4, 3, 2, 1 } : new[] { 3, 2, 1 };
            for (int row = 0; row < rowCounts.Length; row++)
            {
                for (int i = 0; i < rowCounts[row]; i++)
                {
                    float x = 0.68f + (i - (rowCounts[row] - 1) * 0.5f) * 0.19f;
                    float y = 0.11f + row * 0.16f;
                    prim.Cylinder(props, bark, new Vector3(x, y, 0.32f), 0.085f, 0.85f, new Vector3(90f, 0f, 0f));
                    prim.Cylinder(props, cut, new Vector3(x, y, -0.108f), 0.075f, 0.012f, new Vector3(90f, 0f, 0f), false);
                }
            }
            prim.Box(props, plankDark, new Vector3(0.45f, 0.06f, 0.32f), new Vector3(0.04f, 0.08f, 0.6f), default, false); // stakes keeping the pile together
            prim.Box(props, plankDark, new Vector3(0.93f, 0.06f, 0.32f), new Vector3(0.04f, 0.08f, 0.6f), default, false);

            // chopping block with axe and split logs, sawhorse with a log, wheelbarrow, plank stack, stump
            prim.Cylinder(props, art.Lit(Palette.TimberLight, PaintedTexture.Bark), new Vector3(-0.35f, 0.2f, -0.6f), 0.17f, 0.24f);
            prim.Cylinder(props, cut, new Vector3(-0.35f, 0.322f, -0.6f), 0.15f, 0.012f);
            prim.Box(props, WoodMat, new Vector3(-0.35f, 0.5f, -0.6f), new Vector3(0.045f, 0.4f, 0.045f), new Vector3(0f, 0f, -20f));
            prim.Box(props, steel, new Vector3(-0.43f, 0.66f, -0.6f), new Vector3(0.14f, 0.09f, 0.03f), new Vector3(0f, 0f, -20f));
            foreach (var z in new[] { -0.15f, 0.15f })
            {
                prim.Box(props, WoodMat, new Vector3(0.05f, 0.12f, -0.72f + z), new Vector3(0.05f, 0.26f, 0.05f), new Vector3(0f, 0f, 14f));
                prim.Box(props, WoodMat, new Vector3(0.15f, 0.12f, -0.72f + z), new Vector3(0.05f, 0.26f, 0.05f), new Vector3(0f, 0f, -14f));
            }
            prim.Cylinder(props, bark, new Vector3(0.1f, 0.27f, -0.72f), 0.06f, 0.52f, new Vector3(0f, 0f, 90f));
            prim.Cylinder(props, cut, new Vector3(-0.165f, 0.27f, -0.72f), 0.055f, 0.012f, new Vector3(0f, 0f, 90f), false);
            for (int i = 0; i < 4; i++) // plank stack, spaced with thin sticks so the boards dry
            {
                prim.Box(props, art.Lit(Palette.LogEnd, PaintedTexture.Planks), new Vector3(-0.72f, 0.03f + i * 0.055f, -0.78f), new Vector3(0.5f, 0.03f, 0.16f), default, false);
                prim.Box(props, plankDark, new Vector3(-0.85f, 0.058f + i * 0.055f, -0.78f), new Vector3(0.03f, 0.022f, 0.16f), default, false);
                prim.Box(props, plankDark, new Vector3(-0.58f, 0.058f + i * 0.055f, -0.78f), new Vector3(0.03f, 0.022f, 0.16f), default, false);
            }
            var wheelbarrow = new GameObject("Wheelbarrow").transform;
            wheelbarrow.SetParent(props, false);
            wheelbarrow.localPosition = new Vector3(0.6f, 0f, -0.52f);
            wheelbarrow.localRotation = Quaternion.Euler(0f, 25f, 0f);
            prim.Box(wheelbarrow, WoodMat, new Vector3(0f, 0.2f, 0f), new Vector3(0.3f, 0.1f, 0.22f), default, false);
            prim.Cylinder(wheelbarrow, art.Lit(Palette.Timber), new Vector3(-0.2f, 0.11f, 0f), 0.1f, 0.04f, new Vector3(0f, 0f, 90f), false);
            prim.Box(wheelbarrow, plankDark, new Vector3(0.25f, 0.22f, -0.07f), new Vector3(0.3f, 0.025f, 0.025f), new Vector3(0f, 0f, 10f), false);
            prim.Box(wheelbarrow, plankDark, new Vector3(0.25f, 0.22f, 0.07f), new Vector3(0.3f, 0.025f, 0.025f), new Vector3(0f, 0f, 10f), false);
            for (int i = 0; i < 3; i++)
                prim.Cylinder(wheelbarrow, bark, new Vector3(-0.04f + i * 0.06f, 0.29f, 0f), 0.035f, 0.2f, new Vector3(90f, 0f, 0f), false);

            WoodcutterDetails(level, Rng("woodcutter_details"), ground, body, roof, props, roofY, ridgeY); // own sequence: the shrubs below keep their shape

            if (Props != null)
            {
                // shrubs stay in the back corners, away from the log pile and the hut walls
                Bush(trees, new Vector3(-0.96f, 0f, 0.96f), 0.7f, rng);
                Bush(trees, new Vector3(0.92f, 0f, 0.97f), 0.7f, rng);
            }
            return root;
        }

        /// <summary>
        /// Second pass over the woodcutter: stone footing, shutters, gable framing and crossed ridge boards, thatch courses, exposed rafter
        /// tips, a rain barrel, sawdust and split kindling, plus extra fittings for levels 2 and 3. Everything stays inside the footprint.
        /// </summary>
        private void WoodcutterDetails(int level, System.Random rng, Transform ground, Transform body, Transform roof, Transform props, float roofY, float ridgeY)
        {
            var wood = WoodMat;
            var dark = FrameMat;
            var steel = SteelMat;
            var iron = art.Lit(Hex("#3a2a20"));
            var rope = art.Lit(Hex("#b79a6a"));

            // footing: fieldstones under the left wall and along the front edge of the yard, a step in front of the workshop
            StoneLine(ground, new Vector3(-0.9f, 0.075f, -0.18f), new Vector3(-0.9f, 0.075f, 0.66f), 6, 0.1f, rng, StoneMat);
            StoneLine(ground, new Vector3(-0.88f, 0.055f, -0.97f), new Vector3(0.88f, 0.055f, -0.97f), 9, 0.075f, rng, StoneDarkMat);
            prim.Box(body, wood, new Vector3(-0.3f, 0.075f, -0.3f), new Vector3(0.5f, 0.05f, 0.15f));

            // left wall: a girt across the battens, a window sill and open shutters
            prim.Box(body, dark, new Vector3(-0.895f, 0.32f, 0.25f), new Vector3(0.03f, 0.045f, 0.92f), default, false);
            prim.Box(body, wood, new Vector3(-0.9f, 0.43f, 0.25f), new Vector3(0.06f, 0.03f, 0.34f), default, false);
            foreach (var side in new[] { -1f, 1f })
            {
                float hinge = 0.25f + side * 0.155f;
                var dir = new Vector3(-0.906f, 0f, side * 0.423f);
                var centre = new Vector3(-0.89f, 0.55f, hinge) + dir * 0.06f;
                prim.Box(body, wood, centre, new Vector3(0.022f, 0.27f, 0.12f), new Vector3(0f, -side * 65f, 0f), false);
            }

            // gable ends: tie beam, braces and crossed ridge boards (chigi)
            foreach (var sx in new[] { -0.3f - 0.61f, -0.3f + 0.61f })
            {
                float sign = sx < -0.3f ? -1f : 1f;
                float bx = sx + sign * 0.04f;
                prim.Box(roof, dark, new Vector3(bx, roofY + 0.1f, 0.25f), new Vector3(0.035f, 0.04f, 0.8f), default, false);
                prim.Box(roof, dark, new Vector3(bx, roofY + 0.22f, 0.25f - 0.12f), new Vector3(0.035f, 0.26f, 0.03f), new Vector3(-36f, 0f, 0f), false);
                prim.Box(roof, dark, new Vector3(bx, roofY + 0.22f, 0.25f + 0.12f), new Vector3(0.035f, 0.26f, 0.03f), new Vector3(36f, 0f, 0f), false);
                float fx = -0.3f + sign * 0.685f;
                prim.Box(roof, dark, new Vector3(fx, ridgeY + 0.08f, 0.25f), new Vector3(0.028f, 0.34f, 0.028f), new Vector3(30f, 0f, 0f), false);
                prim.Box(roof, dark, new Vector3(fx, ridgeY + 0.08f, 0.25f), new Vector3(0.028f, 0.34f, 0.028f), new Vector3(-30f, 0f, 0f), false);
            }

            // thatch (or slate) courses across the front slope, and rafter tips under the eave
            var course = art.NoOutline(level >= 3 ? Hex("#2c3a4d") : Hex("#a77a2c"), level >= 3 ? PaintedTexture.Roof : PaintedTexture.Thatch);
            const float roofH = 0.52f, roofCurve = 0.3f, halfDepth = 0.725f;
            foreach (float f in new[] { 0.3f, 0.55f, 0.8f })
            {
                float z = 0.25f - halfDepth * (1f - f);
                float y = roofY + RoofSurface(roofH, roofCurve, f) + 0.012f;
                float slope = Mathf.Atan2(RoofSurface(roofH, roofCurve, f + 0.05f) - RoofSurface(roofH, roofCurve, f - 0.05f), halfDepth * 0.1f) * Mathf.Rad2Deg;
                prim.Box(roof, course, new Vector3(-0.3f, y, z), new Vector3(1.28f, 0.02f, 0.04f), new Vector3(-slope, 0f, 0f), false);
            }
            for (int i = 0; i < 9; i++)
                prim.Box(roof, dark, new Vector3(-0.3f + (i - 4) * 0.15f, roofY - 0.02f, -0.56f), new Vector3(0.04f, 0.045f, 0.1f), default, false);

            // rain barrel with hoops at the front-left corner, a bucket on the workbench
            prim.Cylinder(props, wood, new Vector3(-0.82f, 0.16f, -0.52f), 0.1f, 0.22f);
            prim.Cylinder(props, iron, new Vector3(-0.82f, 0.1f, -0.52f), 0.104f, 0.02f, default, false);
            prim.Cylinder(props, iron, new Vector3(-0.82f, 0.22f, -0.52f), 0.104f, 0.02f, default, false);
            prim.Cylinder(props, art.NoOutline(Palette.WaterShallow), new Vector3(-0.82f, 0.265f, -0.52f), 0.082f, 0.01f, default, false);
            prim.Cylinder(body, wood, new Vector3(-0.52f, 0.375f, 0.5f), 0.055f, 0.09f);
            prim.Cylinder(body, art.NoOutline(Palette.WaterShallow), new Vector3(-0.52f, 0.42f, 0.5f), 0.045f, 0.008f, default, false);

            // sawdust under the sawhorse, a hand saw biting into its log, a stump to sit on and split kindling by the block
            prim.Sphere(ground, art.NoOutline(Hex("#e6cd9a")), new Vector3(0.1f, 0.055f, -0.72f), new Vector3(0.5f, 0.02f, 0.4f), false);
            prim.Box(props, steel, new Vector3(0.0f, 0.3f, -0.72f), new Vector3(0.3f, 0.075f, 0.012f), new Vector3(0f, 0f, -4f), false);
            prim.Box(props, wood, new Vector3(0.2f, 0.32f, -0.72f), new Vector3(0.05f, 0.07f, 0.03f), default, false);
            prim.Cylinder(props, art.Lit(Palette.TimberLight, PaintedTexture.Bark), new Vector3(0.6f, 0.12f, -0.82f), 0.1f, 0.14f);
            prim.Cylinder(props, LogEndMat, new Vector3(0.6f, 0.192f, -0.82f), 0.088f, 0.01f, default, false);
            for (int i = 0; i < 4; i++)
            {
                float a = (float)rng.NextDouble() * 180f;
                prim.Box(props, art.Lit(Palette.LogEnd), new Vector3(-0.24f + i * 0.045f, 0.085f + (i % 2) * 0.05f, -0.47f + (i % 2) * 0.03f), new Vector3(0.07f, 0.07f, 0.2f), new Vector3(0f, a, 0f));
            }
            // a woven basket of wood chips beside the stump
            prim.Cylinder(props, art.Lit(Hex("#b98a4a"), PaintedTexture.Thatch), new Vector3(0.85f, 0.115f, -0.75f), 0.09f, 0.13f);
            prim.Cylinder(props, rope, new Vector3(0.85f, 0.18f, -0.75f), 0.095f, 0.02f, default, false);
            prim.Sphere(props, art.Lit(Palette.LogEnd), new Vector3(0.85f, 0.185f, -0.75f), new Vector3(0.14f, 0.05f, 0.14f), false);
            // a coil of rope on a peg at the left wall
            prim.Cylinder(body, rope, new Vector3(-0.92f, 0.78f, 0.55f), 0.05f, 0.025f, new Vector3(0f, 0f, 90f), false);

            if (level >= 2)
            {
                // lean-to: rafters under its roof, a cross brace and a second lantern
                foreach (float z in new[] { -0.35f, -0.05f, 0.25f })
                    prim.Box(roof, dark, new Vector3(0.6f, 0.575f, z), new Vector3(0.85f, 0.04f, 0.04f), new Vector3(0f, 0f, -14f), false);
                prim.Box(body, dark, new Vector3(0.98f, 0.3f, -0.05f), new Vector3(0.04f, 0.04f, 0.7f), default, false);
                PaperLantern(body, new Vector3(-0.83f, 0.72f, -0.26f), 0.65f);
            }
            if (level >= 3)
            {
                // hanging signboard under the porch roof with a gold emblem
                foreach (float x in new[] { -0.86f, -0.7f })
                    prim.Box(body, rope, new Vector3(x, 0.735f, -0.5f), new Vector3(0.012f, 0.06f, 0.012f), default, false);
                prim.Box(body, art.Lit(Palette.BannerRed), new Vector3(-0.78f, 0.66f, -0.5f), new Vector3(0.24f, 0.17f, 0.025f), default, false);
                prim.Cylinder(body, GoldTrimMat, new Vector3(-0.78f, 0.66f, -0.52f), 0.045f, 0.012f, new Vector3(90f, 0f, 0f), false);
            }
        }

        // ---------------------------------------------------------------- Rice Paddy (2x2)

        private GameObject RicePaddy(int level, int w, int d)
        {
            var rng = Rng("rice_paddy");
            var root = NewRoot("RicePaddy_Model", 1.55f, 1.0f);
            var ground = Group(root, "Ground");
            var body = Group(root, "Body");
            var roof = Group(root, "Roof");
            var trees = Group(root, "Trees");
            var props = Group(root, "Props");

            var mud = art.Lit(Palette.Mud, PaintedTexture.Earth);
            var bank = art.Lit(Palette.Dirt, PaintedTexture.Earth);
            var water = art.NoOutline(Palette.WaterShallow);
            var green = art.Foliage(Color.white, false);
            var greenOutlined = art.Foliage(Color.white, true);

            prim.BoxOnGround(ground, mud, Vector3.zero, new Vector3(1.94f, 0.07f, 1.94f));
            prim.BoxOnGround(ground, water, new Vector3(0f, 0.07f, 0f), new Vector3(1.72f, 0.02f, 1.72f));
            prim.BoxOnGround(ground, bank, new Vector3(0f, 0f, -0.93f), new Vector3(1.94f, 0.15f, 0.12f));
            prim.BoxOnGround(ground, bank, new Vector3(0f, 0f, 0.93f), new Vector3(1.94f, 0.15f, 0.12f));
            prim.BoxOnGround(ground, bank, new Vector3(-0.93f, 0f, 0f), new Vector3(0.12f, 0.15f, 1.74f));
            prim.BoxOnGround(ground, bank, new Vector3(0.93f, 0f, 0f), new Vector3(0.12f, 0.15f, 1.74f));
            // little stones and grass tufts on the banks
            StoneLine(ground, new Vector3(-0.9f, 0.14f, -0.93f), new Vector3(0.9f, 0.14f, -0.93f), 7, 0.08f, rng, StoneMat);
            // reflected sky: pale streaks on the water
            for (int i = 0; i < 4; i++)
                prim.Box(ground, art.NoOutline(Color.Lerp(Palette.WaterShallow, Color.white, 0.55f)), new Vector3(-0.5f + i * 0.35f, 0.085f, -0.3f + (i % 2) * 0.5f), new Vector3(0.3f, 0.004f, 0.03f), default, false);

            // rows of young rice: soft leaf tufts (6 x 6 staggered)
            for (int ix = 0; ix < 6; ix++)
            {
                for (int iz = 0; iz < 6; iz++)
                {
                    float x = -0.7f + ix * 0.27f + (iz % 2) * 0.06f;
                    float z = -0.7f + iz * 0.27f;
                    if (x > 0.52f && z > 0.44f) continue; // the scarecrow stands in this corner
                    prim.Add(body, ToonMeshes.Blob(1, ix + iz, 0.35f, FoliagePalette.Leaf), green, new Vector3(x, 0.17f, z), new Vector3(0.17f, 0.3f, 0.17f), new Vector3(0f, (ix * 37 + iz * 53) % 360, 0f), "Rice", false);
                }
            }

            // scarecrow with hat and sleeves, straw sheaves, a bucket and a bamboo feed pipe on the bank
            prim.Cylinder(props, FrameMat, new Vector3(0.68f, 0.5f, 0.66f), 0.025f, 0.85f);
            prim.Box(props, FrameMat, new Vector3(0.68f, 0.7f, 0.66f), new Vector3(0.44f, 0.035f, 0.035f));
            prim.Box(props, art.Lit(Palette.KimonoIndigo), new Vector3(0.68f, 0.63f, 0.66f), new Vector3(0.3f, 0.2f, 0.03f), default, false);
            prim.Cone(props, ThatchMat, new Vector3(0.68f, 0.9f, 0.66f), 0.17f, 0.13f);
            prim.Sphere(props, art.Lit(Palette.Skin), new Vector3(0.68f, 0.86f, 0.66f), 0.055f);
            for (int i = 0; i < 2; i++)
            {
                var sheaf = new Vector3(-0.84f + i * 0.2f, 0.15f, 0.84f);
                prim.Cone(props, ThatchMat, sheaf - new Vector3(0f, 0.08f, 0f), 0.085f, 0.36f);
                prim.Cylinder(props, ThatchDarkMat, sheaf + new Vector3(0f, 0.06f, 0f), 0.05f, 0.03f);
            }
            prim.Cylinder(props, art.Lit(Palette.TimberLight, PaintedTexture.Planks), new Vector3(0.84f, 0.2f, -0.84f), 0.09f, 0.16f);
            prim.Cylinder(props, FrameMat, new Vector3(0.84f, 0.26f, -0.84f), 0.094f, 0.02f);
            prim.Cylinder(props, art.Lit(Palette.Bamboo), new Vector3(-0.3f, 0.26f, 0.95f), 0.035f, 0.9f, new Vector3(0f, 0f, 90f));
            prim.Cylinder(props, art.Lit(Palette.Bamboo), new Vector3(-0.78f, 0.22f, 0.95f), 0.035f, 0.1f);
            return root;
        }

        // ---------------------------------------------------------------- Stone Garden (2x2)

        private GameObject Garden(int level, int w, int d)
        {
            var rng = Rng("garden");
            var root = NewRoot("Garden_Model", 1.75f, 1.2f);
            var ground = Group(root, "Ground");
            var body = Group(root, "Body");
            var roof = Group(root, "Roof");
            var trees = Group(root, "Trees");
            var props = Group(root, "Props");

            var sand = art.Lit(Hex("#dccb9a"), PaintedTexture.Earth);
            var sandDark = art.Lit(Hex("#b9a677"), PaintedTexture.Earth);
            var rockA = art.Lit(Palette.RockA, PaintedTexture.Stone);
            var rockB = art.Lit(Palette.RockC, PaintedTexture.Stone);
            var moss = art.Foliage(ToonStyle.Moss, false);
            var pine = art.Foliage(Color.white);

            prim.BoxOnGround(ground, StoneDarkMat, Vector3.zero, new Vector3(1.94f, 0.06f, 1.94f));
            prim.BoxOnGround(ground, sand, new Vector3(0f, 0.06f, 0f), new Vector3(1.8f, 0.03f, 1.8f));
            for (int i = 0; i < 4; i++) // raked lines, curving around the rocks in two groups
                prim.BoxOnGround(ground, sandDark, new Vector3(-0.3f, 0.09f, -0.8f + i * 0.13f), new Vector3(0.7f, 0.014f, 0.03f));
            for (int i = 0; i < 6; i++)
                prim.BoxOnGround(ground, sandDark, new Vector3(0.2f, 0.09f, 0.1f + i * 0.12f), new Vector3(0.03f, 0.014f, 0.35f), default);
            // stepping stones across the sand
            for (int i = 0; i < 4; i++)
                prim.Cylinder(ground, StoneMat, new Vector3(-0.78f + i * 0.17f, 0.1f, 0.08f - i * 0.06f), 0.075f, 0.04f);

            // a small pond with a stone rim, lily pad and two koi
            var pondWater = art.NoOutline(Palette.WaterDeep);
            var pond = prim.Cylinder(body, pondWater, new Vector3(-0.15f, 0.085f, -0.45f), 0.3f, 0.02f);
            pond.transform.localScale = new Vector3(1.2f, 1f, 0.85f);
            for (int i = 0; i < 12; i++)
            {
                float a = i / 12f * Mathf.PI * 2f;
                var stone = prim.Sphere(body, i % 2 == 0 ? rockA : rockB, new Vector3(-0.15f + Mathf.Cos(a) * 0.37f, 0.1f, -0.45f + Mathf.Sin(a) * 0.27f), new Vector3(0.14f, 0.1f, 0.12f), false);
                stone.transform.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);
            }
            prim.Cylinder(body, art.NoOutline(Palette.LeafGreen), new Vector3(-0.05f, 0.1f, -0.52f), 0.07f, 0.01f);
            prim.Sphere(body, art.Lit(Hex("#f08a3c")), new Vector3(-0.28f, 0.105f, -0.4f), new Vector3(0.1f, 0.03f, 0.04f), false);
            prim.Sphere(body, art.Lit(Color.white), new Vector3(-0.22f, 0.105f, -0.52f), new Vector3(0.09f, 0.03f, 0.035f), false);

            // rocks with moss caps
            var rock = ToonMeshes.Blob(2, 11, 0.22f, FoliagePalette.None);
            prim.Add(body, rock, rockA, new Vector3(-0.5f, 0.17f, 0.45f), new Vector3(0.66f, 0.44f, 0.54f), new Vector3(0f, 30f, 0f), "Rock");
            prim.Add(body, ToonMeshes.Blob(1, 4, 0.3f, FoliagePalette.Leaf), moss, new Vector3(-0.5f, 0.37f, 0.45f), new Vector3(0.36f, 0.12f, 0.3f), default, "MossCap", false);
            prim.Add(body, rock, rockB, new Vector3(-0.1f, 0.1f, 0.68f), new Vector3(0.3f, 0.2f, 0.26f), new Vector3(0f, 70f, 0f), "Rock");
            prim.Add(body, rock, rockB, new Vector3(0.05f, 0.09f, 0.45f), new Vector3(0.22f, 0.16f, 0.2f), new Vector3(0f, 10f, 0f), "Rock");

            // bamboo water feature (shishi-odoshi) next to the pond
            prim.Cylinder(props, FrameMat, new Vector3(0.25f, 0.2f, -0.65f), 0.025f, 0.4f);
            prim.Cylinder(props, art.Lit(Palette.Bamboo), new Vector3(0.25f, 0.42f, -0.65f), 0.035f, 0.3f, new Vector3(0f, 0f, 60f));
            prim.Cylinder(props, StoneMat, new Vector3(0.18f, 0.08f, -0.5f), 0.1f, 0.14f);
            prim.Cylinder(props, art.NoOutline(Palette.WaterShallow), new Vector3(0.18f, 0.15f, -0.5f), 0.075f, 0.01f);
            StoneLantern(props, new Vector3(-0.72f, 0.06f, -0.74f), 1.05f);

            if (Props != null)
            {
                // a compact sakura in the back corner (crown capped at half a cell so it stays in the garden) and a bonsai pine
                var bark = BarkMat;
                prim.Cylinder(trees, bark, new Vector3(0.62f, 0.32f, -0.3f), 0.05f, 0.5f, new Vector3(0f, 0f, -12f));
                prim.Add(trees, ToonMeshes.Blob(2, 5, 0.25f, FoliagePalette.Spruce), pine, new Vector3(0.56f, 0.68f, -0.3f), new Vector3(0.46f, 0.26f, 0.42f), default, "Bonsai");
                prim.Add(trees, ToonMeshes.Blob(1, 6, 0.25f, FoliagePalette.Spruce), pine, new Vector3(0.72f, 0.5f, -0.22f), new Vector3(0.3f, 0.2f, 0.27f), default, "Bonsai");
                Limited(0.42f, () => Props.Cherry(trees, new Vector3(0.58f, 0.06f, 0.6f), rng, 0.42f));
            }
            return root;
        }

        // ---------------------------------------------------------------- Shrine (2x2)

        private GameObject Shrine(int level, int w, int d)
        {
            prim.ThinBoxes = true; // plaques, rafter tips and streamers: flat boxes for everything hair-thin
            try { return BuildShrine(level); }
            finally { prim.ThinBoxes = false; }
        }

        private GameObject BuildShrine(int level)
        {
            var root = NewRoot("Shrine_Model", 2.2f, 1.7f);
            var ground = Group(root, "Ground");
            var body = Group(root, "Body");
            var roof = Group(root, "Roof");
            var trees = Group(root, "Trees");
            var props = Group(root, "Props");

            var redDark = art.Lit(Hex("#9a2d25"), PaintedTexture.Planks);
            var black = art.Lit(Hex("#3a2a20"), PaintedTexture.Planks);
            var white = art.Lit(Hex("#f6f1e4"));

            prim.BoxOnGround(ground, StoneDarkMat, Vector3.zero, new Vector3(1.94f, 0.07f, 1.94f));
            prim.BoxOnGround(ground, art.Lit(Hex("#d8cfb6"), PaintedTexture.Stone), new Vector3(0f, 0.07f, 0f), new Vector3(1.78f, 0.02f, 1.78f));
            for (int i = 0; i < 3; i++)
                prim.BoxOnGround(ground, StoneMat, new Vector3(0f, 0.09f, -0.75f + i * 0.4f), new Vector3(0.36f, 0.03f, 0.28f));

            // small shrine building at the back on a stone base with steps, railing, offering box, bell
            prim.BoxOnGround(body, StoneMat, new Vector3(0f, 0.09f, 0.5f), new Vector3(0.9f, 0.14f, 0.72f));
            prim.BoxOnGround(body, StoneMat, new Vector3(0f, 0.09f, 0.1f), new Vector3(0.4f, 0.07f, 0.12f));
            prim.BoxOnGround(body, PlasterMat, new Vector3(0f, 0.23f, 0.5f), new Vector3(0.66f, 0.42f, 0.5f));
            foreach (var sx in new[] { -1f, 1f })
                prim.BoxOnGround(body, RedMat, new Vector3(sx * 0.32f, 0.23f, 0.24f), new Vector3(0.07f, 0.46f, 0.07f));
            prim.Box(body, black, new Vector3(0f, 0.36f, 0.24f), new Vector3(0.3f, 0.3f, 0.03f));
            prim.Box(body, RedMat, new Vector3(0f, 0.24f, 0.2f), new Vector3(0.5f, 0.035f, 0.03f), default, false);
            // offering box with slats, bell and rope
            prim.BoxOnGround(body, WoodMat, new Vector3(0f, 0.16f, 0.14f), new Vector3(0.36f, 0.13f, 0.11f));
            for (int i = 0; i < 5; i++) prim.Box(body, FrameMat, new Vector3(-0.14f + i * 0.07f, 0.3f, 0.14f), new Vector3(0.022f, 0.012f, 0.1f), default, false);
            prim.Sphere(body, GoldMat, new Vector3(0f, 0.5f, 0.18f), new Vector3(0.07f, 0.08f, 0.07f), false);
            prim.Cylinder(body, white, new Vector3(0f, 0.41f, 0.18f), 0.012f, 0.16f, default, false);
            // low fence (tamagaki) along the sides
            foreach (var sx in new[] { -1f, 1f })
            {
                for (int i = 0; i < 4; i++)
                    prim.BoxOnGround(body, white, new Vector3(sx * 0.6f, 0.09f, 0.3f + i * 0.17f), new Vector3(0.04f, 0.3f, 0.04f));
                prim.Box(body, RedMat, new Vector3(sx * 0.6f, 0.34f, 0.47f), new Vector3(0.04f, 0.035f, 0.56f), default, false);
            }
            prim.MeshObject(roof, ProceduralMeshes.HipRoof(1.0f, 0.86f, 0.44f, 0.5f, 0.07f), SlateMat, new Vector3(0f, 0.65f, 0.5f));
            foreach (var sx in new[] { -1f, 1f }) // chigi: crossed boards at the ridge ends
            {
                prim.Box(roof, black, new Vector3(sx * 0.17f, 1.12f, 0.5f), new Vector3(0.022f, 0.3f, 0.03f), new Vector3(0f, 0f, sx * 28f), false);
                prim.Box(roof, black, new Vector3(sx * 0.17f, 1.12f, 0.5f), new Vector3(0.022f, 0.3f, 0.03f), new Vector3(0f, 0f, -sx * 28f), false);
            }
            prim.Sphere(roof, GoldMat, new Vector3(0f, 1.12f, 0.5f), 0.045f);

            // torii gate in front with a twisted straw rope and paper zigzags
            const float gx = 0.52f, gz = -0.55f;
            foreach (var sx in new[] { -1f, 1f })
            {
                prim.Cylinder(props, RedMat, new Vector3(sx * gx, 0.62f, gz), 0.065f, 1.1f);
                prim.Cylinder(props, black, new Vector3(sx * gx, 0.1f, gz), 0.075f, 0.1f);
            }
            prim.Box(props, RedMat, new Vector3(0f, 1.0f, gz), new Vector3(1.32f, 0.07f, 0.07f));
            prim.Box(props, black, new Vector3(0f, 1.2f, gz), new Vector3(1.5f, 0.09f, 0.16f));
            prim.Box(props, black, new Vector3(-0.75f, 1.24f, gz), new Vector3(0.2f, 0.07f, 0.16f), new Vector3(0f, 0f, 20f));
            prim.Box(props, black, new Vector3(0.75f, 1.24f, gz), new Vector3(0.2f, 0.07f, 0.16f), new Vector3(0f, 0f, -20f));
            prim.Box(props, redDark, new Vector3(0f, 1.12f, gz), new Vector3(1.2f, 0.06f, 0.1f));
            prim.Cylinder(props, ThatchMat, new Vector3(0f, 0.93f, gz - 0.06f), 0.03f, 0.95f, new Vector3(0f, 0f, 90f), false);
            for (int i = 0; i < 4; i++)
                prim.Box(props, white, new Vector3(-0.33f + i * 0.22f, 0.83f, gz - 0.065f), new Vector3(0.05f, 0.14f, 0.012f), new Vector3(0f, 0f, i % 2 == 0 ? 12f : -12f), false);

            // a pair of fox guardians on small plinths at the steps, with red bibs
            foreach (var sx in new[] { -1f, 1f })
            {
                var fox = new GameObject("Kitsune").transform;
                fox.SetParent(props, false);
                fox.localPosition = new Vector3(sx * 0.42f, 0.09f, -0.05f);
                fox.localRotation = Quaternion.Euler(0f, 180f + sx * 20f, 0f); // facing the visitors, slightly turned in
                prim.BoxOnGround(fox, StoneDarkMat, Vector3.zero, new Vector3(0.22f, 0.08f, 0.26f));
                prim.Sphere(fox, white, new Vector3(0f, 0.2f, -0.02f), new Vector3(0.14f, 0.2f, 0.16f), false);
                prim.Sphere(fox, white, new Vector3(0f, 0.34f, 0.0f), new Vector3(0.1f, 0.1f, 0.1f), false);
                prim.Cone(fox, white, new Vector3(-0.035f, 0.37f, -0.01f), 0.03f, 0.08f);
                prim.Cone(fox, white, new Vector3(0.035f, 0.37f, -0.01f), 0.03f, 0.08f);
                prim.Box(fox, RedMat, new Vector3(0f, 0.27f, 0.045f), new Vector3(0.1f, 0.04f, 0.03f), default, false);
            }

            StoneLantern(props, new Vector3(-0.8f, 0.07f, 0.1f), 0.95f);
            StoneLantern(props, new Vector3(0.8f, 0.07f, 0.1f), 0.95f);
            ShrineDetails(level, Rng("shrine_details"), ground, body, roof, props, trees);
            return root;
        }

        /// <summary>
        /// Second pass over the shrine: layered plinth, side window and wall beams, rafter tips and corner finials, shimenawa over the door,
        /// bell rope, raked gravel, water basin, torii plaque, fox faces; level 2 adds prayer plaques, nobori banners and eave lanterns,
        /// level 3 gilded fittings, sake barrels, a finial on the roof and two more stone lanterns. Everything stays inside the footprint.
        /// </summary>
        private void ShrineDetails(int level, System.Random rng, Transform ground, Transform body, Transform roof, Transform props, Transform trees)
        {
            var wood = WoodMat;
            var dark = FrameMat;
            var black = art.Lit(Hex("#3a2a20"), PaintedTexture.Planks);
            var white = art.Lit(Hex("#f6f1e4"));
            var gold = GoldMat;
            var water = art.NoOutline(Palette.WaterShallow);
            var straw = ThatchMat;

            // plinth: a wider lower course under the shrine base
            prim.BoxOnGround(body, StoneDarkMat, new Vector3(0f, 0.09f, 0.5f), new Vector3(1.0f, 0.05f, 0.8f));

            // left wall: glowing lattice window, a dark sill beam and a head beam running round the wall
            Window(body, new Vector3(-0.335f, 0.44f, 0.5f), 0.2f, 0.14f, true);
            prim.Box(body, dark, new Vector3(-0.345f, 0.25f, 0.5f), new Vector3(0.03f, 0.035f, 0.54f), default, false);
            prim.Box(body, dark, new Vector3(0f, 0.25f, 0.245f), new Vector3(0.7f, 0.035f, 0.03f), default, false);
            prim.Box(body, dark, new Vector3(-0.345f, 0.58f, 0.5f), new Vector3(0.03f, 0.04f, 0.54f), default, false);
            prim.Box(body, dark, new Vector3(0f, 0.58f, 0.245f), new Vector3(0.7f, 0.04f, 0.03f), default, false);

            // eave: rafter tips under the lip and a finial on every corner (dark tile caps; gilded from level 2)
            for (int i = 0; i < 7; i++) // rafters run from the wall out under the eave
                prim.Box(roof, dark, new Vector3(-0.4f + i * 0.133f, 0.63f, 0.12f), new Vector3(0.03f, 0.03f, 0.34f), default, false);
            for (int i = 0; i < 6; i++)
                prim.Box(roof, dark, new Vector3(-0.45f, 0.63f, 0.15f + i * 0.14f), new Vector3(0.34f, 0.03f, 0.03f), default, false);
            var cornerCap = level >= 2 ? gold : black;
            foreach (var sx in new[] { -1f, 1f })
                foreach (var sz in new[] { -1f, 1f })
                    prim.Sphere(roof, cornerCap, new Vector3(sx * 0.585f, 0.72f, 0.5f + sz * 0.515f), 0.022f);

            // door: shimenawa rope with paper streamers (shide), a red head beam, the bell with a braided rope and a bell beam
            prim.Cylinder(body, straw, new Vector3(0f, 0.545f, 0.215f), 0.016f, 0.5f, new Vector3(0f, 0f, 90f), false);
            for (int i = 0; i < 4; i++)
                prim.Box(body, white, new Vector3(-0.15f + i * 0.1f, 0.5f, 0.212f), new Vector3(0.04f, 0.07f, 0.01f), new Vector3(0f, 0f, i % 2 == 0 ? 10f : -10f), false);
            prim.Box(body, RedMat, new Vector3(0f, 0.57f, 0.19f), new Vector3(0.24f, 0.03f, 0.04f), default, false);
            prim.Cylinder(body, RedMat, new Vector3(0f, 0.33f, 0.18f), 0.02f, 0.06f, default, false);
            prim.Cylinder(body, white, new Vector3(0.0f, 0.37f, 0.18f), 0.021f, 0.02f, default, false);

            // raked gravel beside the path, a water basin (temizuya) with a bamboo ladle, stepping stones
            var rake = art.NoOutline(Hex("#bfb497"));
            foreach (float x in new[] { -0.69f, -0.64f, 0.64f, 0.69f })
                prim.Box(ground, rake, new Vector3(x, 0.094f, -0.2f), new Vector3(0.014f, 0.004f, 0.6f), default, false);
            prim.BoxOnGround(props, StoneDarkMat, new Vector3(-0.74f, 0.09f, -0.68f), new Vector3(0.3f, 0.12f, 0.2f));
            prim.Box(props, water, new Vector3(-0.74f, 0.213f, -0.68f), new Vector3(0.24f, 0.01f, 0.14f), default, false);
            var bamboo = art.Lit(Palette.Bamboo);
            prim.Cylinder(props, bamboo, new Vector3(-0.74f, 0.235f, -0.68f), 0.01f, 0.24f, new Vector3(0f, 0f, 90f), false);
            prim.Cylinder(props, bamboo, new Vector3(-0.66f, 0.245f, -0.68f), 0.022f, 0.045f, new Vector3(0f, 0f, 90f), false);
            prim.Cylinder(props, bamboo, new Vector3(-0.84f, 0.3f, -0.74f), 0.014f, 0.2f);
            prim.Cylinder(props, bamboo, new Vector3(-0.8f, 0.37f, -0.72f), 0.012f, 0.1f, new Vector3(0f, 25f, 75f), false);
            var moss = art.Lit(Color.Lerp(ToonStyle.Moss, Palette.GrassDark, 0.3f));
            StoneLine(ground, new Vector3(-0.88f, 0.1f, -0.2f), new Vector3(-0.88f, 0.1f, -0.9f), 3, 0.09f, rng, StoneMat);
            StoneLine(ground, new Vector3(0.88f, 0.1f, -0.2f), new Vector3(0.88f, 0.1f, -0.9f), 3, 0.09f, rng, StoneMat);
            prim.Sphere(ground, moss, new Vector3(0.88f, 0.135f, -0.55f), new Vector3(0.1f, 0.03f, 0.08f), false);

            // torii: a gold name plaque between the beams and wedge keys where the lower beam meets the pillars
            const float gx = 0.52f, gz = -0.55f;
            prim.Box(props, black, new Vector3(0f, 1.06f, gz - 0.04f), new Vector3(0.17f, 0.12f, 0.02f), default, false);
            prim.Box(props, gold, new Vector3(0f, 1.06f, gz - 0.055f), new Vector3(0.13f, 0.085f, 0.012f), default, false);
            foreach (var sx in new[] { -1f, 1f })
            {
                prim.Box(props, black, new Vector3(sx * (gx + 0.08f), 1.0f, gz), new Vector3(0.03f, 0.11f, 0.05f), new Vector3(0f, 0f, sx * 8f), false);
                prim.Cylinder(props, StoneDarkMat, new Vector3(sx * gx, 0.075f, gz), 0.095f, 0.06f);
            }

            // foxes (turned to face the visitors in BuildShrine): add a snout, eyes, tail and front legs
            foreach (var sx in new[] { -1f, 1f })
            {
                var f = new GameObject("KitsuneDetail").transform;
                f.SetParent(props, false);
                f.localPosition = new Vector3(sx * 0.42f, 0.09f, -0.05f);
                f.localRotation = Quaternion.Euler(0f, 180f + sx * 20f, 0f); // the fox looks along its local +z
                var dk = art.NoOutline(Hex("#2a1d18"));
                prim.Sphere(f, white, new Vector3(0f, 0.325f, 0.065f), new Vector3(0.05f, 0.045f, 0.06f), false);
                prim.Sphere(f, dk, new Vector3(0f, 0.335f, 0.098f), 0.012f);
                foreach (var ex in new[] { -1f, 1f })
                    prim.Sphere(f, dk, new Vector3(ex * 0.026f, 0.352f, 0.05f), 0.009f);
                prim.Sphere(f, white, new Vector3(0f, 0.2f, -0.115f), new Vector3(0.07f, 0.16f, 0.07f), false);
                prim.Box(f, white, new Vector3(-0.03f, 0.11f, 0.06f), new Vector3(0.035f, 0.12f, 0.035f), default, false);
                prim.Box(f, white, new Vector3(0.03f, 0.11f, 0.06f), new Vector3(0.035f, 0.12f, 0.035f), default, false);
            }

            if (level >= 2)
            {
                // prayer plaques (ema) on a small rack behind the left lantern
                foreach (float z in new[] { 0.34f, 0.6f })
                    prim.Cylinder(props, black, new Vector3(-0.84f, 0.34f, z), 0.02f, 0.5f);
                prim.Box(props, black, new Vector3(-0.84f, 0.57f, 0.47f), new Vector3(0.06f, 0.035f, 0.34f), default, false);
                prim.Box(props, RedMat, new Vector3(-0.84f, 0.6f, 0.47f), new Vector3(0.14f, 0.02f, 0.36f), new Vector3(0f, 0f, 0f), false);
                for (int i = 0; i < 6; i++)
                {
                    var tint = i % 3 == 0 ? Palette.TimberLight : (i % 3 == 1 ? Hex("#d9b074") : Hex("#c28a52"));
                    prim.Box(props, art.Lit(tint, PaintedTexture.Planks), new Vector3(-0.84f, 0.5f, 0.36f + i * 0.045f), new Vector3(0.014f, 0.085f, 0.05f), new Vector3(i % 2 == 0 ? 6f : -6f, 0f, 0f), false);
                }

                // one nobori banner at the right of the entrance (a left one would hide the fox)
                foreach (var sx in new[] { 1f })
                {
                    float px = sx * 0.86f;
                    prim.Cylinder(props, black, new Vector3(px, 0.5f, -0.3f), 0.015f, 0.82f);
                    prim.Box(props, black, new Vector3(px - sx * 0.09f, 0.9f, -0.3f), new Vector3(0.2f, 0.014f, 0.014f), default, false);
                    prim.Box(props, white, new Vector3(px - sx * 0.09f, 0.6f, -0.3f), new Vector3(0.15f, 0.55f, 0.012f), default, false);
                    prim.Box(props, RedMat, new Vector3(px - sx * 0.09f, 0.86f, -0.304f), new Vector3(0.15f, 0.06f, 0.014f), default, false);
                    prim.Box(props, RedMat, new Vector3(px - sx * 0.09f, 0.62f, -0.304f), new Vector3(0.05f, 0.22f, 0.014f), default, false);
                    prim.Sphere(props, gold, new Vector3(px, 0.935f, -0.3f), 0.022f);
                }

                // paper lanterns under the front eave
                foreach (var sx in new[] { -1f, 1f })
                    PaperLantern(roof, new Vector3(sx * 0.56f, 0.52f, 0.02f), 0.55f);
            }

            if (level >= 3)
            {
                // sake barrels (komodaru) with straw wrap and rope bands, stacked at the right side of the shrine
                var barrel = art.Lit(Hex("#c9953c"), PaintedTexture.Thatch);
                var rope = art.Lit(Hex("#b79a6a"));
                foreach (float z in new[] { 0.37f, 0.55f })
                    StackedBarrel(props, barrel, rope, new Vector3(0.78f, 0.09f, z));
                StackedBarrel(props, barrel, rope, new Vector3(0.78f, 0.23f, 0.46f));

                // gilded fittings: finial on the roof, caps on the fence posts, golden bell
                prim.Cone(roof, gold, new Vector3(0f, 1.145f, 0.5f), 0.022f, 0.13f);
                foreach (var sx in new[] { -1f, 1f })
                    for (int i = 0; i < 4; i++)
                        prim.Sphere(body, gold, new Vector3(sx * 0.6f, 0.405f, 0.3f + i * 0.17f), 0.02f);

                // two more stone lanterns where the path meets the gravel
                StoneLantern(props, new Vector3(-0.34f, 0.07f, -0.86f), 0.8f);
                StoneLantern(props, new Vector3(0.34f, 0.07f, -0.86f), 0.8f);
            }

            if (Props != null)
            {
                // a blossom shrub and a green one in the back corners, out of the fence's way
                BlossomBush(trees, new Vector3(-0.86f, 0f, 0.86f), 0.75f, rng);
                Bush(trees, new Vector3(0.86f, 0f, 0.86f), 0.7f, rng);
            }
        }

        /// <summary>A small straw-wrapped sake barrel standing upright: body, two rope bands and a lid.</summary>
        private void StackedBarrel(Transform parent, Material barrel, Material rope, Vector3 basePos)
        {
            prim.Cylinder(parent, barrel, basePos + new Vector3(0f, 0.07f, 0f), 0.085f, 0.14f);
            foreach (float y in new[] { 0.035f, 0.105f })
                prim.Cylinder(parent, rope, basePos + new Vector3(0f, y, 0f), 0.089f, 0.014f, default, false);
            prim.Cylinder(parent, art.Lit(Palette.LogEnd), basePos + new Vector3(0f, 0.143f, 0f), 0.07f, 0.008f, default, false);
        }

        private void StoneLantern(Transform parent, Vector3 basePos, float scale)
        {
            var stone = StoneMat;
            var glow = art.Glow(ToonStyle.WindowGlow(false), ToonStyle.WindowGlow(false) * 0.7f);
            prim.Cylinder(parent, stone, basePos + new Vector3(0f, 0.04f * scale, 0f), 0.07f * scale, 0.08f * scale);
            prim.Cylinder(parent, stone, basePos + new Vector3(0f, 0.19f * scale, 0f), 0.035f * scale, 0.22f * scale);
            prim.Box(parent, stone, basePos + new Vector3(0f, 0.34f * scale, 0f), new Vector3(0.15f, 0.04f, 0.15f) * scale);
            prim.Box(parent, glow, basePos + new Vector3(0f, 0.41f * scale, 0f), new Vector3(0.09f, 0.1f, 0.09f) * scale);
            prim.Cone(parent, stone, basePos + new Vector3(0f, 0.46f * scale, 0f), 0.13f * scale, 0.1f * scale);
            prim.Sphere(parent, stone, basePos + new Vector3(0f, 0.575f * scale, 0f), 0.035f * scale);
        }
    }
}
