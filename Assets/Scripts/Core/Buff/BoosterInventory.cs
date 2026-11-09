using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>The shop and gameplay use the same saved booster counts.</summary>
public static class BoosterInventory
{
    private const string SaveKey = "player_booster_inventory_v1";
    private static BoosterInventoryStore store;
    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        store = null;
        Changed = null;
    }

    public static void Initialize(TextAsset legacyDefaults = null)
    {
        if (store != null) return;
        store = new BoosterInventoryStore(
            () => PlayerPrefs.GetString(SaveKey, ""),
            json =>
            {
                PlayerPrefs.SetString(SaveKey, json);
                PlayerPrefs.Save();
            },
            () => LoadLegacyJson(legacyDefaults));
        store.Changed += NotifyChanged;
    }

    private static BoosterInventoryStore Store
    {
        get { Initialize(); return store; }
    }

    public static int GetAmount(BuffType type) => Store.GetAmount(type);
    public static void AddAmount(BuffType type, int amount) => Store.AddAmount(type, amount);
    public static void AddBundle(int amountPerBooster, int unlimitedHeart = 0) => Store.AddBundle(amountPerBooster, unlimitedHeart);
    public static bool TrySpend(BuffType type, int amount = 1) => Store.TrySpend(type, amount);
    public static int GetUnlimitedHeartAmount() => Store.UnlimitedHeartAmount;
    public static void AddUnlimitedHearts(int amount) => Store.AddUnlimitedHearts(amount);
    public static bool TrySpendUnlimitedHeart(int amount = 1) => Store.TrySpendUnlimitedHeart(amount);

    // The receipt and all four counts are saved together, so replayed store
    // callbacks cannot grant the same bundle again after an application restart.
    public static bool GrantBundleOnce(string transactionId, int amountPerBooster, int unlimitedHeart = 0) =>
        Store.GrantBundleOnce(transactionId, amountPerBooster, unlimitedHeart);

    private static string LoadLegacyJson(TextAsset fallback)
    {
        string[] paths =
        {
            Path.Combine(Application.persistentDataPath, "PlayerData/buff.json"),
            Path.Combine(Application.persistentDataPath, "Resources/PlayerData/buff.json"),
            Path.Combine(Application.persistentDataPath, "buff.json"),
            Path.Combine(Application.dataPath, "Resources/PlayerData/buff.json")
        };
        foreach (string path in paths)
        {
            try
            {
                if (!File.Exists(path)) continue;
                string json = File.ReadAllText(path);
                if (BoosterInventoryStore.IsValidSave(json)) return json;
            }
            catch (IOException exception) { Debug.LogWarning("Cannot migrate booster inventory: " + exception.Message); }
            catch (UnauthorizedAccessException exception) { Debug.LogWarning("Cannot migrate booster inventory: " + exception.Message); }
        }

        if (fallback == null) fallback = Resources.Load<TextAsset>("PlayerData/buff");
        return fallback != null ? fallback.text : "";
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

/// <summary>
/// A storage-independent inventory. Injected storage also allows migration,
/// purchase grants and reloads to be tested without touching player saves.
/// </summary>
public sealed class BoosterInventoryStore
{
    [Serializable]
    private sealed class InventoryData
    {
        public int SplitGun;
        public int TakeBelowGun;
        public int SuffleGridGuns;
        public int RevealGun;
        public int UnlimitedHeart;
        public List<string> grantedBundles = new List<string>();
    }

    private InventoryData data;
    private readonly Action<string> save;
    public event Action Changed;
    public int UnlimitedHeartAmount => data.UnlimitedHeart;

    public BoosterInventoryStore(Func<string> load, Action<string> save, Func<string> loadLegacy = null)
    {
        if (load == null) throw new ArgumentNullException(nameof(load));
        this.save = save ?? throw new ArgumentNullException(nameof(save));
        string savedJson = load();
        data = ReadSave(savedJson);
        if (data != null) return;

        if (!string.IsNullOrWhiteSpace(savedJson))
            Debug.LogWarning("Saved booster inventory is invalid; recovering the legacy inventory.");
        data = ReadSave(loadLegacy != null ? loadLegacy() : "") ?? new InventoryData();
        save(JsonUtility.ToJson(data));
    }

    public int GetAmount(BuffType type)
    {
        switch (type)
        {
            case BuffType.SplitGun: return data.SplitGun;
            case BuffType.TakeBelowGun: return data.TakeBelowGun;
            case BuffType.SuffleGridGuns: return data.SuffleGridGuns;
            case BuffType.RevealGun: return data.RevealGun;
            default: return 0;
        }
    }

    public void AddAmount(BuffType type, int amount)
    {
        ValidateType(type);
        ValidateAmount(amount);
        if (amount == 0) return;
        InventoryData next = Copy();
        SetAmount(next, type, checked(GetAmount(type) + amount));
        Commit(next);
    }

    public void AddBundle(int amountPerBooster, int unlimitedHeart = 0)
    {
        ValidateAmount(amountPerBooster);
        ValidateAmount(unlimitedHeart);
        if (amountPerBooster == 0 && unlimitedHeart == 0) return;
        InventoryData next = Copy();
        AddBundleTo(next, amountPerBooster, unlimitedHeart);
        Commit(next);
    }

    public bool GrantBundleOnce(string transactionId, int amountPerBooster, int unlimitedHeart = 0)
    {
        if (string.IsNullOrWhiteSpace(transactionId)) throw new ArgumentException("A store transaction ID is required.", nameof(transactionId));
        ValidateAmount(amountPerBooster);
        ValidateAmount(unlimitedHeart);
        if (data.grantedBundles.Contains(transactionId)) return false;
        InventoryData next = Copy();
        AddBundleTo(next, amountPerBooster, unlimitedHeart);
        next.grantedBundles.Add(transactionId);
        Commit(next);
        return true;
    }

    public bool TrySpend(BuffType type, int amount = 1)
    {
        if (!IsBooster(type) || amount <= 0 || GetAmount(type) < amount) return false;
        InventoryData next = Copy();
        SetAmount(next, type, GetAmount(type) - amount);
        Commit(next);
        return true;
    }

    public void AddUnlimitedHearts(int amount)
    {
        ValidateAmount(amount);
        if (amount == 0) return;
        InventoryData next = Copy();
        next.UnlimitedHeart = checked(next.UnlimitedHeart + amount);
        Commit(next);
    }

    public bool TrySpendUnlimitedHeart(int amount = 1)
    {
        if (amount <= 0 || data.UnlimitedHeart < amount) return false;
        InventoryData next = Copy();
        next.UnlimitedHeart -= amount;
        Commit(next);
        return true;
    }

    public static bool IsValidSave(string json) => ReadSave(json) != null;

    private static InventoryData ReadSave(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        string trimmed = json.Trim();
        if (!trimmed.StartsWith("{") || !trimmed.EndsWith("}")) return null;
        if (!trimmed.Contains("\"SplitGun\"") && !trimmed.Contains("\"TakeBelowGun\"") &&
            !trimmed.Contains("\"SuffleGridGuns\"") && !trimmed.Contains("\"RevealGun\"") &&
            !trimmed.Contains("\"UnlimitedHeart\"")) return null;
        try
        {
            InventoryData result = JsonUtility.FromJson<InventoryData>(json);
            if (result == null) return null;
            result.SplitGun = Math.Max(0, result.SplitGun);
            result.TakeBelowGun = Math.Max(0, result.TakeBelowGun);
            result.SuffleGridGuns = Math.Max(0, result.SuffleGridGuns);
            result.RevealGun = Math.Max(0, result.RevealGun);
            result.UnlimitedHeart = Math.Max(0, result.UnlimitedHeart);
            if (result.grantedBundles == null) result.grantedBundles = new List<string>();
            return result;
        }
        catch (ArgumentException) { return null; }
    }

    private InventoryData Copy()
    {
        return new InventoryData
        {
            SplitGun = data.SplitGun,
            TakeBelowGun = data.TakeBelowGun,
            SuffleGridGuns = data.SuffleGridGuns,
            RevealGun = data.RevealGun,
            UnlimitedHeart = data.UnlimitedHeart,
            grantedBundles = new List<string>(data.grantedBundles)
        };
    }

    private void Commit(InventoryData next)
    {
        // Keep the in-memory inventory unchanged if durable storage fails.
        save(JsonUtility.ToJson(next));
        data = next;
        if (Changed == null) return;
        foreach (Action listener in Changed.GetInvocationList())
        {
            try { listener(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }

    private static void AddBundleTo(InventoryData next, int amount, int unlimitedHeart)
    {
        checked
        {
            next.SplitGun += amount;
            next.TakeBelowGun += amount;
            next.SuffleGridGuns += amount;
            next.RevealGun += amount;
            next.UnlimitedHeart += unlimitedHeart;
        }
    }

    private static bool IsBooster(BuffType type) => type == BuffType.SplitGun || type == BuffType.TakeBelowGun ||
        type == BuffType.SuffleGridGuns || type == BuffType.RevealGun;

    private static void ValidateType(BuffType type)
    {
        if (!IsBooster(type)) throw new ArgumentOutOfRangeException(nameof(type));
    }

    private static void ValidateAmount(int amount)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
    }

    private static void SetAmount(InventoryData next, BuffType type, int amount)
    {
        switch (type)
        {
            case BuffType.SplitGun: next.SplitGun = amount; break;
            case BuffType.TakeBelowGun: next.TakeBelowGun = amount; break;
            case BuffType.SuffleGridGuns: next.SuffleGridGuns = amount; break;
            case BuffType.RevealGun: next.RevealGun = amount; break;
        }
    }
}
