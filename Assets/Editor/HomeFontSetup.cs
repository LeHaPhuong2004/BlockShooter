#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class HomeFontSetup
{
    private const string RequestPath = "Assets/Editor/HomeFontSetup.request";
    private static bool livePreviewApplied;

    static HomeFontSetup() => EditorApplication.delayCall += ApplyIfRequested;

    private static void ApplyIfRequested()
    {
        if (!File.Exists(RequestPath)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            if (EditorApplication.isPlaying && !livePreviewApplied)
            {
                Scene home = SceneManager.GetSceneByPath("Assets/Scenes/Home.unity");
                if (home.IsValid() && home.isLoaded)
                {
                    int count = HomeUIFont.ApplyToScene(home);
                    File.WriteAllText("Temp/HomeFontSetup.result", "Live Home updated: " + count + " labels. Scene save pending Stop Play Mode.\n" + CheckMissionTextLayout());
                    ScreenCapture.CaptureScreenshot("Temp/HomeMissionsAutoSize.png");
                    livePreviewApplied = true;
                }
            }
            EditorApplication.delayCall += ApplyIfRequested;
            return;
        }
        try
        {
            Apply();
            File.Delete(RequestPath);
            File.Delete(RequestPath + ".meta");
        }
        catch (Exception exception)
        {
            File.WriteAllText("Temp/HomeFontSetup.result", exception.ToString());
            Debug.LogException(exception);
        }
    }

    [MenuItem("Tools/Home UI/Apply Block Blast Font")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before saving the Home font.");
        const string path = "Assets/Scenes/Home.unity";
        Scene scene = SceneManager.GetSceneByPath(path);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            int count = HomeUIFont.ApplyToScene(scene);
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var text in root.GetComponentsInChildren<UnityEngine.UI.Text>(true)) RecordFontOverride(text);
                foreach (var text in root.GetComponentsInChildren<TMPro.TMP_Text>(true)) RecordFontOverride(text);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the Home font.");
            string result = "OK: Applied blockblast_font to " + count + " Home text components; source font: " + HomeUIFont.LoadSourceFont().name
                + "\n" + CheckMissionTextLayout();
            File.WriteAllText("Temp/HomeFontSetup.result", result);
            Debug.Log(result);
        }
        finally
        {
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void RecordFontOverride(Component text)
    {
        EditorUtility.SetDirty(text);
        if (PrefabUtility.IsPartOfPrefabInstance(text))
            PrefabUtility.RecordPrefabInstancePropertyModifications(text);
    }

    private static string CheckMissionTextLayout()
    {
        string[] samples = { "NHIỆM VỤ HẰNG NGÀY", "PHẦN THƯỞNG\nĐĂNG NHẬP HÀNG NGÀY", "Phá 1.000 khối", "Phá tổng cộng 1.000 khối trong ngày", "1000/1000", "80" };
        var sizes = new[] { new Vector2(620, 80), new Vector2(455, 88), new Vector2(351, 58), new Vector2(351, 42), new Vector2(336, 44), new Vector2(92, 48) };
        int[] maxima = { 44, 32, 44, 26, 32, 36 };
        int[] minima = { 30, 22, 32, 20, 26, 28 };
        var canvas = new GameObject("TMP Auto Size Check", typeof(Canvas));
        canvas.hideFlags = HideFlags.HideAndDontSave;
        int count = 0;
        var report = new System.Text.StringBuilder();
        try
        {
            for (int i = 0; i < samples.Length; i++)
            {
                var go = new GameObject("Label", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
                go.transform.SetParent(canvas.transform, false);
                var label = go.GetComponent<TMPro.TextMeshProUGUI>();
                label.rectTransform.sizeDelta = sizes[i];
                label.text = samples[i];
                HomeMissionTypography.Configure(label, maxima[i], minima[i], TextAnchor.MiddleCenter, i == 1);
                label.ForceMeshUpdate(true, true);
                if (label.textInfo.characterCount == 0 || label.isTextOverflowing || (i != 1 && label.textInfo.lineCount > 1))
                    throw new InvalidOperationException("TMP Auto Size failed for: " + samples[i] + " (font size " + label.fontSize + ")");
                if (label.GetComponent<HomeTextFit>() != null)
                    throw new InvalidOperationException("Mission text still has custom font fitting.");
                report.AppendLine(samples[i].Replace("\n", " / ") + ": " + label.fontSize.ToString("0.0"));
                count++;
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(canvas); }
        return "PASS: " + count + " fixed TMP text boxes use native Auto Size without overflow.\n" + report;
    }
}
#endif
