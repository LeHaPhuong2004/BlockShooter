using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

public class GridCollapse
{
    private GridManager gridManager;
    private readonly List<Sequence> activeSequences = new List<Sequence>();
    private bool disposed;
    private bool recoveryPending;
    private Coroutine recoveryCoroutine;

    private bool IsReady => !disposed && gridManager != null && !gridManager.IsExiting &&
        gridManager.IsCurrentCollapse(this) && gridManager.GetGrid() != null;

    public GridCollapse(GridManager gridManager)
    {
        this.gridManager = gridManager;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        recoveryPending = false;
        if (recoveryCoroutine != null && gridManager != null)
            gridManager.StopCoroutine(recoveryCoroutine);
        recoveryCoroutine = null;
        // Kill by the root owner rather than relying on nested tween handles.
        // The target also covers roots that have outlived a tracking callback.
        if (gridManager != null) DOTween.Kill(gridManager, false);
        activeSequences.Clear();
        if (gridManager != null) gridManager.isProcessingCollapse = false;
    }

    private Sequence CreateRootSequence()
    {
        Sequence sequence = DOTween.Sequence();
        sequence.SetTarget(gridManager).SetLink(gridManager.gameObject, LinkBehaviour.KillOnDestroy);
        activeSequences.Add(sequence);
        sequence.OnKill(() => activeSequences.Remove(sequence));
        return sequence;
    }

    private void SetRootCompletion(Sequence sequence, TweenCallback continuation)
    {
        bool completedNormally = false;
        float duration = sequence.Duration(false);
        sequence.OnComplete(() =>
        {
            completedNormally = true;
            activeSequences.Remove(sequence);
            if (IsReady) continuation();
        });
        sequence.OnKill(() =>
        {
            activeSequences.Remove(sequence);
            if (!completedNormally && duration > 0f && activeSequences.Count == 0 && IsReady)
                QueueCancelledMovementRecovery();
        });
    }

    private void QueueCancelledMovementRecovery()
    {
        if (recoveryPending || !IsReady) return;
        recoveryPending = true;
        // Keep the board busy until its logical positions and visible positions agree.
        gridManager.isProcessingCollapse = true;
        if (Application.isPlaying && gridManager.isActiveAndEnabled)
            recoveryCoroutine = gridManager.StartCoroutine(RecoverAfterCancellation());
    }

    private IEnumerator RecoverAfterCancellation()
    {
        yield return null;
        RecoverCancelledMovement();
    }

    private void RecoverCancelledMovement()
    {
        recoveryCoroutine = null;
        if (!recoveryPending) return;
        recoveryPending = false;
        if (!IsReady || activeSequences.Count != 0) return;

        SnapAllBlocksToGrid();
        gridManager.isProcessingCollapse = false;
        gridManager.CheckSpecialGroupsAtBottom();
        DropFloatingClusters();
        gridManager.CheckLevelComplete();
    }

    public void CheckAllRows()
    {
        if (!IsReady) return;
        if (gridManager.isProcessingExplosion || gridManager.isProcessingCollapse) return;

        Block[,] grid = gridManager.GetGrid();

        for (int y = 0; y < gridManager.height; y++)
        {
            bool empty = true;

            for (int x = 0; x < gridManager.width; x++)
            {
                if (grid[x, y] != null)
                {
                    empty = false;
                    break;
                }
            }

            if (empty)
            {
                CollapseRows(y);
                return;
            }
        }

        DropFloatingClusters();
    }

    public void CollapseRows(int removedY)
    {
        if (!IsReady) return;
        if (gridManager.isProcessingExplosion || gridManager.isProcessingCollapse) return;

        if (removedY < 0 || removedY >= gridManager.height) return;

        Block[,] grid = gridManager.GetGrid();
        int emptyRows = 0;

        for (int y = removedY; y < gridManager.height; y++)
        {
            bool empty = true;

            for (int x = 0; x < gridManager.width; x++)
            {
                if (grid[x, y] != null)
                {
                    empty = false;
                    break;
                }
            }

            if (!empty) break;

            emptyRows++;
        }

        if (emptyRows <= 0)
        {
            gridManager.CheckSpecialGroupsAtBottom();
            DropFloatingClusters();
            return;
        }

        List<BlockGroup> groupsToCollapse = new List<BlockGroup>();

        foreach (BlockGroup group in gridManager.Groups)
        {
            if (group == null || group.blocks == null || group.blocks.Count == 0) continue;

            bool above = false;

            foreach (Block block in group.blocks)
            {
                if (block != null && block.gridY >= removedY + emptyRows)
                {
                    above = true;
                    break;
                }
            }

            if (above) groupsToCollapse.Add(group);
        }

        if (groupsToCollapse.Count == 0)
        {
            gridManager.CheckSpecialGroupsAtBottom();
            DropFloatingClusters();
            return;
        }

        gridManager.isProcessingCollapse = true;

        foreach (BlockGroup group in groupsToCollapse)
        {
            foreach (Block block in group.blocks)
            {
                if (block == null) continue;

                int x = block.gridX;
                int y = block.gridY;

                if (x >= 0 && x < gridManager.width && y >= 0 && y < gridManager.height && grid[x, y] == block) grid[x, y] = null;
            }
        }

        Sequence sequence = CreateRootSequence();

        foreach (BlockGroup group in groupsToCollapse)
        {
            foreach (Block block in group.blocks)
            {
                if (block == null) continue;

                int x = block.gridX;
                int oldY = block.gridY;
                int newY = oldY - emptyRows;

                if (x < 0 || x >= gridManager.width || newY < 0 || newY >= gridManager.height) continue;
                block.gridX = x;
                block.gridY = newY;
                grid[x, newY] = block;
                block.transform.DOKill();

                // Đã tăng tốc từ 0.3f lên 0.15f
                Tween moveTween = block.transform.DOMove(gridManager.GetBlockPosition(x, newY), 0.15f).SetEase(Ease.OutQuad);

                sequence.Join(moveTween);
            }
        }

        SetRootCompletion(sequence, () => {
            if (!IsReady) return;
            gridManager.isProcessingCollapse = false;
            SnapAllBlocksToGrid();
            gridManager.CheckSpecialGroupsAtBottom();
            DropFloatingClusters();
            gridManager.CheckLevelComplete();
        });
    }

    private void SnapAllBlocksToGrid()
    {
        if (!IsReady) return;
        Block[,] grid = gridManager.GetGrid();

        foreach (BlockGroup group in gridManager.Groups)
        {
            if (group == null) continue;

            foreach (Block block in group.blocks)
            {
                if (block == null) continue;

                int x = block.gridX;
                int y = block.gridY;

                if (x < 0 || x >= gridManager.width || y < 0 || y >= gridManager.height) continue;

                grid[x, y] = block;
                block.transform.position = gridManager.GetBlockPosition(x, y);
            }
        }

        foreach (Block block in GetStandaloneBlocks())
        {
            if (block == null) continue;
            int x = block.gridX;
            int y = block.gridY;
            if (x >= 0 && x < gridManager.width && y >= 0 && y < gridManager.height)
            {
                grid[x, y] = block;
                block.transform.position = gridManager.GetBlockPosition(x, y);
            }
        }
    }

    private List<Block> GetStandaloneBlocks()
    {
        List<Block> result = new List<Block>();
        if (!IsReady) return result;
        Block[,] grid = gridManager.GetGrid();

        for (int x = 0; x < gridManager.width; x++)
        {
            for (int y = 0; y < gridManager.height; y++)
            {
                Block block = grid[x, y];

                if (block == null)
                    continue;

                if (block.group == null)
                    result.Add(block);
            }
        }

        return result;
    }

    private Tween DropGroup(BlockGroup group, int dropDistance)
    {
        if (!IsReady || group == null || group.blocks == null || group.blocks.Count == 0)
            return null;

        Block[,] grid = gridManager.GetGrid();
        Sequence sequence = DOTween.Sequence();

        foreach (Block block in group.blocks)
        {
            if (block == null)
                continue;

            int x = block.gridX;
            int y = block.gridY;

            if (x >= 0 && x < gridManager.width &&
                y >= 0 && y < gridManager.height &&
                grid[x, y] == block)
            {
                grid[x, y] = null;
            }
        }

        foreach (Block block in group.blocks)
        {
            if (block == null)
                continue;

            int x = block.gridX;
            int oldY = block.gridY;
            int newY = oldY - dropDistance;

            if (x < 0 || x >= gridManager.width || newY < 0 || newY >= gridManager.height)
                continue;

            block.gridY = newY;
            grid[x, newY] = block;

            block.transform.DOKill();

            // Đã tăng tốc từ 0.3f lên 0.15f
            sequence.Join(
                block.transform
                    .DOMove(gridManager.GetBlockPosition(x, newY), 0.2f)
                    .SetEase(Ease.OutQuad)
            );
        }

        return sequence;
    }

    public void DropFloatingClusters()
    {
        if (!IsReady) return;
        if (gridManager.isProcessingExplosion || gridManager.isProcessingCollapse)
            return;

        List<BlockGroup> groupsToDrop = new List<BlockGroup>();

        foreach (BlockGroup group in gridManager.Groups)
        {
            if (group == null || group.blocks == null || group.blocks.Count == 0)
                continue;

            int dropDistance = CalculateGroupDropDistance(group);
            if (dropDistance > 0)
            {
                groupsToDrop.Add(group);
            }
        }

        List<Block> standaloneBlocks = GetStandaloneBlocks();
        List<Block> blocksToDrop = new List<Block>();

        foreach (Block block in standaloneBlocks)
        {
            if (block == null) continue;
            int dropDist = CalculateStandaloneDropDistance(block);
            if (dropDist > 0)
            {
                blocksToDrop.Add(block);
            }
        }

        if (groupsToDrop.Count == 0 && blocksToDrop.Count == 0)
        {
            gridManager.CheckSpecialGroupsAtBottom();
            gridManager.CheckLevelComplete();
            return;
        }

        gridManager.isProcessingCollapse = true;

        Sequence sequence = CreateRootSequence();
        bool hasMovement = false;

        foreach (BlockGroup group in groupsToDrop)
        {
            int dropDistance = CalculateGroupDropDistance(group);
            if (dropDistance <= 0) continue;

            hasMovement = true;
            Tween tween = DropGroup(group, dropDistance);
            if (tween != null)
                sequence.Join(tween);
        }

        Block[,] grid = gridManager.GetGrid();
        foreach (Block block in blocksToDrop)
        {
            int dropDistance = CalculateStandaloneDropDistance(block);
            if (dropDistance <= 0) continue;

            hasMovement = true;
            int x = block.gridX;
            int oldY = block.gridY;
            int newY = oldY - dropDistance;

            if (grid[x, oldY] == block) grid[x, oldY] = null;
            block.gridY = newY;
            grid[x, newY] = block;

            block.transform.DOKill();
            
            // Đã tăng tốc từ 0.3f lên 0.15f
            Tween tween = block.transform.DOMove(gridManager.GetBlockPosition(x, newY), 0.15f).SetEase(Ease.OutQuad);
            sequence.Join(tween);
        }

        if (!hasMovement)
        {
            sequence.Kill(false);
            gridManager.isProcessingCollapse = false;
            gridManager.CheckSpecialGroupsAtBottom();
            gridManager.CheckLevelComplete();
            return;
        }

        SetRootCompletion(sequence, () =>
        {
            if (!IsReady) return;
            SnapAllBlocksToGrid();
            gridManager.isProcessingCollapse = false;
            gridManager.CheckSpecialGroupsAtBottom();
            DropFloatingClusters(); 
            gridManager.CheckLevelComplete();
        });
    }

    private int CalculateGroupDropDistance(BlockGroup group)
    {
        if (!IsReady || group == null || group.blocks == null || group.blocks.Count == 0)
            return 0;

        Block[,] grid = gridManager.GetGrid();
        bool hasLiveBlock = false;
        foreach (Block block in group.blocks)
        {
            if (block == null) continue;
            if (block.gridX < 0 || block.gridX >= grid.GetLength(0) ||
                block.gridY < 0 || block.gridY >= grid.GetLength(1)) return 0;
            hasLiveBlock = true;
        }
        if (!hasLiveBlock) return 0;

        int distance = 0;
        while (distance < grid.GetLength(1))
        {
            int nextDistance = distance + 1;

            foreach (Block block in group.blocks)
            {
                if (block == null)
                    continue;

                int x = block.gridX;
                int y = block.gridY;
                int newY = y - nextDistance;

                if (newY < 0)
                    return distance;

                Block target = grid[x, newY];

                if (target == null)
                    continue;

                if (target.group == group)
                    continue;

                return distance;
            }

            distance = nextDistance;
        }
        return distance;
    }

    private int CalculateStandaloneDropDistance(Block block)
    {
        if (!IsReady || block == null) return 0;

        Block[,] grid = gridManager.GetGrid();
        int distance = 0;
        int x = block.gridX;
        if (x < 0 || x >= grid.GetLength(0) || block.gridY < 0 || block.gridY >= grid.GetLength(1)) return 0;

        while (distance < grid.GetLength(1))
        {
            int nextDistance = distance + 1;
            int newY = block.gridY - nextDistance;

            if (newY < 0)
                return distance;

            Block target = grid[x, newY];

            if (target != null && target != block)
                return distance;

            distance = nextDistance;
        }
        return distance;
    }
}
