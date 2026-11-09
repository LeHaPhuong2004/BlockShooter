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

/// <summary>Inspects the authored Home foreground and shop without changing other scenes.</summary>
[InitializeOnLoad]
public static class HomeForegroundSetup
{
    private const string ScenePath = "Assets/Scenes/Home.unity";
    private const string Request = "Temp/HomeForegroundSetup.request";
    private const string Result = "Temp/HomeForegroundSetup.result";
    private const string Revision = "home-edges-v3";
    private static bool running;

    static HomeForegroundSetup() => EditorApplication.update += Poll;

    private static void Poll()
    {
        if (running || !File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        string mode = File.ReadAllText(Request).Trim();
        bool stopAndApply = mode == "stop-and-apply-" + Revision;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            if (stopAndApply) EditorApplication.isPlaying = false;
            return;
        }
        running = true;
        try { if (mode == "apply-" + Revision || stopAndApply) Apply(); else Audit(); }
        catch (Exception exception) { File.WriteAllText(Result, "FAIL\n" + exception); Debug.LogException(exception); }
        finally { File.Delete(Request); running = false; }
    }

    [MenuItem("Tools/Home UI/Apply Selective Responsive Foreground")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Restore the authored Home foreground outside Play Mode after compilation.");
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            GameObject[] roots = scene.GetRootGameObjects();
            Transform[] originalObjects = roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            HomeExperienceUI home = roots.SelectMany(root => root.GetComponentsInChildren<HomeExperienceUI>(true)).Single();
            Canvas canvas = home.GetComponentsInChildren<Canvas>(true).Single(item => item.name == "Home Canvas");
            RectTransform homeRoot = home.GetComponentsInChildren<RectTransform>(true).Single(node => node.name == "Home");
            var geometry = new Dictionary<RectTransform, RectLayout>();
            foreach (RectTransform rect in canvas.GetComponentsInChildren<RectTransform>(true))
                if (rect != canvas.transform && rect != homeRoot && rect.name != "Robot Background" && rect.name != "Home Content")
                    geometry.Add(rect, new RectLayout(rect));
            var overlays = canvas.transform.Cast<Transform>().Where(node => node != homeRoot).ToArray();
            foreach (GameObject root in roots) Undo.RegisterFullObjectHierarchyUndo(root, "Apply selective Home foreground");
            home.ApplyResponsiveLayout(PortraitDisplay.ReferenceScreenSize);
            foreach (var item in geometry)
                if (item.Key == null || !item.Value.Matches(item.Key, item.Key.name == "Play Multiplier Badge"))
                    throw new InvalidOperationException("Home foreground rectangle changed: " + (item.Key == null ? "missing object" : Path(item.Key)));
            RectTransform content = homeRoot.Find("Home Content") as RectTransform;
            float width = 1920f * PortraitDisplay.ReferenceScreenSize.x / PortraitDisplay.ReferenceScreenSize.y;
            Require(content != null && Mathf.Abs(content.sizeDelta.x - width) < .01f &&
                content.anchorMin == new Vector2(.5f, 0f) && content.anchorMax == new Vector2(.5f, 1f), "Home Content is not fixed to the authored phone width.");
            ValidateEdgeHosts(content, width);
            Require(homeRoot.Find("Robot Background") != null, "The expanding background must remain outside the fixed Home Content.");
            foreach (string name in new[] { "Floating Robot Character", "Robot Contact Shadows" })
            {
                Transform character = content.GetComponentsInChildren<Transform>(true).SingleOrDefault(node => node.name == name);
                Require(character != null && character.parent == content, name + " must belong to fixed Home Content.");
            }
            foreach (Transform overlay in overlays)
                Require(overlay != null && overlay.parent == canvas.transform, "A Home popup moved into the fixed foreground.");
            Require(originalObjects.All(node => node != null), "Home setup removed an existing scene object.");
            EditorSceneManager.MarkSceneDirty(scene);
            Require(EditorSceneManager.SaveScene(scene), "Could not save the fixed Home foreground.");
            var report = new StringBuilder("VERSION=" + Revision + "\n");
            report.AppendLine("PASS centered Home Content fixed width=" + width + "; selective controls retain phone baseline size and position, background and popup overlays retain full display.");
            report.AppendLine("PASS all " + geometry.Count + " original foreground/shop rectangles preserve authored dimensions, anchors, positions, pivots and scales; only multiplier badge position may change.");
            report.AppendLine("PASS full-width nav host, left/right HUD hosts and left/right level hosts; shop, currency, banners, hero and Play remain in centered phone-width content.");
            report.AppendLine("PASS all original Home scene objects retained. Home saved; other scenes and player data were not edited.");
            File.WriteAllText(Result, report.ToString());
        }
        finally { if (openedHere) EditorSceneManager.CloseScene(scene, true); }
    }

    [MenuItem("Tools/Diagnostics/Audit Home Foreground")]
    public static void Audit()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool preview = !scene.IsValid() || !scene.isLoaded;
        if (preview) scene = EditorSceneManager.OpenPreviewScene(ScenePath);
        try
        {
            var report = new StringBuilder("VERSION=" + Revision + "\n");
            report.AppendLine("Home preview=" + preview + " dirty=" + scene.isDirty);
            var home = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<HomeExperienceUI>(true)).Single();
            var nodes = home.GetComponentsInChildren<RectTransform>(true);
            Canvas canvas = home.GetComponentsInChildren<Canvas>(true).Single(item => item.name == "Home Canvas");
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            report.AppendLine("CANVAS " + Path(canvas.transform) + " mode=" + canvas.renderMode + " reference=" + scaler.referenceResolution + " match=" + scaler.matchWidthOrHeight);
            RectTransform root = nodes.Single(node => node.name == "Home");
            foreach (Transform child in canvas.transform) report.AppendLine("CANVAS CHILD " + Path(child) + " active=" + child.gameObject.activeSelf);
            foreach (Transform child in root) report.AppendLine("HOME CHILD " + Path(child) + " active=" + child.gameObject.activeSelf);
            foreach (RectTransform node in nodes.Where(node => IsSelectiveNode(node) || node.name == "Home Content" || node.name == "Home Only Content"
                || node.name == "COINS" || node.name == "ENERGY" || node.name == "LEVEL_BANNER" || node.name == "Home Screen Edges"
                || node.name == "Left HUD Edge" || node.name == "Right HUD Edge" || node.name == "Level Screen Edges"
                || node.name == "Left Level Edge" || node.name == "Right Level Edge"))
                Dump(node, report, "SELECTIVE");
            foreach (RectTransform node in nodes.Where(node => node.name == "PLAY" || node.name == "Play Multiplier Badge" ||
                node.name == "Play Multiplier Label" || node.name.IndexOf("hook", StringComparison.OrdinalIgnoreCase) >= 0))
                Dump(node, report, "PLAY");
            foreach (RectTransform viewport in nodes.Where(node => node.name == "Gold Shop Viewport" || node.name == "Booster Shop Viewport"))
            {
                Dump(viewport, report, "SHOP VIEWPORT");
                ScrollRect scroll = viewport.GetComponent<ScrollRect>();
                if (scroll != null && scroll.content != null) Dump(scroll.content, report, "SHOP SCROLL CONTENT");
                foreach (RectTransform layer in viewport.GetComponentsInChildren<RectTransform>(true).Where(node => node.name == "Shop Card Items" || node.name == "Shop Card Frames"))
                {
                    Dump(layer, report, "SHOP LAYER");
                    var cards = layer.Cast<Transform>().OfType<RectTransform>().ToArray();
                    for (int index = 0; index < cards.Length; index++)
                    {
                        RectTransform card = cards[index];
                        Dump(card, report, "CARD " + index);
                        float center = PhoneCenterX(card);
                        float width = DrawnWidth(card);
                        report.AppendLine("  PHONE CARD centerX=" + center + " drawnWidth=" + width);
                        if (index % 2 == 1)
                        {
                            RectTransform left = cards[index - 1];
                            float gap = center - PhoneCenterX(left) - (width + DrawnWidth(left)) * .5f;
                            report.AppendLine("  PHONE ROW gap=" + gap);
                        }
                    }
                }
            }
            report.AppendLine("PASS read-only Home hierarchy, authored hook/card rectangles and computed phone geometry inspected.");
            File.WriteAllText(Result, report.ToString());
        }
        finally { if (preview) EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static Vector2 PhoneSize(RectTransform rect)
    {
        Canvas canvas = rect.GetComponent<Canvas>();
        if (canvas != null && canvas.isRootCanvas)
        {
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            float height = scaler == null ? 1920f : scaler.referenceResolution.y;
            return new Vector2(height * PortraitDisplay.ReferenceScreenSize.x / PortraitDisplay.ReferenceScreenSize.y, height);
        }
        Vector2 parent = rect.parent is RectTransform parentRect ? PhoneSize(parentRect) : Vector2.zero;
        return Vector2.Scale(rect.anchorMax - rect.anchorMin, parent) + rect.sizeDelta;
    }

    private static float PhoneCenterX(RectTransform rect)
    {
        RectTransform parent = rect.parent as RectTransform;
        float parentWidth = parent == null ? 0f : PhoneSize(parent).x;
        float anchor = Mathf.Lerp(rect.anchorMin.x, rect.anchorMax.x, rect.pivot.x);
        return (anchor - (parent == null ? .5f : parent.pivot.x)) * parentWidth + rect.anchoredPosition.x
            + (.5f - rect.pivot.x) * PhoneSize(rect).x * rect.localScale.x;
    }

    private static float DrawnWidth(RectTransform rect)
    {
        Vector2 size = PhoneSize(rect);
        Image image = rect.GetComponent<Image>();
        float width = size.x;
        if (image != null && image.preserveAspect && image.sprite != null)
            width = Mathf.Min(width, size.y * image.sprite.rect.width / image.sprite.rect.height);
        return width * rect.localScale.x;
    }

    private static bool IsSelectiveNode(RectTransform rect)
    {
        if (rect.name == "PROFILE" || rect.name == "SETTINGS" || rect.name == "Bottom Navigation"
            || rect.name.StartsWith("LOCKED_", StringComparison.Ordinal)) return true;
        for (Transform parent = rect.parent; parent != null; parent = parent.parent)
            if (parent.name == "Bottom Navigation") return true;
        return false;
    }

    private static void ValidateEdgeHosts(RectTransform content, float width)
    {
        var nodes = content.GetComponentsInChildren<RectTransform>(true);
        RectTransform hud = content.Find("Home Screen Edges") as RectTransform;
        RectTransform homeOnly = content.Find("Home Only Content") as RectTransform;
        RectTransform levels = homeOnly == null ? null : homeOnly.Find("Level Screen Edges") as RectTransform;
        Require(hud != null && levels != null, "The Home and level edge hosts are missing.");
        Require(Mathf.Abs(PhoneSize(hud).x - width) < .01f && Mathf.Abs(PhoneSize(levels).x - width) < .01f,
            "Edge hosts must preserve the authored phone width when saving the phone baseline.");
        RectTransform leftHud = hud.Find("Left HUD Edge") as RectTransform;
        RectTransform rightHud = hud.Find("Right HUD Edge") as RectTransform;
        RectTransform leftLevel = levels.Find("Left Level Edge") as RectTransform;
        RectTransform rightLevel = levels.Find("Right Level Edge") as RectTransform;
        foreach (RectTransform edge in new[] { leftHud, rightHud, leftLevel, rightLevel })
            Require(edge != null && Mathf.Abs(PhoneSize(edge).x - width) < .01f && edge.anchorMin.y == 0f && edge.anchorMax.y == 1f,
                "Each edge host must keep the authored phone width and parent height.");
        Require(nodes.Single(node => node.name == "PROFILE").parent == leftHud, "PROFILE must use the left HUD host.");
        Require(nodes.Single(node => node.name == "SETTINGS").parent == rightHud, "SETTINGS must use the right HUD host.");
        Require(nodes.Single(node => node.name == "Bottom Navigation").parent == hud, "Bottom Navigation must use the full-width HUD host.");
        foreach (RectTransform node in nodes.Where(node => node.name.StartsWith("LOCKED_", StringComparison.Ordinal)))
            Require(node.parent == (node.name.StartsWith("LOCKED_50", StringComparison.Ordinal) ? rightLevel : leftLevel),
                "A level card or its sibling glow is outside the matching edge host: " + node.name);
        foreach (string name in new[] { "COINS", "ENERGY", "LEVEL_BANNER", "PLAY", "Shop Page", "Daily Missions Page", "Achievements Page", "Character Page", "Floating Robot Character", "Robot Contact Shadows" })
        {
            RectTransform node = nodes.Single(item => item.name == name);
            Require(!node.IsChildOf(hud) && !node.IsChildOf(levels), "Centered content moved into an edge host: " + name);
        }
    }

    private static void Dump(RectTransform rect, StringBuilder report, string label)
    {
        Image image = rect.GetComponent<Image>();
        report.AppendLine(label + " " + Path(rect) + " active=" + rect.gameObject.activeSelf + " anchors=" + rect.anchorMin + "/" + rect.anchorMax
            + " pivot=" + rect.pivot + " pos=" + rect.anchoredPosition3D + " size=" + rect.sizeDelta + " scale=" + rect.localScale
            + " actualRect=" + rect.rect + " phoneSize=" + PhoneSize(rect)
            + (image == null ? "" : " sprite=" + (image.sprite == null ? "null" : image.sprite.name) + " preserveAspect=" + image.preserveAspect));
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
        public bool Matches(RectTransform rect, bool movingBadge) => min == rect.anchorMin && max == rect.anchorMax &&
            (movingBadge || (position - rect.anchoredPosition3D).sqrMagnitude < .000001f) &&
            (size - rect.sizeDelta).sqrMagnitude < .000001f && pivot == rect.pivot && scale == rect.localScale && rotation == rect.localRotation;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static string Path(Transform node) => node.parent == null ? node.name : Path(node.parent) + "/" + node.name;
}
#endif
