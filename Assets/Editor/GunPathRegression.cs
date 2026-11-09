#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class GunPathRegression
{
    private const string Request = "Temp/GunPathRegression.request";
    private const string Result = "Temp/GunPathRegression.result";
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static bool running;
    static GunPathRegression() { EditorApplication.update += Poll; }
    private static void Poll()
    {
        if (running || !File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        running = true;
        try { Run(); }
        catch (Exception exception) { Debug.LogWarning("Gun path regression failed: " + exception.Message); }
        finally { File.Delete(Request); running = false; }
    }

    [MenuItem("Tools/Diagnostics/Check Clear Gun Paths")]
    public static void Run()
    {
        Scene source = default, fixture = default;
        Scene previousScene = SceneManager.GetActiveScene();
        GridManager previousGrid = GridManager.Instance;
        GunGridManager previousGuns = GunGridManager.Instance;
        var report = new StringBuilder();
        try
        {
            source = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Game.unity");
            GameObject[] roots = source.GetRootGameObjects();
            GridManager template = roots.SelectMany(root => root.GetComponentsInChildren<GridManager>(true)).Single();
            Gun templateGun = roots.SelectMany(root => root.GetComponentsInChildren<Gun>(true)).First(gun => gun.bulletPrefab != null);
            BlockColorData white = AssetDatabase.LoadAssetAtPath<BlockColorData>("Assets/Resources/BlockColorData/Data/White.asset");
            BlockColorData red = AssetDatabase.LoadAssetAtPath<BlockColorData>("Assets/Resources/BlockColorData/Data/Red.asset");
            Require(white != null && red != null, "Saved colors missing.");
            fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(fixture);
            var boardObject = new GameObject("Path Test Board");
            boardObject.transform.position = template.transform.position;
            boardObject.transform.rotation = template.transform.rotation;
            boardObject.transform.localScale = template.transform.lossyScale;
            GridManager grid = boardObject.AddComponent<GridManager>();
            grid.cellSize = template.cellSize;
            var area = UnityEngine.Object.Instantiate(template.gridArea.gameObject);
            area.transform.SetParent(null, false);
            area.transform.position = template.gridArea.position;
            area.transform.rotation = template.gridArea.rotation;
            area.transform.localScale = template.gridArea.lossyScale;
            grid.gridArea = area.transform;
            var mask = boardObject.AddComponent<SpriteMask>();
            EditorUtility.CopySerialized(template.GetComponent<SpriteMask>(), mask);
            grid.InitializeGrid(8, 8);
            GridManager.Instance = grid;
            GunGridManager.Instance = null;
            Require(BoardViewport.TryGetBounds(grid, out Bounds board), "Actual board mask missing.");
            var gunObject = new GameObject("White Path Test Gun");
            Gun gun = gunObject.AddComponent<Gun>();
            gun.firePoint = new GameObject("Muzzle").transform;
            gun.firePoint.SetParent(gunObject.transform, false);
            gun.currentColor = white; gun.currentHP = gun.maxHP = 20;
            gun.lockedInTray = true; gun.inGrid = false; gun.canShoot = true;
            gun.bulletPrefab = templateGun.bulletPrefab;
            gun.transform.position = new Vector3(grid.GetBlockPosition(0, 0).x, board.min.y - .6f);

            // A clear front target must use a direct shot.
            BlockGroup front = Group(grid, template.blockPrefab, white, new[] { new Vector2Int(0, 0) });
            Physics2D.SyncTransforms();
            Require(Find(gun, front, out _, out _, out List<Vector2> direct) && direct.Count == 2 && Clear(gun, direct, front),
                "An unobstructed white block in front of the gun must be targetable directly.");
            Require(gun.HasValidShootTarget() && gun.HasPotentialShootTarget(), "The shooting and loss queries disagree on a direct path.");
            ClearBlocks(grid);

            // The old route entered the stone above gun.x for every candidate.
            BlockGroup exposed = Group(grid, template.blockPrefab, white, new[] { new Vector2Int(7, 0), new Vector2Int(7, 1) });
            foreach (int column in new[] { 0, 2, 3, 4, 5, 6 }) Obstacle(grid, template.blockPrefab, red, column, 0);
            Physics2D.SyncTransforms();
            Require(!Clear(gun, new List<Vector2> { gun.firePoint.position, exposed.blocks[0].transform.position }, exposed), "Alternate-entry fixture must block the direct shot.");
            Require(Find(gun, exposed, out Block chosen, out _, out List<Vector2> entry) && Clear(gun, entry, exposed),
                "The open entry lane must reach the exposed white group.");
            Require(entry.Skip(1).Any(point => point.y < board.min.y - .01f), "The path must approach through the clear lane below the board.");
            Require(gun.HasValidShootTarget() && gun.HasPotentialShootTarget(), "The loss query must recognize the alternate clear entry.");
            int ammo = gun.currentHP;
            Require(Shoot(gun) && gun.currentHP == ammo - 1, "The clear white route must launch a shot and consume exactly one ammo.");
            Bullet shot = Bullets(fixture).Single();
            List<Vector2> flight = (List<Vector2>)typeof(Bullet).GetField("path", Private).GetValue(shot);
            Require(flight.Skip(1).Any(point => point.y < board.min.y - .01f) && Clear(gun, flight, exposed),
                "Bullet.Initialize must retain the valid below-board approach instead of clamping it through occupied cells.");
            DestroyBullets(fixture);
            report.AppendLine("PASS: white targets directly in front and behind a blocked gun entry are reachable; alternate entry remains clear after bullet initialization.");
            ClearBlocks(grid);

            // Two alternating walls need more than a single rise/turn route.
            BlockGroup bent = Group(grid, template.blockPrefab, white, new[] { new Vector2Int(7, 6) });
            for (int x = 0; x < 7; x++) Obstacle(grid, template.blockPrefab, red, x, 2);
            for (int x = 1; x < 8; x++) Obstacle(grid, template.blockPrefab, red, x, 4);
            Physics2D.SyncTransforms();
            Require(Find(gun, bent, out _, out _, out List<Vector2> bentPath) && bentPath.Count > 3 && Clear(gun, bentPath, bent),
                "A bent open passage must remain reachable through the board graph.");
            report.AppendLine("PASS: a winding passage around alternating walls produces a fully clear route.");
            ClearBlocks(grid);

            BlockGroup closed = Group(grid, template.blockPrefab, white, new[] { new Vector2Int(4, 3) });
            Vector3 center = closed.blocks[0].transform.position;
            BoxObstacle(grid, template.blockPrefab, red, center + Vector3.up * .6f, new Vector2(1.6f, .3f));
            BoxObstacle(grid, template.blockPrefab, red, center + Vector3.down * .6f, new Vector2(1.6f, .3f));
            BoxObstacle(grid, template.blockPrefab, red, center + Vector3.left * .6f, new Vector2(.3f, 1.6f));
            BoxObstacle(grid, template.blockPrefab, red, center + Vector3.right * .6f, new Vector2(.3f, 1.6f));
            Physics2D.SyncTransforms();
            Require(!Find(gun, closed, out _, out _, out _) && !gun.HasValidShootTarget() && !gun.HasPotentialShootTarget(),
                "An enclosed white target must not produce a blocked or false-potential path.");
            BlockGroup reachable = Group(grid, template.blockPrefab, white, new[] { new Vector2Int(7, 0) });
            Physics2D.SyncTransforms();
            Require(Shoot(gun), "A blocked white group must not prevent selecting another reachable white group.");
            Bullet reachableShot = Bullets(fixture).Single();
            Require(ReferenceEquals(typeof(Bullet).GetField("targetGroup", Private).GetValue(reachableShot), reachable),
                "Target selection must reject the enclosed group and choose the reachable group.");
            DestroyBullets(fixture);
            report.AppendLine("PASS: enclosed targets are rejected consistently, while another reachable group of the same color can still be shot.");
            report.AppendLine("No Scene, prefab, player data, level or authored UI was saved.");
            File.WriteAllText(Result, report.ToString());
        }
        catch (Exception exception) { File.WriteAllText(Result, report + "\nFAIL\n" + exception); throw; }
        finally
        {
            if (fixture.IsValid()) DestroyBullets(fixture);
            if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
            if (fixture.IsValid()) EditorSceneManager.CloseScene(fixture, true);
            if (source.IsValid()) EditorSceneManager.ClosePreviewScene(source);
            GridManager.Instance = previousGrid; GunGridManager.Instance = previousGuns;
        }
    }

    private static BlockGroup Group(GridManager grid, Block template, BlockColorData color, IEnumerable<Vector2Int> cells)
    {
        var group = new BlockGroup();
        foreach (Vector2Int cell in cells)
        {
            Block block = Spawn(grid, template, color, grid.GetBlockPosition(cell.x, cell.y));
            block.gridX = cell.x; block.gridY = cell.y; grid.grid[cell.x, cell.y] = block; group.Add(block);
        }
        grid.Groups.Add(group); group.InitializedType(); return group;
    }
    private static Block Spawn(GridManager grid, Block template, BlockColorData color, Vector3 position)
    {
        Block block = UnityEngine.Object.Instantiate(template, grid.transform, false);
        block.gameObject.SetActive(true); block.transform.position = position; block.SetColor(color); block.SetBlockType(BlockType.Normal); return block;
    }
    private static void Obstacle(GridManager grid, Block template, BlockColorData color, int x, int y)
    {
        Block block = Spawn(grid, template, color, grid.GetBlockPosition(x, y));
        block.gridX = x; block.gridY = y; grid.grid[x, y] = block;
    }
    private static void BoxObstacle(GridManager grid, Block template, BlockColorData color, Vector3 position, Vector2 size)
    {
        Block block = Spawn(grid, template, color, position);
        BoxCollider2D box = block.GetComponentInChildren<BoxCollider2D>();
        Require(box != null, "Saved normal block has no box collider.");
        box.size = new Vector2(size.x / Mathf.Abs(box.transform.lossyScale.x), size.y / Mathf.Abs(box.transform.lossyScale.y));
    }
    private static void ClearBlocks(GridManager grid)
    {
        foreach (Block block in grid.GetComponentsInChildren<Block>(true)) UnityEngine.Object.DestroyImmediate(block.gameObject);
        grid.Groups.Clear(); grid.grid = new Block[grid.width, grid.height];
    }
    private static Bullet[] Bullets(Scene scene) => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Bullet>(true)).ToArray();
    private static void DestroyBullets(Scene scene) { foreach (Bullet bullet in Bullets(scene)) UnityEngine.Object.DestroyImmediate(bullet.gameObject); }
    private static bool Find(Gun gun, BlockGroup group, out Block block, out Vector2 point, out List<Vector2> path)
    {
        object[] args = { group, null, Vector2.zero, null };
        bool found = (bool)typeof(Gun).GetMethod("FindPathToGroup", Private).Invoke(gun, args);
        block = (Block)args[1]; point = (Vector2)args[2]; path = (List<Vector2>)args[3]; return found;
    }
    private static bool Clear(Gun gun, List<Vector2> path, BlockGroup group) => (bool)typeof(Gun).GetMethod("IsPathClear", Private).Invoke(gun, new object[] { path, group });
    private static bool Shoot(Gun gun) => (bool)typeof(Gun).GetMethod("Shoot", Private).Invoke(gun, null);
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
#endif
