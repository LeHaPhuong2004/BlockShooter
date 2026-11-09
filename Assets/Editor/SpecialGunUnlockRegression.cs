#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Checks the real gun spawner at the special-gun unlock boundary without player writes.</summary>
[InitializeOnLoad]
public static class SpecialGunUnlockRegression
{
    private const string Request = "Temp/SpecialGunUnlockRegression.request";
    private const string Result = "Temp/SpecialGunUnlockRegression.result";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static bool running;

    static SpecialGunUnlockRegression() => EditorApplication.update += Poll;
    private static void Poll()
    {
        if (running || !File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        running = true;
        try { Run(); }
        catch (Exception exception) { Debug.LogException(exception); }
        finally { File.Delete(Request); running = false; }
    }

    [MenuItem("Tools/Diagnostics/Check Special Gun Unlock Level")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Run the special-gun check outside Play Mode.");
        Scene active = SceneManager.GetActiveScene();
        GridManager previousGrid = GridManager.Instance;
        GunGridManager previousGuns = GunGridManager.Instance;
        LevelManager previousLevel = LevelManager.Instance;
        UnityEngine.Random.State previousRandom = UnityEngine.Random.state;
        var report = new StringBuilder();
        try
        {
            foreach (int level in new[] { 1, 24, 25, 26, 0 }) CheckLevel(level, report);
            report.AppendLine("PASS: no authored scenes, prefabs, preferences or progression changed; singletons and Random.state restored.");
            File.WriteAllText(Result, report.ToString());
        }
        catch (Exception exception) { File.WriteAllText(Result, report + "\nFAIL\n" + exception); throw; }
        finally
        {
            GridManager.Instance = previousGrid; GunGridManager.Instance = previousGuns;
            LevelManager.Instance = previousLevel; UnityEngine.Random.state = previousRandom;
            if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
        }
    }

    private static void CheckLevel(int level, StringBuilder report)
    {
        Scene fixture = default;
        var colors = new BlockColorData[3];
        Sprite sprite = null;
        try
        {
            fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(fixture);
            LevelManager.Instance = null;
            if (level > 0)
            {
                var manager = new GameObject("Synthetic Level").AddComponent<LevelManager>();
                typeof(LevelManager).GetField("currentLevel", Private).SetValue(manager, level);
                LevelManager.Instance = manager;
            }
            bool unlocked = level >= 25;
            string context = level == 0 ? "No LevelManager" : "Level " + level;
            Require(GunGridSpawner.SpecialGunsUnlocked == unlocked, context + ": incorrect unlock policy.");
            var grid = new GameObject("Synthetic Block Grid").AddComponent<GridManager>();
            grid.width = 3; grid.height = 1; grid.grid = new Block[3, 1]; GridManager.Instance = grid;
            var guns = new GameObject("Synthetic Gun Grid").AddComponent<GunGridManager>();
            typeof(GunGridManager).GetMethod("Awake", Private).Invoke(guns, null);
            guns.gridOrigin = guns.transform;
            sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
            guns.gunPrefabs = new Gun[colors.Length];
            for (int index = 0; index < colors.Length; index++)
            {
                colors[index] = ScriptableObject.CreateInstance<BlockColorData>();
                colors[index].name = "Unlock Color " + index;
                colors[index].color = Color.HSVToRGB(index / 3f, 1, 1);
                Gun template = MakeGun("Synthetic Gun Template " + index, colors[index], sprite);
                template.transform.SetParent(guns.transform, false); guns.gunPrefabs[index] = template;
                var block = new GameObject("Synthetic Block " + index).AddComponent<Block>();
                block.transform.SetParent(grid.transform, false);
                block.blockColor = colors[index]; block.hp = 80; grid.grid[index, 0] = block;
            }
            Gun first = MakeGun("Direct Pair A", colors[0], sprite);
            Gun second = MakeGun("Direct Pair B", colors[1], sprite);
            first.SetHP(7); second.SetHP(11); guns.Chain.CreateChain(first, second);
            Require(unlocked ? first.chain != null && first.chain == second.chain && first.chain.IsPair
                : first.chain == null && second.chain == null, context + ": direct chain creation bypassed the gate.");
            Require(first.currentHP == 7 && second.currentHP == 11 && first.currentColor == colors[0] &&
                second.currentColor == colors[1] && first.gunType == GunType.Normal && second.gunType == GunType.Normal &&
                !first.mysteryHidden && !second.mysteryHidden, context + ": chain gate changed ordinary guns.");

            UnityEngine.Random.InitState(7325);
            var spawner = (GunGridSpawner)typeof(GunGridManager).GetField("spawner", Private).GetValue(guns);
            spawner.SpawnInitialGuns();
            Gun[] actual = guns.Grid.Cast<Gun>().Where(gun => gun != null).ToArray();
            int hidden = actual.Count(gun => gun.mysteryHidden);
            int paired = actual.Count(gun => gun.chain != null && gun.chain.IsPair);
            Require(actual.Length == 24 && actual.Sum(gun => gun.currentHP) == 240 &&
                colors.All(color => actual.Where(gun => gun.currentColor == color).Sum(gun => gun.currentHP) == 80),
                context + ": generation changed slot count or color/ammo budgets.");
            Require(unlocked ? hidden > 0 && paired > 0 : hidden == 0 && paired == 0,
                context + ": generated hidden=" + hidden + ", paired=" + paired + ".");
            report.AppendLine("PASS: " + context + ", hidden=" + hidden + ", paired=" + paired + ", ammo=240; direct chain gate correct.");
        }
        finally
        {
            if (fixture.IsValid())
            {
                foreach (LineRenderer line in fixture.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<LineRenderer>(true)))
                {
                    Material material = line.sharedMaterial; line.sharedMaterial = null;
                    if (material != null) UnityEngine.Object.DestroyImmediate(material);
                }
                EditorSceneManager.CloseScene(fixture, true);
            }
            foreach (BlockColorData color in colors) if (color != null) UnityEngine.Object.DestroyImmediate(color);
            if (sprite != null) UnityEngine.Object.DestroyImmediate(sprite);
        }
    }

    private static Gun MakeGun(string name, BlockColorData color, Sprite sprite)
    {
        var gun = new GameObject(name).AddComponent<Gun>();
        var renderer = new GameObject("Gun Visual").AddComponent<SpriteRenderer>();
        renderer.transform.SetParent(gun.transform, false); renderer.sprite = sprite;
        gun.gunSprite = renderer; gun.gunVisual = renderer.transform;
        gun.questionMarkSprite = sprite; gun.currentColor = color;
        return gun;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
