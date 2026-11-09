#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Inspects every authored scene before applying responsive display settings.</summary>
[InitializeOnLoad]
public static class ResponsiveDisplaySetup
{
    private const string Request = "Temp/ResponsiveDisplaySetup.request";
    private const string Result = "Temp/ResponsiveDisplaySetup.result";
    private const string Revision = "responsive-bounded-v4";
    private static bool running;

    static ResponsiveDisplaySetup() => EditorApplication.update += Poll;

    private static void Poll()
    {
        if (running || !File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        string mode = File.ReadAllText(Request).Trim();
        bool stopAndApply = mode == "stop-and-apply-" + Revision;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            // Only this explicitly requested, versioned mutation may leave Play Mode.
            // Retain its request so Apply runs after the authored scene is restored.
            if (stopAndApply) EditorApplication.isPlaying = false;
            return;
        }
        running = true;
        try { if (mode == "apply-" + Revision || stopAndApply) Apply(); else Audit(); }
        catch (Exception exception) { File.WriteAllText(Result, "FAIL\n" + exception); Debug.LogException(exception); }
        finally { File.Delete(Request); running = false; }
    }

    [MenuItem("Tools/Diagnostics/Audit Responsive Scene Layouts")]
    public static void Audit()
    {
        var report = new StringBuilder("VERSION=" + Revision + "\n");
        report.AppendLine("BUILD device=" + PlayerSettings.iOS.targetDevice + " fullscreen=" + PlayerSettings.iOS.requiresFullScreen
            + " orientation=" + PlayerSettings.defaultInterfaceOrientation + " size=" + PlayerSettings.defaultScreenWidth + "x" + PlayerSettings.defaultScreenHeight);
        foreach (EditorBuildSettingsScene build in EditorBuildSettings.scenes)
            report.AppendLine("BUILD SCENE enabled=" + build.enabled + " path=" + build.path);
        foreach (string path in ScenePaths())
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            bool preview = !scene.IsValid() || !scene.isLoaded;
            if (preview) scene = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                var nodes = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
                report.AppendLine("SCENE " + path + " preview=" + preview + " dirty=" + scene.isDirty);
                foreach (Camera camera in nodes.Select(node => node.GetComponent<Camera>()).Where(item => item != null))
                    report.AppendLine(" CAMERA " + Path(camera.transform) + " active=" + camera.gameObject.activeInHierarchy + " enabled=" + camera.enabled
                        + " ortho=" + camera.orthographic + " size=" + camera.orthographicSize + " rect=" + camera.rect + " aspect=" + camera.aspect
                        + " background=" + camera.backgroundColor + " clear=" + camera.clearFlags);
                foreach (Canvas canvas in nodes.Select(node => node.GetComponent<Canvas>()).Where(item => item != null))
                {
                    var scaler = canvas.GetComponent<CanvasScaler>();
                    report.AppendLine(" CANVAS " + Path(canvas.transform) + " active=" + canvas.gameObject.activeInHierarchy + " root=" + canvas.isRootCanvas
                        + " mode=" + canvas.renderMode + " scale=" + canvas.scaleFactor + " order=" + canvas.sortingOrder + RectDescription(canvas.transform));
                    if (scaler != null) report.AppendLine("  SCALER mode=" + scaler.uiScaleMode + " reference=" + scaler.referenceResolution
                        + " matchMode=" + scaler.screenMatchMode + " match=" + scaler.matchWidthOrHeight + " factor=" + scaler.scaleFactor);
                }
                foreach (AspectRatioFitter fitter in nodes.Select(node => node.GetComponent<AspectRatioFitter>()).Where(item => item != null))
                    report.AppendLine(" FITTER " + Path(fitter.transform) + " enabled=" + fitter.enabled + " active=" + fitter.gameObject.activeInHierarchy
                        + " mode=" + fitter.aspectMode + " aspect=" + fitter.aspectRatio + RectDescription(fitter.transform));
                foreach (Image image in nodes.Select(node => node.GetComponent<Image>()).Where(item => item != null && IsBackground(item.transform, item.sprite)))
                    report.AppendLine(" BACKGROUND IMAGE " + Path(image.transform) + " active=" + image.gameObject.activeInHierarchy
                        + " sprite=" + (image.sprite == null ? "null" : image.sprite.name) + " preserveAspect=" + image.preserveAspect + " color=" + image.color + RectDescription(image.transform));
                foreach (SpriteRenderer sprite in nodes.Select(node => node.GetComponent<SpriteRenderer>()).Where(item => item != null && IsBackground(item.transform, item.sprite)))
                    report.AppendLine(" BACKGROUND SPRITE " + Path(sprite.transform) + " active=" + sprite.gameObject.activeInHierarchy
                        + " sprite=" + (sprite.sprite == null ? "null" : sprite.sprite.name) + " bounds=" + sprite.bounds
                        + " position=" + sprite.transform.position + " scale=" + sprite.transform.localScale + " color=" + sprite.color);
                foreach (Transform root in scene.GetRootGameObjects().Select(item => item.transform))
                    report.AppendLine(" ROOT " + Path(root) + " active=" + root.gameObject.activeSelf + RectDescription(root));
                DumpGameplay(nodes, report);
            }
            finally { if (preview) EditorSceneManager.ClosePreviewScene(scene); }
        }
        File.WriteAllText(Result, report.ToString());
    }

    [MenuItem("Tools/Game/Apply Bounded Responsive Scene Layouts")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Apply responsive scene settings outside Play Mode after compilation.");
        var report = new StringBuilder("VERSION=" + Revision + "\n");
        Scene originalActive = SceneManager.GetActiveScene();
        foreach (string path in new[] { "Assets/Scenes/Start.unity", "Assets/Scenes/Home.unity", "Assets/Scenes/Game.unity" })
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                GameObject[] roots = scene.GetRootGameObjects();
                Transform[] originalObjects = roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
                GameplayGeometry gameplay = scene.name == "Game" ? new GameplayGeometry(originalObjects) : null;
                foreach (GameObject root in roots) Undo.RegisterFullObjectHierarchyUndo(root, "Apply responsive display layout");
                Camera[] cameras = originalObjects.Select(node => node.GetComponent<Camera>()).Where(camera => camera != null).ToArray();
                Camera main = cameras.FirstOrDefault(camera => camera.CompareTag("MainCamera")) ?? cameras.FirstOrDefault();
                if (main == null) throw new InvalidOperationException("No scene camera in " + path);
                float authoredCameraSize = main.orthographicSize;
                foreach (Camera camera in cameras)
                {
                    camera.rect = new Rect(0f, 0f, 1f, 1f);
                    camera.ResetAspect();
                    Color background = camera.backgroundColor;
                    background.a = 1f;
                    camera.backgroundColor = background;
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    EditorUtility.SetDirty(camera);
                }
                int canvasCount = 0;
                foreach (Canvas canvas in originalObjects.Select(node => node.GetComponent<Canvas>()).Where(canvas => canvas != null && canvas.isRootCanvas && canvas.renderMode != RenderMode.WorldSpace))
                {
                    CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                    if (scaler == null) scaler = Undo.AddComponent<CanvasScaler>(canvas.gameObject);
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = scene.name == "Home" ? new Vector2(1080f, 1920f) : PortraitDisplay.ReferenceResolutionForScene(scene);
                    scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                    scaler.matchWidthOrHeight = 1f;
                    EditorUtility.SetDirty(scaler);
                    canvasCount++;
                }
                // Save only the authored phone baseline. Device-specific geometry is
                // recomputed by the same helper at runtime and in isolated previews.
                main.aspect = PortraitDisplay.ReferenceScreenSize.x / PortraitDisplay.ReferenceScreenSize.y;
                try { ResponsiveSceneLayout.Apply(scene, main, PortraitDisplay.ReferenceScreenSize); }
                finally { main.ResetAspect(); }
                if (scene.name == "Game" && !Mathf.Approximately(main.orthographicSize, authoredCameraSize))
                    throw new InvalidOperationException("Phone authoring changed the saved Game camera framing.");
                if (gameplay != null) gameplay.Validate(report);
                if (originalObjects.Any(node => node == null))
                    throw new InvalidOperationException("Responsive setup removed existing scene objects in " + path);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save responsive settings in " + path);
                report.AppendLine("PASS " + path + " cameras=" + cameras.Length + " screenCanvases=" + canvasCount + " all authored objects retained; phone baseline saved.");
            }
            finally { if (openedHere) EditorSceneManager.CloseScene(scene, true); }
        }
        if (originalActive.IsValid() && originalActive.isLoaded && SceneManager.GetActiveScene() != originalActive)
            SceneManager.SetActiveScene(originalActive);
        PortraitDisplaySetup.Run();
        report.AppendLine("PASS Start, Home and Game bounded display baselines saved; authored gameplay positions retained. Other scene files were not edited.");
        File.WriteAllText(Result, report.ToString());
    }

    private static string[] ScenePaths() => Directory.GetFiles("Assets/Scenes", "*.unity")
        .Select(value => value.Replace('\\', '/')).OrderBy(value => value).ToArray();

    private sealed class GameplayGeometry
    {
        private readonly Canvas canvas;
        private readonly Dictionary<RectTransform, RectLayout> rectangles = new Dictionary<RectTransform, RectLayout>();
        private readonly Dictionary<Transform, Vector3> worldPositions = new Dictionary<Transform, Vector3>();
        private readonly Dictionary<GunGridManager, Vector2> spacing = new Dictionary<GunGridManager, Vector2>();

        public GameplayGeometry(Transform[] nodes)
        {
            canvas = nodes.Select(node => node.GetComponent<Canvas>()).Single(item => item != null && item.name == "Canvas" && item.isRootCanvas);
            foreach (RectTransform rect in canvas.GetComponentsInChildren<RectTransform>(true))
                if (rect != canvas.transform && rect.name != "Game Content") rectangles.Add(rect, new RectLayout(rect));
            foreach (TraySlot slot in nodes.Select(node => node.GetComponent<TraySlot>()).Where(item => item != null))
                worldPositions[slot.transform] = slot.transform.position;
            foreach (GunGridManager grid in nodes.Select(node => node.GetComponent<GunGridManager>()).Where(item => item != null))
            {
                spacing.Add(grid, new Vector2(grid.spacingX, grid.spacingY));
                if (grid.gridOrigin != null) worldPositions[grid.gridOrigin] = grid.gridOrigin.position;
            }
            foreach (GridManager grid in nodes.Select(node => node.GetComponent<GridManager>()).Where(item => item != null))
                if (grid.gridArea != null) worldPositions[grid.gridArea] = grid.gridArea.position;
        }

        public void Validate(StringBuilder report)
        {
            foreach (var item in rectangles)
                if (item.Key == null || !item.Value.Matches(item.Key))
                    throw new InvalidOperationException("Game foreground rectangle changed: " + (item.Key == null ? "missing object" : item.Key.name));
            foreach (var item in worldPositions)
                if (item.Key == null || (item.Key.position - item.Value).sqrMagnitude > .000001f)
                    throw new InvalidOperationException("Game board, tray or gun origin moved.");
            foreach (var item in spacing)
                if (item.Key == null || new Vector2(item.Key.spacingX, item.Key.spacingY) != item.Value)
                    throw new InvalidOperationException("Game gun grid spacing changed.");
            RectTransform content = canvas.transform.Find("Game Content") as RectTransform;
            if (content == null || !Mathf.Approximately(content.sizeDelta.x, 1080f) ||
                !Mathf.Approximately(content.anchorMin.x, .5f) || !Mathf.Approximately(content.anchorMax.x, .5f) ||
                !Mathf.Approximately(content.anchorMin.y, 0f) || !Mathf.Approximately(content.anchorMax.y, 1f))
                throw new InvalidOperationException("Game foreground requires a centered 1080-wide, height-stretched Game Content root.");
            report.AppendLine("PASS all " + rectangles.Count + " authored Game UI rectangles, tray/board positions and gun spacing unchanged; foreground width fixed at 1080.");
        }
    }

    private readonly struct RectLayout
    {
        private readonly Vector2 min, max, size, pivot;
        private readonly Vector3 position, scale;
        private readonly Quaternion rotation;
        public RectLayout(RectTransform rect)
        {
            min = rect.anchorMin; max = rect.anchorMax; size = rect.sizeDelta; pivot = rect.pivot;
            position = rect.anchoredPosition3D; scale = rect.localScale; rotation = rect.localRotation;
        }
        public bool Matches(RectTransform rect) => min == rect.anchorMin && max == rect.anchorMax &&
            (position - rect.anchoredPosition3D).sqrMagnitude < .000001f && (size - rect.sizeDelta).sqrMagnitude < .000001f &&
            pivot == rect.pivot && scale == rect.localScale && rotation == rect.localRotation;
    }

    private static void DumpGameplay(Transform[] nodes, StringBuilder report)
    {
        foreach (GridManager grid in nodes.Select(node => node.GetComponent<GridManager>()).Where(item => item != null))
        {
            report.AppendLine(" GRID " + Path(grid.transform) + " cells=" + grid.width + "x" + grid.height + " cellSize=" + grid.cellSize + " offsetY=" + grid.gridOffsetY);
            if (grid.gridArea != null)
                foreach (Transform node in grid.gridArea.GetComponentsInChildren<Transform>(true)) DumpWorld(node, report, "GRID AREA");
        }
        foreach (GunGridManager gun in nodes.Select(node => node.GetComponent<GunGridManager>()).Where(item => item != null))
        {
            report.AppendLine(" GUN GRID " + Path(gun.transform) + " cells=" + gun.columns + "x" + gun.rows + " spacing=" + gun.spacingX + "/" + gun.spacingY + " multiplier=" + gun.verticalSpacingMultiplier);
            if (gun.gridOrigin != null) DumpWorld(gun.gridOrigin, report, "GUN ORIGIN");
        }
        foreach (SpriteMask mask in nodes.Select(node => node.GetComponent<SpriteMask>()).Where(item => item != null))
            report.AppendLine(" MASK " + Path(mask.transform) + " active=" + mask.gameObject.activeInHierarchy + " sprite=" + (mask.sprite == null ? "null" : mask.sprite.name)
                + " spriteBounds=" + (mask.sprite == null ? "null" : mask.sprite.bounds.ToString()) + " position=" + mask.transform.position + " scale=" + mask.transform.lossyScale);
        foreach (Transform node in nodes.Where(node => node.name == "TopPanel" || node.name == "ItemPanel" || node.name == "Tray"))
        {
            DumpWorld(node, report, "GAMEPLAY ANCHOR");
            if (node.name == "Tray") foreach (Transform child in node) DumpWorld(child, report, "TRAY CHILD");
        }
        foreach (TraySlot slot in nodes.Select(node => node.GetComponent<TraySlot>()).Where(item => item != null))
        {
            DumpWorld(slot.transform, report, "TRAY SLOT");
            if (slot.slotPoint != null) DumpWorld(slot.slotPoint, report, "TRAY POINT");
            if (slot.slotInfo != null) DumpWorld(slot.slotInfo, report, "TRAY INFO");
        }
    }

    private static void DumpWorld(Transform node, StringBuilder report, string label)
    {
        report.AppendLine(" " + label + " " + Path(node) + " world=" + node.position + " scale=" + node.lossyScale + RectDescription(node));
        SpriteRenderer renderer = node.GetComponent<SpriteRenderer>();
        if (renderer != null) report.AppendLine("  SPRITE bounds=" + renderer.bounds + " sprite=" + (renderer.sprite == null ? "null" : renderer.sprite.name));
    }

    private static bool IsBackground(Transform transform, Sprite sprite)
    {
        string name = transform.name.ToLowerInvariant();
        string spriteName = sprite == null ? "" : sprite.name.ToLowerInvariant();
        if (name.Contains("background") || name == "bg" || name.StartsWith("bg ") || name.Contains("backdrop")
            || spriteName.Contains("background") || spriteName.StartsWith("bg")) return true;
        if (transform is RectTransform rect && rect.anchorMin == Vector2.zero && rect.anchorMax == Vector2.one)
            return transform.parent == null || transform.parent.GetComponent<Canvas>() != null;
        return false;
    }

    private static string RectDescription(Transform node) => node is RectTransform rect
        ? " anchors=" + rect.anchorMin + "/" + rect.anchorMax + " offsets=" + rect.offsetMin + "/" + rect.offsetMax
            + " pos=" + rect.anchoredPosition + " size=" + rect.sizeDelta + " rect=" + rect.rect + " scale=" + rect.localScale
        : " pos=" + node.localPosition + " scale=" + node.localScale;

    private static string Path(Transform node) => node.parent == null ? node.name : Path(node.parent) + "/" + node.name;
}
#endif
