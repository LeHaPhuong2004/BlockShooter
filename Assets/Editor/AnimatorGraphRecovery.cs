#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>Rebuilds the Animator window's transient graph without editing animation assets.</summary>
[InitializeOnLoad]
public static class AnimatorGraphRecovery
{
    private const string RequestPath = "Assets/Editor/AnimatorGraphRecovery.request";
    private const string ReportPath = "Temp/AnimatorGraphRecovery.result";

    static AnimatorGraphRecovery() => EditorApplication.delayCall += RecoverIfRequested;

    private static void RecoverIfRequested()
    {
        if (!File.Exists(RequestPath)) return;
        try
        {
            Recover();
            File.Delete(RequestPath);
            File.Delete(RequestPath + ".meta");
        }
        catch (Exception exception)
        {
            File.WriteAllText(ReportPath, exception.ToString());
            Debug.LogException(exception);
        }
    }

    [MenuItem("Tools/Diagnostics/Recover Animator Graph")]
    public static void Recover()
    {
        var report = new StringBuilder();
        int closed = 0;
        foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
        {
            if (window == null || window.GetType().FullName != "UnityEditor.Graphs.AnimatorControllerTool") continue;
            window.Close();
            closed++;
        }
        report.AppendLine("Closed Animator windows: " + closed);
        int controllers = 0;
        int issues = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:AnimatorController", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null) continue;
            controllers++;
            var visited = new HashSet<AnimatorStateMachine>();
            int before = issues;
            foreach (var layer in controller.layers)
                InspectStateMachine(layer.stateMachine, path, visited, report, ref issues);
            if (issues == before) report.AppendLine("OK: " + path);
        }
        report.AppendLine("Controllers checked: " + controllers + "; reference issues: " + issues);
        report.AppendLine("Animation assets were not modified. Reopen Window > Animation > Animator to rebuild the view.");
        Directory.CreateDirectory("Temp");
        File.WriteAllText(ReportPath, report.ToString());
        Debug.Log(report.ToString());
    }

    private static void InspectStateMachine(AnimatorStateMachine machine, string path,
        HashSet<AnimatorStateMachine> visited, StringBuilder report, ref int issues)
    {
        if (machine == null)
        {
            report.AppendLine("Missing state machine: " + path);
            issues++;
            return;
        }
        if (!visited.Add(machine)) return;
        foreach (var transition in machine.anyStateTransitions) InspectTransition(transition, path, report, ref issues);
        foreach (var transition in machine.entryTransitions) InspectTransition(transition, path, report, ref issues);
        foreach (var child in machine.states)
        {
            if (child.state == null)
            {
                report.AppendLine("Missing state: " + path + " / " + machine.name);
                issues++;
                continue;
            }
            foreach (var transition in child.state.transitions) InspectTransition(transition, path, report, ref issues);
        }
        foreach (var child in machine.stateMachines)
        {
            if (child.stateMachine != null)
                foreach (var transition in machine.GetStateMachineTransitions(child.stateMachine))
                    InspectTransition(transition, path, report, ref issues);
            InspectStateMachine(child.stateMachine, path, visited, report, ref issues);
        }
    }

    private static void InspectTransition(AnimatorTransitionBase transition, string path, StringBuilder report, ref int issues)
    {
        if (transition != null && (transition.isExit || transition.destinationState != null || transition.destinationStateMachine != null)) return;
        issues++;
        report.AppendLine("Missing transition or destination: " + path);
    }
}
#endif
