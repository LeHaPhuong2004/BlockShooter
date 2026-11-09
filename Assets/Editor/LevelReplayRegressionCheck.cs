#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>Checks replay progression without loading scenes or changing saved progress.</summary>
[InitializeOnLoad]
public static class LevelReplayRegressionCheck
{
    private const string RequestPath = "Assets/Editor/LevelReplayRegressionCheck.request";
    private const string ReportPath = "Temp/LevelReplayRegressionCheck.result";
    private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;
    private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

    static LevelReplayRegressionCheck() => EditorApplication.delayCall += RunIfRequested;

    private static void RunIfRequested()
    {
        if (!File.Exists(RequestPath)) return;
        try
        {
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

    [MenuItem("Tools/Diagnostics/Check Current Level Replay")]
    public static void Run()
    {
        var type = typeof(LevelManager);
        var session = type.GetField("sessionLevel", PrivateStatic);
        var loaded = type.GetField("loadedLevel", PrivateStatic);
        var initialize = type.GetMethod("InitializeLevelProgress", PrivateInstance);
        var reset = type.GetMethod("ResetSession", PrivateStatic);
        var originalInstance = LevelManager.Instance;
        object originalSession = session.GetValue(null);
        object originalLoaded = loaded.GetValue(null);
        var root = new GameObject("Level Replay Regression Check") { hideFlags = HideFlags.HideAndDontSave };
        root.SetActive(false); // Keep Awake and DontDestroyOnLoad out of this isolated check.
        try
        {
            var manager = root.AddComponent<LevelManager>();
            type.GetField("testLevel", PrivateInstance).SetValue(manager, 1);
            session.SetValue(null, 0);
            loaded.SetValue(null, 0);
            initialize.Invoke(manager, null);
            Expect(manager.CurrentLevel == 1, "Fresh Editor Play should start at testLevel.");

            for (int i = 1; i < 5; i++) manager.CompleteLevel();
            loaded.SetValue(null, 5);
            manager.PrepareRestartCurrentLevel();
            initialize.Invoke(manager, null);
            Expect(manager.CurrentLevel == 5, "Replay after progressing 1-5 must stay at 5.");

            manager.CompleteLevel();
            Expect(manager.CurrentLevel == 6, "Completing level 5 should offer level 6.");
            manager.PrepareRestartCurrentLevel();
            Expect(manager.CurrentLevel == 5, "Replay from the win panel must replay 5, not 6.");

            var reloaded = root.AddComponent<LevelManager>();
            type.GetField("testLevel", PrivateInstance).SetValue(reloaded, 1);
            initialize.Invoke(reloaded, null);
            Expect(reloaded.CurrentLevel == 5, "A recreated scene manager must retain level 5.");

            reset.Invoke(null, null);
            initialize.Invoke(reloaded, null);
            Expect(reloaded.CurrentLevel == 1, "A new Editor Play session should reset to testLevel.");
            File.WriteAllText(ReportPath, "PASS: 1-5 replay stays at 5; win-panel replay stays at 5; " +
                "scene recreation retains 5; fresh Editor Play resets to testLevel; saved progress unchanged.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            LevelManager.Instance = originalInstance;
            session.SetValue(null, originalSession);
            loaded.SetValue(null, originalLoaded);
        }
    }

    private static void Expect(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
