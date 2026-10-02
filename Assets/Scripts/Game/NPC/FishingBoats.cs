using System.Collections.Generic;
using AgeOfSakura.Core;
using UnityEngine;

namespace AgeOfSakura.Game
{
    /// <summary>
    /// A fishing boat with its fisher: leaves the Fisher Dock while the dock is catching fish (production "Fish" running), sails to a spot on
    /// the river, fishes there for a while, moves on, and heads home when the catch is done. Purely cosmetic, like the villagers.
    /// </summary>
    public sealed class FishingBoat
    {
        private enum Phase { Appearing, Sailing, Fishing, Returning, Leaving }

        private const float WaterY = 0.05f;

        private readonly GameSession session;
        private readonly BuildingInstance dock;
        private readonly GridPos berth;
        private readonly System.Random rng;
        private readonly Transform root;
        private readonly Transform hull;
        private readonly Transform rod;
        private readonly Transform bobber;
        private readonly ActorVisual fisher;
        private readonly Material rippleMaterial;
        private readonly Prim prim;
        private readonly List<GridPos> path = new List<GridPos>(32);
        private readonly List<GameObject> ripples = new List<GameObject>();
        private readonly List<float> rippleAges = new List<float>();

        private Phase phase = Phase.Appearing;
        private Vector3 position;
        private Vector3 spot;
        private int pathIndex;
        private float timer;
        private float age;
        private float nextCatch;
        private float speed;

        public bool IsGone { get; private set; }
        public string DockId => dock.InstanceId;
        public GridPos DockOrigin { get; }
        public int DockRotation { get; }

        public FishingBoat(GameSession session, BuildingInstance dock, GridPos berth, Transform parent, GameArt art, Prim prim, ActorFactory actors, System.Random rng)
        {
            this.session = session;
            this.dock = dock;
            this.berth = berth;
            this.rng = rng;
            this.prim = prim;
            DockOrigin = dock.Origin;
            DockRotation = dock.Rotation;
            speed = 0.62f;

            root = new GameObject("FishingBoat").transform;
            root.SetParent(parent, false);
            hull = new GameObject("Hull").transform;
            hull.SetParent(root, false);
            fisher = BuildBoat(art, prim, actors, hull, rng, out rod, out bobber);
            rippleMaterial = art.Transparent(new Color(1f, 1f, 1f, 0.55f));

            position = CellCenter(berth);
            root.position = position;
            root.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            root.localScale = Vector3.one * 0.1f;
            nextCatch = 2f;
        }

        private Vector3 CellCenter(GridPos c)
        {
            var (x, z) = session.Grid.CellCenterToWorld(c);
            return new Vector3(x, WaterY, z);
        }

        private bool Sailable(GridCell c) => c.Terrain == TerrainType.Water && !c.IsOccupied;

        /// <summary>Called when the dock stops catching: the boat finishes its leg, sails home and disappears.</summary>
        public void GoHome()
        {
            if (phase == Phase.Returning || phase == Phase.Leaving) return;
            var here = session.Grid.WorldToCell(position.x, position.z);
            phase = Phase.Returning;
            path.Clear();
            pathIndex = 0;
            if (!GridPathfinder.FindPath(session.Grid, here, berth, c => Sailable(c) || c.Pos == berth, path)) phase = Phase.Leaving; // cut off: just vanish
            timer = 0f;
        }

        public void Update(float dt)
        {
            age += dt;
            switch (phase)
            {
                case Phase.Appearing:
                    timer += dt;
                    root.localScale = Vector3.one * Mathf.Lerp(0.1f, 1f, EaseOut(timer / 0.7f));
                    if (timer >= 0.7f) { root.localScale = Vector3.one; PickSpot(); }
                    break;
                case Phase.Sailing:
                case Phase.Returning:
                    Sail(dt);
                    break;
                case Phase.Fishing:
                    Fish(dt);
                    break;
                case Phase.Leaving:
                    timer += dt;
                    root.localScale = Vector3.one * Mathf.Lerp(1f, 0.05f, EaseOut(timer / 0.7f));
                    if (timer >= 0.7f) Destroy();
                    break;
            }

            // gentle rocking on the water
            hull.localPosition = new Vector3(0f, 0.02f + Mathf.Sin(age * 2.1f) * 0.012f, 0f);
            hull.localRotation = Quaternion.Euler(Mathf.Sin(age * 1.7f) * 1.6f, 0f, Mathf.Sin(age * 1.3f + 1f) * 2.2f);
            fisher.Animate(NpcState.Idle, age, 0f);
            bobber.gameObject.SetActive(phase == Phase.Fishing);
            UpdateRipples(dt);
        }

        private static float EaseOut(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t);
        }

        private void PickSpot()
        {
            var grid = session.Grid;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                var goal = new GridPos(berth.X + rng.Next(-7, 8), berth.Z + rng.Next(-7, 8));
                if (!grid.InBounds(goal) || goal == berth || !Sailable(grid.GetCell(goal))) continue;
                if (!GridPathfinder.FindPath(grid, grid.WorldToCell(position.x, position.z), goal, Sailable, path) || path.Count == 0 || path.Count > 22) continue;
                pathIndex = 0;
                phase = Phase.Sailing;
                return;
            }
            // nowhere to go (a dead-end pond): fish right at the pier
            phase = Phase.Fishing;
            timer = 6f + (float)rng.NextDouble() * 4f;
            spot = position;
        }

        private void Sail(float dt)
        {
            if (pathIndex >= path.Count)
            {
                if (phase == Phase.Returning)
                {
                    phase = Phase.Leaving;
                    timer = 0f;
                }
                else
                {
                    phase = Phase.Fishing;
                    timer = 6f + (float)rng.NextDouble() * 5f;
                    spot = position;
                }
                return;
            }

            var target = CellCenter(path[pathIndex]);
            var delta = target - position;
            delta.y = 0f;
            float step = speed * dt;
            if (delta.magnitude <= step)
            {
                position = target;
                pathIndex++;
            }
            else
            {
                position += delta.normalized * step;
                var look = Quaternion.LookRotation(delta.normalized);
                root.rotation = Quaternion.RotateTowards(root.rotation, look, 110f * dt);
                if (Random.value < dt * 2.5f) AddRipple(position - delta.normalized * 0.35f, 0.18f);
            }
            root.position = position;
        }

        private void Fish(float dt)
        {
            timer -= dt;
            // the rod dips now and then, the bobber floats on the water ahead of the boat
            rod.localRotation = Quaternion.Euler(-28f + Mathf.Sin(age * 1.4f) * 4f, 0f, 0f);
            var bobPos = root.TransformPoint(new Vector3(0.34f, 0f, 0.62f));
            bobPos.y = WaterY + Mathf.Sin(age * 3.1f) * 0.01f;
            bobber.position = bobPos;
            nextCatch -= dt;
            if (nextCatch <= 0f)
            {
                nextCatch = 2.4f + (float)rng.NextDouble() * 2.2f;
                AddRipple(bobPos, 0.1f);
                rod.localRotation = Quaternion.Euler(-48f, 0f, 0f);
            }
            if (timer <= 0f) PickSpotOrGoHome();
        }

        private void PickSpotOrGoHome()
        {
            phase = Phase.Sailing;
            PickSpot();
        }

        private void AddRipple(Vector3 at, float startRadius)
        {
            var go = prim.Cylinder(root.parent, rippleMaterial, new Vector3(at.x, WaterY + 0.012f, at.z), startRadius, 0.004f, default, false);
            ripples.Add(go);
            rippleAges.Add(0f);
        }

        private void UpdateRipples(float dt)
        {
            for (int i = ripples.Count - 1; i >= 0; i--)
            {
                rippleAges[i] += dt;
                float t = rippleAges[i] / 1.1f;
                if (t >= 1f)
                {
                    Object.Destroy(ripples[i]);
                    ripples.RemoveAt(i);
                    rippleAges.RemoveAt(i);
                    continue;
                }
                ripples[i].transform.localScale = new Vector3(1f + t * 2.6f, 0.4f * (1f - t), 1f + t * 2.6f);
            }
        }

        public void Destroy()
        {
            IsGone = true;
            foreach (var r in ripples) if (r != null) Object.Destroy(r);
            ripples.Clear();
            if (bobber != null) Object.Destroy(bobber.gameObject);
            if (root != null) Object.Destroy(root.gameObject);
        }

        // ------------------------------------------------------------------ the boat itself

        /// <summary>A small open boat (about 1 long, 0.4 wide, bow along +Z) with a fisher and a rod. Returns the fisher; the rod and bobber are out parameters.</summary>
        private static ActorVisual BuildBoat(GameArt art, Prim prim, ActorFactory actors, Transform parent, System.Random rng, out Transform rod, out Transform bobber)
        {
            var plank = art.Lit(Palette.Plank, PaintedTexture.Planks);
            var plankLight = art.Lit(Palette.TimberLight, PaintedTexture.Planks);
            var dark = art.Lit(Palette.Timber, PaintedTexture.Planks);
            var rope = art.Lit(Palette.Hex("#b79a6a"));
            var silver = art.Lit(Palette.Hex("#b9c7d1"));
            var cork = art.Lit(Palette.Hex("#d6a45a"));
            var red = art.Lit(Palette.BannerRed);

            // hull: a flat bottom, two flared side boards, a pointed bow made of two angled boards and a flat stern
            prim.Box(parent, dark, new Vector3(0f, 0.03f, 0f), new Vector3(0.3f, 0.05f, 0.82f));
            foreach (var s in new[] { -1f, 1f })
            {
                prim.Box(parent, plank, new Vector3(s * 0.17f, 0.11f, -0.04f), new Vector3(0.05f, 0.17f, 0.8f), new Vector3(0f, 0f, s * -12f));
                prim.Box(parent, plankLight, new Vector3(s * 0.1f, 0.12f, 0.5f), new Vector3(0.05f, 0.17f, 0.34f), new Vector3(0f, s * -34f, s * -8f));
                prim.Box(parent, dark, new Vector3(s * 0.205f, 0.2f, -0.04f), new Vector3(0.025f, 0.03f, 0.82f), default, false);   // gunwale
            }
            prim.Box(parent, plank, new Vector3(0f, 0.11f, -0.43f), new Vector3(0.36f, 0.17f, 0.05f), new Vector3(-8f, 0f, 0f));       // stern board
            prim.Box(parent, dark, new Vector3(0f, 0.2f, 0.68f), new Vector3(0.04f, 0.2f, 0.05f), new Vector3(24f, 0f, 0f));          // raised prow
            prim.Box(parent, red, new Vector3(0f, 0.31f, 0.73f), new Vector3(0.05f, 0.05f, 0.05f), new Vector3(24f, 0f, 0f), false);

            // floor boards, two benches, a net pile and a crate with the catch
            prim.Box(parent, plankLight, new Vector3(0f, 0.065f, 0f), new Vector3(0.26f, 0.02f, 0.7f), default, false);
            foreach (var z in new[] { -0.28f, 0.28f })
                prim.Box(parent, plank, new Vector3(0f, 0.17f, z), new Vector3(0.36f, 0.035f, 0.1f), default, false);
            prim.Sphere(parent, rope, new Vector3(0.0f, 0.1f, -0.34f), new Vector3(0.2f, 0.09f, 0.14f), false);
            prim.Box(parent, plankLight, new Vector3(-0.05f, 0.1f, 0.36f), new Vector3(0.14f, 0.07f, 0.14f), default, false);
            for (int f = 0; f < 2; f++)
                prim.Sphere(parent, silver, new Vector3(-0.08f + f * 0.06f, 0.15f, 0.36f), new Vector3(0.05f, 0.03f, 0.12f), false);
            prim.Box(parent, dark, new Vector3(0.22f, 0.12f, -0.05f), new Vector3(0.02f, 0.025f, 0.5f), new Vector3(0f, 0f, 0f), false);   // an oar laid along the side

            // the fisher stands amidships, with a rod over the bow
            var fisher = actors.Villager(rng.Next(0, 6));
            fisher.Root.transform.SetParent(parent, false);
            fisher.Root.transform.localPosition = new Vector3(0f, 0.07f, -0.02f);
            fisher.Root.transform.localRotation = Quaternion.identity;
            fisher.Root.transform.localScale = Vector3.one * 0.62f;

            rod = new GameObject("Rod").transform;
            rod.SetParent(parent, false);
            rod.localPosition = new Vector3(0.17f, 0.4f, 0.1f);
            prim.Cylinder(rod, art.Lit(Palette.Bamboo), new Vector3(0f, 0f, 0.3f), 0.012f, 0.74f, new Vector3(90f, 0f, 0f), false);
            rod.localRotation = Quaternion.Euler(-28f, 0f, 0f);

            var bob = prim.Sphere(parent, cork, new Vector3(0.34f, 0.05f, 0.62f), 0.028f);
            bob.transform.SetParent(parent.parent.parent, true);
            bobber = bob.transform;
            return fisher;
        }
    }

    /// <summary>Keeps one <see cref="FishingBoat"/> per Fisher Dock that is catching fish right now.</summary>
    public sealed class FishingBoatManager : MonoBehaviour
    {
        public const string DockId = "fisher_dock";

        private readonly Dictionary<string, FishingBoat> boats = new Dictionary<string, FishingBoat>();
        private GameSession session;
        private GameArt art;
        private Prim prim;
        private ActorFactory actors;
        private System.Random rng;
        private float syncTimer;

        public int Count => boats.Count;

        public static FishingBoatManager Create(Transform parent, GameSession session, GameArt art, Prim prim, ActorFactory actors)
        {
            var go = new GameObject("FishingBoats");
            go.transform.SetParent(parent, false);
            var manager = go.AddComponent<FishingBoatManager>();
            manager.session = session;
            manager.art = art;
            manager.prim = prim;
            manager.actors = actors;
            manager.rng = new System.Random(session.Definitions.Map.Seed + 321);
            manager.Sync();
            return manager;
        }

        private void Update()
        {
            syncTimer -= Time.deltaTime;
            if (syncTimer <= 0f)
            {
                syncTimer = 0.4f;
                Sync();
            }
            float dt = Time.deltaTime;
            List<string> gone = null;
            foreach (var pair in boats)
            {
                pair.Value.Update(dt);
                if (pair.Value.IsGone) (gone ?? (gone = new List<string>())).Add(pair.Key);
            }
            if (gone != null) foreach (var id in gone) boats.Remove(id);
        }

        private void Sync()
        {
            foreach (var b in session.Buildings.All)
            {
                if (b.DefinitionId != DockId) continue;
                bool catching = b.State == ProductionState.Producing;
                boats.TryGetValue(b.InstanceId, out var boat);

                if (boat != null && (boat.DockOrigin != b.Origin || boat.DockRotation != b.Rotation))
                {
                    boat.Destroy(); // the dock was moved: the old berth is gone
                    boats.Remove(b.InstanceId);
                    boat = null;
                }
                if (catching && boat == null)
                {
                    if (TryFindBerth(b, out var berth)) boats[b.InstanceId] = new FishingBoat(session, b, berth, transform, art, prim, actors, new System.Random(rng.Next()));
                }
                else if (!catching && boat != null)
                {
                    boat.GoHome();
                }
            }
        }

        /// <summary>The free water cell next to the dock that lies furthest out along the pier.</summary>
        private bool TryFindBerth(BuildingInstance dock, out GridPos berth)
        {
            berth = default;
            var grid = session.Grid;
            if (!grid.TryGetFootprint(dock.InstanceId, out var origin, out int w, out int h)) return false;
            Vector2 dir;
            switch (Footprint.NormalizeRotation(dock.Rotation))
            {
                case 0: dir = new Vector2(0f, 1f); break;
                case 1: dir = new Vector2(1f, 0f); break;
                case 2: dir = new Vector2(0f, -1f); break;
                default: dir = new Vector2(-1f, 0f); break;
            }
            var centre = new Vector2(origin.X + w * 0.5f, origin.Z + h * 0.5f);
            float best = float.NegativeInfinity;
            bool found = false;
            for (int x = origin.X - 1; x <= origin.X + w; x++)
            {
                for (int z = origin.Z - 1; z <= origin.Z + h; z++)
                {
                    bool insideX = x >= origin.X && x < origin.X + w;
                    bool insideZ = z >= origin.Z && z < origin.Z + h;
                    if (insideX == insideZ) continue; // the four sides only
                    if (!grid.TryGetCell(x, z, out var cell) || cell.Terrain != TerrainType.Water || cell.IsOccupied) continue;
                    float score = Vector2.Dot(new Vector2(x + 0.5f, z + 0.5f) - centre, dir);
                    if (score > best)
                    {
                        best = score;
                        berth = cell.Pos;
                        found = true;
                    }
                }
            }
            return found;
        }
    }
}
