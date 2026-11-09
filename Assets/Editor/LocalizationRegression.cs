#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Checks language changes on disposable authored scenes without playing or awarding rewards.</summary>
[InitializeOnLoad]
public static class LocalizationRegression
{
    private const string Request = "Temp/LocalizationRegression.request";
    private const string Result = "Temp/LocalizationRegression.result";
    private const string InventoryResult = "Temp/LocalizationUntranslated.result";
    private const string GlyphResult = "Temp/LocalizationGlyphs.result";
    private const string Revision = "localization-v3";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
    private static bool running;

    private sealed class Caption
    {
        public Component component;
        public string vi;
    }

    static LocalizationRegression() => EditorApplication.update += Poll;

    private static void Poll()
    {
        if (running || !File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        running = true;
        try { Run(); }
        catch (Exception exception) { File.WriteAllText(Result, "VERSION=" + Revision + "\nFAIL\n" + exception); Debug.LogException(exception); }
        finally { File.Delete(Request); running = false; }
    }

    [MenuItem("Tools/Diagnostics/Check Vietnamese English Localization")]
    public static void Run()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Run the isolated language regression outside Play Mode.");
        bool hadPreference = PlayerPrefs.HasKey(Localize.LanguagePreferenceKey);
        int previousPreference = PlayerPrefs.GetInt(Localize.LanguagePreferenceKey, Localize.Vietnamese);
        int previousLanguage = Localize.Language;
        AudioManager previousAudio = AudioManager.Instance;
        GameMaster previousMaster = GameMaster.staticScript;
        CurrencyManager previousCurrency = CurrencyManager.Instance;
        LevelManager previousLevel = LevelManager.Instance;
        FieldInfo heartData = typeof(HeartManager).GetField("data", StaticPrivate);
        FieldInfo heartRound = typeof(HeartManager).GetField("roundInProgress", StaticPrivate);
        object previousHeartData = heartData.GetValue(null), previousRound = heartRound.GetValue(null);
        float previousTimeScale = Time.timeScale;
        int previousMoney = PlayerPrefs.GetInt("PlayerMoney", 0);
        var previews = new List<Scene>();
        Action refreshHome = null;
        var report = new StringBuilder("VERSION=" + Revision + "\n");
        var glyphs = new HashSet<string>();
        try
        {
            AudioManager.Instance = null;
            GameMaster.staticScript = null;
            CurrencyManager.Instance = null;
            heartData.SetValue(null, new HeartState());
            heartRound.SetValue(null, false);
            Localize.SetLanguage(Localize.Vietnamese, false);
            Scene homeScene = Open("Home", previews);
            HomeExperienceUI home = Nodes(homeScene).Select(node => node.GetComponent<HomeExperienceUI>()).Single(item => item != null);
            Caption[] dailyLoginCaptions = Enumerable.Range(1, 7).Select(day => new Caption
            {
                component = Node(home.transform, "Day " + day + " Label").GetComponent<TMP_Text>(),
                vi = Node(home.transform, "Day " + day + " Label").GetComponent<TMP_Text>().text
            }).ToArray();
            LevelManager.Instance = Nodes(homeScene).Select(node => node.GetComponent<LevelManager>()).FirstOrDefault(item => item != null);
            Invoke(home, "LoadHomeResources");
            Require((bool)Invoke(home, "BindSceneHome"), "Authored Home could not bind for localization checks.");
            Canvas homeCanvas = home.GetComponentsInChildren<Canvas>(true).Single(canvas => canvas.name == "Home Canvas");
            refreshHome = () => Invoke(home, "RefreshLocalizedHome");
            Localize.LanguageChanged += refreshHome;
            refreshHome();
            AssertAuthoredDailyCaptions(dailyLoginCaptions);
            Invoke(home, "RefreshHearts");
            Caption[] homeSamples = CaptureHomeSamples(home, homeCanvas.transform);
            Scene game = Open("Game", previews), start = Open("Start", previews);
            Apply(game); Apply(start);
            Caption[] gameSamples = CaptureAll(game), startSamples = CaptureAll(start);
            InventoryAllScenes(glyphs, report);
            WriteGlyphs(glyphs);

            // Drive the same private action used by both language dropdown options.
            foreach (int language in new[] { Localize.English, Localize.Vietnamese, Localize.English, Localize.Vietnamese })
            {
                Invoke(home, "SelectSettingsLanguage", language);
                Require(Localize.Language == language && PlayerPrefs.GetInt(Localize.LanguagePreferenceKey, -1) == language,
                    "The actual Settings selection did not update the shared language and saved preference.");
                Apply(game); Apply(start);
                AssertSamples(homeSamples, "Home"); AssertSamples(gameSamples, "Game"); AssertSamples(startSamples, "Start");
                foreach (string writer in new[] { "RefreshDailyState", "RefreshPlayButtonLevel", "RefreshAchievements", "UpdateCharacterPageState", "RefreshShopPurchaseLabels", "RefreshBoosterShopPrices", "RefreshHearts" })
                    Invoke(home, writer);
                AssertSamples(homeSamples, "Home after dynamic refresh");
                AssertAuthoredDailyCaptions(dailyLoginCaptions);
                Require(Text(Get<Text>(home, "settingsLanguageValueLabel")) == (Localize.IsEnglish ? "English" : "Tiếng Việt"), "The language dropdown value is stale.");
                Require(HeartManager.TimerText == (Localize.IsEnglish ? "FULL" : "ĐẦY"), "Full hearts are not localized at the display getter.");
                CheckGlyphs(homeScene, glyphs); CheckGlyphs(game, glyphs); CheckGlyphs(start, glyphs);
            }
            report.AppendLine("PASS actual Settings selection VI/EN roundtrip: footer, settings, mission title/description, achievement name, character action, Play level/difficulty and heart timer.");
            report.AppendLine("PASS periodic Home refreshes retain the selected language; inactive Game/Start captions also roundtrip.");
            report.AppendLine("PASS all seven authored daily login captions remain unchanged through binding, VI/EN switches and periodic refreshes.");
            CheckGameWriters(game, heartData, heartRound, previousTimeScale, report);
            WriteGlyphs(glyphs);
            if (glyphs.Count == 0) CaptureEnglishPreviews(home, homeScene, homeCanvas, report, dailyLoginCaptions);
            else report.AppendLine("PNG previews skipped until missing font glyphs are repaired; see " + GlyphResult);
            Require(Mathf.Approximately(Time.timeScale, previousTimeScale), "Localization changed gameplay time scale.");
            Require(PlayerPrefs.GetInt("PlayerMoney", 0) == previousMoney, "A localization-only action changed the saved wallet.");
            Require(glyphs.Count == 0, "Localized TMP captions have missing letter glyphs; see " + GlyphResult);
            report.AppendLine("PASS preview-only checks: no gameplay, rewards, purchases or scene saves. Language preference and temporary global references restored.");
            File.WriteAllText(Result, report.ToString());
        }
        finally
        {
            if (refreshHome != null) Localize.LanguageChanged -= refreshHome;
            for (int index = previews.Count - 1; index >= 0; index--)
                if (previews[index].IsValid()) EditorSceneManager.ClosePreviewScene(previews[index]);
            heartData.SetValue(null, previousHeartData); heartRound.SetValue(null, previousRound);
            AudioManager.Instance = previousAudio; GameMaster.staticScript = previousMaster;
            CurrencyManager.Instance = previousCurrency; LevelManager.Instance = previousLevel;
            Localize.SetLanguage(previousLanguage, false);
            if (hadPreference) PlayerPrefs.SetInt(Localize.LanguagePreferenceKey, previousPreference);
            else PlayerPrefs.DeleteKey(Localize.LanguagePreferenceKey);
            PlayerPrefs.Save();
            WriteGlyphs(glyphs);
        }
    }

    private static Caption[] CaptureHomeSamples(HomeExperienceUI home, Transform root)
    {
        var captions = new List<Component>();
        captions.AddRange(Get<Text[]>(home, "bottomTabLabels"));
        captions.Add(Get<Text>(home, "settingsTitleLabel"));
        captions.AddRange(Get<Text[]>(home, "settingsRowLabels"));
        foreach (string field in new[] { "characterActionLabel", "characterPreviewName", "playLevelLabel", "playDifficultyLabel", "heartTimerLabel", "settingsHelpLabel", "settingsCodePlaceholderLabel" })
            captions.Add(Get<Text>(home, field));
        foreach (string name in new[] { "Break 50 Blocks Title", "Break 50 Blocks Description" })
            captions.Add(Node(root, name).GetComponent<TMP_Text>());
        Transform achievement = Node(root, "Achievement Card - blocks_100");
        captions.Add(Node(achievement, "Achievement Name").GetComponent<Text>());
        Require(captions.All(caption => caption != null), "A representative Home localization label is missing.");
        return captions.Distinct().Select(component => new Caption { component = component, vi = Text(component) }).ToArray();
    }

    private static Caption[] CaptureAll(Scene scene) => Labels(scene).Select(component => new Caption { component = component, vi = Text(component) }).ToArray();

    private static void AssertSamples(IEnumerable<Caption> samples, string scope)
    {
        foreach (Caption caption in samples)
        {
            string expected = Localize.IsEnglish ? Localize.Translate(caption.vi) : caption.vi;
            Require(Text(caption.component) == expected, scope + " stale caption " + Path(caption.component.transform) + ": " + Text(caption.component) + " expected " + expected);
            if (Localize.IsEnglish && HasVietnamese(caption.vi) && caption.vi != "Tiếng Việt")
                Require(expected != caption.vi, "Missing English translation for sample: " + caption.vi);
        }
    }

    private static void CheckGameWriters(Scene game, FieldInfo heartData, FieldInfo heartRound, float timeScale, StringBuilder report)
    {
        GamePauseUI pause = Nodes(game).Select(node => node.GetComponent<GamePauseUI>()).Single(item => item != null);
        object oldPanel = Field(pause, "activePanel").GetValue(pause), oldPending = Field(pause, "pendingScene").GetValue(pause);
        try
        {
            foreach (int language in new[] { Localize.Vietnamese, Localize.English, Localize.Vietnamese })
            {
                Localize.SetLanguage(language, false);
                foreach (string panel in new[] { "menuPanel", "victoryPanel", "lossPanel" })
                {
                    Field(pause, "activePanel").SetValue(pause, Field(pause, panel).GetValue(pause));
                    Invoke(pause, "RefreshLocalizedLabels");
                }
                int level = LevelManager.Instance == null ? 1 : Mathf.Max(1, LevelManager.Instance.CurrentLevel);
                Require(Text(Get<TMP_Text>(pause, "settingsLevelLabel")) == Localize.Format("CẤP ĐỘ {0}", "LEVEL {0}", level), "Game settings level writer ignored the selected language.");
                Require(Text(Get<TMP_Text>(pause, "victoryLevelLabel")) == Localize.Format("HOÀN THÀNH CẤP ĐỘ {0}", "LEVEL {0} COMPLETE", Mathf.Max(1, level - 1)), "Victory completion caption ignored the selected language.");
                var hearts = new HeartState { hearts = 1 };
                heartData.SetValue(null, hearts); heartRound.SetValue(null, true);
                Field(pause, "activePanel").SetValue(pause, Field(pause, "confirmationPanel").GetValue(pause));
                Field(pause, "pendingScene").SetValue(pause, "Game");
                Invoke(pause, "RefreshLocalizedLabels");
                Require(Text(Get<TMP_Text>(pause, "confirmationMessage")).Contains(Localize.Text("Bạn sẽ mất 1 tim.", "You will lose 1 heart.")), "Active leave confirmation was not translated.");
                Require(hearts.hearts == 1 && HeartManager.RoundInProgress && Mathf.Approximately(Time.timeScale, timeScale), "Refreshing confirmation performed a heart transaction or paused gameplay.");
                foreach (GridManager grid in Nodes(game).Select(node => node.GetComponent<GridManager>()).Where(item => item != null))
                {
                    Invoke(grid, "RefreshLocalizedLevelLabel");
                    if (grid.levelText != null && LevelManager.Instance != null)
                        Require(grid.levelText.text == Localize.Format("CẤP ĐỘ {0}", "LEVEL {0}", level), "Game HUD level is stale.");
                }
                foreach (NavigationManager nav in Nodes(game).Select(node => node.GetComponent<NavigationManager>()).Where(item => item != null))
                    Invoke(nav, "RefreshLocalizedLevelLabel");
            }
            report.AppendLine("PASS Game dynamic HUD, active result labels and active leave confirmation refresh without changing pause/time or hearts.");
        }
        finally { Field(pause, "activePanel").SetValue(pause, oldPanel); Field(pause, "pendingScene").SetValue(pause, oldPending); heartData.SetValue(null, new HeartState()); heartRound.SetValue(null, false); }
    }

    private static void InventoryAllScenes(HashSet<string> glyphs, StringBuilder report)
    {
        var inventory = new StringBuilder("VERSION=" + Revision + "\n");
        int missing = 0, count = 0;
        foreach (string path in Directory.GetFiles("Assets/Scenes", "*.unity").Select(path => path.Replace('\\', '/')).OrderBy(path => path))
        {
            Scene scene = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                Localize.SetLanguage(Localize.Vietnamese, false); Apply(scene); CheckGlyphs(scene, glyphs);
                Localize.SetLanguage(Localize.English, false); Apply(scene); CheckGlyphs(scene, glyphs);
                foreach (Component label in Labels(scene))
                {
                    count++;
                    string text = Text(label);
                    if (!HasVietnamese(text) || text.Trim() == "Tiếng Việt" || Archived(label.transform)) continue;
                    inventory.AppendLine("UNTRANSLATED " + scene.name + "/" + Path(label.transform) + " active=" + label.gameObject.activeInHierarchy + " text=" + text.Replace("\n", "\\n"));
                    missing++;
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        inventory.AppendLine("CHECKED " + count + " saved labels across six scenes; remaining Vietnamese=" + missing + " (native language choice and archived templates excluded).");
        File.WriteAllText(InventoryResult, inventory.ToString());
        report.AppendLine("INVENTORY " + count + " labels across six saved scenes; remaining Vietnamese=" + missing + "; details " + InventoryResult);
    }

    private static void CheckGlyphs(Scene scene, HashSet<string> missing)
    {
        foreach (TMP_Text label in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<TMP_Text>(true)))
        {
            if (label.font == null || Archived(label.transform)) continue;
            string text = Regex.Replace(label.text ?? string.Empty, "<[^>]*>", "");
            foreach (char character in text.Distinct())
                if (char.IsLetter(character) && !label.font.HasCharacter(character, searchFallbacks: true, tryAddCharacter: false))
                    missing.Add((Localize.IsEnglish ? "EN " : "VI ") + scene.name + "/" + Path(label.transform) + " font=" + label.font.name + " missing=" + character + " U+" + ((int)character).ToString("X4"));
        }
    }

    private static void WriteGlyphs(HashSet<string> glyphs) => File.WriteAllText(GlyphResult,
        "VERSION=" + Revision + "\n" + (glyphs.Count == 0 ? "PASS localized TMP letter glyphs exist in their font or fallback fonts.\n" : string.Join("\n", glyphs.OrderBy(value => value)) + "\nMISSING " + glyphs.Count));

    private static void AssertAuthoredDailyCaptions(IEnumerable<Caption> captions, bool checkFit = false)
    {
        foreach (Caption caption in captions)
        {
            var label = caption.component as TMP_Text;
            Require(label != null && label.text == caption.vi, "Runtime refresh replaced an authored daily login caption: " + caption.component?.name);
            if (!checkFit) continue;
            label.ForceMeshUpdate(true, true);
            Require(!label.isTextTruncated && !label.isTextOverflowing,
                (Localize.IsEnglish ? "English" : "Vietnamese") + " authored daily login caption is clipped: " + label.name);
        }
    }

    private static void CaptureEnglishPreviews(HomeExperienceUI home, Scene scene, Canvas canvas, StringBuilder report, Caption[] dailyLoginCaptions)
    {
        Camera camera = Nodes(scene).Select(node => node.GetComponent<Camera>()).First(item => item != null && item.CompareTag("MainCamera"));
        camera.scene = scene;
        RenderTexture old = RenderTexture.active;
        foreach (var sample in new[] { (size: new Vector2(1536, 2048), language: Localize.English, page: 0, settings: false, file: "Home-EN-iPad.png"),
            (size: new Vector2(1170, 2532), language: Localize.English, page: 0, settings: true, file: "Settings-EN-Phone.png"),
            (size: new Vector2(1170, 2532), language: Localize.English, page: 2, settings: false, file: "Missions-EN-Phone.png"),
            (size: new Vector2(1170, 2532), language: Localize.Vietnamese, page: 2, settings: false, file: "Missions-VI-Phone.png") })
        {
            var target = new RenderTexture((int)sample.size.x, (int)sample.size.y, 24, RenderTextureFormat.ARGB32);
            try
            {
                Localize.SetLanguage(sample.language, false);
                target.Create(); camera.targetTexture = target;
                PortraitDisplay.ApplyCanvasLayout(canvas, camera, PortraitDisplay.ReferenceResolutionForScene(scene), sample.size);
                ResponsiveSceneLayout.Apply(scene, camera, sample.size);
                Invoke(home, "CloseSettingsPopup"); Invoke(home, "SelectTab", sample.page);
                if (sample.settings) Invoke(home, "ShowSettings");
                Localize.Apply(canvas.transform); Canvas.ForceUpdateCanvases();
                foreach (TMP_Text label in canvas.GetComponentsInChildren<TMP_Text>(false)) label.ForceMeshUpdate(true, true);
                camera.Render(); Canvas.ForceUpdateCanvases(); camera.Render();
                if (sample.page == 2)
                {
                    AssertAuthoredDailyCaptions(dailyLoginCaptions, true);
                    report.AppendLine("PASS " + (Localize.IsEnglish ? "EN" : "VI") + " phone: all seven authored daily login captions are preserved and fit without truncation.");
                }
                RenderTexture.active = target;
                var pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
                try
                {
                    pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
                    Directory.CreateDirectory("Temp/LocalizationValidation");
                    File.WriteAllBytes("Temp/LocalizationValidation/" + sample.file, pixels.EncodeToPNG());
                }
                finally { UnityEngine.Object.DestroyImmediate(pixels); }
                report.AppendLine("PNG Temp/LocalizationValidation/" + sample.file);
            }
            finally { camera.targetTexture = null; RenderTexture.active = old; target.Release(); UnityEngine.Object.DestroyImmediate(target); }
        }
    }

    private static bool Archived(Transform node)
    {
        for (Transform ancestor = node; ancestor != null; ancestor = ancestor.parent)
        {
            if (ancestor.name.IndexOf("Archived", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (ancestor.name == "UI" && ancestor.GetComponent<Canvas>() != null && !ancestor.gameObject.activeSelf) return true;
        }
        return false;
    }

    private static bool HasVietnamese(string value) => !string.IsNullOrEmpty(value) && value.Any(character =>
        (character >= '\u1ea0' && character <= '\u1eff') || "ÀÁÂÃÈÉÊÌÍÒÓÔÕÙÚÝàáâãèéêìíòóôõùúýĂăĐđĨĩŨũƠơƯư".IndexOf(character) >= 0);

    private static Scene Open(string name, List<Scene> scenes) { Scene scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/" + name + ".unity"); scenes.Add(scene); return scene; }
    private static Transform[] Nodes(Scene scene) => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
    private static Component[] Labels(Scene scene) => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Text>(true).Cast<Component>().Concat(root.GetComponentsInChildren<TMP_Text>(true))).ToArray();
    private static void Apply(Scene scene) { foreach (GameObject root in scene.GetRootGameObjects()) Localize.Apply(root.transform); }
    private static Transform Node(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).First(node => node.name == name);
    private static string Text(Component component) => component is Text legacy ? legacy.text : component is TMP_Text modern ? modern.text : string.Empty;
    private static FieldInfo Field(object target, string name) => target.GetType().GetField(name, Private) ?? throw new MissingFieldException(target.GetType().Name, name);
    private static T Get<T>(object target, string name) => (T)Field(target, name).GetValue(target);
    private static object Invoke(object target, string name, params object[] values) => (target.GetType().GetMethod(name, Private) ?? throw new MissingMethodException(target.GetType().Name, name)).Invoke(target, values);
    private static string Path(Transform node) => node.parent == null ? node.name : Path(node.parent) + "/" + node.name;
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
#endif
