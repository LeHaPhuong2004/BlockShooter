#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Audits and captures the saved special-level artwork without opening gameplay or player data.</summary>
[InitializeOnLoad]
public static class SpecialLevelPopupTextSetup
{
    private const string ScenePath = "Assets/Scenes/Home.unity";
    private const string Request = "Temp/SpecialLevelPopupTextSetup.request";
    private const string Result = "Temp/SpecialLevelPopupTextSetup.result";
    private const string Revision = "special-popup-text-v4";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static bool running;

    static SpecialLevelPopupTextSetup() => EditorApplication.update += Poll;

    private static void Poll()
    {
        if (running || !File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        running = true;
        try
        {
            string command = File.ReadAllText(Request).Trim();
            if (command == "apply-" + Revision) Apply();
            else if (command == "capture-" + Revision) Capture();
            else if (command == "check-card-locks") CheckCardLocks();
            else Audit();
        }
        catch (Exception exception) { File.WriteAllText(Result, "VERSION=" + Revision + "\nFAIL\n" + exception); Debug.LogException(exception); }
        finally { File.Delete(Request); running = false; }
    }

    [MenuItem("Tools/Diagnostics/Check Home Level Card Locks")]
    public static void CheckCardLocks()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Check card locks outside Play Mode.");
        Scene scene = EditorSceneManager.OpenPreviewScene(ScenePath);
        LevelManager previousLevel = LevelManager.Instance;
        int savedLevel = PlayerPrefs.GetInt("CurrentLevel", 1);
        var report = new StringBuilder("VERSION=" + Revision + "\nCHECK=card-locks-v1\n");
        try
        {
            Transform[] nodes = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            HomeExperienceUI home = nodes.Select(node => node.GetComponent<HomeExperienceUI>()).Single(item => item != null);
            LevelManager level = nodes.Select(node => node.GetComponent<LevelManager>()).Single(item => item != null);
            LevelManager.Instance = level;
            typeof(HomeExperienceUI).GetField("tabPages", Private).SetValue(home, new[] { nodes.Single(node => node.name == "Home Only Content").gameObject });
            foreach (string name in new[] { "playLevelLabel", "playDifficultyLabel" })
                typeof(HomeExperienceUI).GetField(name, Private).SetValue(home, nodes.Single(node => node.name == (name == "playLevelLabel" ? "Play Level Label" : "Play Difficulty Label")).GetComponent<Text>());
            int[] thresholds = { 25, 30, 50 };
            Transform[] cards = thresholds.Select(threshold => nodes.Single(node => node.name == "LOCKED_" + threshold)).ToArray();
            foreach (Transform card in cards)
                report.AppendLine(card.name + " children: " + string.Join(", ", card.Cast<Transform>().Select(child => child.name + " [" +
                    (child.GetComponent<Image>()?.sprite == null ? "no sprite" : child.GetComponent<Image>().sprite.name) + "]")));
            File.WriteAllText(Result, report.ToString());
            Transform[] locks = cards.Select(card => card.Find("Lock (1)") ?? card.Find("Lock")).ToArray();
            Require(locks.All(icon => icon != null), "An expected authored direct child Lock or Lock (1) is missing. Actual hierarchy:\n" + report);
            var rectangles = cards.SelectMany(card => card.GetComponentsInChildren<RectTransform>(true)).ToDictionary(rect => rect, rect => new Layout(rect));
            var graphics = cards.SelectMany(card => card.GetComponentsInChildren<Component>(true)).Where(component => component is Graphic || component is Button)
                .ToDictionary(component => component, component => EditorJsonUtility.ToJson(component));
            bool[] active = cards.Select(card => card.gameObject.activeSelf).ToArray();
            Invoke(home, "BindSpecialLevelCardLocks");
            foreach (int current in new[] { 1, 24, 25, 29, 30, 49, 50, 51, 24 })
            {
                typeof(LevelManager).GetField("currentLevel", Private).SetValue(level, current);
                for (int pass = 0; pass < 2; pass++)
                {
                    if (pass == 1) for (int index = 0; index < locks.Length; index++) locks[index].gameObject.SetActive(current >= thresholds[index]);
                    Invoke(home, "RefreshPlayButtonLevel");
                    for (int index = 0; index < locks.Length; index++) Require(locks[index].gameObject.activeSelf == (current < thresholds[index]), "Incorrect lock visibility at level" + current + " on " + cards[index].name);
                }
                report.AppendLine("PASS current=" + current + " and unchanged HUD cache: locks=" + string.Join(",", locks.Select(icon => icon.gameObject.activeSelf)));
            }
            foreach (var original in rectangles) Require(original.Value.Matches(original.Key), "Card/lock rectangle changed.");
            foreach (var original in graphics) Require(EditorJsonUtility.ToJson(original.Key) == original.Value, "Card artwork, captions, input or callback changed.");
            Require(cards.Select((card, index) => card.gameObject.activeSelf == active[index]).All(value => value) && PlayerPrefs.GetInt("CurrentLevel", 1) == savedLevel,
                "A card root or saved progression changed.");
            report.AppendLine("PASS only the three existing lock activeSelf states change; card roots, interactability, art, captions, rectangles and callbacks preserved. Disposable preview only; no scene save or player-state actions.");
            File.WriteAllText(Result, report.ToString());
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); LevelManager.Instance = previousLevel; }
    }

    [MenuItem("Tools/Diagnostics/Audit Authored Special Popup Text")]
    public static void Audit()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Audit the saved Home outside Play Mode.");
        Scene scene = EditorSceneManager.OpenPreviewScene(ScenePath);
        try
        {
            var report = new StringBuilder("VERSION=" + Revision + "\nSAVED Home preview; no binding, localization, player state or scene saves.\n");
            Transform layer = Layer(scene);
            foreach (Transform node in layer.GetComponentsInChildren<Transform>(true))
            {
                var line = new StringBuilder(Path(node, layer) + " active=" + node.gameObject.activeSelf);
                if (node is RectTransform rect)
                    line.Append(" anchors=" + rect.anchorMin + "/" + rect.anchorMax + " position=" + rect.anchoredPosition + " size=" + rect.sizeDelta + " pivot=" + rect.pivot + " scale=" + rect.localScale + " actual=" + rect.rect.size);
                Text legacy = node.GetComponent<Text>();
                if (legacy != null) line.Append(" TEXT=" + Quote(legacy.text) + " font=" + (legacy.font == null ? "none" : legacy.font.name) + " fontSize=" + legacy.fontSize + " enabled=" + legacy.enabled);
                TMP_Text modern = node.GetComponent<TMP_Text>();
                if (modern != null) line.Append(" TMP=" + Quote(modern.text) + " font=" + (modern.font == null ? "none" : modern.font.name) + " fontSize=" + modern.fontSize + " enabled=" + modern.enabled);
                Image image = node.GetComponent<Image>();
                if (image != null) line.Append(" IMAGE=" + (image.sprite == null ? "none" : image.sprite.name) + " asset=" + (image.sprite == null ? "none" : AssetDatabase.GetAssetPath(image.sprite)) + " enabled=" + image.enabled);
                Button button = node.GetComponent<Button>();
                if (button != null)
                    for (int index = 0; index < button.onClick.GetPersistentEventCount(); index++) line.Append(" callback=" + button.onClick.GetPersistentMethodName(index));
                report.AppendLine(line.ToString());
            }
            File.WriteAllText(Result, report.ToString());
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [MenuItem("Tools/Home UI/Fit Authored Locked Level Details")]
    public static void Apply()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Fit authored captions outside Play Mode.");
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        var report = new StringBuilder("VERSION=" + Revision + "\n");
        try
        {
            Transform layer = Layer(scene);
            var mutableGraphics = new HashSet<Graphic>();
            var mutableRects = new HashSet<RectTransform>();
            foreach (int level in new[] { 25, 30, 50 })
            {
                Transform frame = layer.Find("Locked Level Popup " + level + "/Frame Area/Frame");
                foreach (string name in new[] { "OpenAtLevel", "TargetLevel", "CurrentLevel", "NumberCurrentLV", "Progress Track/Title (5)", "Title" })
                {
                    Text label = frame.Find(name)?.GetComponent<Text>();
                    Require(label != null, "An existing locked-level caption is missing: " + level + "/" + name);
                    mutableGraphics.Add(label);
                    if (name != "Title") mutableRects.Add(label.rectTransform);
                }
                Button acknowledge = frame.GetComponentsInChildren<Button>(true).Single(button => button.name.StartsWith("SPECIAL_LOCK_ACK_", StringComparison.Ordinal));
                mutableGraphics.Add(acknowledge.GetComponentsInChildren<Text>(true).Single());
                foreach (string name in new[] { "Progress Track", "Progress Track/Progress Fill" })
                {
                    Image image = frame.Find(name)?.GetComponent<Image>();
                    Require(image != null, "An existing locked-level progress image is missing.");
                    mutableGraphics.Add(image); mutableRects.Add(image.rectTransform);
                }
            }
            var originals = layer.GetComponentsInChildren<Transform>(true);
            var rectangles = layer.GetComponentsInChildren<RectTransform>(true).Where(rect => !mutableRects.Contains(rect))
                .ToDictionary(rect => rect, rect => new Layout(rect));
            var components = layer.GetComponentsInChildren<Component>(true)
                .Where(component => component != null && (component is Graphic || component is Button) && !mutableGraphics.Contains(component as Graphic))
                .ToDictionary(component => component, component => EditorJsonUtility.ToJson(component));
            foreach (Graphic graphic in mutableGraphics) Undo.RecordObject(graphic, "Fit authored locked-level details");
            foreach (RectTransform rect in mutableRects) Undo.RecordObject(rect, "Fit authored locked-level details");
            foreach (int level in new[] { 25, 30, 50 })
            {
                Transform frame = layer.Find("Locked Level Popup " + level + "/Frame Area/Frame");
                FitLockedLabel(frame.Find("OpenAtLevel").GetComponent<Text>(), new Vector2(.53f, .63f), new Vector2(.825f, .755f), 36, 28);
                FitLockedLabel(frame.Find("TargetLevel").GetComponent<Text>(), new Vector2(.825f, .63f), new Vector2(.945f, .755f), 44, 36);
                FitLockedLabel(frame.Find("CurrentLevel").GetComponent<Text>(), new Vector2(.53f, .37f), new Vector2(.825f, .485f), 36, 28);
                FitLockedLabel(frame.Find("NumberCurrentLV").GetComponent<Text>(), new Vector2(.825f, .37f), new Vector2(.945f, .485f), 44, 36);
                frame.Find("TargetLevel").GetComponent<Text>().text = level.ToString();
                frame.Find("NumberCurrentLV").GetComponent<Text>().text = "1";
                Image track = frame.Find("Progress Track").GetComponent<Image>();
                SetRect(track.rectTransform, new Vector2(.535f, .495f), new Vector2(.935f, .595f));
                track.preserveAspect = false;
                Image fill = frame.Find("Progress Track/Progress Fill").GetComponent<Image>();
                SetRect(fill.rectTransform, new Vector2(.025f, .13f), new Vector2(.975f, .87f));
                fill.preserveAspect = false; fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal;
                fill.fillOrigin = (int)Image.OriginHorizontal.Left; fill.fillAmount = 1f / level;
                Text ratio = frame.Find("Progress Track/Title (5)").GetComponent<Text>();
                FitLockedLabel(ratio, Vector2.zero, Vector2.one, 28, 26);
                ratio.rectTransform.offsetMin = new Vector2(4, 0); ratio.rectTransform.offsetMax = new Vector2(-4, 0); ratio.text = "1/" + level;
                Text title = frame.Find("Title").GetComponent<Text>();
                SetTypography(title, 42, 36);
                Button acknowledge = frame.GetComponentsInChildren<Button>(true).Single(button => button.name.StartsWith("SPECIAL_LOCK_ACK_", StringComparison.Ordinal));
                SetTypography(acknowledge.GetComponentsInChildren<Text>(true).Single(), 40, 32);
                report.AppendLine("FITTED Locked Level Popup " + level + " existing caption rows, numeric holders and constant-height progress.");
            }
            foreach (Graphic graphic in mutableGraphics) EditorUtility.SetDirty(graphic);
            foreach (RectTransform rect in mutableRects) EditorUtility.SetDirty(rect);
            Require(originals.All(node => node != null) && originals.Length == layer.GetComponentsInChildren<Transform>(true).Length,
                "The caption-only migration added or removed an authored object.");
            foreach (var original in rectangles)
                Require(original.Key != null && original.Value.Matches(original.Key), "An authored rectangle changed: " + (original.Key == null ? "deleted" : original.Key.name));
            foreach (var original in components)
                Require(original.Key != null && EditorJsonUtility.ToJson(original.Key) == original.Value, "An authored caption, art or callback changed: " + (original.Key == null ? "deleted" : original.Key.name));
            EditorSceneManager.MarkSceneDirty(scene);
            Require(EditorSceneManager.SaveScene(scene), "Could not save the targeted Home caption layout.");
            report.AppendLine("PASS Home saved; existing locked-level caption rows/fonts, initial numeric values and progress layouts updated. All confirmation layouts, other objects/rectangles/art and persistent callbacks unchanged.");
        }
        finally { if (openedHere) EditorSceneManager.CloseScene(scene, true); }
        VerifySaved(report);
        File.WriteAllText(Result, report.ToString());
    }

    [MenuItem("Tools/Diagnostics/Capture Authored Special Popup Text")]
    public static void Capture()
    {
        var report = new StringBuilder("VERSION=" + Revision + "\n");
        VerifySaved(report);
        File.WriteAllText(Result, report.ToString());
    }

    private static void VerifySaved(StringBuilder report)
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Capture the saved Home outside Play Mode.");
        Scene scene = EditorSceneManager.OpenPreviewScene(ScenePath);
        RenderTexture previous = RenderTexture.active;
        AudioManager oldAudio = AudioManager.Instance;
        GameMaster oldMaster = GameMaster.staticScript;
        CurrencyManager oldCurrency = CurrencyManager.Instance;
        LevelManager oldLevel = LevelManager.Instance;
        int oldLanguage = Localize.Language;
        float oldTimeScale = Time.timeScale;
        int oldMoney = PlayerPrefs.GetInt("PlayerMoney", 0), oldSavedLevel = PlayerPrefs.GetInt("CurrentLevel", 1);
        FieldInfo heartData = typeof(HeartManager).GetField("data", BindingFlags.Static | BindingFlags.NonPublic);
        FieldInfo heartRound = typeof(HeartManager).GetField("roundInProgress", BindingFlags.Static | BindingFlags.NonPublic);
        object oldHearts = heartData.GetValue(null), oldRound = heartRound.GetValue(null);
        try
        {
            AudioManager.Instance = null; GameMaster.staticScript = null; CurrencyManager.Instance = null;
            heartData.SetValue(null, new HeartState()); heartRound.SetValue(null, false);
            Localize.SetLanguage(Localize.Vietnamese, false);
            HomeExperienceUI home = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<HomeExperienceUI>(true)).Single();
            LevelManager levelManager = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<LevelManager>(true)).Single();
            LevelManager.Instance = levelManager;
            Invoke(home, "LoadHomeResources");
            Require((bool)Invoke(home, "BindSceneHome"), "Home could not bind after removing obsolete writer holders.");
            Transform layer = Layer(scene);
            Localize.Apply(layer);
            foreach (int level in new[] { 25, 30, 50 })
            {
                Transform confirm = layer.Find("Special Level Confirm " + level);
                foreach (Button button in confirm.GetComponentsInChildren<Button>(true))
                {
                    Action action = Invoke(home, "SpecialLevelPopupButtonAction", button) as Action;
                    Require(action != null, "An authored special-level action did not resolve: " + button.name);
                    if (!button.name.StartsWith("SPECIAL_CONFIRM_START_", StringComparison.Ordinal)) continue;
                    FieldInfo capturedLevel = action.Target?.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .SingleOrDefault(field => field.FieldType == typeof(int) && field.Name == "level");
                    Require(capturedLevel != null && (int)capturedLevel.GetValue(action.Target) == level,
                        "Copied START button resolved the wrong containing level: " + level);
                }
            }
            report.AppendLine("PASS copied START button names route to their containing 25/30/50 panels; delegates inspected without entering gameplay.");
            foreach (int current in new[] { 1, 5, 7, 24, 29, 49, 25, 30, 50 })
            {
                typeof(LevelManager).GetField("currentLevel", Private).SetValue(levelManager, current);
                foreach (int threshold in new[] { 25, 30, 50 })
                {
                    Invoke(home, "OpenSpecialLevelPopup", threshold);
                    AssertLockedData(layer, current);
                    Require(layer.Find((current < threshold ? "Locked Level Popup " : "Special Level Confirm ") + threshold).gameObject.activeSelf,
                        "The threshold boundary selected the wrong popup.");
                }
            }
            typeof(LevelManager).GetField("currentLevel", Private).SetValue(levelManager, 7);
            foreach (int language in new[] { Localize.English, Localize.Vietnamese })
            {
                Localize.SetLanguage(language, false); Invoke(home, "RefreshLocalizedHome"); AssertLockedData(layer, 7);
            }
            report.AppendLine("PASS fresh numeric values/fill at levels1/5/7/24/29/49 and thresholds25/30/50, including language refresh; fill rectangles remain constant.");
            Camera camera = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true)).First(item => item.CompareTag("MainCamera"));
            camera.scene = scene;
            Directory.CreateDirectory("Temp/SpecialLevelPopupValidation");
            foreach (Vector2 size in new[] { new Vector2(1170, 2532), new Vector2(1536, 2048) })
            {
                bool phone = size.x == 1170;
                var target = new RenderTexture((int)size.x, (int)size.y, 24, RenderTextureFormat.ARGB32);
                try
                {
                    target.Create(); camera.targetTexture = target;
                    foreach (Canvas canvas in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Canvas>(true)).Where(canvas => canvas.isRootCanvas))
                        PortraitDisplay.ApplyCanvasLayout(canvas, camera, PortraitDisplay.ReferenceResolutionForScene(scene), size);
                    ResponsiveSceneLayout.Apply(scene, camera, size);
                    foreach (int language in new[] { Localize.Vietnamese, Localize.English })
                    {
                        Localize.SetLanguage(language, false); Localize.Apply(layer);
                        int captionCount = layer.GetComponentsInChildren<Component>(true).Count(component => component is Text || component is TMP_Text);
                        var captions = layer.GetComponentsInChildren<Component>(true).Where(component => (component is Text || component is TMP_Text) && !IsLockedNumeric(component))
                            .ToDictionary(component => component, component => Caption(component));
                        foreach (string prefix in new[] { "Locked Level Popup ", "Special Level Confirm " })
                            foreach (int level in new[] { 25, 30, 50 })
                            {
                                bool locked = prefix.StartsWith("Locked", StringComparison.Ordinal);
                                typeof(LevelManager).GetField("currentLevel", Private).SetValue(levelManager, locked ? 5 : level);
                                Invoke(home, "OpenSpecialLevelPopup", level);
                                Transform selected = layer.GetComponentsInChildren<Transform>(true).Single(node => node.name == prefix + level);
                                Require(layer.gameObject.activeSelf && selected.gameObject.activeSelf, "The requested special-level state did not open: " + selected.name);
                                Require(layer.Cast<Transform>().Where(child => child.name.StartsWith("Locked Level Popup ", StringComparison.Ordinal) || child.name.StartsWith("Special Level Confirm ", StringComparison.Ordinal))
                                    .All(child => child.gameObject.activeSelf == (child == selected)), "More than one special-level popup opened.");
                                Require(captionCount == layer.GetComponentsInChildren<Component>(true).Count(component => component is Text || component is TMP_Text), "Opening a popup created a caption.");
                                foreach (var caption in captions) Require(caption.Key != null && Caption(caption.Key) == caption.Value, "Opening a popup overwrote authored text: " + caption.Key.name);
                                AssertLockedData(layer, locked ? 5 : level);
                                Canvas.ForceUpdateCanvases(); Canvas.ForceUpdateCanvases();
                                foreach (TMP_Text label in selected.GetComponentsInChildren<TMP_Text>(true)) label.ForceMeshUpdate(true, true);
                                camera.Render(); Canvas.ForceUpdateCanvases(); camera.Render();
                                if (locked) ValidateLockedText(selected, report);
                                else ValidateConfirmationText(selected, level, report);
                                RenderTexture.active = target;
                                var pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
                                string file = "Temp/SpecialLevelPopupValidation/" + (locked ? "Locked-" : "Confirm-") + level + "-" +
                                    (Localize.IsEnglish ? "EN" : "VI") + "-" + (phone ? "Phone" : "iPad") + ".png";
                                try { pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply(); File.WriteAllBytes(file, pixels.EncodeToPNG()); }
                                finally { UnityEngine.Object.DestroyImmediate(pixels); }
                                report.AppendLine("PNG " + file);
                            }
                    }
                }
                finally { camera.targetTexture = null; RenderTexture.active = previous; target.Release(); UnityEngine.Object.DestroyImmediate(target); }
            }
            Require(Mathf.Approximately(Time.timeScale, oldTimeScale) && PlayerPrefs.GetInt("PlayerMoney", 0) == oldMoney && PlayerPrefs.GetInt("CurrentLevel", 1) == oldSavedLevel,
                "Popup-only verification changed time scale, saved money or saved progression.");
            report.AppendLine("PASS locked/confirm25/30/50 fit their light boxes in VI/EN on phone/iPad; only existing locked numeric holders refresh, with no runtime text creation, gameplay, rewards, preference writes or verification scene saves.");
        }
        finally
        {
            RenderTexture.active = previous; EditorSceneManager.ClosePreviewScene(scene);
            heartData.SetValue(null, oldHearts); heartRound.SetValue(null, oldRound);
            AudioManager.Instance = oldAudio; GameMaster.staticScript = oldMaster; CurrencyManager.Instance = oldCurrency; LevelManager.Instance = oldLevel;
            Localize.SetLanguage(oldLanguage, false);
        }
    }

    private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static void SetTypography(Text label, int maximum, int minimum)
    {
        label.alignment = TextAnchor.MiddleCenter; label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate; label.resizeTextForBestFit = true;
        label.resizeTextMinSize = minimum; label.resizeTextMaxSize = maximum; label.fontSize = maximum;
    }

    private static void FitLockedLabel(Text label, Vector2 min, Vector2 max, int maximum, int minimum)
    {
        SetRect(label.rectTransform, min, max); SetTypography(label, maximum, minimum);
    }

    private static bool IsLockedNumeric(Component label) => label.name == "NumberCurrentLV" ||
        (label.name == "Title (5)" && label.transform.parent.name == "Progress Track");

    private static void AssertLockedData(Transform layer, int current)
    {
        foreach (int threshold in new[] { 25, 30, 50 })
        {
            Transform frame = layer.Find("Locked Level Popup " + threshold + "/Frame Area/Frame");
            Require(frame.Find("NumberCurrentLV").GetComponent<Text>().text == current.ToString() &&
                frame.Find("Progress Track/Title (5)").GetComponent<Text>().text == current + "/" + threshold,
                "The existing locked numeric holders are stale or contain placeholders.");
            Image fill = frame.Find("Progress Track/Progress Fill").GetComponent<Image>();
            Require(fill.type == Image.Type.Filled && fill.fillMethod == Image.FillMethod.Horizontal && fill.fillOrigin == (int)Image.OriginHorizontal.Left &&
                !fill.preserveAspect && Mathf.Abs(fill.fillAmount - Mathf.Clamp01(current / (float)threshold)) < .0001f,
                "Locked fill must preserve its thickness and display the current ratio.");
            Require(Vector2.Distance(fill.rectTransform.anchorMin, new Vector2(.025f, .13f)) < .0001f &&
                Vector2.Distance(fill.rectTransform.anchorMax, new Vector2(.975f, .87f)) < .0001f && fill.rectTransform.offsetMin == Vector2.zero && fill.rectTransform.offsetMax == Vector2.zero,
                "Runtime changed the locked fill rectangle rather than fillAmount.");
        }
    }

    private static void ValidateLockedText(Transform popup, StringBuilder report)
    {
        RectTransform frame = popup.Find("Frame Area/Frame") as RectTransform;
        foreach (string name in new[] { "OpenAtLevel", "TargetLevel", "CurrentLevel", "NumberCurrentLV" })
        {
            Text label = frame.Find(name).GetComponent<Text>();
            ValidateTextGeometry(label, frame, new Vector2(.515f, .325f), new Vector2(.96f, .775f));
        }
        RectTransform track = frame.Find("Progress Track") as RectTransform;
        Require(track.rect.height >= frame.rect.height * .095f && !track.GetComponent<Image>().preserveAspect, "The progress track is too thin.");
        ValidateTextGeometry(track.Find("Title (5)").GetComponent<Text>(), track, Vector2.zero, Vector2.one);
        foreach (Text label in new[] { frame.Find("Title").GetComponent<Text>(), frame.GetComponentsInChildren<Button>(true)
            .Single(button => button.name.StartsWith("SPECIAL_LOCK_ACK_", StringComparison.Ordinal)).GetComponentsInChildren<Text>(true).Single() })
        {
            label.cachedTextGenerator.PopulateWithErrors(label.text, label.GetGenerationSettings(label.rectTransform.rect.size), label.gameObject);
            Require(label.cachedTextGenerator.characterCountVisible >= label.text.Length && label.cachedTextGenerator.fontSizeUsedForBestFit >= label.resizeTextMinSize,
                "Locked title/acknowledge caption is clipped or too small.");
        }
        report.AppendLine("PASS " + (Localize.IsEnglish ? "EN" : "VI") + " " + popup.name + ": readable caption/number vertices inside light box, full-height ratio bar.");
    }

    private static void ValidateTextGeometry(Text label, RectTransform reference, Vector2 min, Vector2 max)
    {
        label.cachedTextGenerator.PopulateWithErrors(label.text, label.GetGenerationSettings(label.rectTransform.rect.size), label.gameObject);
        Require(label.cachedTextGenerator.characterCountVisible >= label.text.Length && label.cachedTextGenerator.fontSizeUsedForBestFit >= label.resizeTextMinSize,
            "An authored locked caption is clipped or too small: " + label.name);
        IList<UIVertex> vertices = label.cachedTextGenerator.verts;
        Require(vertices.Count > 0, "The locked caption has no generated glyphs.");
        Rect safe = Rect.MinMaxRect(reference.rect.xMin + reference.rect.width * min.x, reference.rect.yMin + reference.rect.height * min.y,
            reference.rect.xMin + reference.rect.width * max.x, reference.rect.yMin + reference.rect.height * max.y);
        foreach (UIVertex vertex in vertices)
        {
            Vector3 point = reference.InverseTransformPoint(label.transform.TransformPoint(vertex.position / label.pixelsPerUnit));
            Require(point.x >= safe.xMin - 2 && point.x <= safe.xMax + 2 && point.y >= safe.yMin - 2 && point.y <= safe.yMax + 2,
                "Generated locked caption extends outside its light box: " + label.name + " at " + point);
        }
    }

    private static void ValidateConfirmationText(Transform popup, int level, StringBuilder report)
    {
        RectTransform frame = popup.Find("Frame Area/Frame") as RectTransform;
        foreach (string name in new[] { "Label (1)", "Label (2)" })
        {
            Text label = popup.GetComponentsInChildren<Text>(true).Single(item => item.name == name);
            Require(label.text == (name == "Label (1)" ? Localize.Text("Bạn đã sẵn sàng vào màn đặc biệt cấp", "Ready to enter special level") : level.ToString()),
                "The authored confirmation sentence or level number is incorrect.");
            label.cachedTextGenerator.PopulateWithErrors(label.text, label.GetGenerationSettings(label.rectTransform.rect.size), label.gameObject);
            Require(label.cachedTextGenerator.characterCountVisible >= label.text.Length, "The confirmation caption is truncated: " + name);
            IList<UIVertex> vertices = label.cachedTextGenerator.verts;
            Require(vertices.Count > 4, "The confirmation caption has no generated text geometry.");
            Rect safe = Rect.MinMaxRect(frame.rect.xMin + frame.rect.width * .515f, frame.rect.yMin + frame.rect.height * .25f,
                frame.rect.xMin + frame.rect.width * .95f, frame.rect.yMin + frame.rect.height * .67f);
            for (int index = 0; index < vertices.Count; index++)
            {
                Vector3 point = frame.InverseTransformPoint(label.transform.TransformPoint(vertices[index].position / label.pixelsPerUnit));
                Require(point.x >= safe.xMin - 2 && point.x <= safe.xMax + 2 && point.y >= safe.yMin - 2 && point.y <= safe.yMax + 2,
                    "Generated confirmation text extends outside the right light box: " + name + " at " + point);
            }
        }
        report.AppendLine("PASS " + (Localize.IsEnglish ? "EN" : "VI") + " confirm " + level + ": complete sentence/number vertices remain inside right light box.");
    }

    private static object Invoke(object target, string method, params object[] arguments) => (target.GetType().GetMethod(method, Private) ?? throw new MissingMethodException(method)).Invoke(target, arguments);
    private static string Caption(Component component) => component is Text legacy ? legacy.text : ((TMP_Text)component).text;
    private readonly struct Layout
    {
        private readonly Vector2 min, max, size, pivot;
        private readonly Vector3 position, scale;
        private readonly Quaternion rotation;
        private readonly Transform parent;
        public Layout(RectTransform rect) { min = rect.anchorMin; max = rect.anchorMax; size = rect.sizeDelta; pivot = rect.pivot; position = rect.anchoredPosition3D; scale = rect.localScale; rotation = rect.localRotation; parent = rect.parent; }
        public bool Matches(RectTransform rect) => min == rect.anchorMin && max == rect.anchorMax && size == rect.sizeDelta && pivot == rect.pivot && position == rect.anchoredPosition3D && scale == rect.localScale && rotation == rect.localRotation && parent == rect.parent;
    }

    private static Transform Layer(Scene scene) => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).Single(node => node.name == "Special Level Popup Layer");
    private static string Path(Transform node, Transform root) => node == root ? node.name : Path(node.parent, root) + "/" + node.name;
    private static string Quote(string text) => "\"" + text.Replace("\r", "\\r").Replace("\n", "\\n") + "\"";
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
#endif
