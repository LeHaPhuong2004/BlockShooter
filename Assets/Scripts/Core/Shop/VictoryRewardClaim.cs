using System;

/// <summary>A single victory can pay either its base reward or its earned ad reward once.</summary>
public sealed class VictoryRewardClaim
{
    public const int BaseAmount = 100;
    public bool Claimed { get; private set; }
    public bool AdPending { get; private set; }
    public int AwardedAmount { get; private set; }

    public bool TryClaimBase(Action<int> award)
    {
        if (Claimed || AdPending) return false;
        return Claim(BaseAmount, award);
    }

    public bool TryBeginAd()
    {
        if (Claimed || AdPending) return false;
        AdPending = true;
        return true;
    }

    public bool CompleteAd(bool earned, Action<int> award)
    {
        if (!AdPending) return false;
        AdPending = false;
        return earned && !Claimed && Claim(BaseAmount * 2, award);
    }

    public void CancelAd() => AdPending = false;

    private bool Claim(int amount, Action<int> award)
    {
        if (award == null) throw new ArgumentNullException(nameof(award));
        // Commit the guard before notifying the wallet/UI, including reentrant callbacks.
        Claimed = true;
        AwardedAmount = amount;
        award(amount);
        return true;
    }
}
