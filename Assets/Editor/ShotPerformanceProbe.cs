#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Measures a repeatable five-gun query and impact burst in the real Game player loop.</summary>
[InitializeOnLoad]
public static class ShotPerformanceProbe
{
    const string Request = "Temp/ShotPerformanceProbe.request";
    static bool busy;
    static ShotPerformanceProbe() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (busy || !File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || !EditorApplication.isPlaying) return;
        var grid = GridManager.Instance;
        if (grid == null || grid.IsExiting || grid.gameObject.scene.name != "Game" || grid.grid == null || GamePauseUI.Instance == null) return;
        busy = true;
        string mode = File.ReadAllText(Request).Trim();
        try { Run(grid, "Temp/ShotPerformance." + mode + ".result"); }
        catch (Exception e) { File.WriteAllText("Temp/ShotPerformance." + mode + ".result", "FAIL\n" + e); }
        finally { File.Delete(Request); busy = false; }
    }

    static void Run(GridManager grid, string result)
    {
        var report = new StringBuilder("Unity Editor Play microbenchmark; CPU work and managed allocations, not device FPS.\n");
        report.AppendLine("The Mono per-thread allocation counter may return zero; use CPU duration/object counts for this comparison.");
        report.AppendLine("Level=" + LevelManager.Instance.CurrentLevel + " board=" + grid.width + "x" + grid.height);
        var root = new GameObject("Temporary Shot Performance Fixture");
        SceneManager.MoveGameObjectToScene(root, grid.gameObject.scene);
        var oldSplashes = new HashSet<int>(UnityEngine.Object.FindObjectsByType<WaterSplashEffect>(FindObjectsInactive.Include).Select(s => s.GetInstanceID()));
        try
        {
            var sourceGun = UnityEngine.Object.FindObjectsByType<Gun>(FindObjectsInactive.Exclude).First(g => g.gameObject.scene == grid.gameObject.scene && g.bulletPrefab != null && g.firePoint != null);
            var color = AssetDatabase.LoadAssetAtPath<BlockColorData>("Assets/Resources/BlockColorData/Data/White.asset");
            Require(BoardViewport.TryGetBounds(grid, out Bounds bounds), "Game board viewport unavailable.");
            var guns = new Gun[5];
            for (int i = 0; i < guns.Length; i++)
            {
                var gun = UnityEngine.Object.Instantiate(sourceGun, root.transform);
                gun.enabled = false;
                gun.inGrid = false; gun.lockedInTray = true; gun.canShoot = false; gun.currentHP = 500; gun.currentColor = color;
                gun.transform.position = new Vector3(Mathf.Lerp(bounds.min.x, bounds.max.x, (i + .5f) / guns.Length), bounds.min.y - .65f);
                guns[i] = gun;
            }
            Physics2D.SyncTransforms();
            int found = 0;
            for (int i = 0; i < 2; i++) foreach (var gun in guns) if (gun.HasPotentialShootTarget()) found++;
            const int rounds = 20;
            GC.Collect();
            long bytes = GC.GetAllocatedBytesForCurrentThread();
            var clock = Stopwatch.StartNew();
            for (int i = 0; i < rounds; i++) foreach (var gun in guns) if (gun.HasPotentialShootTarget()) found++;
            clock.Stop(); bytes = GC.GetAllocatedBytesForCurrentThread() - bytes;
            report.AppendLine("TARGET queries=" + rounds * guns.Length + " elapsedMs=" + clock.Elapsed.TotalMilliseconds.ToString("F3") + " managedBytes=" + bytes + " successfulQueries=" + found);

            var block = grid.GetComponentsInChildren<Block>().First(b => !b.IsDestroyed && b.spriteRenderer != null && BoardViewport.IsBlockVisible(b));
            // Warm resource loading, then measure a synchronous burst separately from fixture/cleanup.
            WaterSplashEffect.Play(block, block.transform.position, Vector2.up, color.color);
            const int impacts = 32;
            bytes = GC.GetAllocatedBytesForCurrentThread(); clock.Restart();
            for (int i = 0; i < impacts; i++) WaterSplashEffect.Play(block, block.transform.position, Vector2.up, color.color);
            clock.Stop(); bytes = GC.GetAllocatedBytesForCurrentThread() - bytes;
            var splashes = UnityEngine.Object.FindObjectsByType<WaterSplashEffect>(FindObjectsInactive.Include).Where(s => !oldSplashes.Contains(s.GetInstanceID())).ToArray();
            int renderers = splashes.Sum(s => s.GetComponentsInChildren<SpriteRenderer>(true).Length);
            report.AppendLine("IMPACTS count=" + impacts + " elapsedMs=" + clock.Elapsed.TotalMilliseconds.ToString("F3") + " managedBytes=" + bytes + " splashObjectsIncludingWarmup=" + splashes.Length + " dropRenderers=" + renderers);
            var totalEffects = UnityEngine.Object.FindObjectsByType<WaterSplashEffect>(FindObjectsInactive.Include).Where(s => s.gameObject.scene == grid.gameObject.scene).ToArray();
            report.AppendLine("SCENE splashInstances=" + totalEffects.Length + " totalDropRenderers=" + totalEffects.Sum(s => s.GetComponentsInChildren<SpriteRenderer>(true).Length));
            report.AppendLine("PASS repeatable native five-gun targeting/impact workload; no ammo, HP, level or save values changed.");
            File.WriteAllText(result, report.ToString());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            foreach (var splash in UnityEngine.Object.FindObjectsByType<WaterSplashEffect>(FindObjectsInactive.Include))
                if (!oldSplashes.Contains(splash.GetInstanceID())) UnityEngine.Object.DestroyImmediate(splash.gameObject);
        }
    }
    static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
#endif
