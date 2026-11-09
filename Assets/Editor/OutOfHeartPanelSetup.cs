#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Inspects and connects the existing out-of-heart artwork without rebuilding scenes.</summary>
[InitializeOnLoad]
public static class OutOfHeartPanelSetup
{
    private const string Request = "Temp/OutOfHeartPanelSetup.request";
    private const string Result = "Temp/OutOfHeartPanelSetup.result";
    private static bool running;

    static OutOfHeartPanelSetup() => EditorApplication.update += Poll;

    private static void Poll()
    {
        if (running || !File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        running = true;
        try { if (File.ReadAllText(Request).Trim() == "apply-v3") Apply(); else Audit(); }
        catch (Exception exception) { File.WriteAllText(Result, "FAIL\n" + exception); Debug.LogException(exception); }
        finally { File.Delete(Request); running = false; }
    }

    [MenuItem("Tools/Diagnostics/Audit Out Of Heart Panels")]
    public static void Audit()
    {
        var report = new StringBuilder();
        report.AppendLine("VERSION=3 (single-button recognition and restored-node refresh)");
        foreach (string path in new[] { "Assets/Scenes/Home.unity", "Assets/Scenes/Game.unity" })
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            bool preview = !scene.IsValid() || !scene.isLoaded;
            if (preview) scene = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                report.AppendLine(path + " preview=" + preview + " dirty=" + scene.isDirty);
                foreach (Canvas canvas in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Canvas>(true)))
                    report.AppendLine("CANVAS " + Path(canvas.transform) + " order=" + canvas.sortingOrder);
                foreach (Transform panel in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).Where(node => node.name.IndexOf("OutOfHear", StringComparison.OrdinalIgnoreCase) >= 0))
                    Dump(panel, report, 0);
            }
            finally { if (preview) EditorSceneManager.ClosePreviewScene(scene); }
        }
        File.WriteAllText(Result, report.ToString());
    }

    [MenuItem("Tools/Home UI/Connect Out Of Heart Panels")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Connect the authored panels outside Play Mode.");
        var report = new StringBuilder();
        foreach (string path in new[] { "Assets/Scenes/Home.unity", "Assets/Scenes/Game.unity" })
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                Transform[] nodes = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
                Transform panel = nodes.Single(node => node.name == "OutOfHeartPanel" || node.name == "OutOfHearPanel");
                Button[] buttons = panel.GetComponentsInChildren<Button>(true);
                if (scene.name == "Game" && buttons.Length == 1 && !buttons[0].gameObject.activeSelf)
                {
                    buttons = new[] { RestoreAuthoredHomeButton(buttons[0]) };
                    nodes = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
                }
                Require(buttons.Length == 2 || (scene.name == "Game" && buttons.Length == 1), "Unexpected out-of-heart button count in " + path);
                Button shop = buttons.Length == 1 ? null : buttons.SingleOrDefault(button => HasCallback(button, "OnOpenShopButton") || button.name == "OUT_OF_HEART_SHOP");
                if (shop == null && buttons.Length == 2) shop = buttons.OrderBy(button => ((RectTransform)button.transform).anchoredPosition.x).First();
                Button close = buttons.Single(button => button != shop);
                if (scene.name == "Game")
                {
                    Button authoredHome = buttons.SingleOrDefault(button => button.gameObject.activeSelf &&
                        (button.GetComponentsInChildren<TMP_Text>(true).Any(label => label.text.IndexOf("home", StringComparison.OrdinalIgnoreCase) >= 0)
                         || button.GetComponentsInChildren<Text>(true).Any(label => label.text.IndexOf("home", StringComparison.OrdinalIgnoreCase) >= 0)));
                    if (authoredHome != null) close = authoredHome;
                }
                Undo.RegisterFullObjectHierarchyUndo(panel.gameObject, "Connect out of heart panel");
                EnsureArtworkContainer(panel);
                if (scene.name == "Home")
                {
                    HomeExperienceUI home = nodes.Select(node => node.GetComponent<HomeExperienceUI>()).Single(item => item != null);
                    SetObject(home, "outOfHeartPanel", panel.gameObject);
                    Bind(shop, home, "OpenHeartShop");
                    Bind(close, home, "CloseOutOfHearts");
                    shop.name = "OUT_OF_HEART_SHOP";
                    close.name = "OUT_OF_HEART_CLOSE";
                    SetLabel(shop, "Tới shop");
                    SetLabel(close, "Đóng");
                    Transform modal = nodes.SingleOrDefault(node => node.name == "Modal" && node.IsChildOf(home.transform));
                    if (modal != null) Undo.DestroyObjectImmediate(modal.gameObject);
                    report.AppendLine("PASS Home uses authored art, shop/close callbacks, placeholder Modal removed.");
                }
                else
                {
                    GamePauseUI game = nodes.Select(node => node.GetComponent<GamePauseUI>()).Single(item => item != null);
                    Transform canvas = nodes.Single(node => node.name == "Game Popup Canvas");
                    Undo.SetTransformParent(panel, canvas, "Raise out of heart panel above gameplay");
                    SetObject(game, "outOfHeartPanel", panel.gameObject);
                    Bind(close, game, "ReturnHomeFromOutOfHearts");
                    close.name = "OUT_OF_HEART_HOME";
                    SetLabel(close, "Về Home");
                    RectTransform closeRect = (RectTransform)close.transform;
                    Vector2 buttonSize = closeRect.rect.size;
                    RectTransform buttonParent = (RectTransform)closeRect.parent;
                    Vector3 centered = buttonParent.InverseTransformPoint(closeRect.TransformPoint(closeRect.rect.center));
                    closeRect.anchorMin = closeRect.anchorMax = new Vector2(0.5f, 0.5f);
                    closeRect.pivot = new Vector2(0.5f, 0.5f);
                    closeRect.sizeDelta = buttonSize;
                    closeRect.localPosition = centered;
                    closeRect.anchoredPosition = new Vector2(0f, closeRect.anchoredPosition.y);
                    close.gameObject.SetActive(true);
                    foreach (Button unused in buttons.Where(button => button != close)) Undo.DestroyObjectImmediate(unused.gameObject);
                    report.AppendLine("PASS Game uses authored art with one centered Home button above gameplay.");
                }
                foreach (NavigationManager navigation in nodes.Where(node => node != null).Select(node => node.GetComponent<NavigationManager>()).Where(item => item != null))
                    SetObject(navigation, "panelOutOfHeart", panel.gameObject);
                StretchBlocker((RectTransform)panel);
                panel.SetAsLastSibling();
                panel.gameObject.SetActive(false);
                EditorSceneManager.MarkSceneDirty(scene);
                Require(EditorSceneManager.SaveScene(scene), "Could not save " + path);
                Dump(panel, report, 0);
            }
            finally { if (openedHere) EditorSceneManager.CloseScene(scene, true); }
        }
        File.WriteAllText(Result, report.ToString());
    }

    private static Button RestoreAuthoredHomeButton(Button inactive)
    {
        Scene sourceScene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Home.unity");
        try
        {
            Button source = sourceScene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Button>(true))
                .Single(button => button.name == "OUT_OF_HEART_SHOP");
            GameObject restored = UnityEngine.Object.Instantiate(source.gameObject, inactive.transform.parent, false);
            Undo.RegisterCreatedObjectUndo(restored, "Restore authored red Home button");
            Undo.DestroyObjectImmediate(inactive.gameObject);
            restored.SetActive(true);
            return restored.GetComponent<Button>();
        }
        finally { EditorSceneManager.ClosePreviewScene(sourceScene); }
    }

    private static RectTransform EnsureArtworkContainer(Transform panel)
    {
        RectTransform existing = panel.Find("Out Of Heart Artwork") as RectTransform;
        if (existing != null) return existing;
        Canvas.ForceUpdateCanvases();
        RectTransform original = (RectTransform)panel;
        var children = Enumerable.Range(0, panel.childCount).Select(panel.GetChild).ToArray();
        var artwork = new GameObject("Out Of Heart Artwork", typeof(RectTransform)).GetComponent<RectTransform>();
        Undo.RegisterCreatedObjectUndo(artwork.gameObject, "Preserve out of heart artwork layout");
        artwork.SetParent(panel, false);
        artwork.anchorMin = artwork.anchorMax = new Vector2(0.5f, 0.5f);
        artwork.pivot = original.pivot;
        artwork.sizeDelta = original.rect.size;
        foreach (Transform child in children)
        {
            // Retain authored anchors, dimensions and local transforms under an
            // identically sized parent while the outer dimmer expands to the screen.
            child.SetParent(artwork, false);
        }
        return artwork;
    }

    private static void StretchBlocker(RectTransform panel)
    {
        RectTransform artwork = (RectTransform)panel.Find("Out Of Heart Artwork");
        Vector3 originalScale = panel.localScale;
        artwork.localScale = Vector3.Scale(artwork.localScale, originalScale);
        panel.anchorMin = Vector2.zero;
        panel.anchorMax = Vector2.one;
        panel.offsetMin = panel.offsetMax = Vector2.zero;
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.localScale = Vector3.one;
        Image dimmer = panel.GetComponent<Image>();
        if (dimmer == null) dimmer = panel.gameObject.AddComponent<Image>();
        dimmer.raycastTarget = true;
        if (dimmer.color.a <= 0f) dimmer.color = new Color32(9, 4, 36, 200);
        EditorUtility.SetDirty(panel);
        EditorUtility.SetDirty(dimmer);
    }

    private static void SetObject(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(field);
        Require(property != null, "Missing serialized field " + target.GetType().Name + "." + field);
        property.objectReferenceValue = value;
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
    }

    private static bool HasCallback(Button button, string method) => Enumerable.Range(0, button.onClick.GetPersistentEventCount())
        .Any(index => button.onClick.GetPersistentMethodName(index) == method);

    private static void Bind(Button button, UnityEngine.Object target, string method)
    {
        UnityAction action = (UnityAction)Delegate.CreateDelegate(typeof(UnityAction), target, method);
        for (int index = button.onClick.GetPersistentEventCount() - 1; index >= 0; index--)
            UnityEventTools.RemovePersistentListener(button.onClick, index);
        button.onClick.RemoveAllListeners();
        UnityEventTools.AddPersistentListener(button.onClick, action);
        button.enabled = true;
        button.interactable = true;
        if (button.targetGraphic != null) button.targetGraphic.raycastTarget = true;
        EditorUtility.SetDirty(button);
    }

    private static void SetLabel(Button button, string value)
    {
        foreach (TMP_Text label in button.GetComponentsInChildren<TMP_Text>(true)) { label.text = value; label.raycastTarget = false; EditorUtility.SetDirty(label); }
        foreach (Text label in button.GetComponentsInChildren<Text>(true)) { label.text = value; label.raycastTarget = false; EditorUtility.SetDirty(label); }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Dump(Transform node, StringBuilder report, int depth)
    {
        string line = new string(' ', depth * 2) + Path(node) + " active=" + node.gameObject.activeSelf;
        if (node is RectTransform rect) line += " anchors=" + rect.anchorMin + "/" + rect.anchorMax + " pos=" + rect.anchoredPosition + " size=" + rect.sizeDelta + " scale=" + rect.localScale;
        Image image = node.GetComponent<Image>();
        if (image != null) line += " IMAGE=" + (image.sprite == null ? "null" : image.sprite.name) + " color=" + image.color + " raycast=" + image.raycastTarget;
        TMP_Text tmp = node.GetComponent<TMP_Text>();
        if (tmp != null) line += " TMP=" + tmp.text.Replace("\n", "\\n") + " font=" + tmp.fontSize;
        Text text = node.GetComponent<Text>();
        if (text != null) line += " TEXT=" + text.text.Replace("\n", "\\n") + " font=" + text.fontSize;
        Button button = node.GetComponent<Button>();
        if (button != null)
        {
            line += " BUTTON interactable=" + button.interactable;
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++) line += " listener=" + button.onClick.GetPersistentTarget(i) + "." + button.onClick.GetPersistentMethodName(i);
        }
        report.AppendLine(line);
        foreach (Transform child in node) Dump(child, report, depth + 1);
    }

    private static string Path(Transform node) => node.parent == null ? node.name : Path(node.parent) + "/" + node.name;
}
#endif
