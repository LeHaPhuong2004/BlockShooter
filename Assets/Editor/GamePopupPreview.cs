#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Renders saved popup GameObjects in an isolated preview scene without starting gameplay.</summary>
[InitializeOnLoad]
public static class GamePopupPreview
{
    private const string RequestPath = "Temp/GamePopupPreview.request";
    private const string ResultPath = "Temp/GamePopupPreview.result";
    private const string OutputDirectory = "Temp/GamePopupPreview";
    private const int Width = 540;
    private const int Height = 1169;
    private const float ReferenceWidth = 1080;
    private static bool running;

    static GamePopupPreview() => EditorApplication.update += Poll;

    private static void Poll()
    {
        if (running || !File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        running = true;
        try
        {
            string mode = File.ReadAllText(RequestPath).Trim();
            if (mode == "hud") MeasureSavedHud();
            else if (mode == "hud-image") RenderSavedHud();
            else Run();
        }
        catch (Exception exception)
        {
            File.WriteAllText(ResultPath, "FAIL\n" + exception);
            Debug.LogWarning("Game popup preview failed: " + exception.Message);
        }
        finally { File.Delete(RequestPath); running = false; }
    }

    [MenuItem("Tools/Diagnostics/Render Saved Game Popups")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Render saved UI outside Play Mode.");
        Directory.CreateDirectory(OutputDirectory);
        Scene saved = default;
        Scene isolated = default;
        RenderTexture target = null;
        RenderTexture previousTarget = RenderTexture.active;
        Scene previousActiveScene = SceneManager.GetActiveScene();
        var report = new StringBuilder();
        report.AppendLine("EDITOR playing=" + EditorApplication.isPlaying + " paused=" + EditorApplication.isPaused
            + " activeScene=" + previousActiveScene.name);
        for (int index = 0; index < SceneManager.sceneCount; index++)
        {
            Scene open = SceneManager.GetSceneAt(index);
            report.AppendLine("LOADED " + open.path + " name=" + open.name + " loaded=" + open.isLoaded + " dirty=" + open.isDirty
                + " active=" + (open == previousActiveScene));
        }
        try
        {
            saved = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Game.unity");
            GameObject source = saved.GetRootGameObjects().Single(root => root.name == "Game Popup Canvas");
            source.SetActive(false);
            isolated = EditorSceneManager.NewPreviewScene();
            GameObject copy = UnityEngine.Object.Instantiate(source);
            copy.name = "Saved Popup Preview";
            SceneManager.MoveGameObjectToScene(copy, isolated);
            // Keep only the authored graphics and UI components; never execute gameplay or popup actions.
            foreach (GamePauseUI controller in copy.GetComponentsInChildren<GamePauseUI>(true))
            {
                controller.enabled = false;
                UnityEngine.Object.DestroyImmediate(controller);
            }
            foreach (Transform transform in copy.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer = 5;

            var cameraObject = new GameObject("Popup Preview Camera");
            SceneManager.MoveGameObjectToScene(cameraObject, isolated);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.scene = isolated;
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(45, 23, 103, 255);
            camera.orthographic = true;
            camera.orthographicSize = 5;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 100;
            camera.cullingMask = 1 << 5;
            camera.transform.position = new Vector3(0, 0, -10);
            target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            target.Create();
            camera.targetTexture = target;

            Canvas canvas = copy.GetComponent<Canvas>();
            if (canvas == null) throw new InvalidOperationException("Saved popup root has no Canvas.");
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            canvas.pixelPerfect = false;
            var scaler = copy.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = Width / ReferenceWidth;
                scaler.referenceResolution = new Vector2(ReferenceWidth, Height * ReferenceWidth / Width);
                scaler.enabled = false;
            }
            canvas.scaleFactor = Width / ReferenceWidth;
            canvas.referencePixelsPerUnit = 100;
            var canvasRect = (RectTransform)copy.transform;
            canvasRect.sizeDelta = new Vector2(ReferenceWidth, Height * ReferenceWidth / Width);
            copy.SetActive(true);
            Transform overlay = copy.transform.Find("Modal Overlay");
            if (overlay == null) throw new InvalidOperationException("Saved popup overlay is missing.");
            overlay.gameObject.SetActive(true);
            string[] names = { "Settings Panel", "NextLevelPanel", "LosePanel", "Leave Confirmation" };
            string[] files = { "Settings", "Victory", "Loss", "Confirmation" };
            report.AppendLine("Saved Game popup artwork, isolated preview scene, " + Width + "x" + Height + ".");
            for (int i = 0; i < names.Length; i++)
            {
                foreach (Transform child in overlay) child.gameObject.SetActive(child.name == names[i]);
                Transform panel = overlay.Find(names[i]);
                if (panel == null) throw new InvalidOperationException("Missing saved popup: " + names[i]);
                PrepareGraphics(canvasRect);
                camera.Render();
                PrepareGraphics(canvasRect);
                camera.Render();
                RenderTexture.active = target;
                var pixels = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
                try
                {
                    pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                    pixels.Apply();
                    string path = OutputDirectory + "/" + files[i] + ".png";
                    File.WriteAllBytes(path, pixels.EncodeToPNG());
                    report.AppendLine("PNG " + path + " bytes=" + new FileInfo(path).Length);
                }
                finally { UnityEngine.Object.DestroyImmediate(pixels); }
                DumpPanel(panel, camera, report);
            }
            GridManager grid = saved.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GridManager>(true)).FirstOrDefault();
            if (grid != null && grid.blockCountText != null)
                MeasureHud(grid.blockCountText, report);
            report.AppendLine("PASS: rendered authored GameObjects only; no scene save, gameplay start or button invocation.");
            File.WriteAllText(ResultPath, report.ToString());
        }
        finally
        {
            RenderTexture.active = previousTarget;
            if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
            if (isolated.IsValid()) EditorSceneManager.ClosePreviewScene(isolated);
            if (saved.IsValid()) EditorSceneManager.ClosePreviewScene(saved);
            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded && SceneManager.GetActiveScene() != previousActiveScene)
                SceneManager.SetActiveScene(previousActiveScene);
        }
    }

    private static void PrepareGraphics(RectTransform root)
    {
        foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(false)) graphic.SetAllDirty();
        LayoutRebuilder.ForceRebuildLayoutImmediate(root);
        Canvas.ForceUpdateCanvases();
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(false)) text.ForceMeshUpdate(true, true);
        Canvas.ForceUpdateCanvases();
    }

    [MenuItem("Tools/Diagnostics/Measure Saved Game HUD Text")]
    public static void MeasureSavedHud()
    {
        Scene scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Game.unity");
        try
        {
            var report = new StringBuilder("Saved Game HUD font measurement; preview scene only.\n");
            GridManager grid = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GridManager>(true)).Single();
            if (grid.blockCountText == null) throw new InvalidOperationException("Saved HUD counter is not assigned.");
            TMP_Text sample = grid.blockCountText;
            TMP_FontAsset originalFont = sample.font; Material originalMaterial = sample.fontSharedMaterial;
            string originalText = sample.text; float originalSize = sample.fontSize; bool originalAuto = sample.enableAutoSizing;
            MeasureHud(sample, report);
            foreach (TMP_FontAsset font in new[] { originalFont, Resources.Load<TMP_FontAsset>("Font/blockblast_font") }.Where(font => font != null))
                foreach (float size in new[] { 67f, 62f, 58f })
                {
                    sample.font = font; sample.fontSharedMaterial = font.material; sample.fontSize = size; sample.enableAutoSizing = false; sample.text = "128/252";
                    sample.ForceMeshUpdate(true, true);
                    report.AppendLine("HUD COMPACT font=" + font.name + " size=" + size + " textWidth=" + sample.textBounds.size.x
                        + " meshWidth=" + sample.mesh.bounds.size.x + " preferredWidth=" + sample.GetPreferredValues(sample.text, float.PositiveInfinity, float.PositiveInfinity).x);
                }
            sample.font = originalFont; sample.fontSharedMaterial = originalMaterial; sample.fontSize = originalSize; sample.enableAutoSizing = originalAuto; sample.text = originalText;
            File.WriteAllText(ResultPath, report.ToString());
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [MenuItem("Tools/Diagnostics/Render Saved Game HUD")]
    public static void RenderSavedHud()
    {
        Directory.CreateDirectory(OutputDirectory);
        Scene saved = default, isolated = default;
        RenderTexture target = null;
        RenderTexture previousTarget = RenderTexture.active;
        Scene previousActive = SceneManager.GetActiveScene();
        var report = new StringBuilder("Saved Game HUD rendering; isolated preview scene only.\n");
        try
        {
            saved = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Game.unity");
            GridManager grid = saved.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GridManager>(true)).Single();
            TMP_Text originalCounter = grid.blockCountText;
            if (originalCounter == null) throw new InvalidOperationException("Saved HUD counter is not assigned.");
            Canvas sourceCanvas = originalCounter.GetComponentInParent<Canvas>(true).rootCanvas;
            string counterPath = AnimationUtility.CalculateTransformPath(originalCounter.transform, sourceCanvas.transform);
            sourceCanvas.gameObject.SetActive(false);
            isolated = EditorSceneManager.NewPreviewScene();
            GameObject copy = UnityEngine.Object.Instantiate(sourceCanvas.gameObject);
            copy.name = "Saved Game HUD Preview";
            SceneManager.MoveGameObjectToScene(copy, isolated);
            foreach (MonoBehaviour component in copy.GetComponentsInChildren<MonoBehaviour>(true))
                if (component != null && !(component is UIBehaviour)) UnityEngine.Object.DestroyImmediate(component);
            foreach (Renderer renderer in copy.GetComponentsInChildren<Renderer>(true))
                if (renderer != null) UnityEngine.Object.DestroyImmediate(renderer);
            foreach (SpriteMask mask in copy.GetComponentsInChildren<SpriteMask>(true))
                if (mask != null) UnityEngine.Object.DestroyImmediate(mask);
            foreach (Animator animator in copy.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            foreach (Transform transform in copy.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer = 5;

            var cameraObject = new GameObject("HUD Preview Camera");
            SceneManager.MoveGameObjectToScene(cameraObject, isolated);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.scene = isolated; camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 5;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(45, 23, 103, 255);
            camera.nearClipPlane = .1f; camera.farClipPlane = 100; camera.cullingMask = 1 << 5;
            camera.transform.position = new Vector3(0, 0, -10);
            target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32); target.Create(); camera.targetTexture = target;
            Canvas canvas = copy.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1; canvas.pixelPerfect = false;
            canvas.scaleFactor = Width / ReferenceWidth; canvas.referencePixelsPerUnit = 100;
            var scaler = copy.GetComponent<CanvasScaler>();
            if (scaler != null) { scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize; scaler.scaleFactor = Width / ReferenceWidth; scaler.enabled = false; }
            var canvasRect = (RectTransform)copy.transform;
            canvasRect.localScale = Vector3.one;
            canvasRect.sizeDelta = new Vector2(ReferenceWidth, Height * ReferenceWidth / Width);
            TMP_Text counter = copy.transform.Find(counterPath).GetComponent<TMP_Text>();
            counter.text = "128/252";
            foreach (TMP_Text heart in copy.GetComponentsInChildren<TMP_Text>(true).Where(label => label.name == "Heart Count")) heart.text = "5";
            copy.SetActive(true);
            PrepareGraphics(canvasRect); camera.Render(); PrepareGraphics(canvasRect); camera.Render();
            RenderTexture.active = target;
            var pixels = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            try { pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); pixels.Apply(); File.WriteAllBytes(OutputDirectory + "/Hud.png", pixels.EncodeToPNG()); }
            finally { UnityEngine.Object.DestroyImmediate(pixels); }
            Rect glyphs = ProjectLocalBounds(counter.textBounds, counter.rectTransform, camera);
            Rect mesh = ProjectLocalBounds(counter.mesh.bounds, counter.rectTransform, camera);
            report.AppendLine("PNG " + OutputDirectory + "/Hud.png");
            report.AppendLine("HUD COUNTER font=" + counter.font.name + " size=" + counter.fontSize + " style=" + counter.fontStyle
                + " text=" + counter.text + " rectPixels=" + ScreenBounds(counter.rectTransform, camera) + " glyphPixels=" + glyphs + " meshPixels=" + mesh);
            foreach (Image image in counter.transform.parent.GetComponentsInChildren<Image>(true))
            {
                Rect bounds = ScreenBounds(image.rectTransform, camera);
                report.AppendLine("HUD IMAGE sprite=" + (image.sprite == null ? "null" : image.sprite.name)
                    + " pixels=" + bounds + " containsGlyph=" + (bounds.Contains(glyphs.min) && bounds.Contains(glyphs.max))
                    + " containsMesh=" + (bounds.Contains(mesh.min) && bounds.Contains(mesh.max)));
            }
            File.WriteAllText(ResultPath, report.ToString());
        }
        finally
        {
            RenderTexture.active = previousTarget;
            if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
            if (isolated.IsValid()) EditorSceneManager.ClosePreviewScene(isolated);
            if (saved.IsValid()) EditorSceneManager.ClosePreviewScene(saved);
            if (previousActive.IsValid() && previousActive.isLoaded && SceneManager.GetActiveScene() != previousActive) SceneManager.SetActiveScene(previousActive);
        }
    }

    private static Rect ProjectLocalBounds(Bounds bounds, RectTransform rect, Camera camera)
    {
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity), max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        foreach (Vector3 corner in new[] { bounds.min, bounds.max, new Vector3(bounds.min.x, bounds.max.y, bounds.center.z), new Vector3(bounds.max.x, bounds.min.y, bounds.center.z) })
        {
            Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(corner));
            min = Vector2.Min(min, point); max = Vector2.Max(max, point);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private static void DumpPanel(Transform panel, Camera camera, StringBuilder report)
    {
        report.AppendLine("PANEL " + panel.name + " pixels=" + ScreenBounds((RectTransform)panel, camera));
        foreach (Button button in panel.GetComponentsInChildren<Button>(false))
        {
            var rect = button.transform as RectTransform;
            report.AppendLine(" BUTTON " + button.name + " hit=" + ScreenBounds(rect, camera) + " interactable=" + button.interactable);
        }
        foreach (TMP_Text label in panel.GetComponentsInChildren<TMP_Text>(false))
            report.AppendLine(" TEXT " + label.name + " font=" + label.fontSize + " pixels=" + ScreenBounds(label.rectTransform, camera)
                + " mesh=" + label.textInfo.characterCount + " value=" + label.text.Replace("\n", "\\n"));
    }

    private static void MeasureHud(TMP_Text label, StringBuilder report)
    {
        string originalText = label.text;
        report.AppendLine("HUD SOURCE font=" + (label.font == null ? "null" : label.font.name)
            + " family=" + (label.font == null ? "null" : label.font.faceInfo.familyName)
            + " fontSize=" + label.fontSize + " autoSize=" + label.enableAutoSizing
            + " originalText=" + originalText + " material=" + (label.fontSharedMaterial == null ? "null" : label.fontSharedMaterial.name));
        label.text = "128 / 252";
        label.enabled = true;
        label.ForceMeshUpdate(true, true);
        RectTransform rect = label.rectTransform;
        Vector2 preferred = label.GetPreferredValues(label.text, float.PositiveInfinity, float.PositiveInfinity);
        float availableWidth = rect.rect.width - label.margin.x - label.margin.z;
        float availableHeight = rect.rect.height - label.margin.y - label.margin.w;
        Bounds glyphs = label.textBounds;
        report.AppendLine("HUD BENCHMARK value=" + label.text + " rect=" + rect.rect + " margin=" + label.margin
            + " available=" + new Vector2(availableWidth, availableHeight) + " preferred=" + preferred
            + " textBounds=" + glyphs + " meshBounds=" + label.mesh.bounds
            + " visibleChars=" + label.textInfo.characterInfo.Take(label.textInfo.characterCount).Count(character => character.isVisible)
            + " widthFitsLabel=" + (glyphs.size.x <= availableWidth)
            + " heightFitsLabel=" + (glyphs.size.y <= availableHeight)
            + " wrapping=" + label.textWrappingMode + " overflow=" + label.overflowMode);
        Transform current = rect;
        for (int depth = 0; current != null && depth < 6; depth++, current = current.parent)
        {
            if (current is RectTransform ancestor)
                report.AppendLine(" HUD ANCESTOR " + HierarchyPath(current) + " rect=" + ancestor.rect
                    + " pos=" + ancestor.anchoredPosition + " localScale=" + ancestor.localScale
                    + " inLabel=" + BoundsInLocal(ancestor, rect));
            foreach (Transform child in current)
            {
                Image image = child.GetComponent<Image>();
                if (image == null) continue;
                Rect imageBounds = BoundsInLocal(image.rectTransform, rect);
                report.AppendLine(" HUD NEARBY IMAGE " + HierarchyPath(child)
                    + " sprite=" + (image.sprite == null ? "null" : image.sprite.name)
                    + " active=" + image.gameObject.activeInHierarchy
                    + " rect=" + image.rectTransform.rect + " pos=" + image.rectTransform.anchoredPosition
                    + " localScale=" + image.rectTransform.localScale + " inLabel=" + imageBounds
                    + " glyphsContained=" + (imageBounds.Contains((Vector2)glyphs.min) && imageBounds.Contains((Vector2)glyphs.max)));
            }
        }
    }

    private static Rect BoundsInLocal(RectTransform subject, RectTransform localSpace)
    {
        var corners = new Vector3[4];
        subject.GetWorldCorners(corners);
        Vector2 min = localSpace.InverseTransformPoint(corners[0]);
        Vector2 max = min;
        foreach (Vector3 corner in corners)
        {
            Vector2 point = localSpace.InverseTransformPoint(corner);
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private static string HierarchyPath(Transform transform) => transform.parent == null ? transform.name : HierarchyPath(transform.parent) + "/" + transform.name;

    private static Rect ScreenBounds(RectTransform rect, Camera camera)
    {
        if (rect == null) return default;
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
        Vector2 max = min;
        foreach (Vector3 corner in corners)
        {
            Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corner);
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
}
#endif
