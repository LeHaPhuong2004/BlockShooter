#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Checks one-time victory rewards and authored actions without ads or wallet writes.</summary>
[InitializeOnLoad]
public static class VictoryRewardRegression
{
    private const string Request = "Temp/VictoryRewardRegression.request";
    private const string Result = "Temp/VictoryRewardRegression.result";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
    private static readonly FieldInfo HeartData = typeof(HeartManager).GetField("data", StaticPrivate);
    private static readonly FieldInfo Round = typeof(HeartManager).GetField("roundInProgress", StaticPrivate);
    private static readonly FieldInfo GameInstance = typeof(GamePauseUI).GetField("<Instance>k__BackingField", StaticPrivate);
    private static bool running;

    static VictoryRewardRegression() => EditorApplication.update += Poll;
    private static void Poll()
    {
        if (running || !File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        running = true;
        try { Run(); }
        catch (Exception exception) { File.WriteAllText(Result, "FAIL\n" + exception); Debug.LogException(exception); }
        finally { File.Delete(Request); running = false; }
    }

    [MenuItem("Tools/Diagnostics/Check Victory Coin Rewards")]
    public static void Run()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Run victory checks outside Play Mode.");
        var report = new StringBuilder();
        CheckTransactions(report);
        CheckSavedGame(report);
        report.AppendLine("PASS: isolated preview only; no ad SDK, wallet, progression or preference writes; no scenes saved.");
        File.WriteAllText(Result, report.ToString());
    }

    private static void CheckTransactions(StringBuilder report)
    {
        int total = 0, calls = 0;
        var normal = new VictoryRewardClaim();
        Action<int> award = amount => { total += amount; calls++; };
        Require(normal.TryClaimBase(amount =>
        {
            award(amount);
            Require(!normal.TryClaimBase(award) && !normal.TryBeginAd() && !normal.CompleteAd(true, award),
                "A reentrant wallet callback credited the same victory twice.");
        }), "The base reward was not claimable.");
        Require(normal.Claimed && normal.AwardedAmount == 100 && total == 100 && calls == 1 &&
            !normal.TryClaimBase(award) && !normal.CompleteAd(true, award), "Normal reward must credit exactly 100 once.");

        total = calls = 0;
        var doubled = new VictoryRewardClaim();
        Require(!doubled.CompleteAd(true, award), "An earned callback without an ad request credited coins.");
        Require(doubled.TryBeginAd() && !doubled.TryBeginAd() && !doubled.TryClaimBase(award),
            "A pending ad allowed another claim or ad request.");
        Require(!doubled.CompleteAd(false, award) && !doubled.Claimed && !doubled.AdPending && total == 0,
            "Early ad close credited coins or prevented retry.");
        Require(doubled.TryBeginAd() && !doubled.CompleteAd(false, award) && total == 0,
            "Ad failure credited coins or prevented another attempt.");
        Require(doubled.TryBeginAd() && doubled.CompleteAd(true, amount =>
        {
            award(amount);
            Require(!doubled.CompleteAd(true, award) && !doubled.TryClaimBase(award),
                "A reentrant earned callback credited twice.");
        }), "An earned retry did not credit the doubled reward.");
        Require(doubled.Claimed && !doubled.AdPending && doubled.AwardedAmount == 200 && total == 200 && calls == 1 &&
            !doubled.CompleteAd(true, award) && !doubled.TryClaimBase(award), "Earned reward must credit exactly 200 once.");

        total = calls = 0;
        var cancelled = new VictoryRewardClaim();
        Require(cancelled.TryBeginAd(), "Could not begin the cancellation case.");
        cancelled.CancelAd();
        Require(!cancelled.CompleteAd(true, award) && total == 0 && cancelled.TryClaimBase(award) && total == 100 && calls == 1,
            "Cancelled or stale ad callbacks credited coins or blocked the base reward.");
        report.AppendLine("PASS transaction: base100, earned200, pending/reentrant/duplicate guards; close/failure/cancel pay nothing and allow retry.");
    }

    private static void CheckSavedGame(StringBuilder report)
    {
        object previousHearts = HeartData.GetValue(null), previousRound = Round.GetValue(null), previousGame = GameInstance.GetValue(null);
        LevelManager previousLevel = LevelManager.Instance;
        NavigationManager previousNavigation = NavigationManager.Instance;
        AudioManager previousAudio = AudioManager.Instance;
        GameMaster previousMaster = GameMaster.staticScript;
        float previousScale = Time.timeScale;
        Scene scene = default;
        GamePauseUI game = null;
        try
        {
            AudioManager.Instance = null; GameMaster.staticScript = null;
            scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Game.unity");
            Transform[] nodes = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            game = nodes.Select(node => node.GetComponent<GamePauseUI>()).Single(item => item != null);
            NavigationManager navigation = nodes.Select(node => node.GetComponent<NavigationManager>()).Single(item => item != null);
            GameObject panel = Get<GameObject>(game, "victoryPanel");
            Require(panel != null && !panel.activeSelf, "Saved victory panel must start hidden.");
            Require(Get<TMP_Text>(game, "victoryRewardAmountLabel")?.text == "100", "Saved victory reward is not 100.");
            Button doubled = Get<Button>(game, "victoryDoubleRewardButton"), normal = Get<Button>(game, "victoryClaimRewardButton");
            Require(panel.GetComponentsInChildren<Button>(true).Length == 2, "Victory must offer only double reward and claim/next.");
            Callback(doubled, game, "ClaimDoubleVictoryReward"); Callback(normal, game, "ClaimVictoryRewardAndContinue");
            Require(doubled.transform.localPosition.x < normal.transform.localPosition.x, "Double reward must occupy the left button.");
            Require(panel.GetComponentsInChildren<Image>(true).Any(image => image.name == "Victory Reward Coin" && image.sprite != null),
                "Victory has no authored reward coin artwork.");
            Require(!nodes.Any(node => node.name == "Next Levels Frame" || node.name == "Next Levels Title" ||
                node.name.StartsWith("Next Level Character ", StringComparison.Ordinal)), "The obsolete next-level strip remains.");
            GameObject demo = Get<GameObject>(game, "simulatedAdPanel");
            Require(demo != null && !demo.activeSelf, "Saved temporary ad must start hidden.");
            Callback(Get<Button>(game, "simulatedAdClaimButton"), game, "FinishSimulatedVictoryAd");
            Callback(Get<Button>(game, "simulatedAdCloseButton"), game, "CancelSimulatedVictoryAd");

            var level = game.gameObject.AddComponent<LevelManager>();
            Set(level, "currentLevel", 2); LevelManager.Instance = level;
            Require(Resources.Load<TextAsset>("LevelData/level_2") != null, "The blocked-Next fixture requires saved level 2.");
            GameInstance.SetValue(null, game); NavigationManager.Instance = navigation;
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            HeartData.SetValue(null, new HeartState { hearts = 0, nextHeartAt = now + HeartState.RefillSeconds });
            Round.SetValue(null, false); Time.timeScale = .75f;
            game.PresentVictory();
            int credited = 0;
            VictoryRewardClaim claim = Get<VictoryRewardClaim>(game, "victoryReward");
            Require(claim.TryClaimBase(amount => credited += amount), "Could not inject an isolated base claim.");
            Invoke(game, "RefreshVictoryRewardUI");
            Require(!doubled.interactable && normal.interactable, "Claimed victory actions have incorrect eligibility.");
            Invoke(game, "ContinueAfterVictory"); Invoke(game, "ContinueAfterVictory");
            Require(Get<GameObject>(game, "outOfHeartPanel").activeSelf && Time.timeScale == 0f &&
                credited == 100 && claim.Claimed && level.CurrentLevel == 2 && HeartManager.Hearts == 0,
                "Blocked Next changed progression, hearts or the claimed reward.");
            game.PresentVictory();
            Require(ReferenceEquals(claim, Get<VictoryRewardClaim>(game, "victoryReward")) && !claim.TryClaimBase(amount => credited += amount) &&
                credited == 100, "Reopening the same victory allowed a second reward.");
            CheckSimulatedAd(game, level, report);
            report.AppendLine("PASS saved Game: coin100 and two persistent reward actions; claimed Next blocked by hearts retains one claim and unchanged progression.");
        }
        finally
        {
            if (game != null) game.PrepareForNextLevel();
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            HeartData.SetValue(null, previousHearts); Round.SetValue(null, previousRound); GameInstance.SetValue(null, previousGame);
            LevelManager.Instance = previousLevel; NavigationManager.Instance = previousNavigation;
            AudioManager.Instance = previousAudio; GameMaster.staticScript = previousMaster; Time.timeScale = previousScale;
        }
    }

    private static void CheckSimulatedAd(GamePauseUI game, LevelManager level, StringBuilder report)
    {
        Set(level, "currentLevel", 3); game.PresentVictory();
        VictoryRewardClaim claim = Get<VictoryRewardClaim>(game, "victoryReward");
        GameObject panel = Get<GameObject>(game, "simulatedAdPanel");
        Button finish = Get<Button>(game, "simulatedAdClaimButton");
        Button doubled = Get<Button>(game, "victoryDoubleRewardButton"), normal = Get<Button>(game, "victoryClaimRewardButton");
        Require(claim.TryBeginAd(), "A new victory did not reset reward eligibility.");
        Set(game, "simulatedAdRequest", claim); Set(game, "simulatedAdRemaining", 5f); panel.SetActive(true);
        Invoke(game, "RefreshVictoryRewardUI"); Invoke(game, "RefreshSimulatedVictoryAdUI");
        Require(!finish.interactable && !doubled.interactable && !normal.interactable,
            "The simulated ad enabled a claim before viewing or allowed another victory action.");
        game.FinishSimulatedVictoryAd();
        Require(claim.AdPending && !claim.Claimed && panel.activeSelf, "Early finish dismissed or credited the simulated ad.");
        Invoke(game, "TickSimulatedVictoryAd", -1f);
        Require(Get<float>(game, "simulatedAdRemaining") == 5f, "Negative timer delta changed the viewing duration.");
        Invoke(game, "TickSimulatedVictoryAd", 4.5f);
        Require(!finish.interactable && !claim.Claimed && Time.timeScale == 0f,
            "The temporary ad enabled its reward before five unscaled seconds.");
        Invoke(game, "TickSimulatedVictoryAd", .5f);
        Require(finish.interactable && !claim.Claimed && Get<float>(game, "simulatedAdRemaining") == 0f,
            "Five seconds should enable the reward without automatically claiming it.");
        Invoke(game, "HideSimulatedVictoryAd"); Invoke(game, "CompleteVictoryAd", claim, false);
        Require(!panel.activeSelf && !claim.Claimed && !claim.AdPending && doubled.interactable && normal.interactable && Time.timeScale == 0f,
            "Skipping the simulated ad failed to return to an unclaimed, paused victory.");

        Require(claim.TryBeginAd(), "Skipping prevented an ad retry.");
        Set(game, "simulatedAdRequest", claim); Set(game, "simulatedAdRemaining", 5f); panel.SetActive(true);
        Set(level, "currentLevel", 4); game.PresentVictory();
        VictoryRewardClaim next = Get<VictoryRewardClaim>(game, "victoryReward");
        Require(!ReferenceEquals(claim, next) && !claim.AdPending && !panel.activeSelf &&
            Get<VictoryRewardClaim>(game, "simulatedAdRequest") == null, "A new victory retained an obsolete ad request.");
        int staleCredit = 0;
        Require(!claim.CompleteAd(true, amount => staleCredit += amount) && staleCredit == 0,
            "An invalidated old ad request could still claim coins.");
        Require(next.TryBeginAd(), "A previous ad blocked the new victory's ad.");
        Invoke(game, "CompleteVictoryAd", claim, false);
        Require(next.AdPending && !next.Claimed, "An old ad callback cancelled the new victory's pending request.");
        next.CancelAd(); Invoke(game, "RefreshVictoryRewardUI");
        report.AppendLine("PASS temporary ad: paused 5-second eligibility, no automatic credit, skip/retry, and stale request/callback isolation.");
    }

    private static void Callback(Button button, GamePauseUI game, string method)
    {
        Require(button != null && button.targetGraphic != null && button.targetGraphic.raycastTarget &&
            Enumerable.Range(0, button.onClick.GetPersistentEventCount()).Any(index =>
                button.onClick.GetPersistentTarget(index) == game && button.onClick.GetPersistentMethodName(index) == method),
            "Missing authored callback " + method);
    }
    private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static object Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
#endif
