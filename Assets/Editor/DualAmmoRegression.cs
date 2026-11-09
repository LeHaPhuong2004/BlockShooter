#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Checks real gun generation against group HP, including mirrored dual blocks.</summary>
[InitializeOnLoad]
public static class DualAmmoRegression
{
    private const string Request = "Temp/DualAmmoRegression.request";
    private const string Result = "Temp/DualAmmoRegression.result";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static bool running;

    static DualAmmoRegression() { EditorApplication.update += Poll; }
    private static void Poll()
    {
        if (running || !File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        running = true;
        try { Run(); }
        catch (Exception exception) { Debug.LogException(exception); }
        finally { File.Delete(Request); running = false; }
    }

    [MenuItem("Tools/Diagnostics/Check Dual Group Gun Ammo")]
    public static void Run()
    {
        Scene source = default;
        Scene active = SceneManager.GetActiveScene();
        GridManager previousGrid = GridManager.Instance;
        GunGridManager previousGuns = GunGridManager.Instance;
        UnityEngine.Random.State previousRandom = UnityEngine.Random.state;
        var report = new StringBuilder();
        try
        {
            source = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Game.unity");
            GunGridManager sourceGuns = source.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<GunGridManager>(true)).Single();
            Gun[] prefabs = sourceGuns.gunPrefabs.Where(gun => gun != null && gun.currentColor != null).ToArray();
            BlockColorData[] colors = prefabs.Select(gun => gun.currentColor).Distinct().Take(10).ToArray();
            Require(colors.Length == 10, "The actual Game must provide all ten gun colors for the rare-color regression.");

            RunScenario(prefabs, (grid, guns, spawner) =>
            {
                var dual = new BlockGroup { groupType = BlockType.Dual };
                for (int i = 0; i < 6; i++)
                {
                    Block block = AddBlock(grid, i, BlockType.Dual, null, 0);
                    block.dualTopColor = colors[0]; block.dualBottomColor = colors[1];
                    block.dualTopHP = block.dualBottomHP = 1;
                    dual.Add(block);
                }
                dual.InitializedType();
                int half = dual.GetDualColorHP(colors[0]);
                Require(half >= 10 && half <= 15 && dual.totalHP == half * 2 &&
                    dual.GetDualColorHP(colors[1]) == half, "Dual group budgets must be equal halves of one even 20..30 total.");
                var normal = new BlockGroup { groupType = BlockType.Normal };
                normal.Add(AddBlock(grid, 6, BlockType.Normal, colors[0], 3));
                normal.Add(AddBlock(grid, 7, BlockType.Normal, colors[0], 4));
                normal.InitializedType();
                var frozen = new BlockGroup { groupType = BlockType.Frozen };
                frozen.Add(AddBlock(grid, 8, BlockType.Frozen, colors[1], 5)); frozen.InitializedType();

                Invoke(spawner, "CalculateTotalHPByGroup");
                var data = (IList)typeof(GunGridSpawner).GetField("groupGunData", Private).GetValue(spawner);
                Require(data.Count == 4, "Six dual blocks must contribute two demand entries, plus one normal and one fire entry.");
                int dualEntries = 0, dualDemand = 0;
                foreach (object entry in data)
                {
                    Type type = entry.GetType();
                    if (type.GetField("group").GetValue(entry) != dual) continue;
                    dualEntries++; dualDemand += (int)type.GetField("totalHP").GetValue(entry);
                }
                Require(dualEntries == 2 && dualDemand == dual.totalHP,
                    "Mirroring per-block dual HP multiplied the generated gun ammo.");
                var expectedNormal = new Dictionary<BlockColorData, int> { [colors[0]] = half + 7, [colors[1]] = half };
                var expectedFire = new Dictionary<BlockColorData, int> { [colors[1]] = 5 };
                spawner.SpawnInitialGuns();
                CheckGuns(guns, expectedNormal, expectedFire, report);
                report.AppendLine("PASS: six-block dual group counted once; actual spawned gun HP exactly matches both half budgets plus normal/frozen demand.");
            });

            RunScenario(prefabs, (grid, guns, spawner) =>
            {
                var normal = new BlockGroup { groupType = BlockType.Normal };
                normal.Add(AddBlock(grid, 0, BlockType.Normal, colors[0], 1000)); normal.InitializedType();
                var expectedFire = new Dictionary<BlockColorData, int>();
                for (int i = 0; i < colors.Length; i++)
                {
                    var frozen = new BlockGroup { groupType = BlockType.Frozen };
                    frozen.Add(AddBlock(grid, i + 1, BlockType.Frozen, colors[i], 1)); frozen.InitializedType();
                    expectedFire.Add(colors[i], 1);
                }
                spawner.SpawnInitialGuns();
                CheckGuns(guns, new Dictionary<BlockColorData, int> { [colors[0]] = 1000 }, expectedFire, report);
                int fireCount = guns.Grid.Cast<Gun>().Count(gun => gun != null && gun.gunType == GunType.Fire);
                Require(fireCount == 10, "The small fire-ammo budget lost one or more required colors.");
                report.AppendLine("PASS: 1000 normal HP plus ten one-HP fire colors produces all ten fire colors and exactly 1010 ammo within 24 slots.");
            });
            report.AppendLine("No Scene, prefab, player data, or authored UI was saved; runtime singletons and Random.state restored.");
            File.WriteAllText(Result, report.ToString());
        }
        catch (Exception exception) { File.WriteAllText(Result, report + "\nFAIL\n" + exception); throw; }
        finally
        {
            GridManager.Instance = previousGrid; GunGridManager.Instance = previousGuns;
            UnityEngine.Random.state = previousRandom;
            if (source.IsValid()) EditorSceneManager.ClosePreviewScene(source);
            if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
        }
    }

    private static void RunScenario(Gun[] prefabs, Action<GridManager, GunGridManager, GunGridSpawner> check)
    {
        Scene fixture = default;
        try
        {
            fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var blockRoot = new GameObject("Ammo Test Blocks");
            var grid = blockRoot.AddComponent<GridManager>();
            grid.width = 8; grid.height = 20; grid.grid = new Block[8, 20];
            GridManager.Instance = grid;
            var gunRoot = new GameObject("Ammo Test Guns");
            var guns = gunRoot.AddComponent<GunGridManager>();
            guns.gunPrefabs = prefabs;
            guns.gridOrigin = new GameObject("Ammo Test Gun Origin").transform;
            guns.gridOrigin.SetParent(gunRoot.transform, false);
            typeof(GunGridManager).GetMethod("Awake", Private).Invoke(guns, null);
            var spawner = (GunGridSpawner)typeof(GunGridManager).GetField("spawner", Private).GetValue(guns);
            check(grid, guns, spawner);
        }
        finally
        {
            if (fixture.IsValid())
            {
                // GunChain owns a runtime-only material. Clear it before editor teardown
                // so its gameplay OnDestroy does not call deferred Destroy in Edit mode.
                foreach (GunChain chain in fixture.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GunChain>(true)))
                {
                    var line = chain.GetComponent<LineRenderer>();
                    if (line == null) continue;
                    Material material = line.sharedMaterial;
                    line.sharedMaterial = null;
                    if (material != null) UnityEngine.Object.DestroyImmediate(material);
                }
                EditorSceneManager.CloseScene(fixture, true);
            }
        }
    }

    private static Block AddBlock(GridManager grid, int index, BlockType type, BlockColorData color, int hp)
    {
        var root = new GameObject("Ammo " + type + " " + index);
        root.transform.SetParent(grid.transform, false);
        Block block = root.AddComponent<Block>();
        block.blockType = type; block.blockColor = color; block.hp = hp;
        block.gridX = index % grid.width; block.gridY = index / grid.width;
        grid.grid[block.gridX, block.gridY] = block;
        return block;
    }

    private static void CheckGuns(GunGridManager guns, Dictionary<BlockColorData, int> normal,
        Dictionary<BlockColorData, int> fire, StringBuilder report)
    {
        Require(guns.columns == 4 && guns.rows == 6 && guns.GetGridCapacity() == 24, "Gun grid geometry changed.");
        Gun[] actual = guns.Grid.Cast<Gun>().Where(gun => gun != null).ToArray();
        Require(actual.Length == 24, "Both scenarios require all 24 authored gun slots.");
        foreach (Gun gun in actual) Require(gun.currentHP > 0, "Spawned a gun with zero ammo.");
        foreach (var set in new[] { (type: GunType.Normal, demand: normal), (type: GunType.Fire, demand: fire) })
        {
            var totals = new Dictionary<BlockColorData, int>();
            foreach (Gun gun in actual.Where(gun => gun.gunType == set.type))
            {
                if (!totals.ContainsKey(gun.currentColor)) totals.Add(gun.currentColor, 0);
                totals[gun.currentColor] += gun.currentHP;
            }
            Require(totals.Count == set.demand.Count, "Required gun color/type count changed: " + set.type);
            foreach (var pair in set.demand)
            {
                Require(totals.TryGetValue(pair.Key, out int amount) && amount == pair.Value,
                    set.type + " " + pair.Key.name + " expected " + pair.Value + " ammo, got " + amount);
                report.AppendLine(set.type + " " + pair.Key.name + ": expected=" + pair.Value + " actual=" + amount);
            }
        }
    }

    private static void Invoke(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
#endif
