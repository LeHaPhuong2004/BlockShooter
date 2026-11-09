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

/// <summary>Authors editable scene objects. No part of this builder runs in the player.</summary>
[InitializeOnLoad]
public static class GamePopupSceneSetup
{
    const string Request = "Temp/GamePopupSceneSetup.request";
    const string Result = "Temp/GamePopupSceneSetup.result";
    const string ScenePath = "Assets/Scenes/Game.unity";
    static bool running;
    static TMP_FontAsset font;
    static Material textMaterial;
    static GamePauseUI controller;
    static SerializedObject fields;
    static GamePopupSceneSetup() { EditorApplication.update += Poll; }

    static void Poll()
    {
        if (running || !File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        running = true;
        try { Run(); }
        catch (Exception e) { File.WriteAllText(Result, e.ToString()); Debug.LogException(e); }
        finally { File.Delete(Request); running = false; }
    }

    [MenuItem("Tools/Game UI/Author Game Popups")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Author the UI outside Play Mode.");
        GamePopupArtSetup.EnsureSprites();
        var original = SceneManager.GetActiveScene();
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            var roots = scene.GetRootGameObjects();
            var navigation = roots.SelectMany(r => r.GetComponentsInChildren<NavigationManager>(true)).Single();
            var nav = new SerializedObject(navigation);
            var victory = (GameObject)nav.FindProperty("winPanel").objectReferenceValue;
            var loss = (GameObject)nav.FindProperty("losePanel").objectReferenceValue;
            var old = roots.FirstOrDefault(r => r.name == "Game Popup Canvas");
            // Keep the existing win/lose GameObjects and their NavigationManager references.
            if (old != null)
            {
                if (victory != null) victory.transform.SetParent(null, false);
                if (loss != null) loss.transform.SetParent(null, false);
                UnityEngine.Object.DestroyImmediate(old);
            }
            font = Resources.Load<TMP_FontAsset>("Font/blockblast_font");
            if (font == null) throw new InvalidOperationException("Missing game UI font.");
            const string materialPath = "Assets/UI/Popup/GamePopupText.mat";
            textMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (textMaterial == null)
            {
                textMaterial = new Material(font.material) { name = "GamePopupText" };
                textMaterial.SetColor(ShaderUtilities.ID_OutlineColor, new Color32(50, 6, 116, 255));
                textMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, .18f);
                AssetDatabase.CreateAsset(textMaterial, materialPath);
            }
            var root = Rect("Game Popup Canvas", null, 0, 0, 1080, 2337.23077f);
            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            var scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 2337.23077f);
            scaler.matchWidthOrHeight = 0.5f;
            root.gameObject.AddComponent<GraphicRaycaster>();
            controller = root.gameObject.AddComponent<GamePauseUI>();
            fields = new SerializedObject(controller);
            var overlay = Rect("Modal Overlay", root, 0, 0, 0, 0);
            Stretch(overlay);
            var dim = overlay.gameObject.AddComponent<Image>();
            dim.color = new Color32(9, 4, 36, 220);
            dim.raycastTarget = true;
            Set("overlay", overlay.gameObject);

            var settings = Rect("Settings Panel", overlay, 0, 20, 930, 1370);
            Set("menuPanel", settings.gameObject);
            BuildSettings(settings);
            var confirm = Rect("Leave Confirmation", overlay, 0, 30, 930, 850);
            Set("confirmationPanel", confirm.gameObject);
            BuildConfirmation(confirm);
            var win = Reuse(victory, "NextLevelPanel", overlay, 960, 1510);
            Set("victoryPanel", win.gameObject);
            BuildVictory(win);
            var lose = Reuse(loss, "LosePanel", overlay, 930, 1370);
            Set("lossPanel", lose.gameObject);
            BuildLoss(lose);

            var pauseImage = roots.Where(r => r != null).SelectMany(r => r.GetComponentsInChildren<Image>(true)).FirstOrDefault(i =>
                i.gameObject.scene == scene && (string.Equals(i.name, "pause", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(i.name, "Pause Button", StringComparison.OrdinalIgnoreCase) ||
                (i.sprite != null && i.sprite.name.StartsWith("HUD_PauseButton"))));
            if (pauseImage == null) throw new InvalidOperationException("The authored HUD pause icon could not be found.");
            var pause = pauseImage.GetComponent<Button>();
            if (pause == null) pause = pauseImage.gameObject.AddComponent<Button>();
            pauseImage.raycastTarget = true;
            pause.targetGraphic = pauseImage;
            Bind(pause, controller.Pause);
            Set("pauseButton", pause);

            foreach (var heartImage in roots.Where(r => r != null).SelectMany(r => r.GetComponentsInChildren<Image>(true)).Where(i =>
                i.sprite != null && i.sprite.name.StartsWith("HUD_Heart")))
            {
                foreach (var text in heartImage.GetComponentsInChildren<TMP_Text>(true).Where(t => t.name == "Heart Count").ToArray())
                    UnityEngine.Object.DestroyImmediate(text.gameObject);
                foreach (var text in heartImage.GetComponentsInChildren<TMP_Text>(true)) text.gameObject.SetActive(false);
                foreach (var text in heartImage.GetComponentsInChildren<Text>(true)) text.gameObject.SetActive(false);
                var heartLabel = Label(heartImage.transform, "Heart Count", "5", 0, 0, 100, 100, 30);
                Stretch(heartLabel.rectTransform);
                heartLabel.rectTransform.anchorMin = new Vector2(.15f, .1f);
                heartLabel.rectTransform.anchorMax = new Vector2(.85f, .9f);
                var heart = heartImage.GetComponent<GameplayHeartHUD>();
                if (heart == null) heart = heartImage.gameObject.AddComponent<GameplayHeartHUD>();
                var heartFields = new SerializedObject(heart);
                heartFields.FindProperty("label").objectReferenceValue = heartLabel;
                heartFields.ApplyModifiedPropertiesWithoutUndo();
            }

            nav.FindProperty("winPanel").objectReferenceValue = win.gameObject;
            nav.FindProperty("losePanel").objectReferenceValue = lose.gameObject;
            nav.FindProperty("settingPanel").objectReferenceValue = settings.gameObject;
            nav.ApplyModifiedPropertiesWithoutUndo();
            fields.ApplyModifiedPropertiesWithoutUndo();
            foreach (var panel in new[] { settings, confirm, win, lose }) panel.gameObject.SetActive(false);
            overlay.gameObject.SetActive(false);

            var grid = roots.Where(r => r != null).SelectMany(r => r.GetComponentsInChildren<GridManager>(true)).Single();
            if (grid.blockCountText == null) throw new InvalidOperationException("The HUD block counter is not assigned.");
            var counter = grid.blockCountText;
            counter.enableAutoSizing = false;
            counter.font = font;
            counter.fontSharedMaterial = textMaterial;
            counter.fontStyle = FontStyles.Normal;
            counter.fontSize = 58;
            counter.text = "0/0";
            counter.alignment = TextAlignmentOptions.Midline;
            counter.textWrappingMode = TextWrappingModes.NoWrap;
            counter.overflowMode = TextOverflowModes.Overflow;
            counter.margin = Vector4.zero;
            counter.raycastTarget = false;
            var countFrame = counter.transform.parent.GetComponentsInChildren<Image>(true)
                .FirstOrDefault(i => i.sprite != null && i.sprite.name.StartsWith("HUD_CurrencyFrame"));
            if (countFrame != null)
            {
                var groupRect = (RectTransform)counter.transform.parent;
                groupRect.anchorMin = groupRect.anchorMax = Vector2.one;
                groupRect.pivot = new Vector2(1, .5f);
                groupRect.anchoredPosition = new Vector2(-145, -86);
                groupRect.sizeDelta = new Vector2(320, 125);
                groupRect.localScale = Vector3.one;
                var frameRect = countFrame.rectTransform;
                frameRect.anchorMin = frameRect.anchorMax = new Vector2(1, .5f);
                frameRect.pivot = new Vector2(.5f, .5f);
                frameRect.anchoredPosition = new Vector2(-115, 0);
                frameRect.sizeDelta = new Vector2(230, 70);
                frameRect.localScale = Vector3.one;
                countFrame.preserveAspect = true;
                var textRect = counter.rectTransform;
                textRect.anchorMin = frameRect.anchorMin;
                textRect.anchorMax = frameRect.anchorMax;
                textRect.pivot = frameRect.pivot;
                textRect.anchoredPosition = new Vector2(-100, 0);
                textRect.sizeDelta = new Vector2(200, 70);
                textRect.localScale = Vector3.one;
                var cubes = groupRect.GetComponentsInChildren<Image>(true).FirstOrDefault(i => i.sprite != null && i.sprite.name.StartsWith("HUD_CubeCluster"));
                if (cubes != null)
                {
                    var cubeRect = cubes.rectTransform;
                    cubeRect.anchorMin = cubeRect.anchorMax = new Vector2(0, .5f);
                    cubeRect.pivot = new Vector2(.5f, .5f);
                    cubeRect.anchoredPosition = new Vector2(55, 0);
                    cubeRect.sizeDelta = new Vector2(118, 110);
                    cubeRect.localScale = Vector3.one;
                    cubes.preserveAspect = true;
                }
                EditorUtility.SetDirty(countFrame);
            }
            EditorUtility.SetDirty(counter);

            if (!File.Exists("Temp/Game.before-popup-ui.unity")) File.Copy(ScenePath, "Temp/Game.before-popup-ui.unity");
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save Game UI.");
            var report = new StringBuilder("PASS: authored Game popup UI saved to Assets/Scenes/Game.unity\n");
            report.AppendLine("Existing NavigationManager win/lose objects reused; settings/confirmation saved as scene objects.");
            report.AppendLine("HUD total-block font: 44.75 -> 58, bold UI font, centered inside a proportionally enlarged counter frame. No board, shop or camera layout moved.");
            report.AppendLine("Counter group components: " + string.Join(", ", counter.transform.parent.GetComponents<Component>().Select(c => c.GetType().Name)));
            report.AppendLine("All modal panels and their blocker are inactive in the saved scene.");
            report.AppendLine("Game Popup Canvas: " + root.GetComponentsInChildren<RectTransform>(true).Length + " editable UI GameObjects.");
            File.WriteAllText(Result, report.ToString());
            Selection.activeGameObject = root.gameObject;
        }
        finally { if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original); }
    }

    static void BuildSettings(RectTransform panel)
    {
        Picture(panel, "Settings Frame and Robot", Sprite("Settings_Atlas_Cut", "SettingsPanel"), 0, 70, 900, 1170);
        Label(panel, "Settings Title", "CÀI ĐẶT", 0, 560, 700, 100, 86);
        Close(panel, 410, 565, controller.Resume);
        var home = Row(panel, "Home Row", "VỀ HOME", "SettingsHome", 390, controller.ExitToMenu);
        Label(home.transform, "Home Arrow", "›", 320, 0, 60, 90, 80);
        Row(panel, "Music Row", "NHẠC NỀN", "SettingsMusic", 240, controller.ToggleMusic);
        State(panel.Find("Music Row"), "music", "Music");
        Row(panel, "Vibration Row", "RUNG", "SettingsVibration", 90, controller.ToggleVibration);
        State(panel.Find("Vibration Row"), "vibration", "Vibration");
        Row(panel, "Sound Row", "ÂM THANH", "SettingsSound", -60, controller.ToggleSound);
        State(panel.Find("Sound Row"), "sound", "Sound");
        Set("settingsLevelLabel", Label(panel, "Settings Level", "CẤP ĐỘ 1", 0, -555, 700, 70, 48));
        Set("continueButton", Button(panel, "Continue Button", Sprite("VictoryRewards_Atlas_Cut", "VictoryRewards_Atlas_Cut_8"),
            "TIẾP TỤC", 0, -665, 650, 130, 57, controller.Resume));
    }

    static Button Row(Transform panel, string name, string text, string icon, float y, UnityAction action)
    {
        var button = Button(panel, name, Sprite("Settings_Atlas_Cut", "SettingsRow"), "", 0, y, 790, 144, 48, action);
        Picture(button.transform, name + " Icon", Sprite("Settings_Atlas_Cut", icon), -319, 0, 79, 83);
        var label = Label(button.transform, name + " Label", text, -68, 0, 390, 100, 49);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        return button;
    }

    static void State(Transform parent, string field, string title)
    {
        foreach (bool on in new[] { true, false })
        {
            var state = Rect(title + (on ? " ON" : " OFF"), parent, 258, 0, 205, 94);
            Picture(state, "Switch Track", Sprite("Settings_Atlas_Cut", on ? "SettingsToggleOn" : "SettingsToggleOff"), 0, 0, 205, 94);
            Picture(state, "Switch Knob", Sprite("Settings_Atlas_Cut", "SettingsToggleKnob"), on ? 56 : -56, 0, 75, 75);
            Label(state, "Switch State", on ? "ON" : "OFF", on ? -35 : 32, 0, 95, 67, 35);
            state.gameObject.SetActive(on);
            Set(field + (on ? "On" : "Off"), state.gameObject);
        }
    }

    static void BuildConfirmation(RectTransform panel)
    {
        Picture(panel, "Exit Frame and Heart", Sprite("ExitConfirm_Atlas_Cut", "ExitConfirm_Atlas_Cut_0"), 0, 60, 930, 633);
        Label(panel, "Exit Title", "THOÁT VÁN?", 0, 300, 740, 100, 83, Gold);
        Close(panel, 413, 330, controller.CancelLeave);
        Set("confirmationMessage", Label(panel, "Heart Warning", "Bạn sẽ mất 1 tim", 0, -115, 790, 120, 42));
        Button(panel, "Stay Button", Sprite("ExitConfirm_Atlas_Cut", "ExitConfirm_Atlas_Cut_7"), "Ở LẠI", -225, -245, 425, 155, 49, controller.CancelLeave);
        Button(panel, "Confirm Leave Button", Sprite("ExitConfirm_Atlas_Cut", "ExitConfirm_Atlas_Cut_6"), "THOÁT VÁN", 225, -245, 425, 155, 49, controller.ConfirmLeave);
    }

    static void BuildLoss(RectTransform panel)
    {
        Picture(panel, "Loss Frame and Robot", Sprite("OutOfSpace_Atlas_Cut", "OutOfSpace_Atlas_Cut_0"), 0, 190, 930, 737);
        Label(panel, "Loss Title", "HẾT CHỖ TRỐNG!", 0, 465, 800, 95, 77, Gold);
        Close(panel, 410, 480, controller.ExitToMenu);
        Label(panel, "Loss Message", "Thử lại để chinh phục màn chơi!", 0, -240, 880, 90, 42);
        Button(panel, "Play Again Button", Sprite("OutOfSpace_Atlas_Cut", "OutOfSpace_Atlas_Cut_1"), "CHƠI LẠI", 0, -370, 860, 181, 67, controller.Restart);
        Button(panel, "Loss Home Button", Sprite("OutOfSpace_Atlas_Cut", "OutOfSpace_Atlas_Cut_2"), "VỀ HOME", 0, -550, 860, 181, 62, controller.ExitToMenu);
        Set("lossLevelLabel", Label(panel, "Loss Level", "CẤP ĐỘ 1", 0, -665, 820, 65, 40));
    }

    static void BuildVictory(RectTransform panel)
    {
        Label(panel, "Victory Title", "CHIẾN THẮNG!", 0, 650, 930, 120, 94, Gold);
        for (int i = 0; i < 3; i++)
            Picture(panel, "Victory Star " + (i + 1), Sprite("VictoryRewards_Atlas_Cut", "VictoryRewards_Atlas_Cut_4"),
                (i - 1) * 170, i == 1 ? 505 : 475, i == 1 ? 175 : 140, i == 1 ? 175 : 140);
        Picture(panel, "Victory Robot and Confetti", Sprite("VictoryRewards_Atlas_Cut", "VictoryRewards_Atlas_Cut_0"), 0, 205, 950, 512);
        Picture(panel, "Completion Banner", Sprite("VictoryRewards_Atlas_Cut", "VictoryRewards_Atlas_Cut_1"), 0, -145, 920, 188);
        Set("victoryLevelLabel", Label(panel, "Completed Level", "HOÀN THÀNH CẤP ĐỘ 1", 0, -145, 850, 130, 57));
        Picture(panel, "Victory Reward Coin", Sprite("VictoryRewards_Atlas_Cut", "VictoryCoins"), -125, -425, 175, 138);
        Set("victoryRewardAmountLabel", Label(panel, "Victory Reward Amount", "100", 105, -425, 260, 130, 88, Gold));
        Set("victoryRewardStatusLabel", Label(panel, "Victory Reward Status", "", 0, -565, 860, 42, 27));
        var doubleReward = Button(panel, "Victory Double Reward Button", Sprite("ExitConfirm_Atlas_Cut", "ExitConfirm_Atlas_Cut_7"),
            "NHẬN X2", -245, -665, 440, 155, 48, controller.ClaimDoubleVictoryReward);
        var doubleLabel = doubleReward.transform.Find("Label").GetComponent<RectTransform>();
        doubleLabel.anchoredPosition = new Vector2(0, 20);
        doubleLabel.sizeDelta = new Vector2(409.2f, 78);
        Label(doubleReward.transform, "Ad Caption", "XEM QUẢNG CÁO", 0, -39, 409.2f, 40, 27);
        Set("victoryDoubleRewardButton", doubleReward);
        Set("victoryClaimRewardButton", Button(panel, "Next Level Button", Sprite("VictoryRewards_Atlas_Cut", "VictoryRewards_Atlas_Cut_8"),
            "NHẬN & TIẾP", 245, -665, 440, 155, 43, controller.ClaimVictoryRewardAndContinue));
        BuildSimulatedVictoryAd();
    }

    static void BuildSimulatedVictoryAd()
    {
        var overlay = Rect("Simulated Victory Ad", controller.transform, 0, 0, 0, 0);
        Stretch(overlay);
        var dim = overlay.gameObject.AddComponent<Image>();
        dim.color = new Color32(9, 4, 36, 235); dim.raycastTarget = true;
        Picture(overlay, "Test Ad Frame", Sprite("Settings_Atlas_Cut", "SettingsPanel"), 0, 0, 900, 1170);
        Label(overlay, "Test Ad Title", "QUẢNG CÁO THỬ", 0, 430, 620, 100, 65);
        Picture(overlay, "Test Ad Reward Coin", Sprite("VictoryRewards_Atlas_Cut", "VictoryCoins"), -125, 140, 175, 138);
        Label(overlay, "Test Ad Reward Amount", "200", 105, 140, 260, 130, 88, Gold);
        Label(overlay, "Test Ad Subtitle", "Sau 5 giây có thể nhận x2", 0, -70, 820, 80, 38);
        Set("simulatedAdCountdownLabel", Label(overlay, "Test Ad Countdown", "Thời gian còn lại: 5s", 0, -190, 820, 75, 42));
        var claim = Button(overlay, "Test Ad Claim Button", Sprite("VictoryRewards_Atlas_Cut", "VictoryRewards_Atlas_Cut_8"),
            "NHẬN X2", 0, -390, 760, 150, 50, controller.FinishSimulatedVictoryAd);
        claim.interactable = false;
        Set("simulatedAdClaimButton", claim);
        Set("simulatedAdCloseButton", Button(overlay, "Test Ad Close Button", Sprite("Special_Level_Confirm_Close", "Special_Level_Confirm_Close"),
            "", 410, 470, 95, 95, 40, controller.CancelSimulatedVictoryAd));
        Set("simulatedAdPanel", overlay.gameObject);
        overlay.gameObject.SetActive(false);
    }

    static readonly Color Gold = new Color32(255, 233, 55, 255);
    static void Set(string name, UnityEngine.Object value)
    {
        var property = fields.FindProperty(name);
        if (property == null) throw new InvalidOperationException("Unknown GamePauseUI field: " + name);
        property.objectReferenceValue = value;
    }
    static Sprite Sprite(string atlas, string name)
    {
        var sprite = AssetDatabase.LoadAllAssetsAtPath("Assets/UI/Popup/" + atlas + ".png").OfType<Sprite>().FirstOrDefault(s => s.name == name);
        if (sprite == null) throw new InvalidOperationException("Missing popup sprite " + name);
        return sprite;
    }
    static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        if (parent != null) rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }
    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
    static RectTransform Reuse(GameObject obj, string name, Transform parent, float w, float h)
    {
        if (obj == null) return Rect(name, parent, 0, 0, w, h);
        while (obj.transform.childCount > 0) UnityEngine.Object.DestroyImmediate(obj.transform.GetChild(0).gameObject);
        var image = obj.GetComponent<Image>();
        if (image != null) UnityEngine.Object.DestroyImmediate(image);
        var rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(w, h);
        return rect;
    }
    static Image Picture(Transform parent, string name, Sprite sprite, float x, float y, float w, float h)
    {
        var image = Rect(name, parent, x, y, w, h).gameObject.AddComponent<Image>();
        image.sprite = sprite; image.preserveAspect = true; image.raycastTarget = false;
        return image;
    }
    static TextMeshProUGUI Label(Transform parent, string name, string text, float x, float y, float w, float h, float size, Color? color = null)
    {
        var label = Rect(name, parent, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font; label.text = text; label.fontSize = size;
        label.fontStyle = FontStyles.Bold; label.color = color ?? Color.white;
        label.alignment = TextAlignmentOptions.Midline; label.margin = Vector4.zero;
        label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;
        label.fontSharedMaterial = textMaterial;
        return label;
    }
    static Button Button(Transform parent, string name, Sprite sprite, string text, float x, float y, float w, float h, float size, UnityAction action)
    {
        var image = Picture(parent, name, sprite, x, y, w, h);
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var colors = button.colors; colors.highlightedColor = new Color(.95f, .95f, 1f); colors.pressedColor = new Color(.70f, .68f, .86f); colors.selectedColor = Color.white;
        button.colors = colors; button.navigation = new Navigation { mode = Navigation.Mode.None };
        Bind(button, action);
        if (!string.IsNullOrEmpty(text)) Label(button.transform, "Label", text, 0, 0, w * .93f, h * .8f, size);
        return button;
    }
    static void Bind(Button button, UnityAction action)
    {
        button.onClick = new Button.ButtonClickedEvent();
        UnityEventTools.AddPersistentListener(button.onClick, action);
    }
    static void Close(Transform parent, float x, float y, UnityAction action)
    {
        Button(parent, "Close Button", Sprite("Special_Level_Confirm_Close", "Special_Level_Confirm_Close"), "", x, y, 95, 95, 40, action);
    }
}
#endif
