#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Reads the current authored character outline, including unsaved Home edits.</summary>
[InitializeOnLoad]
public static class CharacterSelectionRegression
{
    private const string ScenePath = "Assets/Scenes/Home.unity";
    private const string Request = "Temp/CharacterSelectionRegression.request";
    private const string Result = "Temp/CharacterSelectionRegression.result";
    private const string Revision = "character-selection-v2";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static bool running;

    static CharacterSelectionRegression() => EditorApplication.update += Poll;

    private static void Poll()
    {
        if (running || !File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        running = true;
        try { if (File.ReadAllText(Request).Trim() == "run-" + Revision) Run(); else Audit(); }
        catch (Exception exception) { File.WriteAllText(Result, "VERSION=" + Revision + "\nFAIL\n" + exception); Debug.LogException(exception); }
        finally { File.Delete(Request); running = false; }
    }

    [MenuItem("Tools/Diagnostics/Check Character Selection Animation")]
    public static void Run()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Check the authored character UI outside Play Mode.");
        AudioManager oldAudio = AudioManager.Instance;
        GameMaster oldMaster = GameMaster.staticScript;
        CurrencyManager oldCurrency = CurrencyManager.Instance;
        LevelManager oldLevel = LevelManager.Instance;
        int oldLanguage = Localize.Language;
        float oldTimeScale = Time.timeScale;
        string[] keys = { "home_equipped_character_index", "home_character_blue_unlocked", "PlayerMoney", "CurrentLevel", Localize.LanguagePreferenceKey };
        var preferences = keys.ToDictionary(key => key, key => (exists: PlayerPrefs.HasKey(key), value: PlayerPrefs.GetInt(key)));
        FieldInfo heartData = typeof(HeartManager).GetField("data", BindingFlags.Static | BindingFlags.NonPublic);
        FieldInfo heartRound = typeof(HeartManager).GetField("roundInProgress", BindingFlags.Static | BindingFlags.NonPublic);
        object oldHearts = heartData.GetValue(null), oldRound = heartRound.GetValue(null);
        Scene scene = EditorSceneManager.OpenPreviewScene(ScenePath);
        RenderTexture previousTarget = RenderTexture.active;
        var report = new StringBuilder("VERSION=" + Revision + "\nSAVED Home disposable preview; no scene saves or character purchase/equip actions.\n");
        try
        {
            AudioManager.Instance = null; GameMaster.staticScript = null; CurrencyManager.Instance = null;
            LevelManager.Instance = Nodes(scene).Select(node => node.GetComponent<LevelManager>()).FirstOrDefault(item => item != null);
            heartData.SetValue(null, new HeartState()); heartRound.SetValue(null, false);
            HomeExperienceUI home = Nodes(scene).Select(node => node.GetComponent<HomeExperienceUI>()).Single(item => item != null);
            Transform page = Nodes(scene).Single(node => node.name == "Character Page");
            Image outline = page.GetComponentsInChildren<Image>(true).Single(image => image.name == "IsChoosing");
            int outlineId = outline.GetInstanceID();
            var outlineLayout = new RectSchema(outline.rectTransform, true);
            Sprite outlineSprite = outline.sprite;
            Color outlineColor = outline.color;
            Invoke(home, "LoadHomeResources");
            Require((bool)Invoke(home, "BindSceneHome"), "The authored Home could not bind.");
            int equipped = Get<int>(home, "equippedCharacterIndex");
            bool unlocked = Get<bool>(home, "blueCharacterUnlocked");
            Button[] cards = Enumerable.Range(0, 4).Select(index => page.GetComponentsInChildren<Button>(true).Single(button => button.name == "Character Skin " + index)).ToArray();
            RectTransform[] frames = cards.Select(card => card.transform.Find("Card Frame Sprite") as RectTransform).ToArray();
            var cardLayouts = cards.Select(card => card.GetComponent<RectTransform>()).Concat(frames).ToDictionary(rect => rect, rect => new RectSchema(rect, false));
            Camera camera = Nodes(scene).Select(node => node.GetComponent<Camera>()).First(item => item != null && item.CompareTag("MainCamera"));
            camera.scene = scene;
            Vector3 alignmentOffset = Vector3.zero;
            Directory.CreateDirectory("Temp/CharacterSelectionValidation");
            foreach (Vector2 size in new[] { new Vector2(1170, 2532), new Vector2(1536, 2048) })
            {
                var target = new RenderTexture((int)size.x, (int)size.y, 24, RenderTextureFormat.ARGB32);
                try
                {
                    target.Create(); camera.targetTexture = target;
                    foreach (Canvas canvas in Nodes(scene).Select(node => node.GetComponent<Canvas>()).Where(item => item != null && item.isRootCanvas))
                        PortraitDisplay.ApplyCanvasLayout(canvas, camera, PortraitDisplay.ReferenceResolutionForScene(scene), size);
                    ResponsiveSceneLayout.Apply(scene, camera, size);
                    Invoke(home, "SelectTab", 4); Canvas.ForceUpdateCanvases(); camera.Render(); Canvas.ForceUpdateCanvases();
                    Tick(home, 0f); Settle(home);
                    if (alignmentOffset == Vector3.zero) alignmentOffset = Center(outline.rectTransform) - Center(frames[Get<int>(home, "previewCharacterIndex")]);
                    Require(outline.gameObject.activeInHierarchy && !outline.raycastTarget, "The existing outline is invisible or blocks card input.");
                    for (int index = 0; index < 4; index++)
                    {
                        Vector3 start = Center(outline.rectTransform), destination = Center(frames[index]) + alignmentOffset;
                        Invoke(home, "SelectCharacterPreview", index);
                        if (Vector3.Distance(start, destination) > 1f)
                        {
                            Tick(home, 1f / 60f);
                            Require(Vector3.Distance(Center(outline.rectTransform), start) > .001f && Vector3.Distance(Center(outline.rectTransform), destination) > .1f,
                                "A visible retarget must animate rather than teleport.");
                        }
                        Settle(home); AssertCenter(outline, frames[index], alignmentOffset);
                        Require(Get<int>(home, "previewCharacterIndex") == index, "Preview selection did not update.");
                        if (index >= 2) Require(!(bool)Invoke(home, "IsCharacterUnlocked", index), "Locked-card preview unexpectedly unlocked a character.");
                        foreach (var authored in cardLayouts) Require(authored.Value.Matches(authored.Key), "Selection introduced a second card geometry/scale writer.");
                        Capture(camera, target, "Temp/CharacterSelectionValidation/" + (size.x == 1170 ? "Phone" : "iPad") + "-Card-" + index + ".png");
                    }
                    Invoke(home, "SelectCharacterPreview", 3); Settle(home);
                    Invoke(home, "CycleCharacterPreview", 1); Settle(home);
                    Require(Get<int>(home, "previewCharacterIndex") == 0, "Next-arrow wrap did not select card0."); AssertCenter(outline, frames[0], alignmentOffset);
                    Invoke(home, "CycleCharacterPreview", -1); Settle(home);
                    Require(Get<int>(home, "previewCharacterIndex") == 3, "Previous-arrow wrap did not select card3."); AssertCenter(outline, frames[3], alignmentOffset);
                    foreach (int index in new[] { 0, 3, 1, 2, 0 }) { Invoke(home, "SelectCharacterPreview", index); Tick(home, .025f); }
                    Settle(home); AssertCenter(outline, frames[0], alignmentOffset);

                    EventSystem events = Nodes(scene).Select(node => node.GetComponent<EventSystem>()).FirstOrDefault(item => item != null);
                    var pointer = new PointerEventData(events) { pointerId = 5, position = RectTransformUtility.WorldToScreenPoint(camera, frames[0].TransformPoint(frames[0].rect.center)) };
                    SendPointer(cards[0], EventTriggerType.PointerEnter, pointer); Settle(home);
                    Require(Vector3.Distance(cards[0].transform.localScale, cardLayouts[(RectTransform)cards[0].transform].Scale * .88f) < .001f, "Existing hover feedback stopped working.");
                    AssertCenter(outline, frames[0], alignmentOffset);
                    SendPointer(cards[0], EventTriggerType.PointerDown, pointer); Settle(home);
                    Require(Vector3.Distance(cards[0].transform.localScale, cardLayouts[(RectTransform)cards[0].transform].Scale * .78f) < .001f, "Existing press feedback stopped working.");
                    SendPointer(cards[0], EventTriggerType.PointerExit, pointer); Settle(home);
                    AssertCenter(outline, frames[0], alignmentOffset);
                    foreach (var authored in cardLayouts) Require(authored.Value.Matches(authored.Key), "Hover did not restore the authored card geometry.");
                    var hits = new List<RaycastResult>();
                    home.GetComponentsInChildren<GraphicRaycaster>(true).Single(item => item.name == "Home Canvas").Raycast(pointer, hits);
                    Require(hits.All(hit => hit.gameObject != outline.gameObject) && hits.Any(hit => hit.gameObject == cards[0].gameObject), "The outline obstructed the selected card hit target.");
                    float lowestAlpha = 1f, highestAlpha = 0f;
                    for (int frame = 0; frame < 120; frame++) { Tick(home, 1f / 60f); lowestAlpha = Mathf.Min(lowestAlpha, outline.color.a); highestAlpha = Mathf.Max(highestAlpha, outline.color.a); }
                    Require(lowestAlpha >= outlineColor.a * .899f && highestAlpha <= outlineColor.a * 1.001f && highestAlpha - lowestAlpha > .03f,
                        "The resting outline pulse is missing or too strong.");
                    Invoke(home, "SelectTab", 0); Tick(home, 0f); Invoke(home, "SelectTab", 4); Tick(home, 0f);
                    Require(Get<int>(home, "previewCharacterIndex") == equipped, "Reopening character selection did not restore equipped preview.");
                    AssertCenter(outline, frames[equipped], alignmentOffset);
                    Invoke(home, "BindCharacterSelectionHighlight", page); Tick(home, 0f); AssertCenter(outline, frames[equipped], alignmentOffset);
                    Require(outline.GetInstanceID() == outlineId && page.GetComponentsInChildren<Image>(true).Count(image => image.name == "IsChoosing") == 1 &&
                        outline.sprite == outlineSprite && outlineLayout.Matches(outline.rectTransform) &&
                        Mathf.Approximately(outline.color.r, outlineColor.r) && Mathf.Approximately(outline.color.g, outlineColor.g) && Mathf.Approximately(outline.color.b, outlineColor.b),
                        "Selection replaced or resized the authored outline artwork.");
                    report.AppendLine("PASS " + size + ": four previews including locked cards; smooth motion, arrow wraps, rapid retarget, hover/press, resting pulse, nonblocking input and reopen/rebind.");
                }
                finally { camera.targetTexture = null; RenderTexture.active = previousTarget; target.Release(); UnityEngine.Object.DestroyImmediate(target); }
            }
            Require(Get<int>(home, "equippedCharacterIndex") == equipped && Get<bool>(home, "blueCharacterUnlocked") == unlocked && Mathf.Approximately(Time.timeScale, oldTimeScale), "Preview selection changed equipment, unlock state or time scale.");
            foreach (var preference in preferences) Require(PlayerPrefs.HasKey(preference.Key) == preference.Value.exists && PlayerPrefs.GetInt(preference.Key) == preference.Value.value,
                "Preview selection changed a saved player preference: " + preference.Key);
            report.AppendLine("PASS original IsChoosing sprite/anchors/size/scale reused; card geometry, equipped character, wallet, unlocks and saved progress unchanged. Eight preview PNGs under Temp/CharacterSelectionValidation.");
            File.WriteAllText(Result, report.ToString());
        }
        finally
        {
            RenderTexture.active = previousTarget; EditorSceneManager.ClosePreviewScene(scene);
            heartData.SetValue(null, oldHearts); heartRound.SetValue(null, oldRound);
            AudioManager.Instance = oldAudio; GameMaster.staticScript = oldMaster; CurrencyManager.Instance = oldCurrency; LevelManager.Instance = oldLevel;
            Localize.SetLanguage(oldLanguage, false);
        }
    }

    private static void Tick(HomeExperienceUI home, float delta)
    {
        Invoke(home, "UpdateButtonFeedback", delta); Invoke(home, "UpdateCharacterSelectionHighlight", delta, false);
        Invoke(home, "UpdateCharacterPreviewAnimation", (float)EditorApplication.timeSinceStartup);
    }
    private static void Settle(HomeExperienceUI home) { for (int frame = 0; frame < 100; frame++) Tick(home, 1f / 60f); }
    private static Vector3 Center(RectTransform rect)
    {
        Transform space = rect.parent;
        while (space != null && space.name != "Character Aspect Content") space = space.parent;
        Require(space != null, "A character selection object moved outside the authored content.");
        return space.InverseTransformPoint(rect.TransformPoint(rect.rect.center));
    }
    private static void SendPointer(Button card, EventTriggerType type, PointerEventData pointer) => card.GetComponent<EventTrigger>().triggers.Single(entry => entry.eventID == type).callback.Invoke(pointer);
    private static void AssertCenter(Image outline, RectTransform card, Vector3 offset) => Require(Vector3.Distance(Center(outline.rectTransform), Center(card) + offset) < .15f, "The outline did not settle around the selected card frame.");
    private static Transform[] Nodes(Scene scene) => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
    private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    private static object Invoke(object target, string method, params object[] arguments) => (target.GetType().GetMethod(method, Private) ?? throw new MissingMethodException(method)).Invoke(target, arguments);
    private static void Capture(Camera camera, RenderTexture target, string path)
    {
        Canvas.ForceUpdateCanvases(); camera.Render(); Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
        var pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        try { pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply(); File.WriteAllBytes(path, pixels.EncodeToPNG()); }
        finally { UnityEngine.Object.DestroyImmediate(pixels); }
    }
    private readonly struct RectSchema
    {
        private readonly Vector2 min, max, size, pivot;
        private readonly Vector3 position, scale;
        private readonly Quaternion rotation;
        private readonly Transform parent;
        private readonly bool moves;
        public Vector3 Scale => scale;
        public RectSchema(RectTransform rect, bool moves) { min = rect.anchorMin; max = rect.anchorMax; size = rect.sizeDelta; pivot = rect.pivot; position = rect.anchoredPosition3D; scale = rect.localScale; rotation = rect.localRotation; parent = rect.parent; this.moves = moves; }
        public bool Matches(RectTransform rect) => min == rect.anchorMin && max == rect.anchorMax && size == rect.sizeDelta && pivot == rect.pivot && Vector3.Distance(scale, rect.localScale) < .0001f && rotation == rect.localRotation && parent == rect.parent && (moves || Vector3.Distance(position, rect.anchoredPosition3D) < .001f);
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    [MenuItem("Tools/Diagnostics/Audit Authored Character Selection")]
    public static void Audit()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Audit authored Home outside Play Mode.");
        var report = new StringBuilder("VERSION=" + Revision + "\n");
        Scene live = SceneManager.GetSceneByPath(ScenePath);
        if (live.IsValid() && live.isLoaded) Dump(live, "LIVE", report);
        else report.AppendLine("LIVE Home is not loaded; unsaved outline cannot be inspected.");
        Scene saved = EditorSceneManager.OpenPreviewScene(ScenePath);
        try { Dump(saved, "SAVED", report); }
        finally { EditorSceneManager.ClosePreviewScene(saved); }
        report.AppendLine("PASS read-only character audit. No layout rebuild, binding, scene save or player-state action.");
        File.WriteAllText(Result, report.ToString());
    }

    private static void Dump(Scene scene, string source, StringBuilder report)
    {
        Transform[] nodes = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
        Transform page = nodes.SingleOrDefault(node => node.name == "Character Page");
        report.AppendLine(source + " Home dirty=" + scene.isDirty + " path=" + scene.path);
        if (page == null) { report.AppendLine(source + " Character Page=MISSING"); return; }
        Transform content = page.GetComponentsInChildren<Transform>(true).SingleOrDefault(node => node.name == "Character Aspect Content");
        Transform[] outlines = page.GetComponentsInChildren<Transform>(true).Where(node => node.name == "IsChoosing").ToArray();
        report.AppendLine(source + " IsChoosing count=" + outlines.Length + (outlines.Length == 0 ? " MISSING" : ""));
        foreach (Transform node in page.GetComponentsInChildren<Transform>(true).Where(node => node == page || node == content ||
            node.name == "IsChoosing" || node.name.StartsWith("Character Skin ", StringComparison.Ordinal) || node.name == "Card Frame Sprite" ||
            node.name.IndexOf("Highlight", StringComparison.OrdinalIgnoreCase) >= 0 || node.name.IndexOf("Selection", StringComparison.OrdinalIgnoreCase) >= 0 ||
            node.name.IndexOf("Selected", StringComparison.OrdinalIgnoreCase) >= 0 || node.name.IndexOf("Choosing", StringComparison.OrdinalIgnoreCase) >= 0 ||
            node.name == "Equipped Check Sprite"))
        {
            var line = new StringBuilder(source + " " + Path(node, page) + " active=" + node.gameObject.activeSelf + " sibling=" + node.GetSiblingIndex());
            if (node is RectTransform rect)
            {
                line.Append(" anchors=" + rect.anchorMin + "/" + rect.anchorMax + " pos=" + rect.anchoredPosition + " size=" + rect.sizeDelta +
                    " pivot=" + rect.pivot + " scale=" + rect.localScale + " actual=" + rect.rect.size);
                if (content != null)
                {
                    var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                    Vector3[] local = corners.Select(corner => content.InverseTransformPoint(corner)).ToArray();
                    line.Append(" contentBounds=(" + local.Min(point => point.x) + "," + local.Min(point => point.y) + ")..(" + local.Max(point => point.x) + "," + local.Max(point => point.y) + ")");
                }
            }
            Image image = node.GetComponent<Image>();
            if (image != null) line.Append(" IMAGE sprite=" + (image.sprite == null ? "none" : image.sprite.name) + " asset=" +
                (image.sprite == null ? "none" : AssetDatabase.GetAssetPath(image.sprite)) + " color=" + image.color + " raycast=" + image.raycastTarget +
                " preserveAspect=" + image.preserveAspect + " type=" + image.type + " enabled=" + image.enabled);
            Animator animator = node.GetComponent<Animator>();
            if (animator != null) line.Append(" ANIMATOR enabled=" + animator.enabled + " controller=" +
                (animator.runtimeAnimatorController == null ? "none" : AssetDatabase.GetAssetPath(animator.runtimeAnimatorController)) + " update=" + animator.updateMode + " culling=" + animator.cullingMode);
            CanvasGroup group = node.GetComponent<CanvasGroup>();
            if (group != null) line.Append(" CanvasGroup alpha=" + group.alpha + " raycast=" + group.blocksRaycasts + " interactable=" + group.interactable);
            Button button = node.GetComponent<Button>();
            if (button != null) line.Append(" BUTTON transition=" + button.transition + " interactable=" + button.interactable);
            line.Append(" components=" + string.Join(",", node.GetComponents<Component>().Where(component => component != null).Select(component => component.GetType().Name)));
            report.AppendLine(line.ToString());
        }
    }

    private static string Path(Transform node, Transform root) => node == root ? node.name : Path(node.parent, root) + "/" + node.name;
}
#endif
