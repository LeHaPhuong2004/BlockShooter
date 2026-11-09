using UnityEngine;
using UnityEngine.UI;

public sealed partial class HomeExperienceUI
{
    private static Sprite achievementDarkProgressTrackSprite;
    private static Sprite achievementRewardCapsuleSprite;

    private GameObject BuildAchievementsPage(Transform parent)
    {
        var page = new GameObject("Achievements Page", typeof(RectTransform));
        page.transform.SetParent(parent, false);
        Stretch(page.GetComponent<RectTransform>());
        var content = new GameObject("Achievements Aspect Content", typeof(RectTransform));
        content.transform.SetParent(page.transform, false);
        Stretch(content.GetComponent<RectTransform>());
        var aspect = content.AddComponent<AspectRatioFitter>();
        aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        aspect.aspectRatio = 941f / 1672f;

        BuildLavenderHeaderBanner(page.transform, "THÀNH TÍCH",
            Resources.Load<Sprite>("UI/TabIcons/Achievements"), "Achievements Trophy Header Icon", false);
        BuildAchievementSummary(content.transform);

        var scroll = BuildAchievementList(content.transform);
        AddAchievementScrollHint(content.transform);
        for (int index = 0; index < AchievementManager.Definitions.Count; index++)
        {
            var definition = AchievementManager.Definitions[index];
            var card = AddAchievementCard(scroll.content, definition.Title,
                "0/" + AchievementNumber(definition.Target), 0f, definition.Reward,
                AchievementArtwork(definition.Type), false, true,
                new RectInt(0, 0, 455, 435));
            ConfigureAchievementCard(card.transform, definition, index);
        }
        return page;
    }

    private void BuildAchievementSummary(Transform parent)
    {
        var summary = CreateAchievementImage(parent, "Overall Achievement Progress Panel",
            Resources.Load<Sprite>("UI/DailyMissions/DailyLoginPanelBackground"),
            new RectInt(47, 390, 846, 190), false, Image.Type.Sliced);

        Sprite sparkle = Resources.Load<Sprite>("UI/DailyMissions/DailyLoginSparkle");
        var trophyGlow = CreateAchievementImage(summary.transform, "Overall Trophy Glow", cardGlowSprite,
            new Vector2(0.20f, 0.50f), new Vector2(0.40f, 0.90f), true, Image.Type.Simple);
        trophyGlow.GetComponent<Image>().color = new Color32(255, 191, 37, 190);
        CreateAchievementImage(summary.transform, "Trophy Sparkle Left", sparkle,
            new Vector2(0.055f, 0.47f), new Vector2(0.065f, 0.21f), true, Image.Type.Simple);
        CreateAchievementImage(summary.transform, "Trophy Sparkle Lower Left", sparkle,
            new Vector2(0.105f, 0.25f), new Vector2(0.052f, 0.16f), true, Image.Type.Simple);
        CreateAchievementImage(summary.transform, "Trophy Sparkle Upper Right", sparkle,
            new Vector2(0.375f, 0.68f), new Vector2(0.052f, 0.17f), true, Image.Type.Simple);
        CreateAchievementImage(summary.transform, "Trophy Sparkle Right", sparkle,
            new Vector2(0.355f, 0.39f), new Vector2(0.052f, 0.17f), true, Image.Type.Simple);
        var trophy = CreateAchievementImage(summary.transform, "Overall Trophy Artwork",
            Resources.Load<Sprite>("UI/TabIcons/Achievements"),
            new Vector2(0.20f, 0.50f), new Vector2(0.30f, 0.81f), true, Image.Type.Simple);
        trophy.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0f, 0f, -6f);
        AddAchievementText(summary.transform, "Overall Achievement Count Current", "0", 86,
            new Vector2(0.57f, 0.74f), new Vector2(0.19f, 0.37f),
            new Color32(255, 211, 31, 255), TextAnchor.MiddleRight, true);
        AddAchievementText(summary.transform, "Overall Achievement Count Total", "/10", 86,
            new Vector2(0.755f, 0.74f), new Vector2(0.20f, 0.37f),
            Color.white, TextAnchor.MiddleLeft, true);
        AddAchievementText(summary.transform, "Overall Achievement Label", "THÀNH TÍCH", 40,
            new Vector2(0.65f, 0.45f), new Vector2(0.42f, 0.18f),
            Color.white, TextAnchor.MiddleCenter, true);

        var track = CreateAchievementImage(summary.transform, "Overall Achievement Progress Track",
            GetAchievementDarkProgressTrackSprite(),
            new Vector2(0.63f, 0.23f), new Vector2(0.62f, 0.255f), false, Image.Type.Sliced);
        track.GetComponent<Image>().color = new Color32(47, 23, 111, 255);
        AddProgressTrackOutline(track);
        var fillObject = new GameObject("Overall Achievement Progress Fill", typeof(RectTransform), typeof(Image));
        fillObject.transform.SetParent(track.transform, false);
        var fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0.025f, 0.16f);
        fillRect.anchorMax = new Vector2(0.025f, 0.84f);
        fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
        var fillImage = fillObject.GetComponent<Image>();
        fillImage.sprite = Resources.Load<Sprite>("UI/Achievements/AchievementProgressFillGold");
        fillImage.color = Color.white;
        fillImage.type = Image.Type.Sliced;
        fillImage.raycastTarget = false;
    }

    private GameObject AddAchievementCard(Transform parent, string title, string progress, float progressValue,
        int reward, Sprite artwork, bool claimed, bool locked, RectInt sourceRect)
    {
        var card = CreateAchievementImage(parent, "Achievement Card - " + title,
            Resources.Load<Sprite>("UI/Achievements/AchievementCardFrameNeon"),
            sourceRect, false, Image.Type.Simple);
        card.AddComponent<RectMask2D>();

        // The neon frame artwork reaches almost to the sprite edges. Keep all card
        // contents under an inset root so the glow, coin badge, and status icons
        // stay inside the visible frame instead of being aligned to its outer glow.
        var contentRoot = new GameObject("Achievement Card Content", typeof(RectTransform));
        contentRoot.transform.SetParent(card.transform, false);
        var contentRect = contentRoot.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0.04f, 0.055f);
        contentRect.anchorMax = new Vector2(0.96f, 0.945f);
        contentRect.offsetMin = contentRect.offsetMax = Vector2.zero;

        if (artwork == null) artwork = Resources.Load<Sprite>("UI/TabIcons/Achievements");
        AddLockedLevelCardGlow(contentRoot.transform, "Achievement " + title, reward,
            new Vector2(0.5f, 0.73f), new Vector2(0.76f, 0.50f), true, true);
        CreateAchievementImage(contentRoot.transform, "Achievement Artwork", artwork,
            new Vector2(0.5f, 0.73f), new Vector2(0.76f, 0.50f), true, Image.Type.Simple);

        AddAchievementText(contentRoot.transform, "Achievement Name", title, 35,
            new Vector2(0.5f, 0.425f), new Vector2(0.94f, 0.14f),
            Color.white, TextAnchor.MiddleCenter, true);

        var progressTrackObject = CreateAchievementImage(contentRoot.transform, "Achievement Progress Track",
            GetAchievementDarkProgressTrackSprite(),
            new Vector2(0.5f, 0.30f), new Vector2(0.90f, 0.15f), false, Image.Type.Sliced);
        progressTrackObject.GetComponent<Image>().color = new Color32(47, 23, 111, 255);
        AddProgressTrackOutline(progressTrackObject);
        var progressFillObject = new GameObject("Achievement Progress Fill", typeof(RectTransform), typeof(Image));
        progressFillObject.transform.SetParent(progressTrackObject.transform, false);
        var fillRect = progressFillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0.025f, 0.14f);
        fillRect.anchorMax = new Vector2(0.025f + Mathf.Clamp01(progressValue) * 0.95f, 0.86f);
        fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
        var fillImage = progressFillObject.GetComponent<Image>();
        fillImage.sprite = Resources.Load<Sprite>("UI/DailyMissions/DailyMissionProgressFillGreen");
        fillImage.type = Image.Type.Sliced;
        fillImage.color = Color.white;
        fillImage.raycastTarget = false;

        AddAchievementText(progressTrackObject.transform, "Achievement Progress Text", progress, 30,
            new Vector2(0.5f, 0.5f), new Vector2(0.96f, 1f), Color.white, TextAnchor.MiddleCenter, true);

        var rewardBadge = CreateAchievementImage(contentRoot.transform, "Achievement Coin Reward Badge",
            GetAchievementRewardCapsuleSprite(), new Vector2(0.30f, 0.175f), new Vector2(0.44f, 0.15f), false, Image.Type.Sliced);
        var rewardBadgeImage = rewardBadge.GetComponent<Image>();
        rewardBadgeImage.color = new Color32(250, 237, 255, 255);
        var rewardOutline = rewardBadge.AddComponent<Outline>();
        rewardOutline.effectColor = new Color32(199, 151, 255, 240);
        rewardOutline.effectDistance = new Vector2(1.5f, -1.5f);

        var rewardCoin = CreateAchievementImage(rewardBadge.transform, "Reward Coin Icon", coinSprite,
            new Vector2(0.20f, 0.5f), new Vector2(0.36f, 1.05f), true, Image.Type.Simple);
        var rewardCoinOutline = rewardCoin.AddComponent<Outline>();
        rewardCoinOutline.effectColor = Color.white;
        rewardCoinOutline.effectDistance = new Vector2(4f, -4f);
        rewardCoinOutline.useGraphicAlpha = true;

        var rewardAmount = AddAchievementText(rewardBadge.transform, "Reward Coin Amount", reward.ToString(), 38,
            new Vector2(0.64f, 0.60f), new Vector2(0.54f, 0.82f),
            new Color32(222, 26, 26, 255), TextAnchor.MiddleCenter, false);
        var rewardAmountShadow = rewardAmount.GetComponent<Shadow>();
        if (rewardAmountShadow != null) DestroyUIObject(rewardAmountShadow);

        if (claimed)
        {
            CreateAchievementImage(contentRoot.transform, "Achievement Claimed Check",
                Resources.Load<Sprite>("UI/Achievements/AchievementClaimCheck"),
                new Vector2(0.83f, 0.225f), new Vector2(0.24f, 0.28f), true, Image.Type.Simple);
        }
        else if (locked)
        {
            CreateAchievementImage(contentRoot.transform, "Achievement Lock Icon", lockSprite,
                new Vector2(0.84f, 0.22f), new Vector2(0.16f, 0.22f), true, Image.Type.Simple);
        }
        return card;
    }

    private static Sprite GetAchievementDarkProgressTrackSprite()
    {
        if (achievementDarkProgressTrackSprite == null)
            achievementDarkProgressTrackSprite = CreatePanelSprite(30);
        return achievementDarkProgressTrackSprite;
    }

    private static Sprite GetAchievementRewardCapsuleSprite()
    {
        if (achievementRewardCapsuleSprite == null)
            achievementRewardCapsuleSprite = CreatePanelSprite(32);
        return achievementRewardCapsuleSprite;
    }

    private static void AddProgressTrackOutline(GameObject trackObject)
    {
        var outline = trackObject.GetComponent<Outline>();
        if (outline == null) outline = trackObject.AddComponent<Outline>();
        outline.effectColor = new Color32(18, 8, 54, 255);
        outline.effectDistance = new Vector2(2f, -2f);
        outline.useGraphicAlpha = true;
    }

    private GameObject CreateAchievementImage(Transform parent, string name, Sprite sprite, RectInt sourceRect,
        bool preserveAspect, Image.Type imageType, bool interactable = false)
    {
        var imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        SetAchievementSourceRect(imageObject.GetComponent<RectTransform>(), sourceRect);
        ConfigureAchievementImage(imageObject, sprite, preserveAspect, imageType, interactable);
        return imageObject;
    }

    private GameObject CreateAchievementImage(Transform parent, string name, Sprite sprite, Vector2 center,
        Vector2 size, bool preserveAspect, Image.Type imageType, bool interactable = false)
    {
        var imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        var rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = center - size * 0.5f;
        rect.anchorMax = center + size * 0.5f;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        ConfigureAchievementImage(imageObject, sprite, preserveAspect, imageType, interactable);
        return imageObject;
    }

    private void ConfigureAchievementImage(GameObject imageObject, Sprite sprite, bool preserveAspect,
        Image.Type imageType, bool interactable)
    {
        var image = imageObject.GetComponent<Image>();
        image.sprite = sprite != null ? sprite : panelSprite;
        image.type = imageType;
        image.preserveAspect = preserveAspect;
        image.color = Color.white;
        image.raycastTarget = interactable;
        if (!interactable) return;

        var button = imageObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
    }

    private void SetAchievementSourceRect(RectTransform rect, RectInt sourceRect)
    {
        rect.anchorMin = new Vector2(sourceRect.x / 941f, 1f - (sourceRect.y + sourceRect.height) / 1672f);
        rect.anchorMax = new Vector2((sourceRect.x + sourceRect.width) / 941f, 1f - sourceRect.y / 1672f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private Text AddAchievementText(Transform parent, string objectName, string text, int size,
        Vector2 center, Vector2 dimensions, Color color, TextAnchor alignment, bool outline = false)
    {
        var label = Label(parent, text, size, center, dimensions, color, FontStyle.Bold);
        label.gameObject.name = objectName;
        label.alignment = alignment;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = Mathf.Max(20, size - 12);
        label.resizeTextMaxSize = size;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;
        if (roundedFont != null) label.font = roundedFont;
        var shadow = label.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color32(25, 8, 108, 225);
        shadow.effectDistance = new Vector2(1f, -3f);
        if (outline)
        {
            var textOutline = label.gameObject.AddComponent<Outline>();
            textOutline.effectColor = new Color32(42, 13, 147, 240);
            textOutline.effectDistance = new Vector2(2f, -2f);
        }
        return label;
    }

}
