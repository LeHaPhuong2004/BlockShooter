#if UNITY_EDITOR
using System;
using System.Collections.Generic;
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

/// <summary>Authors victory rewards and captures the victory and test-ad panels without claiming rewards.</summary>
[InitializeOnLoad]
public static class VictoryRewardSetup
{
    private const string ScenePath = "Assets/Scenes/Game.unity";
    private const string Request = "Temp/VictoryRewardSetup.request";
    private const string Result = "Temp/VictoryRewardSetup.result";
    private const string Revision = "victory-reward-v3";
    private static bool running;

    static VictoryRewardSetup() => EditorApplication.update += Poll;

    private static void Poll()
    {
        if (running || !File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        string command = File.ReadAllText(Request).Trim();
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            if (command == "stop-and-apply-" + Revision) EditorApplication.isPlaying = false;
            return;
        }
        running = true;
        try
        {
            if (command == "apply-" + Revision || command == "stop-and-apply-" + Revision) Apply();
            else if (command == "capture-" + Revision) Capture();
            else Audit();
        }
        catch (Exception exception) { File.WriteAllText(Result, "VERSION=" + Revision + "\nFAIL\n" + exception); Debug.LogException(exception); }
        finally { File.Delete(Request); running = false; }
    }

    [MenuItem("Tools/Diagnostics/Audit Victory Reward Panel")]
    public static void Audit()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Audit saved victory artwork outside Play Mode.");
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool preview = !scene.IsValid() || !scene.isLoaded;
        if (preview) scene = EditorSceneManager.OpenPreviewScene(ScenePath);
        try
        {
            var report = new StringBuilder("VERSION=" + Revision + "\n");
            report.AppendLine("Game preview=" + preview + " dirty=" + scene.isDirty);
            Dump(Controller(scene), report);
            File.WriteAllText(Result, report.ToString());
        }
        finally { if (preview) EditorSceneManager.ClosePreviewScene(scene); }
    }

    [MenuItem("Tools/Game UI/Use Victory Coin Rewards")]
    public static void Apply()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Author victory rewards outside Play Mode.");
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            GamePauseUI controller = Controller(scene);
            var fields = new SerializedObject(controller);
            GameObject panel = Reference<GameObject>(fields, "victoryPanel");
            Require(panel != null, "Game has no authored victory panel.");
            TMP_Text template = Find<TMP_Text>(panel.transform, "Completed Level");
            Button doubleReward = Find<Button>(panel.transform, "Victory Double Reward Button") ?? Find<Button>(panel.transform, "Victory Home Button");
            Button claimReward = Find<Button>(panel.transform, "Next Level Button");
            Require(template != null && doubleReward != null && claimReward != null, "The authored victory labels or button slots are missing.");
            var geometry = panel.GetComponentsInChildren<RectTransform>(true)
                .Where(rect => !LegacyStrip(rect.name) && rect != doubleReward.transform.Find("Label"))
                .ToDictionary(rect => rect, rect => new Layout(rect));
            Undo.RegisterFullObjectHierarchyUndo(panel, "Use victory coin rewards");
            Undo.RecordObject(controller, "Bind victory coin rewards");
            foreach (Transform legacy in panel.GetComponentsInChildren<Transform>(true).Where(node => LegacyStrip(node.name)).ToArray())
                if (legacy != null) Undo.DestroyObjectImmediate(legacy.gameObject);

            Image coin = Find<Image>(panel.transform, "Victory Reward Coin");
            if (coin == null) coin = NewRect("Victory Reward Coin", panel.transform, -125, -425, 175, 138).gameObject.AddComponent<Image>();
            coin.sprite = CoinSprite(); coin.preserveAspect = true; coin.raycastTarget = false;
            TMP_Text amount = Label(panel.transform, template, "Victory Reward Amount", "100", 105, -425, 260, 130, 88);
            amount.color = new Color32(255, 233, 55, 255);
            TMP_Text status = Label(panel.transform, template, "Victory Reward Status", "", 0, -565, 860, 42, 27);
            doubleReward.name = "Victory Double Reward Button";
            Bind(doubleReward, controller, "ClaimDoubleVictoryReward");
            TMP_Text doubleLabel = Find<TMP_Text>(doubleReward.transform, "Label");
            Require(doubleLabel != null, "The existing blue button has no caption.");
            ConfigureLabel(doubleLabel, "NHẬN X2", 48);
            doubleLabel.rectTransform.anchoredPosition = new Vector2(0, 20);
            doubleLabel.rectTransform.sizeDelta = new Vector2(409.2f, 78);
            Label(doubleReward.transform, template, "Ad Caption", "XEM QUẢNG CÁO", 0, -39, 409.2f, 40, 27);
            Bind(claimReward, controller, "ClaimVictoryRewardAndContinue");
            TMP_Text claimLabel = Find<TMP_Text>(claimReward.transform, "Label");
            Require(claimLabel != null, "The existing green button has no caption.");
            ConfigureLabel(claimLabel, "NHẬN & TIẾP", 43);
            Set(fields, "victoryRewardAmountLabel", amount);
            Set(fields, "victoryRewardStatusLabel", status);
            Set(fields, "victoryDoubleRewardButton", doubleReward);
            Set(fields, "victoryClaimRewardButton", claimReward);
            BuildSimulatedAd(controller, fields, template, claimReward);
            foreach (string obsolete in new[] { "nextLevelLabel", "followingLevelLabel", "laterLevelLabel" })
                if (fields.FindProperty(obsolete) != null) fields.FindProperty(obsolete).objectReferenceValue = null;
            fields.ApplyModifiedPropertiesWithoutUndo();
            foreach (var original in geometry)
                Require(original.Key != null && original.Value.Matches(original.Key), "Existing victory layout changed: " + (original.Key == null ? "deleted object" : original.Key.name));
            panel.SetActive(false);
            GameObject overlay = Reference<GameObject>(fields, "overlay");
            if (overlay != null) overlay.SetActive(false);
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            Require(EditorSceneManager.SaveScene(scene), "Could not save the victory reward controls.");
            var report = new StringBuilder("VERSION=" + Revision + "\n");
            Dump(controller, report);
            report.AppendLine("PASS Game saved: coin 100, two persistent reward actions, old progression strip removed; existing hero/stars/banner/panel/button rectangles preserved. Authored five-second test-ad overlay is inactive and ready.");
            File.WriteAllText(Result, report.ToString());
        }
        finally { if (openedHere) EditorSceneManager.CloseScene(scene, true); }
    }

    [MenuItem("Tools/Diagnostics/Capture Victory Reward Languages")]
    public static void Capture()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Capture disposable victory previews outside Play Mode.");
        Scene scene = EditorSceneManager.OpenPreviewScene(ScenePath);
        int oldLanguage = Localize.Language;
        RenderTexture previousTarget = RenderTexture.active;
        try
        {
            GamePauseUI controller = Controller(scene);
            var fields = new SerializedObject(controller);
            GameObject panel = Reference<GameObject>(fields, "victoryPanel");
            GameObject overlay = Reference<GameObject>(fields, "overlay");
            GameObject simulatedAd = Reference<GameObject>(fields, "simulatedAdPanel");
            Require(panel != null && Find<TMP_Text>(panel.transform, "Victory Reward Amount") != null, "Apply the authored victory reward migration before capturing.");
            Require(simulatedAd != null, "The authored test-ad preview panel is missing.");
            if (overlay != null)
            {
                foreach (Transform sibling in overlay.transform) sibling.gameObject.SetActive(sibling.gameObject == panel);
                overlay.SetActive(true);
            }
            panel.SetActive(true);
            Camera camera = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true)).First(item => item.CompareTag("MainCamera"));
            camera.scene = scene;
            var size = new Vector2(1170, 2532);
            var target = new RenderTexture((int)size.x, (int)size.y, 24, RenderTextureFormat.ARGB32);
            try
            {
                target.Create(); camera.targetTexture = target;
                foreach (Canvas canvas in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Canvas>(true)).Where(canvas => canvas.isRootCanvas))
                    PortraitDisplay.ApplyCanvasLayout(canvas, camera, PortraitDisplay.ReferenceResolutionForScene(scene), size);
                ResponsiveSceneLayout.Apply(scene, camera, size);
                var report = new StringBuilder("VERSION=" + Revision + "\n");
                foreach (int language in new[] { Localize.Vietnamese, Localize.English })
                {
                    Localize.SetLanguage(language, false); Localize.Apply(panel.transform); Localize.Apply(simulatedAd.transform);
                    foreach (bool testAd in new[] { false, true })
                    {
                        if (overlay != null) overlay.SetActive(!testAd);
                        panel.SetActive(!testAd); simulatedAd.SetActive(testAd);
                        Canvas.ForceUpdateCanvases();
                        foreach (TMP_Text label in (testAd ? simulatedAd : panel).GetComponentsInChildren<TMP_Text>(true)) label.ForceMeshUpdate(true, true);
                        camera.Render(); Canvas.ForceUpdateCanvases(); camera.Render();
                        RenderTexture.active = target;
                        var pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
                        string filename = "Temp/" + (testAd ? "VictoryTestAd-" : "VictoryReward-") + (language == Localize.English ? "EN" : "VI") + ".png";
                        try { pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply(); File.WriteAllBytes(filename, pixels.EncodeToPNG()); }
                        finally { UnityEngine.Object.DestroyImmediate(pixels); }
                        report.AppendLine("PNG " + filename);
                    }
                }
                report.AppendLine("PASS read-only saved Game previews; no reward actions, preferences or scene saves.");
                File.WriteAllText(Result, report.ToString());
            }
            finally { camera.targetTexture = null; target.Release(); UnityEngine.Object.DestroyImmediate(target); }
        }
        finally { RenderTexture.active = previousTarget; EditorSceneManager.ClosePreviewScene(scene); Localize.SetLanguage(oldLanguage, false); }
    }

    private static void Dump(GamePauseUI controller, StringBuilder report)
    {
        var fields = new SerializedObject(controller);
        GameObject panel = Reference<GameObject>(fields, "victoryPanel");
        Require(panel != null, "Missing authored victory panel.");
        foreach (Transform node in panel.GetComponentsInChildren<Transform>(true))
        {
            var line = new StringBuilder("NODE " + node.name + " active=" + node.gameObject.activeSelf);
            if (node is RectTransform rect) line.Append(" anchors=" + rect.anchorMin + "/" + rect.anchorMax + " pos=" + rect.anchoredPosition + " size=" + rect.sizeDelta + " scale=" + rect.localScale);
            TMP_Text label = node.GetComponent<TMP_Text>();
            if (label != null) line.Append(" text=" + label.text.Replace("\n", "\\n"));
            Image image = node.GetComponent<Image>();
            if (image != null) line.Append(" sprite=" + (image.sprite == null ? "none" : image.sprite.name));
            Button button = node.GetComponent<Button>();
            if (button != null)
                for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++) line.Append(" callback=" + button.onClick.GetPersistentMethodName(i));
            report.AppendLine(line.ToString());
        }
        foreach (string field in new[] { "victoryRewardAmountLabel", "victoryRewardStatusLabel", "victoryDoubleRewardButton", "victoryClaimRewardButton",
            "simulatedAdPanel", "simulatedAdCountdownLabel", "simulatedAdClaimButton", "simulatedAdCloseButton" })
        {
            SerializedProperty property = fields.FindProperty(field);
            report.AppendLine("FIELD " + field + "=" + (property == null ? "not compiled" : property.objectReferenceValue == null ? "null" : property.objectReferenceValue.name));
        }
    }

    private static void BuildSimulatedAd(GamePauseUI controller, SerializedObject fields, TMP_Text template, Button claimTemplate)
    {
        Transform canvas = controller.transform;
        RectTransform overlay = canvas.GetComponentsInChildren<RectTransform>(true).SingleOrDefault(rect => rect.name == "Simulated Victory Ad");
        if (overlay == null) overlay = NewRect("Simulated Victory Ad", canvas, 0, 0, 0, 0);
        overlay.SetParent(canvas, false); overlay.SetAsLastSibling();
        overlay.anchorMin = Vector2.zero; overlay.anchorMax = Vector2.one; overlay.offsetMin = overlay.offsetMax = Vector2.zero;
        overlay.localScale = Vector3.one; overlay.localRotation = Quaternion.identity;
        Image dim = overlay.GetComponent<Image>();
        if (dim == null) dim = overlay.gameObject.AddComponent<Image>();
        dim.color = new Color32(9, 4, 36, 235); dim.raycastTarget = true;
        Picture(overlay, "Test Ad Frame", PopupSprite("Settings_Atlas_Cut", "SettingsPanel"), 0, 0, 900, 1170);
        Label(overlay, template, "Test Ad Title", "QUẢNG CÁO THỬ", 0, 430, 620, 100, 65);
        Picture(overlay, "Test Ad Reward Coin", CoinSprite(), -125, 140, 175, 138);
        TMP_Text amount = Label(overlay, template, "Test Ad Reward Amount", "200", 105, 140, 260, 130, 88);
        amount.color = new Color32(255, 233, 55, 255);
        Label(overlay, template, "Test Ad Subtitle", "Sau 5 giây có thể nhận x2", 0, -70, 820, 80, 38);
        TMP_Text countdown = Label(overlay, template, "Test Ad Countdown", "Thời gian còn lại: 5s", 0, -190, 820, 75, 42);
        Button claim = AdButton(overlay, template, controller, "Test Ad Claim Button", claimTemplate.image.sprite, "NHẬN X2", 0, -390, 760, 150, 50, "FinishSimulatedVictoryAd");
        claim.colors = claimTemplate.colors; claim.interactable = false;
        Button close = AdButton(overlay, template, controller, "Test Ad Close Button", PopupSprite("Special_Level_Confirm_Close", "Special_Level_Confirm_Close"), "", 410, 470, 95, 95, 40, "CancelSimulatedVictoryAd");
        close.interactable = true;
        Set(fields, "simulatedAdPanel", overlay.gameObject);
        Set(fields, "simulatedAdCountdownLabel", countdown);
        Set(fields, "simulatedAdClaimButton", claim);
        Set(fields, "simulatedAdCloseButton", close);
        overlay.gameObject.SetActive(false);
    }

    private static Button AdButton(Transform parent, TMP_Text template, GamePauseUI controller, string name, Sprite sprite, string caption,
        float x, float y, float width, float height, float size, string action)
    {
        Image image = Picture(parent, name, sprite, x, y, width, height);
        image.raycastTarget = true;
        Button button = image.GetComponent<Button>();
        if (button == null) button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image; Bind(button, controller, action);
        if (!string.IsNullOrEmpty(caption)) Label(button.transform, template, "Label", caption, 0, 0, width * .93f, height * .8f, size);
        return button;
    }

    private static Image Picture(Transform parent, string name, Sprite sprite, float x, float y, float width, float height)
    {
        Image image = Find<Image>(parent, name);
        if (image == null) image = NewRect(name, parent, x, y, width, height).gameObject.AddComponent<Image>();
        image.sprite = sprite; image.preserveAspect = true; image.raycastTarget = false;
        return image;
    }

    private static Sprite PopupSprite(string atlas, string name)
    {
        Sprite sprite = AssetDatabase.LoadAllAssetsAtPath("Assets/UI/Popup/" + atlas + ".png").OfType<Sprite>().FirstOrDefault(item => item.name == name);
        Require(sprite != null, "Missing existing popup artwork " + name);
        return sprite;
    }

    private static bool LegacyStrip(string name) => name == "Next Levels Frame" || name == "Next Levels Title" ||
        name.StartsWith("Next Level Character ", StringComparison.Ordinal) || name == "Next Level 0" || name == "Next Level 1" || name == "Next Level 2";
    private static GamePauseUI Controller(Scene scene) => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GamePauseUI>(true)).Single();
    private static T Find<T>(Transform parent, string name) where T : Component => parent.GetComponentsInChildren<T>(true).SingleOrDefault(item => item.name == name);
    private static T Reference<T>(SerializedObject fields, string name) where T : UnityEngine.Object => fields.FindProperty(name)?.objectReferenceValue as T;
    private static void Set(SerializedObject fields, string name, UnityEngine.Object value)
    {
        SerializedProperty field = fields.FindProperty(name);
        Require(field != null, "Missing runtime victory field " + name + "; refresh compiled sources before applying.");
        field.objectReferenceValue = value;
    }
    private static void Bind(Button button, GamePauseUI controller, string method)
    {
        var callback = (UnityAction)Delegate.CreateDelegate(typeof(UnityAction), controller, method);
        button.onClick = new Button.ButtonClickedEvent();
        UnityEventTools.AddPersistentListener(button.onClick, callback);
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.interactable = true;
        EditorUtility.SetDirty(button);
    }
    private static Sprite CoinSprite()
    {
        Sprite sprite = AssetDatabase.LoadAllAssetsAtPath("Assets/UI/Popup/VictoryRewards_Atlas_Cut.png").OfType<Sprite>().FirstOrDefault(item => item.name == "VictoryCoins");
        Require(sprite != null, "The existing VictoryCoins sprite is missing.");
        return sprite;
    }
    private static RectTransform NewRect(string name, Transform parent, float x, float y, float width, float height)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(width, height);
        return rect;
    }
    private static TMP_Text Label(Transform parent, TMP_Text template, string name, string text, float x, float y, float width, float height, float size)
    {
        TMP_Text label = Find<TMP_Text>(parent, name);
        if (label == null)
        {
            label = NewRect(name, parent, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = template.font; label.fontSharedMaterial = template.fontSharedMaterial;
            label.fontStyle = FontStyles.Bold; label.color = Color.white;
            label.alignment = TextAlignmentOptions.Midline; label.margin = Vector4.zero;
        }
        ConfigureLabel(label, text, size);
        return label;
    }
    private static void ConfigureLabel(TMP_Text label, string text, float size)
    {
        label.text = text; label.fontSize = size; label.enableAutoSizing = true;
        label.fontSizeMin = Mathf.Min(size, 24); label.fontSizeMax = size;
        label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Ellipsis; label.raycastTarget = false;
        EditorUtility.SetDirty(label);
    }
    private readonly struct Layout
    {
        private readonly Vector2 min, max, position, size, pivot;
        private readonly Vector3 scale;
        private readonly Quaternion rotation;
        public Layout(RectTransform rect) { min = rect.anchorMin; max = rect.anchorMax; position = rect.anchoredPosition; size = rect.sizeDelta; pivot = rect.pivot; scale = rect.localScale; rotation = rect.localRotation; }
        public bool Matches(RectTransform rect) => min == rect.anchorMin && max == rect.anchorMax && position == rect.anchoredPosition && size == rect.sizeDelta && pivot == rect.pivot && scale == rect.localScale && rotation == rect.localRotation;
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
#endif
