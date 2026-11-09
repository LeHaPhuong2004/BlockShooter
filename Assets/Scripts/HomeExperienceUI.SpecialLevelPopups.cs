using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public sealed partial class HomeExperienceUI
{
    private static readonly int[] SpecialLevelThresholds = { 25, 30, 50 };

    private GameObject specialLevelPopupLayer;
    private GameObject[] lockedLevelPopups;
    private GameObject[] specialLevelConfirmPopups;
    private Image[] lockedProgressFills;
    private Text[] lockedCurrentLevelValues;
    private Text[] lockedProgressValues;

#if UNITY_EDITOR
    /// <summary>
    /// Creates the popup hierarchy as saved scene GameObjects. Runtime code only updates
    /// numeric progress and toggles their active state; captions belong to the authored UI.
    /// </summary>
    public void BuildSpecialLevelPopupObjectsForEditor()
    {
        if (Application.isPlaying)
            throw new InvalidOperationException("Build the special-level popup objects outside Play Mode.");

        LoadHomeResources();
        var canvas = SceneComponent<Canvas>("Home Canvas", transform);
        if (canvas == null)
            throw new InvalidOperationException("The authored Home Canvas is missing.");

        var existingLayer = canvas.transform.Find("Special Level Popup Layer");
        if (existingLayer != null)
        {
            // Older baked buttons had an AspectRatioFitter on their root. It continually
            // rewrites the RectTransform, so Scene-view dragging appears to snap back.
            bool changed = false;
            foreach (var node in existingLayer.GetComponentsInChildren<Transform>(true))
            {
                if (node == null || !node.name.StartsWith("SPECIAL_CONFIRM_CLOSE_", StringComparison.Ordinal))
                    continue;

                var slot = node.parent;
                var obsoleteObject = slot != null && slot.name == node.name + " Slot"
                    ? slot.gameObject
                    : node.gameObject;
                DestroyImmediate(obsoleteObject);
                changed = true;
            }

            foreach (var button in existingLayer.GetComponentsInChildren<Button>(true))
            {
                bool buttonChanged = false;
                var fitter = button.GetComponent<AspectRatioFitter>();
                if (fitter != null)
                {
                    DestroyImmediate(fitter);
                    buttonChanged = true;
                }

                var image = button.GetComponent<Image>();
                if (image != null && !image.preserveAspect)
                {
                    image.preserveAspect = true;
                    EditorUtility.SetDirty(image);
                    buttonChanged = true;
                }

                if (buttonChanged)
                {
                    EditorUtility.SetDirty(button.gameObject);
                    changed = true;
                }
            }

            if (changed)
            {
                EditorUtility.SetDirty(existingLayer.gameObject);
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
            return;
        }

        Sprite[] lockedFrames =
        {
            LoadPopupAtlasSprite("Assets/UI/Popup/Locked_Level_Popups.png", "ui-atlas-separated_1"),
            LoadPopupAtlasSprite("Assets/UI/Popup/Locked_Level_Popups.png", "ui-atlas-separated_0"),
            LoadPopupAtlasSprite("Assets/UI/Popup/Locked_Level_Popups.png", "ui-atlas-separated_2")
        };
        Sprite greenButton = LoadPopupAtlasSprite("Assets/UI/Popup/Locked_Level_Popups.png", "ui-atlas-separated_3");
        Sprite blueButton = LoadPopupAtlasSprite("Assets/UI/Popup/Locked_Level_Popups.png", "ui-atlas-separated_4");
        Sprite lockIcon = LoadPopupAtlasSprite("Assets/UI/Popup/Locked_Level_Popups.png", "ui-atlas-separated_5");
        Sprite progressFill = LoadPopupAtlasSprite("Assets/UI/Popup/Locked_Level_Popups.png", "ui-atlas-separated_6");
        Sprite progressTrack = LoadPopupAtlasSprite("Assets/UI/Popup/Locked_Level_Popups.png", "ui-atlas-separated_7");
        Sprite closeButton = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Popup/Special_Level_Confirm_Close.png");
        Sprite[] confirmationFrames = new Sprite[SpecialLevelThresholds.Length];
        for (int i = 0; i < confirmationFrames.Length; i++)
        {
            string path = "Assets/UI/Popup/Special_Level_Confirm_Level" + SpecialLevelThresholds[i] + ".png";
            confirmationFrames[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        if (Array.Exists(lockedFrames, sprite => sprite == null) || greenButton == null || blueButton == null ||
            lockIcon == null || progressFill == null || progressTrack == null || closeButton == null ||
            Array.Exists(confirmationFrames, sprite => sprite == null))
            throw new InvalidOperationException("A special-level popup sprite is missing from Assets/UI/Popup.");

        var layer = new GameObject("Special Level Popup Layer", typeof(RectTransform));
        layer.transform.SetParent(canvas.transform, false);
        Stretch(layer.GetComponent<RectTransform>());
        var dimObject = new GameObject("Dim Background", typeof(RectTransform), typeof(Image));
        dimObject.transform.SetParent(layer.transform, false);
        Stretch(dimObject.GetComponent<RectTransform>());
        var dim = dimObject.GetComponent<Image>();
        dim.color = new Color(0.025f, 0.012f, 0.08f, 0.74f);
        dim.raycastTarget = true;

        for (int i = 0; i < SpecialLevelThresholds.Length; i++)
        {
            int threshold = SpecialLevelThresholds[i];
            BuildLockedLevelPopup(layer.transform, threshold, lockedFrames[i], lockIcon,
                progressTrack, progressFill, blueButton, closeButton);
            BuildSpecialLevelConfirmPopup(layer.transform, threshold, confirmationFrames[i], greenButton,
                blueButton);
        }

        layer.SetActive(false);
        EditorUtility.SetDirty(layer);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }

    private static Sprite LoadPopupAtlasSprite(string path, string spriteName)
    {
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            if (asset is Sprite sprite && sprite.name == spriteName)
                return sprite;
        return null;
    }

    private void BuildLockedLevelPopup(Transform layer, int threshold, Sprite frameSprite, Sprite lockSprite,
        Sprite trackSprite, Sprite fillSprite, Sprite acknowledgeSprite, Sprite closeSprite)
    {
        var window = CreatePopupRect(layer, "Locked Level Popup " + threshold, Vector2.zero, Vector2.one);
        var frameArea = CreatePopupRect(window.transform, "Frame Area", new Vector2(0.03f, 0.17f), new Vector2(0.97f, 0.83f));
        var frame = CreatePopupImage(frameArea.transform, "Frame", frameSprite, Vector2.zero, Vector2.one, true);
        FitPopupImageToSprite(frame, frameSprite);

        CreatePopupText(frame.transform, "Title", "CHƯA MỞ KHÓA", 34,
            new Vector2(0.24f, 0.82f), new Vector2(0.83f, 0.98f), Color.white, true);
        CreatePopupImage(frame.transform, "Lock Icon", lockSprite,
            new Vector2(0.47f, 0.59f), new Vector2(0.62f, 0.85f), true);

        var track = CreatePopupImage(frame.transform, "Progress Track", trackSprite,
            new Vector2(0.56f, 0.45f), new Vector2(0.96f, 0.58f), true);
        var fill = CreatePopupImage(track.transform, "Progress Fill", fillSprite,
            new Vector2(0.018f, 0.13f), new Vector2(0.45f, 0.87f), true);
        fill.raycastTarget = false;

        CreatePopupButton(frame.transform, "SPECIAL_LOCK_ACK_" + threshold, acknowledgeSprite, "ĐÃ HIỂU", 25,
            new Vector2(0.55f, 0.12f), new Vector2(0.43f, 0.18f));
        CreatePopupButton(frame.transform, "SPECIAL_LOCK_CLOSE_" + threshold, closeSprite, string.Empty, 20,
            new Vector2(0.89f, 0.84f), new Vector2(0.13f, 0.22f));
        window.SetActive(false);
    }

    private void BuildSpecialLevelConfirmPopup(Transform layer, int threshold, Sprite frameSprite,
        Sprite greenButtonSprite, Sprite blueButtonSprite)
    {
        var window = CreatePopupRect(layer, "Special Level Confirm " + threshold, Vector2.zero, Vector2.one);
        var frameArea = CreatePopupRect(window.transform, "Frame Area", new Vector2(0.03f, 0.335f), new Vector2(0.97f, 0.665f));
        var frame = CreatePopupImage(frameArea.transform, "Frame", frameSprite,
            Vector2.zero, Vector2.one, true);
        FitPopupImageToSprite(frame, frameSprite);

        CreatePopupText(frame.transform, "Title", "SẴN SÀNG CHƯA?", 34,
            new Vector2(0.22f, 0.83f), new Vector2(0.80f, 0.99f), Color.white, true);

        CreatePopupButton(frame.transform, "SPECIAL_CONFIRM_START_" + threshold, greenButtonSprite,
            "CHƠI NGAY", 25, new Vector2(0.27f, 0.105f), new Vector2(0.43f, 0.15f));
        CreatePopupButton(frame.transform, "SPECIAL_CONFIRM_LATER_" + threshold, blueButtonSprite,
            "ĐỂ SAU", 25, new Vector2(0.73f, 0.105f), new Vector2(0.43f, 0.15f));
        window.SetActive(false);
    }

    private static GameObject CreatePopupRect(Transform parent, string name, Vector2 min, Vector2 max)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return go;
    }

    private static Image CreatePopupImage(Transform parent, string name, Sprite sprite,
        Vector2 min, Vector2 max, bool preserveAspect)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = preserveAspect;
        image.type = Image.Type.Simple;
        image.color = Color.white;
        image.raycastTarget = false;
        return image;
    }

    private static void FitPopupImageToSprite(Image image, Sprite sprite)
    {
        var fitter = image.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = sprite.rect.width / sprite.rect.height;
    }

    private Text CreatePopupText(Transform parent, string name, string value, int size,
        Vector2 min, Vector2 max, Color color, bool outlined)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var text = go.GetComponent<Text>();
        text.font = font;
        text.text = Localize.Translate(value);
        text.fontSize = size;
        text.fontStyle = FontStyle.Bold;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = Mathf.Max(15, size - 10);
        text.resizeTextMaxSize = size;
        text.raycastTarget = false;
        if (outlined)
        {
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color32(15, 22, 113, 220);
            outline.effectDistance = new Vector2(2f, -2f);
        }
        return text;
    }

    private void CreatePopupButton(Transform parent, string name, Sprite sprite, string label, int fontSize,
        Vector2 center, Vector2 size)
    {
        var slot = CreatePopupRect(parent, name + " Slot", center - size * 0.5f, center + size * 0.5f);
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(slot.transform, false);
        Stretch(go.GetComponent<RectTransform>());
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.type = Image.Type.Simple;
        image.color = Color.white;
        image.raycastTarget = true;
        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        CreatePopupText(go.transform, "Label", label, fontSize,
            Vector2.zero, Vector2.one, Color.white, true);
    }

#endif

    private void BindSpecialLevelPopups()
    {
        specialLevelPopupLayer = SceneObject("Special Level Popup Layer", sceneHomeCanvas.transform);
        if (specialLevelPopupLayer == null)
            throw new InvalidOperationException("The authored Special Level Popup Layer is missing from Home.unity.");

        int count = SpecialLevelThresholds.Length;
        lockedLevelPopups = new GameObject[count];
        specialLevelConfirmPopups = new GameObject[count];
        lockedProgressFills = new Image[count];
        lockedCurrentLevelValues = new Text[count];
        lockedProgressValues = new Text[count];

        for (int i = 0; i < count; i++)
        {
            int level = SpecialLevelThresholds[i];
            lockedLevelPopups[i] = SceneObject("Locked Level Popup " + level, specialLevelPopupLayer.transform);
            specialLevelConfirmPopups[i] = SceneObject("Special Level Confirm " + level, specialLevelPopupLayer.transform);
            if (lockedLevelPopups[i] == null || specialLevelConfirmPopups[i] == null)
                throw new InvalidOperationException("Missing authored popup GameObjects for special level " + level + ".");

            var track = SceneObject("Progress Track", lockedLevelPopups[i].transform);
            lockedProgressFills[i] = track != null ? SceneComponent<Image>("Progress Fill", track.transform) : null;
            lockedCurrentLevelValues[i] = SceneComponent<Text>("NumberCurrentLV", lockedLevelPopups[i].transform);
            lockedProgressValues[i] = track != null ? SceneComponent<Text>("Title (5)", track.transform) : null;
            if (lockedProgressFills[i] == null || lockedCurrentLevelValues[i] == null || lockedProgressValues[i] == null)
                throw new InvalidOperationException("A special-level popup is missing its authored progress or numeric labels: " + level + ".");
        }

        RefreshSpecialLevelProgress();
        specialLevelPopupLayer.SetActive(false);
    }

    private void RefreshSpecialLevelProgress()
    {
        if (lockedProgressFills == null || lockedCurrentLevelValues == null || lockedProgressValues == null) return;
        int currentLevel = GetCurrentPlayerLevel();
        string current = currentLevel.ToString(CultureInfo.InvariantCulture);
        for (int i = 0; i < SpecialLevelThresholds.Length; i++)
        {
            int threshold = SpecialLevelThresholds[i];
            // Update existing number fields only; localized captions stay in the scene.
            lockedCurrentLevelValues[i].text = current;
            lockedProgressValues[i].text = current + "/" + threshold.ToString(CultureInfo.InvariantCulture);
            Image fill = lockedProgressFills[i];
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.preserveAspect = false;
            fill.fillAmount = Mathf.Clamp01(currentLevel / (float)threshold);
        }
    }

    private Action SpecialLevelPopupButtonAction(Button button)
    {
        if (button == null) return null;
        string buttonName = button.name;
        for (int i = 0; i < SpecialLevelThresholds.Length; i++)
        {
            int level = SpecialLevelThresholds[i];
            if (buttonName == "LOCKED_" + level)
                return () => OpenSpecialLevelPopup(level);

            // Duplicated authored panels can retain another level's button suffix.
            // The containing bound panel determines which level its actions use.
            if (lockedLevelPopups != null && i < lockedLevelPopups.Length && lockedLevelPopups[i] != null &&
                button.transform.IsChildOf(lockedLevelPopups[i].transform) &&
                (buttonName.StartsWith("SPECIAL_LOCK_ACK_", StringComparison.Ordinal) ||
                 buttonName.StartsWith("SPECIAL_LOCK_CLOSE_", StringComparison.Ordinal)))
                return CloseSpecialLevelPopup;
            if (specialLevelConfirmPopups == null || i >= specialLevelConfirmPopups.Length ||
                specialLevelConfirmPopups[i] == null || !button.transform.IsChildOf(specialLevelConfirmPopups[i].transform))
                continue;
            if (buttonName.StartsWith("SPECIAL_CONFIRM_LATER_", StringComparison.Ordinal))
                return CloseSpecialLevelPopup;
            if (buttonName.StartsWith("SPECIAL_CONFIRM_START_", StringComparison.Ordinal))
                return () => EnterSpecialLevel(level);
        }
        return null;
    }

    private void OpenSpecialLevelPopup(int threshold)
    {
        int index = Array.IndexOf(SpecialLevelThresholds, threshold);
        if (index < 0 || specialLevelPopupLayer == null) return;

        CloseOutOfHearts();
        for (int i = 0; i < lockedLevelPopups.Length; i++)
        {
            lockedLevelPopups[i].SetActive(false);
            specialLevelConfirmPopups[i].SetActive(false);
        }

        RefreshSpecialLevelProgress();
        int currentLevel = GetCurrentPlayerLevel();
        if (currentLevel < threshold)
        {
            lockedLevelPopups[index].SetActive(true);
        }
        else
        {
            specialLevelConfirmPopups[index].SetActive(true);
        }

        specialLevelPopupLayer.SetActive(true);
        specialLevelPopupLayer.transform.SetAsLastSibling();
    }

    private static int GetCurrentPlayerLevel()
    {
        if (LevelManager.Instance != null)
            return Mathf.Max(1, LevelManager.Instance.CurrentLevel);
        if (GameMaster.staticScript != null)
            return Mathf.Max(1, GameMaster.CurrLevel);
        return Mathf.Max(1, PlayerPrefs.GetInt("CurrentLevel", 1));
    }

    private void CloseSpecialLevelPopup()
    {
        if (specialLevelPopupLayer != null)
            specialLevelPopupLayer.SetActive(false);
    }

    private void EnterSpecialLevel(int level)
    {
        if (!HeartManager.CanPlay)
        {
            CloseSpecialLevelPopup();
            ShowEnergy();
            return;
        }

        if (LevelManager.Instance != null)
            LevelManager.Instance.SetLevel(level);
        else
        {
            PlayerPrefs.SetInt("CurrentLevel", level);
            PlayerPrefs.Save();
        }

        CloseSpecialLevelPopup();
        if (HomeController.staticScript != null)
            HomeController.staticScript.PlayGame();
        else
            SceneManager.LoadScene("Game");
    }
}
