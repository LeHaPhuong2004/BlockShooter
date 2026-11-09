#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Reads the saved board's render/mask configuration without modifying it.</summary>
[InitializeOnLoad]
public static class BoardClipAudit
{
    private const string Request = "Temp/BoardClipAudit.request";
    private const string Result = "Temp/BoardClipAudit.result";
    private static bool running;
    static BoardClipAudit() { EditorApplication.update += Poll; }
    private static void Poll()
    {
        if (running || !File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        running = true;
        try { Run(); }
        catch (Exception exception) { File.WriteAllText(Result, exception.ToString()); Debug.LogException(exception); }
        finally { File.Delete(Request); running = false; }
    }
    [MenuItem("Tools/Diagnostics/Audit Board Clipping")]
    public static void Run()
    {
        Scene scene = default;
        try
        {
            scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Game.unity");
            GameObject[] roots = scene.GetRootGameObjects();
            var all = roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            var report = new StringBuilder();
            var manager = all.Select(t => t.GetComponent<GridManager>()).First(component => component != null);
            report.AppendLine("GridManager " + Path(manager.transform) + " width=" + manager.width + " height=" + manager.height + " cell=" + manager.cellSize);
            report.AppendLine("gridArea " + Path(manager.gridArea));
            DumpTransform(manager.gridArea, report);
            report.AppendLine("GRID AREA ANCESTORS AND CHILDREN");
            Transform ancestor = manager.gridArea;
            for (int i = 0; ancestor != null && i < 3; i++, ancestor = ancestor.parent)
            {
                DumpTransform(ancestor, report);
                foreach (Transform child in ancestor) DumpTransform(child, report);
            }
            report.AppendLine("ALL SPRITE MASKS");
            foreach (var mask in all.SelectMany(t => t.GetComponents<SpriteMask>()))
            {
                report.AppendLine(Path(mask.transform) + " active=" + mask.gameObject.activeInHierarchy + " enabled=" + mask.enabled + " alpha=" + mask.alphaCutoff + " customRange=" + mask.isCustomRangeActive + " back=" + mask.backSortingLayerID + "/" + mask.backSortingOrder + " front=" + mask.frontSortingLayerID + "/" + mask.frontSortingOrder + " sprite=" + AssetDatabase.GetAssetPath(mask.sprite) + " spriteBounds=" + (mask.sprite == null ? "null" : mask.sprite.bounds.ToString("F5")));
                DumpTransform(mask.transform, report);
            }
            report.AppendLine("ALL SORTING GROUPS");
            foreach (var group in all.SelectMany(t => t.GetComponents<SortingGroup>())) report.AppendLine(Path(group.transform) + " enabled=" + group.enabled + " sort=" + group.sortingLayerID + "/" + group.sortingOrder + " root=" + group.sortAtRoot);
            report.AppendLine("ALL SPRITE RENDERERS");
            foreach (var renderer in all.SelectMany(t => t.GetComponents<SpriteRenderer>())) DumpRenderer(renderer, report);
            report.AppendLine("NORMAL BLOCK");
            DumpBlock(manager.blockPrefab, report);
            report.AppendLine("DUAL BLOCK");
            DumpBlock(manager.dualBlockPrefab, report);
            File.WriteAllText(Result, report.ToString());
        }
        finally { if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene); }
    }
    private static void DumpBlock(Block block, StringBuilder report)
    {
        if (block == null) { report.AppendLine("NULL"); return; }
        report.AppendLine(Path(block.transform));
        foreach (var renderer in block.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is SpriteRenderer sprite) DumpRenderer(sprite, report);
            else report.AppendLine(Path(renderer.transform) + " type=" + renderer.GetType().Name + " enabled=" + renderer.enabled + " sort=" + renderer.sortingLayerID + "/" + renderer.sortingOrder + " worldBounds=" + renderer.bounds.ToString("F5") + " material=" + (renderer.sharedMaterial == null ? "null" : renderer.sharedMaterial.name + " shader=" + renderer.sharedMaterial.shader.name + " keywords=" + string.Join(",", renderer.sharedMaterial.shaderKeywords)));
        }
        foreach (TMP_Text label in block.GetComponentsInChildren<TMP_Text>(true)) report.AppendLine("TMP " + Path(label.transform) + " text=" + label.text + " font=" + label.font + " fontMat=" + label.fontSharedMaterial + " rect=" + label.rectTransform.rect);
    }
    private static void DumpRenderer(SpriteRenderer renderer, StringBuilder report)
    {
        report.AppendLine(Path(renderer.transform) + " active=" + renderer.gameObject.activeInHierarchy + " enabled=" + renderer.enabled + " mask=" + renderer.maskInteraction + " sort=" + renderer.sortingLayerID + "/" + renderer.sortingOrder + " worldBounds=" + renderer.bounds.ToString("F5") + " sprite=" + AssetDatabase.GetAssetPath(renderer.sprite) + " color=" + renderer.color + " material=" + (renderer.sharedMaterial == null ? "null" : renderer.sharedMaterial.name + " shader=" + renderer.sharedMaterial.shader.name));
    }
    private static void DumpTransform(Transform transform, StringBuilder report)
    {
        if (transform == null) { report.AppendLine("NULL Transform"); return; }
        report.AppendLine(Path(transform) + " world=" + transform.position.ToString("F5") + " scale=" + transform.lossyScale.ToString("F5") + " rotation=" + transform.eulerAngles.ToString("F5") + " components=" + string.Join(",", transform.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().Name)));
        var sprite = transform.GetComponent<SpriteRenderer>();
        if (sprite != null) DumpRenderer(sprite, report);
        var rect = transform as RectTransform;
        if (rect != null) { Vector3[] corners = new Vector3[4]; rect.GetWorldCorners(corners); report.AppendLine("rect=" + rect.rect + " corners=" + string.Join(";", corners.Select(v => v.ToString("F5")))); }
    }
    private static string Path(Transform transform) => transform == null ? "null" : transform.parent == null ? transform.name : Path(transform.parent) + "/" + transform.name;
}
#endif
