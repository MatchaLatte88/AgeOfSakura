using AgeOfSakura.Core;
using UnityEngine;

namespace AgeOfSakura.Game
{
    /// <summary>
    /// Scene object of one placed building. Gameplay identity (instance id, footprint, tap collider) is kept apart
    /// from the visual model, so swapping <see cref="CurrentModel"/> later breaks nothing:
    /// BuildingView
    ///   GameplayRoot (tap collider) | VisualRoot/CurrentModel | SelectionVisual | ProductionIndicatorAnchor | VFXAnchor
    /// </summary>
    public sealed class BuildingView : MonoBehaviour
    {
        public string InstanceId { get; private set; }
        public string DefinitionId { get; private set; }

        public Transform GameplayRoot { get; private set; }
        public Transform VisualRoot { get; private set; }
        public Transform CurrentModel { get; private set; }
        public Transform ProductionIndicatorAnchor { get; private set; }
        public Transform VfxAnchor { get; private set; }
        public WorldIndicator Indicator { get; private set; }

        private GameArt art;
        private Prim prim;
        private BoxCollider tapCollider;
        private GameObject selectionVisual;
        private BuildingModel modelInfo;
        private ParticleSystem smoke;
        private Material selectionMaterial;
        private bool settling;
        private float settleTime;
        private const float SettleDuration = 0.42f;

        public static BuildingView Create(BuildingInstance instance, BuildingDefinition definition, GridMap grid, GameArt art, Prim prim,
            IBuildingModelProvider models, VfxFactory vfx, Quaternion cameraRotation, Transform parent)
        {
            var go = new GameObject($"Building_{definition.Id}_{instance.InstanceId.Substring(0, 6)}");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<BuildingView>();
            view.Build(instance, definition, art, prim, models, vfx, cameraRotation);
            view.ApplyPlacement(instance, definition, grid);
            return view;
        }

        private void Build(BuildingInstance instance, BuildingDefinition def, GameArt artRef, Prim primRef, IBuildingModelProvider models, VfxFactory vfx, Quaternion cameraRotation)
        {
            art = artRef;
            prim = primRef;
            InstanceId = instance.InstanceId;
            DefinitionId = def.Id;

            GameplayRoot = new GameObject("GameplayRoot").transform;
            GameplayRoot.SetParent(transform, false);
            tapCollider = GameplayRoot.gameObject.AddComponent<BoxCollider>();

            VisualRoot = new GameObject("VisualRoot").transform;
            VisualRoot.SetParent(transform, false);

            // a higher building level asks the provider for a higher visual level (upgrades change the look)
            int visualLevel = def.VisualLevel + instance.Level - 1;
            var model = models.Create(def.VisualId, visualLevel, def.FootprintWidth, def.FootprintHeight);
            model.name = "CurrentModel";
            model.transform.SetParent(VisualRoot, false);
            CurrentModel = model.transform;
            modelInfo = model.GetComponent<BuildingModel>();
            if (modelInfo == null) modelInfo = model.AddComponent<BuildingModel>();

            if (def.Levels.Count > 0 && instance.Level > 1 && def.Category != "civic") AddLevelMarks(instance.Level, def);

            selectionVisual = new GameObject("SelectionVisual");
            selectionVisual.transform.SetParent(transform, false);
            selectionVisual.SetActive(false);

            ProductionIndicatorAnchor = new GameObject("ProductionIndicatorAnchor").transform;
            ProductionIndicatorAnchor.SetParent(transform, false);
            // lifted and nudged toward the (fixed) camera so buildings never hide it
            ProductionIndicatorAnchor.localPosition = new Vector3(-0.35f, modelInfo.IndicatorHeight, -0.35f);
            Indicator = WorldIndicator.Create(ProductionIndicatorAnchor, art, cameraRotation);

            VfxAnchor = modelInfo.SmokeAnchor != null ? modelInfo.SmokeAnchor : new GameObject("VFXAnchor").transform;
            if (modelInfo.SmokeAnchor == null) VfxAnchor.SetParent(transform, false);
            else smoke = vfx.CreateSmoke(VfxAnchor, Vector3.zero);
            if (smoke != null) smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        /// <summary>Snaps to the building's grid position/rotation and resizes footprint-dependent parts.</summary>
        public void ApplyPlacement(BuildingInstance instance, BuildingDefinition def, GridMap grid)
        {
            Footprint.Size(def.FootprintWidth, def.FootprintHeight, instance.Rotation, out int w, out int h);
            var (cx, cz) = grid.FootprintCenterToWorld(instance.Origin, w, h);
            transform.position = new Vector3(cx, 0f, cz);
            VisualRoot.localRotation = modelInfo.IgnorePlacementRotation
                ? Quaternion.identity
                : Quaternion.Euler(0f, 90f * instance.Rotation, 0f);

            float height = modelInfo.ColliderHeight;
            GameplayRoot.localPosition = new Vector3(0f, height * 0.5f, 0f);
            tapCollider.size = new Vector3(w * grid.CellSize - 0.1f, height, h * grid.CellSize - 0.1f);

            BuildSelectionRing(w * grid.CellSize, h * grid.CellSize);
        }

        private void BuildSelectionRing(float width, float depth)
        {
            for (int i = selectionVisual.transform.childCount - 1; i >= 0; i--)
                Destroy(selectionVisual.transform.GetChild(i).gameObject);

            if (selectionMaterial == null) selectionMaterial = art.Transparent(new Color(1f, 0.86f, 0.42f, 0.9f));
            var mat = selectionMaterial;
            float hw = width * 0.5f + 0.08f;
            float hd = depth * 0.5f + 0.08f;
            const float t = 0.07f;
            var parent = selectionVisual.transform;
            prim.Add(parent, art.Cube, mat, new Vector3(0f, 0.07f, -hd), new Vector3(hw * 2f + t, 0.02f, t), Vector3.zero, "Ring", false);
            prim.Add(parent, art.Cube, mat, new Vector3(0f, 0.07f, hd), new Vector3(hw * 2f + t, 0.02f, t), Vector3.zero, "Ring", false);
            prim.Add(parent, art.Cube, mat, new Vector3(-hw, 0.07f, 0f), new Vector3(t, 0.02f, hd * 2f + t), Vector3.zero, "Ring", false);
            prim.Add(parent, art.Cube, mat, new Vector3(hw, 0.07f, 0f), new Vector3(t, 0.02f, hd * 2f + t), Vector3.zero, "Ring", false);
        }

        /// <summary>
        /// Level 2 adds a pair of stone lanterns beside the entrance, level 3 also two banners. They are ordinary primitives, so they show
        /// with any model provider (including the flat test sprites) and make an upgrade visible at a glance.
        /// </summary>
        private void AddLevelMarks(int level, BuildingDefinition def)
        {
            var marks = new GameObject("LevelMarks").transform;
            marks.SetParent(VisualRoot, false);
            float half = def.FootprintWidth * 0.5f - 0.08f;
            float front = -(def.FootprintHeight * 0.5f - 0.05f);
            var stone = art.Lit(Palette.Stone);
            var glow = art.Lit(Palette.Hex("#F6D98A"));
            foreach (var sx in new[] { -1f, 1f })
            {
                var basePos = new Vector3(sx * half, 0f, front);
                prim.Cylinder(marks, stone, basePos + new Vector3(0f, 0.04f, 0f), 0.08f, 0.08f);
                prim.Cylinder(marks, stone, basePos + new Vector3(0f, 0.24f, 0f), 0.04f, 0.32f);
                prim.Box(marks, stone, basePos + new Vector3(0f, 0.44f, 0f), new Vector3(0.18f, 0.04f, 0.18f));
                prim.Box(marks, glow, basePos + new Vector3(0f, 0.52f, 0f), new Vector3(0.11f, 0.12f, 0.11f));
                prim.Cone(marks, stone, basePos + new Vector3(0f, 0.58f, 0f), 0.15f, 0.12f);
            }
            if (level < 3) return;

            var red = art.Lit(Palette.BannerRed);
            var gold = art.Lit(Palette.Gold, 0.3f);
            var pole = art.Lit(Palette.Timber);
            foreach (var sx in new[] { -1f, 1f })
            {
                var basePos = new Vector3(sx * (half + 0.28f), 0f, front + 0.25f);
                prim.Cylinder(marks, pole, basePos + new Vector3(0f, 0.75f, 0f), 0.03f, 1.5f);
                prim.Sphere(marks, gold, basePos + new Vector3(0f, 1.53f, 0f), 0.05f);
                prim.Box(marks, red, basePos + new Vector3(-sx * 0.13f, 1.15f, 0f), new Vector3(0.22f, 0.6f, 0.02f));
                prim.Box(marks, gold, basePos + new Vector3(-sx * 0.13f, 1.43f, 0f), new Vector3(0.22f, 0.045f, 0.025f));
            }
        }

        /// <summary>Roads only: which neighbours the paving reaches toward (see <see cref="RoadSurface"/>).</summary>
        public void SetRoadMask(int mask)
        {
            var surface = CurrentModel != null ? CurrentModel.GetComponentInChildren<RoadSurface>(true) : null;
            if (surface != null) surface.SetMask(mask);
        }

        public bool IsSelected => selectionVisual.activeSelf;

        public void SetSelected(bool selected) => selectionVisual.SetActive(selected);

        /// <summary>Hides the building while its ghost is being dragged (move mode).</summary>
        public void SetVisible(bool visible)
        {
            VisualRoot.gameObject.SetActive(visible);
            Indicator.gameObject.SetActive(visible);
            tapCollider.enabled = visible;
            if (!visible) selectionVisual.SetActive(false);
        }

        public void SetProducing(bool producing)
        {
            if (smoke == null) return;
            if (producing) smoke.Play(true);
            else smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        /// <summary>The style's construction animation for a newly placed or upgraded building (see <see cref="BuildInAnimation"/>).</summary>
        public void PlayBuildIn()
        {
            var animation = CurrentModel != null ? CurrentModel.GetComponent<BuildInAnimation>() : null;
            if (animation != null) animation.Play();
            else PlaySettle();
        }

        public void PlaySettle()
        {
            settling = true;
            settleTime = 0f;
            enabled = true;
            VisualRoot.localScale = new Vector3(1f, 0.3f, 1f);
        }

        private void Awake()
        {
            enabled = false; // Update only runs during the short settle animation
        }

        private void Update()
        {
            if (!settling) { enabled = false; return; }
            settleTime += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(settleTime / SettleDuration);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float eased = 1f + c3 * Mathf.Pow(p - 1f, 3f) + c1 * Mathf.Pow(p - 1f, 2f);
            float xz = Mathf.Lerp(1.12f, 1f, eased);
            VisualRoot.localScale = new Vector3(xz, Mathf.LerpUnclamped(0.3f, 1f, eased), xz);
            if (p >= 1f)
            {
                settling = false;
                VisualRoot.localScale = Vector3.one;
                enabled = false;
            }
        }
    }
}
