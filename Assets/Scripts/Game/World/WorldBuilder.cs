using System;
using System.Collections.Generic;
using AgeOfSakura.Core;
using UnityEngine;

namespace AgeOfSakura.Game
{
    /// <summary>Everything the rest of the game needs from the built landscape.</summary>
    public sealed class WorldView
    {
        public Transform Root;
        public GridOverlay Grid;
        public LockedAreaOverlay Locked;
        public CosmeticRegistry Cosmetics;
        public Transform BuildingsRoot;
        public Light Sun;
    }

    /// <summary>Small removable vegetation, keyed by cell. Hidden while a building covers the cell.</summary>
    public sealed class CosmeticRegistry
    {
        private readonly Dictionary<GridPos, List<GameObject>> byCell = new Dictionary<GridPos, List<GameObject>>();

        public void Add(GridPos cell, GameObject go)
        {
            if (!byCell.TryGetValue(cell, out var list)) byCell[cell] = list = new List<GameObject>(2);
            list.Add(go);
        }

        public void Refresh(GridMap grid)
        {
            foreach (var pair in byCell)
            {
                bool visible = !grid.GetCell(pair.Key).IsOccupied;
                foreach (var go in pair.Value)
                {
                    if (go != null && go.activeSelf != visible) go.SetActive(visible);
                }
            }
        }
    }

    /// <summary>Builds the handcrafted Japanese valley from the logical map: terrain, stream, vegetation, landmarks, light.</summary>
    public sealed class WorldBuilder
    {
        private readonly GameArt art;
        private readonly Prim prim;
        private readonly PropFactory props;
        private readonly WorldAnimator animator;

        public WorldBuilder(GameArt art, Prim prim, PropFactory props, WorldAnimator animator)
        {
            this.art = art;
            this.prim = prim;
            this.props = props;
            this.animator = animator;
        }

        public WorldView Build(Transform parent, GameSession session)
        {
            var grid = session.Grid;
            var map = session.Definitions.Map;
            var rng = new System.Random(map.Seed);

            var root = new GameObject("World").transform;
            root.SetParent(parent, false);

            BuildGround(root, grid);
            BuildWater(root, grid);
            var view = new WorldView
            {
                Root = root,
                Cosmetics = new CosmeticRegistry(),
                Grid = new GridOverlay(art, root, grid),
                Locked = new LockedAreaOverlay(art, root, grid),
                Sun = BuildLighting(root)
            };

            // Two decor roots: near scenery (unlocked land) keeps ambient sway; far scenery is built without
            // sway and merged with static batching so hundreds of trees cost a handful of draw calls.
            var liveRoot = new GameObject("Decor_Animated").transform;
            liveRoot.SetParent(root, false);
            var staticRoot = new GameObject("Decor_Static").transform;
            staticRoot.SetParent(root, false);

            BuildCellDecor(liveRoot, staticRoot, grid, map, view.Cosmetics, rng);
            BuildShore(liveRoot, staticRoot, grid, map, view.Cosmetics, rng);
            props.MaxRadius = float.PositiveInfinity;
            props.AnimateProps = false;
            BuildBridges(staticRoot, grid);
            BuildLandmarks(staticRoot, grid, rng);
            BuildHills(staticRoot, grid, rng);
            props.AnimateProps = true;
            BuildButterflies(liveRoot, grid, rng);
            StaticBatchingUtility.Combine(staticRoot.gameObject);

            var buildings = new GameObject("Buildings").transform;
            buildings.SetParent(root, false);
            view.BuildingsRoot = buildings;

            // the boundary posts follow the edge of the open land, so they are rebuilt whenever the Town Hall opens more of it
            var boundary = new GameObject("Boundary").transform;
            boundary.SetParent(root, false);
            RebuildBoundary(boundary, grid);
            grid.LandUnlocked += () =>
            {
                view.Locked.Refresh();
                RebuildBoundary(boundary, grid);
            };

            view.Cosmetics.Refresh(grid);
            grid.OccupancyChanged += () => view.Cosmetics.Refresh(grid);
            return view;
        }

        private Vector3 CellCenter(GridMap grid, int x, int z) =>
            new Vector3((x + 0.5f) * grid.CellSize, 0f, (z + 0.5f) * grid.CellSize);

        private void BuildGround(Transform root, GridMap grid)
        {
            float w = grid.Width * grid.CellSize;
            float h = grid.Height * grid.CellSize;

            var far = new GameObject("FarGround");
            far.transform.SetParent(root, false);
            far.transform.localPosition = new Vector3(w * 0.5f - 90f, -0.03f, h * 0.5f - 90f);
            far.AddComponent<MeshFilter>().sharedMesh = ProceduralMeshes.GroundQuad(180f, 180f);
            far.AddComponent<MeshRenderer>().sharedMaterial = art.NoOutline(Palette.GrassFar);

            var ground = new GameObject("Ground");
            ground.transform.SetParent(root, false);
            ground.AddComponent<MeshFilter>().sharedMesh = ProceduralMeshes.GroundQuad(w, h);
            ground.AddComponent<MeshRenderer>().sharedMaterial = art.Lit(Color.white, TerrainTextures.BuildGround(grid), 0f);
        }

        private void BuildWater(Transform root, GridMap grid)
        {
            AddWaterLayer(root, grid, TerrainTextures.BuildRipples(3), 0.5f, new Color(1f, 1f, 1f, 0.75f), new Vector2(0f, -0.05f), 0.045f);
            AddWaterLayer(root, grid, TerrainTextures.BuildRipples(11), 0.37f, new Color(0.85f, 0.97f, 1f, 0.55f), new Vector2(0.025f, -0.1f), 0.05f);
        }

        private void AddWaterLayer(Transform root, GridMap grid, Texture2D texture, float uvScale, Color tint, Vector2 scrollSpeed, float y)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            const float inset = 0.1f;
            for (int x = 0; x < grid.Width; x++)
            {
                for (int z = 0; z < grid.Height; z++)
                {
                    var t = grid.GetCell(x, z).Terrain;
                    if (t != TerrainType.Water && t != TerrainType.Bridge) continue;
                    float x0 = (x + inset) * grid.CellSize, x1 = (x + 1 - inset) * grid.CellSize;
                    float z0 = (z + inset) * grid.CellSize, z1 = (z + 1 - inset) * grid.CellSize;
                    int i = verts.Count;
                    verts.Add(new Vector3(x0, 0f, z0)); verts.Add(new Vector3(x0, 0f, z1));
                    verts.Add(new Vector3(x1, 0f, z1)); verts.Add(new Vector3(x1, 0f, z0));
                    uvs.Add(new Vector2(x0 * uvScale, z0 * uvScale)); uvs.Add(new Vector2(x0 * uvScale, z1 * uvScale));
                    uvs.Add(new Vector2(x1 * uvScale, z1 * uvScale)); uvs.Add(new Vector2(x1 * uvScale, z0 * uvScale));
                    tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
                    tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
                }
            }
            if (verts.Count == 0) return;

            var mesh = new Mesh { name = "WaterLayer" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject("Water");
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(0f, y, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var mat = art.Transparent(tint, texture);
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            animator.AddScroller(mat, scrollSpeed);
        }

        private Light BuildLighting(Transform root)
        {
            var go = new GameObject("Sun");
            go.transform.SetParent(root, false);
            var sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft; // PCF soft, the style's shadow setting
            sun.shadowStrength = 0.72f;
            sun.shadowBias = 0.04f;
            sun.shadowNormalBias = 0.4f;
            // colour, intensity and direction come from the style (YAML light.day); the toon shader reads the hemisphere globals
            ToonStyle.ApplyLighting(sun, false);
            RenderSettings.fog = false;
            return sun;
        }

        private void BuildCellDecor(Transform liveRoot, Transform staticRoot, GridMap grid, MapDefinition map, CosmeticRegistry cosmetics, System.Random rng)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                for (int z = 0; z < grid.Height; z++)
                {
                    var cell = grid.GetCell(x, z);
                    bool near = cell.Unlocked || WillOpen(map, x, z);
                    var parent = near ? liveRoot : staticRoot;
                    props.AnimateProps = near;
                    var center = CellCenter(grid, x, z);
                    var jitter = new Vector3((float)(rng.NextDouble() - 0.5) * 0.4f, 0f, (float)(rng.NextDouble() - 0.5) * 0.4f);
                    // scenery next to buildable land must stay inside its own cell: buildings stay inside their footprint, so then nothing
                    // (crowns, boulders, bamboo) can ever clip a house, however the player builds
                    bool compact = NextToBuildableLand(grid, map, x, z);
                    props.MaxRadius = compact ? 0.47f : float.PositiveInfinity;
                    if (compact) jitter *= 0.2f;
                    switch (cell.Terrain)
                    {
                        case TerrainType.Tree:
                            int pick = rng.Next(20);
                            if (pick < 8) props.Pine(parent, center + jitter, rng);
                            else if (pick < 14) props.RoundTree(parent, center + jitter, rng);
                            else if (pick < 18) props.Birch(parent, center + jitter, rng);
                            else props.OldTree(parent, center + jitter, rng);
                            break;
                        case TerrainType.Cherry:
                            props.Cherry(parent, center, rng);
                            break;
                        case TerrainType.Bamboo:
                            props.Bamboo(parent, center + jitter * 0.5f, rng);
                            break;
                        case TerrainType.Rock:
                            props.Rock(parent, center, rng, true);
                            break;
                        case TerrainType.Mountain:
                            props.MaxRadius = float.PositiveInfinity;
                            cosmetics.Add(cell.Pos, props.MountainRock(parent, center, rng, MountainHeight(grid, x, z)));
                            break;
                        case TerrainType.Grass:
                        case TerrainType.Dirt:
                            AddCosmetic(parent, cosmetics, cell, center + jitter * 0.6f, rng);
                            break;
                    }
                }
            }
        }

        /// <summary>Mountain cells rise toward the middle of the range: one more neighbour in the rock, one more step of height.</summary>
        private static float MountainHeight(GridMap grid, int x, int z)
        {
            int around = 0;
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                    if ((dx != 0 || dz != 0) && grid.TryGetCell(x + dx, z + dz, out var c) && c.Terrain == TerrainType.Mountain) around++;
            return 0.9f + around * 0.28f;
        }

        /// <summary>Land that is closed now but opens at some Town Hall level.</summary>
        private static bool WillOpen(MapDefinition map, int x, int z)
        {
            foreach (var e in map.Expansions)
                if (x >= e.X && x < e.X + e.Width && z >= e.Z && z < e.Z + e.Height) return true;
            return false;
        }

        private static bool NextToBuildableLand(GridMap grid, MapDefinition map, int x, int z)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (grid.TryGetCell(x + dx, z + dz, out var c) && (c.Unlocked || WillOpen(map, x + dx, z + dz)) && c.TerrainBuildable) return true;
                }
            }
            return false;
        }

        /// <summary>Shore detail: rocks and cattails along the banks, lily pads on the water (all near scenery hides under buildings like other cosmetics).</summary>
        private void BuildShore(Transform liveRoot, Transform staticRoot, GridMap grid, MapDefinition map, CosmeticRegistry cosmetics, System.Random rng)
        {
            props.MaxRadius = float.PositiveInfinity;
            var dirs = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
            for (int x = 0; x < grid.Width; x++)
            {
                for (int z = 0; z < grid.Height; z++)
                {
                    var cell = grid.GetCell(x, z);
                    if (cell.Terrain != TerrainType.Water || NearBridge(grid, x, z)) continue;
                    var parent = cell.Unlocked || NextToBuildableLand(grid, map, x, z) ? liveRoot : staticRoot;
                    props.AnimateProps = parent == liveRoot;
                    var center = CellCenter(grid, x, z);

                    if (rng.NextDouble() < 0.16)
                        props.LilyPad(parent, center + new Vector3((float)(rng.NextDouble() - 0.5) * 0.5f, 0.0f, (float)(rng.NextDouble() - 0.5) * 0.5f), rng);

                    foreach (var (dx, dz) in dirs)
                    {
                        if (!grid.TryGetCell(x + dx, z + dz, out var land)) continue;
                        if (land.Terrain != TerrainType.Grass && land.Terrain != TerrainType.Dirt) continue;
                        double roll = rng.NextDouble();
                        float along = (float)(rng.NextDouble() - 0.5) * 0.6f;
                        var edge = center + new Vector3(dx * 0.5f + (dz != 0 ? along : 0f), 0f, dz * 0.5f + (dx != 0 ? along : 0f));
                        if (roll < 0.3)
                        {
                            var landPos = CellCenter(grid, x + dx, z + dz) + new Vector3(-dx * 0.32f + (dz != 0 ? along : 0f), 0f, -dz * 0.32f + (dx != 0 ? along : 0f));
                            var rock = props.Rock(parent, landPos, rng, false);
                            cosmetics.Add(land.Pos, rock);
                        }
                        else if (roll < 0.5)
                        {
                            props.Reeds(parent, edge + new Vector3(-dx * 0.1f, 0f, -dz * 0.1f), rng);
                        }
                    }
                }
            }
        }

        private static bool NearBridge(GridMap grid, int x, int z)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                    if (grid.TryGetCell(x + dx, z + dz, out var c) && c.Terrain == TerrainType.Bridge) return true;
            return false;
        }

        private void AddCosmetic(Transform parent, CosmeticRegistry registry, GridCell cell, Vector3 position, System.Random rng)
        {
            var kind = cell.Cosmetic;
            if (kind == CosmeticKind.None && cell.Terrain == TerrainType.Grass && rng.NextDouble() < (cell.Unlocked ? 0.16 : 0.22)) kind = CosmeticKind.Tuft;
            GameObject go = null;
            // mushrooms are ground detail that replaces some tufts (there is no core cosmetic kind for them on purpose)
            if (kind == CosmeticKind.Tuft && rng.NextDouble() < 0.14)
            {
                go = props.Mushrooms(parent, position, rng);
                registry.Add(cell.Pos, go);
                return;
            }
            switch (kind)
            {
                case CosmeticKind.Flowers: go = props.Flowers(parent, position, rng); break;
                case CosmeticKind.Shrub: go = props.Shrub(parent, position, rng); break;
                case CosmeticKind.Tuft: go = props.Tuft(parent, position, rng); break;
                case CosmeticKind.SmallRock: go = props.Rock(parent, position, rng, false); break;
            }
            if (go != null) registry.Add(cell.Pos, go);
        }

        private void BuildBridges(Transform parent, GridMap grid)
        {
            for (int z = 0; z < grid.Height; z++)
            {
                int x = 0;
                while (x < grid.Width)
                {
                    if (grid.GetCell(x, z).Terrain != TerrainType.Bridge) { x++; continue; }
                    int start = x;
                    while (x < grid.Width && grid.GetCell(x, z).Terrain == TerrainType.Bridge) x++;
                    float length = (x - start) * grid.CellSize + 0.7f;
                    props.Bridge(parent, new Vector3((start + x) * 0.5f * grid.CellSize, 0.02f, (z + 0.5f) * grid.CellSize), length);
                }
            }
        }

        private void RebuildBoundary(Transform parent, GridMap grid)
        {
            for (int i = parent.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(parent.GetChild(i).gameObject);
            props.AnimateProps = false;
            props.MaxRadius = float.PositiveInfinity;
            BuildBoundary(parent, grid);
        }

        /// <summary>Wooden posts and rope marking the edge of the unlocked area (only where the ground is open).</summary>
        private void BuildBoundary(Transform parent, GridMap grid)
        {
            var posts = new Dictionary<(int, int, int), Vector3>();
            for (int x = 0; x < grid.Width; x++)
            {
                for (int z = 0; z < grid.Height; z++)
                {
                    var cell = grid.GetCell(x, z);
                    if (!cell.Unlocked || !cell.TerrainBuildable) continue;
                    TryBoundaryPost(parent, grid, posts, cell, x, z, -1, 0);
                    TryBoundaryPost(parent, grid, posts, cell, x, z, 1, 0);
                    TryBoundaryPost(parent, grid, posts, cell, x, z, 0, -1);
                    TryBoundaryPost(parent, grid, posts, cell, x, z, 0, 1);
                }
            }
            foreach (var pair in posts)
            {
                var (px, pz, dir) = pair.Key;
                var neighborKey = dir == 0 ? (px, pz + 2, dir) : (px + 2, pz, dir);
                if (posts.TryGetValue(neighborKey, out var other)) props.Rope(parent, pair.Value + Vector3.up * 0.3f, other + Vector3.up * 0.3f);
            }
        }

        private void TryBoundaryPost(Transform parent, GridMap grid, Dictionary<(int, int, int), Vector3> posts, GridCell cell, int x, int z, int dx, int dz)
        {
            if (!grid.TryGetCell(x + dx, z + dz, out var outside) || outside.Unlocked || !outside.TerrainBuildable) return;
            // one post per boundary cell edge, keyed by (edge x, edge z, axis) so neighbours can be linked by rope
            Vector3 pos = new Vector3((x + 0.5f + dx * 0.5f) * grid.CellSize, 0f, (z + 0.5f + dz * 0.5f) * grid.CellSize);
            int axis = dx != 0 ? 0 : 1; // 0: edge runs along Z, 1: edge runs along X
            var key = axis == 0 ? (dx > 0 ? x + 1 : x, z, 0) : (x, dz > 0 ? z + 1 : z, 1);
            if ((axis == 0 ? z : x) % 2 != 0) return; // a post every second cell, roped together
            if (posts.ContainsKey(key)) return;
            posts[key] = pos;
            props.Post(parent, pos);
        }

        private void BuildLandmarks(Transform parent, GridMap grid, System.Random rng)
        {
            // Candidate cells are checked against the map; a candidate that is not open ground is skipped (and reported).
            PlaceLandmark(grid, new[] { (2, 10), (1, 11), (3, 9), (2, 8) }, "torii", pos => props.Torii(parent, pos, 90f));
            PlaceLandmark(grid, new[] { (21, 8), (22, 8), (21, 7) }, "bridge lantern", pos => props.StoneLantern(parent, pos));
            PlaceLandmark(grid, new[] { (21, 11), (22, 11), (21, 12) }, "bridge lantern 2", pos => props.StoneLantern(parent, pos));
            PlaceLandmark(grid, new[] { (26, 10), (27, 11), (25, 11) }, "east lantern", pos => props.StoneLantern(parent, pos));
            PlaceLandmark(grid, new[] { (9, 30), (10, 31), (8, 30), (11, 30) }, "south torii", pos => props.Torii(parent, pos, 0f));
            PlaceLandmark(grid, new[] { (5, 24), (6, 24), (4, 25) }, "south log pile", pos => props.LogPile(parent, pos, rng));
            PlaceLandmark(grid, new[] { (12, 25), (13, 26), (14, 25) }, "south signboard", pos => props.Signboard(parent, pos, 20f));
            PlaceLandmark(grid, new[] { (2, 5), (2, 6), (3, 3), (5, 2) }, "log pile", pos => props.LogPile(parent, pos, rng));
            PlaceLandmark(grid, new[] { (3, 2), (4, 1), (2, 2) }, "fence", pos => props.Fence(parent, pos, 1.6f, 20f));
            PlaceLandmark(grid, new[] { (22, 14), (23, 14), (22, 15), (24, 15) }, "well", pos => props.Well(parent, pos, rng));
            PlaceLandmark(grid, new[] { (2, 13), (3, 14), (2, 15), (1, 12) }, "signboard", pos => props.Signboard(parent, pos, 45f));
            PlaceLandmark(grid, new[] { (3, 17), (4, 16), (2, 16), (3, 15) }, "barrels", pos => { props.Barrel(parent, pos, rng); props.Crate(parent, pos + new Vector3(0.4f, 0f, 0.1f), rng); });
            PlaceLandmark(grid, new[] { (15, 2), (16, 2), (14, 3), (12, 3) }, "stone wall", pos => props.StoneWall(parent, pos, 1.4f, -25f));
            PlaceLandmark(grid, new[] { (2, 11), (2, 10), (1, 12), (2, 12) }, "banner", pos => props.BannerPole(parent, pos, 0f));
        }

        private void PlaceLandmark(GridMap grid, (int x, int z)[] candidates, string label, Action<Vector3> place)
        {
            foreach (var (x, z) in candidates)
            {
                if (!grid.TryGetCell(x, z, out var cell)) continue;
                if (cell.Unlocked || (cell.Terrain != TerrainType.Grass && cell.Terrain != TerrainType.Dirt)) continue;
                place(CellCenter(grid, x, z));
                return;
            }
            GameLogUnity.Warn($"Landmark '{label}' has no open locked cell among its candidates; skipped.");
        }

        private void BuildHills(Transform parent, GridMap grid, System.Random rng)
        {
            float cx = grid.Width * grid.CellSize * 0.5f;
            float cz = grid.Height * grid.CellSize * 0.5f;
            // the ring starts just outside the corners of the map (half the diagonal) and grows with its circumference
            float halfDiagonal = Mathf.Sqrt(cx * cx + cz * cz);
            int count = Mathf.RoundToInt(26f * halfDiagonal / 14.1f);
            for (int i = 0; i < count; i++)
            {
                float angle = (float)i / count * Mathf.PI * 2f + (float)rng.NextDouble() * 0.2f;
                float dist = halfDiagonal + 2.4f + (float)rng.NextDouble() * 9f;
                float radius = 3f + (float)rng.NextDouble() * 3f;
                float height = 1.4f + (float)rng.NextDouble() * 2.2f;
                var pos = new Vector3(cx + Mathf.Cos(angle) * dist, 0f, cz + Mathf.Sin(angle) * dist);
                props.Hill(parent, pos, radius, height, rng);
            }
        }

        private void BuildButterflies(Transform parent, GridMap grid, System.Random rng)
        {
            for (int i = 0; i < 4; i++)
            {
                var center = new Vector3(5f + (float)rng.NextDouble() * 9f, 0f, 5f + (float)rng.NextDouble() * 9f);
                props.Butterfly(parent, center, rng);
            }
        }
    }
}
