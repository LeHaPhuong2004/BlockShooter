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

/// <summary>Upgrades only the authored achievement page and retains the original scene objects.</summary>
[InitializeOnLoad]
public static class HomeAchievementsSetup
{
    private const string ScenePath = "Assets/Scenes/Home.unity";
    private const string RequestPath = "Temp/HomeAchievementsSetup.request";
    private const string LockButtonsRequestPath = "Temp/HomeAchievementLockButtons.request";
    private const string LockButtonsReportPath = "Temp/HomeAchievementLockButtons.result";
    private const string ReportPath = "Temp/HomeAchievementsSetup.result";
    private const string OutputDirectory = "output/achievements";
    private static bool running;

    static HomeAchievementsSetup() => EditorApplication.update += ProcessRequest;

    private static void ProcessRequest()
    {
        bool lockButtonsOnly = File.Exists(LockButtonsRequestPath);
        if (running || (!lockButtonsOnly && !File.Exists(RequestPath)) || EditorApplication.isCompiling ||
            EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        running = true;
        try
        {
            if (lockButtonsOnly) ApplyLockButtonPresentation();
            else Prepare();
        }
        catch (Exception exception)
        {
            Directory.CreateDirectory("Temp");
            File.WriteAllText(lockButtonsOnly ? LockButtonsReportPath : ReportPath, exception.ToString());
            Debug.LogException(exception);
        }
        finally
        {
            File.Delete(lockButtonsOnly ? LockButtonsRequestPath : RequestPath);
            running = false;
        }
    }

    [MenuItem("Tools/Home UI/Apply Achievement Lock Buttons")]
    public static void ApplyLockButtonPresentation()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before editing authored achievement buttons.");
        Directory.CreateDirectory(OutputDirectory);
        string backup = OutputDirectory + "/Home-before-lock-only-buttons.unity.backup";
        if (!File.Exists(backup)) File.Copy(ScenePath, backup);
        var scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            var page = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Single(node => node.name == "Achievements Page");
            int centeredLocks = 0;
            foreach (var button in page.GetComponentsInChildren<Button>(true)
                .Where(button => button.name.StartsWith("CLAIM_ACHIEVEMENT_", StringComparison.Ordinal)))
            {
                var label = button.transform.Find("Label")?.GetComponent<Text>();
                if (label != null && label.text == "CHƯA ĐẠT")
                {
                    Undo.RecordObject(label, "Remove achievement locked label");
                    label.text = string.Empty;
                    EditorUtility.SetDirty(label);
                }
                var icon = button.GetComponentsInChildren<RectTransform>(true)
                    .FirstOrDefault(rect => rect.name == "Achievement Lock Icon");
                if (icon == null) continue;
                Undo.RecordObject(icon, "Center achievement lock");
                icon.anchorMin = new Vector2(0.38f, 0.02f);
                icon.anchorMax = new Vector2(0.62f, 0.98f);
                icon.offsetMin = icon.offsetMax = Vector2.zero;
                EditorUtility.SetDirty(icon);
                centeredLocks++;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save achievement lock buttons.");
            File.WriteAllText(LockButtonsReportPath, "PASS: removed locked-button text and centered " + centeredLocks +
                " achievement locks. Only button labels and lock positions were edited; Home.unity saved.");
        }
        finally { if (openedHere) EditorSceneManager.CloseScene(scene, true); }
    }

    [MenuItem("Tools/Home UI/Prepare Ten Achievements")]
    public static void Prepare()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before authoring the achievement page.");
        Directory.CreateDirectory("Temp");
        Directory.CreateDirectory(OutputDirectory);
        string backup = OutputDirectory + "/Home-before-ten-achievements.unity.backup";
        if (!File.Exists(backup)) File.Copy(ScenePath, backup);

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            var home = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<HomeExperienceUI>(true)).Single();
            Transform page = home.GetComponentsInChildren<Transform>(true)
                .Single(node => node.name == "Achievements Page");
            int[] originalObjects = ObjectIds(scene);
            var otherObjects = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(node => node != page && !node.IsChildOf(page))
                .ToDictionary(node => node.GetInstanceID(), HierarchySignature);
            int[] originalCards = page.GetComponentsInChildren<Transform>(true)
                .Where(node => node.name.StartsWith("Achievement Card - ", StringComparison.Ordinal))
                .Select(node => node.GetInstanceID()).ToArray();
            Undo.RegisterFullObjectHierarchyUndo(page.gameObject, "Prepare ten achievement cards");
            home.PrepareAchievementsForEditor();
            var retained = new HashSet<int>(ObjectIds(scene));
            Require(originalObjects.All(retained.Contains), "An existing Home object was removed.");
            Require(originalCards.All(retained.Contains), "An original achievement card was replaced.");
            var afterNodes = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .ToDictionary(node => node.GetInstanceID());
            foreach (var original in otherObjects)
                Require(afterNodes.TryGetValue(original.Key, out var node) &&
                    HierarchySignature(node) == original.Value,
                    "An object outside the achievement page changed its hierarchy.");

            int[] firstUpgrade = ObjectIds(scene);
            home.PrepareAchievementsForEditor();
            Require(firstUpgrade.SequenceEqual(ObjectIds(scene)),
                "Repeated achievement setup created duplicate objects.");
            Canvas.ForceUpdateCanvases();
            var report = new StringBuilder();
            report.AppendLine("PASS: all original Home objects and the four existing achievement cards retained.");
            report.AppendLine("PASS: no hierarchy outside the achievement page changed; repeated setup creates no objects.");
            report.AppendLine(AchievementRegression.ValidateAuthoredScene(home));
            EditorUtility.SetDirty(home);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save the ten authored achievement cards in Home.unity.");
            CaptureCatalogPreview(home, OutputDirectory + "/achievement-cards-preview.png");
            CapturePagePreview(home, OutputDirectory + "/achievement-top-preview.png", false);
            CapturePagePreview(home, OutputDirectory + "/achievement-bottom-preview.png", true);
            report.AppendLine("PASS: Home.unity saved; all ten cards and page top/bottom previews captured in output/achievements.");
            report.AppendLine("Authoring and preview did not read or write PlayerPrefs, grant rewards or contact services.");
            File.WriteAllText(ReportPath, report.ToString());
            File.WriteAllText(OutputDirectory + "/achievement-ui-validation.txt", report.ToString());
            Debug.Log(report.ToString());
        }
        finally { if (openedHere) EditorSceneManager.CloseScene(scene, true); }
    }

    public static void CaptureCatalogPreview(HomeExperienceUI home, string path)
    {
        var source = home.GetComponentsInChildren<ScrollRect>(true)
            .Single(scroll => scroll.name == "Achievement List").content;
        CapturePreview(source.gameObject, path, 2199, true, false);
    }

    public static void CapturePagePreview(HomeExperienceUI home, string path, bool bottom)
    {
        var source = home.GetComponentsInChildren<Transform>(true)
            .Single(node => node.name == "Achievements Page");
        CapturePreview(source.gameObject, path, 1672, false, bottom);
    }

    private static void CapturePreview(GameObject source, string path, int height, bool removeAspect, bool bottom)
    {
        Scene preview = default;
        RenderTexture target = null;
        Texture2D pixels = null;
        RenderTexture previous = RenderTexture.active;
        try
        {
            preview = EditorSceneManager.NewPreviewScene();
            var cameraObject = new GameObject("Achievement Preview Camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, preview);
            var camera = cameraObject.GetComponent<Camera>();
            camera.scene = preview;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(26, 9, 79, 255);
            camera.orthographic = true;
            camera.orthographicSize = height * 0.5f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 1000f;
            camera.transform.position = new Vector3(0f, 0f, -100f);
            camera.cullingMask = 1 << 31;
            target = new RenderTexture(941, height, 24, RenderTextureFormat.ARGB32);
            target.Create();
            camera.targetTexture = target;
            var canvasObject = new GameObject("Achievement Preview Canvas", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(canvasObject, preview);
            canvasObject.layer = 31;
            canvasObject.SetActive(false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10f;
            var clone = UnityEngine.Object.Instantiate(source, canvasObject.transform, false)
                .GetComponent<RectTransform>();
            var aspect = clone.GetComponent<AspectRatioFitter>();
            if (removeAspect && aspect != null) UnityEngine.Object.DestroyImmediate(aspect);
            clone.gameObject.SetActive(true);
            clone.anchorMin = Vector2.zero;
            clone.anchorMax = Vector2.one;
            clone.pivot = Vector2.one * 0.5f;
            clone.offsetMin = clone.offsetMax = Vector2.zero;
            clone.localScale = Vector3.one;
            foreach (var node in clone.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 31;
            canvasObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            var scroll = clone.GetComponentsInChildren<ScrollRect>(true)
                .FirstOrDefault(item => item.name == "Achievement List");
            if (scroll != null)
            {
                scroll.StopMovement();
                scroll.verticalNormalizedPosition = bottom ? 0f : 1f;
                Canvas.ForceUpdateCanvases();
            }
            camera.Render();
            RenderTexture.active = target;
            pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            pixels.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0);
            pixels.Apply();
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllBytes(path, pixels.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            if (target != null)
            {
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
            if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
        }
    }

    private static int[] ObjectIds(Scene scene) => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
        .Select(node => node.GetInstanceID()).OrderBy(id => id).ToArray();

    private static string HierarchySignature(Transform node) => node.name + "|" +
        (node.parent != null ? node.parent.GetInstanceID() : 0) + "|" + node.GetSiblingIndex() + "|" +
        node.gameObject.activeSelf;

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Achievement setup: " + message);
    }
}
#endif
