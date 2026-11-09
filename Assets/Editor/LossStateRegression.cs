#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DG.Tweening;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Exercises full-tray loss decisions without entering Play or changing player data.</summary>
[InitializeOnLoad]
public static class LossStateRegression
{
    private const string Request = "Temp/LossStateRegression.request";
    private const string Result = "Temp/LossStateRegression.result";
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static bool running;
    static LossStateRegression() { EditorApplication.update += Poll; }
    private static void Poll()
    {
        if (running || !File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        running = true;
        try { Run(); }
        catch (Exception exception) { Debug.LogWarning("Loss state regression failed; see " + Result + ": " + exception.Message); }
        finally { File.Delete(Request); running = false; }
    }

    [MenuItem("Tools/Diagnostics/Check Settled Tray Loss State")]
    public static void Run()
    {
        Scene source = default, fixture = default;
        Scene previousScene = SceneManager.GetActiveScene();
        GridManager previousGrid = GridManager.Instance;
        GunGridManager previousGuns = GunGridManager.Instance;
        NavigationManager previousNavigation = NavigationManager.Instance;
        float previousTimeScale = Time.timeScale;
        var report = new StringBuilder();
        try
        {
            source = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Game.unity");
            GameObject[] roots = source.GetRootGameObjects();
            GridManager templateGrid = roots.SelectMany(root => root.GetComponentsInChildren<GridManager>(true)).Single();
            Gun templateGun = roots.SelectMany(root => root.GetComponentsInChildren<Gun>(true)).First(gun => gun.bulletPrefab != null);
            BlockColorData blue = AssetDatabase.LoadAssetAtPath<BlockColorData>("Assets/Resources/BlockColorData/Data/Blue.asset");
            BlockColorData white = AssetDatabase.LoadAssetAtPath<BlockColorData>("Assets/Resources/BlockColorData/Data/White.asset");
            Require(blue != null && white != null, "Saved block color data missing.");

            fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(fixture);
            var boardObject = new GameObject("Loss Board", typeof(RectTransform));
            boardObject.transform.position = templateGrid.transform.position;
            boardObject.transform.localScale = templateGrid.transform.lossyScale;
            GridManager board = boardObject.AddComponent<GridManager>();
            board.gameMode = GameMode.Level;
            var area = UnityEngine.Object.Instantiate(templateGrid.gridArea.gameObject);
            area.name = "Loss Board Area";
            area.transform.SetParent(null, false);
            area.transform.position = templateGrid.gridArea.position;
            area.transform.localScale = templateGrid.gridArea.lossyScale;
            board.gridArea = area.transform;
            SpriteMask mask = boardObject.AddComponent<SpriteMask>();
            EditorUtility.CopySerialized(templateGrid.GetComponent<SpriteMask>(), mask);
            board.InitializeGrid(8, 8);
            GridManager.Instance = board;
            NavigationManager.Instance = null;
            Time.timeScale = 1f;
            Require(BoardViewport.TryGetBounds(board, out Bounds bounds), "Authored board mask unavailable.");
            var gunManagerObject = new GameObject("Loss Gun Manager");
            GunGridManager manager = gunManagerObject.AddComponent<GunGridManager>();
            GunGridManager.Instance = manager;

            Block target = UnityEngine.Object.Instantiate(templateGrid.blockPrefab, board.transform, false);
            target.name = "Reachable Blue Target";
            target.gameObject.SetActive(true);
            target.transform.position = bounds.center;
            target.SetColor(blue); target.SetBlockType(BlockType.Normal); target.hp = 10;
            var group = new BlockGroup(); group.Add(target); group.InitializedType();
            board.Groups.Add(group); board.grid[3, 3] = target; target.gridX = target.gridY = 3;

            var slots = new List<TraySlot>();
            var guns = new List<Gun>();
            for (int i = 0; i < 5; i++)
            {
                var slotObject = new GameObject("Loss Slot " + i);
                slotObject.transform.position = new Vector3(bounds.center.x - .8f + i * .4f, bounds.min.y - .6f);
                TraySlot slot = slotObject.AddComponent<TraySlot>(); slot.slotPoint = slotObject.transform;
                Gun gun = UnityEngine.Object.Instantiate(templateGun, slot.slotPoint, false);
                gun.gameObject.SetActive(true); gun.transform.localPosition = Vector3.zero;
                gun.currentColor = white; gun.currentHP = gun.maxHP = 5;
                gun.gunType = GunType.Normal; gun.inGrid = false; gun.lockedInTray = true; gun.canShoot = true;
                gun.SetCurrentTray(slot); typeof(TraySlot).GetField("currentGun", Private).SetValue(slot, gun);
                var muzzle = new GameObject("Loss Test Muzzle"); muzzle.transform.SetParent(gun.transform, false); gun.firePoint = muzzle.transform;
                slots.Add(slot); guns.Add(gun);
            }
            Physics2D.SyncTransforms();

            manager.ResetLoseState(); Tick(manager, 0f); Require(!Lost(manager), "Loss must allow a stable board interval.");
            manager.CheckLoseCondition(); Tick(manager, .1f); manager.CheckLoseCondition(); Tick(manager, .4f);
            Require(Lost(manager), "A full settled tray without a legal shot must eventually lose, even with repeated callbacks.");
            report.AppendLine("PASS: a full settled blocked tray reaches loss; repeated check requests cannot restart or postpone the decision.");

            manager.ResetLoseState();
            var projectile = new GameObject("Long Flight").AddComponent<Bullet>();
            Tick(manager, 0f); Tick(manager, 10f);
            Require(!Lost(manager), "A shot still in flight must prevent loss beyond the old three-second cutoff.");
            UnityEngine.Object.DestroyImmediate(projectile.gameObject);
            Tick(manager, 11f); Tick(manager, 11.4f); Require(Lost(manager), "Loss must be rechecked after the last projectile leaves.");
            report.AppendLine("PASS: live bullets defer loss beyond three seconds; full-tray loss resumes automatically after flight ends.");

            manager.ResetLoseState();
            var pair = new GameObject("Pending Dual Resolution").AddComponent<DualBulletPair>();
            group.ReserveDamage(1, blue);
            Tick(manager, 0f); Tick(manager, 10f);
            Require(!Lost(manager) && group.pendingDamage == 1, "Pending dual resolution must prevent loss and reservation repair.");
            UnityEngine.Object.DestroyImmediate(pair.gameObject);
            guns[0].currentColor = blue;
            Tick(manager, 11f);
            Require(group.pendingDamage == 0 && !Lost(manager), "Only orphaned reservations may be cleared when simulation is idle.");
            typeof(Gun).GetField("nextFireTime", Private).SetValue(guns[0], 99999f);
            Tick(manager, 12f); Tick(manager, 20f);
            Require(!Lost(manager), "A reachable target remains playable throughout gun cooldown.");
            report.AppendLine("PASS: dual resolution owns its reservations; idle orphan reservations recover; legal cooldown shots do not cause loss.");

            guns[0].currentColor = white;
            manager.ResetLoseState(); board.isProcessingCollapse = true; Tick(manager, 0f); Tick(manager, 10f);
            Require(!Lost(manager), "A collapsing board must not lose.");
            board.isProcessingCollapse = false; board.isProcessingExplosion = true; Tick(manager, 20f);
            Require(!Lost(manager), "An explosion must not lose mid-resolution."); board.isProcessingExplosion = false;
            guns[0].transform.position += Vector3.up * .3f; guns[0].canShoot = false;
            Tick(manager, 30f); Tick(manager, 40f); Require(!Lost(manager), "A gun moving into the tray must not lose.");
            guns[0].transform.position = slots[0].slotPoint.position;
            guns[0].currentColor = blue;
            Physics2D.SyncTransforms(); Tick(manager, 41f);
            Require(guns[0].canShoot && !Lost(manager), "A seated gun must recover from a canceled arrival callback.");
            report.AppendLine("PASS: collapse, explosion and tray movement defer loss; a seated gun's missed readiness callback is repaired.");

            guns[0].currentColor = white; manager.ResetLoseState(); Tick(manager, 0f); Tick(manager, .4f);
            Require(Lost(manager), "Delayed panel fixture must first confirm a blocked tray.");
            guns[0].currentColor = blue; Physics2D.SyncTransforms();
            Require(!manager.CanPresentLosePanel() && !Lost(manager), "A newly available shot must cancel a delayed loss panel.");
            guns[0].currentColor = white; manager.ResetLoseState();
            typeof(TraySlot).GetField("currentGun", Private).SetValue(slots[0], null);
            group.ReserveDamage(1, blue);
            Tick(manager, 0f); Tick(manager, 10f); Require(!Lost(manager), "An open tray slot must not count as full.");
            Require(group.pendingDamage == 0, "Orphaned reservations must recover even before the tray fills.");
            typeof(TraySlot).GetField("currentGun", Private).SetValue(slots[0], guns[0]);
            board.Groups.Clear(); Tick(manager, 11f); Tick(manager, 12f);
            Require(!Lost(manager), "A finished board must win rather than lose."); board.Groups.Add(group);
            board.PrepareForSceneExit(); Tick(manager, 20f); Tick(manager, 30f);
            Require(!Lost(manager), "Scene teardown must not trigger loss.");
            report.AppendLine("PASS: delayed panels revalidate; open slots, completed boards and scene teardown cannot produce loss.");
            report.AppendLine("No Scene, prefab or player data was saved; no Play mode was entered.");
            File.WriteAllText(Result, report.ToString());
        }
        catch (Exception exception) { File.WriteAllText(Result, report + "\nFAIL\n" + exception); throw; }
        finally
        {
            if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
            if (fixture.IsValid()) EditorSceneManager.CloseScene(fixture, true);
            if (source.IsValid()) EditorSceneManager.ClosePreviewScene(source);
            GridManager.Instance = previousGrid; GunGridManager.Instance = previousGuns;
            NavigationManager.Instance = previousNavigation; Time.timeScale = previousTimeScale;
        }
    }
    private static void Tick(GunGridManager manager, float now) => typeof(GunGridManager).GetMethod("EvaluateLoseCondition", Private).Invoke(manager, new object[] { now });
    private static bool Lost(GunGridManager manager) => (bool)typeof(GunGridManager).GetField("loseTriggered", Private).GetValue(manager);
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
#endif
