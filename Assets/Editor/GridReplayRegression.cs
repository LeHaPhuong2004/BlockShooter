#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using DG.Tweening;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

/// <summary>Checks bounded collapse and cancellation before replacing or unloading a board.</summary>
[InitializeOnLoad]
public static class GridReplayRegression
{
    private const string Request = "Temp/GridReplayRegression.request";
    private const string Result = "Temp/GridReplayRegression.result";
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static bool running;
    static GridReplayRegression() { EditorApplication.update += Poll; }
    private static void Poll()
    {
        if (running || !File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        running = true;
        try { Run(); }
        catch (Exception exception) { Debug.LogWarning("Grid replay regression failed; see " + Result + ": " + exception.Message); }
        finally { File.Delete(Request); running = false; }
    }

    [MenuItem("Tools/Diagnostics/Check Grid Replay Cleanup")]
    public static void Run()
    {
        Scene fixture = default;
        GridManager previousGrid = GridManager.Instance;
        Sprite areaSprite = null;
        var report = new StringBuilder();
        try
        {
            fixture = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Replay Grid Fixture");
            SceneManager.MoveGameObjectToScene(root, fixture);
            GridManager grid = root.AddComponent<GridManager>();
            grid.gameMode = GameMode.Level;
            var area = new GameObject("Grid Area");
            area.transform.SetParent(root.transform, false);
            areaSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f, 1);
            area.AddComponent<SpriteRenderer>().sprite = areaSprite;
            grid.gridArea = area.transform;
            grid.InitializeGrid(2, 5);
            GridCollapse collapse = Scheduler(grid);

            var stale = new BlockGroup();
            Block destroyed = Spawn(grid, 0, 4);
            stale.blocks.Add(destroyed);
            UnityEngine.Object.DestroyImmediate(destroyed.gameObject);
            grid.Groups.Add(stale);
            Stopwatch timer = Stopwatch.StartNew();
            Require(Distance(collapse, stale) == 0, "A group containing only destroyed blocks must not drop.");
            Require(timer.ElapsedMilliseconds < 500, "Destroyed-group scan must return immediately.");
            collapse.DropFloatingClusters();
            Require(!grid.isProcessingCollapse, "A destroyed-only group must not schedule movement.");
            report.AppendLine("PASS: a nonempty group whose members are all destroyed returns zero without an unbounded loop.");
            grid.Groups.Clear();

            Block block = Spawn(grid, 0, 4);
            var group = new BlockGroup { totalHP = 1 };
            group.blocks.Add(block); block.group = group; grid.Groups.Add(group);
            grid.grid[0, 4] = block;
            Require(Distance(collapse, group) == 4, "A valid group must retain its normal drop distance.");
            block.gridX = 99;
            Require(Distance(collapse, group) == 0, "Stale grid coordinates must not read outside a replacement grid.");
            block.gridX = 0;

            collapse.DropFloatingClusters();
            Sequence active = Active(collapse);
            int completed = 0;
            TweenCallback original = active.onComplete;
            active.OnComplete(() => { completed++; original?.Invoke(); });
            Require(grid.isProcessingCollapse && active.IsActive(), "Fixture did not start a real nested collapse sequence.");
            grid.InitializeGrid(2, 5);
            report.AppendLine("Replacement state: oldActive=" + active.IsActive() + " completed=" + completed +
                " processing=" + grid.isProcessingCollapse + " oldDisposed=" +
                typeof(GridCollapse).GetField("disposed", Private).GetValue(collapse) +
                " newScheduler=" + !ReferenceEquals(collapse, Scheduler(grid)));
            bool activeAfterReplacement = active.IsActive();
            report.AppendLine("Old tween details: " + DescribeTween(active) + " ownerTweening=" + DOTween.IsTweening(grid));
            if (activeAfterReplacement)
            {
                int ownerKilled = DOTween.Kill(grid, false);
                report.AppendLine("Owner-kill probe: killed=" + ownerKilled + " oldActive=" + active.IsActive() +
                    " details=" + DescribeTween(active) + " ownerTweening=" + DOTween.IsTweening(grid));
            }
            Require(!activeAfterReplacement && completed == 0 && !grid.isProcessingCollapse,
                "Replacing the grid must cancel movement without completing old callbacks.");
            Require(!DOTween.IsTweening(grid), "The replaced grid still owns active root sequences.");
            Require(!ReferenceEquals(collapse, Scheduler(grid)), "Grid replacement must install a fresh scheduler.");
            report.AppendLine("PASS: replacing a board cancels its actual nested movement sequence without invoking its completion callback.");

            Block interrupted = Spawn(grid, 0, 4);
            var interruptedGroup = new BlockGroup { totalHP = 1 };
            interruptedGroup.blocks.Add(interrupted); interrupted.group = interruptedGroup;
            grid.Groups.Add(interruptedGroup); grid.grid[0, 4] = interrupted;
            GridCollapse interruptedScheduler = Scheduler(grid);
            interruptedScheduler.DropFloatingClusters();
            Sequence interruptedMovement = Active(interruptedScheduler);
            int interruptedCompleted = 0;
            TweenCallback interruptedOriginal = interruptedMovement.onComplete;
            interruptedMovement.OnComplete(() => { interruptedCompleted++; interruptedOriginal?.Invoke(); });
            DOTween.Goto(grid, 0.05f, false);
            Vector3 settledPosition = grid.GetBlockPosition(interrupted.gridX, interrupted.gridY);
            Require((interrupted.transform.position - settledPosition).sqrMagnitude > 0.0001f,
                "The interrupted fixture must still be between its original and logical positions.");
            DOTween.Kill(grid, false);
            Require(!interruptedMovement.IsActive() && interruptedCompleted == 0 &&
                grid.isProcessingCollapse && RecoveryPending(interruptedScheduler),
                "Unexpected cancellation must keep collapse busy until its deferred recovery runs.");
            Recover(interruptedScheduler);
            Require(!grid.isProcessingCollapse && !RecoveryPending(interruptedScheduler) &&
                interruptedCompleted == 0 && !DOTween.IsTweening(grid) &&
                (interrupted.transform.position - settledPosition).sqrMagnitude < 0.0001f,
                "Cancelled movement must snap to the logical grid and resume checks without completing stale callbacks.");
            Recover(interruptedScheduler);
            Require(interruptedCompleted == 0 && !DOTween.IsTweening(grid),
                "Deferred recovery must run at most once.");
            report.AppendLine("PASS: unexpected midflight cancellation remains busy until recovery, snaps blocks, resumes checks once, and suppresses stale completion callbacks.");

            grid.InitializeGrid(2, 5);
            Block finishing = Spawn(grid, 0, 4);
            var finishingGroup = new BlockGroup { totalHP = 1 };
            finishingGroup.blocks.Add(finishing); finishing.group = finishingGroup;
            grid.Groups.Add(finishingGroup); grid.grid[0, 4] = finishing;
            GridCollapse finishingScheduler = Scheduler(grid);
            finishingScheduler.DropFloatingClusters();
            DOTween.Complete(grid, true);
            Require(!grid.isProcessingCollapse && !RecoveryPending(finishingScheduler) && !DOTween.IsTweening(grid),
                "Normal completion and auto-kill must not queue cancellation recovery.");
            report.AppendLine("PASS: normal completion and auto-kill do not schedule cancellation recovery.");

            grid.InitializeGrid(2, 5);
            Block staleRecoveryBlock = Spawn(grid, 0, 4);
            var staleRecoveryGroup = new BlockGroup { totalHP = 1 };
            staleRecoveryGroup.blocks.Add(staleRecoveryBlock); staleRecoveryBlock.group = staleRecoveryGroup;
            grid.Groups.Add(staleRecoveryGroup); grid.grid[0, 4] = staleRecoveryBlock;
            GridCollapse staleRecoveryScheduler = Scheduler(grid);
            staleRecoveryScheduler.DropFloatingClusters();
            DOTween.Kill(grid, false);
            Require(RecoveryPending(staleRecoveryScheduler), "Fixture must queue recovery before replacement.");
            grid.InitializeGrid(2, 5);
            Recover(staleRecoveryScheduler);
            Require(!RecoveryPending(staleRecoveryScheduler) && !grid.isProcessingCollapse &&
                !DOTween.IsTweening(grid) && Scheduler(grid) != staleRecoveryScheduler,
                "Replacing a board must discard recovery queued by its old scheduler.");
            report.AppendLine("PASS: a replacement board rejects its old scheduler's pending recovery.");

            Block fresh = Spawn(grid, 0, 4);
            var freshGroup = new BlockGroup { totalHP = 1 };
            freshGroup.blocks.Add(fresh); fresh.group = freshGroup; grid.Groups.Add(freshGroup); grid.grid[0, 4] = fresh;
            GridCollapse next = Scheduler(grid);
            next.DropFloatingClusters();
            Sequence exitMovement = Active(next);
            int exitCompleted = 0;
            TweenCallback exitOriginal = exitMovement.onComplete;
            exitMovement.OnComplete(() => { exitCompleted++; exitOriginal?.Invoke(); });
            grid.PrepareForSceneExit();
            grid.PrepareForSceneExit();
            Require(grid.IsExiting && !exitMovement.IsActive() && exitCompleted == 0 && !DOTween.IsTweening(grid),
                "Scene exit must cancel sequences once without running completion callbacks.");
            next.DropFloatingClusters(); next.CheckAllRows(); next.CollapseRows(0);
            Require(!DOTween.IsTweening(grid), "A disposed collapse scheduler must not restart during scene teardown.");
            grid.InitializeGrid(2, 5);
            Require(!grid.IsExiting && Scheduler(grid) != next, "A subsequent board must be usable after cleanup.");
            report.AppendLine("PASS: scene exit is idempotent, suppresses stale callbacks, and the next board receives a usable scheduler.");
            report.AppendLine("No Scene, prefab, player data or game progress was saved; Play mode was not entered.");
            File.WriteAllText(Result, report.ToString());
        }
        catch (Exception exception) { File.WriteAllText(Result, report + "\nFAIL\n" + exception); throw; }
        finally
        {
            if (fixture.IsValid()) EditorSceneManager.ClosePreviewScene(fixture);
            if (areaSprite != null) UnityEngine.Object.DestroyImmediate(areaSprite);
            GridManager.Instance = previousGrid;
        }
    }

    private static Block Spawn(GridManager grid, int x, int y)
    {
        var root = new GameObject("Replay Block"); root.transform.SetParent(grid.transform, false);
        Block block = root.AddComponent<Block>(); block.gridX = x; block.gridY = y;
        root.transform.position = grid.GetBlockPosition(x, y); return block;
    }
    private static GridCollapse Scheduler(GridManager grid) => (GridCollapse)typeof(GridManager).GetField("gridCollapse", Private).GetValue(grid);
    private static bool RecoveryPending(GridCollapse collapse) => (bool)typeof(GridCollapse).GetField("recoveryPending", Private).GetValue(collapse);
    private static void Recover(GridCollapse collapse) => typeof(GridCollapse).GetMethod("RecoverCancelledMovement", Private).Invoke(collapse, null);
    private static int Distance(GridCollapse collapse, BlockGroup group) => (int)typeof(GridCollapse).GetMethod("CalculateGroupDropDistance", Private).Invoke(collapse, new object[] { group });
    private static Sequence Active(GridCollapse collapse)
    {
        var sequences = (List<Sequence>)typeof(GridCollapse).GetField("activeSequences", Private).GetValue(collapse);
        Require(sequences.Count == 1, "Expected exactly one owned root collapse sequence."); return sequences[0];
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static string DescribeTween(Tween tween)
    {
        var result = new StringBuilder();
        foreach (string name in new[] { "active", "isSequenced", "isRecyclable", "activeId", "isPlaying", "target" })
        {
            FieldInfo field = typeof(Tween).GetField(name, Private | BindingFlags.Public);
            if (field != null) result.Append(name).Append('=').Append(field.GetValue(tween)).Append(' ');
        }
        return result.ToString();
    }
}
#endif
