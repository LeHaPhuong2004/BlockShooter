using UnityEngine;
using UnityEngine.UI;

public sealed partial class HomeExperienceUI
{
    [SerializeField, HideInInspector] private RectTransform characterPreviewAnimationSource;
    [SerializeField, HideInInspector] private Vector2 characterPreviewBasePosition;
    [SerializeField, HideInInspector] private Vector3 characterPreviewBaseScale;

    private void BindCharacterPreviewAnimation()
    {
        RectTransform rect = characterPreviewImage != null ? characterPreviewImage.rectTransform : null;
        if (rect == null || characterPreviewAnimationSource == rect) return;
        characterPreviewAnimationSource = rect;
        characterPreviewBasePosition = rect.anchoredPosition;
        characterPreviewBaseScale = rect.localScale;
    }

    private void UpdateCharacterPreviewAnimation(float unscaledTime)
    {
        if (characterPreviewImage == null || !characterPreviewImage.gameObject.activeInHierarchy) return;
        BindCharacterPreviewAnimation();
        float pulse = Mathf.Sin(unscaledTime * 1.8f);
        characterPreviewAnimationSource.anchoredPosition = characterPreviewBasePosition + Vector2.up * (pulse * 10f);
        characterPreviewAnimationSource.localScale = characterPreviewBaseScale * (1f + pulse * .025f);
    }

    private GameObject BuildCharacterPage(Transform parent)
    {
        var page = new GameObject("Character Page", typeof(RectTransform));
        page.transform.SetParent(parent, false);
        Stretch(page.GetComponent<RectTransform>());
        var content = new GameObject("Character Aspect Content", typeof(RectTransform));
        content.transform.SetParent(page.transform, false);
        Stretch(content.GetComponent<RectTransform>());
        var pageAspect = content.AddComponent<AspectRatioFitter>();
        pageAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        pageAspect.aspectRatio = 941f / 1672f;

        BuildCharacterHeaderBanner(page.transform);

        var landingShadow = new GameObject("Character Rug Contact Shadow", typeof(RectTransform), typeof(Image));
        landingShadow.transform.SetParent(content.transform, false);
        var landingShadowRect = landingShadow.GetComponent<RectTransform>();
        Vector2 shadowCenter = new Vector2(0.5f, 0.433f);
        Vector2 shadowSize = new Vector2(0.56f, 0.055f);
        landingShadowRect.anchorMin = shadowCenter - shadowSize * 0.5f;
        landingShadowRect.anchorMax = shadowCenter + shadowSize * 0.5f;
        landingShadowRect.offsetMin = landingShadowRect.offsetMax = Vector2.zero;
        var landingShadowImage = landingShadow.GetComponent<Image>();
        landingShadowImage.sprite = softContactShadowSprite;
        landingShadowImage.color = new Color32(56, 16, 125, 118);
        landingShadowImage.raycastTarget = false;

        var heroObject = new GameObject("Character Preview Sprite", typeof(RectTransform), typeof(Image));
        heroObject.transform.SetParent(content.transform, false);
        var heroRect = heroObject.GetComponent<RectTransform>();
        Vector2 heroCenter = new Vector2(0.5f, 0.60f);
        Vector2 heroSize = new Vector2(0.94f, 0.41f);
        heroRect.anchorMin = heroCenter - heroSize * 0.5f;
        heroRect.anchorMax = heroCenter + heroSize * 0.5f;
        heroRect.offsetMin = heroRect.offsetMax = Vector2.zero;
        characterPreviewImage = heroObject.GetComponent<Image>();
        characterPreviewImage.sprite = GetCharacterSprite(previewCharacterIndex);
        characterPreviewImage.preserveAspect = true;
        characterPreviewImage.raycastTarget = false;

        var namePlate = Panel(content.transform, "Character Name Capsule", new Color32(226, 194, 255, 255),
            new Vector2(0.255f, 0.389f), new Vector2(0.745f, 0.433f));
        var namePlateImage = namePlate.GetComponent<Image>();
        namePlateImage.sprite = CreatePanelSprite(32);
        namePlateImage.type = Image.Type.Sliced;
        namePlateImage.raycastTarget = false;
        characterPreviewName = Label(namePlate.transform, CharacterDisplayNames[previewCharacterIndex], 50,
            new Vector2(0.5f, 0.5f), new Vector2(0.92f, 0.88f), new Color32(31, 25, 196, 255), FontStyle.Bold);
        if (roundedFont != null) characterPreviewName.font = roundedFont;
        characterPreviewName.resizeTextForBestFit = true;
        characterPreviewName.resizeTextMinSize = 32;
        characterPreviewName.resizeTextMaxSize = 50;
        characterPreviewName.raycastTarget = false;

        characterActionButton = Button(content.transform, "CHARACTER_ACTION", "SỬ DỤNG",
            new Vector2(0.5f, 0.354f), new Vector2(0.50f, 0.062f), Color.white, 48, ActivateCharacterAction);
        var actionImage = characterActionButton.GetComponent<Image>();
        actionImage.sprite = characterActionButtonSprite != null ? characterActionButtonSprite : shopBuyFaceSprite;
        actionImage.type = Image.Type.Simple;
        actionImage.preserveAspect = false;
        actionImage.color = Color.white;
        characterActionButton.transition = Selectable.Transition.None;
        var actionOutline = characterActionButton.GetComponent<Outline>();
        if (actionOutline != null) DestroyUIObject(actionOutline);
        var actionShadow = characterActionButton.GetComponent<Shadow>();
        if (actionShadow != null) DestroyUIObject(actionShadow);
        characterActionLabel = characterActionButton.transform.Find("Label").GetComponent<Text>();
        if (roundedFont != null) characterActionLabel.font = roundedFont;
        characterActionLabel.fontSize = 48;
        characterActionLabel.resizeTextMinSize = 34;
        characterActionLabel.resizeTextMaxSize = 48;
        characterActionLabel.color = Color.white;
        characterActionLabel.raycastTarget = false;

        var coinIconObject = new GameObject("Character Action Coin Icon", typeof(RectTransform), typeof(Image));
        coinIconObject.transform.SetParent(characterActionButton.transform, false);
        var coinIconRect = coinIconObject.GetComponent<RectTransform>();
        coinIconRect.anchorMin = new Vector2(0.12f, 0.12f);
        coinIconRect.anchorMax = new Vector2(0.29f, 0.88f);
        coinIconRect.offsetMin = coinIconRect.offsetMax = Vector2.zero;
        characterActionCoinIcon = coinIconObject.GetComponent<Image>();
        characterActionCoinIcon.sprite = coinSprite;
        characterActionCoinIcon.preserveAspect = true;
        characterActionCoinIcon.raycastTarget = false;
        characterActionCoinIcon.gameObject.SetActive(false);

        characterCardFrames = new Image[4];
        characterCardPortraits = new Image[4];
        characterCardCheckMarks = new GameObject[4];
        characterCardPrices = new GameObject[4];
        characterCardLocks = new GameObject[4];
        characterCardUseBadges = new GameObject[4];
        float[] cardCenters = { 0.19f, 0.40f, 0.61f, 0.82f };
        for (int i = 0; i < cardCenters.Length; i++)
            AddCharacterSkinCard(content.transform, i, cardCenters[i]);

        var previous = Button(content.transform, "CHARACTER_PREVIOUS", "‹",
            new Vector2(0.045f, 0.238f), new Vector2(0.078f, 0.062f), new Color32(87, 35, 222, 255), 60,
            () => CycleCharacterPreview(-1));
        var next = Button(content.transform, "CHARACTER_NEXT", "›",
            new Vector2(0.955f, 0.238f), new Vector2(0.078f, 0.062f), new Color32(87, 35, 222, 255), 60,
            () => CycleCharacterPreview(1));
        StyleCharacterArrow(previous, characterArrowLeftSprite);
        StyleCharacterArrow(next, characterArrowRightSprite);
        UpdateCharacterPageState();
        return page;
    }

    private void BuildCharacterHeaderBanner(Transform parent)
    {
        BuildLavenderHeaderBanner(parent, "NHÂN VẬT", characterCardSprite, "Character Header Icon",
            false);
    }

    private void AddCharacterSkinCard(Transform parent, int index, float centerX)
    {
        var cardObject = new GameObject("Character Skin " + index, typeof(RectTransform), typeof(Image), typeof(Button));
        cardObject.transform.SetParent(parent, false);
        var cardRect = cardObject.GetComponent<RectTransform>();
        Vector2 center = new Vector2(centerX, 0.238f);
        Vector2 size = new Vector2(0.214f, 0.16f);
        cardRect.anchorMin = center - size * 0.5f;
        cardRect.anchorMax = center + size * 0.5f;
        cardRect.offsetMin = cardRect.offsetMax = Vector2.zero;
        var hitImage = cardObject.GetComponent<Image>();
        hitImage.color = new Color(1f, 1f, 1f, 0f);
        var cardButton = cardObject.GetComponent<Button>();
        cardButton.targetGraphic = hitImage;
        cardButton.transition = Selectable.Transition.None;
        cardButton.onClick.AddListener(PlayButtonSfx);
        cardButton.onClick.AddListener(() => SelectCharacterPreview(index));
        AddButtonFeedback(cardButton);

        var frameObject = new GameObject("Card Frame Sprite", typeof(RectTransform), typeof(Image));
        frameObject.transform.SetParent(cardObject.transform, false);
        Stretch(frameObject.GetComponent<RectTransform>());
        var frameImage = frameObject.GetComponent<Image>();
        frameImage.sprite = characterCardFrameSprites != null && index < characterCardFrameSprites.Length
            ? characterCardFrameSprites[index]
            : characterCardFrameSprite;
        frameImage.type = Image.Type.Simple;
        frameImage.preserveAspect = false;
        frameImage.color = Color.white;
        frameImage.raycastTarget = false;
        characterCardFrames[index] = frameImage;

        var portraitObject = new GameObject("Character Portrait Sprite", typeof(RectTransform), typeof(Image));
        portraitObject.transform.SetParent(cardObject.transform, false);
        var portraitRect = portraitObject.GetComponent<RectTransform>();
        portraitRect.anchorMin = new Vector2(-0.10f, 0.205f);
        portraitRect.anchorMax = new Vector2(1.10f, 0.995f);
        portraitRect.offsetMin = portraitRect.offsetMax = Vector2.zero;
        var portraitImage = portraitObject.GetComponent<Image>();
        portraitImage.sprite = characterPortraitSprites != null && index < characterPortraitSprites.Length
            ? characterPortraitSprites[index]
            : GetCharacterSprite(index);
        portraitImage.preserveAspect = true;
        portraitImage.raycastTarget = false;
        characterCardPortraits[index] = portraitImage;

        var checkObject = new GameObject("Equipped Check Sprite", typeof(RectTransform), typeof(Image));
        checkObject.transform.SetParent(cardObject.transform, false);
        var checkRect = checkObject.GetComponent<RectTransform>();
        checkRect.anchorMin = new Vector2(0.28f, 0.02f);
        checkRect.anchorMax = new Vector2(0.72f, 0.32f);
        checkRect.offsetMin = checkRect.offsetMax = Vector2.zero;
        var checkImage = checkObject.GetComponent<Image>();
        checkImage.sprite = Resources.Load<Sprite>("UI/CharacterSkins/CharacterCheckBadge");
        if (checkImage.sprite == null) checkImage.sprite = circleSprite;
        checkImage.preserveAspect = true;
        checkImage.color = Color.white;
        checkImage.raycastTarget = false;
        var checkText = Label(checkObject.transform, "✓", 48, new Vector2(0.5f, 0.5f), new Vector2(0.9f, 0.9f),
            Color.white, FontStyle.Bold);
        checkText.fontSize = 60;
        checkText.raycastTarget = false;
        checkText.gameObject.SetActive(false);
        characterCardCheckMarks[index] = checkObject;

        var price = new GameObject("Blue Skin Price Badge", typeof(RectTransform), typeof(Image));
        price.transform.SetParent(cardObject.transform, false);
        var priceRect = price.GetComponent<RectTransform>();
        priceRect.anchorMin = new Vector2(0.075f, 0.025f);
        priceRect.anchorMax = new Vector2(0.925f, 0.2825f);
        priceRect.offsetMin = priceRect.offsetMax = Vector2.zero;
        var priceImage = price.GetComponent<Image>();
        priceImage.sprite = Resources.Load<Sprite>("UI/CharacterSkins/CharacterPriceBadge200");
        priceImage.type = Image.Type.Simple;
        priceImage.preserveAspect = true;
        priceImage.color = Color.white;
        priceImage.raycastTarget = false;
        characterCardPrices[index] = price;

        var locked = Panel(cardObject.transform, "Locked Skin Badge", new Color32(75, 23, 220, 255),
            new Vector2(0.075f, 0.025f), new Vector2(0.925f, 0.275f));
        var lockedImage = locked.GetComponent<Image>();
        lockedImage.sprite = shopTabSelectedSprite;
        lockedImage.type = Image.Type.Simple;
        lockedImage.preserveAspect = false;
        lockedImage.color = Color.white;
        lockedImage.raycastTarget = false;
        var lockedOutline = locked.GetComponent<Outline>();
        if (lockedOutline != null) DestroyUIObject(lockedOutline);
        var lockObject = new GameObject("Lock Sprite", typeof(RectTransform), typeof(Image));
        lockObject.transform.SetParent(locked.transform, false);
        var lockRect = lockObject.GetComponent<RectTransform>();
        lockRect.anchorMin = new Vector2(0.34f, 0.04f);
        lockRect.anchorMax = new Vector2(0.66f, 0.96f);
        lockRect.offsetMin = lockRect.offsetMax = Vector2.zero;
        var lockImage = lockObject.GetComponent<Image>();
        lockImage.sprite = Resources.Load<Sprite>("UI/LevelCharacters/GoldLockClean");
        if (lockImage.sprite == null) lockImage.sprite = lockSprite;
        lockImage.preserveAspect = true;
        lockImage.raycastTarget = false;
        characterCardLocks[index] = locked;

        var useBadgeObject = new GameObject("Use Skin Badge", typeof(RectTransform), typeof(Image));
        useBadgeObject.transform.SetParent(cardObject.transform, false);
        var useBadgeRect = useBadgeObject.GetComponent<RectTransform>();
        useBadgeRect.anchorMin = new Vector2(0.075f, 0.025f);
        useBadgeRect.anchorMax = new Vector2(0.925f, 0.275f);
        useBadgeRect.offsetMin = useBadgeRect.offsetMax = Vector2.zero;
        var useBadgeImage = useBadgeObject.GetComponent<Image>();
        useBadgeImage.sprite = characterActionButtonSprite != null ? characterActionButtonSprite : shopBuyFaceSprite;
        useBadgeImage.type = Image.Type.Simple;
        useBadgeImage.preserveAspect = false;
        useBadgeImage.raycastTarget = false;
        var useBadgeText = Label(useBadgeObject.transform, "SỬ DỤNG", 25, new Vector2(0.5f, 0.5f),
            new Vector2(0.94f, 0.90f), Color.white, FontStyle.Bold);
        if (roundedFont != null) useBadgeText.font = roundedFont;
        useBadgeText.raycastTarget = false;
        characterCardUseBadges[index] = useBadgeObject;
    }

    private void StyleCharacterArrow(Button button, Sprite arrowSprite)
    {
        var image = button.GetComponent<Image>();
        image.sprite = arrowSprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.color = Color.white;
        image.raycastTarget = true;
        button.transition = Selectable.Transition.None;
        var buttonOutline = button.GetComponent<Outline>();
        if (buttonOutline != null) DestroyUIObject(buttonOutline);
        var buttonShadow = button.GetComponent<Shadow>();
        if (buttonShadow != null) DestroyUIObject(buttonShadow);
        var label = button.transform.Find("Label").GetComponent<Text>();
        label.gameObject.SetActive(false);
    }

    private void SelectCharacterPreview(int index)
    {
        previewCharacterIndex = Mathf.Clamp(index, 0, characterSkins.Length - 1);
        UpdateCharacterPageState();
    }

    private void CycleCharacterPreview(int direction)
    {
        int nextIndex = (previewCharacterIndex + direction + characterSkins.Length) % characterSkins.Length;
        SelectCharacterPreview(nextIndex);
    }

    private void UpdateCharacterPageState()
    {
        if (characterSkins == null || characterSkins.Length == 0) return;
        previewCharacterIndex = Mathf.Clamp(previewCharacterIndex, 0, characterSkins.Length - 1);
        if (characterPreviewImage != null) characterPreviewImage.sprite = GetCharacterSprite(previewCharacterIndex);
        if (characterPreviewName != null) characterPreviewName.text = Localize.Translate(CharacterDisplayNames[previewCharacterIndex]);

        bool unlocked = IsCharacterUnlocked(previewCharacterIndex);
        bool isEquipped = previewCharacterIndex == equippedCharacterIndex;
        if (characterActionLabel != null)
        {
            characterActionLabel.text = Localize.Translate(unlocked ? "SỬ DỤNG"
                : previewCharacterIndex == 1 ? "MUA 200"
                : "MỞ KHÓA");
            var labelRect = characterActionLabel.rectTransform;
            labelRect.anchorMin = previewCharacterIndex == 1 && !blueCharacterUnlocked
                ? new Vector2(0.30f, 0.04f) : new Vector2(0.04f, 0.04f);
            labelRect.anchorMax = new Vector2(0.96f, 0.96f);
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        }
        if (characterActionButton != null)
        {
            characterActionButton.gameObject.SetActive(!isEquipped);
            characterActionButton.interactable = !isEquipped;
        }
        if (characterActionCoinIcon != null)
            characterActionCoinIcon.gameObject.SetActive(previewCharacterIndex == 1 && !blueCharacterUnlocked && !isEquipped);

        UpdateCharacterSelectionHighlight(0f);
        if (characterCardCheckMarks == null) return;
        for (int i = 0; i < characterCardCheckMarks.Length; i++)
        {
            if (characterCardPortraits[i] != null) characterCardPortraits[i].color = Color.white;
            if (characterCardCheckMarks[i] != null) characterCardCheckMarks[i].SetActive(i == equippedCharacterIndex);
            if (characterCardPrices[i] != null) characterCardPrices[i].SetActive(i == 1 && !blueCharacterUnlocked);
            if (characterCardLocks[i] != null) characterCardLocks[i].SetActive(i >= 2);
            if (characterCardUseBadges[i] != null)
                characterCardUseBadges[i].SetActive(false);
        }
    }

    private void ActivateCharacterAction()
    {
        if (!IsCharacterUnlocked(previewCharacterIndex))
        {
            if (previewCharacterIndex == 1)
            {
                if (CurrentCoins() < BlueCharacterPrice)
                {
                    ShowCharacterMessage("CHƯA ĐỦ VÀNG", "Cần 200 vàng để mở khóa nhân vật xanh.");
                    return;
                }
                if (CurrencyManager.Instance != null)
                    CurrencyManager.Instance.SetMoney(CurrentCoins() - BlueCharacterPrice);
                else if (GameMaster.staticScript != null)
                    GameMaster.UserGold = CurrentCoins() - BlueCharacterPrice;
                blueCharacterUnlocked = true;
                PlayerPrefs.SetInt(BlueCharacterUnlockedKey, 1);
                PlayerPrefs.Save();
                UpdateCharacterPageState();
                return;
            }

            ShowCharacterMessage("NHÂN VẬT ĐANG KHÓA", "Nhân vật này sẽ được mở khóa qua sự kiện.");
            return;
        }

        equippedCharacterIndex = previewCharacterIndex;
        PlayerPrefs.SetInt(EquippedCharacterKey, equippedCharacterIndex);
        PlayerPrefs.Save();
        if (homeCharacterImage != null) homeCharacterImage.sprite = GetCharacterSprite(equippedCharacterIndex);
        UpdateCharacterPageState();
    }

    private bool IsCharacterUnlocked(int index)
    {
        return index == 0 || (index == 1 && blueCharacterUnlocked);
    }

    private Sprite GetCharacterSprite(int index)
    {
        if (characterSkins != null && characterSkins.Length > 0)
        {
            int safeIndex = Mathf.Clamp(index, 0, characterSkins.Length - 1);
            if (characterSkins[safeIndex] != null) return characterSkins[safeIndex];
            if (characterSkins[0] != null) return characterSkins[0];
        }
        return Resources.Load<Sprite>("UI/HomeRobotCharacterCutout");
    }

    private void ShowCharacterMessage(string title, string body)
    {
        ShowInlineFeedback(characterPreviewName, title);
    }

    private static Sprite CreateCharacterSkinCardFrameSprite()
    {
        const int width = 128;
        const int height = 168;
        const float cornerRadius = 18f;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "Character Skin Card Frame Sprite",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float qx = Mathf.Abs(x - (width - 1) * 0.5f) - ((width - 1) * 0.5f - cornerRadius);
                float qy = Mathf.Abs(y - (height - 1) * 0.5f) - ((height - 1) * 0.5f - cornerRadius);
                float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f))
                    + Mathf.Min(Mathf.Max(qx, qy), 0f) - cornerRadius;
                Color fill;
                float alpha;
                if (outside > 0f)
                {
                    float glow = Mathf.Clamp01(1f - outside / 5f);
                    fill = new Color32(249, 99, 255, 255);
                    alpha = glow * 0.72f;
                }
                else
                {
                    float edge = -outside;
                    float vertical = y / (float)(height - 1);
                    Color face = Color.Lerp(new Color32(68, 19, 199, 255), new Color32(111, 43, 247, 255), vertical * 0.72f + 0.12f);
                    Color outerRim = new Color32(227, 149, 255, 255);
                    Color hotRim = new Color32(255, 224, 255, 255);
                    Color innerRim = new Color32(124, 61, 255, 255);
                    fill = Color.Lerp(outerRim, face, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2f, 9f, edge)));
                    if (edge < 2.5f) fill = Color.Lerp(fill, hotRim, 0.9f);
                    else if (edge < 6f) fill = Color.Lerp(fill, new Color32(245, 113, 255, 255), 0.62f);
                    else if (edge < 9f) fill = Color.Lerp(fill, innerRim, 0.62f);

                    float star = CharacterCardStarGlow(x, y, 15f, 23f)
                        + CharacterCardStarGlow(x, y, width - 16f, 23f)
                        + CharacterCardStarGlow(x, y, 15f, height - 24f)
                        + CharacterCardStarGlow(x, y, width - 16f, height - 24f);
                    fill = Color.Lerp(fill, new Color32(255, 243, 255, 255), Mathf.Clamp01(star * 0.78f));
                    alpha = 1f;
                }

                pixels[y * width + x] = new Color32(
                    (byte)(Mathf.Clamp01(fill.r) * 255f),
                    (byte)(Mathf.Clamp01(fill.g) * 255f),
                    (byte)(Mathf.Clamp01(fill.b) * 255f),
                    (byte)(Mathf.Clamp01(alpha) * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f,
            0, SpriteMeshType.FullRect, new Vector4(20f, 20f, 20f, 20f));
    }

    private static float CharacterCardStarGlow(float x, float y, float centerX, float centerY)
    {
        float dx = Mathf.Abs(x - centerX);
        float dy = Mathf.Abs(y - centerY);
        float verticalRay = Mathf.Exp(-dx * dx / 3f) * Mathf.Exp(-dy * dy / 55f);
        float horizontalRay = Mathf.Exp(-dy * dy / 3f) * Mathf.Exp(-dx * dx / 55f);
        return (verticalRay + horizontalRay) * 0.55f;
    }
}
