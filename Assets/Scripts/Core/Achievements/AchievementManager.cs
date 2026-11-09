using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum AchievementType { BreakBlocks, UseBoosters, WinGames }

public sealed class AchievementDefinition
{
    public string Id { get; }
    public string Title { get; }
    public AchievementType Type { get; }
    public int Target { get; }
    public int Reward { get; }

    public AchievementDefinition(string id, string title, AchievementType type, int target, int reward)
    {
        Id = id;
        Title = title;
        Type = type;
        Target = target;
        Reward = reward;
    }
}

/// <summary>Lifetime achievement totals and reward receipts, with injected storage for isolated tests.</summary>
public sealed class AchievementProgressStore
{
    private static readonly IReadOnlyList<AchievementDefinition> definitions = Array.AsReadOnly(new[]
    {
        new AchievementDefinition("blocks_100", "Phá 100 khối", AchievementType.BreakBlocks, 100, 50),
        new AchievementDefinition("wins_3", "Thắng 3 ván", AchievementType.WinGames, 3, 60),
        new AchievementDefinition("boosters_5", "Dùng 5 booster", AchievementType.UseBoosters, 5, 80),
        new AchievementDefinition("blocks_1000", "Phá 1.000 khối", AchievementType.BreakBlocks, 1000, 100),
        new AchievementDefinition("wins_20", "Thắng 20 ván", AchievementType.WinGames, 20, 120),
        new AchievementDefinition("boosters_25", "Dùng 25 booster", AchievementType.UseBoosters, 25, 150),
        new AchievementDefinition("blocks_5000", "Phá 5.000 khối", AchievementType.BreakBlocks, 5000, 200),
        new AchievementDefinition("wins_100", "Thắng 100 ván", AchievementType.WinGames, 100, 250),
        new AchievementDefinition("boosters_100", "Dùng 100 booster", AchievementType.UseBoosters, 100, 300),
        new AchievementDefinition("blocks_20000", "Phá 20.000 khối", AchievementType.BreakBlocks, 20000, 500)
    });

    [Serializable]
    private sealed class PendingClaim
    {
        public string achievementId;
        public int targetGold;
    }

    [Serializable]
    private sealed class SaveData
    {
        public int version = 1;
        public int blocks;
        public int boosters;
        public int wins;
        public int claimedMask;
        public bool hasPendingClaim;
        public PendingClaim pendingClaim;
    }

    private readonly Action<string> save;
    private readonly SaveData data;
    private bool applyingClaim;

    public static IReadOnlyList<AchievementDefinition> Definitions => definitions;

    public AchievementProgressStore(Func<string> load, Action<string> save)
    {
        if (load == null) throw new ArgumentNullException(nameof(load));
        this.save = save ?? throw new ArgumentNullException(nameof(save));
        string json = load();
        if (string.IsNullOrEmpty(json)) data = new SaveData();
        else
        {
            data = JsonUtility.FromJson<SaveData>(json);
            // Never erase a damaged claim receipt and accidentally allow its reward again.
            if (data == null || data.version != 1 || !json.Contains("\"version\"") ||
                data.blocks < 0 || data.boosters < 0 || data.wins < 0 || data.claimedMask < 0)
                throw new InvalidOperationException("The achievement progress save could not be loaded.");
            // JsonUtility can restore a serialized null nested class as an empty
            // object. An explicit flag distinguishes that object from a receipt.
            // Preserve valid receipts written before the flag was introduced.
            if (!data.hasPendingClaim && data.pendingClaim != null &&
                !string.IsNullOrEmpty(data.pendingClaim.achievementId))
                data.hasPendingClaim = true;
            if (data.hasPendingClaim && (data.pendingClaim == null ||
                FindIndex(data.pendingClaim.achievementId) < 0 || data.pendingClaim.targetGold < 0))
                throw new InvalidOperationException("The pending achievement reward could not be loaded.");
            if (!data.hasPendingClaim) data.pendingClaim = null;
        }
    }

    public int GetProgress(string id)
    {
        int index = FindIndex(id);
        if (index < 0) return 0;
        switch (definitions[index].Type)
        {
            case AchievementType.BreakBlocks: return data.blocks;
            case AchievementType.UseBoosters: return data.boosters;
            case AchievementType.WinGames: return data.wins;
            default: return 0;
        }
    }

    public bool IsClaimed(string id)
    {
        int index = FindIndex(id);
        return index >= 0 && (data.claimedMask & (1 << index)) != 0;
    }

    public bool CanClaim(string id)
    {
        return IsComplete(id) && !IsClaimed(id);
    }

    public bool IsComplete(string id)
    {
        int index = FindIndex(id);
        return index >= 0 && GetProgress(id) >= definitions[index].Target;
    }

    public int CompletedCount
    {
        get
        {
            int count = 0;
            foreach (var definition in definitions)
                if (GetProgress(definition.Id) >= definition.Target) count++;
            return count;
        }
    }

    public int ClaimedCount
    {
        get
        {
            int count = 0;
            foreach (var definition in definitions)
                if (IsClaimed(definition.Id)) count++;
            return count;
        }
    }

    public int ClaimableCount
    {
        get
        {
            int count = 0;
            foreach (var definition in definitions)
                if (CanClaim(definition.Id)) count++;
            return count;
        }
    }

    public void RecordBlockDestroyed() => Increment(AchievementType.BreakBlocks);
    public void RecordBoosterUsed() => Increment(AchievementType.UseBoosters);
    public void RecordGameWon() => Increment(AchievementType.WinGames);

    private void Increment(AchievementType type)
    {
        int previous;
        switch (type)
        {
            case AchievementType.BreakBlocks:
                previous = data.blocks;
                if (previous == int.MaxValue) return;
                data.blocks++;
                try { Save(); }
                catch { data.blocks = previous; throw; }
                break;
            case AchievementType.UseBoosters:
                previous = data.boosters;
                if (previous == int.MaxValue) return;
                data.boosters++;
                try { Save(); }
                catch { data.boosters = previous; throw; }
                break;
            case AchievementType.WinGames:
                previous = data.wins;
                if (previous == int.MaxValue) return;
                data.wins++;
                try { Save(); }
                catch { data.wins = previous; throw; }
                break;
        }
    }

    public bool TryClaim(string id, Func<int> getGold, Action<int> awardGold)
    {
        if (getGold == null) throw new ArgumentNullException(nameof(getGold));
        if (awardGold == null) throw new ArgumentNullException(nameof(awardGold));
        if (applyingClaim) return false;

        if (data.hasPendingClaim)
        {
            string pendingId = data.pendingClaim.achievementId;
            ResumePendingClaim(getGold, awardGold);
            if (string.Equals(id, pendingId, StringComparison.Ordinal)) return true;
        }
        if (!CanClaim(id)) return false;
        int index = FindIndex(id);
        data.pendingClaim = new PendingClaim
        {
            achievementId = id,
            targetGold = checked(Math.Max(0, getGold()) + definitions[index].Reward)
        };
        data.hasPendingClaim = true;
        try { Save(); }
        catch { data.pendingClaim = null; data.hasPendingClaim = false; throw; }

        ResumePendingClaim(getGold, awardGold);
        return true;
    }

    /// <summary>Recover before user input; saved wallet targets make interrupted claims idempotent.</summary>
    public bool ResumePendingClaim(Func<int> getGold, Action<int> awardGold)
    {
        if (getGold == null) throw new ArgumentNullException(nameof(getGold));
        if (awardGold == null) throw new ArgumentNullException(nameof(awardGold));
        if (applyingClaim || !data.hasPendingClaim) return false;
        applyingClaim = true;
        try
        {
            PendingClaim pending = data.pendingClaim;
            int index = FindIndex(pending.achievementId);
            int previousMask = data.claimedMask;
            if ((previousMask & (1 << index)) == 0)
            {
                int missingGold = Math.Max(0, pending.targetGold - Math.Max(0, getGold()));
                if (missingGold > 0) awardGold(missingGold);
            }
            data.claimedMask |= 1 << index;
            data.pendingClaim = null;
            data.hasPendingClaim = false;
            try { Save(); }
            catch
            {
                data.claimedMask = previousMask;
                data.pendingClaim = pending;
                data.hasPendingClaim = true;
                throw;
            }
            return true;
        }
        finally { applyingClaim = false; }
    }

    private static int FindIndex(string id)
    {
        for (int i = 0; i < definitions.Count; i++)
            if (string.Equals(definitions[i].Id, id, StringComparison.Ordinal)) return i;
        return -1;
    }

    private void Save() => save(JsonUtility.ToJson(data));
}

/// <summary>Lifetime progress shared by gameplay and the Home achievement cards.</summary>
public static class AchievementManager
{
    private const string SaveKey = "achievements_v1";
    private static AchievementProgressStore store;
    private static bool initializationFailed;
    private static bool recoveringClaim;
    public static event Action Changed;

    public static IReadOnlyList<AchievementDefinition> Definitions => AchievementProgressStore.Definitions;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        store = null;
        initializationFailed = false;
        recoveringClaim = false;
        Changed = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        GetStore();
        TryRecoverPendingClaim();
    }

    private static AchievementProgressStore GetStore()
    {
        if (!Application.isPlaying || initializationFailed) return null;
        if (store != null) return store;
        try
        {
#if UNITY_EDITOR
            // A new Play session starts fresh; scene changes retain the same store.
            store = new AchievementProgressStore(() => string.Empty, json => { });
#else
            store = new AchievementProgressStore(
                () => PlayerPrefs.GetString(SaveKey, ""),
                json => { PlayerPrefs.SetString(SaveKey, json); PlayerPrefs.Save(); });
#endif
            return store;
        }
        catch (Exception exception)
        {
            initializationFailed = true;
            store = null;
            Debug.LogException(exception);
            return null;
        }
    }

    private static bool TryRecoverPendingClaim()
    {
        // Start.unity precedes Home.unity in player builds. Its temporary wallet
        // must not receive a reward before GameMaster has loaded canonical gold.
        if (store == null || GameMaster.staticScript == null || recoveringClaim) return false;
        recoveringClaim = true;
        try
        {
            bool recovered = store.ResumePendingClaim(() => CurrencyManager.Balance, CurrencyManager.AwardMoney);
            if (recovered) NotifyChanged();
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return false;
        }
        finally { recoveringClaim = false; }
    }

    public static int GetProgress(string id) => GetStore()?.GetProgress(id) ?? 0;
    public static bool IsComplete(string id) => GetStore()?.IsComplete(id) ?? false;
    public static bool IsClaimed(string id) => GetStore()?.IsClaimed(id) ?? false;
    public static bool CanClaim(string id) => GetStore()?.CanClaim(id) ?? false;
    public static int CompletedCount => GetStore()?.CompletedCount ?? 0;
    public static int ClaimedCount => GetStore()?.ClaimedCount ?? 0;
    public static int ClaimableCount => GetStore()?.ClaimableCount ?? 0;

    public static bool TryClaim(string id)
    {
        if (GameMaster.staticScript == null) return false;
        var progress = GetStore();
        if (progress == null || !TryRecoverPendingClaim()) return false;
        try
        {
            bool claimed = progress.TryClaim(id, () => CurrencyManager.Balance, CurrencyManager.AwardMoney);
            if (claimed) NotifyChanged();
            return claimed;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            NotifyChanged();
            return false;
        }
    }

    public static void RecordBlockDestroyed() => Record(progress => progress.RecordBlockDestroyed());
    public static void RecordBoosterUsed() => Record(progress => progress.RecordBoosterUsed());
    public static void RecordGameWon() => Record(progress => progress.RecordGameWon());

    private static void Record(Action<AchievementProgressStore> record)
    {
        var progress = GetStore();
        if (progress == null) return;
        try { record(progress); NotifyChanged(); }
        catch (Exception exception) { Debug.LogException(exception); }
    }

    private static void NotifyChanged()
    {
        if (Changed == null) return;
        foreach (Action listener in Changed.GetInvocationList())
        {
            try { listener(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }
}
