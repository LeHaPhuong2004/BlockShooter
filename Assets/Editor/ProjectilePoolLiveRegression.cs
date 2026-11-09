#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>Exercises actual projectile retirement/reuse without consuming ammo or committing damage.</summary>
[InitializeOnLoad]
public static class ProjectilePoolLiveRegression
{
    const string Request = "Temp/ProjectilePoolLiveRegression.request";
    const string Result = "Temp/ProjectilePoolLiveRegression.result";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly StringBuilder report = new StringBuilder();
    static bool busy, running, reservationOwned;
    static int phase, warmupFrame, originalHP, originalPending;
    static double began;
    static float phaseAt;
    static GridManager warmupGrid;
    static Bullet first, second, reused, prefab;
    static DualBulletPair pair;
    static Block block;
    static BlockGroup group;
    static BlockColorData firstColor, secondColor;
    static Dictionary<BlockColorData, int> originalColorHP;
    static Vector2 targetPoint, secondLaunch;
    static int firstId;

    static ProjectilePoolLiveRegression() => EditorApplication.update += Poll;

    static void Poll()
    {
        if (busy || !File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        busy = true;
        try
        {
            if (!EditorApplication.isPlaying)
            {
                if (running && !EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException("Play stopped during projectile pool validation.");
                return;
            }
            var grid = GridManager.Instance;
            if (grid == null || grid.IsExiting || grid.gameObject.scene.name != "Game" || grid.grid == null) return;
            if (!running)
            {
                if (warmupGrid != grid) { warmupGrid = grid; warmupFrame = Time.frameCount; return; }
                if (Time.frameCount - warmupFrame < 5 || Time.timeScale <= 0f) return;
                Begin(grid);
                return;
            }
            Require(EditorApplication.timeSinceStartup - began < 10, "Projectile pool validation exceeded 10 seconds.");
            Require(Time.timeScale > 0f, "Keep Game unpaused while projectile retirement is checked.");
            Step(grid);
        }
        catch (Exception exception)
        {
            report.AppendLine("FAIL " + exception);
            Cleanup();
            File.WriteAllText(Result, report.ToString());
            File.Delete(Request);
            Reset();
        }
        finally { busy = false; }
    }

    static void Begin(GridManager grid)
    {
        running = true;
        began = EditorApplication.timeSinceStartup;
        report.Clear();
        report.AppendLine("Native Play projectile lifecycle check; no ammo, HP or player-save transactions.");
        Require(!HasActive(grid), "Close existing projectile activity before this isolated pool check.");
        block = grid.GetComponentsInChildren<Block>().FirstOrDefault(candidate =>
            candidate.blockType == BlockType.Dual && !candidate.IsDestroyed && candidate.group != null &&
            candidate.group.pendingDamage == 0 && candidate.group.totalHP >= 20 &&
            candidate.group.CanReserveDualPair() && BoardViewport.TryGetTargetPoint(candidate, out _));
        Require(block != null, "A visible dual group with unused paired HP is required.");
        group = block.group;
        originalHP = group.totalHP;
        originalPending = group.pendingDamage;
        originalColorHP = new Dictionary<BlockColorData, int>(Get<Dictionary<BlockColorData, int>>(group, "dualHPByColor"));
        Require(group.TryGetDualColors(out firstColor, out secondColor), "Dual colors are unavailable.");
        Require(BoardViewport.TryGetTargetPoint(block, out targetPoint), "Visible target/viewport missing.");
        Require(BoardViewport.TryGetBounds(grid, out Bounds board), "Visible target/viewport missing.");
        prefab = UnityEngine.Object.FindObjectsByType<Gun>(FindObjectsInactive.Exclude)
            .Where(gun => gun.gameObject.scene == grid.gameObject.scene && gun.bulletPrefab != null)
            .Select(gun => gun.bulletPrefab).FirstOrDefault();
        Require(prefab != null, "The saved Game bullet prefab is unavailable.");
        Require(group.TryReserveDualPair(), "Could not reserve the isolated pair.");
        reservationOwned = true;
        var launch = new Vector2(board.center.x, board.min.y - .6f);
        first = Bullet.Spawn(prefab, launch, Quaternion.identity);
        second = Bullet.Spawn(prefab, launch + Vector2.right * .1f, Quaternion.identity);
        first.Initialize(block, firstColor, targetPoint, new List<Vector2> { launch, targetPoint });
        second.Initialize(block, secondColor, targetPoint, new List<Vector2> { launch + Vector2.right * .1f, targetPoint });
        first.SetFireBullet(true);
        first.speed = second.speed = 0;
        Bullet.PairDualShots(first, second);
        pair = Get<DualBulletPair>(first, "dualPair");
        Require(pair != null && pair == Get<DualBulletPair>(second, "dualPair"), "Shots did not share their pair gate.");
        second.enabled = false;
        firstId = first.GetInstanceID();
        if (first.bulletTrail != null)
        {
            first.bulletTrail.AddPosition(launch);
            first.bulletTrail.AddPosition(targetPoint);
        }
        first.transform.position = targetPoint;
        Invoke(first, "HitTarget", block);
        Require(group.totalHP == originalHP && group.pendingDamage == originalPending + 2,
            "One pair arrival changed HP or released the pair reservation.");
        Require(!first.enabled && first.gameObject.activeSelf && Get<bool>(first, "despawning"), "First shot did not retire into its trail fade.");
        Require(first.bulletSprite == null || !first.bulletSprite.enabled, "Retired projectile sprite remains visible.");
        Require(first.GetComponentsInChildren<Collider2D>(true).All(collider => !collider.enabled), "Retired projectile collider remains enabled.");
        Require(first.bulletTrail == null || first.bulletTrail.transform.IsChildOf(first.transform), "Pooling detached the fading trail.");
        Require(!HasActive(grid) && DualBulletPair.HasPendingShots(), "Activity registries lost the pending dual gate or retained an inactive projectile.");
        report.AppendLine("PASS first arrival retires its sprite/collider, retains a parented fading trail and leaves paired HP untouched.");
        phase = 1;
        phaseAt = Time.time;
        File.WriteAllText(Result, report.ToString());
    }

    static void Step(GridManager grid)
    {
        if (phase == 1)
        {
            if (Time.time - phaseAt < .22f) return;
            Require(first != null && !first.gameObject.activeSelf, "Retired bullet did not become idle after its trail lifetime.");
            secondLaunch = first.LaunchPosition + Vector2.down * .7f;
            reused = Bullet.Spawn(prefab, secondLaunch, Quaternion.identity);
            Require(reused.GetInstanceID() == firstId, "The next shot did not reuse the first idle projectile.");
            // This shot owns no reservation: it is canceled before the next player Update.
            reused.Initialize(block, secondColor, targetPoint, new List<Vector2> { secondLaunch, targetPoint });
            reused.SetFireBullet(false);
            Require(reused.isActiveAndEnabled && !Get<bool>(reused, "hitTarget") && !Get<bool>(reused, "damageResolved") &&
                !Get<bool>(reused, "despawning") && Get<bool>(reused, "initialized") && Get<int>(reused, "pathIndex") == 0 &&
                Get<DualBulletPair>(reused, "dualPair") == null, "Reused projectile retained impact/pair state.");
            Require((reused.LaunchPosition - secondLaunch).sqrMagnitude < .000001f && reused.bulletColor == secondColor,
                "Reused projectile retained its old launch or color.");
            Require(reused.bulletSprite == null || reused.bulletSprite.enabled, "Reused projectile sprite was not restored.");
            Require(reused.fireOverlay == null || !reused.fireOverlay.enabled, "Normal reused shot retained the fire overlay.");
            Require(reused.bulletTrail == null || (reused.bulletTrail.emitting && reused.bulletTrail.positionCount == 0 &&
                reused.bulletTrail.transform.IsChildOf(reused.transform)), "Reused trail retained stale points/emission/parent.");
            var colliders = Get<Collider2D[]>(reused, "colliders");
            var defaults = Get<bool[]>(reused, "colliderEnabled");
            Require(colliders.Length == defaults.Length && Enumerable.Range(0, colliders.Length).All(i => colliders[i].enabled == defaults[i]),
                "Reused projectile did not restore prefab collider states.");
            Require(BoardProjectileClipping.TryGetFlightBounds(reused, grid, out Bounds flight) && flight.min.y < first.LaunchPosition.y,
                "Reused projectile clipping did not extend to its new lower launch.");
            if (reused.bulletSprite != null)
            {
                var properties = new MaterialPropertyBlock();
                reused.bulletSprite.GetPropertyBlock(properties);
                Vector4 actual = properties.GetVector(Shader.PropertyToID("_BoardClipRect"));
                Require(properties.GetFloat(Shader.PropertyToID("_BoardClipEnabled")) > .5f && Mathf.Abs(actual.y - flight.min.y) < .0001f,
                    "Reused projectile has stale sprite clipping properties.");
            }
            pair.Cancel();
            reservationOwned = false;
            Require(reused.isActiveAndEnabled && !Get<bool>(reused, "hitTarget") && !Get<bool>(reused, "despawning"),
                "The old pair canceled the newly reused projectile.");
            Require(group.totalHP == originalHP && group.pendingDamage == originalPending && !DualBulletPair.HasPendingShots(),
                "Pair cancellation changed HP or leaked reservations/gates.");
            report.AppendLine("PASS the same pooled instance resets launch/color/trail/clipping/colliders; cancellation of its old pair leaves the new shot alive.");
            Invoke(reused, "CancelDualShot");
            Require(!HasActive(grid), "Inactive trail fades remain registered as live projectiles.");
            phase = 2;
            phaseAt = Time.time;
            File.WriteAllText(Result, report.ToString());
            return;
        }
        if (phase != 2 || Time.time - phaseAt < .22f) return;
        Require(first != null && second != null && !first.gameObject.activeSelf && !second.gameObject.activeSelf && !HasActive(grid),
            "The finished pair/reused shot did not return to idle storage.");
        Require(group.totalHP == originalHP && group.pendingDamage == originalPending, "The lifecycle check altered board HP/reservations.");
        first.speed = second.speed = prefab.speed;
        report.AppendLine("PASS all shots idle after fade; no active-projectile/pair leak, HP/ammo/progression unchanged.");
        File.WriteAllText(Result, report.ToString());
        File.Delete(Request);
        Reset();
    }

    static void Cleanup()
    {
        if (pair != null) pair.Cancel();
        else if (reservationOwned && group != null)
        {
            group.ReleaseDamage(1, firstColor);
            group.ReleaseDamage(1, secondColor);
        }
        foreach (var bullet in new[] { first, second, reused }.Distinct())
        {
            if (bullet == null) continue;
            if (!Get<bool>(bullet, "despawning")) Invoke(bullet, "CancelDualShot");
            if (prefab != null) bullet.speed = prefab.speed;
        }
        if (group != null && originalColorHP != null && group.totalHP != originalHP)
        {
            var colors = Get<Dictionary<BlockColorData, int>>(group, "dualHPByColor");
            colors.Clear();
            foreach (var entry in originalColorHP) colors.Add(entry.Key, entry.Value);
            group.totalHP = originalHP;
            Invoke(group, "SyncDualBlockHP");
            group.UpdateAllTexts();
        }
    }

    static bool HasActive(GridManager grid) => (bool)typeof(Bullet).GetMethod("HasActive", BindingFlags.NonPublic | BindingFlags.Static)
        .Invoke(null, new object[] { grid.gameObject.scene });
    static T Get<T>(object value, string name) => (T)value.GetType().GetField(name, Private).GetValue(value);
    static void Invoke(object value, string name, params object[] arguments) => value.GetType().GetMethod(name, Private).Invoke(value, arguments);
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    static void Reset()
    {
        running = reservationOwned = false;
        phase = 0;
        warmupGrid = null;
        first = second = reused = prefab = null;
        pair = null;
        block = null;
        group = null;
        originalColorHP = null;
    }
}
#endif
