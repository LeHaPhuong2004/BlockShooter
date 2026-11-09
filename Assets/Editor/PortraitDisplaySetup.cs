#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Stores the portrait-only build configuration without editing scenes.</summary>
[InitializeOnLoad]
public static class PortraitDisplaySetup
{
    private const string RequestPath = "Temp/PortraitDisplaySetup.request";
    private const string ReportPath = "Temp/PortraitDisplaySetup.result";
    private static bool running;

    static PortraitDisplaySetup() => EditorApplication.update += ProcessRequest;

    private static void ProcessRequest()
    {
        if (running || !File.Exists(RequestPath) || EditorApplication.isCompiling ||
            EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        running = true;
        try { Run(); }
        catch (Exception exception)
        {
            Directory.CreateDirectory("Temp");
            File.WriteAllText(ReportPath, exception.ToString());
            Debug.LogException(exception);
        }
        finally { File.Delete(RequestPath); running = false; }
    }

    [MenuItem("Tools/Game/Apply Portrait Display Settings")]
    public static void Run()
    {
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Apply portrait display settings outside Play Mode after compilation.");

        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToPortrait = true;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false;
        PlayerSettings.allowedAutorotateToLandscapeRight = false;
        PlayerSettings.iOS.requiresFullScreen = true;
        PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
        PlayerSettings.defaultScreenWidth = (int)PortraitDisplay.ReferenceScreenSize.x;
        PlayerSettings.defaultScreenHeight = (int)PortraitDisplay.ReferenceScreenSize.y;
        AssetDatabase.SaveAssets();

        if (PlayerSettings.defaultInterfaceOrientation != UIOrientation.Portrait ||
            !PlayerSettings.allowedAutorotateToPortrait || PlayerSettings.allowedAutorotateToPortraitUpsideDown ||
            PlayerSettings.allowedAutorotateToLandscapeLeft || PlayerSettings.allowedAutorotateToLandscapeRight ||
            !PlayerSettings.iOS.requiresFullScreen ||
            PlayerSettings.iOS.targetDevice != iOSTargetDevice.iPhoneAndiPad ||
            PlayerSettings.defaultScreenWidth != (int)PortraitDisplay.ReferenceScreenSize.x ||
            PlayerSettings.defaultScreenHeight != (int)PortraitDisplay.ReferenceScreenSize.y)
            throw new InvalidOperationException("Portrait display settings failed validation.");

        const string report = "PASS: saved portrait-only orientation, landscape/upside-down autorotation disabled, " +
            "iPhone and iPad supported, iOS fullscreen required, portrait reference 1170x2532. No scenes were changed.";
        Directory.CreateDirectory("Temp");
        File.WriteAllText(ReportPath, report);
        Debug.Log(report);
    }
}
#endif
