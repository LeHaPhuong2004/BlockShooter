#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Authors the two footer reward counts without rebuilding the Home UI.</summary>
[InitializeOnLoad]
public static class HomeRewardBadgesSetup
{
    private const string ScenePath = "Assets/Scenes/Home.unity";
    private const string RequestPath = "Temp/HomeRewardBadgesSetup.request";
    private const string ReportPath = "Temp/HomeRewardBadgesSetup.result";
    private const string OutputDirectory = "output/achievements";
    private static bool running;

    static HomeRewardBadgesSetup() => EditorApplication.update += ProcessRequest;

    private static void ProcessRequest()
    {
        if (running || !File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        running = true;
        try { Prepare(); }
        catch (Exception exception)
        {
            Directory.CreateDirectory("Temp");
            File.WriteAllText(ReportPath, exception.ToString());
            Debug.LogException(exception);
        }
        finally { File.Delete(RequestPath); running = false; }
    }

    [MenuItem("Tools/Home UI/Prepare Reward Notification Counts")]
    public static void Prepare()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Prepare footer reward counts outside Play Mode after compilation.");
        Directory.CreateDirectory("Temp");
        Directory.CreateDirectory(OutputDirectory);
        string backup = OutputDirectory + "/Home-before-reward-count-badges.unity.backup";
        if (!File.Exists(backup)) File.Copy(ScenePath, backup);
        var scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            var nav = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Single(node => node.name == "Bottom Navigation");
            int[] originals = ObjectIds(scene);
            PrepareBadges(nav);
            var retained = new HashSet<int>(ObjectIds(scene));
            Require(originals.All(retained.Contains), "A pre-existing Home object was removed.");
            int[] firstPass = ObjectIds(scene);
            PrepareBadges(nav);
            Require(firstPass.SequenceEqual(ObjectIds(scene)), "Repeated badge setup created duplicate objects.");
            var mission = FindBadge(nav, "MISSIONS", "Mission Notification");
            var achievement = FindBadge(nav, "ACHIEVEMENTS", "Achievement Notification");
            foreach (var badge in new[] { mission, achievement })
            {
                Require(!badge.activeSelf && string.IsNullOrEmpty(badge.GetComponentInChildren<Text>(true).text),
                    "A zero-count footer badge should start hidden with an empty count.");
                Require(badge.GetComponentsInChildren<Graphic>(true).All(graphic => !graphic.raycastTarget),
                    "A footer reward badge blocks navigation clicks.");
            }
            var report = new StringBuilder();
            report.AppendLine("PASS: original Home objects retained; one native reward badge per Missions/Achievements tab; repeated setup creates no duplicates.");
            report.AppendLine(ValidateBadgeDisplay());
            report.AppendLine(ValidateDailyClaimableCount());
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save footer reward badges in Home.unity.");
            report.AppendLine("PASS: Home.unity saved; native badge artwork, fonts and anchors preserved, both zero-count badges hidden.");
            report.AppendLine("No saved progress, PlayerPrefs, player wallet, claim action, or service was changed.");
            File.WriteAllText(ReportPath, report.ToString());
            File.WriteAllText(OutputDirectory + "/reward-badge-validation.txt", report.ToString());
            Debug.Log(report.ToString());
        }
        finally { if (openedHere) EditorSceneManager.CloseScene(scene, true); }
    }

    private static void PrepareBadges(Transform nav)
    {
        var mission = FindBadge(nav, "MISSIONS", "Mission Notification");
        var achievementButton = nav.GetComponentsInChildren<Button>(true).Single(button => button.name == "ACHIEVEMENTS");
        var achievements = achievementButton.GetComponentsInChildren<Transform>(true)
            .Where(node => node.name == "Achievement Notification").ToArray();
        Require(achievements.Length <= 1, "The Achievements tab already has duplicate reward badges.");
        GameObject achievement;
        if (achievements.Length == 0)
        {
            achievement = UnityEngine.Object.Instantiate(mission, achievementButton.transform, false);
            achievement.name = "Achievement Notification";
            Undo.RegisterCreatedObjectUndo(achievement, "Add achievement reward count");
        }
        else achievement = achievements[0].gameObject;
        foreach (var badge in new[] { mission, achievement })
        {
            var label = badge.GetComponentInChildren<Text>(true);
            Require(label != null, "A native footer badge has no count label: " + badge.name);
            Undo.RegisterFullObjectHierarchyUndo(badge, "Prepare reward count badge");
            label.text = string.Empty;
            foreach (var graphic in badge.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
                EditorUtility.SetDirty(graphic);
            }
            badge.SetActive(false);
            EditorUtility.SetDirty(badge);
        }
    }

    public static string ValidateBadgeDisplay()
    {
        var method = typeof(HomeExperienceUI).GetMethod("SetRewardBadgeCount", BindingFlags.Static | BindingFlags.NonPublic);
        Require(method != null, "SetRewardBadgeCount is missing.");
        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var badge = new GameObject("Isolated Reward Count", typeof(RectTransform));
            badge.SetActive(false);
            SceneManager.MoveGameObjectToScene(badge, preview);
            var label = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            label.transform.SetParent(badge.transform, false);
            foreach (int count in new[] { 0, 3, 1, 0 })
            {
                method.Invoke(null, new object[] { badge, count });
                Require(badge.activeSelf == (count > 0) && label.text == (count > 0 ? count.ToString() : string.Empty),
                    "Reward count " + count + " has the wrong text or visibility.");
            }
            return "PASS: actual badge renderer hides zero, displays 3, updates to 1 after a reward, and hides again at zero.";
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }

    private static string ValidateDailyClaimableCount()
    {
        Require(!Application.isPlaying, "Isolate daily mission counts outside Play Mode.");
        var dataField = typeof(DailyMissionManager).GetField("data", BindingFlags.Static | BindingFlags.NonPublic);
        Require(dataField != null, "Daily mission data is unavailable for isolated count checks.");
        object previous = dataField.GetValue(null);
        try
        {
            object isolated = Activator.CreateInstance(dataField.FieldType, true);
            dataField.SetValue(null, isolated);
            Require(DailyMissionManager.ClaimableCount == 0, "Empty daily progress should have no notification.");
            foreach (var entry in new[]
            {
                new KeyValuePair<string, object>("blocks", 1000),
                new KeyValuePair<string, object>("games", 3),
                new KeyValuePair<string, object>("boosters", 5),
                new KeyValuePair<string, object>("wins", 5),
                new KeyValuePair<string, object>("winStreakCompleted", true)
            }) dataField.FieldType.GetField(entry.Key).SetValue(isolated, entry.Value);
            int expected = Enum.GetValues(typeof(DailyMissionType)).Length;
            Require(expected == 6 && DailyMissionManager.ClaimableCount == expected,
                "Daily notification must count all six completed unclaimed missions.");
            int claimedMask = Enum.GetValues(typeof(DailyMissionType)).Cast<DailyMissionType>()
                .Aggregate(0, (mask, type) => mask | (1 << (int)type));
            dataField.FieldType.GetField("claimedMask").SetValue(isolated, claimedMask);
            Require(DailyMissionManager.ClaimableCount == 0, "Claimed missions must be removed from the notification count.");
            return "PASS: isolated daily mission counts are 0 before completion, 6 when complete, and 0 after all rewards are claimed.";
        }
        finally { dataField.SetValue(null, previous); }
    }

    private static GameObject FindBadge(Transform nav, string buttonName, string badgeName)
    {
        var button = nav.GetComponentsInChildren<Button>(true).Single(candidate => candidate.name == buttonName);
        return button.GetComponentsInChildren<Transform>(true).Single(node => node.name == badgeName).gameObject;
    }

    private static int[] ObjectIds(Scene scene) => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
        .Select(node => node.gameObject.GetInstanceID()).OrderBy(id => id).ToArray();

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Reward badges: " + message);
    }
}
#endif
