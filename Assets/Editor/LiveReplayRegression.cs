#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DG.Tweening;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Checks two real Game reloads after the user enters Play, without charging a heart or invoking a leave confirmation.</summary>
[InitializeOnLoad]
public static class LiveReplayRegression
{
    private const string Request = "Temp/LiveReplayRegression.request";
    private const string Result = "Temp/LiveReplayRegression.result";
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly string[] StringSaveKeys = { "player_hearts_v1", "daily_missions_v1", "achievements_v1" };
    private static readonly Dictionary<string, string> savedStrings = new Dictionary<string, string>();
    private static readonly StringBuilder report = new StringBuilder();
    private static bool active, started, waitingForReload;
    private static int completedReloads, stableFrame, initialFrame, expectedLevel, initialHearts, savedLevel, savedMoney;
    private static string stableGridId, previousGridId;
    private static double lastStatusWrite, reloadStarted;
    private static GamePauseUI testPausedController;
    private static bool autoMode, autoEntryIssued, originalBackground;
    private static int homeFirstFrame, ownedKills, unexpectedCompletions;
    private static Sequence pendingOwnerSequence;

    static LiveReplayRegression() { EditorApplication.update += Poll; }

    private static void Poll()
    {
        if (!File.Exists(Request))
        {
            if (active) RestoreTemporaryState();
            active = false;
            return;
        }
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (!active) Reset();
        try
        {
            if (!Application.isPlaying)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (started || autoEntryIssued) throw new InvalidOperationException("Play stopped before the two reload checks completed.");
                string scene = SceneManager.GetActiveScene().name;
                if (autoMode && (scene == "Start" || scene == "Home"))
                {
                    autoEntryIssued = true;
                    File.AppendAllText(Request, "\nentered");
                    report.AppendLine("AUTO: entering Play from the existing " + scene + " scene; no scene asset was opened or saved.");
                    File.WriteAllText(Result, report.ToString());
                    EditorApplication.EnterPlaymode();
                    return;
                }
                Status("WAITING: enter Play and open Game; this check will not enter Play itself.");
                return;
            }
            if (EditorApplication.isPaused)
            {
                Status("WAITING: Editor Play is paused; the check will not resume it automatically.");
                return;
            }
            if (autoMode && !started && SceneManager.GetActiveScene().name == "Home")
            {
                if (homeFirstFrame == 0) homeFirstFrame = Time.frameCount;
                if (Time.frameCount - homeFirstFrame < 5 || GameMaster.staticScript == null)
                { Status("WAITING: normal Start/Home bootstrap has not settled yet."); return; }
                Require(HeartManager.CanPlay, "The normal Home entry does not currently allow playing.");
                report.AppendLine("AUTO: normal Home bootstrap settled; loading Game without purchases, rewards or a leave confirmation.");
                File.WriteAllText(Result, report.ToString());
                SceneManager.LoadScene("Game");
                return;
            }
            GridManager grid = GridManager.Instance;
            if (grid == null || grid.gameObject.scene.name != "Game" || !grid.gameObject.activeInHierarchy || grid.IsExiting || grid.grid == null)
            {
                CheckReloadTimeout();
                Status("WAITING: live Game grid is not initialized yet.");
                return;
            }
            string id = grid.GetEntityId().ToString();
            if (waitingForReload && id == previousGridId)
            {
                CheckReloadTimeout();
                return;
            }
            if (stableGridId != id)
            {
                stableGridId = id;
                stableFrame = Time.frameCount;
                return;
            }
            if (Time.frameCount - stableFrame < 5) return;
            if (GamePauseUI.Instance == null || Time.timeScale <= 0f)
            {
                CheckReloadTimeout();
                Status("WAITING: Game pause UI or running time scale is not ready.");
                return;
            }

            if (!started) CaptureBaseline();
            if (waitingForReload)
            {
                completedReloads++;
                waitingForReload = false;
                report.AppendLine("Reload " + completedReloads + " returned to a new live grid after " +
                    (EditorApplication.timeSinceStartup - reloadStarted).ToString("F3") + "s.");
                Require(ownedKills == completedReloads && unexpectedCompletions == 0,
                    "Replay did not kill its pending grid-owned animation without invoking completion.");
                pendingOwnerSequence = null;
            }
            ValidateLiveGrid(grid, completedReloads);
            if (completedReloads >= 2)
            {
                VerifySavedState();
                report.AppendLine("PASS: two consecutive real Game reloads completed with new grid instances, rendered frame progress, running time, responsive UI hit targets and centered dual HP glyphs.");
                report.AppendLine("No scene assets were saved. The check did not call ConfirmLeave/AbandonRound or write PlayerPrefs. Auto mode may enter Play from the existing Start/Home scene and leaves Game running for inspection.");
                Finish();
                return;
            }
            TriggerReload(grid);
        }
        catch (Exception exception)
        {
            if (testPausedController != null) testPausedController.Resume();
            testPausedController = null;
            report.AppendLine("FAIL\n" + exception);
            Finish();
            Debug.LogWarning("Live replay regression failed; see " + Result + ". " + exception.Message);
        }
    }

    private static void Reset()
    {
        active = true; started = false; waitingForReload = false;
        completedReloads = 0; stableGridId = previousGridId = null;
        stableFrame = initialFrame = 0; expectedLevel = 0;
        testPausedController = null; report.Clear(); savedStrings.Clear(); lastStatusWrite = 0;
        homeFirstFrame = ownedKills = unexpectedCompletions = 0; pendingOwnerSequence = null;
        string requestText = File.ReadAllText(Request);
        autoMode = requestText.IndexOf("auto", StringComparison.OrdinalIgnoreCase) >= 0;
        autoEntryIssued = requestText.Contains("entered");
        originalBackground = requestText.Contains("background=1") ||
            (!requestText.Contains("background=0") && Application.runInBackground);
        if (!requestText.Contains("background=")) File.AppendAllText(Request, "\nbackground=" + (originalBackground ? "1" : "0"));
        Application.runInBackground = true;
    }

    private static void CaptureBaseline()
    {
        Require(LevelManager.Instance != null, "Live LevelManager is missing.");
        FieldInfo loaded = typeof(LevelManager).GetField("loadedLevel", BindingFlags.Static | BindingFlags.NonPublic);
        int actualLoaded = loaded != null ? (int)loaded.GetValue(null) : 0;
        expectedLevel = actualLoaded > 0 ? actualLoaded : LevelManager.Instance.CurrentLevel;
        initialHearts = HeartManager.Hearts;
        savedLevel = PlayerPrefs.GetInt("CurrentLevel", 1);
        savedMoney = PlayerPrefs.GetInt("PlayerMoney", 0);
        foreach (string key in StringSaveKeys) savedStrings[key] = PlayerPrefs.GetString(key, "");
        initialFrame = Time.frameCount;
        started = true;
        report.AppendLine("BEGIN live replay: displayedLevel=" + LevelManager.Instance.CurrentLevel + " actualLoadedLevel=" +
            expectedLevel + " hearts=" + initialHearts + " frame=" + initialFrame + ". ConfirmLeave and its heart/daily charge are bypassed.");
    }

    private static void ValidateLiveGrid(GridManager grid, int stage)
    {
        Require(Time.timeScale > 0f && !grid.IsExiting, "The new grid is stopped or marked as exiting.");
        if (stage > 0) Require(LevelManager.Instance.CurrentLevel == expectedLevel, "Replay changed the actual loaded level.");
        Block[] duals = grid.GetComponentsInChildren<Block>(false)
            .Where(block => block.blockType == BlockType.Dual && block.group != null && !block.group.IsDead()).ToArray();
        int visible = 0;
        foreach (Block block in duals)
        {
            TMP_Text[] activeLabels = block.GetComponentsInChildren<TMP_Text>(true)
                .Where(label => label.isActiveAndEnabled && label.gameObject.activeInHierarchy).ToArray();
            Require(activeLabels.Length == 1 && activeLabels[0] == block.hpText,
                "Dual block must have exactly one active shared HP label: " + block.name);
            TMP_Text label = activeLabels[0];
            Require(label.text == block.group.totalHP.ToString(), "Dual HP text disagrees with its group total.");
            Require(label.margin == Vector4.zero, "Dual HP retained asymmetric half-label margins.");
            Require(label.fontSharedMaterial != null && label.fontSharedMaterial.shader != null,
                "Dual HP lost its font material during scene reload.");
            if (!BoardViewport.IsBlockVisible(block)) continue;
            visible++;
            Renderer renderer = label is TextMeshPro text ? text.renderer : label.GetComponent<Renderer>();
            MeshFilter filter = label.GetComponent<MeshFilter>();
            Require(renderer != null && renderer.enabled && filter != null && filter.sharedMesh != null && filter.sharedMesh.vertexCount > 0,
                "Visible dual HP has no rendered mesh.");
            Bounds glyphs = GlyphBounds(label);
            float distance = Vector2.Distance(glyphs.center, block.transform.position);
            Require(distance < .05f, "Visible HP glyphs are off-center: distance=" + distance + " glyph=" +
                glyphs.center.ToString("F5") + " block=" + block.transform.position.ToString("F5"));
        }
        ValidatePauseHitTarget();
        report.AppendLine("PASS stage=" + stage + " grid=" + grid.GetEntityId() + " sceneHandle=" + grid.gameObject.scene.handle +
            " frame=" + Time.frameCount + " frameAdvance=" + (Time.frameCount - initialFrame) + " timeScale=" + Time.timeScale +
            " level=" + LevelManager.Instance.CurrentLevel + " duals=" + duals.Length + " visibleGlyphsChecked=" + visible +
            " ownedAnimationKills=" + ownedKills + " unexpectedCompletions=" + unexpectedCompletions +
            (visible == 0 ? " (no dual currently visible; stored HP/material/margins checked only)" : ""));
        File.WriteAllText(Result, report.ToString());
    }

    private static Bounds GlyphBounds(TMP_Text label)
    {
        TMP_TextInfo info = label.textInfo;
        Require(info != null && info.characterInfo != null && info.meshInfo != null, "No generated HP text info.");
        bool any = false; Bounds bounds = default;
        for (int i = 0; i < info.characterCount && i < info.characterInfo.Length; i++)
        {
            TMP_CharacterInfo character = info.characterInfo[i];
            if (!character.isVisible || character.materialReferenceIndex >= info.meshInfo.Length) continue;
            Vector3[] vertices = info.meshInfo[character.materialReferenceIndex].vertices;
            if (vertices == null || character.vertexIndex + 3 >= vertices.Length) continue;
            for (int vertex = character.vertexIndex; vertex < character.vertexIndex + 4; vertex++)
            {
                Vector3 point = label.transform.TransformPoint(vertices[vertex]);
                if (!any) { any = true; bounds = new Bounds(point, Vector3.zero); }
                else bounds.Encapsulate(point);
            }
        }
        Require(any, "Visible dual HP contains no generated glyphs.");
        return bounds;
    }

    private static void ValidatePauseHitTarget()
    {
        GamePauseUI pause = GamePauseUI.Instance;
        Button button = typeof(GamePauseUI).GetField("pauseButton", Private)?.GetValue(pause) as Button;
        EventSystem events = EventSystem.current;
        Require(button != null && button.isActiveAndEnabled && button.IsInteractable() && events != null &&
            events.isActiveAndEnabled && events.currentInputModule != null && events.currentInputModule.isActiveAndEnabled,
            "The reloaded Game UI input target is not ready.");
        Canvas canvas = button.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        RectTransform rect = (RectTransform)button.transform;
        Vector2 position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
        var hits = new List<RaycastResult>();
        events.RaycastAll(new PointerEventData(events) { position = position }, hits);
        Require(hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == button.gameObject,
            "A different object blocks the reloaded Game pause button.");
    }

    private static void TriggerReload(GridManager grid)
    {
        MethodInfo load = typeof(GamePauseUI).GetMethod("LoadScene", Private);
        Require(load != null, "The actual pause/replay LoadScene method is missing.");
        previousGridId = grid.GetEntityId().ToString();
        stableGridId = null;
        waitingForReload = true;
        reloadStarted = EditorApplication.timeSinceStartup;
        pendingOwnerSequence = DOTween.Sequence();
        pendingOwnerSequence.SetTarget(grid);
        pendingOwnerSequence.AppendInterval(60f);
        pendingOwnerSequence.OnComplete(() => unexpectedCompletions++);
        pendingOwnerSequence.OnKill(() => ownedKills++);
        report.AppendLine("RELOAD START " + (completedReloads + 1) + " oldGrid=" + previousGridId + " frame=" + Time.frameCount);
        File.WriteAllText(Result, report.ToString());
        testPausedController = GamePauseUI.Instance;
        testPausedController.Pause();
        Require(Time.timeScale <= 0f, "Pause callback did not respond before replay.");
        load.Invoke(testPausedController, new object[] { "Game" });
        testPausedController = null;
    }

    private static void VerifySavedState()
    {
        Require(HeartManager.Hearts == initialHearts, "The replay test changed the player's heart count.");
        Require(PlayerPrefs.GetInt("CurrentLevel", 1) == savedLevel && PlayerPrefs.GetInt("PlayerMoney", 0) == savedMoney,
            "Saved level or money changed during the replay check.");
        foreach (string key in StringSaveKeys)
            Require(PlayerPrefs.GetString(key, "") == savedStrings[key], "Saved player state changed: " + key);
        report.AppendLine("PASS: saved level, money, hearts, daily missions and achievement values are unchanged; replay stays on loaded level " + expectedLevel + ".");
    }

    private static void CheckReloadTimeout()
    {
        if (waitingForReload && EditorApplication.timeSinceStartup - reloadStarted > 20)
            throw new TimeoutException("The requested real Game reload did not produce a new initialized grid within 20 seconds.");
    }

    private static void Status(string status)
    {
        if (EditorApplication.timeSinceStartup - lastStatusWrite < 1) return;
        lastStatusWrite = EditorApplication.timeSinceStartup;
        File.WriteAllText(Result, report + status + "\n");
    }

    private static void Finish()
    {
        RestoreTemporaryState();
        File.WriteAllText(Result, report.ToString());
        File.Delete(Request);
        active = false;
    }

    private static void RestoreTemporaryState()
    {
        if (pendingOwnerSequence != null && pendingOwnerSequence.IsActive()) pendingOwnerSequence.Kill(false);
        pendingOwnerSequence = null;
        if (testPausedController != null) testPausedController.Resume();
        testPausedController = null;
        Application.runInBackground = originalBackground;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
