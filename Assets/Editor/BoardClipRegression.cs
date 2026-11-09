#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Exercises the saved board mask together with actual block and HP rendering.</summary>
[InitializeOnLoad]
public static class BoardClipRegression
{
    private const string Request = "Temp/BoardClipRegression.request";
    private const string Result = "Temp/BoardClipRegression.result";
    private const string Output = "Temp/BoardClipValidation";
    private const int ImageSize = 768;
    private static bool running;

    static BoardClipRegression() { EditorApplication.update += Poll; }

    private static void Poll()
    {
        if (running || !File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        running = true;
        try { Run(); }
        catch (Exception exception) { Debug.LogException(exception); }
        finally { File.Delete(Request); running = false; }
    }

    [MenuItem("Tools/Diagnostics/Check Board Sprite and HP Clipping")]
    public static void Run()
    {
        Scene source = default, preview = default;
        RenderTexture target = null;
        RenderTexture oldTarget = RenderTexture.active;
        var report = new StringBuilder();
        try
        {
            Directory.CreateDirectory(Output);
            source = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Game.unity");
            GameObject[] roots = source.GetRootGameObjects();
            var sourceGrid = roots.SelectMany(root => root.GetComponentsInChildren<GridManager>(true)).Single();
            SpriteMask sourceMask = sourceGrid.GetComponent<SpriteMask>();
            Require(sourceMask != null && sourceMask.enabled && sourceMask.sprite != null, "Saved Game board mask missing.");
            Bounds board = WorldBounds(sourceMask);
            report.AppendLine("Saved board mask: " + board.ToString("F5"));

            preview = EditorSceneManager.NewPreviewScene();
            var boardObject = new GameObject("Actual Game Board Mask", typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(boardObject, preview);
            boardObject.transform.position = sourceGrid.transform.position;
            boardObject.transform.rotation = sourceGrid.transform.rotation;
            boardObject.transform.localScale = sourceGrid.transform.lossyScale;
            var grid = boardObject.AddComponent<GridManager>();
            grid.width = sourceGrid.width;
            grid.height = sourceGrid.height;
            grid.cellSize = sourceGrid.cellSize;
            var mask = boardObject.AddComponent<SpriteMask>();
            EditorUtility.CopySerialized(sourceMask, mask);
            Require(WorldBounds(mask) == board, "The integration fixture changed the authored mask bounds.");

            GameObject area = UnityEngine.Object.Instantiate(sourceGrid.gridArea.gameObject);
            SceneManager.MoveGameObjectToScene(area, preview);
            area.transform.SetParent(null, false);
            area.transform.position = sourceGrid.gridArea.position;
            area.transform.rotation = sourceGrid.gridArea.rotation;
            area.transform.localScale = sourceGrid.gridArea.lossyScale;
            foreach (Renderer renderer in area.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            foreach (SpriteMask areaMask in area.GetComponentsInChildren<SpriteMask>(true)) areaMask.enabled = false;
            grid.gridArea = area.transform;

            var sourceCamera = roots.SelectMany(root => root.GetComponentsInChildren<Camera>(true)).First(camera => camera.CompareTag("MainCamera"));
            GameObject cameraObject = UnityEngine.Object.Instantiate(sourceCamera.gameObject);
            SceneManager.MoveGameObjectToScene(cameraObject, preview);
            var camera = cameraObject.GetComponent<Camera>();
            camera.enabled = false;
            camera.scene = preview;
            camera.rect = new Rect(0, 0, 1, 1);
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(board.extents.x, board.extents.y) + 0.7f;
            camera.aspect = 1;
            camera.transform.position = new Vector3(board.center.x, board.center.y, -10);
            camera.transform.rotation = Quaternion.identity;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.35f, 0.35f, 0.35f, 1);
            foreach (GameObject root in roots)
            {
                if (!root.GetComponentsInChildren<Component>(true).Any(component => component != null && component.GetType().Name == "Light2D")) continue;
                var light = UnityEngine.Object.Instantiate(root);
                SceneManager.MoveGameObjectToScene(light, preview);
            }
            target = new RenderTexture(ImageSize, ImageSize, 24, RenderTextureFormat.ARGB32);
            target.Create();
            camera.targetTexture = target;
            Color32[] baseline = Capture(camera, null);
            var red = AssetDatabase.LoadAssetAtPath<BlockColorData>("Assets/Resources/BlockColorData/Data/Red.asset");
            var blue = AssetDatabase.LoadAssetAtPath<BlockColorData>("Assets/Resources/BlockColorData/Data/Blue.asset");
            Require(red != null && blue != null, "Missing Red/Blue block colors.");

            foreach (BlockType kind in new[] { BlockType.Normal, BlockType.Mystery, BlockType.Dual })
            {
                Block template = kind == BlockType.Dual ? sourceGrid.dualBlockPrefab : sourceGrid.blockPrefab;
                Require(template != null, "Saved Game template missing for " + kind);
                Block block = UnityEngine.Object.Instantiate(template, boardObject.transform, false);
                block.name = "Board Test " + kind;
                block.transform.position = board.center;
                block.gameObject.SetActive(true);
                block.group = null;
                if (kind == BlockType.Dual) block.SetDual(red, blue);
                else { block.SetColor(red); block.SetBlockType(kind); }
                block.UpdateTypeVisual();
                block.UpdateHP();
                var transforms = block.GetComponentsInChildren<Transform>(true).Select(transform => new TransformSnapshot(transform)).ToArray();
                TMP_Text[] labels = block.GetComponentsInChildren<TMP_Text>(true);
                var originalMaterials = labels.Select(label => label.fontSharedMaterial).Where(material => material != null)
                    .Distinct().Select(material => new MaterialSnapshot(material)).ToArray();
                if (kind == BlockType.Dual)
                {
                    bool[] activeLabels = labels.Select(label => label.gameObject.activeSelf).ToArray();
                    foreach (TMP_Text label in labels) label.gameObject.SetActive(false);
                    RenderStats(camera, baseline, board, "Dual-before-repair-real-board", report);
                    for (int i = 0; i < labels.Length; i++) labels[i].gameObject.SetActive(activeLabels[i]);
                }
                BoardBlockClipping.Bind(block);
                var clipping = block.GetComponent<BoardBlockClipping>();
                Require(clipping != null, "Bind did not attach board clipping to " + kind);
                foreach (TransformSnapshot snapshot in transforms) snapshot.Check();

                foreach (TMP_Text label in labels) label.gameObject.SetActive(false);
                clipping.RefreshClipping();
                PixelStats inside = RenderStats(camera, baseline, board, kind + "-sprite-inside", report);
                Require(inside.foreground > 200, kind + " sprite must be visible inside the actual board mask.");
                Require(inside.outside <= 2, kind + " sprite escaped the board while inside.");
                if (kind == BlockType.Dual)
                    Require(inside.red > 200 && inside.blue > 200, "Dual must retain both colors under the actual board mask.");
                Bounds spriteBounds = SpriteBounds(block);
                Vector3 offset = block.transform.position - spriteBounds.center;
                var edges = new[]
                {
                    new Vector3(board.center.x, board.max.y, 0),
                    new Vector3(board.center.x, board.min.y, 0),
                    new Vector3(board.min.x, board.center.y, 0),
                    new Vector3(board.max.x, board.center.y, 0)
                };
                for (int edge = 0; edge < edges.Length; edge++)
                {
                    block.transform.position = edges[edge] + offset;
                    clipping.RefreshClipping();
                    PixelStats boundary = RenderStats(camera, baseline, board, kind + "-sprite-edge-" + edge, report);
                    Require(boundary.foreground > inside.foreground * 0.12f && boundary.foreground < inside.foreground * 0.88f,
                        kind + " sprite must be partially clipped at edge " + edge);
                    Require(boundary.outside <= 2, kind + " sprite leaked outside edge " + edge);
                }
                block.transform.position = new Vector3(board.center.x, board.max.y + spriteBounds.extents.y + 0.15f, 0) + offset;
                clipping.RefreshClipping();
                PixelStats outside = RenderStats(camera, baseline, board, kind + "-sprite-outside", report);
                Require(outside.foreground <= 2, kind + " sprite must disappear wholly outside board.");

                // HP is rendered on its own, so sprite masking cannot hide a text failure.
                foreach (SpriteRenderer sprite in block.GetComponentsInChildren<SpriteRenderer>(true)) sprite.enabled = false;
                foreach (TMP_Text label in labels)
                {
                    label.gameObject.SetActive(true);
                    label.text = "88";
                    label.color = Color.black;
                    label.ForceMeshUpdate(true, true);
                }
                Bounds textBounds = TextBounds(labels);
                report.AppendLine(kind + " HP forced glyph bounds: " + textBounds.ToString("F5"));
                offset = block.transform.position - textBounds.center;
                block.transform.position = board.center + offset;
                foreach (TMP_Text label in labels) label.ForceMeshUpdate(true, true);
                clipping.RefreshClipping();
                PixelStats textInside = RenderStats(camera, baseline, board, kind + "-hp-inside", report);
                Require(textInside.foreground > 15, kind + " HP must be visible inside board.");
                Require(textInside.outside <= 2, kind + " HP leaked while inside.");
                block.transform.position = new Vector3(board.center.x, board.max.y, 0) + offset;
                clipping.RefreshClipping();
                PixelStats textEdge = RenderStats(camera, baseline, board, kind + "-hp-edge", report);
                Require(textEdge.foreground > 0 && textEdge.foreground < textInside.foreground * 0.95f,
                    kind + " HP must be partially clipped at top edge after movement.");
                Require(textEdge.outside <= 2, kind + " HP leaked above board.");
                block.transform.position = new Vector3(board.center.x, board.max.y + textBounds.extents.y + 0.3f, 0) + offset;
                clipping.RefreshClipping();
                PixelStats textOutside = RenderStats(camera, baseline, board, kind + "-hp-outside", report);
                Require(textOutside.foreground <= 2, kind + " HP must disappear wholly outside board.");
                foreach (MaterialSnapshot snapshot in originalMaterials) snapshot.Check();
                UnityEngine.Object.DestroyImmediate(block.gameObject);
            }
            report.AppendLine("PASS: normal, mystery and dual sprites respect all four board edges; dual retains both colors; moving HP clips at the same board boundary. Bind preserved authored transforms and shared font assets.");
            report.AppendLine("No scene, prefab, color data or player data was saved.");
            File.WriteAllText(Result, report.ToString());
            Debug.Log(report.ToString());
        }
        catch (Exception exception)
        {
            File.WriteAllText(Result, report + "\nFAIL\n" + exception);
            throw;
        }
        finally
        {
            RenderTexture.active = oldTarget;
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            if (source.IsValid()) EditorSceneManager.ClosePreviewScene(source);
            if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
        }
    }

    private struct PixelStats { public int foreground, outside, red, blue; }

    private static PixelStats RenderStats(Camera camera, Color32[] baseline, Bounds board, string name, StringBuilder report)
    {
        Color32[] pixels = Capture(camera, name);
        var stats = new PixelStats();
        float unitsPerPixel = camera.orthographicSize * 2 / ImageSize;
        float left = camera.transform.position.x - camera.orthographicSize;
        float bottom = camera.transform.position.y - camera.orthographicSize;
        for (int y = 0; y < ImageSize; y++)
        {
            float worldY = bottom + (y + 0.5f) * unitsPerPixel;
            for (int x = 0; x < ImageSize; x++)
            {
                int index = y * ImageSize + x;
                Color32 color = pixels[index], empty = baseline[index];
                int delta = Mathf.Max(Mathf.Abs(color.r - empty.r), Mathf.Abs(color.g - empty.g), Mathf.Abs(color.b - empty.b));
                if (delta < 18) continue;
                stats.foreground++;
                float worldX = left + (x + 0.5f) * unitsPerPixel;
                if (worldX < board.min.x - unitsPerPixel || worldX > board.max.x + unitsPerPixel ||
                    worldY < board.min.y - unitsPerPixel || worldY > board.max.y + unitsPerPixel) stats.outside++;
                if (color.r > 65 && color.r > color.b * 1.3f && color.r > color.g * 1.15f) stats.red++;
                if (color.b > 65 && color.b > color.r * 1.3f && color.b > color.g * 1.15f) stats.blue++;
            }
        }
        report.AppendLine(name + ": pixels=" + stats.foreground + " outside=" + stats.outside + " red=" + stats.red + " blue=" + stats.blue);
        return stats;
    }

    private static Color32[] Capture(Camera camera, string name)
    {
        camera.Render();
        RenderTexture.active = camera.targetTexture;
        var texture = new Texture2D(ImageSize, ImageSize, TextureFormat.RGBA32, false);
        try
        {
            texture.ReadPixels(new Rect(0, 0, ImageSize, ImageSize), 0, 0);
            texture.Apply();
            if (name != null) File.WriteAllBytes(Output + "/" + name + ".png", texture.EncodeToPNG());
            return texture.GetPixels32();
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
    }

    private static Bounds SpriteBounds(Block block)
    {
        var renderers = block.GetComponentsInChildren<SpriteRenderer>(true).Where(renderer => renderer.enabled && renderer.sprite != null).ToArray();
        Require(renderers.Length > 0, "No active block sprites.");
        Bounds bounds = renderers[0].bounds;
        foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    private static Bounds TextBounds(IEnumerable<TMP_Text> labels)
    {
        // Renderer.bounds can remain from the previous Camera.Render in a preview
        // scene. Use the freshly generated glyph vertices in their actual local
        // transform instead, so an edge test really crosses the glyphs' center.
        Bounds bounds = default;
        bool any = false;
        foreach (TMP_Text label in labels)
        {
            TMP_TextInfo info = label.textInfo;
            for (int i = 0; i < info.characterCount; i++)
            {
                TMP_CharacterInfo character = info.characterInfo[i];
                if (!character.isVisible) continue;
                Vector3[] vertices = info.meshInfo[character.materialReferenceIndex].vertices;
                for (int vertex = character.vertexIndex; vertex < character.vertexIndex + 4; vertex++)
                {
                    Vector3 point = label.transform.TransformPoint(vertices[vertex]);
                    if (!any) { bounds = new Bounds(point, Vector3.zero); any = true; }
                    else bounds.Encapsulate(point);
                }
            }
        }
        Require(any, "No generated HP glyph vertices.");
        return bounds;
    }

    private static Bounds WorldBounds(SpriteMask mask)
    {
        Bounds local = mask.sprite.bounds;
        var bounds = new Bounds(mask.transform.TransformPoint(local.min), Vector3.zero);
        bounds.Encapsulate(mask.transform.TransformPoint(new Vector3(local.min.x, local.max.y, 0)));
        bounds.Encapsulate(mask.transform.TransformPoint(new Vector3(local.max.x, local.min.y, 0)));
        bounds.Encapsulate(mask.transform.TransformPoint(local.max));
        return bounds;
    }

    private sealed class TransformSnapshot
    {
        private readonly Transform transform;
        private readonly Vector3 position, scale;
        private readonly Quaternion rotation;
        public TransformSnapshot(Transform source) { transform = source; position = source.localPosition; scale = source.localScale; rotation = source.localRotation; }
        public void Check() { Require(transform.localPosition == position && transform.localScale == scale && transform.localRotation == rotation, "Bind moved authored transform " + transform.name); }
    }

    private sealed class MaterialSnapshot
    {
        private readonly Material material;
        private readonly string keywords;
        private readonly Vector4 clip;
        public MaterialSnapshot(Material source) { material = source; keywords = string.Join(";", source.shaderKeywords.OrderBy(keyword => keyword)); clip = source.HasProperty("_ClipRect") ? source.GetVector("_ClipRect") : Vector4.zero; }
        public void Check() { Require(keywords == string.Join(";", material.shaderKeywords.OrderBy(keyword => keyword)) && (!material.HasProperty("_ClipRect") || clip == material.GetVector("_ClipRect")), "Clipping modified shared font material " + material.name); }
    }

    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
#endif
