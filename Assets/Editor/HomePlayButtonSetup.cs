#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class HomePlayButtonSetup
{
    private const string ScenePath = "Assets/Scenes/Home.unity";
    private const string RequestPath = "Assets/Editor/HomePlayButtonSetup.request";
    private const string ReportPath = "Temp/HomePlayButtonSetup.result";

    static HomePlayButtonSetup()
    {
        EditorApplication.update += ProcessRequest;
    }

    [MenuItem("Tools/Home UI/Prepare Play Button Text Objects")]
    public static void PrepareFromMenu()
    {
        PrepareScene();
    }

    private static void ProcessRequest()
    {
        if (!File.Exists(RequestPath)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;

        try
        {
            PrepareScene();
            Debug.Log("Play button text objects were saved into the Home scene.");
            File.WriteAllText(ReportPath, "PASS: Play button tag and text objects were prepared and saved.");
        }
        catch (Exception exception)
        {
            File.WriteAllText(ReportPath, exception.ToString());
            Debug.LogException(exception);
        }
        finally
        {
            if (File.Exists(RequestPath)) File.Delete(RequestPath);
            if (File.Exists(RequestPath + ".meta")) File.Delete(RequestPath + ".meta");
        }
    }

    private static void PrepareScene()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            HomeExperienceUI home = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                home = root.GetComponentInChildren<HomeExperienceUI>(true);
                if (home != null) break;
            }
            if (home == null) throw new InvalidOperationException("HomeExperienceUI was not found in Home.unity.");

            home.PreparePlayButtonObjectsForEditor();
            EditorUtility.SetDirty(home);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Unity could not save the prepared Home scene.");
        }
        finally
        {
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
        }
    }
}
#endif
