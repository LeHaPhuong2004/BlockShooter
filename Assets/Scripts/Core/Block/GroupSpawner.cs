using System.Collections.Generic;
using UnityEngine;

public class GroupSpawner
{
    private GridManager gridManager;

    [SerializeField] private float frozenChance = 0.3f;
    [SerializeField] private float reinforcedChance = 0.3f;
    [SerializeField] private float mysteryChance = 0.2f;
    [SerializeField] private float bossChance = 0f;

    private bool bossSpawned = false;

    public GroupSpawner(GridManager gridManager)
    {
        this.gridManager = gridManager;
    }

    public void SpawnGroups()
    {
        bossSpawned = false;

        int attempts = 0;
        int maxAttempts = 5000;

        while (attempts < maxAttempts)
        {
            attempts++;

            List<Vector2Int> shape = CreateRandomShape();

            if (shape.Count < gridManager.minGroupSize)
            {
                continue;
            }

            if (shape.Count == 9)
            {
                if (Random.value > 0.15f)
                    continue;
            }

            BlockColorData color = gridManager.colors[Random.Range(0, gridManager.colors.Length)];
            BlockType type = GetRandomBlockType(shape);

            if (type == BlockType.Boss)
            {
                shape = CreateBossShape(shape);
            }

            Vector2Int position = FindPositionForShape(shape, color, type);

            if (position.x < 0)
            {
                if (attempts > 2000) break;
                continue;
            }

            SpawnGroup(shape, position, color, type);

            if (type == BlockType.Boss)
            {
                bossSpawned = true;
            }
        }

        FillSmallGaps();

        Debug.Log("Spawned Groups: " + gridManager.Groups.Count + " | Attempts: " + attempts);
    }

    private List<Vector2Int> CreateRandomShape()
    {
        List<List<Vector2Int>> shapes = new List<List<Vector2Int>>();

        shapes.Add(new List<Vector2Int> { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(3, 0) });
        shapes.Add(new List<Vector2Int> { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1) });
        shapes.Add(new List<Vector2Int> { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2), new Vector2Int(1, 0) });
        shapes.Add(new List<Vector2Int> { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(1, 2) });
        shapes.Add(new List<Vector2Int> { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2), new Vector2Int(1, 2) });
        shapes.Add(new List<Vector2Int> { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2), new Vector2Int(1, 0) });
        shapes.Add(new List<Vector2Int> { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(1, 1) });
        shapes.Add(new List<Vector2Int> { new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1) });
        shapes.Add(new List<Vector2Int> { new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(1, 0), new Vector2Int(2, 0) });
        shapes.Add(new List<Vector2Int> { new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1) });
        shapes.Add(new List<Vector2Int> { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 2) });
        shapes.Add(new List<Vector2Int> {
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0),
            new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1),
            new Vector2Int(0, 2), new Vector2Int(1, 2), new Vector2Int(2, 2)
        });

        List<Vector2Int> shape = new List<Vector2Int>(shapes[Random.Range(0, shapes.Count)]);

        int rotations = Random.Range(0, 4);
        for (int i = 0; i < rotations; i++)
        {
            for (int j = 0; j < shape.Count; j++)
            {
                Vector2Int p = shape[j];
                shape[j] = new Vector2Int(-p.y, p.x);
            }
            NormalizeShape(shape);
        }

        if (Random.value < 0.5f)
        {
            for (int i = 0; i < shape.Count; i++)
            {
                Vector2Int p = shape[i];
                shape[i] = new Vector2Int(-p.x, p.y);
            }
            NormalizeShape(shape);
        }

        if (!IsConnectedShape(shape))
            return CreateRandomShape();

        return shape;
    }

    private List<Vector2Int> CreateBossShape(List<Vector2Int> shape)
    {
        List<Vector2Int> bossShape = new List<Vector2Int>();
        foreach (Vector2Int p in shape)
        {
            bossShape.Add(new Vector2Int(p.x * 2, p.y * 2));
            bossShape.Add(new Vector2Int(p.x * 2 + 1, p.y * 2));
            bossShape.Add(new Vector2Int(p.x * 2, p.y * 2 + 1));
            bossShape.Add(new Vector2Int(p.x * 2 + 1, p.y * 2 + 1));
        }
        NormalizeShape(bossShape);
        return bossShape;
    }

    private bool IsConnectedShape(List<Vector2Int> shape)
    {
        if (shape == null || shape.Count == 0) return false;

        HashSet<Vector2Int> cells = new HashSet<Vector2Int>(shape);
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        Queue<Vector2Int> queue = new Queue<Vector2Int>();

        Vector2Int start = shape[0];
        queue.Enqueue(start);
        visited.Add(start);

        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            foreach (Vector2Int dir in directions)
            {
                Vector2Int next = current + dir;
                if (cells.Contains(next) && !visited.Contains(next))
                {
                    visited.Add(next);
                    queue.Enqueue(next);
                }
            }
        }
        return visited.Count == cells.Count;
    }

    private void NormalizeShape(List<Vector2Int> shape)
    {
        int minX = int.MaxValue;
        int minY = int.MaxValue;
        foreach (Vector2Int p in shape)
        {
            minX = Mathf.Min(minX, p.x);
            minY = Mathf.Min(minY, p.y);
        }
        for (int i = 0; i < shape.Count; i++)
        {
            shape[i] -= new Vector2Int(minX, minY);
        }
    }

    private Vector2Int FindPositionForShape(List<Vector2Int> shape, BlockColorData color, BlockType type)
    {
        List<Vector2Int> positions = new List<Vector2Int>();
        for (int y = 0; y < gridManager.height; y++)
        {
            for (int x = 0; x < gridManager.width; x++)
            {
                if (CanPlaceShape(shape, x, y, color, type))
                {
                    positions.Add(new Vector2Int(x, y));
                }
            }
        }
        return positions.Count == 0 ? new Vector2Int(-1, -1) : positions[Random.Range(0, positions.Count)];
    }

    private bool CanPlaceShape(List<Vector2Int> shape, int startX, int startY, BlockColorData color, BlockType type)
    {
        Block[,] grid = gridManager.GetGrid();
        foreach (Vector2Int offset in shape)
        {
            int x = startX + offset.x;
            int y = startY + offset.y;

            if (x < 0 || x >= gridManager.width || y < 0 || y >= gridManager.height)
                return false;

            if (type == BlockType.Mystery && y == 0)
                return false;

            if (grid[x, y] != null)
                return false;

            if (type == BlockType.Stone || type == BlockType.Bubble)
                continue;

            Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            foreach (Vector2Int dir in directions)
            {
                int nx = x + dir.x;
                int ny = y + dir.y;
                if (nx < 0 || nx >= gridManager.width || ny < 0 || ny >= gridManager.height)
                    continue;

                Block neighbor = grid[nx, ny];
                if (neighbor != null && neighbor.blockColor == color)
                    return false;
            }
        }
        return true;
    }

    private BlockType GetRandomBlockType(List<Vector2Int> shape)
    {
        if (bossSpawned || shape.Count == 9)
        {
            float value = Random.value;
            float current = 0f;
            current += frozenChance; if (value < current) return BlockType.Frozen;
            current += reinforcedChance; if (value < current) return BlockType.Reinforced;
            current += mysteryChance; if (value < current) return BlockType.Mystery;
            return BlockType.Normal;
        }

        if (Random.value < bossChance) return BlockType.Boss;

        float normalValue = Random.value;
        float normalCurrent = 0f;
        normalCurrent += frozenChance; if (normalValue < normalCurrent) return BlockType.Frozen;
        normalCurrent += reinforcedChance; if (normalValue < normalCurrent) return BlockType.Reinforced;
        normalCurrent += mysteryChance; if (normalValue < normalCurrent) return BlockType.Mystery;
        return BlockType.Normal;
    }

    public void SpawnBlock(int x, int y, BlockColorData color, BlockGroup group)
    {
        Vector3 position = gridManager.GetBlockPosition(x, y);
        Block block = Object.Instantiate(gridManager.blockPrefab, position, Quaternion.identity, gridManager.transform);

        block.gridX = x;
        block.gridY = y;
        block.SetColor(color);

        BlockType type = group != null ? group.groupType : BlockType.Normal;
        block.SetBlockType(type);

        Block[,] grid = gridManager.GetGrid();
        grid[x, y] = block;
        group.Add(block);
    }

    public void FillSmallGaps()
    {
    //    Block[,] grid = gridManager.GetGrid();
    //    List<Vector2Int> emptyCells = new List<Vector2Int>();

    //    // Thu thập các ô trống không nằm ở tầng 1 (y == 0)
    //    for (int y = 1; y < gridManager.height; y++)
    //    {
    //        for (int x = 0; x < gridManager.width; x++)
    //        {
    //            if (grid[x, y] == null)
    //            {
    //                emptyCells.Add(new Vector2Int(x, y));
    //            }
    //        }
    //    }

    //    HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

    //    foreach (Vector2Int cell in emptyCells)
    //    {
    //        if (visited.Contains(cell)) continue;

    //        // Tìm cụm các ô trống liên thông để gom thành group đá hoặc bubble lớn
    //        List<Vector2Int> connectedEmpties = new List<Vector2Int>();
    //        Queue<Vector2Int> q = new Queue<Vector2Int>();

    //        q.Enqueue(cell);
    //        visited.Add(cell);

    //        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    //        while (q.Count > 0)
    //        {
    //            Vector2Int curr = q.Dequeue();
    //            connectedEmpties.Add(curr);

    //            foreach (var d in dirs)
    //            {
    //                Vector2Int next = curr + d;
    //                if (gridManager.IsValidCell(next) && next.y > 0 && grid[next.x, next.y] == null && !visited.Contains(next))
    //                {
    //                    visited.Add(next);
    //                    q.Enqueue(next);
    //                }
    //            }
    //        }

    //        // Tỉ lệ số lượng đá = 3/2 bubble (đá chiếm 60%, bubble chiếm 40%)
    //        if (connectedEmpties.Count >= 2 && Random.value < 0.6f)
    //        {
    //            SpawnStoneGroup(connectedEmpties);
    //        }
    //        else
    //        {
    //            foreach (var singleCell in connectedEmpties)
    //            {
    //                SpawnBubbleGroup(new List<Vector2Int> { singleCell });
    //            }
    //        }
    //    }
    //}

    //public void SpawnStoneGroup(List<Vector2Int> cells)
    //{
    //    if (cells == null || cells.Count == 0) return;

    //    BlockGroup group = new BlockGroup();
    //    group.groupColor = null;
    //    group.SetType(BlockType.Stone);
    //    gridManager.Groups.Add(group);

    //    foreach (Vector2Int cell in cells)
    //    {
    //        if (gridManager.GetGrid()[cell.x, cell.y] != null) return;
    //    }

    //    foreach (Vector2Int cell in cells)
    //    {
    //        SpawnBlock(cell.x, cell.y, null, group);
    //    }

    //    group.InitializedType();
    //    group.UpdateAllTexts();
    }

    private void SpawnBubbleGroup(List<Vector2Int> cells)
    {
        if (cells == null || cells.Count != 1) return;

        Vector2Int cell = cells[0];
        if (gridManager.GetGrid()[cell.x, cell.y] != null) return;

        BlockGroup group = new BlockGroup();
        group.groupColor = null;
        group.SetType(BlockType.Bubble);
        gridManager.Groups.Add(group);

        SpawnBlock(cell.x, cell.y, null, group);
        group.InitializedType();
        group.UpdateAllTexts();
    }

    public void SpawnGroup(List<Vector2Int> shape, Vector2Int start, BlockColorData color, BlockType type)
    {
        BlockGroup group = new BlockGroup();
        group.groupColor = color;
        group.SetType(type);
        gridManager.Groups.Add(group);

        foreach (Vector2Int offset in shape)
        {
            int x = start.x + offset.x;
            int y = start.y + offset.y;
            SpawnBlock(x, y, color, group);
        }

        group.InitializedType();

        if (type == BlockType.Boss)
        {
            group.NormalizeBossHP();
        }

        group.UpdateAllTexts();
    }
}