using DG.Tweening;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI; // Hoặc using TMPro nếu bạn dùng TextMeshPro

public class GridManager : MonoBehaviour
{
    public int width = 8;
    public int height = 5;
    public float cellSize = 1f;
    public float gridOffsetY = 2f;
    public Block blockPrefab;
    public Block dualBlockPrefab;
    public BlockColorData[] colors;
    public int minGroupSize = 4;
    public int maxGroupSize = 4;
    public int maxGroups = 10;

    private bool levelCompleted;
    public Block[,] grid;

    public static GridManager Instance;

    private int normalCount;
    private int frozenCount;
    private int reinforcedCount;
    private int mysteryCount;
    private int bombCount;

    private List<BlockGroup> groups = new List<BlockGroup>();
    public List<BlockGroup> Groups => groups;

    private GroupSpawner groupSpawner;
    private GridCollapse gridCollapse;
    public bool IsExiting { get; private set; }

    internal bool IsCurrentCollapse(GridCollapse collapse) => ReferenceEquals(gridCollapse, collapse);

    public bool IsProcessingExplosion => isProcessingExplosion;
    public bool IsProcessingCollapse => isProcessingCollapse;

    public bool isProcessingExplosion = false;
    public bool isProcessingCollapse = false;

    private bool isProcessingBanhMi = false;

    public Transform gridArea;
    public bool useLevelFile;
    public LevelLoader levelLoader;
    public GameMode gameMode = GameMode.Level;

    [Header("HUD UI References")]
    public TMP_Text levelText;       // Kéo Text hiển thị Level vào đây
    public TMP_Text blockCountText;  // Kéo Text hiển thị số lượng block vào đây (dùng TMP_Text)

    private int totalBlocksInLevel = 0;
    private int destroyedBlocksCount = 0;
    private bool dailyGameRecorded;
    private readonly HashSet<Block> reportedDestroyedBlocks = new HashSet<Block>();

    private void Awake()
    {
        Instance = this;

        DOTween.SetTweensCapacity(500, 125);

        groupSpawner = new GroupSpawner(this);
        gridCollapse = new GridCollapse(this);
    }

    public void PrepareForSceneExit()
    {
        if (IsExiting) return;
        IsExiting = true;
        StopAllCoroutines();
        gridCollapse?.Dispose();
    }

    private void OnEnable()
    {
        Localize.LanguageChanged += RefreshLocalizedLevelLabel;
        RefreshLocalizedLevelLabel();
    }

    private void OnDisable()
    {
        Localize.LanguageChanged -= RefreshLocalizedLevelLabel;
        PrepareForSceneExit();
    }

    private void OnDestroy()
    {
        PrepareForSceneExit();
        if (Instance == this) Instance = null;
    }

    public void SetProcessingCollapse(bool value)
    {
        if (IsExiting) return;
        isProcessingCollapse = value;

        if (!isProcessingCollapse)
        {
            CheckSpecialGroupsAtBottom();
        }
    }

    public void DestroyGroupFromGroup(BlockGroup group)
    {
        DestroyGroup(group);
    }

    private void Start()
    {
        if (!HeartManager.CanPlay)
        {
            if (GamePauseUI.Instance != null && GamePauseUI.Instance.gameObject.scene == gameObject.scene)
                GamePauseUI.Instance.PresentOutOfHearts();
            return;
        }
        if (gameMode == GameMode.Level)
        {
            LevelManager.Instance.LoadCurrentLevel();
            return;
        }

        InitializeGrid(width, height);
        groupSpawner.SpawnGroups();
        groupSpawner.FillSmallGaps();
        InitLevelUI();
    }

    // --- HÀM KHỞI TẠO VÀ CẬP NHẬT UI TRONG GRID MANAGER ---
    public void InitLevelUI()
    {
        RefreshLocalizedLevelLabel();

        CalculateTotalBlocks();
        if (totalBlocksInLevel > 0 && !dailyGameRecorded)
            HeartManager.BeginRound();
        destroyedBlocksCount = 0;
        UpdateBlockCountUI();
    }

    private void RefreshLocalizedLevelLabel()
    {
        if (levelText != null && LevelManager.Instance != null)
            levelText.text = Localize.Format("CẤP ĐỘ {0}", "LEVEL {0}", LevelManager.Instance.CurrentLevel);
    }

    public void RecordDailyGameFinished(bool won = false)
    {
        // Loading, refreshing the HUD or restarting an unfinished level is not a played round.
        if (dailyGameRecorded || totalBlocksInLevel <= 0) return;
        dailyGameRecorded = true;
        HeartManager.FinishRound(won);
        DailyMissionManager.RecordGamePlayed(won);
        if (won) AchievementManager.RecordGameWon();
    }

    private void CalculateTotalBlocks()
    {
        totalBlocksInLevel = 0;
        if (grid == null) return;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y] != null)
                {
                    totalBlocksInLevel++;
                }
            }
        }
    }

    public void ReportBlockDestroyed()
    {
        destroyedBlocksCount++;
        DailyMissionManager.RecordBlockDestroyed();
        AchievementManager.RecordBlockDestroyed();
        UpdateBlockCountUI();
    }

    private void ReportBlockDestroyed(Block block)
    {
        if (block != null && reportedDestroyedBlocks.Add(block))
            ReportBlockDestroyed();
    }

    private void UpdateBlockCountUI()
    {
        if (blockCountText != null)
        {
            blockCountText.text = $"{destroyedBlocksCount}/{totalBlocksInLevel}";
        }
    }
    // -----------------------------------------------------

    public Block[,] GetGrid()
    {
        return grid;
    }

    private void DestroyGroup(BlockGroup group, bool resolveSpecial = true)
    {
        if (group == null)
            return;

        if (!groups.Contains(group))
            return;

        if (group.groupType == BlockType.Stone || group.groupType == BlockType.Bubble)
        {
            if (!CanDestroySpecialGroup(group))
                return;
        }

        List<Block> blocksToDestroy = new List<Block>(group.blocks);

        foreach (Block block in blocksToDestroy)
        {
            if (block == null)
                continue;

            int x = block.gridX;
            int y = block.gridY;

            if (x >= 0 && x < width && y >= 0 && y < height && grid[x, y] == block)
            {
                grid[x, y] = null;
            }

            block.group = null;
            block.transform.DOKill(true);

            Collider2D col = block.GetComponent<Collider2D>();
            if (col != null)
                col.enabled = false;

            BlockShatter.Shatter(block, 1f);

            // Báo cáo block bị phá hủy để trừ/cập nhật UI
            ReportBlockDestroyed(block);

            if (block.gameObject != null)
            {
                block.gameObject.SetActive(false);
                Destroy(block.gameObject);
            }
        }

        group.blocks.Clear();
        groups.Remove(group);

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.groupBreakSound);
        }

        if (resolveSpecial)
        {
            CheckBubblesBeforeCollapse();
            CheckSpecialGroupsAtBottom();
        }

        gridCollapse.CheckAllRows();
        gridCollapse.DropFloatingClusters();
    }

    private void CheckBubblesBeforeCollapse()
    {
        bool destroyed;

        do
        {
            destroyed = false;
            List<BlockGroup> currentGroups = new List<BlockGroup>(groups);

            foreach (BlockGroup group in currentGroups)
            {
                if (group == null || !groups.Contains(group))
                    continue;

                if (group.groupType != BlockType.Bubble)
                    continue;

                if (!CanDestroySpecialGroup(group))
                    continue;

                DestroyGroup(group, false);
                destroyed = true;
            }
        }
        while (destroyed);
    }

    public bool CanDestroySpecialGroup(BlockGroup group)
    {
        if (group == null || group.blocks == null || group.blocks.Count == 0)
            return false;

        if (group.groupType == BlockType.Stone)
        {
            foreach (Block block in group.blocks)
            {
                if (block != null && block.gridY == 0)
                    return true;
            }
            return false;
        }

        if (group.groupType == BlockType.Bubble)
        {
            foreach (Block block in group.blocks)
            {
                if (block == null)
                    continue;

                int x = block.gridX;
                int y = block.gridY;

                if (x > 0 && grid[x - 1, y] != null && grid[x - 1, y].group != group)
                    return false;

                if (x < width - 1 && grid[x + 1, y] != null && grid[x + 1, y].group != group)
                    return false;

                if (y > 0 && grid[x, y - 1] != null && grid[x, y - 1].group != group)
                    return false;
            }
            return true;
        }

        return false;
    }

    public void CheckSpecialGroupsAtBottom()
    {
        if (IsExiting) return;
        CheckBanhMiAtBottom();

        List<BlockGroup> currentGroups = new List<BlockGroup>(groups);

        foreach (BlockGroup group in currentGroups)
        {
            if (group == null || !groups.Contains(group))
                continue;

            if (group.groupType != BlockType.Stone)
                continue;

            if (!CanDestroySpecialGroup(group))
                continue;

            DestroyGroup(group);
            CheckBubblesBeforeCollapse();
        }
    }

    private void CheckBanhMiAtBottom()
    {
        if (isProcessingBanhMi || grid == null)
            return;

        List<Block> banhMiBlocks = new List<Block>();

        for (int x = 0; x < width; x++)
        {
            Block block = grid[x, 0];

            if (block == null || block.blockType != BlockType.BanhMi || block.IsDestroyed)
                continue;

            banhMiBlocks.Add(block);
        }

        if (banhMiBlocks.Count == 0)
            return;

        isProcessingBanhMi = true;

        foreach (Block block in banhMiBlocks)
        {
            CollectBanhMi(block);
        }

        gridCollapse.CheckAllRows();
        gridCollapse.DropFloatingClusters();

        isProcessingBanhMi = false;
    }

    private void CollectBanhMi(Block block)
    {
        if (block == null)
            return;

        ReportBlockDestroyed(block);

        int x = block.gridX;
        int y = block.gridY;

        if (x >= 0 && x < width && y >= 0 && y < height && grid[x, y] == block)
        {
            grid[x, y] = null;
        }

        BlockGroup group = block.group;

        if (group != null)
        {
            group.Remove(block);
            block.group = null;

            if (group.blocks.Count == 0)
            {
                groups.Remove(group);
            }
        }

        block.transform.DOKill(true);

        Collider2D col = block.GetComponent<Collider2D>();
        if (col != null)
            col.enabled = false;

        block.transform.DOScale(Vector3.zero, 0.2f)
            .SetEase(Ease.InBack)
            .OnComplete(() =>
            {
                if (LevelManager.Instance != null)
                {
                    LevelManager.Instance.CollectFood(BlockType.BanhMi);
                }

                if (block != null && block.gameObject != null)
                {
                    Destroy(block.gameObject);
                }
            });
    }

    public void BlockDied(Block block)
    {
        if (block == null || !block.IsDestroyed)
            return;

        BlockGroup group = block.group;

        if (group != null)
        {
            if (group.IsDead() || ((group.groupType == BlockType.Stone || group.groupType == BlockType.Bubble) && CanDestroySpecialGroup(group)))
            {
                DestroyGroup(group);
            }
        }

        gridCollapse.CheckAllRows();
        gridCollapse.DropFloatingClusters();
    }

    private void RemoveDeadGroup(BlockGroup group)
    {
        if (group == null || !groups.Contains(group))
            return;

        if (group.groupType == BlockType.Stone || group.groupType == BlockType.Bubble)
        {
            if (!CanDestroySpecialGroup(group))
                return;
        }
        else if (!group.IsDead())
        {
            return;
        }

        groups.Remove(group);
        List<Block> deadBlocks = new List<Block>(group.blocks);

        foreach (Block block in deadBlocks)
        {
            if (block == null)
                continue;

            ReportBlockDestroyed(block);
            int x = block.gridX;
            int y = block.gridY;

            if (x >= 0 && x < width && y >= 0 && y < height && grid[x, y] == block)
            {
                grid[x, y] = null;
            }

            block.group = null;
            block.transform.DOKill(true);

            if (block.gameObject != null)
            {
                block.gameObject.SetActive(false);
                Destroy(block.gameObject);
            }
        }

        group.blocks.Clear();
        gridCollapse.CheckAllRows();
    }

    public Vector3 GetBlockPosition(int x, int y)
    {
        SpriteRenderer sr = gridArea.GetComponent<SpriteRenderer>();
        Bounds bounds = sr.bounds;

        float gridWidth = width * cellSize;
        float startX = bounds.center.x - gridWidth / 2f + cellSize / 2f;
        float startY = bounds.min.y + cellSize / 2f + 0.25f;

        return new Vector3(startX + x * cellSize, startY + y * cellSize, 0f);
    }

    public void CountBlock(BlockType type)
    {
        switch (type)
        {
            case BlockType.Normal: normalCount++; break;
            case BlockType.Frozen: frozenCount++; break;
            case BlockType.Reinforced: reinforcedCount++; break;
            case BlockType.Mystery: mysteryCount++; break;
        }
    }

    public int GetTotalHPByColor(BlockColorData color)
    {
        int total = 0;
        if (color == null) return 0;
        var countedDualGroups = new HashSet<BlockGroup>();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Block block = grid[x, y];
                if (block == null) continue;

                if (block.blockType == BlockType.Dual)
                {
                    if (block.group != null)
                    {
                        if (countedDualGroups.Add(block.group)) total += block.group.GetDualColorHP(color);
                    }
                    else
                    {
                        if (block.dualTopColor == color) total += block.dualTopHP;
                        if (block.dualBottomColor == color) total += block.dualBottomHP;
                    }
                    continue;
                }

                if (block.blockColor != color) continue;
                total += block.GetInitialRequiredHP();
            }
        }

        return total;
    }

    public void ExplodeBomb(Block bomb)
    {
        if (bomb == null || isProcessingExplosion)
            return;

        isProcessingExplosion = true;
        List<Block> affectedBlocks = new List<Block>();

        int centerX = bomb.gridX;
        int centerY = bomb.gridY;

        for (int x = centerX - 1; x <= centerX + 1; x++)
        {
            for (int y = centerY - 1; y <= centerY + 1; y++)
            {
                if (x < 0 || x >= width || y < 0 || y >= height)
                    continue;

                Block block = grid[x, y];
                if (block == null || block == bomb || block.IsDestroyed)
                    continue;

                if (!affectedBlocks.Contains(block))
                {
                    affectedBlocks.Add(block);
                }
            }
        }

        int bx = bomb.gridX;
        int by = bomb.gridY;

        if (bx >= 0 && bx < width && by >= 0 && by < height && grid[bx, by] == bomb)
        {
            grid[bx, by] = null;
        }

        if (bomb.group != null)
        {
            BlockGroup bombGroup = bomb.group;
            bombGroup.Remove(bomb);

            if (bombGroup.IsDead() || ((bombGroup.groupType == BlockType.Stone || bombGroup.groupType == BlockType.Bubble) && CanDestroySpecialGroup(bombGroup)))
            {
                RemoveDeadGroup(bombGroup);
            }
        }

        ReportBlockDestroyed(bomb);
        bomb.transform.DOKill(true);
        if (bomb.gameObject != null)
        {
            Destroy(bomb.gameObject);
        }

        isProcessingExplosion = false;
        CheckSpecialGroupsAtBottom();
    }

    public bool IsValidCell(Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < width && pos.y >= 0 && pos.y < height;
    }

    public bool IsValidCell(int x, int y)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    public List<Vector3> GetGroupCentersByColor(BlockColorData color)
    {
        List<Vector3> centers = new List<Vector3>();
        if (color == null) return centers;

        foreach (BlockGroup group in groups)
        {
            if (group == null || group.blocks == null || group.blocks.Count == 0)
                continue;

            Vector3 center = Vector3.zero;
            int count = 0;

            foreach (Block block in group.blocks)
            {
                if (block == null || block.IsDestroyed)
                    continue;

                bool match;
                if (block.blockType == BlockType.Dual)
                {
                    match = (block.dualTopColor == color && block.dualTopHP > 0) ||
                            (block.dualBottomColor == color && block.dualBottomHP > 0);
                }
                else
                {
                    match = block.blockColor == color;
                }

                if (!match) continue;

                center += block.transform.position;
                count++;
            }

            if (count > 0)
            {
                center /= count;
                centers.Add(center);
            }
        }

        return centers;
    }

    public void InitializeGrid(int newWidth, int newHeight)
    {
        // Stop old callbacks before destroying their blocks or replacing the grid.
        gridCollapse?.Dispose();
        IsExiting = false;
        gridCollapse = new GridCollapse(this);
        dailyGameRecorded = false;
        totalBlocksInLevel = 0;
        reportedDestroyedBlocks.Clear();
        levelCompleted = false;
        isProcessingExplosion = false;
        isProcessingCollapse = false;
        isProcessingBanhMi = false;

        if (grid != null)
        {
            for (int y = 0; y < grid.GetLength(1); y++)
            {
                for (int x = 0; x < grid.GetLength(0); x++)
                {
                    Block block = grid[x, y];
                    if (block == null) continue;

                    block.transform.DOKill(false);
                    if (block.gameObject != null)
                    {
                        if (Application.isPlaying) Destroy(block.gameObject);
                        else DestroyImmediate(block.gameObject);
                    }
                }
            }
        }

        groups.Clear();

        width = newWidth;
        height = newHeight;

        grid = new Block[width, height];
    }

    public void CheckLevelComplete()
    {
        if (IsExiting) return;
        if (levelCompleted)
            return;

        if (gameMode != GameMode.Level)
            return;

        if (isProcessingCollapse)
            return;

        if (groups.Count > 0)
            return;

        levelCompleted = true;

        RecordDailyGameFinished(true);
        LevelManager.Instance.CompleteLevel();

        if (NavigationManager.Instance != null)
        {
            NavigationManager.Instance.ShowWinPanel();
        }
    }
}
