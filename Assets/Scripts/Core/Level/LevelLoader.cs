
using System;
using System.Collections.Generic;
using UnityEngine;

public class LevelLoader : MonoBehaviour
{
    private GridManager gridManager;

    private void Awake()
    {
        gridManager = GetComponent<GridManager>();
    }

    public void LoadLevel(TextAsset levelFile)
    {
        if (levelFile == null)
        {
            Debug.LogError(
                "LevelLoader: Level file không tồn tại."
            );
            return;
        }

        if (gridManager == null)
        {
            Debug.LogError(
                "LevelLoader: Không tìm thấy GridManager trên GameObject này."
            );
            return;
        }

        Debug.Log(
            $"===== LOAD {levelFile.name} ====="
        );

        string[] lines =
            levelFile.text.Split(
                new[]
                {
                    '\r',
                    '\n'
                },
                StringSplitOptions.RemoveEmptyEntries
            );

        if (lines.Length == 0)
        {
            Debug.LogError(
                $"LevelLoader: {levelFile.name} không có dữ liệu."
            );
            return;
        }

        List<string[]> rows =
            new List<string[]>();

        int width = -1;

        for (int row = 0; row < lines.Length; row++)
        {
            string line =
                lines[row].Trim();

            if (string.IsNullOrEmpty(line))
                continue;

            string[] tokens =
                line.Split(
                    new[]
                    {
                        ' ',
                        '\t'
                    },
                    StringSplitOptions.RemoveEmptyEntries
                );

            if (tokens.Length == 0)
                continue;

            if (width < 0)
            {
                width = tokens.Length;
            }
            else if (tokens.Length != width)
            {
                Debug.LogError(
                    $"LevelLoader: {levelFile.name} - " +
                    $"dòng {row + 1} có {tokens.Length} ô, " +
                    $"cần {width} ô."
                );
                return;
            }

            rows.Add(tokens);
        }

        if (rows.Count == 0 || width <= 0)
        {
            Debug.LogError(
                $"LevelLoader: {levelFile.name} grid rỗng."
            );
            return;
        }

        int height = rows.Count;

        gridManager.InitializeGrid(
            width,
            height
        );

        List<LevelCell> cells =
            new List<LevelCell>();

        Dictionary<BlockType, int> foodCounts =
            new Dictionary<BlockType, int>();

        for (int row = 0; row < height; row++)
        {
            string[] rowData = rows[row];

            for (int col = 0; col < width; col++)
            {
                string token = rowData[col];

                if (string.IsNullOrEmpty(token))
                    token = ".";

                token = token.Trim().ToUpper();

                // Bỏ qua ô trống và Stone.
                if (token == "." || token == "S")
                    continue;

                if (!TryParseToken(
                    token,
                    out int colorIndex,
                    out int secondColorIndex,
                    out BlockType blockType
                ))
                {
                    Debug.LogError(
                        $"Token '{token}' không hợp lệ " +
                        $"ở dòng {row + 1}, cột {col + 1}."
                    );

                    continue;
                }

                if (colorIndex >= 0 &&
                    colorIndex >= gridManager.colors.Length)
                {
                    Debug.LogError(
                        $"Color {colorIndex + 1} không tồn tại."
                    );

                    continue;
                }

                if (secondColorIndex >= 0 &&
                    secondColorIndex >= gridManager.colors.Length)
                {
                    Debug.LogError(
                        $"Color {secondColorIndex + 1} không tồn tại."
                    );

                    continue;
                }

                if (
                    blockType == BlockType.BanhMi ||
                    blockType == BlockType.FriedChicken
                )
                {
                    if (!foodCounts.ContainsKey(blockType))
                    {
                        foodCounts.Add(blockType, 0);
                    }

                    foodCounts[blockType]++;
                }

                int x = col;
                int y = height - 1 - row;

                cells.Add(
                    new LevelCell(
                        x,
                        y,
                        colorIndex,
                        secondColorIndex,
                        blockType
                    )
                );
            }
        }

        SpawnGroups(cells);

        LoadFoodGoals(foodCounts);

        if (GunGridManager.Instance != null)
        {
            GunGridManager.Instance.ReloadGuns();
        }
        else
        {
            Debug.LogError(
                "LevelLoader: GunGridManager.Instance = NULL"
            );
        }

        Debug.Log(
            $"Đã load {levelFile.name} - {width}x{height}"
        );

        foreach (KeyValuePair<BlockType, int> food in foodCounts)
        {
            Debug.Log(
                $"[FOOD] {food.Key} = {food.Value}"
            );
        }

        if (GridManager.Instance != null)
        {
            GridManager.Instance.InitLevelUI();
        }
    }

    private void LoadFoodGoals(
        Dictionary<BlockType, int> foodCounts
    )
    {
        List<LevelManager.FoodGoal> goals =
            new List<LevelManager.FoodGoal>();

        foreach (KeyValuePair<BlockType, int> food in foodCounts)
        {
            if (food.Value <= 0)
                continue;

            LevelManager.FoodGoal goal =
                new LevelManager.FoodGoal();

            goal.type = food.Key;
            goal.required = food.Value;
            goal.collected = 0;
            goal.ui = null;

            goals.Add(goal);
        }

        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.SetFoodGoals(goals);
        }
        else
        {
            Debug.LogError(
                "LevelLoader: LevelManager.Instance = NULL"
            );
        }
    }

    private void SpawnGroups(
        List<LevelCell> cells
    )
    {
        bool[,] visited =
            new bool[
                gridManager.width,
                gridManager.height
            ];

        foreach (LevelCell cell in cells)
        {
            if (visited[cell.x, cell.y])
                continue;

            List<LevelCell> groupCells =
                new List<LevelCell>();

            FloodFill(
                cell,
                cells,
                visited,
                groupCells
            );

            SpawnGroup(groupCells);
        }
    }

    private void FloodFill(
        LevelCell start,
        List<LevelCell> cells,
        bool[,] visited,
        List<LevelCell> result
    )
    {
        Queue<LevelCell> queue =
            new Queue<LevelCell>();

        queue.Enqueue(start);

        visited[start.x, start.y] = true;

        while (queue.Count > 0)
        {
            LevelCell current = queue.Dequeue();

            result.Add(current);

            CheckNeighbor(
                current.x + 1,
                current.y,
                start,
                cells,
                visited,
                queue
            );

            CheckNeighbor(
                current.x - 1,
                current.y,
                start,
                cells,
                visited,
                queue
            );

            CheckNeighbor(
                current.x,
                current.y + 1,
                start,
                cells,
                visited,
                queue
            );

            CheckNeighbor(
                current.x,
                current.y - 1,
                start,
                cells,
                visited,
                queue
            );
        }
    }

    private void CheckNeighbor(
        int x,
        int y,
        LevelCell start,
        List<LevelCell> cells,
        bool[,] visited,
        Queue<LevelCell> queue
    )
    {
        if (
            x < 0 ||
            x >= gridManager.width ||
            y < 0 ||
            y >= gridManager.height
        )
            return;

        if (visited[x, y])
            return;

        LevelCell? cell =
            FindCell(cells, x, y);

        if (!cell.HasValue)
            return;

        LevelCell actual = cell.Value;

        if (actual.blockType != start.blockType)
            return;

        if (actual.colorIndex != start.colorIndex)
            return;

        if (
            actual.blockType == BlockType.Dual &&
            actual.secondColorIndex != start.secondColorIndex
        )
            return;

        visited[x, y] = true;

        queue.Enqueue(actual);
    }

    private LevelCell? FindCell(
        List<LevelCell> cells,
        int x,
        int y
    )
    {
        foreach (LevelCell cell in cells)
        {
            if (cell.x == x && cell.y == y)
                return cell;
        }

        return null;
    }

    private void SpawnGroup(
        List<LevelCell> groupCells
    )
    {
        if (groupCells.Count == 0)
            return;

        LevelCell first = groupCells[0];

        BlockGroup group = new BlockGroup();

        group.SetType(first.blockType);

        if (first.colorIndex >= 0)
        {
            group.groupColor =
                gridManager.colors[first.colorIndex];
        }
        else
        {
            group.groupColor = null;
        }

        gridManager.Groups.Add(group);

        foreach (LevelCell cell in groupCells)
        {
            Block prefab =
                cell.blockType == BlockType.Dual &&
                gridManager.dualBlockPrefab != null
                    ? gridManager.dualBlockPrefab
                    : gridManager.blockPrefab;

            Block block = Instantiate(
                prefab,
                gridManager.GetBlockPosition(
                    cell.x,
                    cell.y
                ),
                Quaternion.identity,
                gridManager.transform
            );

            block.gridX = cell.x;
            block.gridY = cell.y;

            if (cell.blockType == BlockType.Dual)
            {
                block.SetDual(
                    gridManager.colors[cell.colorIndex],
                    gridManager.colors[cell.secondColorIndex]
                );
            }
            else if (cell.colorIndex >= 0)
            {
                block.SetColor(
                    gridManager.colors[cell.colorIndex]
                );

                block.SetBlockType(cell.blockType);
            }
            else
            {
                block.blockColor = null;
                block.hp = 0;
                block.SetBlockType(cell.blockType);
            }

            gridManager.GetGrid()[cell.x, cell.y] = block;

            group.Add(block);
        }

        group.InitializedType();

        if (first.blockType == BlockType.Boss)
        {
            group.NormalizeBossHP();
        }

        group.UpdateTypeVisuals();
    }

    private bool TryParseToken(
        string token,
        out int colorIndex,
        out int secondColorIndex,
        out BlockType blockType
    )
    {
        colorIndex = -1;
        secondColorIndex = -1;

        blockType = BlockType.Normal;

        token = token.Trim().ToUpper();

        if (token.StartsWith("D"))
        {
            string[] parts =
                token.Substring(1).Split('-');

            if (parts.Length != 2)
                return false;

            if (!int.TryParse(parts[0], out int topColor))
                return false;

            if (!int.TryParse(parts[1], out int bottomColor))
                return false;

            topColor--;
            bottomColor--;

            if (topColor < 0 || bottomColor < 0)
                return false;

            if (topColor == bottomColor)
                return false;

            if (
                topColor >= gridManager.colors.Length ||
                bottomColor >= gridManager.colors.Length
            )
                return false;

            colorIndex = topColor;
            secondColorIndex = bottomColor;
            blockType = BlockType.Dual;

            return true;
        }

        if (token == "B")
        {
            blockType = BlockType.Bubble;
            return true;
        }

        if (token == "BM")
        {
            blockType = BlockType.BanhMi;
            return true;
        }

        if (token == "FC")
        {
            blockType = BlockType.FriedChicken;
            return true;
        }

        int i = 0;

        while (
            i < token.Length &&
            char.IsDigit(token[i])
        )
        {
            i++;
        }

        if (i == 0)
            return false;

        string numberPart = token.Substring(0, i);

        if (!int.TryParse(numberPart, out int parsedColor))
            return false;

        colorIndex = parsedColor - 1;

        if (i >= token.Length)
            return true;

        string typePart = token.Substring(i);

        switch (typePart)
        {
            case "F":
                blockType = BlockType.Frozen;
                break;

            case "R":
                blockType = BlockType.Reinforced;
                break;

            case "M":
                blockType = BlockType.Mystery;
                break;

            case "L":
                blockType = BlockType.Locked;
                break;

            case "C":
                blockType = BlockType.Countdown;
                break;

            default:
                return false;
        }

        return true;
    }

    private struct LevelCell
    {
        public int x;
        public int y;
        public int colorIndex;
        public int secondColorIndex;
        public BlockType blockType;

        public LevelCell(
            int x,
            int y,
            int colorIndex,
            int secondColorIndex,
            BlockType blockType
        )
        {
            this.x = x;
            this.y = y;
            this.colorIndex = colorIndex;
            this.secondColorIndex = secondColorIndex;
            this.blockType = blockType;
        }
    }
}
