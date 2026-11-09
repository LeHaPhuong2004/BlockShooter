using UnityEngine;
using System.Collections.Generic;

public class GunColorSelector
{
    private GunGridManager manager;

    public GunColorSelector(GunGridManager manager)
    {
        this.manager = manager;
    }

    public BlockColorData GetRandomColor()
    {
        if (GridManager.Instance == null)return null;

        List<BlockColorData> colors = GetAvailableColors();
        if (colors.Count == 0)return null;

        List<BlockColorData> validColors = new List<BlockColorData>();
        List<float> weights = new List<float>();
        float totalWeight = 0f;

        foreach (BlockColorData color in colors)
        {
            int hp = GridManager.Instance.GetTotalHPByColor(color);
            if (hp <= 0)continue;

            int groupCount = CountGroupsByColor(color);
            if (groupCount <= 0)continue;

            int gunCount = CountGunsByColor(color);
            float lowestY = GetLowestBlockY(color);

            float normalizedY = 1f - lowestY / Mathf.Max(1f, GridManager.Instance.height - 1);
            float heightWeight = 1f + normalizedY * 4f;
            float groupWeight = 1f + groupCount;
            float gunPenalty = 1f + gunCount * 0.8f;
            float weight = Mathf.Max(0.05f, groupWeight * heightWeight / gunPenalty);

            validColors.Add(color);
            weights.Add(weight);
            totalWeight += weight;      
        }

        if (validColors.Count == 0)return null;

        if (validColors.Count == 1)return validColors[0];

        float random = Random.Range(0f, totalWeight);

        for (int i = 0; i < validColors.Count; i++)
        {
            random -= weights[i];

            if (random <= 0f)return validColors[i];
        }

        return validColors[validColors.Count - 1];
    }

    public List<BlockColorData> GetAvailableColors()
    {
        List<BlockColorData> colors = new List<BlockColorData>();

        if (GridManager.Instance == null)return colors;

        GridManager gridManager = GridManager.Instance;
        Block[,] blockGrid = gridManager.GetGrid();

        if (blockGrid == null) return colors;

        for (int y = 0; y < gridManager.height; y++)
        {
            for (int x = 0; x < gridManager.width; x++)
            {
                Block block = blockGrid[x, y];

                if (block == null || block.IsDestroyed || block.hp <= 0 || block.blockColor == null)continue;

                if (!colors.Contains(block.blockColor))
                    colors.Add(block.blockColor);
            }
        }

        return colors;
    }

    public List<BlockColorData> GetPriorityColors()
    {
        List<BlockColorData> colors = new List<BlockColorData>();

        if (GridManager.Instance == null)return colors;

        Block[,] blockGrid = GridManager.Instance.GetGrid();

        if (blockGrid == null)return colors;

        for (int y = 0; y < GridManager.Instance.height; y++)
        {
            for (int x = 0; x < GridManager.Instance.width; x++)
            {
                Block block = blockGrid[x, y];

                if (block == null || block.IsDestroyed || block.hp <= 0 || block.blockColor == null)continue;

                if (!colors.Contains(block.blockColor))colors.Add(block.blockColor);
            }
        }

        return colors;
    }

    public int CountGroupsByColor(BlockColorData targetColor)
    {
        if (GridManager.Instance == null)return 0;

        Block[,] blockGrid = GridManager.Instance.GetGrid();

        if (blockGrid == null) return 0;

        int width = GridManager.Instance.width;
        int height = GridManager.Instance.height;
        bool[,] visited = new bool[width, height];
        int groupCount = 0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Block block = blockGrid[x, y];

                if (block == null || block.IsDestroyed || block.hp <= 0 || block.blockColor != targetColor || visited[x, y])continue;

                groupCount++;
                FloodCountGroup(blockGrid, targetColor, x, y, visited, width, height);
            }
        }

        return groupCount;
    }

    private void FloodCountGroup(Block[,] blockGrid, BlockColorData targetColor, int startX, int startY, bool[,] visited, int width, int height)
    {
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(new Vector2Int(startX, startY));
        visited[startX, startY] = true;

        while (queue.Count > 0)
        {
            Vector2Int pos = queue.Dequeue();

            TryAddGroupBlock(blockGrid, targetColor, pos.x + 1, pos.y, visited, queue, width, height);
            TryAddGroupBlock(blockGrid, targetColor, pos.x - 1, pos.y, visited, queue, width, height);
            TryAddGroupBlock(blockGrid, targetColor, pos.x, pos.y + 1, visited, queue, width, height);
            TryAddGroupBlock(blockGrid, targetColor, pos.x, pos.y - 1, visited, queue, width, height);
        }
    }

    private void TryAddGroupBlock(Block[,] blockGrid, BlockColorData targetColor, int x, int y, bool[,] visited, Queue<Vector2Int> queue, int width, int height)
    {
        if (x < 0 || x >= width || y < 0 || y >= height || visited[x, y]) return;

        Block block = blockGrid[x, y];

        if (block == null || block.IsDestroyed || block.hp <= 0 || block.blockColor != targetColor) return;

        visited[x, y] = true;
        queue.Enqueue(new Vector2Int(x, y));
    }

    public float GetLowestBlockY(BlockColorData targetColor)
    {
        if (GridManager.Instance == null)return 999f;

        Block[,] blockGrid = GridManager.Instance.GetGrid();

        if (blockGrid == null) return 999f;

        int width = GridManager.Instance.width;
        int height = GridManager.Instance.height;
        float lowestY = height - 1;
        bool found = false;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Block block = blockGrid[x, y];

                if (block == null || block.IsDestroyed || block.hp <= 0 || block.blockColor != targetColor)continue;

                lowestY = Mathf.Min(lowestY, y);
                found = true;
            }
        }

        return found ? lowestY : 999f;
    }

    public int CountGunsByColor(BlockColorData color)
    {
        if (color == null) return 0;

        int count = 0;
        Gun[,] grid = manager.Grid;

        for (int y = 0; y < manager.rows; y++)
        {
            for (int x = 0; x < manager.columns; x++)
            {
                Gun gun = grid[x, y];

                if (gun == null || !gun.inGrid) continue;

                if (gun.currentColor == color) count++;
            }
        }

        return count;
    }
}