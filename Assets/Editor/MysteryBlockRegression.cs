#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Checks mystery reveal and shooting rules without scenes or player-data writes.</summary>
[InitializeOnLoad]
public static class MysteryBlockRegression
{
    private const string RequestPath = "Temp/MysteryBlockRegression.request";
    private const string ReportPath = "Temp/MysteryBlockRegression.result";
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static bool running;

    static MysteryBlockRegression() => EditorApplication.update += ProcessRequest;

    private static void ProcessRequest()
    {
        if (running || !File.Exists(RequestPath) || EditorApplication.isCompiling ||
            EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        running = true;
        try { Run(); }
        catch (Exception exception)
        {
            Directory.CreateDirectory("Temp");
            File.WriteAllText(ReportPath, exception.ToString());
            Debug.LogException(exception);
        }
        finally
        {
            File.Delete(RequestPath);
            running = false;
        }
    }

    [MenuItem("Tools/Diagnostics/Check Mystery Block First Hit")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Run mystery regressions outside Play Mode after compilation.");

        GridManager originalGrid = GridManager.Instance;
        AudioManager originalAudio = AudioManager.Instance;
        Scene preview = default;
        BlockColorData color = null;
        BlockColorData otherColor = null;
        Sprite icon = null;
        try
        {
            GridManager.Instance = null;
            AudioManager.Instance = null;
            preview = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Mystery Block Regression") { hideFlags = HideFlags.HideAndDontSave };
            root.SetActive(false);
            SceneManager.MoveGameObjectToScene(root, preview);
            color = ScriptableObject.CreateInstance<BlockColorData>();
            color.defaultHP = 4;
            otherColor = ScriptableObject.CreateInstance<BlockColorData>();
            icon = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);

            var group = new BlockGroup();
            group.SetType(BlockType.Mystery);
            Block first = CreateBlock(root.transform, group, color, icon, 1);
            Block second = CreateBlock(root.transform, group, color, icon, 2);
            group.InitializedType();
            ExpectHidden(group, "A fresh mystery group must be hidden.");

            // Empty neighboring cells and normal grid maintenance must not reveal the group.
            // Avoid destroying a neighbor: destruction also writes achievement/mission progress.
            var grid = root.AddComponent<GridManager>();
            grid.width = 3;
            grid.height = 1;
            grid.grid = new Block[3, 1];
            grid.grid[1, 0] = first;
            grid.grid[2, 0] = second;
            grid.Groups.Add(group);
            new GridCollapse(grid).CheckAllRows();
            ExpectHidden(group, "An exposed mystery group must stay hidden before impact.");

            // Both target searches use this filter, followed by the same color reservation.
            // Inactive preview objects stay out of FindObjectsOfType and the user's scene.
            var gun = root.AddComponent<Gun>();
            gun.currentColor = color;
            MethodInfo matches = typeof(Gun).GetMethod("BlockMatchesColor", PrivateInstance);
            Expect(matches != null, "The actual gun target filter was not found.");
            Expect((bool)matches.Invoke(gun, new object[] { first }) && group.CanReserveDamage(1, color),
                "A normal gun must be able to target and reserve damage on a matching hidden mystery.");
            gun.currentColor = otherColor;
            Expect(!(bool)matches.Invoke(gun, new object[] { first }) && !group.CanReserveDamage(1, otherColor),
                "Mystery must preserve the existing gun color rule.");
            gun.currentColor = color;
            gun.gunType = GunType.Fire;
            Expect(!(bool)matches.Invoke(gun, new object[] { first }),
                "Mystery must preserve the existing fire-gun restriction.");

            int initialHP = group.totalHP;
            first.TakeDamage(0, color);
            first.TakeDamage(-1, color);
            first.TakeDamage(1, null);
            ExpectHidden(group, "Invalid damage must not reveal a mystery.");
            Expect(group.totalHP == initialHP, "Invalid damage changed mystery HP.");

            group.ReserveDamage(1, color);
            Expect(group.pendingDamage == 1, "The first shot did not reserve exactly one damage.");
            ExpectHidden(group, "Firing/reserving a shot must not reveal a mystery before impact.");
            group.ReleaseDamage(1, color);
            Expect(group.pendingDamage == 0, "A canceled shot did not release its reservation.");
            ExpectHidden(group, "A canceled shot must not reveal a mystery.");

            group.ReserveDamage(1, color);
            first.TakeDamage(1, color);
            ExpectRevealed(group, "The first accepted hit must reveal every block in the group.");
            Expect(group.totalHP == initialHP - 1 && group.pendingDamage == 0,
                "The reveal hit must apply normal damage and settle its reservation.");
            Expect(group.groupType == BlockType.Mystery && first.blockType == BlockType.Mystery,
                "Revealing must preserve the authored block type.");

            group.UpdateTypeVisuals();
            first.UpdateTypeVisual();
            second.UpdateTypeVisual();
            ExpectRevealed(group, "Refreshing visuals must not hide a revealed mystery again.");
            group.ReserveDamage(1, color);
            second.TakeDamage(1, color);
            Expect(group.totalHP == initialHP - 2 && group.pendingDamage == 0,
                "A later hit must keep normal damage and reservation behavior.");
            ExpectRevealed(group, "Later hits must leave the entire group revealed.");

            var normal = new BlockGroup();
            normal.SetType(BlockType.Normal);
            Block normalBlock = CreateBlock(root.transform, normal, color, icon, 0);
            normal.InitializedType();
            normal.ReserveDamage(1, color);
            normalBlock.TakeDamage(1, color);
            Expect(normal.totalHP == color.defaultHP - 1 && normal.pendingDamage == 0 &&
                !normal.IsMysteryRevealed && !normalBlock.typeIconRenderer.enabled,
                "Normal blocks must retain their existing damage and visual behavior.");

            group.InitializedType();
            ExpectHidden(group, "Initializing a new mystery lifecycle must reset the reveal state.");
            Directory.CreateDirectory("Temp");
            const string report = "PASS: exposed mystery stays hidden; matching normal gun can target/reserve; " +
                "color/fire rules preserved; invalid/canceled shots do not reveal; first hit reveals the whole group " +
                "and applies normal HP/reservation; later hits and visual refresh stay revealed; normal blocks " +
                "unchanged; initialization resets reveal state. Preview objects only; no scene or player-data saves.";
            File.WriteAllText(ReportPath, report);
            Debug.Log(report);
        }
        finally
        {
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            if (color != null) UnityEngine.Object.DestroyImmediate(color);
            if (otherColor != null) UnityEngine.Object.DestroyImmediate(otherColor);
            if (icon != null) UnityEngine.Object.DestroyImmediate(icon);
            GridManager.Instance = originalGrid;
            AudioManager.Instance = originalAudio;
        }
    }

    private static Block CreateBlock(Transform parent, BlockGroup group, BlockColorData color, Sprite icon, int x)
    {
        var root = new GameObject("Regression " + group.groupType + " " + x);
        root.transform.SetParent(parent, false);
        var block = root.AddComponent<Block>();
        typeof(Block).GetMethod("Awake", PrivateInstance).Invoke(block, null);
        block.gridX = x;
        block.gridY = 0;
        var visual = new GameObject("Type Icon");
        visual.transform.SetParent(root.transform, false);
        block.typeIconRenderer = visual.AddComponent<SpriteRenderer>();
        block.mysteryIcon = icon;
        block.SetColor(color);
        group.Add(block);
        return block;
    }

    private static void ExpectHidden(BlockGroup group, string message)
    {
        Expect(!group.IsMysteryRevealed, message);
        foreach (Block block in group.blocks) Expect(block.typeIconRenderer.enabled, message);
    }

    private static void ExpectRevealed(BlockGroup group, string message)
    {
        Expect(group.IsMysteryRevealed, message);
        foreach (Block block in group.blocks) Expect(!block.typeIconRenderer.enabled, message);
    }

    private static void Expect(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
