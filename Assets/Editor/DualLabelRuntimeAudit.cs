#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Reads live dual HP geometry without forcing a TMP rebuild or changing the scene.</summary>
[InitializeOnLoad]
public static class DualLabelRuntimeAudit
{
    private const string Request = "Temp/DualLabelRuntimeAudit.request";
    private const string Result = "Temp/DualLabelRuntimeAudit.result";
    private static bool running;
    static DualLabelRuntimeAudit() { EditorApplication.update += Poll; }
    private static void Poll()
    {
        if (running || !File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        running = true;
        try { Run(); }
        catch (Exception exception) { File.WriteAllText(Result, "AUDIT FAILED\n" + exception); }
        finally { File.Delete(Request); running = false; }
    }

    [MenuItem("Tools/Diagnostics/Read Live Dual HP Labels")]
    public static void Run()
    {
        var report = new StringBuilder();
        report.AppendLine("playing=" + Application.isPlaying + " editorPaused=" + EditorApplication.isPaused +
            " timeScale=" + Time.timeScale + " frame=" + Time.frameCount + " activeScene=" + SceneManager.GetActiveScene().name);
        if (!Application.isPlaying)
        {
            report.AppendLine("No live Play snapshot; no preview scene or TMP rebuild was requested.");
            File.WriteAllText(Result, report.ToString());
            return;
        }

        GridManager[] grids = UnityEngine.Object.FindObjectsByType<GridManager>(FindObjectsInactive.Include);
        foreach (GridManager grid in grids)
        {
            report.AppendLine("GRID " + Path(grid.transform) + " scene=" + grid.gameObject.scene.name +
                " dims=" + grid.width + "x" + grid.height + " scale=" + grid.transform.lossyScale);
            if (BoardViewport.TryGetBounds(grid, out Bounds board)) report.AppendLine("BOARD " + board.ToString("F5"));
            if (grid.dualBlockPrefab != null) DumpBlock(grid.dualBlockPrefab, report, "SOURCE TEMPLATE");
        }

        Block[] duals = UnityEngine.Object.FindObjectsByType<Block>(FindObjectsInactive.Include)
            .Where(block => block.blockType == BlockType.Dual || block.group != null && block.group.groupType == BlockType.Dual).ToArray();
        report.AppendLine("DUAL BLOCK COUNT " + duals.Length);
        foreach (Block block in duals.Take(24)) DumpBlock(block, report, "LIVE DUAL");
        report.AppendLine("No state was changed; ForceMeshUpdate was not called.");
        File.WriteAllText(Result, report.ToString());
    }

    private static void DumpBlock(Block block, StringBuilder report, string title)
    {
        report.AppendLine(title + " " + Path(block.transform) + " active=" + block.gameObject.activeInHierarchy +
            " type=" + block.blockType + " totalHP=" + (block.group != null ? block.group.totalHP.ToString() : "no group") +
            " refs hp=" + Name(block.hpText) + " top=" + Name(block.dualTopHPText) + " bottom=" + Name(block.dualBottomHPText) +
            " world=" + block.transform.position.ToString("F5") + " localScale=" + block.transform.localScale.ToString("F5") +
            " worldScale=" + block.transform.lossyScale.ToString("F5") + " clipComponent=" + (block.GetComponent<BoardBlockClipping>() != null));
        foreach (TMP_Text label in block.GetComponentsInChildren<TMP_Text>(true))
        {
            RectTransform rect = label.rectTransform;
            report.AppendLine(" LABEL " + Path(label.transform) + " class=" + label.GetType().Name + " text=" + label.text +
                " goActive=" + label.gameObject.activeInHierarchy + " enabled=" + label.enabled + " color=" + label.color +
                " fontSize=" + label.fontSize + " autoSize=" + label.enableAutoSizing + " alignment=" + label.alignment +
                " margin=" + label.margin + " rect=" + rect.rect + " anchors=" + rect.anchorMin + "/" + rect.anchorMax +
                " pivot=" + rect.pivot + " world=" + label.transform.position.ToString("F5") +
                " localScale=" + label.transform.localScale.ToString("F5") + " worldScale=" + label.transform.lossyScale.ToString("F5"));
            Material font = label.fontSharedMaterial;
            report.AppendLine("  FONT " + Mat(font) + " fontAsset=" + (label.font != null ? label.font.name : "null"));
            Renderer renderer = label is TextMeshPro worldText ? worldText.renderer : label.GetComponent<Renderer>();
            if (renderer != null)
            {
                var properties = new MaterialPropertyBlock(); renderer.GetPropertyBlock(properties);
                report.AppendLine("  RENDERER enabled=" + renderer.enabled + " visible=" + renderer.isVisible +
                    " sort=" + renderer.sortingLayerID + "/" + renderer.sortingOrder + " bounds=" + renderer.bounds.ToString("F5") +
                    " material=" + Mat(renderer.sharedMaterial) + " MPBclip=" + properties.GetVector("_ClipRect") +
                    " MPBsoft=" + properties.GetFloat("_MaskSoftnessX") + "/" + properties.GetFloat("_MaskSoftnessY"));
                if (font != null && font.HasProperty("_StencilComp"))
                    report.AppendLine("  STENCIL comp=" + font.GetFloat("_StencilComp") + " ref=" + font.GetFloat("_Stencil") +
                        " read=" + font.GetFloat("_StencilReadMask"));
            }
            MeshFilter filter = label.GetComponent<MeshFilter>();
            report.AppendLine("  ACTUAL MESH vertices=" + (filter != null && filter.sharedMesh != null ? filter.sharedMesh.vertexCount : 0));
            TMP_TextInfo info = label.textInfo;
            bool any = false; Bounds glyphBounds = default;
            if (info != null && info.characterInfo != null && info.meshInfo != null)
            {
                for (int i = 0; i < info.characterCount && i < info.characterInfo.Length; i++)
                {
                    TMP_CharacterInfo character = info.characterInfo[i];
                    if (!character.isVisible || character.materialReferenceIndex >= info.meshInfo.Length) continue;
                    Vector3[] vertices = info.meshInfo[character.materialReferenceIndex].vertices;
                    if (vertices == null || character.vertexIndex + 3 >= vertices.Length) continue;
                    for (int vertex = character.vertexIndex; vertex < character.vertexIndex + 4; vertex++)
                    {
                        Vector3 world = label.transform.TransformPoint(vertices[vertex]);
                        if (!any) { glyphBounds = new Bounds(world, Vector3.zero); any = true; }
                        else glyphBounds.Encapsulate(world);
                    }
                }
            }
            report.AppendLine("  TMP INFO chars=" + (info != null ? info.characterCount : 0) + " glyphBounds=" +
                (any ? glyphBounds.ToString("F5") : "no visible generated glyphs"));
        }
    }

    private static string Mat(Material material) => material == null ? "null" : material.name + " id=" + material.GetEntityId() +
        " shader=" + material.shader.name + " keywords=" + string.Join(",", material.shaderKeywords);
    private static string Name(Component component) => component == null ? "null" : Path(component.transform);
    private static string Path(Transform value)
    {
        string path = value.name;
        while (value.parent != null) { value = value.parent; path = value.name + "/" + path; }
        return path;
    }
}
#endif
