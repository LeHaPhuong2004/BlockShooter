using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Finds a clear entry lane, sharing the board graph only while testing one group's members.</summary>
internal static class GunPathPlanner
{
    [ThreadStatic] private static Workspace reusable;
    private static Workspace Scratch => reusable ?? (reusable = new Workspace());

    // Never retain collision results between group searches or frames.
    internal static void BeginGroup() { var w = Scratch; if (w.batchDepth++ == 0) w.graphValid = false; }
    internal static void EndGroup()
    {
        var w = Scratch;
        if (--w.batchDepth <= 0)
        {
            w.batchDepth = 0; w.graphValid = false;
            // Keep numeric buffers, never keep a finished scene alive through a static cache.
            w.grid = null; w.group = null;
        }
    }

    internal static bool TryFind(Vector2 start, Vector2 goal, GridManager grid,
        Func<Vector2, Vector2, bool> clear, out List<Vector2> path, BlockGroup ignoredGroup = null)
    {
        path = null;
        if (clear(start, goal))
        {
            path = new List<Vector2>(2) { start, goal };
            return true;
        }
        if (!BoardViewport.TryGetBounds(grid, out Bounds board)) return false;
        var w = Scratch;
        if (w.batchDepth == 0) w.graphValid = false;
        float inset = Mathf.Min(.025f, grid.cellSize * .05f);
        SpriteRenderer frame = grid.gridArea != null ? grid.gridArea.GetComponent<SpriteRenderer>() : null;
        bool hasGrid = frame != null;
        float frameBottom = hasGrid ? Mathf.Min(board.min.y, frame.bounds.min.y) : board.min.y;
        float approachY = start.y < board.min.y
            ? Mathf.Max(start.y, frameBottom - Mathf.Max(.15f, grid.cellSize * .35f))
            : board.min.y + inset;
        Vector2 approach = new Vector2(start.x, approachY);

        // Test entry routes without allocating a five-point List for every column.
        w.entries.Clear();
        AddX(w.entries, start.x, board, inset);
        AddX(w.entries, goal.x, board, inset);
        AddX(w.entries, board.min.x + inset, board, inset);
        AddX(w.entries, board.max.x - inset, board, inset);
        if (hasGrid)
            for (int x = 0; x < grid.width; x++) AddX(w.entries, grid.GetBlockPosition(x, 0).x, board, inset);
        bool foundEntry = false;
        float bestLength = float.PositiveInfinity, bestX = 0;
        if (clear(start, approach))
        {
            float prefix = Vector2.Distance(start, approach) + Mathf.Abs(goal.y - approachY);
            foreach (float x in w.entries)
            {
                float length = prefix + Mathf.Abs(start.x - x) + Mathf.Abs(goal.x - x);
                if (length >= bestLength) continue;
                Vector2 entry = new Vector2(x, approachY), turn = new Vector2(x, goal.y);
                if (!clear(approach, entry) || !clear(entry, turn) || !clear(turn, goal)) continue;
                foundEntry = true; bestLength = length; bestX = x;
            }
        }
        if (foundEntry)
        {
            path = new List<Vector2>(5) { start, approach, new Vector2(bestX, approachY), new Vector2(bestX, goal.y), goal };
            Simplify(path, clear);
            return true;
        }
        if (!hasGrid || grid.width <= 0 || grid.height <= 0) return false;

        if (!w.graphValid || w.grid != grid || w.group != ignoredGroup || w.start != start || w.approachY != approachY)
            BuildGraph(w, start, approachY, grid, board, inset, ignoredGroup, clear);

        // Source/grid edges and distances are independent of the chosen member.
        int bestNode = -1;
        bestLength = float.PositiveInfinity;
        foreach (int node in w.rowNodes)
        {
            if (node < 0 || float.IsPositiveInfinity(w.distance[node])) continue;
            float length = w.distance[node] + Vector2.Distance(w.points[node], goal);
            if (length >= bestLength || !clear(w.points[node], goal)) continue;
            bestLength = length; bestNode = node;
        }
        if (bestNode < 0) return false;
        path = new List<Vector2>();
        for (int at = bestNode; at >= 0; at = w.previous[at]) path.Add(w.points[at]);
        path.Reverse();
        if ((path[path.Count - 1] - goal).sqrMagnitude > .00000001f) path.Add(goal);
        Simplify(path, clear);
        return path.Count >= 2;
    }

    private static void BuildGraph(Workspace w, Vector2 start, float approachY, GridManager grid, Bounds board,
        float inset, BlockGroup ignoredGroup, Func<Vector2, Vector2, bool> clear)
    {
        w.points.Clear(); w.rowNodes.Clear(); w.edgeTo.Clear(); w.edgeNext.Clear(); w.edgeLength.Clear();
        w.grid = grid; w.group = ignoredGroup; w.start = start; w.approachY = approachY;
        AddNode(w, start);
        for (int y = 0; y < grid.height; y++)
        {
            float rowY = grid.GetBlockPosition(0, y).y;
            if (rowY > board.max.y + grid.cellSize * .5f) break;
            if (rowY < board.min.y - grid.cellSize * .5f) continue;
            for (int x = 0; x < grid.width; x++)
            {
                Vector2 point = grid.GetBlockPosition(x, y);
                point.x = Mathf.Clamp(point.x, board.min.x + inset, board.max.x - inset);
                point.y = Mathf.Clamp(point.y, board.min.y + inset, board.max.y - inset);
                // Check actual shapes: logical cells can lead animated collider positions.
                w.rowNodes.Add(IsOccupied(grid, x, y, point, ignoredGroup) ? -1 : AddNode(w, point));
            }
        }
        int approachStart = AddNode(w, new Vector2(start.x, approachY));
        Connect(w, 0, approachStart, clear);
        if (w.rowNodes.Count > 0)
        {
            for (int x = 0; x < grid.width; x++)
            {
                int entry = AddNode(w, new Vector2(Mathf.Clamp(grid.GetBlockPosition(x, 0).x, board.min.x + inset, board.max.x - inset), approachY));
                Connect(w, approachStart, entry, clear);
                Connect(w, entry, w.rowNodes[x], clear);
            }
            foreach (int node in w.rowNodes) Connect(w, 0, node, clear);
            int rows = w.rowNodes.Count / grid.width;
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < grid.width; x++)
                {
                    int node = w.rowNodes[y * grid.width + x];
                    if (x + 1 < grid.width) Connect(w, node, w.rowNodes[y * grid.width + x + 1], clear);
                    if (y + 1 < rows) Connect(w, node, w.rowNodes[(y + 1) * grid.width + x], clear);
                }
        }
        int count = w.points.Count;
        for (int i = 0; i < count; i++) { w.distance[i] = float.PositiveInfinity; w.previous[i] = -1; w.visited[i] = false; }
        w.distance[0] = 0;
        for (int step = 0; step < count; step++)
        {
            int current = -1;
            for (int i = 0; i < count; i++)
                if (!w.visited[i] && !float.IsPositiveInfinity(w.distance[i]) && (current < 0 || w.distance[i] < w.distance[current])) current = i;
            if (current < 0) break;
            w.visited[current] = true;
            for (int edge = w.head[current]; edge >= 0; edge = w.edgeNext[edge])
            {
                int next = w.edgeTo[edge];
                float distance = w.distance[current] + w.edgeLength[edge];
                if (distance >= w.distance[next]) continue;
                w.distance[next] = distance; w.previous[next] = current;
            }
        }
        w.graphValid = true;
    }

    private static bool IsOccupied(GridManager grid, int x, int y, Vector2 point, BlockGroup ignoredGroup)
    {
        if (ignoredGroup == null || grid.grid == null || x >= grid.grid.GetLength(0) || y >= grid.grid.GetLength(1)) return false;
        Block block = grid.grid[x, y];
        if (block == null || block.IsDestroyed || block.group == ignoredGroup || !block.gameObject.activeInHierarchy || !BoardViewport.IsBlockVisible(block)) return false;
        Collider2D collider = block.GetComponentInChildren<Collider2D>();
        return collider != null && collider.enabled && (!collider.isTrigger || Physics2D.queriesHitTriggers) &&
            ((1 << collider.gameObject.layer) & Physics2D.DefaultRaycastLayers) != 0 && collider.OverlapPoint(point);
    }

    private static int AddNode(Workspace w, Vector2 point)
    {
        int index = w.points.Count;
        w.EnsureNodes(index + 1); w.points.Add(point); w.head[index] = -1;
        return index;
    }
    private static void Connect(Workspace w, int a, int b, Func<Vector2, Vector2, bool> clear)
    {
        if (a < 0 || b < 0 || !clear(w.points[a], w.points[b])) return;
        float length = Vector2.Distance(w.points[a], w.points[b]);
        AddEdge(w, a, b, length); AddEdge(w, b, a, length);
    }
    private static void AddEdge(Workspace w, int a, int b, float length)
    {
        int index = w.edgeTo.Count;
        w.edgeTo.Add(b); w.edgeNext.Add(w.head[a]); w.edgeLength.Add(length); w.head[a] = index;
    }
    private static void AddX(List<float> values, float x, Bounds board, float inset)
    {
        x = Mathf.Clamp(x, board.min.x + inset, board.max.x - inset);
        foreach (float value in values) if (Mathf.Abs(value - x) < .001f) return;
        values.Add(x);
    }
    private static void Simplify(List<Vector2> points, Func<Vector2, Vector2, bool> clear)
    {
        for (int i = 0; i + 2 < points.Count; i++)
            for (int j = points.Count - 1; j > i + 1; j--)
                if (clear(points[i], points[j])) { points.RemoveRange(i + 1, j - i - 1); break; }
    }

    private sealed class Workspace
    {
        internal int batchDepth;
        internal bool graphValid;
        internal GridManager grid;
        internal BlockGroup group;
        internal Vector2 start;
        internal float approachY;
        internal readonly List<float> entries = new List<float>(16);
        internal readonly List<Vector2> points = new List<Vector2>(128);
        internal readonly List<int> rowNodes = new List<int>(128), edgeTo = new List<int>(768), edgeNext = new List<int>(768);
        internal readonly List<float> edgeLength = new List<float>(768);
        internal int[] head = new int[128], previous = new int[128];
        internal float[] distance = new float[128];
        internal bool[] visited = new bool[128];
        internal void EnsureNodes(int count)
        {
            if (head.Length >= count) return;
            int capacity = Math.Max(count, head.Length * 2);
            Array.Resize(ref head, capacity); Array.Resize(ref previous, capacity);
            Array.Resize(ref distance, capacity); Array.Resize(ref visited, capacity);
        }
    }
}
