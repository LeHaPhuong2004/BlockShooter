#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Inspects the Game scene's actual dual block without changing authored objects.</summary>
[InitializeOnLoad]
public static class DualBlockAudit
{
    private const string RequestPath = "Temp/DualBlockAudit.request";
    private const string FixedRequestPath = "Temp/DualBlockAudit.fixed.request";
    private const string ReportPath = "Temp/DualBlockAudit.result";
    private static bool running;

    static DualBlockAudit() => EditorApplication.update += ProcessRequest;

    private static void ProcessRequest()
    {
        if (running || (!File.Exists(RequestPath) && !File.Exists(FixedRequestPath)) || EditorApplication.isCompiling ||
            EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        running = true;
        try { Run(File.Exists(FixedRequestPath)); }
        catch (Exception exception)
        {
            File.WriteAllText(ReportPath, exception.ToString());
            Debug.LogException(exception);
        }
        finally { File.Delete(RequestPath); File.Delete(FixedRequestPath); running = false; }
    }

    [MenuItem("Tools/Diagnostics/Audit Dual Block Rendering")]
    public static void Run() => Run(false);

    [MenuItem("Tools/Diagnostics/Check Repaired Dual Block Rendering")]
    public static void RunFixedValidation() => Run(true);

    private static void Run(bool requireFixed)
    {
        Scene source = default;
        Scene preview = default;
        RenderTexture target = null;
        RenderTexture originalTarget = RenderTexture.active;
        try
        {
            var report = new StringBuilder();
            source = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Game.unity");
            var roots = source.GetRootGameObjects();
            var manager = roots.SelectMany(root => root.GetComponentsInChildren<GridManager>(true)).Single();
            Block template = manager.dualBlockPrefab;
            if (template == null) throw new InvalidOperationException("Game GridManager.dualBlockPrefab is null.");
            report.AppendLine("Game GridManager: " + Path(manager.transform));
            report.AppendLine("Dual template: " + Path(template.transform) + " asset=" + AssetDatabase.GetAssetPath(template) +
                " prefabSource=" + PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(template.gameObject));
            report.AppendLine("Top=" + ObjectPath(template.dualTopRenderer) + " Bottom=" + ObjectPath(template.dualBottomRenderer) +
                " sameReference=" + (template.dualTopRenderer == template.dualBottomRenderer));
            report.AppendLine("Body=" + ObjectPath(template.spriteRenderer) + " icon=" + ObjectPath(template.typeIconRenderer));
            Dump(template, report, "AUTHORED");
            if (requireFixed) ValidateFixedConfiguration(template);

            preview = EditorSceneManager.NewPreviewScene();
            GameObject copy = UnityEngine.Object.Instantiate(template.gameObject);
            SceneManager.MoveGameObjectToScene(copy, preview);
            copy.name = "Isolated Dual Audit";
            copy.transform.SetParent(null, false);
            copy.transform.position = Vector3.zero;
            copy.transform.rotation = Quaternion.identity;
            copy.SetActive(true);
            Block block = copy.GetComponent<Block>();
            block.group = null;
            var red = AssetDatabase.LoadAssetAtPath<BlockColorData>("Assets/Resources/BlockColorData/Data/Red.asset");
            var blue = AssetDatabase.LoadAssetAtPath<BlockColorData>("Assets/Resources/BlockColorData/Data/Blue.asset");
            if (red == null || blue == null) throw new InvalidOperationException("Missing Red/Blue block data.");
            block.SetDual(red, blue);
            block.UpdateTypeVisual();
            block.UpdateHP();
            if (requireFixed)
            {
                ValidateFixedConfiguration(block);
                Require(block.dualTopRenderer.sortingOrder == template.dualTopRenderer.sortingOrder &&
                    block.dualBottomRenderer.sortingOrder == template.dualBottomRenderer.sortingOrder,
                    "SetDual or visual/HP refresh changed the authored renderer sorting orders.");
            }
            foreach (var text in copy.GetComponentsInChildren<TMPro.TMP_Text>(true)) text.gameObject.SetActive(false);
            Dump(block, report, "AFTER SetDual(Red, Blue)");

            Camera sourceCamera = roots.SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                .First(camera => camera.CompareTag("MainCamera"));
            GameObject cameraCopy = UnityEngine.Object.Instantiate(sourceCamera.gameObject);
            SceneManager.MoveGameObjectToScene(cameraCopy, preview);
            cameraCopy.name = "Isolated Dual Camera";
            Camera camera = cameraCopy.GetComponent<Camera>();
            camera.enabled = false;
            camera.scene = preview;
            camera.rect = new Rect(0, 0, 1, 1);
            camera.ResetAspect();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 1f);
            var sprites = copy.GetComponentsInChildren<SpriteRenderer>(true).Where(renderer => renderer.enabled && renderer.sprite != null).ToArray();
            if (sprites.Length == 0) throw new InvalidOperationException("No enabled dual sprite renderer.");
            Bounds bounds = sprites[0].bounds;
            foreach (var renderer in sprites.Skip(1)) bounds.Encapsulate(renderer.bounds);
            camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, -10f);
            camera.transform.rotation = Quaternion.identity;
            camera.orthographicSize = Mathf.Max(0.1f, Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.35f);
            foreach (GameObject root in roots)
            {
                if (!root.GetComponentsInChildren<Component>(true).Any(component => component != null && component.GetType().Name == "Light2D")) continue;
                GameObject light = UnityEngine.Object.Instantiate(root);
                SceneManager.MoveGameObjectToScene(light, preview);
            }
            target = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32);
            target.Create();
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var pixels = new Texture2D(512, 512, TextureFormat.RGBA32, false);
            try
            {
                pixels.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
                pixels.Apply();
                Directory.CreateDirectory("Temp/DualBlockValidation");
                File.WriteAllBytes("Temp/DualBlockValidation/before.png", pixels.EncodeToPNG());
                int redCount = 0, blueCount = 0;
                foreach (Color color in pixels.GetPixels())
                {
                    if (color.r > 0.25f && color.r > color.b * 1.3f && color.r > color.g * 1.15f) redCount++;
                    if (color.b > 0.25f && color.b > color.r * 1.3f && color.b > color.g * 1.15f) blueCount++;
                }
                report.AppendLine("Render: redPixels=" + redCount + " bluePixels=" + blueCount +
                    " top=" + pixels.GetPixel(256, 330) + " bottom=" + pixels.GetPixel(256, 182));
                if (requireFixed)
                {
                    Require(redCount > 5000 && blueCount > 5000,
                        "The repaired scene template must already render both red and blue before any audit candidate changes.");
                    report.AppendLine("PASS: saved Game scene dual template renders both colors after SetDual, UpdateTypeVisual and UpdateHP; HP text stays above both halves.");
                    File.WriteAllBytes("Temp/DualBlockValidation/fixed-after.png", pixels.EncodeToPNG());
                }
                report.AppendLine("Preview saved: Temp/DualBlockValidation/before.png");
                foreach (SpriteRenderer renderer in sprites) renderer.maskInteraction = SpriteMaskInteraction.None;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
                pixels.Apply();
                File.WriteAllBytes("Temp/DualBlockValidation/control-no-masks.png", pixels.EncodeToPNG());
                redCount = blueCount = 0;
                foreach (Color color in pixels.GetPixels())
                {
                    if (color.r > 0.25f && color.r > color.b * 1.3f && color.r > color.g * 1.15f) redCount++;
                    if (color.b > 0.25f && color.b > color.r * 1.3f && color.b > color.g * 1.15f) blueCount++;
                }
                report.AppendLine("CONTROL no mask interaction: redPixels=" + redCount + " bluePixels=" + blueCount +
                    " top=" + pixels.GetPixel(256, 330) + " bottom=" + pixels.GetPixel(256, 182));
                var sortingGroup = copy.GetComponent<SortingGroup>();
                if (sortingGroup == null) sortingGroup = copy.AddComponent<SortingGroup>();
                sortingGroup.sortingLayerID = block.dualTopRenderer.sortingLayerID;
                sortingGroup.sortingOrder = 0;
                ConfigureCandidateHalf(block.dualTopRenderer, -3, -4, -2);
                ConfigureCandidateHalf(block.dualBottomRenderer, -1, -2, 0);
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
                pixels.Apply();
                File.WriteAllBytes("Temp/DualBlockValidation/candidate-after.png", pixels.EncodeToPNG());
                redCount = blueCount = 0;
                foreach (Color color in pixels.GetPixels())
                {
                    if (color.r > 0.25f && color.r > color.b * 1.3f && color.r > color.g * 1.15f) redCount++;
                    if (color.b > 0.25f && color.b > color.r * 1.3f && color.b > color.g * 1.15f) blueCount++;
                }
                report.AppendLine("CANDIDATE separate negative mask ranges + SortingGroup: redPixels=" + redCount +
                    " bluePixels=" + blueCount + " top=" + pixels.GetPixel(256, 330) + " bottom=" + pixels.GetPixel(256, 182));
                Dump(block, report, "CANDIDATE clone only");
            }
            finally { UnityEngine.Object.DestroyImmediate(pixels); }
            report.AppendLine("No authored scene, prefab, color data, or player data was saved.");
            File.WriteAllText(ReportPath, report.ToString());
            Debug.Log(report.ToString());
        }
        finally
        {
            RenderTexture.active = originalTarget;
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            if (source.IsValid()) EditorSceneManager.ClosePreviewScene(source);
            if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
        }
    }

    private static void Dump(Block block, StringBuilder report, string label)
    {
        report.AppendLine("\n" + label + " rootScale=" + block.transform.localScale + " type=" + block.blockType);
        foreach (var renderer in block.GetComponentsInChildren<SpriteRenderer>(true))
        {
            report.AppendLine("SPRITE " + Path(renderer.transform) + " enabled=" + renderer.enabled +
                " active=" + renderer.gameObject.activeSelf + " position=" + renderer.transform.localPosition +
                " scale=" + renderer.transform.localScale + " sprite=" + (renderer.sprite != null ? renderer.sprite.name : "none") +
                " bounds=" + renderer.bounds + " color=" + renderer.color + " mask=" + renderer.maskInteraction +
                " layer=" + renderer.sortingLayerName + "/" + renderer.sortingLayerID + " order=" + renderer.sortingOrder +
                " shader=" + (renderer.sharedMaterial != null ? renderer.sharedMaterial.shader.name : "none"));
        }
        foreach (var mask in block.GetComponentsInChildren<SpriteMask>(true))
        {
            report.AppendLine("MASK " + Path(mask.transform) + " enabled=" + mask.enabled + " active=" + mask.gameObject.activeSelf +
                " pos=" + mask.transform.localPosition + " rotation=" + mask.transform.localEulerAngles + " scale=" + mask.transform.localScale +
                " sprite=" + (mask.sprite != null ? mask.sprite.name : "none") + " asset=" + AssetDatabase.GetAssetPath(mask.sprite) +
                " spriteBounds=" + (mask.sprite != null ? mask.sprite.bounds.ToString() : "none") +
                " cutoff=" + mask.alphaCutoff + " custom=" + mask.isCustomRangeActive +
                " front=" + mask.frontSortingLayerID + "/" + mask.frontSortingOrder +
                " back=" + mask.backSortingLayerID + "/" + mask.backSortingOrder);
        }
        foreach (var group in block.GetComponentsInChildren<SortingGroup>(true))
            report.AppendLine("SORTING GROUP " + Path(group.transform) + " enabled=" + group.enabled + " layer=" + group.sortingLayerName + " order=" + group.sortingOrder);
        foreach (var mesh in block.GetComponentsInChildren<MeshRenderer>(true))
            report.AppendLine("MESH / HP TEXT " + Path(mesh.transform) + " enabled=" + mesh.enabled + " active=" + mesh.gameObject.activeSelf +
                " layer=" + mesh.sortingLayerName + "/" + mesh.sortingLayerID + " order=" + mesh.sortingOrder +
                " shader=" + (mesh.sharedMaterial != null ? mesh.sharedMaterial.shader.name : "none"));
    }

    private static void ConfigureCandidateHalf(SpriteRenderer renderer, int order, int back, int front)
    {
        if (renderer == null) throw new InvalidOperationException("Candidate dual renderer is missing.");
        SpriteMask mask = renderer.GetComponent<SpriteMask>();
        if (mask == null) throw new InvalidOperationException("Candidate half has no authored SpriteMask.");
        renderer.sortingOrder = order;
        renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        mask.enabled = true;
        mask.isCustomRangeActive = true;
        mask.backSortingLayerID = mask.frontSortingLayerID = renderer.sortingLayerID;
        mask.backSortingOrder = back;
        mask.frontSortingOrder = front;
    }

    private static void ValidateFixedConfiguration(Block block)
    {
        SortingGroup group = block.GetComponent<SortingGroup>();
        Require(group != null && group.enabled, "The dual root must isolate its masks with an enabled SortingGroup.");
        Require(block.dualTopRenderer != null && block.dualBottomRenderer != null &&
            block.dualTopRenderer != block.dualBottomRenderer, "The two color renderer references must be distinct.");
        ValidateHalf(block.dualTopRenderer, block.dualBottomRenderer);
        ValidateHalf(block.dualBottomRenderer, block.dualTopRenderer);
        int highestColor = Mathf.Max(block.dualTopRenderer.sortingOrder, block.dualBottomRenderer.sortingOrder);
        foreach (var text in new[] { block.dualTopHPText, block.dualBottomHPText })
        {
            Require(text != null, "Both authored HP text references must remain assigned.");
            var renderer = text.GetComponent<Renderer>();
            Require(renderer != null && renderer.sortingOrder > highestColor,
                "HP text must render above both colored halves.");
        }
    }

    private static void ValidateHalf(SpriteRenderer renderer, SpriteRenderer other)
    {
        SpriteMask mask = renderer.GetComponent<SpriteMask>();
        Require(renderer.maskInteraction == SpriteMaskInteraction.VisibleInsideMask && mask != null &&
            mask.enabled && mask.sprite != null && mask.isCustomRangeActive,
            "Each half must use its enabled authored SpriteMask.");
        Require(mask.frontSortingLayerID == renderer.sortingLayerID && mask.backSortingLayerID == renderer.sortingLayerID &&
            mask.backSortingOrder < renderer.sortingOrder && renderer.sortingOrder < mask.frontSortingOrder,
            "Each color renderer must lie strictly inside its own mask sorting range.");
        Require(other.sortingLayerID != renderer.sortingLayerID || other.sortingOrder <= mask.backSortingOrder ||
            other.sortingOrder >= mask.frontSortingOrder, "The two mask sorting ranges must not affect the other color.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static string ObjectPath(Component component) => component == null ? "null" : Path(component.transform);
    private static string Path(Transform transform) => transform.parent != null ? Path(transform.parent) + "/" + transform.name : transform.name;
}
#endif
