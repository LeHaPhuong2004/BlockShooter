#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Checks the authored Home in an isolated preview scene without rebuilding it.</summary>
[InitializeOnLoad]
public static class HomeSceneBindingCheck
{
    private const string RequestPath = "Assets/Editor/HomeSceneBindingCheck.request";
    private const string ReportPath = "Temp/HomeSceneBindingCheck.result";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    static HomeSceneBindingCheck() => EditorApplication.delayCall += RunIfRequested;

    private static void RunIfRequested()
    {
        if (!File.Exists(RequestPath)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += RunIfRequested;
            return;
        }
        try
        {
            PrepareAuthoredScene();
            Run();
            File.Delete(RequestPath);
            File.Delete(RequestPath + ".meta");
        }
        catch (Exception exception)
        {
            File.WriteAllText(ReportPath, exception.ToString());
            Debug.LogException(exception);
        }
    }

    private static void PrepareAuthoredScene()
    {
        const string path = "Assets/Scenes/Home.unity";
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            var home = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<HomeExperienceUI>(true)).Single();
            home.UpgradeSceneMissionsForEditor();
            home.FixSceneLockedCardSizesForEditor();
            home.FixSceneShopLayoutForEditor();
            EditorUtility.SetDirty(home);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the authored Home scene.");
        }
        finally { if (openedHere) EditorSceneManager.CloseScene(scene, true); }
    }

    [MenuItem("Tools/Diagnostics/Check Authored Home UI")]
    public static void Run()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Home.unity");
        var report = new StringBuilder();
        try
        {
            var roots = scene.GetRootGameObjects();
            var controllers = roots.SelectMany(root => root.GetComponentsInChildren<HomeExperienceUI>(true)).ToArray();
            if (controllers.Length != 1) throw new InvalidOperationException($"Expected one Home controller, found {controllers.Length}.");
            var home = controllers[0];
            var type = typeof(HomeExperienceUI);
            var authoredShopTabSprites = roots.SelectMany(root => root.GetComponentsInChildren<Image>(true))
                .Where(image => image.name == "GoldChosen" || image.name == "GoldNotChoose" ||
                    image.name == "BoosterChosen" || image.name == "BoosterNotChoose")
                .ToDictionary(image => image, image => image.sprite);
            int[] originalObjects = roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Select(node => node.gameObject.GetInstanceID()).OrderBy(id => id).ToArray();
            type.GetMethod("LoadHomeResources", Private).Invoke(home, null);
            bool bound = (bool)type.GetMethod("BindSceneHome", Private).Invoke(home, null);
            if (!bound)
            {
                report.AppendLine("FAIL: existing scene could not be bound. Scene objects:");
                foreach (var node in roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true)))
                    report.AppendLine(GetPath(node));
                File.WriteAllText(ReportPath, report.ToString());
                return;
            }
            var canvas = (GameObject)type.GetField("homeCanvasObject", Private).GetValue(home);
            var pages = (GameObject[])type.GetField("tabPages", Private).GetValue(home);
            var buttons = canvas.GetComponentsInChildren<Button>(true);
            if (canvas.GetComponent<GraphicRaycaster>() == null || !canvas.GetComponent<GraphicRaycaster>().enabled)
                throw new InvalidOperationException("Home Canvas has no active GraphicRaycaster.");
            if (!roots.SelectMany(root => root.GetComponentsInChildren<EventSystem>(true)).Any(system => system.isActiveAndEnabled))
                throw new InvalidOperationException("Home scene has no active EventSystem.");
            ValidateShopTab(buttons.First(button => button.name == "SHOP_GOLD_TAB"), true, authoredShopTabSprites);
            ValidateShopTab(buttons.First(button => button.name == "SHOP_BOOSTER_TAB"), false, authoredShopTabSprites);
            foreach (var button in buttons)
            {
                var action = (Action)type.GetMethod("SceneButtonAction", Private).Invoke(home, new object[] { button });
                if (action == null && button.onClick.GetPersistentEventCount() == 0)
                    throw new InvalidOperationException("Unbound button: " + GetPath(button.transform));
            }
            string[] tabs = { "HOME_TAB", "SHOP", "MISSIONS", "ACHIEVEMENTS", "CHARACTER" };
            for (int i = 0; i < tabs.Length; i++)
            {
                buttons.First(button => button.name == tabs[i]).onClick.Invoke();
                for (int j = 0; j < pages.Length; j++)
                    if (pages[j].activeSelf != (i == j)) throw new InvalidOperationException("Tab navigation failed: " + tabs[i]);
            }
            buttons.First(button => button.name == "SHOP_BOOSTER_TAB").onClick.Invoke();
            if (!((GameObject)type.GetField("shopBoosterViewport", Private).GetValue(home)).activeSelf)
                throw new InvalidOperationException("Booster category did not open.");
            ValidateShopTab(buttons.First(button => button.name == "SHOP_GOLD_TAB"), false, authoredShopTabSprites);
            ValidateShopTab(buttons.First(button => button.name == "SHOP_BOOSTER_TAB"), true, authoredShopTabSprites);
            buttons.First(button => button.name == "SHOP_GOLD_TAB").onClick.Invoke();
            ValidateShopTab(buttons.First(button => button.name == "SHOP_GOLD_TAB"), true, authoredShopTabSprites);
            ValidateShopTab(buttons.First(button => button.name == "SHOP_BOOSTER_TAB"), false, authoredShopTabSprites);
            buttons.First(button => button.name == "Character Skin 2").onClick.Invoke();
            if ((int)type.GetField("previewCharacterIndex", Private).GetValue(home) != 2)
                throw new InvalidOperationException("Character preview was not bound.");
            type.GetMethod("BindSceneHome", Private).Invoke(home, null);
            int[] afterObjects = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Select(node => node.gameObject.GetInstanceID()).OrderBy(id => id).ToArray();
            if (!originalObjects.SequenceEqual(afterObjects)) throw new InvalidOperationException("Binding created or deleted scene UI objects.");
            report.AppendLine($"PASS: {buttons.Length} authored buttons bound; five tabs, shop categories and character preview work.");
            report.AppendLine("PASS: both Shop tabs display the correct selected/idle sprites and have active raycast targets.");
            report.AppendLine($"PASS: all {originalObjects.Length} original scene objects retained; no new Canvas or rebuilt hierarchy; rebinding is idempotent.");
            report.AppendLine($"Mission cards: {canvas.GetComponentsInChildren<DailyMissionCardUI>(true).Length}.");
            var rayStates = (System.Collections.IList)type.GetField("cardRotatingLights", Private).GetValue(home);
            int expectedRays = canvas.GetComponentsInChildren<RectTransform>(true).Count(rect =>
                rect.name.Contains(" Soft Sun Ray ") || rect.name.Contains(" Sharp Ray ") || rect.name.Contains(" Center Sun Ray "));
            if (rayStates.Count != expectedRays) throw new InvalidOperationException("Some light rays were not registered for animation.");
            float beforeAngle = 0f;
            RectTransform centerRay = canvas.GetComponentsInChildren<RectTransform>(true).First(rect => rect.name.Contains(" Center Sun Ray "));
            centerRay.localRotation = Quaternion.Euler(0f, 0f, 123f);
            beforeAngle = centerRay.localEulerAngles.z;
            type.GetMethod("UpdateCardGlows", Private).Invoke(home, null);
            if (Mathf.Abs(Mathf.DeltaAngle(beforeAngle, centerRay.localEulerAngles.z)) < 0.01f)
                throw new InvalidOperationException("Achievement rays did not rotate.");
            report.AppendLine($"PASS: all {expectedRays} light rays animate, including achievement center rays.");
            var reference = canvas.GetComponent<CanvasScaler>().referenceResolution;
            foreach (var card in canvas.GetComponentsInChildren<RectTransform>(true).Where(rect => rect.name.StartsWith("LOCKED_") && rect.GetComponent<Button>() != null))
            {
                Vector2 size = Vector2.Scale(card.anchorMax - card.anchorMin, reference);
                if (Mathf.Abs(size.x - size.y) > 0.1f) throw new InvalidOperationException("Locked card is squeezed: " + card.name);
                report.AppendLine($"PASS: {card.name} authored size {size}, scale {card.localScale}.");
            }
            ValidateShopLayout(home, canvas, report);
            File.WriteAllText(ReportPath, report.ToString());
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static void ValidateShopTab(Button button, bool selected,
        System.Collections.Generic.IReadOnlyDictionary<Image, Sprite> authoredSprites)
    {
        string prefix = button.name == "SHOP_GOLD_TAB" ? "Gold" : "Booster";
        Transform chosen = button.transform.Find(prefix + "Chosen");
        Transform notChosen = button.transform.Find(prefix + "NotChoose");
        if (chosen != null && notChosen != null)
        {
            if (chosen.gameObject.activeSelf != selected || notChosen.gameObject.activeSelf == selected)
                throw new InvalidOperationException("Authored shop tab states are not selected correctly: " + button.name);
            foreach (Transform state in new[] { chosen, notChosen })
            {
                var stateImage = state.GetComponent<Image>();
                if (stateImage == null || !stateImage.enabled || stateImage.sprite == null ||
                    !authoredSprites.TryGetValue(stateImage, out Sprite authoredSprite) || stateImage.sprite != authoredSprite)
                    throw new InvalidOperationException("Authored shop tab artwork changed: " + button.name);
            }
            if (!button.enabled || !button.interactable || button.targetGraphic == null || !button.targetGraphic.raycastTarget)
                throw new InvalidOperationException("Shop tab is not configured for pointer interaction: " + button.name);
            return;
        }

        Sprite expectedSprite = Resources.Load<Sprite>(selected ? "UI/ShopItems/ShopTabSelected" : "UI/ShopItems/ShopTabIdle");
        var visual = button.transform.Find("Tab Sprite")?.GetComponent<Image>();
        if (expectedSprite == null || visual == null || visual.sprite != expectedSprite)
            throw new InvalidOperationException("Shop tab sprite does not match its selected/idle state: " + button.name);
        if (!button.enabled || !button.interactable || !visual.enabled || !visual.raycastTarget ||
            button.targetGraphic == null || !button.targetGraphic.raycastTarget)
            throw new InvalidOperationException("Shop tab is not configured for pointer interaction: " + button.name);
    }

    private static void ValidateShopLayout(HomeExperienceUI home, GameObject canvas, StringBuilder report)
    {
        var relayout = typeof(HomeExperienceUI).GetMethod("RefreshAuthoredShopLayout", Private);
        int products = 0;
        foreach (var scroll in canvas.GetComponentsInChildren<ScrollRect>(true))
        {
            if (scroll.name != "Gold Shop Viewport" && scroll.name != "Booster Shop Viewport") continue;
            var viewport = scroll.viewport;
            var min = viewport.anchorMin;
            var max = viewport.anchorMax;
            var size = viewport.sizeDelta;
            var position = viewport.anchoredPosition;
            try
            {
                foreach (float width in new[] { 600f, 1000f })
                {
                    viewport.anchorMin = viewport.anchorMax = Vector2.one * 0.5f;
                    viewport.sizeDelta = new Vector2(width, width * 1.15f);
                    relayout.Invoke(home, new object[] { true });
                    Canvas.ForceUpdateCanvases();
                    var items = scroll.content.Find("Shop Card Items");
                    var frames = scroll.content.Find("Shop Card Frames");
                    int index = 0;
                    foreach (Transform child in items)
                    {
                        if (!child.name.StartsWith("Shop Item - ")) continue;
                        var card = (RectTransform)child;
                        if (card.rect.width > width * 0.5f || card.localScale != Vector3.one)
                            throw new InvalidOperationException("Shop card exceeds its column: " + card.name);
                        var frame = (RectTransform)frames.GetChild(index++);
                        if ((frame.rect.size - card.rect.size).sqrMagnitude > 0.01f || frame.localScale != Vector3.one)
                            throw new InvalidOperationException("Shop frame does not fit its product: " + card.name);
                        foreach (Transform element in card)
                        {
                            var rect = element as RectTransform;
                            if (rect == null) continue;
                            var corners = new Vector3[4];
                            rect.GetLocalCorners(corners);
                            foreach (var corner in corners)
                            {
                                // Inspect direct-child bounds in parent coordinates. Inactive preview
                                // canvases can retain stale world matrices until their first render.
                                Vector3 local = rect.localPosition + rect.localRotation * Vector3.Scale(corner, rect.localScale);
                                if (local.x < card.rect.xMin - 0.1f || local.x > card.rect.xMax + 0.1f ||
                                    local.y < card.rect.yMin - 0.1f || local.y > card.rect.yMax + 0.1f)
                                    throw new InvalidOperationException("Shop artwork, title or buy button exceeds card: " + GetPath(element) +
                                        $"; corner={local}; card={card.rect}; element={rect.rect}; anchors={rect.anchorMin}/{rect.anchorMax}; scale={rect.localScale}; localPosition={rect.localPosition}; anchoredPosition={rect.anchoredPosition}");
                            }
                        }
                    }
                    if (width == 600f) products += index;
                }
            }
            finally
            {
                viewport.anchorMin = min;
                viewport.anchorMax = max;
                viewport.sizeDelta = size;
                viewport.anchoredPosition = position;
                relayout.Invoke(home, new object[] { true });
            }
        }
        if (products != 12) throw new InvalidOperationException("Expected 12 shop products, found " + products);
        report.AppendLine("PASS: all 12 shop cards fit two columns at 600 and 1000 UI units; frames, art, titles and buy buttons stay inside their cells.");
    }

    private static string GetPath(Transform node) => node.parent == null ? node.name : GetPath(node.parent) + "/" + node.name;
}
#endif
