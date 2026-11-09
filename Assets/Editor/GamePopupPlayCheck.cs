#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Opt-in native Play check which restores the original editor scenes and saved player state.</summary>
[InitializeOnLoad]
public static class GamePopupPlayCheck
{
    private const string Request = "Temp/GamePopupPlayCheck.request";
    private const string Result = "Temp/GamePopupPlayCheck.result";
    private const string StatePath = "Temp/GamePopupPlayCheck.state.json";
    private const string ReplayRequest = "Temp/LiveReplayRegression.request";
    private const string ReplayResult = "Temp/LiveReplayRegression.result";
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static bool polling;

    [Serializable] private class SceneRecord { public string path; public bool isLoaded, isActive; }
    [Serializable] private class PrefRecord { public string key, kind, text; public bool exists; public int number; }
    [Serializable] private class CheckState
    {
        public string phase, report, failure;
        public double began, phaseBegan;
        public bool background;
        public int frame;
        public string gridId;
        public List<SceneRecord> scenes = new List<SceneRecord>();
        public List<PrefRecord> prefs = new List<PrefRecord>();
    }

    static GamePopupPlayCheck() { EditorApplication.update += Poll; }
    private static void Poll()
    {
        if (polling || !File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        polling = true;
        CheckState state = null;
        try
        {
            if (!File.Exists(StatePath)) { Begin(); return; }
            state = JsonUtility.FromJson<CheckState>(File.ReadAllText(StatePath));
            if (state.phase != "Exiting" && EditorApplication.timeSinceStartup - state.began > 45)
                throw new TimeoutException("Native popup check exceeded 45s in phase " + state.phase);
            if (state.phase == "Exiting")
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                RestoreAndFinish(state);
                return;
            }
            if (!EditorApplication.isPlaying)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                throw new InvalidOperationException("Play stopped before native popup checks finished.");
            }
            Require(!EditorApplication.isPaused, "Native Play must not be Editor-paused.");
            Application.runInBackground = true;
            Step(state);
        }
        catch (Exception exception)
        {
            if (state == null && File.Exists(StatePath)) state = JsonUtility.FromJson<CheckState>(File.ReadAllText(StatePath));
            if (state == null)
            {
                File.WriteAllText(Result, "FAIL\n" + exception);
                File.Delete(Request);
            }
            else
            {
                state.failure = exception.ToString();
                Append(state, "FAIL " + exception.Message);
                Exit(state);
            }
            Debug.LogWarning("Native popup Play check failed: " + exception.Message);
        }
        finally { polling = false; }
    }

    private static void Begin()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Stop Play before starting the autonomous popup check.");
        Require(!File.Exists(ReplayRequest), "A live replay check is already pending.");
        var state = new CheckState { phase = "Bootstrap", began = EditorApplication.timeSinceStartup, background = Application.runInBackground, report = "" };
        foreach (SceneSetup setup in EditorSceneManager.GetSceneManagerSetup())
        {
            Require(!string.IsNullOrEmpty(setup.path), "Cannot restore an unsaved editor scene.");
            Scene scene = SceneManager.GetSceneByPath(setup.path);
            Require(!scene.IsValid() || !scene.isDirty, "Save the current scene before native checks: " + setup.path);
            state.scenes.Add(new SceneRecord { path = setup.path, isLoaded = setup.isLoaded, isActive = setup.isActive });
        }
        foreach (string key in new[] { "player_hearts_v1", "daily_missions_v1", "achievements_v1" }) CapturePref(state, key, "string");
        foreach (string key in new[] { "CurrentLevel", "PlayerMoney", "Money", "Mussic", "SFX", "Vibration", "sound_on_off", "music_on_off", "user_gold" })
            CapturePref(state, key, "int");
        foreach (string key in new[] { "sound_on_off", "music_on_off", "user_gold" })
        {
            CapturePref(state, key + "_encryption_check", "string");
            CapturePref(state, key + "_used_key", "int");
        }
        Append(state, "BEGIN: clean editor scenes and exact player save snapshots captured. No scene assets will be saved.");
        Save(state);
        EditorSceneManager.OpenScene("Assets/Scenes/Start.unity", OpenSceneMode.Single);
        Application.runInBackground = true;
        EditorApplication.EnterPlaymode();
    }

    private static void Step(CheckState state)
    {
        switch (state.phase)
        {
            case "Bootstrap":
                if (SceneManager.GetActiveScene().name != "Home" || GameMaster.staticScript == null) return;
                state.frame = Time.frameCount;
                Phase(state, "HomeWarmup");
                return;
            case "HomeWarmup":
                if (Time.frameCount - state.frame < 5) return;
                Require(HeartManager.CanPlay, "Normal Home bootstrap does not permit starting a game.");
                Append(state, "PASS normal Start/Home bootstrap; GameMaster initialized.");
                Phase(state, "GameWarmup");
                SceneManager.LoadScene("Game");
                return;
            case "GameWarmup":
                if (File.Exists("Temp/ProjectilePoolLiveRegression.request")) return;
                GridManager grid = GridManager.Instance;
                if (grid == null || grid.gameObject.scene.name != "Game" || grid.IsExiting || grid.grid == null || GamePauseUI.Instance == null) return;
                string gridId = grid.GetEntityId().ToString();
                if (state.gridId != gridId) { state.gridId = gridId; state.frame = Time.frameCount; Save(state); return; }
                if (Time.frameCount - state.frame < 5) return;
                Require(Time.timeScale > 0f && HeartManager.RoundInProgress, "Game did not begin a running heart round.");
                Append(state, "PASS native Game initialization and frame advance; grid=" + gridId);
                Click(GamePauseUI.Instance, "Pause");
                state.frame = Time.frameCount;
                Phase(state, "SettingsWarmup");
                return;
            case "SettingsWarmup":
                if (Time.frameCount - state.frame < 3) return;
                RunButtonsAndToggles(state);
                Phase(state, "CaptureSettings");
                return;
            case "CaptureSettings":
                if (Time.frameCount - state.frame < 3) return;
                ScreenCapture.CaptureScreenshot("Temp/GamePopupPlayCheck.settings.png");
                state.frame = Time.frameCount;
                Phase(state, "AfterSettings");
                return;
            case "AfterSettings":
                if (Time.frameCount - state.frame < 3) return;
                Click(GamePauseUI.Instance, "Resume");
                Require(Time.timeScale > 0f && !Get<GameObject>(GamePauseUI.Instance, "overlay").activeSelf, "Resume persistent callback left gameplay paused or blocked.");
                InvokeRegressionLive();
                Append(state, "PASS modal pause/resume, exit-confirm cancel, victory/loss closure; settings unchanged after two clicks each.");
                GamePauseUI.Instance.PresentVictory();
                state.frame = Time.frameCount;
                Phase(state, "CaptureVictory");
                return;
            case "CaptureVictory":
                if (Time.frameCount - state.frame < 3) return;
                ScreenCapture.CaptureScreenshot("Temp/GamePopupPlayCheck.victory.png");
                state.frame = Time.frameCount;
                Phase(state, "AfterVictory");
                return;
            case "AfterVictory":
                if (Time.frameCount - state.frame < 3) return;
                GamePauseUI.Instance.PrepareForNextLevel();
                GamePauseUI.Instance.PresentLoss();
                state.frame = Time.frameCount;
                Phase(state, "CaptureLoss");
                return;
            case "CaptureLoss":
                if (Time.frameCount - state.frame < 3) return;
                ScreenCapture.CaptureScreenshot("Temp/GamePopupPlayCheck.loss.png");
                state.frame = Time.frameCount;
                Phase(state, "AfterLoss");
                return;
            case "AfterLoss":
                if (Time.frameCount - state.frame < 3) return;
                GamePauseUI.Instance.PrepareForNextLevel();
                Require(Time.timeScale > 0f, "Closing loss did not resume time.");
                File.WriteAllText(ReplayRequest, "manual");
                Phase(state, "Replay");
                return;
            case "Replay":
                if (File.Exists(ReplayRequest)) return;
                Require(File.Exists(ReplayResult), "Live replay produced no result.");
                string replay = File.ReadAllText(ReplayResult);
                Require(replay.Contains("PASS: two consecutive real Game reloads"), "Native replay regression failed:\n" + replay);
                Append(state, "PASS two actual Game scene reloads after authored popup integration.\n" + replay);
                Exit(state);
                return;
            default: throw new InvalidOperationException("Unknown native popup check phase " + state.phase);
        }
    }

    private static void RunButtonsAndToggles(CheckState state)
    {
        GamePauseUI controller = GamePauseUI.Instance;
        Require(Time.timeScale == 0f && Get<GameObject>(controller, "menuPanel").activeSelf, "HUD pause persistent callback did not open settings.");
        foreach (var setting in new[] { new[] { "ToggleMusic", "musicOn", "musicOff", "Mussic" }, new[] { "ToggleSound", "soundOn", "soundOff", "SFX" }, new[] { "ToggleVibration", "vibrationOn", "vibrationOff", "Vibration" } })
        {
            GameObject on = Get<GameObject>(controller, setting[1]);
            GameObject off = Get<GameObject>(controller, setting[2]);
            bool initial = on.activeSelf;
            Click(controller, setting[0]);
            Require(on.activeSelf != initial && off.activeSelf == initial && PlayerPrefs.GetInt(setting[3]) == (initial ? 0 : 1), setting[0] + " first click did not change visible state and preference.");
            Click(controller, setting[0]);
            Require(on.activeSelf == initial && off.activeSelf != initial && PlayerPrefs.GetInt(setting[3]) == (initial ? 1 : 0), setting[0] + " second click did not restore visible state and preference.");
            if (AudioManager.Instance != null && setting[0] != "ToggleVibration")
            {
                float actual = setting[0] == "ToggleMusic" ? AudioManager.Instance.musicVolume : AudioManager.Instance.sfxVolume;
                Require(Mathf.Approximately(actual, initial ? 1f : 0f), setting[0] + " did not apply audio volume immediately.");
            }
            Append(state, "PASS pointer hit + saved callback " + setting[0] + " twice; state restored=" + initial);
        }
        state.frame = Time.frameCount;
        Save(state);
    }

    private static void Click(GamePauseUI controller, string method)
    {
        Button button = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude)
            .FirstOrDefault(candidate => candidate.gameObject.scene == controller.gameObject.scene && candidate.isActiveAndEnabled && candidate.IsInteractable() &&
                Enumerable.Range(0, candidate.onClick.GetPersistentEventCount()).Any(index => candidate.onClick.GetPersistentTarget(index) == controller && candidate.onClick.GetPersistentMethodName(index) == method));
        Require(button != null, "No active authored button for " + method);
        Canvas.ForceUpdateCanvases();
        Canvas canvas = button.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        RectTransform rect = (RectTransform)button.transform;
        Vector2 position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
        EventSystem events = EventSystem.current;
        Require(events != null && events.currentInputModule != null && events.currentInputModule.isActiveAndEnabled, "Game EventSystem is not ready.");
        var hits = new List<RaycastResult>();
        events.RaycastAll(new PointerEventData(events) { position = position }, hits);
        Require(hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == button.gameObject,
            method + " is blocked by " + (hits.Count > 0 ? hits[0].gameObject.name : "no raycast hit") +
            "; pixel=" + position + "; screen=" + Screen.width + "x" + Screen.height +
            "; canvas=" + (canvas == null ? "null" : canvas.name + " rect=" + ((RectTransform)canvas.transform).rect + " mode=" + canvas.renderMode) +
            "; camera=" + (camera == null ? "null" : camera.name + " pixelRect=" + camera.pixelRect) +
            "; graphic=" + (button.targetGraphic == null ? "null" : button.targetGraphic.name + " depth=" + button.targetGraphic.depth + " cull=" + button.targetGraphic.canvasRenderer.cull));
        button.onClick.Invoke();
    }

    private static void InvokeRegressionLive()
    {
        MethodInfo method = typeof(GamePopupRegression).GetMethod("RunLive", BindingFlags.Static | BindingFlags.NonPublic);
        Require(method != null, "Popup live regression method is missing.");
        method.Invoke(null, null);
    }

    private static void Exit(CheckState state)
    {
        state.phase = "Exiting";
        Save(state);
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Time.timeScale = 1f;
            if (GamePauseUI.Instance != null) GamePauseUI.Instance.PrepareForNextLevel();
            EditorApplication.ExitPlaymode();
        }
        else RestoreAndFinish(state);
    }

    private static void RestoreAndFinish(CheckState state)
    {
        foreach (PrefRecord pref in state.prefs)
        {
            if (!pref.exists) PlayerPrefs.DeleteKey(pref.key);
            else if (pref.kind == "string") PlayerPrefs.SetString(pref.key, pref.text);
            else PlayerPrefs.SetInt(pref.key, pref.number);
        }
        PlayerPrefs.Save();
        Application.runInBackground = state.background;
        EditorSceneManager.RestoreSceneManagerSetup(state.scenes.Select(scene => new SceneSetup { path = scene.path, isLoaded = scene.isLoaded, isActive = scene.isActive }).ToArray());
        foreach (PrefRecord pref in state.prefs)
        {
            Require(PlayerPrefs.HasKey(pref.key) == pref.exists, "Could not restore save key existence: " + pref.key);
            if (pref.exists) Require(pref.kind == "string" ? PlayerPrefs.GetString(pref.key) == pref.text : PlayerPrefs.GetInt(pref.key) == pref.number, "Could not restore save key: " + pref.key);
        }
        Append(state, (string.IsNullOrEmpty(state.failure) ? "PASS" : "FAIL") + ": exited Play and restored original editor scene setup plus exact heart/progression/money/mission/achievement/settings saves.");
        if (!string.IsNullOrEmpty(state.failure)) state.report += "\n" + state.failure;
        File.WriteAllText(Result, state.report);
        File.Delete(StatePath);
        File.Delete(Request);
    }

    private static void CapturePref(CheckState state, string key, string kind) => state.prefs.Add(new PrefRecord {
        key = key, kind = kind, exists = PlayerPrefs.HasKey(key), text = kind == "string" ? PlayerPrefs.GetString(key, "") : "", number = kind == "int" ? PlayerPrefs.GetInt(key, 0) : 0
    });
    private static void Phase(CheckState state, string phase) { state.phase = phase; state.phaseBegan = EditorApplication.timeSinceStartup; Save(state); }
    private static void Append(CheckState state, string value) { state.report += value + "\n"; File.WriteAllText(Result, state.report); }
    private static void Save(CheckState state) => File.WriteAllText(StatePath, JsonUtility.ToJson(state, true));
    private static T Get<T>(GamePauseUI controller, string field) where T : UnityEngine.Object => typeof(GamePauseUI).GetField(field, Private)?.GetValue(controller) as T;
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
#endif
