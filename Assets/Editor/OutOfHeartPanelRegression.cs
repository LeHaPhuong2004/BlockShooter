#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Checks saved artwork and heart routing in isolated preview scenes.</summary>
[InitializeOnLoad]
public static class OutOfHeartPanelRegression
{
    private const string Request = "Temp/OutOfHeartPanelRegression.request";
    private const string Result = "Temp/OutOfHeartPanelRegression.result";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
    private static readonly FieldInfo HeartData = typeof(HeartManager).GetField("data", StaticPrivate);
    private static readonly FieldInfo RoundInProgress = typeof(HeartManager).GetField("roundInProgress", StaticPrivate);
    private static readonly FieldInfo GameInstance = typeof(GamePauseUI).GetField("<Instance>k__BackingField", StaticPrivate);
    private static bool running;

    static OutOfHeartPanelRegression() => EditorApplication.update += Poll;

    private static void Poll()
    {
        if (running || !File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        running = true;
        try { Run(); }
        catch (Exception exception) { File.WriteAllText(Result, "FAIL\n" + exception); Debug.LogException(exception); }
        finally { File.Delete(Request); running = false; }
    }

    [MenuItem("Tools/Diagnostics/Check Out Of Heart Panels")]
    public static void Run()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Run the preview check outside Play Mode.");
        object previousHearts = HeartData.GetValue(null);
        object previousRound = RoundInProgress.GetValue(null);
        object previousGame = GameInstance.GetValue(null);
        NavigationManager previousNavigation = NavigationManager.Instance;
        AudioManager previousAudio = AudioManager.Instance;
        GameMaster previousMaster = GameMaster.staticScript;
        float previousScale = Time.timeScale;
        var report = new StringBuilder();
        try
        {
            AudioManager.Instance = null;
            GameMaster.staticScript = null;
            CheckHome(report);
            CheckGame(report);
            report.AppendLine("PASS saved panel artwork, callbacks, Home shop/close and Game heart routing; no scenes saved or player preferences written.");
            File.WriteAllText(Result, report.ToString());
        }
        finally
        {
            HeartData.SetValue(null, previousHearts);
            RoundInProgress.SetValue(null, previousRound);
            GameInstance.SetValue(null, previousGame);
            NavigationManager.Instance = previousNavigation;
            AudioManager.Instance = previousAudio;
            GameMaster.staticScript = previousMaster;
            Time.timeScale = previousScale;
        }
    }

    private static void CheckHome(StringBuilder report)
    {
        Scene scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Home.unity");
        try
        {
            Transform[] nodes = Nodes(scene);
            HomeExperienceUI home = nodes.Select(node => node.GetComponent<HomeExperienceUI>()).Single(item => item != null);
            GameObject panel = Field<GameObject>(home, "outOfHeartPanel");
            CheckSavedPanel(panel, "Home");
            Require(!nodes.Any(node => node.name == "Modal" && node.IsChildOf(home.transform)), "Home still contains the placeholder Modal.");
            Button[] buttons = panel.GetComponentsInChildren<Button>(true);
            Require(buttons.Length == 2, "Home must have exactly shop and close buttons.");
            Button shop = CallbackButton(buttons, home, "OpenHeartShop");
            Button close = CallbackButton(buttons, home, "CloseOutOfHearts");
            Require(shop != close, "Home shop and close must be separate buttons.");
            InjectHearts(0);
            Invoke(home, "LoadHomeResources");
            Require((bool)Invoke(home, "BindSceneHome"), "Home failed binding without the placeholder Modal.");
            Invoke(home, "Play");
            Require(panel.activeSelf && HeartManager.Hearts == 0 && !HeartManager.RoundInProgress, "Home Play did not present out-of-hearts without starting a round.");
            InvokeCallback(close);
            Require(!panel.activeSelf, "Home Close left the input blocker active.");
            home.ShowOutOfHearts();
            InvokeCallback(shop);
            GameObject[] pages = Field<GameObject[]>(home, "tabPages");
            Require(!panel.activeSelf && Field<int>(home, "activeTabIndex") == 1 && pages[1].activeSelf, "Home shop callback did not close the panel and select Shop.");
            Require(Field<GameObject>(home, "shopGoldViewport").activeSelf, "Heart shop did not open the gold/heart category.");
            report.AppendLine("PASS Home: authored panel hidden in saved scene; two valid buttons; zero-heart Play, Close and Shop release the blocker.");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static void CheckGame(StringBuilder report)
    {
        Scene scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Game.unity");
        GamePauseUI game = null;
        try
        {
            Transform[] nodes = Nodes(scene);
            game = nodes.Select(node => node.GetComponent<GamePauseUI>()).Single(item => item != null);
            NavigationManager navigation = nodes.Select(node => node.GetComponent<NavigationManager>()).Single(item => item != null);
            GameObject panel = Field<GameObject>(game, "outOfHeartPanel");
            GameObject overlay = Field<GameObject>(game, "overlay");
            CheckSavedPanel(panel, "Game");
            Require(!overlay.activeSelf, "Game shared popup overlay must be hidden in the saved scene.");
            Require(panel.transform.parent == overlay.transform.parent && panel.transform.GetSiblingIndex() > overlay.transform.GetSiblingIndex(), "Game out-of-hearts panel must be above the shared popup overlay.");
            Canvas canvas = panel.GetComponentInParent<Canvas>(true);
            Require(canvas != null && canvas.gameObject.name == "Game Popup Canvas" && canvas.GetComponent<GraphicRaycaster>() != null, "Game panel requires the popup Canvas and a GraphicRaycaster.");
            Require(nodes.Select(node => node.GetComponent<Canvas>()).Where(item => item != null && item != canvas).All(item => item.sortingOrder <= canvas.sortingOrder), "Game out-of-hearts Canvas is covered by another Canvas.");
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            Require(panelRect.anchorMin == Vector2.zero && panelRect.anchorMax == Vector2.one && panelRect.offsetMin.sqrMagnitude < .01f && panelRect.offsetMax.sqrMagnitude < .01f && (panelRect.localScale - Vector3.one).sqrMagnitude < .001f, "Game blocker must stretch across the full popup Canvas.");
            Button[] buttons = panel.GetComponentsInChildren<Button>(true);
            Require(buttons.Length == 1, "Game must have one Home button only.");
            Button home = CallbackButton(buttons, game, "ReturnHomeFromOutOfHearts");
            Require(Mathf.Abs(((RectTransform)home.transform).anchoredPosition.x) < .01f, "Game Home button is not centered.");
            Require(navigation.panelOutOfHeart == panel, "NavigationManager points to a different out-of-hearts panel.");
            GameInstance.SetValue(null, game);
            NavigationManager.Instance = navigation;
            Time.timeScale = .75f;

            InjectHearts(0);
            game.PresentLoss();
            CheckExclusiveGamePanel(game, panel, "zero-heart loss");
            ResetGame(game);
            game.Restart();
            CheckExclusiveGamePanel(game, panel, "zero-heart restart");
            ResetGame(game);
            navigation.OnNextButton();
            CheckExclusiveGamePanel(game, panel, "zero-heart Next");
            ResetGame(game);
            report.AppendLine("PASS Game: one centered Home callback, full-screen input blocker and top popup Canvas; zero-heart loss/restart/Next keep the scene paused.");

            InjectHearts(1);
            Require(HeartManager.BeginRound(), "Could not start the isolated final-heart round.");
            HeartManager.FinishRound(false);
            Require(HeartManager.Hearts == 0 && !HeartManager.RoundInProgress, "Final loss must consume exactly the last heart.");
            game.PresentLoss();
            CheckExclusiveGamePanel(game, panel, "final-heart loss");
            Require(HeartManager.Hearts == 0, "Presenting the final-heart popup changed hearts again.");
            ResetGame(game);

            InjectHearts(0, true);
            Require(HeartManager.BeginRound(), "Unlimited hearts must allow a round with zero stored hearts.");
            HeartManager.FinishRound(false);
            game.PresentLoss();
            Require(!panel.activeSelf && overlay.activeSelf && Field<GameObject>(game, "lossPanel").activeSelf && Time.timeScale == 0f, "Unlimited hearts must retain the regular loss popup.");
            ResetGame(game);
            report.AppendLine("PASS final-heart loss opens the art panel without another debit; unlimited hearts retain normal loss; popup cleanup restores the previous time scale.");
        }
        finally
        {
            if (game != null) game.PrepareForNextLevel();
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static void CheckSavedPanel(GameObject panel, string scene)
    {
        Require(panel != null && !panel.activeSelf, scene + " authored out-of-hearts reference is missing or starts active.");
        Require(panel.GetComponentsInChildren<Image>(true).Any(image => image.sprite != null && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(image.sprite))), scene + " panel has no saved sprite artwork.");
        Image blocker = panel.GetComponent<Image>();
        Require(blocker != null && blocker.enabled && blocker.raycastTarget && blocker.color.a > 0f, scene + " panel has no dimming input blocker.");
    }

    private static Button CallbackButton(Button[] buttons, UnityEngine.Object target, string method)
    {
        Button button = buttons.SingleOrDefault(item => Enumerable.Range(0, item.onClick.GetPersistentEventCount()).Any(index => item.onClick.GetPersistentTarget(index) == target && item.onClick.GetPersistentMethodName(index) == method));
        Require(button != null && button.enabled && button.interactable && button.gameObject.activeSelf && button.targetGraphic != null && button.targetGraphic.raycastTarget, "Missing usable callback " + target.GetType().Name + "." + method);
        return button;
    }

    private static void InvokeCallback(Button button)
    {
        var states = Enumerable.Range(0, button.onClick.GetPersistentEventCount()).Select(button.onClick.GetPersistentListenerState).ToArray();
        try
        {
            for (int index = 0; index < states.Length; index++) button.onClick.SetPersistentListenerState(index, UnityEventCallState.EditorAndRuntime);
            button.onClick.Invoke();
        }
        finally
        {
            for (int index = 0; index < states.Length; index++) button.onClick.SetPersistentListenerState(index, states[index]);
        }
    }

    private static void CheckExclusiveGamePanel(GamePauseUI game, GameObject panel, string action)
    {
        Require(panel.activeSelf && Time.timeScale == 0f && !Field<GameObject>(game, "overlay").activeSelf, action + " did not show the exclusive out-of-hearts panel.");
        foreach (string name in new[] { "menuPanel", "confirmationPanel", "victoryPanel", "lossPanel" })
            Require(!Field<GameObject>(game, name).activeSelf, action + " left " + name + " active.");
    }

    private static void ResetGame(GamePauseUI game)
    {
        game.PrepareForNextLevel();
        Require(Mathf.Approximately(Time.timeScale, .75f) && !Field<GameObject>(game, "outOfHeartPanel").activeSelf, "Game popup cleanup did not restore time and release its blocker.");
    }

    private static void InjectHearts(int hearts, bool unlimited = false)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        HeartData.SetValue(null, new HeartState { hearts = hearts, nextHeartAt = now + HeartState.RefillSeconds, unlimitedUntil = unlimited ? now + HeartState.UnlimitedSeconds : 0 });
        RoundInProgress.SetValue(null, false);
    }

    private static Transform[] Nodes(Scene scene) => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    private static object Invoke(object target, string name) => target.GetType().GetMethod(name, Private).Invoke(target, null);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
#endif
