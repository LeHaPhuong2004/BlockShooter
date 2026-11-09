
using System;
using UnityEngine;

public class CurrencyManager : MonoBehaviour
{
    public static CurrencyManager Instance;
    public static event Action<int, int> MoneyAwarded;
    public event Action<int> OnMoneyChanged;

    private const string MoneyKey = "PlayerMoney";
    public int Money { get; private set; }
    public static int Balance => GameMaster.staticScript != null
        ? Mathf.Max(0, GameMaster.UserGold)
        : (Instance != null ? Instance.Money : Mathf.Max(0, PlayerPrefs.GetInt(MoneyKey, 0)));

    /// <summary>Spend saved gold only when the full price is available.</summary>
    public static bool TrySpendMoney(int amount)
    {
        if (amount <= 0) return false;
        int balance = Balance;
        if (balance < amount) return false;

        int remaining = balance - amount;
        if (Instance != null)
        {
            Instance.SetMoney(remaining);
        }
        else
        {
            if (GameMaster.staticScript != null) GameMaster.UserGold = remaining;
            PlayerPrefs.SetInt(MoneyKey, remaining);
            PlayerPrefs.Save();
        }
        return true;
    }

    // A failed purchase returns its debit without playing a reward animation.
    public static void RefundMoney(int amount)
    {
        if (amount <= 0) return;
        int restored = checked(Balance + amount);
        if (Instance != null)
        {
            Instance.Money = restored;
            if (GameMaster.staticScript != null) GameMaster.UserGold = restored;
            Instance.SaveMoney();
            Instance.NotifyMoneyChanged();
        }
        else
        {
            if (GameMaster.staticScript != null) GameMaster.UserGold = restored;
            PlayerPrefs.SetInt(MoneyKey, restored);
            PlayerPrefs.Save();
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // GameMaster's Awake may run after this singleton's Awake. Keep a safe
        // local value here, then sync from GameMaster once all Awake calls finish.
        Money = PlayerPrefs.GetInt(MoneyKey, 0);
    }

    private void Start()
    {
        if (GameMaster.staticScript != null)
            Money = GameMaster.UserGold;

        SaveMoney();
        NotifyMoneyChanged();
    }

    public void AddMoney(int amount)
    {
        Debug.Log($"AddMoney CALLED: amount={amount}, before={Money}");

        if (amount <= 0) return;

        Money = checked(Balance + amount);
        if (GameMaster.staticScript != null)
            GameMaster.UserGold = Money;
        SaveMoney();
        NotifyMoneyChanged();
        NotifyMoneyAwarded(amount, Money);

        Debug.Log($"AddMoney RESULT: amount={amount}, after={Money}");
    }

    public static void AwardMoney(int amount)
    {
        if (amount <= 0) return;

        if (Instance != null)
        {
            Instance.AddMoney(amount);
            return;
        }

        if (GameMaster.staticScript == null)
        {
            int savedBalance = PlayerPrefs.GetInt(MoneyKey, 0) + amount;
            PlayerPrefs.SetInt(MoneyKey, savedBalance);
            PlayerPrefs.Save();
            NotifyMoneyAwarded(amount, savedBalance);
            return;
        }

        GameMaster.UserGold += amount;
        PlayerPrefs.SetInt(MoneyKey, GameMaster.UserGold);
        PlayerPrefs.Save();
        NotifyMoneyAwarded(amount, GameMaster.UserGold);
    }

    private static void NotifyMoneyAwarded(int amount, int newBalance)
    {
        try
        {
            MoneyAwarded?.Invoke(amount, newBalance);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    public void SetMoney(int amount)
    {
        int previousBalance = Balance;
        Money = Mathf.Max(0, amount);
        if (GameMaster.staticScript != null)
            GameMaster.UserGold = Money;
        SaveMoney();
        NotifyMoneyChanged();
        if (Money > previousBalance)
            NotifyMoneyAwarded(Money - previousBalance, Money);
    }

    public void RefreshUI()
    {
        NotifyMoneyChanged();
    }

    private void NotifyMoneyChanged()
    {
        if (OnMoneyChanged == null) return;
        foreach (Action<int> listener in OnMoneyChanged.GetInvocationList())
        {
            try { listener(Money); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }

    private void SaveMoney()
    {
        PlayerPrefs.SetInt(MoneyKey, Money);
        PlayerPrefs.Save();
    }
}
