using System;

public enum BoosterPurchaseResult { Success, InsufficientGold, Failed }

/// <summary>Debits a single booster purchase and refunds gold if inventory cannot be saved.</summary>
public static class BoosterPurchaseTransaction
{
    public const int UnitPrice = 200;

    public static BoosterPurchaseResult TryBuy(BuffType type, int price, Func<int> balance,
        Func<int, bool> spend, Action<BuffType, int> grant, Action<int> refund)
    {
        if (type == BuffType.None || !Enum.IsDefined(typeof(BuffType), type) || price <= 0 || balance == null ||
            spend == null || grant == null || refund == null) return BoosterPurchaseResult.Failed;
        if (balance() < price || !spend(price)) return BoosterPurchaseResult.InsufficientGold;
        try { grant(type, 1); }
        catch
        {
            refund(price);
            return BoosterPurchaseResult.Failed;
        }
        return BoosterPurchaseResult.Success;
    }
}
