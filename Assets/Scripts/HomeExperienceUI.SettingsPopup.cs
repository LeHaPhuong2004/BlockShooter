using System;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed partial class HomeExperienceUI
{
    // The Settings visual tree is serialized into Home.unity by the Editor baker.
    private const string SettingsAtlasPath = "Assets/UI/Popup/Settings_UI.png";
    private const float SettingsPanelAspect = 767f / 979f;
    private GameObject settingsLanguageOptionsRoot;
    private Button settingsLanguageVietnameseButton;
    private Button settingsLanguageEnglishButton;

#if UNITY_EDITOR
    public void BuildSettingsPopupObjectsForEditor()
    {
        if (Application.isPlaying)
            throw new InvalidOperationException("Build the Settings popup outside Play Mode.");

        LoadHomeResources();
        sceneHomeCanvas = SceneComponent<Canvas>("Home Canvas", transform);
        if (sceneHomeCanvas == null)
            throw new InvalidOperationException("The authored Home Canvas is missing.");
        homeCanvasObject = sceneHomeCanvas.gameObject;

        UnityEngine.Object[] imported = AssetDatabase.LoadAllAssetsAtPath(SettingsAtlasPath);
        Sprite[] atlasSprites = imported == null
            ? Array.Empty<Sprite>()
            : Array.ConvertAll(imported, asset => asset as Sprite);

        Sprite Atlas(int index)
        {
            string spriteName = "Settings_UI_" + index;
            foreach (Sprite sprite in atlasSprites)
                if (sprite != null && sprite.name == spriteName)
                    return sprite;
            throw new InvalidOperationException("Missing sprite slice " + spriteName + " in " + SettingsAtlasPath);
        }

        Transform existing = sceneHomeCanvas.transform.Find("Settings Popup");
        if (existing != null)
            UnityEngine.Object.DestroyImmediate(existing.gameObject);

        var popup = new GameObject("Settings Popup", typeof(RectTransform), typeof(Image));
        popup.transform.SetParent(sceneHomeCanvas.transform, false);
        var popupRect = popup.GetComponent<RectTransform>();
        Stretch(popupRect);
        var dimmer = popup.GetComponent<Image>();
        dimmer.color = new Color32(8, 5, 34, 205);
        dimmer.raycastTarget = true;

        var window = new GameObject("Settings Window", typeof(RectTransform), typeof(Image));
        window.transform.SetParent(popup.transform, false);
        var windowRect = window.GetComponent<RectTransform>();
        windowRect.anchorMin = new Vector2(0.02f, 0.155f);
        windowRect.anchorMax = new Vector2(0.98f, 0.845f);
        windowRect.offsetMin = windowRect.offsetMax = Vector2.zero;
        var panelImage = window.GetComponent<Image>();
        panelImage.sprite = Atlas(0);
        panelImage.type = Image.Type.Simple;
        panelImage.preserveAspect = true;
        panelImage.raycastTarget = false;

        Color labelColor = new Color32(15, 29, 133, 255);
        AddSettingsText(window.transform, "Settings Title Label", "CÀI ĐẶT", 48,
            new Vector2(0.37f, 0.835f), new Vector2(0.91f, 0.945f), labelColor, TextAnchor.MiddleCenter);

        float[] rowY = { 0.748f, 0.647f, 0.556f, 0.465f, 0.374f, 0.283f, 0.192f };
        string[] labelNames = { "Music Label", "Vibration Label", "SFX Label", "Language Label", "Code Label", "Blind Mode Label", "Icon Support Label" };
        string[] labels = { "Nhạc nền", "Rung", "Hiệu ứng âm thanh", "Ngôn ngữ", "Nhập code", "Chế độ người mù", "Hỗ trợ biểu tượng" };
        int[] iconSlices = { 1, 4, 7, 9, 13, 14, 15 };
        settingsRowLabels = new Text[rowY.Length];
        for (int i = 0; i < rowY.Length; i++)
        {
            float y = rowY[i];
            var icon = AddSettingsImage(window.transform, "Settings Icon " + i, Atlas(iconSlices[i]),
                new Vector2(0.075f, y - 0.038f), new Vector2(0.225f, y + 0.038f));
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            settingsRowLabels[i] = AddSettingsText(window.transform, labelNames[i], labels[i], 30,
                new Vector2(0.255f, y - 0.036f), new Vector2(0.67f, y + 0.036f), labelColor, TextAnchor.MiddleLeft);
        }

        CreateSettingsToggle(window.transform, "Settings Toggle Music", rowY[0]);
        CreateSettingsToggle(window.transform, "Settings Toggle Vibration", rowY[1]);
        CreateSettingsToggle(window.transform, "Settings Toggle SFX", rowY[2]);
        CreateSettingsToggle(window.transform, "Settings Toggle Blind Mode", rowY[5]);
        CreateSettingsToggle(window.transform, "Settings Toggle Icon Support", rowY[6]);

        var language = CreateSettingsButton(window.transform, "Settings Language Dropdown", Atlas(10),
            new Vector2(0.56f, rowY[3] - 0.041f), new Vector2(0.95f, rowY[3] + 0.041f));
        AddSettingsText(language.transform, "Settings Language Value", "Tiếng Việt", 24,
            new Vector2(0.03f, 0.12f), new Vector2(0.78f, 0.88f), labelColor, TextAnchor.MiddleCenter);

        var languageOptions = new GameObject("Settings Language Options", typeof(RectTransform), typeof(Image));
        languageOptions.transform.SetParent(window.transform, false);
        var optionsRect = languageOptions.GetComponent<RectTransform>();
        optionsRect.anchorMin = new Vector2(0.56f, 0.302f);
        optionsRect.anchorMax = new Vector2(0.95f, 0.424f);
        optionsRect.offsetMin = optionsRect.offsetMax = Vector2.zero;
        var optionsImage = languageOptions.GetComponent<Image>();
        optionsImage.sprite = Atlas(11);
        optionsImage.type = Image.Type.Simple;
        optionsImage.preserveAspect = false;
        optionsImage.raycastTarget = false;
        var vietnameseOption = CreateSettingsOption(languageOptions.transform, "Settings Language Vietnamese Option",
            "Tiếng Việt", labelColor, new Vector2(0.035f, 0.51f), new Vector2(0.965f, 0.97f));
        var englishOption = CreateSettingsOption(languageOptions.transform, "Settings Language English Option",
            "English", labelColor, new Vector2(0.035f, 0.03f), new Vector2(0.965f, 0.49f));
        languageOptions.SetActive(false);

        var code = CreateSettingsButton(window.transform, "Settings Enter Code", Atlas(11),
            new Vector2(0.56f, rowY[4] - 0.041f), new Vector2(0.95f, rowY[4] + 0.041f));
        AddSettingsText(code.transform, "Settings Code Placeholder", "Nhập code...", 23,
            new Vector2(0.05f, 0.12f), new Vector2(0.95f, 0.88f), labelColor, TextAnchor.MiddleCenter);

        var help = CreateSettingsButton(window.transform, "Settings Help Button", Atlas(12),
            new Vector2(0.20f, 0.035f), new Vector2(0.82f, 0.155f));
        var headset = AddSettingsImage(help.transform, "Settings Help Icon", Atlas(16),
            new Vector2(0.10f, 0.10f), new Vector2(0.28f, 0.90f));
        headset.preserveAspect = true;
        headset.raycastTarget = false;
        AddSettingsText(help.transform, "Settings Help Label", "TRỢ GIÚP & HỖ TRỢ", 27,
            new Vector2(0.29f, 0.12f), new Vector2(0.94f, 0.88f), Color.white, TextAnchor.MiddleCenter);

        var close = CreateSettingsButton(window.transform, "Settings Close Button", Atlas(2),
            new Vector2(0.865f, 0.894f), new Vector2(1.025f, 1.019f));
        close.GetComponent<Image>().preserveAspect = true;
        var closeX = AddSettingsImage(close.transform, "Settings Close X", Atlas(3),
            new Vector2(0.20f, 0.20f), new Vector2(0.80f, 0.80f));
        closeX.preserveAspect = true;
        closeX.raycastTarget = false;

        popup.SetActive(false);
        settingsPopupRoot = popup;
        settingsWindowRect = windowRect;
        settingsLanguageOptionsRoot = languageOptions;
        settingsLanguageVietnameseButton = vietnameseOption;
        settingsLanguageEnglishButton = englishOption;
        lastSettingsScreenWidth = -1;
        lastSettingsScreenHeight = -1;
        UpdateSettingsPopupLayout(true);
        EditorUtility.SetDirty(popup);
    }

    private void CreateSettingsToggle(Transform parent, string name, float centerY)
    {
        var toggleObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Toggle), typeof(Outline));
        toggleObject.transform.SetParent(parent, false);
        var rect = toggleObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.71f, centerY - 0.041f);
        rect.anchorMax = new Vector2(0.95f, centerY + 0.041f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        var track = toggleObject.GetComponent<Image>();
        track.sprite = settingsToggleTrackSprite;
        track.type = Image.Type.Simple;
        track.preserveAspect = false;
        track.color = Color.white;
        track.raycastTarget = true;
        var outline = toggleObject.GetComponent<Outline>();
        outline.effectColor = new Color32(32, 118, 28, 190);
        outline.effectDistance = new Vector2(1f, -1f);

        var handleObject = new GameObject("Switch Handle", typeof(RectTransform), typeof(Image));
        handleObject.transform.SetParent(toggleObject.transform, false);
        var handleRect = handleObject.GetComponent<RectTransform>();
        handleRect.anchorMin = handleRect.anchorMax = new Vector2(0.22f, 0.5f);
        handleRect.sizeDelta = new Vector2(64f, 64f);
        var handle = handleObject.GetComponent<Image>();
        handle.sprite = settingsToggleKnobSprite;
        handle.preserveAspect = true;
        handle.raycastTarget = false;

        var state = AddSettingsText(toggleObject.transform, "State Label", "OFF", 20,
            new Vector2(0.48f, 0.17f), new Vector2(0.94f, 0.83f), Color.white, TextAnchor.MiddleCenter);
        state.fontStyle = FontStyle.Bold;
        state.raycastTarget = false;

        var toggle = toggleObject.GetComponent<Toggle>();
        toggle.transition = Selectable.Transition.None;
        toggle.targetGraphic = track;
    }

    private static Image AddSettingsImage(Transform parent, string name, Sprite sprite, Vector2 min, Vector2 max)
    {
        var imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        var rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = Color.white;
        image.type = Image.Type.Simple;
        image.raycastTarget = false;
        return image;
    }

    private Button CreateSettingsButton(Transform parent, string name, Sprite sprite, Vector2 min, Vector2 max)
    {
        var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        var rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var image = buttonObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = Color.white;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        var button = buttonObject.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = image;
        return button;
    }

    private Button CreateSettingsOption(Transform parent, string name, string value, Color color, Vector2 min, Vector2 max)
    {
        var optionObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        optionObject.transform.SetParent(parent, false);
        var rect = optionObject.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var image = optionObject.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0f);
        image.raycastTarget = true;
        var button = optionObject.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = image;
        AddSettingsText(optionObject.transform, name + " Label", value, 22,
            new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.98f), color, TextAnchor.MiddleCenter);
        return button;
    }

    private Text AddSettingsText(Transform parent, string name, string value, int size,
        Vector2 min, Vector2 max, Color color, TextAnchor alignment)
    {
        var labelObject = new GameObject(name, typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(parent, false);
        var rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var label = labelObject.GetComponent<Text>();
        label.font = roundedFont != null ? roundedFont : font;
        label.text = value;
        label.fontSize = size;
        label.fontStyle = FontStyle.Bold;
        label.color = color;
        label.alignment = alignment;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = Mathf.Max(12, size - 8);
        label.resizeTextMaxSize = size;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;
        return label;
    }
#endif

    private void BindSettingsPopup()
    {
        settingsPopupRoot = SceneObject("Settings Popup", homeCanvasObject != null ? homeCanvasObject.transform : null);
        if (settingsPopupRoot == null)
        {
            Debug.LogError("The authored Settings Popup is missing. Run Tools > Home UI > Bake into Home Scene.", this);
            return;
        }

        settingsWindowRect = SceneComponent<RectTransform>("Settings Window", settingsPopupRoot.transform);
        settingsTitleLabel = SceneComponent<Text>("Settings Title Label", settingsPopupRoot.transform);
        settingsLanguageValueLabel = SceneComponent<Text>("Settings Language Value", settingsPopupRoot.transform);
        settingsCodePlaceholderLabel = SceneComponent<Text>("Settings Code Placeholder", settingsPopupRoot.transform);
        settingsHelpLabel = SceneComponent<Text>("Settings Help Label", settingsPopupRoot.transform);
        settingsCloseButton = SceneComponent<Button>("Settings Close Button", settingsPopupRoot.transform);
        settingsLanguageButton = SceneComponent<Button>("Settings Language Dropdown", settingsPopupRoot.transform);
        settingsLanguageOptionsRoot = SceneObject("Settings Language Options", settingsPopupRoot.transform);
        settingsLanguageVietnameseButton = SceneComponent<Button>("Settings Language Vietnamese Option", settingsPopupRoot.transform);
        settingsLanguageEnglishButton = SceneComponent<Button>("Settings Language English Option", settingsPopupRoot.transform);
        settingsCodeButton = SceneComponent<Button>("Settings Enter Code", settingsPopupRoot.transform);
        settingsHelpButton = SceneComponent<Button>("Settings Help Button", settingsPopupRoot.transform);
        settingsRowLabels = new[]
        {
            SceneComponent<Text>("Music Label", settingsPopupRoot.transform),
            SceneComponent<Text>("Vibration Label", settingsPopupRoot.transform),
            SceneComponent<Text>("SFX Label", settingsPopupRoot.transform),
            SceneComponent<Text>("Language Label", settingsPopupRoot.transform),
            SceneComponent<Text>("Code Label", settingsPopupRoot.transform),
            SceneComponent<Text>("Blind Mode Label", settingsPopupRoot.transform),
            SceneComponent<Text>("Icon Support Label", settingsPopupRoot.transform)
        };

        settingToggleVisuals.Clear();
        BindSettingsToggle("Settings Toggle Music", "Mussic", true, 1);
        BindSettingsToggle("Settings Toggle Vibration", "Vibration", false, 1);
        BindSettingsToggle("Settings Toggle SFX", "SFX", false, 1);
        BindSettingsToggle("Settings Toggle Blind Mode", "VisualAccessbility", false, 0);
        BindSettingsToggle("Settings Toggle Icon Support", "IconAccesstionce", false, 1);

        if (settingsCloseButton != null)
        {
            settingsCloseButton.onClick.RemoveAllListeners();
            settingsCloseButton.onClick.AddListener(PlayButtonSfx);
            settingsCloseButton.onClick.AddListener(CloseSettingsPopup);
        }
        if (settingsLanguageButton != null)
        {
            settingsLanguageButton.onClick.RemoveAllListeners();
            settingsLanguageButton.onClick.AddListener(PlayButtonSfx);
            settingsLanguageButton.onClick.AddListener(ToggleSettingsLanguageOptions);
        }
        if (settingsLanguageOptionsRoot != null)
            settingsLanguageOptionsRoot.SetActive(false);
        if (settingsLanguageVietnameseButton != null)
        {
            settingsLanguageVietnameseButton.onClick.RemoveAllListeners();
            settingsLanguageVietnameseButton.onClick.AddListener(PlayButtonSfx);
            settingsLanguageVietnameseButton.onClick.AddListener(() => SelectSettingsLanguage(0));
        }
        if (settingsLanguageEnglishButton != null)
        {
            settingsLanguageEnglishButton.onClick.RemoveAllListeners();
            settingsLanguageEnglishButton.onClick.AddListener(PlayButtonSfx);
            settingsLanguageEnglishButton.onClick.AddListener(() => SelectSettingsLanguage(1));
        }
        if (settingsCodeButton != null)
        {
            settingsCodeButton.onClick.RemoveAllListeners();
            settingsCodeButton.onClick.AddListener(PlayButtonSfx);
        }
        if (settingsHelpButton != null)
        {
            settingsHelpButton.onClick.RemoveAllListeners();
            settingsHelpButton.onClick.AddListener(PlayButtonSfx);
        }
        RefreshSettingsLanguageLabels();
        UpdateSettingsPopupLayout(true);
    }

    private void BindSettingsToggle(string objectName, string key, bool musicSetting, int defaultState)
    {
        var toggle = SceneComponent<Toggle>(objectName, settingsPopupRoot.transform);
        if (toggle == null) return;
        var track = toggle.GetComponent<Image>();
        var outline = toggle.GetComponent<Outline>();
        var handle = SceneComponent<Image>("Switch Handle", toggle.transform);
        var handleRect = handle != null ? handle.rectTransform : null;
        var stateText = SceneComponent<Text>("State Label", toggle.transform);
        if (track == null || handle == null || stateText == null) return;

        int savedDefault = key == "Mussic" && GameMaster.staticScript != null
            ? GameMaster.MusicOnOff
            : key == "SFX" && GameMaster.staticScript != null ? GameMaster.SoundOnOff : defaultState;
        bool initialState = PlayerPrefs.GetInt(key, savedDefault) == 1;
        if (Application.isPlaying && !PlayerPrefs.HasKey(key))
        {
            PlayerPrefs.SetInt(key, initialState ? 1 : 0);
            PlayerPrefs.Save();
        }

        toggle.transition = Selectable.Transition.None;
        toggle.targetGraphic = track;
        toggle.isOn = initialState;
        stateText.text = initialState ? Localize.Text("BẬT", "ON") : Localize.Text("TẮT", "OFF");
        var visual = new SettingToggleVisual
        {
            track = track,
            outline = outline,
            handleRect = handleRect,
            handle = handle,
            stateRect = stateText.rectTransform,
            stateText = stateText,
            offTrackColor = new Color32(93, 101, 135, 255),
            onTrackColor = Color.white,
            offOutlineColor = new Color32(105, 113, 157, 190),
            onOutlineColor = new Color32(68, 255, 50, 225),
            value = initialState ? 1f : 0f,
            targetValue = initialState ? 1f : 0f
        };
        settingToggleVisuals.Add(visual);
        ApplySettingToggleVisual(visual, visual.value);
        if (musicSetting || key == "SFX") ApplyAudioSetting(musicSetting, initialState);

        toggle.onValueChanged.AddListener(value =>
        {
            bool enablingSfx = key == "SFX" && value;
            if (!enablingSfx) PlayButtonSfx();
            PlayerPrefs.SetInt(key, value ? 1 : 0);
            PlayerPrefs.Save();
            visual.targetValue = value ? 1f : 0f;
            stateText.text = value ? Localize.Text("BẬT", "ON") : Localize.Text("TẮT", "OFF");
            if (musicSetting || key == "SFX") ApplyAudioSetting(musicSetting, value);
            if (enablingSfx) PlayButtonSfx();
        });
    }

    private void RefreshSettingsLanguageLabels()
    {
        bool english = Localize.IsEnglish;
        if (settingsTitleLabel != null) settingsTitleLabel.text = english ? "SETTINGS" : "CÀI ĐẶT";
        string[] vietnamese = { "Nhạc nền", "Rung", "Hiệu ứng âm thanh", "Ngôn ngữ", "Nhập code", "Chế độ người mù", "Hỗ trợ biểu tượng" };
        string[] englishLabels = { "Music", "Vibration", "Sound effects", "Language", "Enter code", "Blind mode", "Icon support" };
        if (settingsRowLabels != null)
            for (int i = 0; i < settingsRowLabels.Length; i++)
                if (settingsRowLabels[i] != null)
                    settingsRowLabels[i].text = english ? englishLabels[i] : vietnamese[i];
        if (settingsLanguageValueLabel != null) settingsLanguageValueLabel.text = english ? "English" : "Tiếng Việt";
        if (settingsCodePlaceholderLabel != null) settingsCodePlaceholderLabel.text = english ? "Enter code..." : "Nhập code...";
        if (settingsHelpLabel != null) settingsHelpLabel.text = english ? "HELP & SUPPORT" : "TRỢ GIÚP & HỖ TRỢ";
    }

    private void ToggleSettingsLanguageOptions()
    {
        if (settingsLanguageOptionsRoot == null) return;
        bool opening = !settingsLanguageOptionsRoot.activeSelf;
        if (opening) settingsLanguageOptionsRoot.transform.SetAsLastSibling();
        settingsLanguageOptionsRoot.SetActive(opening);
    }

    private void SelectSettingsLanguage(int language)
    {
        if (settingsLanguageOptionsRoot != null) settingsLanguageOptionsRoot.SetActive(false);
        Localize.SetLanguage(language);
        RefreshSettingsLanguageLabels();
    }

    private void CloseSettingsPopup()
    {
        if (settingsLanguageOptionsRoot != null) settingsLanguageOptionsRoot.SetActive(false);
        if (settingsPopupRoot != null) settingsPopupRoot.SetActive(false);
    }

    private void UpdateSettingsPopupLayout(bool force = false)
    {
        if (settingsWindowRect == null) return;
        var parentRect = settingsWindowRect.parent as RectTransform;
        Vector2 size = parentRect != null ? parentRect.rect.size : Vector2.zero;
        if (size.x <= 0f || size.y <= 0f) size = new Vector2(1080f, 1920f);
        int width = Mathf.Max(1, Mathf.RoundToInt(size.x));
        int height = Mathf.Max(1, Mathf.RoundToInt(size.y));
        if (!force && width == lastSettingsScreenWidth && height == lastSettingsScreenHeight) return;
        lastSettingsScreenWidth = width;
        lastSettingsScreenHeight = height;

        // Keep the iPhone-authored size as wider screens gain canvas space.
        var scaler = sceneHomeCanvas != null ? sceneHomeCanvas.GetComponent<CanvasScaler>() : null;
        float referenceHeight = scaler != null && scaler.referenceResolution.y > 0f
            ? scaler.referenceResolution.y : 1920f;
        Vector2 phone = PortraitDisplay.ReferenceScreenSize;
        float referenceWidth = .96f * referenceHeight * phone.x / phone.y;
        float referenceWindowHeight = referenceWidth / SettingsPanelAspect;
        float fit = Mathf.Min(1f, .96f * size.x / referenceWidth, .90f * size.y / referenceWindowHeight);
        float windowWidth = referenceWidth * fit / size.x;
        float windowHeight = referenceWindowHeight * fit / size.y;

        settingsWindowRect.anchorMin = new Vector2(0.5f - windowWidth * 0.5f, 0.5f - windowHeight * 0.5f);
        settingsWindowRect.anchorMax = new Vector2(0.5f + windowWidth * 0.5f, 0.5f + windowHeight * 0.5f);
        settingsWindowRect.offsetMin = settingsWindowRect.offsetMax = Vector2.zero;
    }

    private void UpdateSettingToggleAnimations(float deltaTime)
    {
        const float transitionDuration = 0.20f;
        for (int i = settingToggleVisuals.Count - 1; i >= 0; i--)
        {
            SettingToggleVisual visual = settingToggleVisuals[i];
            if (visual == null || visual.track == null || visual.handleRect == null || visual.stateText == null)
            {
                settingToggleVisuals.RemoveAt(i);
                continue;
            }
            if (Mathf.Approximately(visual.value, visual.targetValue)) continue;
            visual.value = Mathf.MoveTowards(visual.value, visual.targetValue, deltaTime / transitionDuration);
            ApplySettingToggleVisual(visual, visual.value);
        }
    }

    private static void ApplySettingToggleVisual(SettingToggleVisual visual, float rawValue)
    {
        float value = Mathf.SmoothStep(0f, 1f, rawValue);
        if (visual.track != null) visual.track.color = Color.Lerp(visual.offTrackColor, visual.onTrackColor, value);
        if (visual.outline != null) visual.outline.effectColor = Color.Lerp(visual.offOutlineColor, visual.onOutlineColor, value);
        if (visual.handle != null) visual.handle.color = Color.Lerp(new Color32(218, 220, 240, 255), Color.white, value);
        if (visual.handleRect != null)
        {
            float x = Mathf.Lerp(0.22f, 0.78f, value);

            // Tỉ lệ rộng/cao của track, tính từ anchor (không cần popup đang hiện).
            // Cửa sổ Settings luôn giữ tỉ lệ SettingsPanelAspect.
            float trackAspect = 2.3f;
            if (visual.track != null)
            {
                RectTransform t = visual.track.rectTransform;
                float w = t.anchorMax.x - t.anchorMin.x;
                float h = t.anchorMax.y - t.anchorMin.y;
                if (w > 0f && h > 0f) trackAspect = w * SettingsPanelAspect / h;
            }

            const float heightFraction = 0.72f;                 // knob cao bằng 72% track
            float widthFraction = heightFraction / trackAspect;  // giữ knob hình tròn

            visual.handleRect.anchorMin = new Vector2(x - widthFraction * 0.5f, 0.5f - heightFraction * 0.5f);
            visual.handleRect.anchorMax = new Vector2(x + widthFraction * 0.5f, 0.5f + heightFraction * 0.5f);
            visual.handleRect.offsetMin = visual.handleRect.offsetMax = Vector2.zero;
        }
        if (visual.stateRect != null)
        {
            visual.stateRect.anchorMin = new Vector2(Mathf.Lerp(0.48f, 0.05f, value), 0.15f);
            visual.stateRect.anchorMax = new Vector2(Mathf.Lerp(0.94f, 0.49f, value), 0.85f);
        }
        if (visual.stateText != null) visual.stateText.color = Color.Lerp(new Color32(224, 229, 249, 255), Color.white, value);
    }

    private void RefreshSettingToggleVisuals()
    {
        foreach (SettingToggleVisual visual in settingToggleVisuals)
            if (visual != null) ApplySettingToggleVisual(visual, visual.value);
    }
}
