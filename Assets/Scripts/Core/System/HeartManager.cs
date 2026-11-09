using System;
using UnityEngine;

public static class HeartManager
{
    private const string SaveKey = "player_hearts_v1";
    private static HeartState data;
    private static bool roundInProgress;
    public static bool RoundInProgress => roundInProgress;
    private static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        data = null;
        roundInProgress = false;
    }

    private static HeartState State
    {
        get
        {
            if (data == null)
            {
#if !UNITY_EDITOR
                try { data = JsonUtility.FromJson<HeartState>(PlayerPrefs.GetString(SaveKey, "")); }
                catch (ArgumentException) { data = null; }
#endif
                if (data == null) data = new HeartState();
            }
            if (data.Advance(Now)) Save();
            return data;
        }
    }

    public static int Hearts => State.hearts;
    public static bool IsUnlimited => State.IsUnlimited(Now);
    public static long UnlimitedUntilUtc => State.unlimitedUntil;
    public static bool CanPlay => IsUnlimited || Hearts > 0;
    public static string CountText => IsUnlimited ? "∞" : Hearts.ToString();
    public static string TimerText
    {
        get
        {
            string timer = State.GetTimerText(Now);
            return timer == "FULL" ? Localize.Text("ĐẦY", "FULL") : timer;
        }
    }

    public static bool BeginRound()
    {
        if (!CanPlay) return false;
        roundInProgress = true;
        return true;
    }

    public static void FinishRound(bool won)
    {
        if (!roundInProgress) return;
        roundInProgress = false;
        if (!won) State.LoseHeart(Now);
        Save();
    }

    public static void AbandonRound()
    {
        if (!roundInProgress) return;
        FinishRound(false);
        DailyMissionManager.RecordAbandonedGame();
    }

    public static void AddHearts(int amount)
    {
        if (!Application.isPlaying || amount <= 0) return;
        HeartState current = State;
        var previous = new HeartState
        {
            hearts = current.hearts, nextHeartAt = current.nextHeartAt, unlimitedUntil = current.unlimitedUntil
        };
        try
        {
            current.AddHearts(Now, amount);
            Save();
        }
        catch
        {
            data = previous;
            throw;
        }
    }

    public static void ActivateUnlimited(int seconds = HeartState.UnlimitedSeconds)
    {
        if (!Application.isPlaying) return;
        State.ActivateUnlimited(Now, seconds);
        Save();
    }

    // An absolute target lets interrupted purchases resume without adding the
    // same paid heart duration again when the store replays its transaction.
    public static void ActivateUnlimitedUntil(long utcSeconds)
    {
        if (!Application.isPlaying || utcSeconds <= Now) return;
        State.unlimitedUntil = Math.Max(State.unlimitedUntil, utcSeconds);
        Save();
    }

    private static void Save()
    {
#if !UNITY_EDITOR
        if (Application.isPlaying && data != null)
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }
#endif
    }
}
