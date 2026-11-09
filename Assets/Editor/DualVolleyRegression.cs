#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DG.Tweening;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>Checks actual tray launches and the two-impact dual HP contract without Play or player writes.</summary>
[InitializeOnLoad]
public static class DualVolleyRegression
{
    private const string Request = "Temp/DualVolleyRegression.request";
    private const string Result = "Temp/DualVolleyRegression.result";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static bool running;
    static DualVolleyRegression() { EditorApplication.update += Poll; }
    private static void Poll()
    {
        if (running || !File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        running = true;
        try { Run(); }
        catch (Exception exception) { Debug.LogException(exception); }
        finally { File.Delete(Request); running = false; }
    }

    [MenuItem("Tools/Diagnostics/Check Dual Tray Volleys")]
    public static void Run()
    {
        Scene source = default, fixture = default;
        Scene previousScene = SceneManager.GetActiveScene();
        GridManager previousGrid = GridManager.Instance;
        EventSystem previousEvents = EventSystem.current;
        UnityEngine.Random.State previousRandom = UnityEngine.Random.state;
        var report = new StringBuilder();
        try
        {
            source = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Game.unity");
            GameObject[] roots = source.GetRootGameObjects();
            GridManager template = roots.SelectMany(root => root.GetComponentsInChildren<GridManager>(true)).Single();
            Gun gunTemplate = roots.SelectMany(root => root.GetComponentsInChildren<Gun>(true))
                .First(gun => gun.firePoint != null && gun.bulletPrefab != null);
            var red = AssetDatabase.LoadAssetAtPath<BlockColorData>("Assets/Resources/BlockColorData/Data/Red.asset");
            var blue = AssetDatabase.LoadAssetAtPath<BlockColorData>("Assets/Resources/BlockColorData/Data/Blue.asset");
            Require(red != null && blue != null && template.dualBlockPrefab != null, "Missing saved Game dual templates/colors.");

            fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(fixture);
            var boardObject = new GameObject("Dual Volley Board", typeof(RectTransform));
            boardObject.transform.position = template.transform.position;
            boardObject.transform.rotation = template.transform.rotation;
            boardObject.transform.localScale = template.transform.lossyScale;
            GridManager grid = boardObject.AddComponent<GridManager>();
            grid.width = template.width; grid.height = template.height; grid.cellSize = template.cellSize;
            SpriteMask mask = boardObject.AddComponent<SpriteMask>();
            EditorUtility.CopySerialized(template.GetComponent<SpriteMask>(), mask);
            Require(BoardViewport.TryGetBounds(grid, out Bounds board), "Saved Game board mask missing.");
            // A terminal edit-mode HP test must not run the live grid's collapse/level completion.
            GridManager.Instance = null;
            Block block = SpawnBlock(template.dualBlockPrefab, grid, board.center, red, blue);
            var group = new BlockGroup { groupType = BlockType.Dual };
            group.Add(block); group.InitializedType();
            int total = group.totalHP;
            Require(total >= 20 && total <= 30 && total % 2 == 0, "Dual group HP must be even 20..30.");
            Require(group.GetDualColorHP(red) == total / 2 && group.GetDualColorHP(blue) == total / 2,
                "Both dual color budgets must start equal.");
            RequireCenteredLabel(block, total);
            report.AppendLine("PASS: saved dual uses one centered label; group total=" + total + " with equal color halves.");

            TraySlot firstSlot = CreateSlot("First Dual Tray", new Vector3(board.center.x - .35f, board.min.y - .65f));
            TraySlot secondSlot = CreateSlot("Second Dual Tray", new Vector3(board.center.x + .35f, board.min.y - .65f));
            Gun first = CreateGun(gunTemplate, firstSlot, red);
            int beforeAmmo = first.currentHP;
            Require(!DualGunVolley.CanFire(first, group) && !DualGunVolley.TryFire(first, group),
                "One matching color on tray must not launch a dual shot.");
            Require(!Shoot(first), "One matching gun must not shoot the dual via normal acquisition.");
            Require(first.currentHP == beforeAmmo && group.pendingDamage == 0 && FixtureBullets(fixture).Length == 0,
                "A lone gun consumed ammo or reserved damage.");

            Gun second = CreateGun(gunTemplate, secondSlot, red);
            Require(!DualGunVolley.CanFire(first, group) && !DualGunVolley.TryFire(first, group),
                "Two guns of the same half color cannot replace the missing other color.");
            second.currentColor = blue;
            Require(DualGunVolley.CanFire(first, group), "Red+blue settled tray guns must form a dual volley.");
            int secondAmmo = second.currentHP;
            Require(DualGunVolley.TryFire(first, group), "Two ready matching guns failed to fire.");
            Bullet[] bullets = FixtureBullets(fixture);
            Require(bullets.Length == 2 && first.currentHP == beforeAmmo - 1 && second.currentHP == secondAmmo - 1,
                "A dual volley must create exactly two projectiles and spend one round per gun.");
            Require(group.pendingDamage == 2 && group.totalHP == total, "Firing must reserve both colors without reducing HP yet.");
            Require(!DualGunVolley.CanFire(first, group) && !DualGunVolley.TryFire(second, group) &&
                DualGunVolley.HasPotentialPair(first, group), "Cooldowns must prevent double fire while preserving a potential pair.");
            Require(FixtureBullets(fixture).Length == 2, "Partner update launched a duplicate pair.");
            report.AppendLine("PASS: lone/wrong-color guns do not fire; two tray colors launch exactly two shots, one ammo each, with synchronized cooldowns.");

            object gate = GetGate(bullets[0]);
            Require(gate != null && ReferenceEquals(gate, GetGate(bullets[1])), "Dual bullets are not linked to the same impact gate.");
            foreach (Bullet bullet in bullets) bullet.transform.position = block.transform.position;
            Invoke(bullets[0], "HitTarget", block);
            Require(group.totalHP == total && group.pendingDamage == 2, "First arrival alone must not reduce dual HP.");
            Invoke(bullets[1], "HitTarget", block);
            Require(group.totalHP == total, "Second arrival must start a staged commit, not an immediate two-HP jump.");
            IEnumerator commit = Commit(gate);
            Require(commit.MoveNext(), "The first one-HP application must yield before the second.");
            block.transform.DOKill(false);
            Require(group.totalHP == total - 1 && group.pendingDamage == 1, "First staged hit must reduce exactly one HP.");
            RequireCenteredLabel(block, total - 1);
            Require(Mathf.Abs(group.GetDualColorHP(red) - group.GetDualColorHP(blue)) == 1,
                "Only the arriving color half should change on the first staged hit.");
            Drain(commit);
            block.transform.DOKill(false);
            Require(group.totalHP == total - 2 && group.pendingDamage == 0 &&
                group.GetDualColorHP(red) == total / 2 - 1 && group.GetDualColorHP(blue) == total / 2 - 1,
                "Both staged hits must subtract one from each half and release all reservations.");
            RequireCenteredLabel(block, total - 2);
            report.AppendLine("PASS: one impact does no damage; committed pair displays " + total + " -> " + (total - 1) + " -> " + (total - 2) + ".");

            SetDualRemaining(group, block, red, blue, 1);
            // The terminal HP contract does not need edit-mode shatter tweens or fragment cleanup.
            block.dualTopRenderer = null; block.dualBottomRenderer = null;
            block.spriteRenderer.enabled = true;
            ResetCooldown(first); ResetCooldown(second);
            Require(DualGunVolley.TryFire(first, group), "Last two dual HP must still accept one complete pair.");
            Bullet[] last = FixtureBullets(fixture);
            Require(last.Length == 2, "Terminal volley created the wrong projectile count.");
            object lastGate = GetGate(last[0]);
            foreach (Bullet bullet in last) { bullet.transform.position = block.transform.position; Invoke(bullet, "HitTarget", block); }
            IEnumerator finish = Commit(lastGate);
            Require(finish.MoveNext() && group.totalHP == 1, "Terminal pair must show 2 -> 1 before death.");
            block.transform.DOKill(false);
            Drain(finish);
            block.transform.DOKill(false);
            Require(group.totalHP == 0 && group.pendingDamage == 0 && group.GetDualColorHP(red) == 0 && group.GetDualColorHP(blue) == 0,
                "Terminal dual volley must finish at zero without negative color or pending HP.");
            report.AppendLine("PASS: terminal pair resolves 2 -> 1 -> 0 with no remaining reservation.");
            report.AppendLine("No Scene, prefab, color asset or player data was saved; no Play mode was entered.");
            File.WriteAllText(Result, report.ToString());
        }
        catch (Exception exception) { File.WriteAllText(Result, report + "\nFAIL\n" + exception); throw; }
        finally
        {
            if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
            if (fixture.IsValid())
                foreach (Transform target in fixture.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)))
                    target.DOKill(false);
            if (fixture.IsValid()) EditorSceneManager.CloseScene(fixture, true);
            if (source.IsValid()) EditorSceneManager.ClosePreviewScene(source);
            GridManager.Instance = previousGrid;
            EventSystem.current = previousEvents;
            UnityEngine.Random.state = previousRandom;
        }
    }

    private static Block SpawnBlock(Block template, GridManager grid, Vector3 position, BlockColorData red, BlockColorData blue)
    {
        Block block = UnityEngine.Object.Instantiate(template, grid.transform, false);
        block.gameObject.SetActive(true); block.transform.position = position;
        typeof(Block).GetMethod("Awake", Private).Invoke(block, null);
        block.SetDual(red, blue);
        BoardBlockClipping.Bind(block);
        return block;
    }
    private static TraySlot CreateSlot(string name, Vector3 position)
    {
        var root = new GameObject(name); root.transform.position = position;
        TraySlot slot = root.AddComponent<TraySlot>();
        slot.slotPoint = new GameObject("Muzzle Slot").transform; slot.slotPoint.SetParent(root.transform, false);
        return slot;
    }
    private static Gun CreateGun(Gun template, TraySlot slot, BlockColorData color)
    {
        Gun gun = UnityEngine.Object.Instantiate(template, slot.slotPoint, false);
        gun.name = "Dual Test " + color.name; gun.transform.localPosition = Vector3.zero;
        gun.gameObject.SetActive(true); gun.currentColor = color; gun.currentHP = 5; gun.maxHP = 5;
        gun.gunType = GunType.Normal; gun.mysteryHidden = false; gun.inGrid = false; gun.lockedInTray = true;
        gun.gunVisual = null; gun.canShoot = true; gun.fireRate = .5f;
        gun.SetCurrentTray(slot);
        typeof(TraySlot).GetField("currentGun", Private).SetValue(slot, gun);
        return gun;
    }
    private static Bullet[] FixtureBullets(Scene scene) => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<Bullet>(true)).ToArray();
    private static bool Shoot(Gun gun) => (bool)typeof(Gun).GetMethod("Shoot", Private).Invoke(gun, null);
    private static void Invoke(Bullet bullet, string name, params object[] args) => typeof(Bullet).GetMethod(name, Private).Invoke(bullet, args);
    private static object GetGate(Bullet bullet) => typeof(Bullet).GetField("dualPair", Private)?.GetValue(bullet);
    private static IEnumerator Commit(object gate)
    {
        Require(gate != null, "Missing paired impact gate.");
        MethodInfo method = gate.GetType().GetMethod("CommitHits", BindingFlags.Public | Private) ?? gate.GetType().GetMethod("ApplyHits", Private);
        Require(method != null, "Paired impact gate has no staged commit method.");
        return (IEnumerator)method.Invoke(gate, null);
    }
    private static void Drain(IEnumerator sequence)
    {
        int steps = 0;
        while (sequence.MoveNext()) Require(++steps < 8, "Unexpected unbounded dual commit sequence.");
    }
    private static void RequireCenteredLabel(Block block, int total)
    {
        TMP_Text[] active = block.GetComponentsInChildren<TMP_Text>(true)
            .Where(label => label.gameObject.activeInHierarchy && !string.IsNullOrEmpty(label.text)).ToArray();
        Require(active.Length == 1 && active[0].text == total.ToString(), "Dual must display exactly one shared total HP label.");
        Require(Vector3.Distance(active[0].transform.position, block.transform.position) < .025f, "Dual HP label is not centered on its block.");
    }
    private static void SetDualRemaining(BlockGroup group, Block block, BlockColorData red, BlockColorData blue, int remaining)
    {
        var hp = (Dictionary<BlockColorData, int>)typeof(BlockGroup).GetField("dualHPByColor", Private).GetValue(group);
        hp[red] = remaining; hp[blue] = remaining; group.totalHP = remaining * 2;
        typeof(BlockGroup).GetMethod("SyncDualBlockHP", Private).Invoke(group, null);
        block.UpdateHP();
    }
    private static void ResetCooldown(Gun gun) => typeof(Gun).GetField("nextFireTime", Private).SetValue(gun, 0f);
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
#endif
