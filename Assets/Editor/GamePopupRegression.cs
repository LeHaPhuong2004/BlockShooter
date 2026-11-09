#if UNITY_EDITOR
using System;
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

/// <summary>Checks saved popup wiring without changing the scene or player progression.</summary>
[InitializeOnLoad]
public static class GamePopupRegression
{
    private const string Request = "Temp/GamePopupRegression.request";
    private const string Result = "Temp/GamePopupRegression.result";
    private static readonly string[] ObjectFields = {
        "overlay", "menuPanel", "confirmationPanel", "victoryPanel", "lossPanel",
        "pauseButton", "continueButton", "confirmationMessage", "settingsLevelLabel", "victoryLevelLabel", "lossLevelLabel",
        "musicOn", "musicOff", "soundOn", "soundOff", "vibrationOn", "vibrationOff"
    };
    private static readonly string[] PanelFields = { "menuPanel", "confirmationPanel", "victoryPanel", "lossPanel" };
    private static bool running;

    static GamePopupRegression() { EditorApplication.update += Poll; }

    private static void Poll()
    {
        if (running || !File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        string mode = File.ReadAllText(Request).Trim();
        if (mode != "live" && EditorApplication.isPlayingOrWillChangePlaymode) return;
        running = true;
        try { if (mode == "live") RunLive(); else RunSaved(); }
        catch (Exception exception) { File.WriteAllText(Result, "FAIL\n" + exception); Debug.LogWarning("Popup regression failed: " + exception.Message); }
        finally { File.Delete(Request); running = false; }
    }

    [MenuItem("Tools/Diagnostics/Check Authored Game Popups")]
    public static void RunSaved()
    {
        var report = new StringBuilder();
        Scene scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Game.unity");
        try
        {
            var roots = scene.GetRootGameObjects();
            var controllers = roots.SelectMany(root => root.GetComponentsInChildren<GamePauseUI>(true)).ToArray();
            Require(controllers.Length == 1, "Expected exactly one authored GamePauseUI, found " + controllers.Length);
            GamePauseUI controller = controllers[0];
            Require(controller.gameObject.activeInHierarchy && controller.enabled, "Popup controller must remain active when modals are hidden.");
            var serialized = new SerializedObject(controller);
            foreach (string name in ObjectFields)
            {
                var property = serialized.FindProperty(name);
                Require(property != null && property.objectReferenceValue != null, "Missing authored field " + name);
                report.AppendLine("REF " + name + "=" + property.objectReferenceValue.name);
            }
            GameObject overlay = ObjectField<GameObject>(serialized, "overlay");
            Require(!overlay.activeSelf, "Saved overlay must be inactive so it cannot block gameplay input.");
            Require(overlay.GetComponentsInChildren<Graphic>(true).Any(graphic => graphic.raycastTarget), "Popup overlay has no input blocker.");
            foreach (string name in PanelFields)
            {
                GameObject panel = ObjectField<GameObject>(serialized, name);
                Require(!panel.activeSelf, "Saved " + name + " must be inactive.");
                Require(panel.transform.IsChildOf(overlay.transform), name + " must belong to shared overlay.");
                var images = panel.GetComponentsInChildren<Image>(true).Where(image => image.sprite != null).ToArray();
                Require(images.Length > 0, name + " has no authored sprite artwork.");
                Require(images.All(image => !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(image.sprite))), name + " uses a transient runtime sprite.");
                foreach (TMP_Text label in panel.GetComponentsInChildren<TMP_Text>(true))
                {
                    Require(label.font != null, "Missing font on " + label.name);
                    Require(label.fontSize > 0 && label.fontSize <= 160f, "Unexpected font size on " + label.name + ": " + label.fontSize);
                    Require(!label.raycastTarget, "Decorative label intercepts input: " + label.name);
                }
                report.AppendLine("PANEL " + name + " sprites=" + images.Length + " buttons=" + panel.GetComponentsInChildren<Button>(true).Length);
            }
            Button[] buttons = roots.SelectMany(root => root.GetComponentsInChildren<Button>(true)).ToArray();
            string[] expectedMethods = { "Pause", "Resume", "ExitToMenu", "ClaimDoubleVictoryReward", "ClaimVictoryRewardAndContinue", "Restart", "ToggleMusic", "ToggleSound", "ToggleVibration", "ConfirmLeave", "CancelLeave" };
            foreach (string method in expectedMethods)
            {
                var matching = buttons.Where(button => Enumerable.Range(0, button.onClick.GetPersistentEventCount())
                    .Any(index => button.onClick.GetPersistentTarget(index) == controller && button.onClick.GetPersistentMethodName(index) == method)).ToArray();
                Require(matching.Length > 0, "Missing persistent button callback GamePauseUI." + method);
                foreach (Button button in matching)
                {
                    Require(button.targetGraphic != null && button.targetGraphic.raycastTarget, "Button has no raycastable graphic: " + button.name);
                    Require(button.interactable, "Authored button disabled: " + button.name);
                }
                report.AppendLine("CALLBACK " + method + "=" + string.Join(",", matching.Select(button => button.name)));
            }
            NavigationManager navigation = roots.SelectMany(root => root.GetComponentsInChildren<NavigationManager>(true)).Single();
            var nav = new SerializedObject(navigation);
            Require(ObjectField<GameObject>(nav, "winPanel") == ObjectField<GameObject>(serialized, "victoryPanel"), "Navigation win panel does not match authored victory panel.");
            Require(ObjectField<GameObject>(nav, "losePanel") == ObjectField<GameObject>(serialized, "lossPanel"), "Navigation loss panel does not match authored loss panel.");
            Require(ObjectField<GameObject>(nav, "settingPanel") == ObjectField<GameObject>(serialized, "menuPanel"), "Navigation setting panel does not match authored settings panel.");
            GridManager grid = roots.SelectMany(root => root.GetComponentsInChildren<GridManager>(true)).Single();
            Require(grid.blockCountText != null && grid.blockCountText.fontSize > 44.75f, "Total block HUD text has not been enlarged.");
            report.AppendLine("HUD total block font=" + grid.blockCountText.fontSize + " text=" + grid.blockCountText.text);
            report.AppendLine("PASS saved popup artwork, persistent callbacks, modal input state and enlarged HUD.");
            File.WriteAllText(Result, report.ToString());
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    /// <summary>Optional check of an already running idle Game; never enters Play or loads a level.</summary>
    private static void RunLive()
    {
        Require(EditorApplication.isPlaying && !EditorApplication.isPaused, "Open Game in running Play mode for the optional live check.");
        GamePauseUI controller = UnityEngine.Object.FindObjectsByType<GamePauseUI>(FindObjectsInactive.Exclude).Single(item => item.gameObject.scene.name == "Game");
        var serialized = new SerializedObject(controller);
        GameObject overlay = ObjectField<GameObject>(serialized, "overlay");
        Require(!overlay.activeSelf && Time.timeScale > 0f, "Close the current modal before optional live check.");
        float previousScale = Time.timeScale;
        var report = new StringBuilder();
        try
        {
            Invoke(controller, "Pause");
            Require(Time.timeScale == 0f && overlay.activeSelf && ObjectField<GameObject>(serialized, "menuPanel").activeSelf, "Pause did not show settings and freeze game.");
            Invoke(controller, "Resume");
            Require(Mathf.Approximately(Time.timeScale, previousScale) && !overlay.activeSelf, "Resume left gameplay blocked or paused.");
            report.AppendLine("PASS pause/resume restores time and removes raycast blocker.");
            Invoke(controller, "Pause");
            Invoke(controller, "ExitToMenu");
            Require(ObjectField<GameObject>(serialized, "confirmationPanel").activeSelf && !ObjectField<GameObject>(serialized, "menuPanel").activeSelf, "Home did not show exclusive exit confirmation.");
            Invoke(controller, "CancelLeave");
            Require(ObjectField<GameObject>(serialized, "menuPanel").activeSelf, "Cancel did not return to settings.");
            Invoke(controller, "Resume");
            Require(!overlay.activeSelf && Mathf.Approximately(Time.timeScale, previousScale), "Cancel/resume left an input blocker.");
            report.AppendLine("PASS exit confirmation cancel returns to usable game.");
            Invoke(controller, "PresentVictory");
            Require(overlay.activeSelf && ObjectField<GameObject>(serialized, "victoryPanel").activeSelf, "Victory popup did not open.");
            Invoke(controller, "PrepareForNextLevel");
            Require(!overlay.activeSelf && Mathf.Approximately(Time.timeScale, previousScale), "Victory close did not restore game state.");
            Invoke(controller, "PresentLoss");
            Require(overlay.activeSelf && ObjectField<GameObject>(serialized, "lossPanel").activeSelf, "Loss popup did not open.");
            Invoke(controller, "PrepareForNextLevel");
            Require(!overlay.activeSelf && Mathf.Approximately(Time.timeScale, previousScale), "Loss close did not restore game state.");
            report.AppendLine("PASS victory/loss switching; no level load or player setting mutation.");
            File.WriteAllText(Result, report.ToString());
        }
        finally
        {
            if (overlay.activeSelf) Invoke(controller, "PrepareForNextLevel");
            Time.timeScale = previousScale;
        }
    }

    private static T ObjectField<T>(SerializedObject serialized, string name) where T : UnityEngine.Object => serialized.FindProperty(name)?.objectReferenceValue as T;
    private static void Invoke(GamePauseUI controller, string name)
    {
        MethodInfo method = typeof(GamePauseUI).GetMethod(name, BindingFlags.Public | BindingFlags.Instance);
        Require(method != null, "Missing controller API " + name);
        method.Invoke(controller, null);
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
#endif
