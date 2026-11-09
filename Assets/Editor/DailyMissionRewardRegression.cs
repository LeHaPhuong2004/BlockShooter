#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Checks coin-based mission claims without playing, awarding gold or saving scenes.</summary>
[InitializeOnLoad]
public static class DailyMissionRewardRegression
{
    private const string Request = "Temp/DailyMissionRewardRegression.request";
    private const string Result = "Temp/DailyMissionRewardRegression.result";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo MissionData = typeof(DailyMissionManager).GetField("data", BindingFlags.Static | BindingFlags.NonPublic);
    private static bool running;

    private sealed class RewardView
    {
        public Transform row;
        public DailyMissionType mission;
        public DailyMissionCardUI ui;
        public Button button;
        public GameObject frame;
        public GameObject amount;
    }

    static DailyMissionRewardRegression() => EditorApplication.update += Poll;

    private static void Poll()
    {
        if (running || !File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        running = true;
        try { Run(); }
        catch (Exception exception) { File.WriteAllText(Result, "FAIL\n" + exception); Debug.LogException(exception); }
        finally { File.Delete(Request); running = false; }
    }

    [MenuItem("Tools/Diagnostics/Check Daily Mission Coin Rewards")]
    public static void Run()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Run the isolated mission UI check outside Play Mode.");
        object previousData = MissionData.GetValue(null);
        AudioManager previousAudio = AudioManager.Instance;
        GameMaster previousMaster = GameMaster.staticScript;
        Scene scene = default;
        var report = new StringBuilder();
        try
        {
            AudioManager.Instance = null;
            GameMaster.staticScript = null;
            InjectProgress(false, false);
            scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Home.unity");
            Transform[] nodes = Nodes(scene);
            HomeExperienceUI home = nodes.Select(node => node.GetComponent<HomeExperienceUI>()).Single(item => item != null);
            Transform page = nodes.Single(node => node.name == "Daily Missions Page");
            Button[] coins = page.GetComponentsInChildren<Button>(true).Where(button => button.name.EndsWith(" Reward Coin Icon", StringComparison.Ordinal)).ToArray();
            Require(coins.Length == 6, "Expected six authored daily-mission reward coin buttons, found " + coins.Length);
            var views = new List<RewardView>();
            foreach (Button coin in coins)
            {
                Transform row = coin.transform.parent;
                DailyMissionType mission = ExpectedMission(row.name);
                var view = new RewardView
                {
                    row = row,
                    mission = mission,
                    ui = row.GetComponent<DailyMissionCardUI>(),
                    button = coin,
                    frame = SuffixNode(row, " Reward Frame").gameObject,
                    amount = SuffixNode(row, " Reward Amount").gameObject
                };
                Require(view.ui != null, row.name + " has no authored DailyMissionCardUI.");
                Require(!row.GetComponentsInChildren<Transform>(true).Any(node => node.name.EndsWith(" Claim Button", StringComparison.Ordinal)), row.name + " still contains the old text claim button.");
                Require(!row.GetComponentsInChildren<TMP_Text>(true).Any(label => IsClaimText(label.text)) && !row.GetComponentsInChildren<Text>(true).Any(label => IsClaimText(label.text)), row.name + " still shows NHẬN/ĐÃ NHẬN claim text.");
                Image icon = coin.GetComponent<Image>();
                Require(icon != null && icon.sprite != null && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(icon.sprite)) && coin.targetGraphic == icon && icon.raycastTarget, row.name + " reward coin lacks a saved, clickable icon.");
                Require(coin.transition == Selectable.Transition.None, row.name + " reward icon must keep its gold appearance while disabled.");
                Require(!view.frame.GetComponent<Graphic>().raycastTarget && !view.amount.GetComponent<Graphic>().raycastTarget, row.name + " reward frame or amount intercepts the coin click.");
                CheckSavedCallback(coin, home, mission);
                views.Add(view);
            }
            Require(views.Select(view => view.mission).Distinct().Count() == 6, "Mission coin callbacks do not cover all six mission types.");
            report.AppendLine("PASS saved Home: six gold coin claim buttons, correct persistent enum callbacks, no legacy claim text and no disabled color transition.");

            int[] before = nodes.Select(node => node.gameObject.GetInstanceID()).OrderBy(id => id).ToArray();
            Invoke(home, "LoadHomeResources");
            Require((bool)Invoke(home, "BindSceneHome"), "Home binding failed after replacing mission claim buttons with coin icons.");
            int[] after = Nodes(scene).Select(node => node.gameObject.GetInstanceID()).OrderBy(id => id).ToArray();
            Require(before.SequenceEqual(after), "Binding the saved Home added or removed GameObjects.");
            foreach (RewardView view in views)
            {
                object action = typeof(HomeExperienceUI).GetMethod("SceneButtonAction", Private).Invoke(home, new object[] { view.button });
                Require(action is Action, view.row.name + " coin suffix has no Home scene action binding.");
            }
            report.AppendLine("PASS Home binding reuses every saved object and recognizes each reward coin action.");

            CheckViews(views, false, false);
            InjectProgress(true, false);
            Refresh(views);
            CheckViews(views, true, false);
            InjectProgress(true, true);
            Refresh(views);
            CheckViews(views, true, true);
            InjectProgress(false, false);
            Refresh(views);
            CheckViews(views, false, false);
            report.AppendLine("PASS all six rows: incomplete rewards stay visible and disabled; completed coins become clickable; claimed coin/frame/amount disappear while the row remains; daily reset restores rewards.");
            report.AppendLine("PASS preview-only UI regression; no claim callback invoked, wallet changed, player preferences written or scenes saved.");
            File.WriteAllText(Result, report.ToString());
        }
        finally
        {
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            MissionData.SetValue(null, previousData);
            AudioManager.Instance = previousAudio;
            GameMaster.staticScript = previousMaster;
        }
    }

    private static void CheckSavedCallback(Button button, HomeExperienceUI home, DailyMissionType mission)
    {
        int[] matches = Enumerable.Range(0, button.onClick.GetPersistentEventCount()).Where(index => button.onClick.GetPersistentTarget(index) == home && button.onClick.GetPersistentMethodName(index) == "ClaimMissionReward").ToArray();
        Require(matches.Length == 1, button.name + " needs exactly one Home.ClaimMissionReward callback.");
        int index = matches[0];
        Require(button.onClick.GetPersistentListenerState(index) != UnityEventCallState.Off, button.name + " callback is disabled.");
        SerializedProperty call = new SerializedObject(button).FindProperty("m_OnClick.m_PersistentCalls.m_Calls").GetArrayElementAtIndex(index);
        Require(call.FindPropertyRelative("m_Mode").enumValueIndex == (int)PersistentListenerMode.Int && call.FindPropertyRelative("m_Arguments.m_IntArgument").intValue == (int)mission, button.name + " callback uses the wrong mission enum argument.");
    }

    private static void CheckViews(IEnumerable<RewardView> views, bool complete, bool claimed)
    {
        foreach (RewardView view in views)
        {
            string label = view.row.name;
            Require(view.row.gameObject.activeSelf, label + " row disappeared with its reward.");
            Require(view.button.gameObject.activeSelf == !claimed && view.frame.activeSelf == !claimed && view.amount.activeSelf == !claimed, label + " reward visuals do not match the claim state.");
            Require(view.button.interactable == (complete && !claimed), label + " coin clickability does not match progress/claim state.");
            Require(view.button.transition == Selectable.Transition.None, label + " coin appearance changes when disabled.");
        }
    }

    private static void Refresh(IEnumerable<RewardView> views)
    {
        foreach (RewardView view in views) Invoke(view.ui, "Refresh");
    }

    private static void InjectProgress(bool complete, bool claimed)
    {
        object data = Activator.CreateInstance(MissionData.FieldType, true);
        SetData(data, "date", DateTime.Now.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture));
        if (complete)
        {
            SetData(data, "blocks", DailyMissionManager.GetTarget(DailyMissionType.BreakMoreBlocks));
            SetData(data, "games", DailyMissionManager.GetTarget(DailyMissionType.PlayGames));
            SetData(data, "boosters", DailyMissionManager.GetTarget(DailyMissionType.UseBoosters));
            SetData(data, "wins", DailyMissionManager.GetTarget(DailyMissionType.WinGames));
            SetData(data, "winStreak", DailyMissionManager.GetTarget(DailyMissionType.WinStreak));
            SetData(data, "winStreakCompleted", true);
        }
        if (claimed)
        {
            int mask = 0;
            foreach (DailyMissionType mission in Enum.GetValues(typeof(DailyMissionType))) mask |= 1 << (int)mission;
            SetData(data, "claimedMask", mask);
        }
        MissionData.SetValue(null, data);
    }

    private static DailyMissionType ExpectedMission(string name)
    {
        switch (name)
        {
            case "Break 50 Blocks": return DailyMissionType.BreakBlocks;
            case "Play 3 Games": return DailyMissionType.PlayGames;
            case "Use 5 Boosters": return DailyMissionType.UseBoosters;
            case "Win 5 Games":
            case "Win 2 Games": return DailyMissionType.WinGames;
            case "Win Streak 3":
            case "Collect 5 Food": return DailyMissionType.WinStreak;
            case "Break 1000 Blocks":
            case "Break 200 Blocks": return DailyMissionType.BreakMoreBlocks;
            default: throw new InvalidOperationException("Unknown daily mission row " + name);
        }
    }

    private static bool IsClaimText(string value) => value.Trim() == "NHẬN" || value.Trim() == "ĐÃ NHẬN";
    private static Transform SuffixNode(Transform row, string suffix) => row.GetComponentsInChildren<Transform>(true).Single(node => node.name.EndsWith(suffix, StringComparison.Ordinal));
    private static Transform[] Nodes(Scene scene) => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
    private static object Invoke(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);
    private static void SetData(object data, string name, object value) => data.GetType().GetField(name).SetValue(data, value);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
#endif
