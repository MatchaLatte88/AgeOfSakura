using AgeOfSakura.Core;
using UnityEngine;

namespace AgeOfSakura.Game
{
    /// <summary>
    /// Grid shown only while placing/moving (or via the debug toggle). Cells differ by pattern, not just colour:
    /// free = light tint, blocked terrain = diagonal stripes, occupied = solid brown with a dot.
    /// </summary>
    public sealed class GridOverlay
    {
        private const int Px = 16;

        private readonly GridMap grid;
        private readonly Texture2D texture;
        private readonly GameObject go;
        private readonly Color32[] buffer;
        private string ignoreOccupantId;
        private bool placementVisible;
        private bool debugVisible;

        public GridOverlay(GameArt art, Transform parent, GridMap grid)
        {
            this.grid = grid;
            texture = new Texture2D(grid.Width * Px, grid.Height * Px, TextureFormat.RGBA32, false)
            {
                name = "GridOverlay",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            buffer = new Color32[texture.width * texture.height];

            go = new GameObject("GridOverlay");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = ProceduralMeshes.GroundQuad(grid.Width * grid.CellSize, grid.Height * grid.CellSize);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = art.Transparent(Color.white, texture);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            go.SetActive(false);
        }

        public bool DebugVisible
        {
            get => debugVisible;
            set { debugVisible = value; Apply(); }
        }

        /// <summary>While moving a building its own cells count as free.</summary>
        public void SetPlacementVisible(bool visible, string ignoreOccupant = null)
        {
            placementVisible = visible;
            ignoreOccupantId = ignoreOccupant;
            if (visible) Refresh();
            Apply();
        }

        private void Apply() => go.SetActive(placementVisible || debugVisible);

        /// <summary>Repaints from the current occupancy. Cheap enough to call whenever the grid changes while visible.</summary>
        public void Refresh()
        {
            for (int z = 0; z < grid.Height; z++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var cell = grid.GetCell(x, z);
                    for (int py = 0; py < Px; py++)
                    {
                        for (int px = 0; px < Px; px++)
                        {
                            buffer[(z * Px + py) * texture.width + x * Px + px] = Paint(cell, px, py, ignoreOccupantId);
                        }
                    }
                }
            }
            texture.SetPixels32(buffer);
            texture.Apply(false);
        }

        private static Color32 Paint(GridCell cell, int px, int py, string ignoreOccupant)
        {
            if (!cell.Unlocked) return new Color32(0, 0, 0, 0);
            bool edge = px == 0 || py == 0 || px == Px - 1 || py == Px - 1;
            if (edge) return new Color32(255, 255, 255, 120);

            if (cell.IsOccupied && cell.OccupantId != ignoreOccupant)
            {
                bool dot = Mathf.Abs(px - Px / 2) < 2 && Mathf.Abs(py - Px / 2) < 2;
                return dot ? new Color32(240, 225, 190, 200) : new Color32(120, 80, 50, 90);
            }
            if (!cell.TerrainBuildable)
            {
                bool stripe = ((px + py) % 8) < 3;
                return stripe ? new Color32(210, 70, 60, 150) : new Color32(210, 70, 60, 35);
            }
            return new Color32(250, 255, 235, 42);
        }
    }

    /// <summary>Soft dark veil over locked land (no padlocks). The system already knows which cells are unlocked.</summary>
    public sealed class LockedAreaOverlay
    {
        private readonly GridMap grid;
        private readonly Texture2D texture;
        private readonly GameObject go;

        public LockedAreaOverlay(GameArt art, Transform parent, GridMap grid)
        {
            this.grid = grid;
            texture = new Texture2D(grid.Width, grid.Height, TextureFormat.RGBA32, false)
            {
                name = "LockedOverlay",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            go = new GameObject("LockedAreaOverlay");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.025f, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = ProceduralMeshes.GroundQuad(grid.Width * grid.CellSize, grid.Height * grid.CellSize);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = art.Transparent(Color.white, texture);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            Refresh();
        }

        public bool Visible
        {
            get => go.activeSelf;
            set => go.SetActive(value);
        }

        public void Refresh()
        {
            var pixels = new Color32[grid.Width * grid.Height];
            for (int z = 0; z < grid.Height; z++)
                for (int x = 0; x < grid.Width; x++)
                    pixels[z * grid.Width + x] = grid.GetCell(x, z).Unlocked ? new Color32(0, 0, 0, 0) : new Color32(28, 40, 48, 92);
            texture.SetPixels32(pixels);
            texture.Apply(false);
        }
    }

    /// <summary>Cell-shaped footprint marker for the placement ghost: tinted quads plus a check or cross symbol.</summary>
    public sealed class FootprintMarker
    {
        private readonly GameArt art;
        private readonly Prim prim;
        private readonly Transform root;
        private readonly Material validMat;
        private readonly Material invalidMat;
        private readonly Material symbolMat;
        private Transform cells;
        private Transform checkSymbol;
        private Transform crossSymbol;
        private int lastW = -1;
        private int lastH = -1;

        public Transform Root => root;

        public FootprintMarker(GameArt art, Prim prim, Transform parent)
        {
            this.art = art;
            this.prim = prim;
            var go = new GameObject("FootprintMarker");
            go.transform.SetParent(parent, false);
            root = go.transform;
            validMat = art.Transparent(Palette.ValidFill);
            invalidMat = art.Transparent(Palette.InvalidFill);
            symbolMat = art.Transparent(new Color(1f, 1f, 1f, 0.95f));
        }

        /// <summary>Positions the marker on the ground at the footprint centre and shows validity.</summary>
        public void Show(Vector3 center, int width, int height, bool valid)
        {
            if (width != lastW || height != lastH) Rebuild(width, height);
            root.gameObject.SetActive(true);
            root.position = new Vector3(center.x, 0.06f, center.z);
            bool isValid = valid;
            foreach (Transform child in cells)
            {
                // the check/cross symbol groups live here too and have no renderer of their own
                if (child.TryGetComponent<MeshRenderer>(out var tile)) tile.sharedMaterial = isValid ? validMat : invalidMat;
            }
            checkSymbol.gameObject.SetActive(isValid);
            crossSymbol.gameObject.SetActive(!isValid);
        }

        public void Hide() => root.gameObject.SetActive(false);

        private void Rebuild(int width, int height)
        {
            lastW = width;
            lastH = height;
            if (cells != null) Object.Destroy(cells.gameObject);
            var cellsGo = new GameObject("Cells");
            cells = cellsGo.transform;
            cells.SetParent(root, false);
            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < height; z++)
                {
                    var q = prim.Add(cells, art.Cube, validMat,
                        new Vector3(x - width * 0.5f + 0.5f, 0f, z - height * 0.5f + 0.5f),
                        new Vector3(0.94f, 0.02f, 0.94f), Vector3.zero, "Cell", false);
                    q.GetComponent<MeshRenderer>().receiveShadows = false;
                }
            }

            var check = new GameObject("Check").transform;
            check.SetParent(cells, false);
            float s = Mathf.Min(width, height) * 0.5f + 0.2f;
            prim.Add(check, art.Cube, symbolMat, new Vector3(-0.22f * s, 0.05f, -0.06f * s), new Vector3(0.11f * s, 0.02f, 0.5f * s), new Vector3(0f, 45f, 0f), "CheckShort", false);
            prim.Add(check, art.Cube, symbolMat, new Vector3(0.14f * s, 0.05f, 0.12f * s), new Vector3(0.11f * s, 0.02f, 0.95f * s), new Vector3(0f, -45f, 0f), "CheckLong", false);
            checkSymbol = check;

            var cross = new GameObject("Cross").transform;
            cross.SetParent(cells, false);
            prim.Add(cross, art.Cube, symbolMat, new Vector3(0f, 0.05f, 0f), new Vector3(0.12f * s, 0.02f, 1.1f * s), new Vector3(0f, 45f, 0f), "CrossA", false);
            prim.Add(cross, art.Cube, symbolMat, new Vector3(0f, 0.05f, 0f), new Vector3(0.12f * s, 0.02f, 1.1f * s), new Vector3(0f, -45f, 0f), "CrossB", false);
            crossSymbol = cross;
        }
    }
}
