#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Reads the authored popup panels and available artwork without saving changes.</summary>
[InitializeOnLoad]
public static class GamePopupAudit
{
    private const string Request = "Temp/GamePopupAudit.request";
    private const string Result = "Temp/GamePopupAudit.result";
    private static bool running;
    static GamePopupAudit() { EditorApplication.update += Poll; }
    private static void Poll()
    {
        if (running || !File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        running = true;
        try { Run(); }
        catch (Exception exception) { File.WriteAllText(Result, exception.ToString()); Debug.LogWarning("Popup audit failed: " + exception.Message); }
        finally { File.Delete(Request); running = false; }
    }
    [MenuItem("Tools/Diagnostics/Audit Authored Game Popups")]
    public static void Run()
    {
        var report = new StringBuilder();
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Game.unity");
        try
        {
            var roots = scene.GetRootGameObjects();
            report.AppendLine("SAVED GAME ROOTS");
            foreach (var root in roots) report.AppendLine(Path(root.transform) + " active=" + root.activeSelf);
            foreach (var navigation in roots.SelectMany(root => root.GetComponentsInChildren<NavigationManager>(true)))
            {
                report.AppendLine("NAVIGATION " + Path(navigation.transform));
                var serialized = new SerializedObject(navigation);
                foreach (string name in new[] { "winPanel", "losePanel", "settingPanel", "levelText", "coinText" })
                {
                    var property = serialized.FindProperty(name);
                    var value = property != null ? property.objectReferenceValue : null;
                    report.AppendLine(" FIELD " + name + "=" + ObjectPath(value));
                    if (value is GameObject panel && name.EndsWith("Panel")) Dump(panel.transform, report, 0);
                }
            }
            foreach (Canvas canvas in roots.SelectMany(root => root.GetComponentsInChildren<Canvas>(true)))
                report.AppendLine("CANVAS " + Path(canvas.transform) + " active=" + canvas.gameObject.activeSelf + " mode=" + canvas.renderMode + " order=" + canvas.sortingOrder + " reference=" + canvas.GetComponent<CanvasScaler>()?.referenceResolution);
            foreach (var grid in roots.SelectMany(root => root.GetComponentsInChildren<GridManager>(true)))
                report.AppendLine("BLOCK COUNTER " + ObjectPath(grid.blockCountText) + " font=" + (grid.blockCountText == null ? 0 : grid.blockCountText.fontSize));
            report.AppendLine("POPUP ARTWORK");
            foreach (string path in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/UI/Popup" }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path))
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                report.AppendLine(path + " dimensions=" + texture.width + "x" + texture.height + " spriteMode=" + importer.spriteImportMode);
                foreach (Sprite sprite in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(sprite => sprite.name))
                    report.AppendLine(" SPRITE " + sprite.name + " rect=" + sprite.rect + " border=" + sprite.border + " guid=" + AssetDatabase.AssetPathToGUID(path));
            }
            File.WriteAllText(Result, report.ToString());
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
    private static void Dump(Transform transform, StringBuilder report, int depth)
    {
        string line = new string(' ', depth * 2) + Path(transform) + " active=" + transform.gameObject.activeSelf;
        if (transform is RectTransform rect) line += " anchors=" + rect.anchorMin + "/" + rect.anchorMax + " pos=" + rect.anchoredPosition + " size=" + rect.sizeDelta + " scale=" + rect.localScale;
        Image image = transform.GetComponent<Image>();
        if (image != null) line += " IMAGE=" + (image.sprite == null ? "null" : image.sprite.name + " @" + AssetDatabase.GetAssetPath(image.sprite)) + " type=" + image.type + " color=" + image.color + " raycast=" + image.raycastTarget;
        TMP_Text tmp = transform.GetComponent<TMP_Text>();
        if (tmp != null) line += " TMP=" + tmp.text.Replace("\n", "\\n") + " font=" + tmp.fontSize + " color=" + tmp.color;
        Text text = transform.GetComponent<Text>();
        if (text != null) line += " TEXT=" + text.text.Replace("\n", "\\n") + " font=" + text.fontSize + " color=" + text.color;
        Button button = transform.GetComponent<Button>();
        if (button != null)
        {
            line += " BUTTON interactable=" + button.interactable;
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++) line += " listener=" + ObjectPath(button.onClick.GetPersistentTarget(i)) + "." + button.onClick.GetPersistentMethodName(i);
        }
        report.AppendLine(line);
        if (depth < 8) foreach (Transform child in transform) Dump(child, report, depth + 1);
    }
    private static string Path(Transform transform) => transform == null ? "null" : transform.parent == null ? transform.name : Path(transform.parent) + "/" + transform.name;
    private static string ObjectPath(UnityEngine.Object value) => value == null ? "null" : value is GameObject go ? Path(go.transform) : value is Component component ? Path(component.transform) : value.name;
}
#endif
