#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Connects the existing mission reward artwork without rebuilding or moving the cards.</summary>
[InitializeOnLoad]
public static class DailyMissionRewardSetup
{
    private const string ScenePath = "Assets/Scenes/Home.unity";
    private const string Request = "Temp/DailyMissionRewardSetup.request";
    private const string Result = "Temp/DailyMissionRewardSetup.result";
    private const string Revision = "daily-reward-v1";
    private static bool running;

    static DailyMissionRewardSetup() => EditorApplication.update += Poll;

    private static void Poll()
    {
        if (running || !File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        running = true;
        try
        {
            if (File.ReadAllText(Request).Trim() == "apply-" + Revision) Apply();
            else Audit();
        }
        catch (Exception exception) { File.WriteAllText(Result, "FAIL\n" + exception); Debug.LogException(exception); }
        finally { File.Delete(Request); running = false; }
    }

    [MenuItem("Tools/Diagnostics/Audit Daily Mission Reward Controls")]
    public static void Audit()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool preview = !scene.IsValid() || !scene.isLoaded;
        if (preview) scene = EditorSceneManager.OpenPreviewScene(ScenePath);
        try
        {
            var report = new StringBuilder("VERSION=" + Revision + "\n");
            report.AppendLine("Home preview=" + preview + " dirty=" + scene.isDirty);
            foreach (Transform card in Cards(Home(scene))) Dump(card, report);
            File.WriteAllText(Result, report.ToString());
        }
        finally { if (preview) EditorSceneManager.ClosePreviewScene(scene); }
    }

    [MenuItem("Tools/Home UI/Use Mission Reward Icons")]
    public static void Apply()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Prepare mission rewards outside Play Mode.");
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            HomeExperienceUI home = Home(scene);
            Transform[] cards = Cards(home);
            Require(cards.Length == 6, "Expected six authored daily mission cards.");
            var geometry = new Dictionary<RectTransform, Layout>();
            foreach (Transform card in cards)
                foreach (RectTransform rect in card.GetComponentsInChildren<RectTransform>(true))
                    if (!IsLegacyClaimControl(rect, card)) geometry.Add(rect, new Layout(rect));
            var claim = (UnityAction<int>)Delegate.CreateDelegate(typeof(UnityAction<int>), home, "ClaimMissionReward");
            var report = new StringBuilder("VERSION=" + Revision + "\n");
            foreach (Transform card in cards)
            {
                TryMission(card.name, out DailyMissionType mission);
                Image icon = Child<Image>(card, " Reward Coin Icon");
                Image frame = Child<Image>(card, " Reward Frame");
                TMP_Text amount = Child<TMP_Text>(card, " Reward Amount");
                Require(icon != null && frame != null && amount != null, "Missing authored reward artwork on " + card.name);
                Undo.RegisterFullObjectHierarchyUndo(card.gameObject, "Use mission reward icon");
                foreach (Transform legacy in card.GetComponentsInChildren<Transform>(true)
                    .Where(node => node.name == card.name + " Claim Button").ToArray())
                    Undo.DestroyObjectImmediate(legacy.gameObject);

                Button button = icon.GetComponent<Button>();
                if (button == null) button = Undo.AddComponent<Button>(icon.gameObject);
                for (int index = button.onClick.GetPersistentEventCount() - 1; index >= 0; index--)
                    UnityEventTools.RemovePersistentListener(button.onClick, index);
                button.onClick.RemoveAllListeners();
                UnityEventTools.AddIntPersistentListener(button.onClick, claim, (int)mission);
                button.enabled = true;
                button.targetGraphic = icon;
                button.transition = Selectable.Transition.None;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                // The saved sample has no completed progress. Runtime card refresh
                // enables this same icon when its mission is ready to claim.
                button.interactable = false;
                icon.gameObject.SetActive(true);
                icon.enabled = true;
                icon.raycastTarget = true;
                frame.gameObject.SetActive(true);
                frame.raycastTarget = false;
                amount.gameObject.SetActive(true);
                amount.raycastTarget = false;
                EditorUtility.SetDirty(button);
                EditorUtility.SetDirty(icon);
                EditorUtility.SetDirty(frame);
                EditorUtility.SetDirty(amount);
                Dump(card, report);
            }
            foreach (var item in geometry)
                Require(item.Key != null && item.Value.Matches(item.Key), "Mission layout changed: " + (item.Key == null ? "missing object" : item.Key.name));
            EditorSceneManager.MarkSceneDirty(scene);
            Require(EditorSceneManager.SaveScene(scene), "Could not save Home mission reward controls.");
            report.AppendLine("PASS six icon reward controls, persistent mission callbacks, legacy claim controls removed; all surviving card rectangles unchanged. Home saved.");
            File.WriteAllText(Result, report.ToString());
        }
        finally { if (openedHere) EditorSceneManager.CloseScene(scene, true); }
    }

    private static HomeExperienceUI Home(Scene scene) => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<HomeExperienceUI>(true)).Single();

    private static Transform[] Cards(HomeExperienceUI home) => home.GetComponentsInChildren<Transform>(true)
        .Where(node => TryMission(node.name, out _)).ToArray();

    private static bool TryMission(string name, out DailyMissionType mission)
    {
        MethodInfo binding = typeof(HomeExperienceUI).GetMethod("TrySceneMission", BindingFlags.Static | BindingFlags.NonPublic);
        Require(binding != null, "The Home mission name binding is missing.");
        object[] arguments = { name, default(DailyMissionType) };
        bool found = (bool)binding.Invoke(null, arguments);
        mission = (DailyMissionType)arguments[1];
        return found;
    }

    private static T Child<T>(Transform card, string suffix) where T : Component => card.GetComponentsInChildren<T>(true)
        .SingleOrDefault(component => component.name == card.name + suffix);

    private static bool IsLegacyClaimControl(Transform node, Transform card)
    {
        for (Transform ancestor = node; ancestor != null && ancestor != card; ancestor = ancestor.parent)
            if (ancestor.name == card.name + " Claim Button") return true;
        return false;
    }

    private static void Dump(Transform card, StringBuilder report)
    {
        TryMission(card.name, out DailyMissionType mission);
        report.AppendLine("CARD " + card.name + " mission=" + (int)mission);
        foreach (Transform node in card.GetComponentsInChildren<Transform>(true).Where(node =>
            node.name.EndsWith(" Claim Button", StringComparison.Ordinal) || node.name.EndsWith(" Reward Coin Icon", StringComparison.Ordinal) ||
            node.name.EndsWith(" Reward Frame", StringComparison.Ordinal) || node.name.EndsWith(" Reward Amount", StringComparison.Ordinal)))
        {
            string line = "  " + node.name + " active=" + node.gameObject.activeSelf;
            if (node is RectTransform rect) line += " anchors=" + rect.anchorMin + "/" + rect.anchorMax + " pos=" + rect.anchoredPosition + " size=" + rect.sizeDelta;
            Button button = node.GetComponent<Button>();
            if (button != null)
            {
                line += " BUTTON interactable=" + button.interactable + " transition=" + button.transition;
                for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                    line += " callback=" + button.onClick.GetPersistentMethodName(i);
            }
            report.AppendLine(line);
        }
    }

    private readonly struct Layout
    {
        private readonly Vector2 min, max, position, size, pivot;
        private readonly Vector3 scale;
        private readonly Quaternion rotation;
        public Layout(RectTransform rect)
        {
            min = rect.anchorMin; max = rect.anchorMax; position = rect.anchoredPosition;
            size = rect.sizeDelta; pivot = rect.pivot; scale = rect.localScale; rotation = rect.localRotation;
        }
        public bool Matches(RectTransform rect) => min == rect.anchorMin && max == rect.anchorMax &&
            position == rect.anchoredPosition && size == rect.sizeDelta && pivot == rect.pivot && scale == rect.localScale && rotation == rect.localRotation;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
