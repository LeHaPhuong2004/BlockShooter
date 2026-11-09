using System;

/// <summary>UTC-based heart recovery, independent of scene lifetime and Time.timeScale.</summary>
[Serializable]
public sealed class HeartState
{
    public const int MaxHearts = 5;
    public const int RefillSeconds = 15 * 60;
    public const int UnlimitedSeconds = 30 * 60;

    public int hearts = MaxHearts;
    public long nextHeartAt;
    public long unlimitedUntil;

    public bool IsUnlimited(long now) => unlimitedUntil > now;

    public bool Advance(long now)
    {
        int oldHearts = hearts;
        long oldNext = nextHeartAt;
        long oldUnlimited = unlimitedUntil;
        // Purchased hearts may exceed the natural regeneration limit.
        hearts = Math.Max(0, hearts);
        if (unlimitedUntil <= now) unlimitedUntil = 0;
        if (hearts >= MaxHearts) nextHeartAt = 0;
        else if (nextHeartAt <= 0) nextHeartAt = now + RefillSeconds;
        else if (now >= nextHeartAt)
        {
            long earned = 1 + (now - nextHeartAt) / RefillSeconds;
            hearts += (int)Math.Min(MaxHearts - hearts, earned);
            nextHeartAt = hearts >= MaxHearts ? 0 : nextHeartAt + earned * RefillSeconds;
        }
        return oldHearts != hearts || oldNext != nextHeartAt || oldUnlimited != unlimitedUntil;
    }

    public bool LoseHeart(long now)
    {
        Advance(now);
        if (IsUnlimited(now) || hearts <= 0) return false;
        hearts--;
        if (hearts < MaxHearts && nextHeartAt == 0) nextHeartAt = now + RefillSeconds;
        return true;
    }

    public void AddHearts(long now, int amount)
    {
        if (amount <= 0) return;
        Advance(now);
        hearts = checked(hearts + amount);
        if (hearts >= MaxHearts) nextHeartAt = 0;
    }

    public void ActivateUnlimited(long now, int seconds)
    {
        Advance(now);
        if (seconds > 0) unlimitedUntil = Math.Max(now, unlimitedUntil) + seconds;
    }

    public int SecondsToNextHeart(long now)
    {
        Advance(now);
        return hearts >= MaxHearts ? 0 : (int)Math.Max(0, nextHeartAt - now);
    }

    public string GetTimerText(long now)
    {
        Advance(now);
        if (IsUnlimited(now))
        {
            long remaining = unlimitedUntil - now;
            string minutesAndSeconds = ((remaining / 60) % 60).ToString("00")
                + ":" + (remaining % 60).ToString("00");
            return remaining >= 60 * 60
                ? (remaining / (60 * 60)).ToString("00") + ":" + minutesAndSeconds
                : minutesAndSeconds;
        }
        if (hearts >= MaxHearts) return "FULL";
        long seconds = Math.Max(0, nextHeartAt - now);
        return (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
    }
}
