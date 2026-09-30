using System;
using System.Collections.Generic;

namespace AgeOfSakura.Core
{
    /// <summary>Breadth-first pathfinding over the grid (8 neighbours, no corner cutting). Used by wandering villagers and animals.</summary>
    public static class GridPathfinder
    {
        private static readonly int[] Dx = { 1, -1, 0, 0, 1, 1, -1, -1 };
        private static readonly int[] Dz = { 0, 0, 1, -1, 1, -1, 1, -1 };

        /// <summary>
        /// Fills <paramref name="path"/> with the cells to walk (start excluded, goal included).
        /// Returns false when the goal is unreachable. The start cell need not be walkable itself.
        /// </summary>
        public static bool FindPath(GridMap grid, GridPos start, GridPos goal, Func<GridCell, bool> walkable, List<GridPos> path)
        {
            path.Clear();
            if (!grid.InBounds(start) || !grid.InBounds(goal)) return false;
            if (start == goal) return true;
            if (!walkable(grid.GetCell(goal))) return false;

            int w = grid.Width;
            int h = grid.Height;
            var cameFrom = new int[w * h];
            for (int i = 0; i < cameFrom.Length; i++) cameFrom[i] = -2;
            var queue = new Queue<int>();
            int startIndex = start.Z * w + start.X;
            int goalIndex = goal.Z * w + goal.X;
            cameFrom[startIndex] = -1;
            queue.Enqueue(startIndex);

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                if (current == goalIndex) break;
                int cx = current % w;
                int cz = current / w;
                for (int d = 0; d < 8; d++)
                {
                    int nx = cx + Dx[d];
                    int nz = cz + Dz[d];
                    if (!grid.InBounds(nx, nz)) continue;
                    int ni = nz * w + nx;
                    if (cameFrom[ni] != -2) continue;
                    if (!walkable(grid.GetCell(nx, nz))) continue;
                    if (d >= 4)
                    {
                        // diagonal step: both orthogonal neighbours must be walkable so villagers do not clip corners
                        if (!walkable(grid.GetCell(cx + Dx[d], cz)) || !walkable(grid.GetCell(cx, cz + Dz[d]))) continue;
                    }
                    cameFrom[ni] = current;
                    queue.Enqueue(ni);
                }
            }

            if (cameFrom[goalIndex] == -2) return false;
            for (int i = goalIndex; i != startIndex; i = cameFrom[i]) path.Add(new GridPos(i % w, i / w));
            path.Reverse();
            return true;
        }
    }
}
