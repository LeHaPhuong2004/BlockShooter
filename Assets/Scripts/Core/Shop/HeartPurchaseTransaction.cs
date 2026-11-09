using System;

public enum HeartPurchaseResult { Success, InsufficientGold, Failed }

/// <summary>Buys finite hearts with gold and refunds a reward that could not be saved.</summary>
public static class HeartPurchaseTransaction
{
    public static int GetPrice(int amount) => amount == 5 ? 2000 : amount == 20 ? 8000 : 0;

    public static HeartPurchaseResult TryBuy(int amount, Func<int> balance, Func<int, bool> spend,
        Action<int> grant, Action<int> refund)
    {
        int price = GetPrice(amount);
        if (price <= 0 || balance == null || spend == null || grant == null || refund == null)
            return HeartPurchaseResult.Failed;
        if (balance() < price || !spend(price)) return HeartPurchaseResult.InsufficientGold;
        try { grant(amount); }
        catch
        {
            refund(price);
            return HeartPurchaseResult.Failed;
        }
        return HeartPurchaseResult.Success;
    }
}
