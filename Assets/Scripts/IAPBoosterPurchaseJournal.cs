using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Saves reward targets before applying a verified purchase. All dependencies
/// are injected so interrupted grants can be tested without player saves.
/// </summary>
public sealed class IAPBoosterPurchaseJournal
{
    [Serializable]
    private sealed class Grant
    {
        public string transactionId;
        public int packNumber;
        public int boosterCount;
        public int targetGold;
        public long unlimitedUntilUtc;
        public bool goldApplied;
        public bool complete;
    }

    [Serializable]
    private sealed class JournalData
    {
        public List<Grant> grants = new List<Grant>();
    }

    private JournalData data;
    private readonly Action<string> save;

    public IAPBoosterPurchaseJournal(Func<string> load, Action<string> save)
    {
        if (load == null) throw new ArgumentNullException(nameof(load));
        this.save = save ?? throw new ArgumentNullException(nameof(save));
        string json = load();
        // A corrupt journal must never silently become an empty receipt list.
        if (string.IsNullOrEmpty(json)) data = new JournalData();
        else
        {
            data = JsonUtility.FromJson<JournalData>(json);
            if (data == null || data.grants == null || !json.Contains("\"grants\""))
                throw new InvalidOperationException("The booster purchase journal could not be loaded.");
        }
    }

    public bool IsComplete(string transactionId)
    {
        Grant grant = Find(transactionId);
        return grant != null && grant.complete;
    }

    public bool Fulfill(string transactionId, int packNumber, int gold, int boosterCount,
        int unlimitedSeconds, long now, int currentGold, long currentUnlimitedUntil,
        Func<int> getGold, Action<int> awardGold, Action<string, int> grantBoosters,
        Action<long> activateUnlimitedUntil)
    {
        if (string.IsNullOrWhiteSpace(transactionId)) throw new ArgumentException("A transaction ID is required.", nameof(transactionId));
        if (packNumber < 1 || gold < 0 || boosterCount < 0 || unlimitedSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(packNumber));
        Grant grant = Find(transactionId);
        if (grant != null && grant.complete) return false;
        if (grant == null)
        {
            grant = new Grant
            {
                transactionId = transactionId,
                packNumber = packNumber,
                boosterCount = boosterCount,
                targetGold = checked(currentGold + gold),
                unlimitedUntilUtc = unlimitedSeconds > 0 ? checked(Math.Max(now, currentUnlimitedUntil) + unlimitedSeconds) : 0
            };
            data.grants.Add(grant);
            try { Save(); }
            catch
            {
                data.grants.Remove(grant);
                throw;
            }
        }
        Apply(grant, getGold, awardGold, grantBoosters, activateUnlimitedUntil);
        return true;
    }

    public void ResumeIncomplete(Func<int> getGold, Action<int> awardGold,
        Action<string, int> grantBoosters, Action<long> activateUnlimitedUntil,
        Action<int> onCompleted = null)
    {
        foreach (Grant grant in data.grants)
        {
            if (grant.complete) continue;
            Apply(grant, getGold, awardGold, grantBoosters, activateUnlimitedUntil);
            onCompleted?.Invoke(grant.packNumber);
        }
    }

    private void Apply(Grant grant, Func<int> getGold, Action<int> awardGold,
        Action<string, int> grantBoosters, Action<long> activateUnlimitedUntil)
    {
        if (!grant.goldApplied)
        {
            // On startup this replays before user input. If the wallet was
            // already saved before an interruption, the difference is zero.
            int missingGold = Math.Max(0, grant.targetGold - getGold());
            if (missingGold > 0) awardGold(missingGold);
            grant.goldApplied = true;
            try { Save(); }
            catch { grant.goldApplied = false; throw; }
        }

        // Inventory saves its receipt atomically with all four counts.
        grantBoosters(grant.transactionId, grant.boosterCount);
        if (grant.unlimitedUntilUtc > 0) activateUnlimitedUntil(grant.unlimitedUntilUtc);

        grant.complete = true;
        try { Save(); }
        catch { grant.complete = false; throw; }
    }

    private Grant Find(string transactionId) =>
        data.grants.Find(grant => string.Equals(grant.transactionId, transactionId, StringComparison.Ordinal));

    private void Save() => save(JsonUtility.ToJson(data));
}
