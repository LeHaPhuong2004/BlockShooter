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
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Checks full-screen responsive rendering and pointer alignment in isolated preview scenes.</summary>
[InitializeOnLoad]
public static class PortraitDisplayRegression
{
    private const string RequestPath = "Temp/PortraitDisplayRegression.request";
    private const string ReportPath = "Temp/PortraitDisplayRegression.result";
    private const string PreviewDirectory = "Temp/PortraitDisplayValidation";
    private const string Revision = "responsive-home-nav-v5";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static;
    private static readonly Vector2 Phone = new Vector2(1170f, 2532f);
    private static readonly Vector2 IPad = new Vector2(1536f, 2048f);
    private static readonly Vector2[] Sizes = { Phone, new Vector2(1080f, 2700f), IPad,
        new Vector2(1668f, 2388f), new Vector2(1600f, 2000f), new Vector2(2388f, 1668f), Phone };
    private static bool running;

    private sealed class TransformSnapshot
    {
        public Transform target;
        public Vector3 position, scale;
        public Quaternion rotation;
    }

    private sealed class RectSnapshot
    {
        public RectTransform target;
        public Vector2 min, max, position, size, pivot;
        public Vector3 scale;
    }

    private sealed class GameplaySnapshot
    {
        public TransformSnapshot[] transforms;
        public RectSnapshot[] controls;
        public Gun[] guns;
        public Vector3[] gunHomes;
        public GunGridManager[] grids;
        public Vector3[] spacing;
    }

    private sealed class HomeLayoutSnapshot
    {
        public readonly Dictionary<RectTransform, Vector2> cardSizes = new Dictionary<RectTransform, Vector2>();
        public readonly Dictionary<Transform, float> columnGaps = new Dictionary<Transform, float>();
        public readonly Dictionary<RectTransform, RectSnapshot> originalRects = new Dictionary<RectTransform, RectSnapshot>();
        public readonly Dictionary<RectTransform, float> edgeInsets = new Dictionary<RectTransform, float>();
        public readonly Dictionary<RectTransform, Rect> centeredRects = new Dictionary<RectTransform, Rect>();
        public readonly Dictionary<RectTransform, float> tabCenters = new Dictionary<RectTransform, float>();
        public readonly Dictionary<RectTransform, RectSnapshot> navigationIcons = new Dictionary<RectTransform, RectSnapshot>();
    }

    static PortraitDisplayRegression() => EditorApplication.update += ProcessRequest;

    private static void ProcessRequest()
    {
        if (running || !File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        running = true;
        try { if (File.ReadAllText(RequestPath).Trim() == "Settings") RunSettings(); else Run(); }
        catch (Exception exception) { File.WriteAllText(ReportPath, "FAIL\n" + exception); Debug.LogException(exception); }
        finally { File.Delete(RequestPath); running = false; }
    }

    [MenuItem("Tools/Diagnostics/Check Phone Sized Settings Popup")]
    public static void RunSettings()
    {
        Expect(!EditorApplication.isPlayingOrWillChangePlaymode, "Check Settings geometry outside Play Mode.");
        Scene preview = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Home.unity");
        RenderTexture previous = RenderTexture.active;
        var report = new StringBuilder("VERSION=settings-phone-size-v1\n");
        try
        {
            Transform[] nodes = preview.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            HomeExperienceUI home = nodes.Select(node => node.GetComponent<HomeExperienceUI>()).Single(item => item != null);
            Canvas canvas = nodes.Select(node => node.GetComponent<Canvas>()).Single(item => item != null && item.name == "Home Canvas");
            RectTransform popup = nodes.Single(node => node.name == "Settings Popup") as RectTransform;
            RectTransform window = nodes.Single(node => node.name == "Settings Window") as RectTransform;
            typeof(HomeExperienceUI).GetField("sceneHomeCanvas", Private).SetValue(home, canvas);
            typeof(HomeExperienceUI).GetField("settingsPopupRoot", Private).SetValue(home, popup.gameObject);
            typeof(HomeExperienceUI).GetField("settingsWindowRect", Private).SetValue(home, window);
            popup.gameObject.SetActive(true);
            var original = new HomeLayoutSnapshot();
            RectTransform[] children = window.GetComponentsInChildren<RectTransform>(true).Where(rect => rect != window).ToArray();
            foreach (RectTransform rect in children) ValidateOriginalHomeRect(rect, original);
            Camera camera = nodes.Select(node => node.GetComponent<Camera>()).First(item => item != null && item.CompareTag("MainCamera"));
            camera.scene = preview;
            Vector2 phoneLogicalSize = Vector2.zero;
            Vector2[] samples = { Phone, IPad, new Vector2(2224, 1668), Phone };
            Directory.CreateDirectory(PreviewDirectory);
            for (int index = 0; index < samples.Length; index++)
            {
                Vector2 size = samples[index];
                var target = new RenderTexture((int)size.x, (int)size.y, 24, RenderTextureFormat.ARGB32);
                try
                {
                    target.Create(); camera.targetTexture = target;
                    foreach (Canvas rootCanvas in nodes.Select(node => node.GetComponent<Canvas>()).Where(item => item != null && item.isRootCanvas))
                        PortraitDisplay.ApplyCanvasLayout(rootCanvas, camera, PortraitDisplay.ReferenceResolutionForScene(preview), size);
                    ResponsiveSceneLayout.Apply(preview, camera, size);
                    Canvas.ForceUpdateCanvases(); camera.Render();
                    typeof(HomeExperienceUI).GetMethod("UpdateSettingsPopupLayout", Private).Invoke(home, new object[] { true });
                    Canvas.ForceUpdateCanvases(); camera.Render(); Canvas.ForceUpdateCanvases(); camera.Render();
                    if (index == 0) phoneLogicalSize = window.rect.size;
                    Expect(Vector2.Distance(window.rect.size, phoneLogicalSize) < .2f, "Settings grew beyond its phone logical size at " + size + ": " + window.rect.size + " expected=" + phoneLogicalSize);
                    Rect displayed = VisualRectInFrame(popup, window);
                    Expect(Vector2.Distance(displayed.center, popup.rect.center) < .2f && displayed.xMin >= popup.rect.xMin - .2f && displayed.xMax <= popup.rect.xMax + .2f &&
                        displayed.yMin >= popup.rect.yMin - .2f && displayed.yMax <= popup.rect.yMax + .2f, "Settings is not centered and fitted inside its available viewport.");
                    foreach (RectTransform rect in children) ValidateOriginalHomeRect(rect, original);
                    Transform close = nodes.Single(node => node.name == "Settings Close Button");
                    Rect decor = default;
                    bool first = true;
                    foreach (Image image in close.GetComponentsInChildren<Image>(false).Where(image => image.enabled && image.sprite != null))
                    {
                        Rect draw = RenderedImageBounds(image);
                        foreach (Vector2 point in new[] { draw.min, new Vector2(draw.xMin, draw.yMax), draw.max, new Vector2(draw.xMax, draw.yMin) })
                        {
                            Vector2 local = popup.InverseTransformPoint(image.rectTransform.TransformPoint(point));
                            if (first) { decor = new Rect(local, Vector2.zero); first = false; }
                            else decor = Rect.MinMaxRect(Mathf.Min(decor.xMin, local.x), Mathf.Min(decor.yMin, local.y), Mathf.Max(decor.xMax, local.x), Mathf.Max(decor.yMax, local.y));
                        }
                    }
                    bool decorFits = decor.xMin >= popup.rect.xMin - .2f && decor.xMax <= popup.rect.xMax + .2f && decor.yMin >= popup.rect.yMin - .2f && decor.yMax <= popup.rect.yMax + .2f;
                    report.AppendLine("PASS " + size + " logicalWindow=" + window.rect.size + " centered=true; closeDrawBounds=" + decor + " available=" + popup.rect + " closeFits=" + decorFits);
                    if (index < samples.Length - 1) Capture(camera, target, "Settings-" + (int)size.x + "x" + (int)size.y + ".png");
                }
                finally { camera.targetTexture = null; RenderTexture.active = previous; target.Release(); UnityEngine.Object.DestroyImmediate(target); }
            }
            report.AppendLine("PASS phone/iPad/landscape/phone resizeback preserve authored Settings child rectangles. Three preview PNGs; no Home binding, preferences, toggle actions or scene saves.");
            File.WriteAllText(ReportPath, report.ToString());
        }
        finally { RenderTexture.active = previous; EditorSceneManager.ClosePreviewScene(preview); }
    }

    [MenuItem("Tools/Diagnostics/Check Responsive Display Layout")]
    public static void Run()
    {
        Expect(!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling, "Run responsive layout checks outside Play Mode after compilation.");
        Directory.CreateDirectory(PreviewDirectory);
        var report = new StringBuilder("VERSION=" + Revision + "\n");
        Expect(PlayerSettings.defaultInterfaceOrientation == UIOrientation.Portrait && PlayerSettings.allowedAutorotateToPortrait
            && !PlayerSettings.allowedAutorotateToPortraitUpsideDown && !PlayerSettings.allowedAutorotateToLandscapeLeft
            && !PlayerSettings.allowedAutorotateToLandscapeRight && PlayerSettings.iOS.requiresFullScreen,
            "Mobile builds must remain upright portrait and use the full iOS display.");
        foreach (Vector2 size in Sizes) ValidateViewportPolicy(size);
        report.AppendLine("PASS full viewport through iPad 4:3; wider displays use a centered iPad-width viewport and symmetric auxiliary sides.");
        string selection = File.Exists(RequestPath) ? File.ReadAllText(RequestPath).Trim() : "";
        string[] paths = Directory.GetFiles("Assets/Scenes", "*.unity").Select(path => path.Replace('\\', '/'))
            .OrderBy(path => path.EndsWith("/Home.unity") ? 0 : path.EndsWith("/Game.unity") ? 1 : path.EndsWith("/Start.unity") ? 2 : 3)
            .ThenBy(path => path).ToArray();
        Expect(paths.Length >= 6, "The responsive check must include build scenes and legacy scenes.");
        bool filter = paths.Any(path => Path.GetFileNameWithoutExtension(path) == selection);
        foreach (string path in paths)
        {
            if (filter && selection != Path.GetFileNameWithoutExtension(path)) continue;
            ValidateScene(path, report);
        }
        report.AppendLine("PASS preview-only rendering and isolated UI pointer callbacks; no gameplay, purchases, progression or scene saves.");
        File.WriteAllText(ReportPath, report.ToString());
        Debug.Log(report.ToString());
    }

    private static void ValidateScene(string path, StringBuilder report)
    {
        Scene preview = EditorSceneManager.OpenPreviewScene(path);
        RenderTexture previousTarget = RenderTexture.active;
        try
        {
            GameObject[] roots = preview.GetRootGameObjects();
            ValidateGraphicSchema(roots.SelectMany(root => root.GetComponentsInChildren<Graphic>(true)), "SAVED " + path);
            Camera camera = roots.SelectMany(root => root.GetComponentsInChildren<Camera>(true)).First(candidate => candidate.CompareTag("MainCamera"));
            camera.scene = preview;
            Canvas[] canvases = roots.SelectMany(root => root.GetComponentsInChildren<Canvas>(true))
                .Where(canvas => canvas.isRootCanvas && canvas.enabled && canvas.gameObject.activeInHierarchy && canvas.renderMode != RenderMode.WorldSpace).ToArray();
            EventSystem events = roots.SelectMany(root => root.GetComponentsInChildren<EventSystem>(true)).FirstOrDefault();
            if (events == null)
            {
                var eventObject = new GameObject("Responsive Preview Events", typeof(EventSystem));
                SceneManager.MoveGameObjectToScene(eventObject, preview);
                events = eventObject.GetComponent<EventSystem>();
            }
            HomeExperienceUI home = roots.SelectMany(root => root.GetComponentsInChildren<HomeExperienceUI>(true)).SingleOrDefault();
            Canvas homeCanvas = canvases.FirstOrDefault(canvas => canvas.name == "Home Canvas");
            if (home != null)
            {
                PrepareHomeTabs(home, homeCanvas);
                SetHomePage(homeCanvas, "Home Only Content");
            }
            Vector2 reference = PortraitDisplay.ReferenceResolutionForScene(preview);
            var boosterWidths = new Dictionary<RectTransform, float>();
            GameplaySnapshot originalGameplay = CaptureGameplay(roots);
            var homeLayout = home != null ? new HomeLayoutSnapshot() : null;
            for (int sample = 0; sample < Sizes.Length; sample++)
            {
                Vector2 size = Sizes[sample];
                var target = new RenderTexture((int)size.x, (int)size.y, 24, RenderTextureFormat.ARGB32);
                try
                {
                    target.Create();
                    camera.targetTexture = target;
                    typeof(PortraitDisplay).GetMethod("ApplyCameraLayout", Private).Invoke(null, new object[] { camera, reference, size });
                    foreach (Canvas canvas in canvases) PortraitDisplay.ApplyCanvasLayout(canvas, camera, reference, size);
                    Canvas.ForceUpdateCanvases();
                    ApplySceneLayout(preview, camera, size);
                    if (home != null) ConfigureHomePlayForPreview(home, homeCanvas, size);
                    Canvas.ForceUpdateCanvases();
                    Canvas.ForceUpdateCanvases();
                    // Camera-space Canvas matrices are synchronized during rendering after a resize.
                    camera.Render();
                    Rect expectedView = PortraitDisplay.CalculateViewport(size, reference);
                    Vector2 contentPixels = Vector2.Scale(size, expectedView.size);
                    Expect(RectMatches(camera.rect, expectedView) && Mathf.Abs(camera.aspect - contentPixels.x / contentPixels.y) < .0001f, path + " uses the wrong bounded content viewport at " + size);
                    ValidateUniformProjection(camera, size, path);
                    foreach (Canvas canvas in canvases)
                    {
                        bool fixedGameForeground = (preview.name == "Game" || preview.name == "Game 1") && canvas.name == "Canvas";
                        Vector2 expected = contentPixels / canvas.scaleFactor;
                        Vector2 actual = ((RectTransform)canvas.transform).rect.size;
                        Expect(Vector2.Distance(actual, expected) < 1.5f, path + " Canvas has the wrong content dimensions at " + size + ": " + actual + " expected=" + expected);
                        Expect(canvas.worldCamera != null && (canvas.renderMode == RenderMode.ScreenSpaceCamera || fixedGameForeground), path + " Canvas has no content camera.");
                        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                        Expect(scaler != null, path + " Canvas has no scaler.");
                        float scale = contentPixels.y / scaler.referenceResolution.y;
                        if (fixedGameForeground) scale = Mathf.Min(scale, contentPixels.x / reference.x);
                        else if (canvas.name == "Home Canvas")
                            scale = Mathf.Min(scale, contentPixels.x / (scaler.referenceResolution.y * Phone.x / Phone.y));
                        Expect(Mathf.Abs(canvas.scaleFactor - scale) < .001f, path + " Canvas does not preserve a uniform authored scale.");
                        RectTransform gameContent = canvas.transform.Find("Game Content") as RectTransform;
                        if (gameContent != null) Expect(Mathf.Abs(gameContent.rect.width - reference.x) < .5f, path + " changed the original Game foreground width.");
                    }
                    camera.Render();
                    RenderAuxiliaryPreview(preview);
                    ValidateAuxiliarySides(preview, expectedView);
                    ValidateBackgroundCoverage(roots, camera, size, path);
                    ValidateGameplay(roots, camera, size, boosterWidths, path);
                    ValidateGameplayUnchanged(originalGameplay, path, size);
                    camera.Render();
                    if (home != null)
                    {
                        ValidateHomeLayout(home, homeCanvas, camera, homeLayout);
                        ValidateButtonRaycast(homeCanvas.GetComponentsInChildren<Button>(true).First(button => button.name == "PLAY"), camera, events, path + " Play");
                        ValidateHomeResponsivePointers(homeCanvas, camera, events);
                        ValidateShopPointers(home, homeCanvas, camera, events);
                        ValidateNavigationIconSelection(home, homeCanvas, camera, homeLayout, size);
                    }
                    if (preview.name == "Game")
                    {
                        Button pause = roots.SelectMany(root => root.GetComponentsInChildren<Button>(true)).First(button => button.name == "pause");
                        ValidateButtonRaycast(pause, camera, events, "Game pause");
                    }
                    if (sample < Sizes.Length - 1 && (size == Phone || size == IPad || size == new Vector2(1668f, 2388f) || size == new Vector2(1600f, 2000f) || size == new Vector2(2388f, 1668f)))
                    {
                        Capture(camera, target, preview.name + "-" + (int)size.x + "x" + (int)size.y + ".png");
                        if (home != null && size == IPad)
                        {
                            SetHomePage(homeCanvas, "Shop Page");
                            SelectShop(home, false);
                            Capture(camera, target, "Home-Shop-1536x2048.png");
                            SelectShop(home, true);
                            Capture(camera, target, "Home-Booster-1536x2048.png");
                            SetHomePage(homeCanvas, "Home Only Content");
                        }
                    }
                }
                finally
                {
                    camera.targetTexture = null;
                    RenderTexture.active = previousTarget;
                    target.Release();
                    UnityEngine.Object.DestroyImmediate(target);
                }
            }
            report.AppendLine("PASS " + preview.name + ": bounded content, matching background coverage, uniform scale, original gun/tray/UI layout and pointer alignment across six sizes plus phone resize-back.");
            if (home != null) report.AppendLine("PASS Home navigation spans the bounded screen; profile/settings and level cards retain their edge insets and authored sizes; currency/banner/Play stay centered in fixed content, compact shop spacing stays fixed, and the visible rotated x10 hook remains at image UV (.88,.95).");
            if (home != null) report.AppendLine("PASS each selected navigation icon eases to 1.16x its authored scale, restores the others, and preserves tab/label/layout geometry across device sizes.");
        }
        finally
        {
            RenderTexture.active = previousTarget;
            EditorSceneManager.ClosePreviewScene(preview);
            Expect(!Resources.FindObjectsOfTypeAll<Canvas>().Any(canvas =>
                canvas != null && canvas.name == "Scene Background Extension" && !EditorUtility.IsPersistent(canvas) &&
                (!canvas.gameObject.scene.IsValid() || !canvas.gameObject.scene.isLoaded)),
                path + " leaked a background overlay after closing its preview scene.");
        }
    }

    private static void ApplySceneLayout(Scene scene, Camera camera, Vector2 size)
    {
        Type layout = typeof(PortraitDisplay).Assembly.GetType("ResponsiveSceneLayout");
        Expect(layout != null, "ResponsiveSceneLayout must apply the same scene layout path used at runtime.");
        layout.GetMethod("Apply", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { scene, camera, size });
    }

    private static void ValidateViewportPolicy(Vector2 screen)
    {
        Rect actual = PortraitDisplay.CalculateViewport(screen, PortraitDisplay.ReferenceScreenSize);
        float aspect = screen.x / screen.y;
        float width = Mathf.Min(1f, .75f / aspect);
        var expected = new Rect((1f - width) * .5f, 0f, width, 1f);
        Expect(RectMatches(actual, expected), "Incorrect iPad-width viewport on " + screen + ": " + actual);
        Expect(Mathf.Abs(actual.xMin - (1f - actual.xMax)) < .0001f, "Auxiliary sides are asymmetric on " + screen);
    }

    private static GameplaySnapshot CaptureGameplay(GameObject[] roots)
    {
        Gun[] guns = roots.SelectMany(root => root.GetComponentsInChildren<Gun>(true)).ToArray();
        GunGridManager[] grids = roots.SelectMany(root => root.GetComponentsInChildren<GunGridManager>(true)).ToArray();
        var transforms = new List<Transform>(guns.Select(gun => gun.transform));
        foreach (GunGridManager grid in grids)
        {
            transforms.Add(grid.transform);
            if (grid.gridOrigin != null) transforms.Add(grid.gridOrigin);
        }
        foreach (TraySlot slot in roots.SelectMany(root => root.GetComponentsInChildren<TraySlot>(true)))
        {
            transforms.Add(slot.transform);
            if (slot.slotInfo != null) transforms.Add(slot.slotInfo);
            if (slot.slotPoint != null) transforms.Add(slot.slotPoint);
        }
        RectTransform[] controls = roots.SelectMany(root => root.GetComponentsInChildren<RectTransform>(true))
            .Where(rect => rect.name == "TopPanel" || rect.name == "ItemPanel" ||
                (rect.parent != null && rect.parent.name == "ItemPanel" &&
                (rect.name == "Item1" || rect.name == "Item2" || rect.name == "Item3" || rect.name == "Item4"))).ToArray();
        return new GameplaySnapshot
        {
            transforms = transforms.Distinct().Select(target => new TransformSnapshot { target = target, position = target.position, rotation = target.rotation, scale = target.lossyScale }).ToArray(),
            controls = controls.Select(target => new RectSnapshot { target = target, min = target.anchorMin, max = target.anchorMax, position = target.anchoredPosition, size = target.sizeDelta, pivot = target.pivot, scale = target.localScale }).ToArray(),
            guns = guns, gunHomes = guns.Select(gun => gun.homePosition).ToArray(), grids = grids,
            spacing = grids.Select(grid => new Vector3(grid.spacingX, grid.spacingY, grid.verticalSpacingMultiplier)).ToArray()
        };
    }

    private static void ValidateGameplayUnchanged(GameplaySnapshot original, string scene, Vector2 size)
    {
        foreach (TransformSnapshot snapshot in original.transforms)
            Expect(snapshot.target != null && Vector3.Distance(snapshot.target.position, snapshot.position) < .0001f && Vector3.Distance(snapshot.target.lossyScale, snapshot.scale) < .0001f && Quaternion.Angle(snapshot.target.rotation, snapshot.rotation) < .001f, scene + " moved or resized an authored gun/tray transform at " + size + ": " + snapshot.target?.name);
        for (int index = 0; index < original.grids.Length; index++)
            Expect(Vector3.Distance(new Vector3(original.grids[index].spacingX, original.grids[index].spacingY, original.grids[index].verticalSpacingMultiplier), original.spacing[index]) < .0001f, scene + " changed authored gun grid spacing at " + size);
        for (int index = 0; index < original.guns.Length; index++)
            Expect(Vector3.Distance(original.guns[index].homePosition, original.gunHomes[index]) < .0001f, scene + " changed an authored gun home position at " + size);
        foreach (RectSnapshot snapshot in original.controls)
        {
            RectTransform rect = snapshot.target;
            Expect(rect != null && Vector2.Distance(rect.anchorMin, snapshot.min) < .0001f && Vector2.Distance(rect.anchorMax, snapshot.max) < .0001f && Vector2.Distance(rect.anchoredPosition, snapshot.position) < .0001f && Vector2.Distance(rect.sizeDelta, snapshot.size) < .0001f && Vector2.Distance(rect.pivot, snapshot.pivot) < .0001f && Vector3.Distance(rect.localScale, snapshot.scale) < .0001f, scene + " changed authored HUD/booster layout at " + size + ": " + rect?.name);
        }
    }

    private static void RenderAuxiliaryPreview(Scene scene)
    {
        Type extension = typeof(PortraitDisplay).Assembly.GetType("SceneBackgroundExtension");
        Expect(extension != null, "SceneBackgroundExtension is required for matching auxiliary sides.");
        extension.GetMethod("RenderPreview", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { scene });
    }

    private static void ValidateAuxiliarySides(Scene scene, Rect viewport)
    {
        GameObject root = scene.GetRootGameObjects().SingleOrDefault(candidate => candidate.name == "Scene Background Extension");
        if (viewport.xMin < .0001f)
        {
            Expect(root == null || !root.activeSelf, scene.name + " still shows auxiliary sides on a phone or iPad.");
            return;
        }
        Expect(root != null && root.activeSelf, scene.name + " has no matching auxiliary background on a wider display.");
        Canvas canvas = root.GetComponent<Canvas>();
        GraphicRaycaster raycaster = root.GetComponent<GraphicRaycaster>();
        Expect(canvas != null && !canvas.enabled && canvas.sortingOrder == short.MaxValue && raycaster != null && !raycaster.enabled, scene.name + " preview padding must not render or intercept input outside explicit preview rendering.");
        RawImage[] strips = root.GetComponentsInChildren<RawImage>(false);
        Expect(strips.Length == 2, scene.name + " needs two background side strips.");
        foreach (RawImage strip in strips)
        {
            bool left = strip.name == "Left Scene Background";
            Expect(left || strip.name == "Right Scene Background", "Unexpected auxiliary strip " + strip.name);
            RectTransform rect = strip.rectTransform;
            Vector2 min = left ? Vector2.zero : new Vector2(viewport.xMax, 0f);
            Vector2 max = left ? new Vector2(viewport.xMin, 1f) : Vector2.one;
            Expect(Vector2.Distance(rect.anchorMin, min) < .0001f && Vector2.Distance(rect.anchorMax, max) < .0001f && rect.offsetMin == Vector2.zero && rect.offsetMax == Vector2.zero, scene.name + " auxiliary strip overlaps content or leaves a side gap.");
            Expect(strip.raycastTarget && strip.texture != null && strip.color.a > 0f, scene.name + " auxiliary strip has no matching image or touch blocker.");
        }
        Expect(root.GetComponentsInChildren<Graphic>(false).Count(graphic => graphic.raycastTarget) == 2, scene.name + " auxiliary background has a blocker outside its two side strips.");
    }

    private static void ValidateUniformProjection(Camera camera, Vector2 size, string scene)
    {
        Vector3 origin = camera.transform.position + camera.transform.forward * 10f;
        Vector2 center = camera.WorldToScreenPoint(origin);
        float x = Vector2.Distance(center, camera.WorldToScreenPoint(origin + camera.transform.right));
        float y = Vector2.Distance(center, camera.WorldToScreenPoint(origin + camera.transform.up));
        Expect(Mathf.Abs(x - y) < .1f, scene + " stretches world sprites at " + size);
    }

    private static void ValidateBackgroundCoverage(GameObject[] roots, Camera camera, Vector2 size, string scene)
    {
        foreach (Image image in roots.SelectMany(root => root.GetComponentsInChildren<Image>(false)).Where(image => image.canvas != null && image.canvas.enabled && image.canvas.rootCanvas.enabled && image.GetComponent("BackgroundCover") != null))
        {
            Vector3[] corners = new Vector3[4];
            image.rectTransform.GetWorldCorners(corners);
            Vector2 min = camera.WorldToScreenPoint(corners[0]), max = camera.WorldToScreenPoint(corners[2]);
            Rect pixels = camera.pixelRect;
            Expect(min.x <= pixels.xMin + 1f && min.y <= pixels.yMin + 1f && max.x >= pixels.xMax - 1f && max.y >= pixels.yMax - 1f, scene + " background does not cover the content viewport: " + image.name + " at " + size + " bounds=" + min + "/" + max);
        }
        foreach (SpriteRenderer background in roots.SelectMany(root => root.GetComponentsInChildren<SpriteRenderer>(false)).Where(sprite => sprite.name == "BG"))
        {
            Vector3 min = camera.WorldToViewportPoint(background.bounds.min), max = camera.WorldToViewportPoint(background.bounds.max);
            Expect(min.x <= .001f && min.y <= .001f && max.x >= .999f && max.y >= .999f, scene + " world background leaves an uncovered edge at " + size);
        }
    }

    private static void ValidateGameplay(GameObject[] roots, Camera camera, Vector2 size, Dictionary<RectTransform, float> widths, string scene)
    {
        foreach (GridManager grid in roots.SelectMany(root => root.GetComponentsInChildren<GridManager>(true)))
        {
            SpriteRenderer frame = grid.gridArea != null ? grid.gridArea.GetComponent<SpriteRenderer>() : null;
            if (frame == null) continue;
            Vector3 min = camera.WorldToViewportPoint(frame.bounds.min), max = camera.WorldToViewportPoint(frame.bounds.max);
            Expect(min.x >= -.002f && min.y >= -.002f && max.x <= 1.002f && max.y <= 1.002f, scene + " board is clipped at " + size + ": " + min + "/" + max);
        }
        foreach (TraySlot slot in roots.SelectMany(root => root.GetComponentsInChildren<TraySlot>(true)))
        {
            SpriteRenderer renderer = slot.GetComponent<SpriteRenderer>();
            Bounds bounds = renderer != null ? renderer.bounds : new Bounds(slot.transform.position, Vector3.zero);
            Expect(camera.WorldToViewportPoint(bounds.min).x >= -.002f && camera.WorldToViewportPoint(bounds.max).x <= 1.002f, scene + " tray slot is outside the screen at " + size);
            Vector3 world = slot.slotPoint != null ? slot.slotPoint.position : slot.transform.position;
            Vector3 roundTrip = camera.ScreenToWorldPoint(camera.WorldToScreenPoint(world));
            Expect(Vector3.Distance(world, roundTrip) < .001f, scene + " gun/tray pointer projection is misaligned.");
        }
        foreach (RectTransform panel in roots.SelectMany(root => root.GetComponentsInChildren<RectTransform>(true)).Where(rect => rect.name == "ItemPanel"))
            foreach (Transform child in panel)
            {
                if (!(child is RectTransform item) || (item.name != "Item1" && item.name != "Item2" && item.name != "Item3" && item.name != "Item4")) continue;
                if (!widths.TryGetValue(item, out float original)) widths[item] = original = item.rect.width;
                Expect(item.rect.width > 0f && Mathf.Abs(item.rect.width - original) < .1f, scene + " stretches a booster button across tablet columns: " + item.name);
                Vector3 center = camera.WorldToViewportPoint(item.TransformPoint(item.rect.center));
                Expect(center.x > 0f && center.x < 1f && center.y > 0f && center.y < 1f, scene + " booster is outside the display at " + size + ": " + item.name + " viewport=" + center + " anchored=" + item.anchoredPosition);
            }
    }

    private static void ConfigureHomePlayForPreview(HomeExperienceUI home, Canvas canvas, Vector2 screenSize)
    {
        RectTransform play = canvas.GetComponentsInChildren<RectTransform>(true).Single(rect => rect.name == "PLAY");
        RectTransform badge = play.Find("Play Multiplier Badge") as RectTransform;
        Expect(badge != null, "Home must retain its authored x10 badge.");
        typeof(HomeExperienceUI).GetField("playButtonRect", Private).SetValue(home, play);
        // Reproduce a stale pivot left by a previous layout, then run the same
        // configuration that BindSceneHome performs after scene display setup.
        badge.position = play.TransformPoint(RenderedImageBounds(play.GetComponent<Image>()).center);
        typeof(HomeExperienceUI).GetMethod("ConfigurePlayButton", Private).Invoke(home, new object[] { play });
        home.ApplyResponsiveLayout(Vector2.Scale(screenSize, PortraitDisplay.CalculateViewport(screenSize, PortraitDisplay.ReferenceScreenSize).size));
    }

    private static void ValidateHomeLayout(HomeExperienceUI home, Canvas canvas, Camera camera, HomeLayoutSnapshot original)
    {
        RectTransform content = canvas.GetComponentsInChildren<RectTransform>(true).SingleOrDefault(rect => rect.name == "Home Content");
        float phoneWidth = 1920f * Phone.x / Phone.y;
        Expect(content != null && Mathf.Abs(content.rect.width - phoneWidth) < .1f, "Home foreground no longer keeps its original phone width.");
        Expect(Mathf.Abs(content.anchorMin.x - .5f) < .0001f && Mathf.Abs(content.anchorMax.x - .5f) < .0001f && content.anchorMin.y == 0f && content.anchorMax.y == 1f, "Home foreground must remain centered with its authored vertical layout.");
        ValidateHomeResponsiveEdges(canvas, content, original);
        RectTransform play = content.GetComponentsInChildren<RectTransform>(true).Single(rect => rect.name == "PLAY");
        RectTransform badge = play.Find("Play Multiplier Badge") as RectTransform;
        var serialized = new SerializedObject(home);
        SerializedProperty attachment = serialized.FindProperty("playMultiplierAttachmentUv");
        Expect(attachment != null && Vector2.Distance(attachment.vector2Value, new Vector2(.88f, .95f)) < .0001f, "The serialized x10 hook attachment changed.");
        SerializedProperty hook = serialized.FindProperty("playMultiplierHookUv");
        Expect(hook != null && Vector2.Distance(hook.vector2Value, new Vector2(.29f, .96f)) < .0001f, "The serialized visible hook location changed.");
        Vector2 contentPixels = camera.pixelRect.size;
        Quaternion rotation = badge.localRotation;
        Vector2 pivot = badge.pivot, badgeSize = badge.sizeDelta;
        Vector3 badgeScale = badge.localScale;
        try
        {
            foreach (Vector2 authoredPivot in new[] { pivot, new Vector2(.5f, .5f) })
                foreach (float swing in new[] { -5f, 0f, 5f })
                {
                    badge.pivot = authoredPivot;
                    Quaternion expectedRotation = rotation * Quaternion.Euler(0f, 0f, swing);
                    badge.localRotation = expectedRotation;
                    Rect bounds = RenderedImageBounds(play.GetComponent<Image>());
                    badge.position = play.TransformPoint(bounds.center);
                    home.ApplyResponsiveLayout(contentPixels);
                    Vector2 hookLocal = badge.rect.min + Vector2.Scale(badge.rect.size, hook.vector2Value);
                    Vector3 visibleHook = play.InverseTransformPoint(badge.TransformPoint(hookLocal));
                    var uv = new Vector2((visibleHook.x - bounds.xMin) / bounds.width, (visibleHook.y - bounds.yMin) / bounds.height);
                    Expect(Vector2.Distance(uv, attachment.vector2Value) < .001f, "Visible x10 hook moved away from the Play art attachment: " + uv + " expected=" + attachment.vector2Value + " swing=" + swing + " pivot=" + authoredPivot);
                    Expect(Vector2.Distance(badge.pivot, authoredPivot) < .0001f && Vector2.Distance(badge.sizeDelta, badgeSize) < .0001f && Vector3.Distance(badge.localScale, badgeScale) < .0001f && Quaternion.Angle(badge.localRotation, expectedRotation) < .001f, "Hook attachment changed authored badge pivot, size, scale or rotation.");
                }
        }
        finally
        {
            badge.pivot = pivot;
            badge.sizeDelta = badgeSize;
            badge.localScale = badgeScale;
            badge.localRotation = rotation;
            home.ApplyResponsiveLayout(contentPixels);
        }

        try
        {
            SetHomePage(canvas, "Shop Page");
            foreach (bool booster in new[] { false, true })
            {
                SelectShop(home, booster);
                Canvas.ForceUpdateCanvases(); camera.Render();
                Transform viewport = canvas.GetComponentsInChildren<Transform>(true).Single(node => node.name == (booster ? "Booster Shop Viewport" : "Gold Shop Viewport"));
                Expect(viewport.IsChildOf(content), "Shop escaped the fixed Home foreground.");
                Transform[] layers = viewport.GetComponentsInChildren<Transform>(true).Where(node => node.name == "Shop Card Frames" && !IsArchivedShopTemplate(node)).ToArray();
                Expect(layers.Length > 0, "Shop has no authored card frames.");
                foreach (Transform layer in layers)
                {
                    RectTransform[] cards = layer.Cast<Transform>().OfType<RectTransform>().Where(rect => rect.gameObject.activeSelf && rect.GetComponent<Image>()?.sprite != null).ToArray();
                    Expect(cards.Length >= 2, "Shop card grid no longer has two columns.");
                    foreach (RectTransform card in cards) ValidateShopVisualSize(content, card, original);
                    Rect left = VisualRectInFrame(content, cards[0]), right = VisualRectInFrame(content, cards[1]);
                    float gap = Mathf.Abs(right.center.x - left.center.x) - (left.width + right.width) * .5f;
                    if (!original.columnGaps.TryGetValue(layer, out float expected)) original.columnGaps[layer] = expected = gap;
                    Expect(Mathf.Abs(gap - expected) < .15f, "Shop columns spread apart after a device resize: " + GraphicPath(layer) + " gap=" + gap + " expected=" + expected);
                }
                if (booster)
                    foreach (RectTransform package in viewport.GetComponentsInChildren<RectTransform>(true).Where(rect => rect.parent != null && rect.parent.name == "Booster Offer Packages" && rect.GetComponent<Image>()?.sprite != null))
                        ValidateShopVisualSize(content, package, original);
            }
        }
        finally
        {
            SelectShop(home, false);
            SetHomePage(canvas, "Home Only Content");
        }
        ValidateHomeResponsiveEdges(canvas, content, original);
    }

    private static void ValidateHomeResponsiveEdges(Canvas canvas, RectTransform content, HomeLayoutSnapshot original)
    {
        RectTransform home = content.parent as RectTransform;
        RectTransform Find(string name) => canvas.GetComponentsInChildren<RectTransform>(true).Single(rect => rect.name == name);
        float phoneWidth = 1920f * Phone.x / Phone.y;
        foreach (string name in new[] { "Home Screen Edges", "Level Screen Edges" })
        {
            RectTransform edges = Find(name);
            Rect bounds = VisualRectInFrame(home, edges);
            Expect(Mathf.Abs(bounds.xMin - home.rect.xMin) < .2f && Mathf.Abs(bounds.xMax - home.rect.xMax) < .2f && edges.GetComponent<Graphic>() == null, name + " does not cover the bounded Home width without blocking input.");
        }
        foreach (string name in new[] { "Left HUD Edge", "Right HUD Edge", "Left Level Edge", "Right Level Edge" })
        {
            RectTransform host = Find(name);
            Expect(Mathf.Abs(host.rect.width - phoneWidth) < .1f && host.GetComponent<Graphic>() == null, name + " changed authored control sizing or added an input blocker.");
        }
        foreach (var pair in new[] { (name: "PROFILE", host: "Left HUD Edge", left: true),
            (name: "SETTINGS", host: "Right HUD Edge", left: false),
            (name: "LOCKED_30", host: "Left Level Edge", left: true),
            (name: "LOCKED_25", host: "Left Level Edge", left: true),
            (name: "LOCKED_50", host: "Right Level Edge", left: false) })
        {
            RectTransform rect = Find(pair.name);
            Expect(rect.IsChildOf(Find(pair.host)), pair.name + " is not attached to the appropriate screen edge.");
            Rect bounds = VisualRectInFrame(home, rect);
            float inset = pair.left ? bounds.xMin - home.rect.xMin : home.rect.xMax - bounds.xMax;
            if (!original.edgeInsets.TryGetValue(rect, out float expected)) original.edgeInsets[rect] = expected = inset;
            Expect(inset >= -.2f && Mathf.Abs(inset - expected) < .2f, pair.name + " lost its screen-edge inset after resizing: " + inset + " expected=" + expected);
            ValidateOriginalHomeRect(rect, original);
        }
        foreach (string name in new[] { "COINS", "ENERGY", "LEVEL_BANNER", "PLAY" })
        {
            RectTransform rect = Find(name);
            ValidateOriginalHomeRect(rect, original);
            Rect bounds = VisualRectInFrame(content, rect);
            var normalized = new Rect(bounds.x, bounds.y / content.rect.height, bounds.width, bounds.height / content.rect.height);
            if (!original.centeredRects.TryGetValue(rect, out Rect expected)) original.centeredRects[rect] = expected = normalized;
            Expect(Vector2.Distance(normalized.position, expected.position) < .2f && Vector2.Distance(normalized.size, expected.size) < .2f, name + " moved or stretched while the edge controls expanded.");
        }
        RectTransform capsule = Find("LEVEL_BANNER").Find("Banner Text Capsule") as RectTransform;
        Expect(capsule != null, "Home lost its lavender banner.");
        ValidateOriginalHomeRect(capsule, original);
        RectTransform navigation = Find("Bottom Navigation");
        ValidateOriginalHomeRect(navigation, original);
        Rect navBounds = VisualRectInFrame(home, navigation);
        Expect(Mathf.Abs(navBounds.xMin - home.rect.xMin) < .2f && Mathf.Abs(navBounds.xMax - home.rect.xMax) < .2f, "Home bottom navigation does not span the bounded screen width.");
        RectTransform footer = navigation.Find("Footer Sprite") as RectTransform;
        Rect footerBounds = VisualRectInFrame(home, footer);
        Expect(Mathf.Abs(footerBounds.xMin - home.rect.xMin) < .2f && Mathf.Abs(footerBounds.xMax - home.rect.xMax) < .2f, "Footer artwork no longer spans the navigation bar.");
        foreach (string name in new[] { "SHOP", "MISSIONS", "HOME_TAB", "ACHIEVEMENTS", "CHARACTER" })
        {
            RectTransform rect = navigation.GetComponentsInChildren<RectTransform>(true).Single(node => node.name == name);
            ValidateOriginalHomeRect(rect, original);
            Rect bounds = VisualRectInFrame(navigation, rect);
            float x = Mathf.InverseLerp(navigation.rect.xMin, navigation.rect.xMax, bounds.center.x);
            if (!original.tabCenters.TryGetValue(rect, out float expected)) original.tabCenters[rect] = expected = x;
            Expect(Mathf.Abs(x - expected) < .001f, name + " changed its authored navigation distribution: actual=" + x + " expected=" + expected + " anchors=" + rect.anchorMin + "/" + rect.anchorMax + " anchored=" + rect.anchoredPosition + " rect=" + rect.rect + " navWidth=" + navigation.rect.width);
            Image icon = rect.GetComponentsInChildren<Image>(true).FirstOrDefault(image => image.name.EndsWith(" Tab Icon", StringComparison.Ordinal));
            Expect(icon != null && icon.preserveAspect, name + " lost its undistorted icon artwork.");
        }
    }

    private static void ValidateOriginalHomeRect(RectTransform rect, HomeLayoutSnapshot original)
    {
        if (!original.originalRects.TryGetValue(rect, out RectSnapshot baseline))
        {
            baseline = new RectSnapshot { target = rect, min = rect.anchorMin, max = rect.anchorMax,
                position = rect.anchoredPosition, size = rect.sizeDelta, pivot = rect.pivot, scale = rect.localScale };
            original.originalRects[rect] = baseline;
        }
        Expect(Vector2.Distance(rect.anchorMin, baseline.min) < .0001f && Vector2.Distance(rect.anchorMax, baseline.max) < .0001f && Vector2.Distance(rect.anchoredPosition, baseline.position) < .0001f && Vector2.Distance(rect.sizeDelta, baseline.size) < .0001f && Vector2.Distance(rect.pivot, baseline.pivot) < .0001f && Vector3.Distance(rect.localScale, baseline.scale) < .0001f, rect.name + " changed authored geometry while responsive hosts resized.");
    }

    private static void ValidateHomeResponsivePointers(Canvas canvas, Camera camera, EventSystem events)
    {
        foreach (string name in new[] { "PROFILE", "SETTINGS", "LOCKED_30", "LOCKED_25", "LOCKED_50", "SHOP", "MISSIONS", "HOME_TAB", "ACHIEVEMENTS", "CHARACTER" })
        {
            Button button = canvas.GetComponentsInChildren<Button>(true).Single(candidate => candidate.name == name);
            ValidateButtonRaycast(button, camera, events, "Home responsive " + name);
        }
    }

    private static Rect RenderedImageBounds(Image image)
    {
        Rect bounds = image.GetPixelAdjustedRect();
        Sprite sprite = image.overrideSprite != null ? image.overrideSprite : image.sprite;
        if (!image.preserveAspect || sprite == null) return bounds;
        float aspect = sprite.rect.width / sprite.rect.height;
        if (aspect > bounds.width / bounds.height)
        {
            float height = bounds.width / aspect;
            bounds.y += (bounds.height - height) * image.rectTransform.pivot.y;
            bounds.height = height;
        }
        else
        {
            float width = bounds.height * aspect;
            bounds.x += (bounds.width - width) * image.rectTransform.pivot.x;
            bounds.width = width;
        }
        return bounds;
    }

    private static bool IsArchivedShopTemplate(Transform node)
    {
        for (Transform ancestor = node; ancestor != null; ancestor = ancestor.parent)
            if (ancestor.name == "Original Booster Offer Templates") return true;
        return false;
    }

    private static Rect VisualRectInFrame(RectTransform frame, RectTransform target)
    {
        var corners = new Vector3[4];
        target.GetWorldCorners(corners);
        Vector2 min = frame.InverseTransformPoint(corners[0]), max = min;
        for (int index = 1; index < corners.Length; index++)
        {
            Vector2 point = frame.InverseTransformPoint(corners[index]);
            min = Vector2.Min(min, point); max = Vector2.Max(max, point);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private static void ValidateShopVisualSize(RectTransform frame, RectTransform card, HomeLayoutSnapshot original)
    {
        Vector2 size = VisualRectInFrame(frame, card).size;
        Expect(size.x > 1f && size.y > 1f, "Shop card has no visual area: " + GraphicPath(card));
        if (!original.cardSizes.TryGetValue(card, out Vector2 expected)) original.cardSizes[card] = expected = size;
        Expect(Vector2.Distance(size, expected) < .2f, "Shop card changed visual size after a device resize: " + GraphicPath(card) + " size=" + size + " expected=" + expected);
    }

    private static void PrepareHomeTabs(HomeExperienceUI home, Canvas canvas)
    {
        Expect(canvas != null, "Home requires its authored Home Canvas.");
        Transform Find(string name) => canvas.GetComponentsInChildren<Transform>(true).First(node => node.name == name);
        Button gold = Find("SHOP_GOLD_TAB").GetComponent<Button>(), booster = Find("SHOP_BOOSTER_TAB").GetComponent<Button>();
        Image Face(Button button) => button.transform.Find("Tab Sprite")?.GetComponent<Image>();
        void Set(string name, object value) => typeof(HomeExperienceUI).GetField(name, Private).SetValue(home, value);
        Set("homeCanvasObject", canvas.gameObject);
        string[] tabNames = { "HOME_TAB", "SHOP", "MISSIONS", "ACHIEVEMENTS", "CHARACTER" };
        Set("tabIconRects", tabNames.Select(name => Find(name).GetComponentsInChildren<RectTransform>(true)
            .Single(rect => rect.name.EndsWith(" Tab Icon", StringComparison.Ordinal))).ToArray());
        Set("selectedTabBlock", Find("Selected Tab").GetComponent<RectTransform>());
        Set("tabPages", new[] { "Home Only Content", "Shop Page", "Daily Missions Page", "Achievements Page", "Character Page" }
            .Select(name => Find(name).gameObject).ToArray());
        Set("shopGoldTabButton", gold); Set("shopBoosterTabButton", booster);
        Set("shopGoldTabImage", Face(gold)); Set("shopBoosterTabImage", Face(booster));
        Set("shopGoldViewport", Find("Gold Shop Viewport").gameObject); Set("shopBoosterViewport", Find("Booster Shop Viewport").gameObject);
        Set("shopTabSelectedSprite", Resources.Load<Sprite>("UI/ShopItems/ShopTabSelected"));
        Set("shopTabIdleSprite", Resources.Load<Sprite>("UI/ShopItems/ShopTabIdle"));
        MethodInfo prepare = typeof(HomeExperienceUI).GetMethod("PrepareAuthoredShopTab", Private);
        prepare.Invoke(home, new object[] { gold, Face(gold) }); prepare.Invoke(home, new object[] { booster, Face(booster) });
    }

    private static void ValidateNavigationIconSelection(HomeExperienceUI home, Canvas canvas, Camera camera, HomeLayoutSnapshot original, Vector2 size)
    {
        var icons = (RectTransform[])typeof(HomeExperienceUI).GetField("tabIconRects", Private).GetValue(home);
        MethodInfo select = typeof(HomeExperienceUI).GetMethod("SelectTab", Private);
        MethodInfo animate = typeof(HomeExperienceUI).GetMethod("UpdateNavigationIconSelection", Private);
        foreach (RectTransform icon in icons)
            if (!original.navigationIcons.ContainsKey(icon)) original.navigationIcons[icon] = new RectSnapshot
            {
                target = icon, min = icon.anchorMin, max = icon.anchorMax, position = icon.anchoredPosition,
                size = icon.sizeDelta, pivot = icon.pivot, scale = icon.localScale
            };
        foreach (int selection in new[] { 1, 2, 3, 4, 0, 1, 0 })
        {
            select.Invoke(home, new object[] { selection });
            var labels = canvas.GetComponentsInChildren<Text>(true).Where(label => label.transform.parent != null &&
                label.transform.parent.parent != null && label.transform.parent.parent.name == "Bottom Navigation")
                .Select(label => (label, size: label.fontSize, scale: label.rectTransform.localScale)).ToArray();
            animate.Invoke(home, new object[] { .02f, false });
            animate.Invoke(home, new object[] { 1f, false });
            for (int index = 0; index < icons.Length; index++)
            {
                RectTransform icon = icons[index]; RectSnapshot baseline = original.navigationIcons[icon];
                Vector3 expected = baseline.scale * (index == selection ? 1.16f : 1f);
                Expect(Vector3.Distance(icon.localScale, expected) < .0001f, "Navigation selection compounded or failed to restore " + icon.name + " at " + size);
                Expect(icon.anchorMin == baseline.min && icon.anchorMax == baseline.max && icon.anchoredPosition == baseline.position &&
                    icon.sizeDelta == baseline.size && icon.pivot == baseline.pivot, "Selection changed navigation icon layout: " + icon.name);
            }
            foreach (var label in labels)
                Expect(label.label.fontSize == label.size && label.label.rectTransform.localScale == label.scale,
                    "Icon selection changed a navigation label's size.");
        }
        if (size == Phone || size == IPad)
            foreach (int selection in new[] { 1, 4, 0 })
            {
                select.Invoke(home, new object[] { selection }); animate.Invoke(home, new object[] { 0f, true });
                Capture(camera, camera.targetTexture, "Home-Navbar-" + selection + "-" + (int)size.x + "x" + (int)size.y + ".png");
            }
    }

    private static void ValidateShopPointers(HomeExperienceUI home, Canvas canvas, Camera camera, EventSystem events)
    {
        Button gold = canvas.GetComponentsInChildren<Button>(true).First(button => button.name == "SHOP_GOLD_TAB");
        Button booster = canvas.GetComponentsInChildren<Button>(true).First(button => button.name == "SHOP_BOOSTER_TAB");
        Button.ButtonClickedEvent oldGold = gold.onClick, oldBooster = booster.onClick;
        bool clicked = false;
        try
        {
            gold.onClick = new Button.ButtonClickedEvent(); booster.onClick = new Button.ButtonClickedEvent();
            gold.onClick.AddListener(() => { SelectShop(home, false); clicked = true; });
            booster.onClick.AddListener(() => { SelectShop(home, true); clicked = true; });
            SetHomePage(canvas, "Shop Page");
            foreach (bool chooseBooster in new[] { true, false })
                foreach (float horizontal in new[] { .1f, .5f, .9f })
                {
                    SelectShop(home, !chooseBooster);
                    Canvas.ForceUpdateCanvases(); camera.Render();
                    Button expected = chooseBooster ? booster : gold;
                    Graphic face = expected.transform.Find(chooseBooster ? "BoosterNotChoose" : "GoldNotChoose").GetComponent<Graphic>();
                    var pointer = new PointerEventData(events) { position = camera.WorldToScreenPoint(face.rectTransform.TransformPoint(new Vector2(Mathf.Lerp(face.rectTransform.rect.xMin, face.rectTransform.rect.xMax, horizontal), face.rectTransform.rect.center.y))), button = PointerEventData.InputButton.Left };
                    List<RaycastResult> hits = Raycast(canvas, pointer);
                    GameObject handler = hits.Count > 0 ? ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) : null;
                    Expect(handler == expected.gameObject, "Home shop tab is blocked at its visible face: " + expected.name + " x=" + horizontal);
                    clicked = false;
                    ExecuteEvents.Execute(handler, pointer, ExecuteEvents.pointerClickHandler);
                    Expect(clicked && (bool)typeof(HomeExperienceUI).GetField("shopBoosterSelected", Private).GetValue(home) == chooseBooster, "Home tab pointer click did not change the visible category.");
                }
        }
        finally
        {
            gold.onClick = oldGold; booster.onClick = oldBooster;
            SelectShop(home, false); SetHomePage(canvas, "Home Only Content");
        }
    }

    private static void ValidateButtonRaycast(Button button, Camera camera, EventSystem events, string label)
    {
        Canvas.ForceUpdateCanvases(); camera.Render();
        RectTransform rect = (RectTransform)button.transform;
        Vector3 view = camera.WorldToViewportPoint(rect.TransformPoint(rect.rect.center));
        Expect(view.x >= 0f && view.x <= 1f && view.y >= 0f && view.y <= 1f, label + " is outside the display.");
        var pointer = new PointerEventData(events) { position = camera.WorldToScreenPoint(rect.TransformPoint(rect.rect.center)) };
        List<RaycastResult> hits = Raycast(button.GetComponentInParent<Canvas>().rootCanvas, pointer);
        Expect(hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == button.gameObject, label + " did not receive a raycast at its visible center; hits=" + string.Join(",", hits.Take(5).Select(hit => hit.gameObject.name)));
    }

    private static List<RaycastResult> Raycast(Canvas canvas, PointerEventData pointer)
    {
        var hits = new List<RaycastResult>();
        var registered = GraphicRegistry.GetGraphicsForCanvas(canvas);
        var graphics = new Graphic[registered.Count];
        for (int i = 0; i < graphics.Length; i++) graphics[i] = registered[i];
        ValidateGraphicSchema(graphics, "REGISTERED " + GraphicPath(canvas.transform) + " scene=" + canvas.gameObject.scene.name);
        canvas.GetComponent<GraphicRaycaster>().Raycast(pointer, hits);
        return hits;
    }

    private static void ValidateGraphicSchema(IEnumerable<Graphic> graphics, string stage)
    {
        var missing = new List<string>();
        foreach (Graphic graphic in graphics)
        {
            if (graphic == null || !graphic.gameObject.activeInHierarchy) continue;
            bool worldText = graphic is TMPro.TextMeshPro;
            if (worldText && !graphic.raycastTarget) continue;
            Canvas canvas = graphic.canvas;
            if (canvas == null || !canvas.enabled || !canvas.rootCanvas.enabled || graphic.GetComponent<CanvasRenderer>() != null) continue;
            string kind = worldText ? "WORLD_TEXT_WITH_UI_RAYCAST" : "MISSING_UI_RENDERER";
            missing.Add(kind + " path=" + GraphicPath(graphic.transform) + " type=" + graphic.GetType().FullName + " scene=" + graphic.gameObject.scene.name + " canvas=" + GraphicPath(canvas.transform));
        }
        File.WriteAllText("Temp/PortraitDisplayGraphicSchema.result", stage + "\n" + (missing.Count == 0 ? "PASS raycast graphics have CanvasRenderers.\n" : string.Join("\n", missing)));
        Expect(missing.Count == 0, stage + " contains invalid UI raycast graphics:\n" + string.Join("\n", missing));
    }

    private static string GraphicPath(Transform node) => node == null ? "<destroyed>" : node.parent == null ? node.name : GraphicPath(node.parent) + "/" + node.name;

    private static void SetHomePage(Canvas canvas, string selected)
    {
        string[] pages = { "Home Only Content", "Shop Page", "Daily Missions Page", "Achievements Page", "Character Page" };
        foreach (Transform node in canvas.GetComponentsInChildren<Transform>(true)) if (pages.Contains(node.name)) node.gameObject.SetActive(node.name == selected);
        Canvas.ForceUpdateCanvases();
    }

    private static void SelectShop(HomeExperienceUI home, bool booster)
    {
        typeof(HomeExperienceUI).GetMethod("SetShopCategory", Private).Invoke(home, new object[] { booster });
        Canvas.ForceUpdateCanvases();
    }

    private static void Capture(Camera camera, RenderTexture target, string filename)
    {
        camera.Render(); RenderAuxiliaryPreview(camera.gameObject.scene); RenderTexture.active = target;
        var pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        try
        {
            pixels.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0); pixels.Apply();
            File.WriteAllBytes(Path.Combine(PreviewDirectory, filename), pixels.EncodeToPNG());
        }
        finally { UnityEngine.Object.DestroyImmediate(pixels); }
    }

    private static bool RectMatches(Rect first, Rect second) => Vector2.Distance(first.position, second.position) < .0001f && Vector2.Distance(first.size, second.size) < .0001f;
    private static void Expect(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
#endif
