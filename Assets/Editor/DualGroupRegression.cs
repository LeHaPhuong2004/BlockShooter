#if UNITY_EDITOR
using System;
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
using UnityEngine.SceneManagement;

/// <summary>Checks group-owned dual HP, its central label and authored board clipping without saving game state.</summary>
[InitializeOnLoad]
public static class DualGroupRegression
{
    private const string Request = "Temp/DualGroupRegression.request";
    private const string Result = "Temp/DualGroupRegression.result";
    private const string Output = "Temp/DualGroupValidation";
    private const int Size = 512;
    private static bool running;
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    static DualGroupRegression() { EditorApplication.update += Poll; }
    private static void Poll()
    {
        if (running || !File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        running = true;
        try { Run(); }
        catch (Exception exception) { Debug.LogWarning("Dual group regression failed; see " + Result + ". " + exception.Message); }
        finally { File.Delete(Request); running = false; }
    }

    [MenuItem("Tools/Diagnostics/Check Dual Group HP and Center Text")]
    public static void Run()
    {
        Scene source = default, preview = default;
        GridManager originalGrid = GridManager.Instance;
        AudioManager originalAudio = AudioManager.Instance;
        UnityEngine.Random.State originalRandom = UnityEngine.Random.state;
        BlockColorData firstColor = null, secondColor = null;
        RenderTexture target = null;
        RenderTexture previousTarget = RenderTexture.active;
        var report = new StringBuilder();
        try
        {
            Directory.CreateDirectory(Output);
            GridManager.Instance = null;
            AudioManager.Instance = null;
            UnityEngine.Random.InitState(73029);
            source = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Game.unity");
            GameObject[] roots = source.GetRootGameObjects();
            GridManager templateGrid = roots.SelectMany(root => root.GetComponentsInChildren<GridManager>(true)).Single();
            Require(templateGrid.dualBlockPrefab != null && templateGrid.blockPrefab != null, "Saved Game block templates are missing.");
            SpriteMask templateMask = templateGrid.GetComponent<SpriteMask>();
            Require(templateMask != null && templateMask.enabled && templateMask.sprite != null, "Saved Game board mask is missing.");
            firstColor = CloneColor("Red"); secondColor = CloneColor("Blue");
            firstColor.defaultHP = 99; secondColor.defaultHP = 1;

            preview = EditorSceneManager.NewPreviewScene();
            var boardObject = new GameObject("Dual Group Regression Grid", typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(boardObject, preview);
            boardObject.transform.position = templateGrid.transform.position;
            boardObject.transform.rotation = templateGrid.transform.rotation;
            boardObject.transform.localScale = templateGrid.transform.lossyScale;
            GridManager grid = boardObject.AddComponent<GridManager>();
            grid.blockPrefab = templateGrid.blockPrefab;
            SpriteMask mask = boardObject.AddComponent<SpriteMask>();
            EditorUtility.CopySerialized(templateMask, mask);
            Require(BoardViewport.TryGetBounds(grid, out Bounds board), "The board helper did not find the actual saved mask.");

            BlockGroup retained = null;
            var totals = new HashSet<int>();
            for (int sample = 0; sample < 32; sample++)
            {
                firstColor.defaultHP = sample % 2 == 0 ? 99 : 1;
                secondColor.defaultHP = sample % 2 == 0 ? 1 : 99;
                var group = new BlockGroup(); group.SetType(BlockType.Dual);
                int count = sample % 6 + 1;
                for (int member = 0; member < count; member++)
                    group.Add(SpawnDual(templateGrid.dualBlockPrefab, grid, board.center, firstColor, secondColor));
                group.InitializedType();
                int initial = group.totalHP;
                totals.Add(initial);
                Require(initial >= 20 && initial <= 30 && initial % 2 == 0, "Dual group total must be even and within 20..30.");
                Require(group.InitialDualHP == initial && group.GetDualColorHP(firstColor) == initial / 2 &&
                    group.GetDualColorHP(secondColor) == initial / 2, "Two color budgets must each equal half of the group total.");
                Require(group.TryGetDualColors(out BlockColorData first, out BlockColorData second) &&
                    first == firstColor && second == secondColor, "Dual group color pair changed.");
                foreach (Block block in group.blocks)
                {
                    block.UpdateHP();
                    Require(block.dualTopHP == initial / 2 && block.dualBottomHP == initial / 2,
                        "Members must reflect the shared group color budgets.");
                    CheckCenterText(block, initial);
                }
                group.InitializedType();
                Require(group.totalHP == initial, "Repeated initialization rerolled or refilled dual HP.");
                if (sample == 0) retained = group;
                else DestroyGroupObjects(group);
            }
            Require(totals.Count > 1, "The sampled dual HP totals were not random.");
            report.AppendLine("PASS: 32 real-template dual groups have even totals 20..30, equal halves, and one centered total label; color defaults 1/99 and group member counts 1..6 do not change the budget. Initialization is idempotent.");

            Block retainedBlock = retained.blocks[0];
            int before = retained.totalHP;
            Require(retained.CanReserveDualPair() && retained.TryReserveDualPair(), "A fresh group failed to reserve a complete color pair.");
            Require(retained.pendingDamage == 2 && retained.GetAvailableHP(firstColor) == before / 2 - 1 &&
                retained.GetAvailableHP(secondColor) == before / 2 - 1, "Pair reservation must reserve exactly one HP per color.");
            retained.TakeDualDamage(retainedBlock, 1, firstColor);
            Require(retained.totalHP == before - 1 && retained.pendingDamage == 1 &&
                retained.GetDualColorHP(firstColor) == before / 2 - 1 && retained.GetDualColorHP(secondColor) == before / 2,
                "First paired hit must decrement exactly one HP of its own half.");
            CheckCenterText(retainedBlock, before - 1);
            StopFixtureTweens(retained);
            retained.TakeDualDamage(retainedBlock, 1, secondColor);
            Require(retained.totalHP == before - 2 && retained.pendingDamage == 0 &&
                retained.GetDualColorHP(firstColor) == before / 2 - 1 && retained.GetDualColorHP(secondColor) == before / 2 - 1,
                "Second paired hit must decrement one HP and restore equal remaining halves.");
            CheckCenterText(retainedBlock, before - 2);
            StopFixtureTweens(retained);
            retained.InitializedType();
            Require(retained.totalHP == before - 2 && retained.InitialDualHP == before,
                "Reinitialization after damage must not heal a dual group.");
            report.AppendLine("PASS: one pair reserves 2 HP, applies visible totals N -> N-1 -> N-2, consumes each color reservation once, and leaves equal half budgets after the pair.");

            // Count an actual multi-member group only once per color in ammo requirements.
            var ammoGroup = new BlockGroup(); ammoGroup.SetType(BlockType.Dual);
            for (int member = 0; member < 3; member++)
                ammoGroup.Add(SpawnDual(templateGrid.dualBlockPrefab, grid, board.center, firstColor, secondColor));
            ammoGroup.InitializedType();
            Block normal = UnityEngine.Object.Instantiate(templateGrid.blockPrefab, grid.transform, false);
            normal.gameObject.SetActive(true); normal.transform.position = board.center;
            InvokeAwake(normal);
            normal.SetColor(firstColor); normal.SetBlockType(BlockType.Normal);
            var normalGroup = new BlockGroup(); normalGroup.SetType(BlockType.Normal); normalGroup.Add(normal); normalGroup.InitializedType();
            Require(normalGroup.totalHP == firstColor.defaultHP, "Normal group HP must preserve the color's existing default.");
            grid.width = 4; grid.height = 1; grid.grid = new Block[4, 1];
            for (int x = 0; x < 3; x++) grid.grid[x, 0] = ammoGroup.blocks[x];
            grid.grid[3, 0] = normal;
            Require(grid.GetTotalHPByColor(firstColor) == ammoGroup.GetDualColorHP(firstColor) + normalGroup.totalHP &&
                grid.GetTotalHPByColor(secondColor) == ammoGroup.GetDualColorHP(secondColor),
                "Ammo requirements multiplied a dual group budget by its member count or color default.");
            report.AppendLine("PASS: ammo HP counting deduplicates dual groups while preserving normal-block HP.");
            // Compare the actual same-digit glyph size, not only the fontSize
            // field: the normal prefab and old dual half labels have different scales.
            normal.hpText.text = "28";
            normal.hpText.gameObject.SetActive(true);
            normal.hpText.ForceMeshUpdate(true, true);
            Bounds normalGlyphs = GlyphBounds(normal.hpText);
            string retainedValue = retainedBlock.hpText.text;
            retainedBlock.hpText.text = "28";
            retainedBlock.hpText.ForceMeshUpdate(true, true);
            Bounds dualGlyphs = GlyphBounds(retainedBlock.hpText);
            float heightRatio = dualGlyphs.size.y / normalGlyphs.size.y;
            report.AppendLine("HP glyph sizing: normalHeight=" + normalGlyphs.size.y.ToString("F6") +
                " dualHeight=" + dualGlyphs.size.y.ToString("F6") + " ratio=" + heightRatio.ToString("F4") +
                " normalFont=" + normal.hpText.fontSize + " dualFont=" + retainedBlock.hpText.fontSize);
            Require(normalGlyphs.size.y > 0 && Mathf.Abs(heightRatio - 1f) <= .05f,
                "Dual HP glyph height must match the normal block within 5 percent.");
            retainedBlock.hpText.text = retainedValue;
            retainedBlock.hpText.ForceMeshUpdate(true, true);
            report.AppendLine("PASS: dual HP matches the actual normal-block glyph height, including nested prefab scales.");
            DestroyGroupObjects(ammoGroup); DestroyGroupObjects(normalGroup);

            Camera sourceCamera = roots.SelectMany(root => root.GetComponentsInChildren<Camera>(true)).First(camera => camera.CompareTag("MainCamera"));
            GameObject cameraObject = UnityEngine.Object.Instantiate(sourceCamera.gameObject);
            SceneManager.MoveGameObjectToScene(cameraObject, preview);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.enabled = false; camera.scene = preview; camera.rect = new Rect(0, 0, 1, 1); camera.aspect = 1;
            camera.orthographic = true; camera.orthographicSize = Mathf.Max(board.extents.x, board.extents.y) + .6f;
            camera.transform.position = new Vector3(board.center.x, board.center.y, -10); camera.transform.rotation = Quaternion.identity;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.35f, .35f, .35f, 1);
            target = new RenderTexture(Size, Size, 24); target.Create(); camera.targetTexture = target;
            foreach (SpriteRenderer renderer in retainedBlock.GetComponentsInChildren<SpriteRenderer>(true)) renderer.enabled = false;
            TMP_Text label = retainedBlock.hpText;
            label.gameObject.SetActive(false);
            Color32[] baseline = Capture(camera, null);
            label.gameObject.SetActive(true); label.color = Color.black;
            BoardBlockClipping clipping = retainedBlock.GetComponent<BoardBlockClipping>();
            Require(clipping != null && label.fontSharedMaterial.IsKeywordEnabled("UNITY_UI_CLIP_RECT"),
                "The reparented center label lost the board clipping material.");
            retainedBlock.transform.position = board.center;
            Bounds glyphs = GlyphBounds(label);
            Require(Vector3.Distance(glyphs.center, retainedBlock.transform.position) < .05f,
                "Actual HP glyphs are offset from the block despite a centered label transform: glyph=" + glyphs.center +
                " block=" + retainedBlock.transform.position);
            clipping.RefreshClipping();
            Stats inside = RenderStats(camera, baseline, board, "center-hp-inside", report);
            Require(inside.foreground > 5 && inside.outside <= 2, "Centered total HP must be visible inside the board.");
            retainedBlock.transform.position = new Vector3(board.center.x, board.max.y, 0);
            clipping.RefreshClipping();
            Stats edge = RenderStats(camera, baseline, board, "center-hp-edge", report);
            Require(edge.foreground > 0 && edge.foreground < inside.foreground * .95f && edge.outside <= 2,
                "Reparented total HP must clip correctly when crossing the upper edge.");
            retainedBlock.transform.position = new Vector3(board.center.x, board.max.y + glyphs.extents.y + .3f, 0);
            clipping.RefreshClipping();
            Require(RenderStats(camera, baseline, board, "center-hp-outside", report).foreground <= 2,
                "Centered total HP escaped outside the board.");
            report.AppendLine("PASS: asymmetric half-label margins are cleared and actual glyphs remain within 0.05 units of their block center without forcing a mesh rebuild or recentering the block; HP clips inside, across, and outside the saved board mask.");
            report.AppendLine("No Scene, prefab, color asset, player data or game progress was saved.");
            File.WriteAllText(Result, report.ToString());
        }
        catch (Exception exception) { File.WriteAllText(Result, report + "\nFAIL\n" + exception); throw; }
        finally
        {
            RenderTexture.active = previousTarget;
            if (preview.IsValid())
            {
                foreach (Block block in preview.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Block>(true))) block.transform.DOKill(false);
                EditorSceneManager.ClosePreviewScene(preview);
            }
            if (source.IsValid()) EditorSceneManager.ClosePreviewScene(source);
            if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
            if (firstColor != null) UnityEngine.Object.DestroyImmediate(firstColor);
            if (secondColor != null) UnityEngine.Object.DestroyImmediate(secondColor);
            GridManager.Instance = originalGrid; AudioManager.Instance = originalAudio; UnityEngine.Random.state = originalRandom;
        }
    }

    private static BlockColorData CloneColor(string name)
    {
        BlockColorData original = AssetDatabase.LoadAssetAtPath<BlockColorData>("Assets/Resources/BlockColorData/Data/" + name + ".asset");
        Require(original != null, "Missing saved color data: " + name);
        BlockColorData clone = UnityEngine.Object.Instantiate(original); clone.hideFlags = HideFlags.HideAndDontSave; return clone;
    }
    private static Block SpawnDual(Block template, GridManager grid, Vector3 position, BlockColorData first, BlockColorData second)
    {
        Block block = UnityEngine.Object.Instantiate(template, grid.transform, false);
        block.gameObject.SetActive(true); block.transform.position = position; InvokeAwake(block);
        // Runtime OnEnable binds before SetDual reparents the center label.
        BoardBlockClipping.Bind(block);
        // Reproduce the authored half-label offsets that hid the live center HP.
        foreach (TMP_Text label in block.GetComponentsInChildren<TMP_Text>(true))
            label.margin = new Vector4(3.87f, 0f, 15.37f, 4.66f);
        block.SetDual(first, second);
        return block;
    }
    private static void InvokeAwake(Block block) => typeof(Block).GetMethod("Awake", Private).Invoke(block, null);
    private static void CheckCenterText(Block block, int expected)
    {
        TMP_Text[] active = block.GetComponentsInChildren<TMP_Text>(true).Where(text => text.isActiveAndEnabled && text.gameObject.activeInHierarchy).ToArray();
        Require(active.Length == 1 && active[0] == block.hpText && active[0].text == expected.ToString(),
            "A dual block must display exactly one total HP label.");
        TMP_Text label = active[0];
        Require(label.transform.parent == block.transform && new Vector2(label.transform.localPosition.x, label.transform.localPosition.y).sqrMagnitude < .000001f &&
            label.alignment == TextAlignmentOptions.Center, "The total HP label must be centered on the dual block root.");
        Require(label.margin == Vector4.zero, "The centered HP label retained asymmetric half-label margins.");
        Bounds glyphs = GlyphBounds(label);
        Require(Vector3.Distance(glyphs.center, block.transform.position) < .05f,
            "The actual HP glyph center must match the block, not only the text Transform: glyph=" + glyphs.center +
            " block=" + block.transform.position);
    }
    private static void DestroyGroupObjects(BlockGroup group)
    {
        foreach (Block block in group.blocks.ToArray()) if (block != null) { block.transform.DOKill(false); UnityEngine.Object.DestroyImmediate(block.gameObject); }
    }
    private static void StopFixtureTweens(BlockGroup group)
    {
        foreach (Block block in group.blocks) if (block != null) { block.transform.DOKill(false); block.transform.localScale = block.BaseScale; }
    }
    private static Bounds GlyphBounds(TMP_Text text)
    {
        bool any = false; Bounds bounds = default;
        for (int i = 0; i < text.textInfo.characterCount; i++)
        {
            TMP_CharacterInfo character = text.textInfo.characterInfo[i]; if (!character.isVisible) continue;
            Vector3[] vertices = text.textInfo.meshInfo[character.materialReferenceIndex].vertices;
            for (int j = character.vertexIndex; j < character.vertexIndex + 4; j++)
            {
                Vector3 point = text.transform.TransformPoint(vertices[j]);
                if (!any) { any = true; bounds = new Bounds(point, Vector3.zero); } else bounds.Encapsulate(point);
            }
        }
        Require(any, "No generated central HP glyphs."); return bounds;
    }
    private struct Stats { public int foreground, outside; }
    private static Stats RenderStats(Camera camera, Color32[] baseline, Bounds board, string name, StringBuilder report)
    {
        Color32[] pixels = Capture(camera, name); var stats = new Stats();
        float units = camera.orthographicSize * 2 / Size;
        float left = camera.transform.position.x - camera.orthographicSize, bottom = camera.transform.position.y - camera.orthographicSize;
        for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++)
        {
            int i = y * Size + x;
            if (Mathf.Max(Mathf.Abs(pixels[i].r - baseline[i].r), Mathf.Abs(pixels[i].g - baseline[i].g), Mathf.Abs(pixels[i].b - baseline[i].b)) < 18) continue;
            stats.foreground++;
            float worldX = left + (x + .5f) * units, worldY = bottom + (y + .5f) * units;
            if (worldX < board.min.x - units || worldX > board.max.x + units || worldY < board.min.y - units || worldY > board.max.y + units) stats.outside++;
        }
        report.AppendLine(name + ": pixels=" + stats.foreground + " outside=" + stats.outside); return stats;
    }
    private static Color32[] Capture(Camera camera, string name)
    {
        camera.Render(); RenderTexture.active = camera.targetTexture;
        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        try { texture.ReadPixels(new Rect(0, 0, Size, Size), 0, 0); texture.Apply(); if (name != null) File.WriteAllBytes(Output + "/" + name + ".png", texture.EncodeToPNG()); return texture.GetPixels32(); }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
#endif
