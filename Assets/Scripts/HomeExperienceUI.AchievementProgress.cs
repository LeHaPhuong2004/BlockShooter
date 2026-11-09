using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class HomeExperienceUI
{
    private const string AchievementCardPrefix = "Achievement Card - ";
    private const string AchievementClaimPrefix = "CLAIM_ACHIEVEMENT_";
    private const int AchievementListHeight = 2199;
    private readonly List<AchievementCardView> achievementCards = new List<AchievementCardView>();
    private Text achievementCountCurrent;
    private Text achievementCountTotal;
    private RectTransform achievementSummaryFill;
    private RectTransform achievementRewardSource;

    private sealed class AchievementCardView
    {
        public AchievementDefinition definition;
        public Text title;
        public Text progress;
        public Text reward;
        public RectTransform fill;
        public Button claim;
        public Text claimLabel;
        public GameObject claimedCheck;
        public GameObject lockedIcon;
    }

    private static string AchievementNumber(int value) =>
        value.ToString("N0", CultureInfo.InvariantCulture).Replace(",", ".");

    private Sprite AchievementArtwork(AchievementType type)
    {
        switch (type)
        {
            case AchievementType.BreakBlocks: return cubesSprite;
            case AchievementType.UseBoosters:
                return Resources.Load<Sprite>("UI/DailyMissions/DailyTaskIconBoosters5");
            default: return Resources.Load<Sprite>("UI/Achievements/AchievementWin50");
        }
    }

    private ScrollRect BuildAchievementList(Transform parent)
    {
        var viewport = BuildShopViewport(parent, "Achievement List",
            new Vector2(0f, 206f / 1672f), new Vector2(1f, 1082f / 1672f), out var scroll);
        viewport.GetComponent<Image>().color = Color.clear;
        scroll.scrollSensitivity = 45f;
        ConfigureAchievementList(scroll);
        return scroll;
    }

    private void ConfigureAchievementList(ScrollRect scroll)
    {
        var nativeScroll = SceneComponent<ScrollRect>("Gold Shop Viewport");
        var fallback = Resources.Load<Sprite>("UI/ShopItems/ShopCardFrame");
        var viewportImage = scroll.viewport.GetComponent<Image>();
        var nativeViewportImage = nativeScroll != null && nativeScroll.viewport != null
            ? nativeScroll.viewport.GetComponent<Image>() : null;
        if (viewportImage != null)
        {
            viewportImage.enabled = true;
            viewportImage.sprite = nativeViewportImage != null && nativeViewportImage.sprite != null
                ? nativeViewportImage.sprite : fallback;
            viewportImage.raycastTarget = true;
        }
        if (scroll.verticalScrollbar != null)
        {
            var scrollbarRect = scroll.verticalScrollbar.GetComponent<RectTransform>();
            scrollbarRect.anchorMin = new Vector2(0.97f, 0.025f);
            scrollbarRect.anchorMax = new Vector2(0.998f, 0.975f);
            scrollbarRect.offsetMin = scrollbarRect.offsetMax = Vector2.zero;
            scroll.verticalScrollbar.gameObject.SetActive(true);
            scroll.verticalScrollbar.transition = Selectable.Transition.None;
            var track = scroll.verticalScrollbar.GetComponent<Image>();
            var nativeTrack = nativeScroll != null && nativeScroll.verticalScrollbar != null
                ? nativeScroll.verticalScrollbar.GetComponent<Image>() : null;
            if (track != null)
            {
                track.sprite = nativeTrack != null && nativeTrack.sprite != null ? nativeTrack.sprite : fallback;
                track.color = new Color32(10, 4, 45, 230);
            }
            var handle = scroll.verticalScrollbar.handleRect != null
                ? scroll.verticalScrollbar.handleRect.GetComponent<Image>() : null;
            var nativeHandle = nativeScroll != null && nativeScroll.verticalScrollbar != null &&
                nativeScroll.verticalScrollbar.handleRect != null
                ? nativeScroll.verticalScrollbar.handleRect.GetComponent<Image>() : null;
            if (handle != null)
            {
                handle.sprite = nativeHandle != null && nativeHandle.sprite != null ? nativeHandle.sprite : fallback;
                handle.color = new Color32(102, 244, 255, 255);
            }
            scroll.verticalScrollbar.transform.SetAsLastSibling();
        }
        scroll.enabled = true;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        var content = scroll.content;
        var aspect = content.GetComponent<AspectRatioFitter>();
        if (aspect == null) aspect = content.gameObject.AddComponent<AspectRatioFitter>();
        aspect.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;
        aspect.aspectRatio = 941f / AchievementListHeight;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 45f;
        scroll.StopMovement();
        content.anchoredPosition = Vector2.zero;
        scroll.verticalNormalizedPosition = 1f;
    }

    private void AddAchievementScrollHint(Transform parent)
    {
        if (SceneComponent<Text>("Achievement Scroll Hint", parent) != null) return;
        AddAchievementText(parent, "Achievement Scroll Hint", "VUỐT LÊN/XUỐNG ĐỂ XEM ĐỦ 10 THẺ", 24,
            new Vector2(0.5f, 0.108f), new Vector2(0.92f, 0.032f),
            new Color32(232, 244, 255, 255), TextAnchor.MiddleCenter, true);
    }

    private void ConfigureAchievementCard(Transform card, AchievementDefinition definition, int index)
    {
        card.name = AchievementCardPrefix + definition.Id;
        card.gameObject.SetActive(true);
        var rect = (RectTransform)card;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        SetDailyMissionChildRect(rect, new RectInt(0, 0, 941, AchievementListHeight),
            new RectInt(index % 2 == 0 ? 12 : 474, index / 2 * 441, 455, 435));
        var content = SceneNode("Achievement Card Content", card);
        if (content == null) throw new InvalidOperationException("The achievement card content is missing.");

        var artwork = SceneComponent<Image>("Achievement Artwork", card);
        if (artwork != null) artwork.sprite = AchievementArtwork(definition.Type);
        var title = SceneComponent<Text>("Achievement Name", card);
        if (title != null) title.text = Localize.Translate(definition.Title);
        var reward = SceneComponent<Text>("Reward Coin Amount", card);
        if (reward != null) reward.text = AchievementNumber(definition.Reward);

        var claim = SceneComponent<Button>(AchievementClaimPrefix + definition.Id, card);
        if (claim == null)
            foreach (var existing in card.GetComponentsInChildren<Button>(true))
                if (existing.name.StartsWith(AchievementClaimPrefix, StringComparison.Ordinal))
                {
                    claim = existing;
                    claim.name = AchievementClaimPrefix + definition.Id;
                    break;
                }
        if (claim == null)
        {
            // Existing cards have no controls. Add a separate native button beside
            // the original coin capsule so its reward artwork remains intact.
            var claimObject = CreateAchievementImage(content, AchievementClaimPrefix + definition.Id,
                Resources.Load<Sprite>("UI/DailyMissions/DailyMissionClaimButton"),
                new Vector2(0.75f, 0.175f), new Vector2(0.38f, 0.17f), false, Image.Type.Sliced, true);
            claim = claimObject.GetComponent<Button>();
            AddAchievementText(claim.transform, "Label", string.Empty, 27,
                new Vector2(0.39f, 0.5f), new Vector2(0.73f, 0.96f), Color.white,
                TextAnchor.MiddleCenter, true);
        }
        claim.enabled = true;
        claim.transition = Selectable.Transition.None;

        var check = SceneNode("Achievement Claimed Check", card);
        if (check == null)
            check = CreateAchievementImage(content, "Achievement Claimed Check",
                Resources.Load<Sprite>("UI/Achievements/AchievementClaimCheck"),
                Vector2.one * 0.5f, Vector2.one, true, Image.Type.Simple).transform;
        ConfigureAchievementStatusIcon(check, claim.transform);
        var locked = SceneNode("Achievement Lock Icon", card);
        if (locked == null)
            locked = CreateAchievementImage(content, "Achievement Lock Icon", lockSprite,
                Vector2.one * 0.5f, Vector2.one, true, Image.Type.Simple).transform;
        ConfigureAchievementStatusIcon(locked, claim.transform);

        foreach (var graphic in card.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
        claim.targetGraphic = claim.GetComponent<Image>();
        claim.targetGraphic.raycastTarget = true;
        ApplyAchievementCardState(CreateAchievementCardView(card, definition), 0, false, false);
    }

    private static void ConfigureAchievementStatusIcon(Transform icon, Transform claim)
    {
        icon.SetParent(claim, false);
        var rect = (RectTransform)icon;
        rect.anchorMin = new Vector2(0.77f, 0.02f);
        rect.anchorMax = new Vector2(0.98f, 0.98f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        var image = icon.GetComponent<Image>();
        if (image != null) image.preserveAspect = true;
    }

    private AchievementCardView CreateAchievementCardView(Transform card, AchievementDefinition definition)
    {
        var view = new AchievementCardView
        {
            definition = definition,
            title = SceneComponent<Text>("Achievement Name", card),
            progress = SceneComponent<Text>("Achievement Progress Text", card),
            reward = SceneComponent<Text>("Reward Coin Amount", card),
            fill = SceneComponent<RectTransform>("Achievement Progress Fill", card),
            claim = SceneComponent<Button>(AchievementClaimPrefix + definition.Id, card),
            claimedCheck = SceneObject("Achievement Claimed Check", card),
            lockedIcon = SceneObject("Achievement Lock Icon", card)
        };
        view.claimLabel = view.claim != null ? SceneComponent<Text>("Label", view.claim.transform) : null;
        var missing = new List<string>();
        if (view.title == null) missing.Add("Achievement Name");
        if (view.progress == null) missing.Add("Achievement Progress Text");
        if (view.reward == null) missing.Add("Reward Coin Amount");
        if (view.fill == null) missing.Add("Achievement Progress Fill");
        if (view.claim == null) missing.Add(AchievementClaimPrefix + definition.Id);
        if (view.claimLabel == null) missing.Add("claim Label");
        if (missing.Count > 0)
            throw new InvalidOperationException("The authored achievement card is incomplete: " + definition.Id +
                ". Missing: " + string.Join(", ", missing));
        return view;
    }

    private void BindSceneAchievements()
    {
        achievementCards.Clear();
        var page = SceneNode("Achievements Page");
        if (page == null) throw new InvalidOperationException("The authored Achievements Page is missing.");
        foreach (var definition in AchievementManager.Definitions)
        {
            var card = SceneNode(AchievementCardPrefix + definition.Id, page);
            if (card == null) throw new InvalidOperationException("The authored achievement card is missing: " + definition.Id);
            achievementCards.Add(CreateAchievementCardView(card, definition));
        }
        achievementCountCurrent = SceneComponent<Text>("Overall Achievement Count Current", page);
        achievementCountTotal = SceneComponent<Text>("Overall Achievement Count Total", page);
        achievementSummaryFill = SceneComponent<RectTransform>("Overall Achievement Progress Fill", page);
        if (achievementCountCurrent == null || achievementCountTotal == null || achievementSummaryFill == null)
            throw new InvalidOperationException("The authored achievement summary is incomplete.");
        RefreshAchievements();
    }

    private void RefreshAchievements()
    {
        foreach (var view in achievementCards)
            ApplyAchievementCardState(view, AchievementManager.GetProgress(view.definition.Id),
                AchievementManager.IsClaimed(view.definition.Id), AchievementManager.CanClaim(view.definition.Id));
        ApplyAchievementSummary(AchievementManager.CompletedCount);
        RefreshFooterRewardBadges();
    }

    private static void ApplyAchievementCardState(AchievementCardView view, int progress, bool claimed, bool canClaim)
    {
        int target = Mathf.Max(1, view.definition.Target);
        int displayed = Mathf.Clamp(progress, 0, target);
        float ratio = displayed / (float)target;
        view.title.text = Localize.Translate(view.definition.Title);
        view.reward.text = AchievementNumber(view.definition.Reward);
        view.progress.text = AchievementNumber(displayed) + "/" + AchievementNumber(target);
        view.fill.anchorMax = new Vector2(0.025f + ratio * 0.95f, view.fill.anchorMax.y);
        bool ready = !claimed && canClaim && progress >= target;
        view.claim.gameObject.SetActive(true);
        view.claim.interactable = ready;
        view.claimLabel.text = claimed ? Localize.Text("ĐÃ NHẬN", "CLAIMED") : ready ? Localize.Text("NHẬN", "CLAIM") : string.Empty;
        view.claimLabel.rectTransform.anchorMin = new Vector2(0.025f, 0.02f);
        view.claimLabel.rectTransform.anchorMax = new Vector2(ready ? 0.975f : 0.75f, 0.98f);
        view.claimLabel.rectTransform.offsetMin = view.claimLabel.rectTransform.offsetMax = Vector2.zero;
        var image = view.claim.GetComponent<Image>();
        if (image != null) image.color = ready ? Color.white : new Color32(147, 139, 172, 255);
        if (view.claimedCheck != null) view.claimedCheck.SetActive(claimed);
        if (view.lockedIcon != null)
        {
            var lockRect = view.lockedIcon.GetComponent<RectTransform>();
            lockRect.anchorMin = new Vector2(0.38f, 0.02f);
            lockRect.anchorMax = new Vector2(0.62f, 0.98f);
            lockRect.offsetMin = lockRect.offsetMax = Vector2.zero;
            view.lockedIcon.SetActive(!claimed && !ready);
        }
    }

    private void ApplyAchievementSummary(int completed)
    {
        int total = AchievementManager.Definitions.Count;
        completed = Mathf.Clamp(completed, 0, total);
        if (achievementCountCurrent != null) achievementCountCurrent.text = completed.ToString(CultureInfo.InvariantCulture);
        if (achievementCountTotal != null) achievementCountTotal.text = "/" + total;
        if (achievementSummaryFill != null)
            achievementSummaryFill.anchorMax = new Vector2(0.025f + (total > 0 ? completed / (float)total : 0f) * 0.95f,
                achievementSummaryFill.anchorMax.y);
    }

    private Action AchievementButtonAction(string buttonName)
    {
        if (buttonName == null || !buttonName.StartsWith(AchievementClaimPrefix, StringComparison.Ordinal)) return null;
        string id = buttonName.Substring(AchievementClaimPrefix.Length);
        foreach (var definition in AchievementManager.Definitions)
            if (string.Equals(definition.Id, id, StringComparison.Ordinal)) return () => ClaimAchievement(id);
        return null;
    }

    private void ClaimAchievement(string id)
    {
        AchievementCardView selected = achievementCards.Find(view => view.definition.Id == id);
        achievementRewardSource = selected != null ? selected.claim.transform as RectTransform : null;
        try { AchievementManager.TryClaim(id); }
        catch (Exception exception) { Debug.LogException(exception, this); }
        finally
        {
            achievementRewardSource = null;
            RefreshAchievements();
        }
    }

#if UNITY_EDITOR
    /// <summary>Authors achievement objects only; never reads or changes saved player progress.</summary>
    public void PrepareAchievementsForEditor()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Prepare achievements outside Play Mode.");
        LoadHomeResources();
        sceneHomeCanvas = SceneComponent<Canvas>("Home Canvas", transform);
        if (sceneHomeCanvas == null) throw new InvalidOperationException("The authored Home Canvas is missing.");
        homeCanvasObject = sceneHomeCanvas.gameObject;
        var page = SceneNode("Achievements Page");
        var content = SceneNode("Achievements Aspect Content", page);
        if (content == null) throw new InvalidOperationException("The authored achievement content is missing.");
        var originalCards = new List<Transform>();
        foreach (var node in content.GetComponentsInChildren<Transform>(true))
            if (node.name.StartsWith(AchievementCardPrefix, StringComparison.Ordinal)) originalCards.Add(node);
        if (originalCards.Count != 4 && originalCards.Count != AchievementManager.Definitions.Count)
            throw new InvalidOperationException("Expected four original cards or ten authored achievement cards.");
        var scroll = SceneComponent<ScrollRect>("Achievement List", content);
        if (scroll == null) scroll = BuildAchievementList(content);
        else ConfigureAchievementList(scroll);
        AddAchievementScrollHint(content);
        var assigned = new HashSet<Transform>();
        for (int index = 0; index < AchievementManager.Definitions.Count; index++)
        {
            var definition = AchievementManager.Definitions[index];
            Transform card = originalCards.Find(node => node.name == AchievementCardPrefix + definition.Id);
            if (card == null && index < originalCards.Count && !assigned.Contains(originalCards[index]))
                card = originalCards[index];
            if (card == null)
                card = Instantiate(originalCards[0].gameObject, scroll.content, false).transform;
            card.SetParent(scroll.content, false);
            card.SetSiblingIndex(index);
            assigned.Add(card);
            ConfigureAchievementCard(card, definition, index);
        }
        achievementCountCurrent = SceneComponent<Text>("Overall Achievement Count Current", page);
        achievementCountTotal = SceneComponent<Text>("Overall Achievement Count Total", page);
        achievementSummaryFill = SceneComponent<RectTransform>("Overall Achievement Progress Fill", page);
        ApplyAchievementSummary(0);
        scroll.content.anchoredPosition = Vector2.zero;
        scroll.verticalNormalizedPosition = 1f;
    }
#endif
}
