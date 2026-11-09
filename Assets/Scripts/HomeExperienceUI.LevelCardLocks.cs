using UnityEngine;

public sealed partial class HomeExperienceUI
{
    private GameObject[] specialLevelCardLocks;

    private void BindSpecialLevelCardLocks()
    {
        specialLevelCardLocks = new GameObject[SpecialLevelThresholds.Length];
        for (int i = 0; i < SpecialLevelThresholds.Length; i++)
        {
            Transform card = SceneNode("LOCKED_" + SpecialLevelThresholds[i], tabPages[0].transform);
            Transform icon = card != null ? card.Find("Lock (1)") ?? card.Find("Lock") : null;
            specialLevelCardLocks[i] = icon != null ? icon.gameObject : null;
        }
        RefreshSpecialLevelCardLocks(GetCurrentPlayerLevel());
    }

    private void RefreshSpecialLevelCardLocks(int currentLevel)
    {
        if (specialLevelCardLocks == null) return;
        for (int i = 0; i < specialLevelCardLocks.Length; i++)
        {
            GameObject icon = specialLevelCardLocks[i];
            bool locked = currentLevel < SpecialLevelThresholds[i];
            if (icon != null && icon.activeSelf != locked) icon.SetActive(locked);
        }
    }
}
