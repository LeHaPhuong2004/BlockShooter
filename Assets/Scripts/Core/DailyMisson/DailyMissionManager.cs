using System;
using System.Globalization;
using UnityEngine;

// Value 4 belonged to the retired food task; do not reuse its saved claim bit.
public enum DailyMissionType { BreakBlocks = 0, PlayGames = 1, UseBoosters = 2, WinGames = 3, BreakMoreBlocks = 5, WinStreak = 6 }

/// <summary>Daily progress shared across scenes; only player builds persist between launches.</summary>
public static class DailyMissionManager
{
    private const string SaveKey = "daily_missions_v1";

    [Serializable]
    private sealed class SaveData
    {
        public string date;
        public int blocks;
        public int games;
        public int boosters;
        public int wins;
        public int winStreak;
        public bool winStreakCompleted;
        public int claimedMask;
    }

    private static SaveData data;
    private static readonly DailyMissionType[] missionTypes = (DailyMissionType[])Enum.GetValues(typeof(DailyMissionType));
    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        data = null;
        Changed = null;
    }

    public static void RefreshDay()
    {
        if (!Application.isPlaying) return;
        string today = DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
#if !UNITY_EDITOR
        if (data == null)
        {
            try { data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey, "")); }
            catch (ArgumentException) { data = null; }
        }
#endif
        if (data != null && data.date == today) return;
        data = new SaveData { date = today };
        Save();
        Changed?.Invoke();
    }

    public static int GetTarget(DailyMissionType type)
    {
        switch (type)
        {
            case DailyMissionType.BreakBlocks: return 50;
            case DailyMissionType.PlayGames: return 3;
            case DailyMissionType.UseBoosters: return 5;
            case DailyMissionType.WinGames: return 5;
            case DailyMissionType.WinStreak: return 3;
            case DailyMissionType.BreakMoreBlocks: return 1000;
            default: throw new ArgumentOutOfRangeException(nameof(type));
        }
    }

    public static int GetReward(DailyMissionType type)
    {
        switch (type)
        {
            case DailyMissionType.BreakBlocks: return 30;
            case DailyMissionType.PlayGames: return 40;
            case DailyMissionType.UseBoosters: return 50;
            case DailyMissionType.WinGames: return 60;
            case DailyMissionType.WinStreak: return 50;
            case DailyMissionType.BreakMoreBlocks: return 80;
            default: throw new ArgumentOutOfRangeException(nameof(type));
        }
    }

    public static int GetProgress(DailyMissionType type)
    {
        RefreshDay();
        if (data == null) return 0;
        switch (type)
        {
            case DailyMissionType.BreakBlocks: return data.blocks;
            case DailyMissionType.PlayGames: return data.games;
            case DailyMissionType.UseBoosters: return data.boosters;
            case DailyMissionType.WinGames: return data.wins;
            case DailyMissionType.WinStreak: return data.winStreakCompleted ? GetTarget(type) : data.winStreak;
            case DailyMissionType.BreakMoreBlocks: return data.blocks;
            default: throw new ArgumentOutOfRangeException(nameof(type));
        }
    }

    public static bool IsClaimed(DailyMissionType type)
    {
        RefreshDay();
        return data != null && (data.claimedMask & (1 << (int)type)) != 0;
    }

    public static bool CanClaim(DailyMissionType type)
    {
        return GetProgress(type) >= GetTarget(type) && !IsClaimed(type);
    }

    public static int ClaimableCount
    {
        get
        {
            int count = 0;
            foreach (var type in missionTypes)
                if (CanClaim(type)) count++;
            return count;
        }
    }

    public static bool HasClaimableReward => ClaimableCount > 0;

    public static void RecordBlockDestroyed() => Record(DailyMissionType.BreakBlocks);
    public static void RecordGamePlayed(bool won = false) => Record(DailyMissionType.PlayGames, won);
    public static void RecordBoosterUsed() => Record(DailyMissionType.UseBoosters);

    public static void RecordAbandonedGame()
    {
        if (!Application.isPlaying) return;
        RefreshDay();
        data.winStreak = 0;
        Save();
        Changed?.Invoke();
    }

    private static void Record(DailyMissionType type, bool won = false)
    {
        if (!Application.isPlaying) return;
        RefreshDay();
        // Keep actual totals; the card clamps its display to the mission target.
        switch (type)
        {
            case DailyMissionType.BreakBlocks: data.blocks = Increment(data.blocks); break;
            case DailyMissionType.PlayGames:
                data.games = Increment(data.games);
                if (won)
                {
                    data.wins = Increment(data.wins);
                    data.winStreak = Increment(data.winStreak);
                    if (data.winStreak >= GetTarget(DailyMissionType.WinStreak))
                        data.winStreakCompleted = true;
                }
                else data.winStreak = 0;
                break;
            case DailyMissionType.UseBoosters: data.boosters = Increment(data.boosters); break;
        }
        Save();
        Changed?.Invoke();
    }

    private static int Increment(int value) => value < int.MaxValue ? value + 1 : value;

    public static bool TryClaim(DailyMissionType type)
    {
        if (!Application.isPlaying || !CanClaim(type)) return false;
        data.claimedMask |= 1 << (int)type;
        int reward = GetReward(type);
        CurrencyManager.AwardMoney(reward);
        Save();
        Changed?.Invoke();
        return true;
    }

    private static void Save()
    {
#if !UNITY_EDITOR
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
#endif
    }
}
