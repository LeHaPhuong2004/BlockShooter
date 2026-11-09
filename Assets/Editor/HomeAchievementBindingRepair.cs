#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>Rebuilds stale Inspector targets without changing authored scene objects.</summary>
[InitializeOnLoad]
public static class HomeAchievementBindingRepair
{
    private const string RequestPath = "Temp/HomeAchievementBindingRepair.request";
    private const string ReportPath = "Temp/HomeAchievementBindingRepair.result";

    static HomeAchievementBindingRepair() => EditorApplication.update += ProcessRequest;

    private static void ProcessRequest()
    {
        if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(RequestPath);
        try
        {
            var inspectorType = typeof(Editor).Assembly.GetType("UnityEditor.InspectorWindow");
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var trackerProperty = inspectorType?.GetProperty("tracker", flags);
            var lockedProperty = inspectorType?.GetProperty("isLocked", flags);
            int repaired = 0;
            if (inspectorType != null)
                foreach (var window in Resources.FindObjectsOfTypeAll(inspectorType).OfType<EditorWindow>())
                {
                    var tracker = trackerProperty?.GetValue(window) as ActiveEditorTracker;
                    if (tracker == null) continue;
                    if (tracker.activeEditors.Any(editor => editor == null || editor.target == null))
                    {
                        if (lockedProperty != null && lockedProperty.CanWrite) lockedProperty.SetValue(window, false);
                        repaired++;
                    }
                    window.Repaint();
                }
            Selection.objects = Array.Empty<UnityEngine.Object>();
            ActiveEditorTracker.sharedTracker.ForceRebuild();
            if (inspectorType != null)
                foreach (var window in Resources.FindObjectsOfTypeAll(inspectorType).OfType<EditorWindow>())
                {
                    var tracker = trackerProperty?.GetValue(window) as ActiveEditorTracker;
                    tracker?.ForceRebuild();
                    window.Repaint();
                }
            File.WriteAllText(ReportPath, "PASS: cleared stale selection and rebuilt Inspector targets; invalid trackers=" +
                repaired + ". No scene objects, assets or saved progress were changed.");
        }
        catch (Exception exception)
        {
            File.WriteAllText(ReportPath, exception.ToString());
            Debug.LogException(exception);
        }
    }
}
#endif
