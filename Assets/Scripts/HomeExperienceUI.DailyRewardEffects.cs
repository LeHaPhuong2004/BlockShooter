using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class HomeExperienceUI
{
    private sealed class DailyRewardGlowState
    {
        public RewardIconGlow glow;
        public Graphic source;
        public GameObject checkmark;
        public Button claimButton;
    }

    private readonly List<DailyRewardGlowState> dailyRewardGlows = new List<DailyRewardGlowState>();
    private bool dailyRewardGlowsBound;

    private void BindDailyRewardGlows()
    {
        if (tabPages == null || tabPages.Length <= 2 || tabPages[2] == null) return;
        dailyRewardGlowsBound = true;
        dailyRewardGlows.Clear();
        foreach (RewardIconGlow glow in tabPages[2].GetComponentsInChildren<RewardIconGlow>(true))
        {
            Graphic source = glow.RewardSource;
            if (source == null) continue;
            Transform parent = source.transform.parent;
            GameObject checkmark = parent != null && parent.name.StartsWith("Daily Login Day ", System.StringComparison.Ordinal)
                ? FindDailyLoginState(parent, "checkmark") : null;
            dailyRewardGlows.Add(new DailyRewardGlowState
            {
                glow = glow, source = source, checkmark = checkmark,
                claimButton = source.GetComponent<Button>()
            });
        }
        RefreshDailyRewardGlows(Time.unscaledTime);
    }

    private void UpdateDailyRewardGlows()
    {
        if (!dailyRewardGlowsBound) BindDailyRewardGlows();
        if (activeTabIndex == 2) RefreshDailyRewardGlows(Time.unscaledTime);
    }

    private void RefreshDailyRewardGlows(float unscaledTime)
    {
        foreach (DailyRewardGlowState state in dailyRewardGlows)
        {
            if (state.glow == null || state.source == null) continue;
            bool visible = state.source.isActiveAndEnabled && (state.checkmark == null || !state.checkmark.activeSelf);
            if (state.glow.enabled != visible) state.glow.enabled = visible;
            if (!visible) continue;
            float alpha = state.claimButton != null && !state.claimButton.interactable ? .55f : 1f;
            Color tint = new Color(1f, 1f, 1f, alpha);
            if (state.glow.color != tint) state.glow.color = tint;
            state.glow.SetTime(unscaledTime);
        }
    }

#if UNITY_EDITOR
    public void PrepareDailyRewardGlowsForEditor()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Author daily reward effects outside Play Mode.");
        Transform page = SceneNode("Daily Missions Page");
        if (page == null) return;
        var sources = new List<Image>();
        Transform panel = SceneNode("Daily Login Reward Panel", page);
        if (panel != null)
            foreach (Image image in panel.GetComponentsInChildren<Image>(true))
            {
                bool ready = image.name.Equals("ready", System.StringComparison.OrdinalIgnoreCase) ||
                    image.name.Equals("chest", System.StringComparison.OrdinalIgnoreCase);
                bool oldIcon = image.name.StartsWith("Day ", System.StringComparison.Ordinal) &&
                    image.name.EndsWith(" Chest Icon", System.StringComparison.Ordinal);
                if (ready || oldIcon || image.name == "Daily Login Main Chest" || image.name == "Daily Login Day 7 Chest")
                    sources.Add(image);
            }
        foreach (Image image in page.GetComponentsInChildren<Image>(true))
            if (image.name.EndsWith(" Reward Coin Icon", System.StringComparison.Ordinal)) sources.Add(image);

        for (int index = 0; index < sources.Count; index++)
        {
            Image source = sources[index];
            string name = source.name + " Golden Glow";
            Transform existing = source.transform.parent.Find(name);
            GameObject effect = existing != null ? existing.gameObject
                : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RewardIconGlow));
            var rect = (RectTransform)effect.transform;
            rect.SetParent(source.transform.parent, false);
            RectTransform sourceRect = source.rectTransform;
            rect.anchorMin = sourceRect.anchorMin;
            rect.anchorMax = sourceRect.anchorMax;
            rect.pivot = sourceRect.pivot;
            rect.sizeDelta = sourceRect.sizeDelta;
            rect.anchoredPosition3D = sourceRect.anchoredPosition3D;
            rect.localRotation = sourceRect.localRotation;
            rect.localScale = sourceRect.localScale * 1.35f;
            int sourceIndex = source.transform.GetSiblingIndex();
            rect.SetSiblingIndex(sourceIndex - (rect.GetSiblingIndex() < sourceIndex ? 1 : 0));
            RewardIconGlow glow = effect.GetComponent<RewardIconGlow>();
            if (glow == null) glow = effect.AddComponent<RewardIconGlow>();
            glow.BindSource(source, index * .173f);
            glow.enabled = false;
        }
    }
#endif
}
