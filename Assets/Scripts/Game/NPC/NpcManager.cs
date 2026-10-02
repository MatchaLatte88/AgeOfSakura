using System.Collections.Generic;
using AgeOfSakura.Core;
using UnityEngine;

namespace AgeOfSakura.Game
{
    /// <summary>One wandering villager or animal: Idle for a while, then walks a grid path to a new spot. Purely cosmetic.</summary>
    public sealed class WanderAgent
    {
        private readonly GameSession session;
        private readonly ActorVisual visual;
        private readonly System.Random rng;
        private readonly float speed;
        private readonly int roamRadius;
        private readonly float minIdle;
        private readonly float maxIdle;
        private readonly bool visitsBuildings;
        private readonly List<GridPos> path = new List<GridPos>(32);

        private Vector3 position;
        private GridPos cell;
        private int pathIndex;
        private float idleTimer;
        private float walkPhase;
        private float age;

        public NpcState State { get; private set; } = NpcState.Idle;
        public Transform Transform => visual.Root.transform;

        public WanderAgent(GameSession session, ActorVisual visual, GridPos startCell, System.Random rng, float speed, int roamRadius, float minIdle, float maxIdle, bool visitsBuildings)
        {
            this.session = session;
            this.visual = visual;
            this.rng = rng;
            this.speed = speed;
            this.roamRadius = roamRadius;
            this.minIdle = minIdle;
            this.maxIdle = maxIdle;
            this.visitsBuildings = visitsBuildings;
            cell = startCell;
            position = CellPosition(startCell);
            visual.Root.transform.position = position;
            visual.Root.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            idleTimer = Range(0f, maxIdle);
        }

        private float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        private Vector3 CellPosition(GridPos c)
        {
            var (x, z) = session.Grid.CellCenterToWorld(c);
            float y = session.Grid.GetCell(c).Terrain == TerrainType.Bridge ? 0.12f : 0f;
            return new Vector3(x, y, z);
        }

        private bool Walkable(GridCell c)
        {
            // villagers stay in and just around the unlocked land, on free walkable ground
            if (!c.IsWalkable) return false;
            if (c.Unlocked) return true;
            var grid = session.Grid;
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                    if (grid.TryGetCell(c.Pos.X + dx, c.Pos.Z + dz, out var n) && n.Unlocked) return true;
            return false;
        }

        public void Update(float dt)
        {
            age += dt;
            var grid = session.Grid;
            var here = grid.WorldToCell(position.x, position.z);
            if (grid.InBounds(here) && !grid.GetCell(here).IsWalkable) Unstick();

            if (State == NpcState.Idle)
            {
                idleTimer -= dt;
                if (idleTimer <= 0f)
                {
                    if (!PickDestination()) idleTimer = Range(minIdle, maxIdle);
                }
            }
            else
            {
                Walk(dt);
                walkPhase += dt * (speed / 0.75f);
            }
            visual.Animate(State, age, walkPhase);
        }

        private void Walk(float dt)
        {
            if (pathIndex >= path.Count)
            {
                Stop();
                return;
            }

            var next = path[pathIndex];
            if (!session.Grid.GetCell(next).IsWalkable)
            {
                // a building was placed on our route: pick a new destination
                if (!PickDestination()) Stop();
                return;
            }

            var target = CellPosition(next);
            var delta = target - position;
            delta.y = 0f;
            float step = speed * dt;
            if (delta.magnitude <= step)
            {
                position = target;
                cell = next;
                pathIndex++;
            }
            else
            {
                position += delta.normalized * step;
                position.y = Mathf.Lerp(position.y, target.y, 0.2f);
                var look = Quaternion.LookRotation(delta.normalized);
                visual.Root.transform.rotation = Quaternion.RotateTowards(visual.Root.transform.rotation, look, 540f * dt);
            }
            visual.Root.transform.position = position;
        }

        private void Stop()
        {
            State = NpcState.Idle;
            idleTimer = Range(minIdle, maxIdle);
            walkPhase = 0f;
        }

        private bool PickDestination()
        {
            var grid = session.Grid;
            var start = grid.WorldToCell(position.x, position.z);
            if (!grid.InBounds(start)) return false;

            for (int attempt = 0; attempt < 8; attempt++)
            {
                GridPos goal;
                if (visitsBuildings && rng.NextDouble() < 0.35 && TryPickNearBuilding(out goal)) { }
                else
                {
                    goal = new GridPos(start.X + rng.Next(-roamRadius, roamRadius + 1), start.Z + rng.Next(-roamRadius, roamRadius + 1));
                    if (!grid.InBounds(goal)) continue;
                }
                if (goal == start) continue;
                if (!GridPathfinder.FindPath(grid, start, goal, Walkable, path) || path.Count == 0 || path.Count > 30) continue;

                pathIndex = 0;
                State = NpcState.Walking;
                return true;
            }
            path.Clear();
            return false;
        }

        private bool TryPickNearBuilding(out GridPos goal)
        {
            goal = default;
            var all = session.Buildings.All;
            if (all.Count == 0) return false;
            var b = all[rng.Next(all.Count)];
            session.Buildings.GetFootprintSize(b, out int w, out int h);
            // a cell on the ring around the footprint, in front of it
            var candidate = new GridPos(b.Origin.X + rng.Next(-1, w + 1), b.Origin.Z - 1);
            if (!session.Grid.InBounds(candidate)) return false;
            goal = candidate;
            return Walkable(session.Grid.GetCell(candidate));
        }

        /// <summary>Something was built on top of us: hop to the nearest free walkable cell.</summary>
        private void Unstick()
        {
            var grid = session.Grid;
            var here = grid.WorldToCell(position.x, position.z);
            for (int r = 1; r <= 6; r++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dz = -r; dz <= r; dz++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != r) continue;
                        if (!grid.TryGetCell(here.X + dx, here.Z + dz, out var c) || !Walkable(c)) continue;
                        cell = c.Pos;
                        position = CellPosition(c.Pos);
                        visual.Root.transform.position = position;
                        path.Clear();
                        Stop();
                        return;
                    }
                }
            }
        }
    }

    /// <summary>Owns all wandering actors and updates them from a single Update (no per-agent MonoBehaviours).</summary>
    public sealed class NpcManager : MonoBehaviour
    {
        private readonly List<WanderAgent> agents = new List<WanderAgent>(8);

        public int Count => agents.Count;

        public static NpcManager Create(Transform parent, GameSession session, ActorFactory factory)
        {
            var go = new GameObject("Npcs");
            go.transform.SetParent(parent, false);
            var manager = go.AddComponent<NpcManager>();
            manager.Populate(session, factory);
            return manager;
        }

        private void Populate(GameSession session, ActorFactory factory)
        {
            var rng = new System.Random(session.Definitions.Map.Seed + 99);

            // 3 villagers, a dog and a few chickens, spawned on free walkable cells in the open foreground.
            for (int i = 0; i < 3; i++)
                Add(session, factory.Villager(i), FindFreeCell(session, 7 + i * 2, 6 + i), rng, 0.72f, 7, 1.5f, 5f, true);
            Add(session, factory.Dog(), FindFreeCell(session, 8, 8), rng, 1.5f, 6, 1.5f, 4f, false);
            for (int i = 0; i < 3; i++)
                Add(session, factory.Chicken(i == 1), FindFreeCell(session, 12 + i % 2, 6 + i), rng, 0.45f, 2, 0.8f, 3f, false);
        }

        private void Add(GameSession session, ActorVisual visual, GridPos start, System.Random rng, float speed, int radius, float minIdle, float maxIdle, bool visits)
        {
            visual.Root.transform.SetParent(transform, false);
            agents.Add(new WanderAgent(session, visual, start, new System.Random(rng.Next()), speed, radius, minIdle, maxIdle, visits));
        }

        private static GridPos FindFreeCell(GameSession session, int x, int z)
        {
            var grid = session.Grid;
            for (int r = 0; r < 10; r++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dz = -r; dz <= r; dz++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != r) continue;
                        if (grid.TryGetCell(x + dx, z + dz, out var c) && c.IsWalkable && c.Unlocked) return c.Pos;
                    }
                }
            }
            throw new System.InvalidOperationException("No free walkable cell for an NPC; check the map definition.");
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < agents.Count; i++) agents[i].Update(dt);
        }
    }
}
