#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Separates the two existing mask ranges on Game's dedicated dual-block template.</summary>
[InitializeOnLoad]
public static class DualBlockMaskSetup
{
    private const string ScenePath = "Assets/Scenes/Game.unity";
    private const string RequestPath = "Temp/DualBlockMaskSetup.request";
    private const string ReportPath = "Temp/DualBlockMaskSetup.result";
    private static bool running;

    static DualBlockMaskSetup() => EditorApplication.update += ProcessRequest;

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
        finally { File.Delete(RequestPath); running = false; }
    }

    [MenuItem("Tools/Game/Repair Dual Block Mask Ranges")]
    public static void Run()
    {
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Repair the dual template outside Play Mode after compilation.");
        Scene originalActive = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        var report = new StringBuilder();
        try
        {
            GridManager grid = FindGrid(scene);
            Block dual = ValidateDedicatedTemplate(grid, scene);
            SpriteRenderer body = dual.spriteRenderer;
            SpriteMask topMask = dual.dualTopRenderer.GetComponent<SpriteMask>();
            SpriteMask bottomMask = dual.dualBottomRenderer.GetComponent<SpriteMask>();
            Require(body != null && body != dual.dualTopRenderer && body != dual.dualBottomRenderer &&
                (body.transform == dual.transform || body.transform.IsChildOf(dual.transform)) &&
                topMask != null && bottomMask != null && topMask != bottomMask,
                "The dual template must have a body and one distinct mask on each color renderer.");
            int layer = body.sortingLayerID;
            int order = body.sortingOrder;
            var before = Capture(dual.transform);
            var normalBefore = Capture(grid.blockPrefab.transform);
            var originalSprites = dual.GetComponentsInChildren<SpriteRenderer>(true)
                .ToDictionary(renderer => renderer, renderer => renderer.sprite);
            var originalMaterials = dual.GetComponentsInChildren<SpriteRenderer>(true)
                .ToDictionary(renderer => renderer, renderer => renderer.sharedMaterials);
            Sprite originalTopMaskSprite = topMask.sprite;
            Sprite originalBottomMaskSprite = bottomMask.sprite;
            int originalHP = dual.hp;
            int originalTopHP = dual.dualTopHP;
            int originalBottomHP = dual.dualBottomHP;
            BlockColorData originalColor = dual.blockColor;
            BlockColorData originalTopColor = dual.dualTopColor;
            BlockColorData originalBottomColor = dual.dualBottomColor;
            report.AppendLine("Target: " + HierarchyPath(dual.transform));
            report.AppendLine("Before: " + Describe(dual));

            SortingGroup group = dual.GetComponent<SortingGroup>();
            if (group == null) group = Undo.AddComponent<SortingGroup>(dual.gameObject);
            Undo.RecordObjects(new UnityEngine.Object[]
                { group, body, dual.dualTopRenderer, dual.dualBottomRenderer, topMask, bottomMask },
                "Repair Dual Block Masks");
            group.sortingLayerID = layer;
            group.sortingOrder = order;
            group.enabled = true;
            ConfigureHalf(dual.dualTopRenderer, topMask, -3, -4, -2);
            ConfigureHalf(dual.dualBottomRenderer, bottomMask, -1, -2, 0);
            body.maskInteraction = SpriteMaskInteraction.None;

            ValidateConfiguration(dual, layer, order);
            Compare(before, Capture(dual.transform), "dual template");
            Compare(normalBefore, Capture(grid.blockPrefab.transform), "normal template");
            foreach (var pair in originalSprites)
                Require(pair.Key.sprite == pair.Value, "A renderer sprite changed: " + pair.Key.name);
            foreach (var pair in originalMaterials)
                Require(pair.Key.sharedMaterials.SequenceEqual(pair.Value), "A renderer material changed: " + pair.Key.name);
            Require(topMask.sprite == originalTopMaskSprite && bottomMask.sprite == originalBottomMaskSprite,
                "A mask sprite changed.");
            Require(dual.hp == originalHP && dual.dualTopHP == originalTopHP && dual.dualBottomHP == originalBottomHP &&
                dual.blockColor == originalColor && dual.dualTopColor == originalTopColor && dual.dualBottomColor == originalBottomColor,
                "The template's color or HP data changed.");
            ValidateClone(dual, layer, order);
            EditorSceneManager.MarkSceneDirty(scene);
            Require(EditorSceneManager.SaveScene(scene), "Could not save the Game scene.");

            Scene reloaded = EditorSceneManager.OpenPreviewScene(ScenePath);
            try
            {
                Block saved = ValidateDedicatedTemplate(FindGrid(reloaded), reloaded);
                ValidateConfiguration(saved, layer, order);
                Compare(before, Capture(saved.transform), "saved dual template");
                ValidateClone(saved, layer, order);
            }
            finally { EditorSceneManager.ClosePreviewScene(reloaded); }
            report.AppendLine("After: " + Describe(dual));
            report.AppendLine("PASS: separate top/bottom mask ranges and per-block SortingGroup saved to Game.unity.");
            report.AppendLine("PASS: fresh clones and the reloaded scene preserve both mask ranges and local isolation.");
            report.AppendLine("PASS: all " + before.Count + " dual transforms, normal-template transforms, sprites, masks, materials, colors and HP retained.");
            report.AppendLine("Only Game.unity was saved; no other scene, player data, gameplay or service was used.");
            Directory.CreateDirectory("Temp");
            File.WriteAllText(ReportPath, report.ToString());
            Debug.Log(report.ToString());
        }
        finally
        {
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
            if (originalActive.IsValid() && originalActive.isLoaded) SceneManager.SetActiveScene(originalActive);
        }
    }

    private static GridManager FindGrid(Scene scene)
    {
        GridManager[] grids = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<GridManager>(true)).ToArray();
        Require(grids.Length == 1, "Expected exactly one Game GridManager, found " + grids.Length + ".");
        return grids[0];
    }

    private static Block ValidateDedicatedTemplate(GridManager grid, Scene scene)
    {
        Require(grid.blockPrefab != null && grid.dualBlockPrefab != null, "Normal or dual block template is missing.");
        Require(grid.blockPrefab != grid.dualBlockPrefab,
            "Normal and dual prefab references are shared; no object was changed.");
        Block dual = grid.dualBlockPrefab;
        Require(!EditorUtility.IsPersistent(dual) && dual.gameObject.scene == scene,
            "The configured dual template must belong to the Game scene; no prefab asset was changed.");
        Require(dual.dualTopRenderer != null && dual.dualBottomRenderer != null &&
            dual.dualTopRenderer != dual.dualBottomRenderer &&
            dual.dualTopRenderer.transform.IsChildOf(dual.transform) &&
            dual.dualBottomRenderer.transform.IsChildOf(dual.transform),
            "Dual color fields must point to two distinct child SpriteRenderers.");
        return dual;
    }

    private static void ConfigureHalf(SpriteRenderer renderer, SpriteMask mask, int order, int back, int front)
    {
        renderer.sortingOrder = order;
        renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        mask.enabled = true;
        mask.isCustomRangeActive = true;
        mask.backSortingLayerID = mask.frontSortingLayerID = renderer.sortingLayerID;
        mask.backSortingOrder = back;
        mask.frontSortingOrder = front;
    }

    private static void ValidateConfiguration(Block dual, int layer, int order)
    {
        SortingGroup group = dual.GetComponent<SortingGroup>();
        Require(group != null && group.enabled && group.sortingLayerID == layer && group.sortingOrder == order,
            "The dual block is not isolated in its original global sorting position.");
        ValidateHalf(dual.dualTopRenderer, -3, -4, -2);
        ValidateHalf(dual.dualBottomRenderer, -1, -2, 0);
        Require(dual.spriteRenderer.maskInteraction == SpriteMaskInteraction.None, "The body is affected by a half mask.");
    }

    private static void ValidateHalf(SpriteRenderer renderer, int order, int back, int front)
    {
        SpriteMask mask = renderer.GetComponent<SpriteMask>();
        Require(renderer.sortingOrder == order && renderer.maskInteraction == SpriteMaskInteraction.VisibleInsideMask &&
            mask != null && mask.enabled && mask.isCustomRangeActive &&
            mask.frontSortingLayerID == renderer.sortingLayerID && mask.backSortingLayerID == renderer.sortingLayerID &&
            mask.backSortingOrder == back && mask.frontSortingOrder == front,
            "Incorrect half-mask isolation on " + renderer.name + ".");
    }

    private static void ValidateClone(Block template, int layer, int order)
    {
        var host = new GameObject("Dual Mask Clone Check") { hideFlags = HideFlags.HideAndDontSave };
        host.SetActive(false);
        try
        {
            Block clone = UnityEngine.Object.Instantiate(template, host.transform, false);
            Require(clone.dualTopRenderer != template.dualTopRenderer && clone.dualBottomRenderer != template.dualBottomRenderer,
                "A clone retains a renderer reference to its template.");
            ValidateConfiguration(clone, layer, order);
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }

    private sealed class TransformState
    {
        public Vector3 position, scale;
        public Quaternion rotation;
        public bool isRect;
        public Vector2 min, max, pivot, size, anchoredPosition;
    }

    private static Dictionary<string, TransformState> Capture(Transform root)
    {
        return root.GetComponentsInChildren<Transform>(true).ToDictionary(node => RelativePath(root, node), node =>
        {
            var rect = node as RectTransform;
            return new TransformState
            {
                position = node.localPosition, scale = node.localScale, rotation = node.localRotation,
                isRect = rect != null, min = rect != null ? rect.anchorMin : Vector2.zero,
                max = rect != null ? rect.anchorMax : Vector2.zero, pivot = rect != null ? rect.pivot : Vector2.zero,
                size = rect != null ? rect.sizeDelta : Vector2.zero,
                anchoredPosition = rect != null ? rect.anchoredPosition : Vector2.zero
            };
        });
    }

    private static void Compare(Dictionary<string, TransformState> before, Dictionary<string, TransformState> after, string label)
    {
        Require(before.Count == after.Count, label + " hierarchy changed.");
        foreach (var pair in before)
        {
            TransformState first = pair.Value;
            Require(after.TryGetValue(pair.Key, out TransformState next) &&
                Vector3.Distance(first.position, next.position) < 0.0001f &&
                Vector3.Distance(first.scale, next.scale) < 0.0001f && Quaternion.Angle(first.rotation, next.rotation) < 0.001f &&
                first.isRect == next.isRect && (!first.isRect ||
                    Vector2.Distance(first.min, next.min) < 0.0001f && Vector2.Distance(first.max, next.max) < 0.0001f &&
                    Vector2.Distance(first.pivot, next.pivot) < 0.0001f && Vector2.Distance(first.size, next.size) < 0.0001f &&
                    Vector2.Distance(first.anchoredPosition, next.anchoredPosition) < 0.0001f),
                label + " transform changed: " + pair.Key);
        }
    }

    private static string RelativePath(Transform root, Transform node) => node == root ? "." :
        RelativePath(root, node.parent) + "/" + node.GetSiblingIndex() + ":" + node.name;

    private static string HierarchyPath(Transform node) => node.parent != null ?
        HierarchyPath(node.parent) + "/" + node.name : node.name;

    private static string Describe(Block dual) => "top order=" + dual.dualTopRenderer.sortingOrder +
        ", bottom order=" + dual.dualBottomRenderer.sortingOrder + ", sorting group=" +
        (dual.GetComponent<SortingGroup>() != null ? "yes" : "no");

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
