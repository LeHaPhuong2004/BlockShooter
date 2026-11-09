using System;
using TMPro;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Controls the authored Home scene. UI construction is an explicit Editor operation.
/// </summary>
public sealed partial class HomeExperienceUI : MonoBehaviour
{
    private const string DailyClaimKey = "home_daily_claim_date";
    private const string DailyStreakKey = "home_daily_streak";
    private const string EquippedCharacterKey = "home_equipped_character_index";
    private const string BlueCharacterUnlockedKey = "home_character_blue_unlocked";
    private const int BlueCharacterPrice = 200;
    // The supplied home HUD reference shows a fixed three-digit demo balance.
    private const int ReferenceHudCoinAmount = 310;
    private static Material whiteSilhouetteMaterial;
    private static Sprite softContactShadowSprite;
    private static Sprite cardGlowSprite;
    private static Sprite cardTriangleRaySprite;
    private static Sprite buttonSparkSprite;
    private static Sprite buttonDotSprite;
    private static readonly Color32[] ButtonBurstPalette =
    {
        new Color32(255, 213, 67, 255),
        new Color32(53, 226, 255, 255),
        new Color32(255, 79, 175, 255),
        new Color32(131, 255, 91, 255),
        new Color32(255, 130, 54, 255),
        new Color32(184, 111, 255, 255)
    };

    private static void DestroyUIObject(UnityEngine.Object target)
    {
        if (target == null) return;
        if (Application.isPlaying) UnityEngine.Object.Destroy(target);
        else UnityEngine.Object.DestroyImmediate(target);
    }

    private sealed class ButtonFeedbackState
    {
        public RectTransform rect;
        public Vector3 baseScale;
        public float currentScale = 1f;
        public float targetScale = 1f;
    }

    private sealed class ButtonSparkParticle
    {
        public RectTransform rect;
        public Image image;
        public Vector2 velocity;
        public float angularVelocity;
        public float age;
        public float lifetime;
        public Color baseColor;
        public Vector3 baseScale;
    }

    private sealed class CardRotatingLightState
    {
        public RectTransform rect;
        public RectTransform parentRect;
        public Vector2 basePosition;
        public Vector2 orbitRadius;
        public float phase;
    }

    private sealed class SettingToggleVisual
    {
        public Image track;
        public Outline outline;
        public RectTransform handleRect;
        public Image handle;
        public RectTransform stateRect;
        public Text stateText;
        public Color offTrackColor;
        public Color onTrackColor;
        public Color offOutlineColor;
        public Color onOutlineColor;
        public float value;
        public float targetValue;
    }

    private Font font;
    private Font roundedFont;
    private Sprite panelSprite;
    private Sprite settingsSwitchSprite;
    private Sprite shopCardSprite;
    private Sprite shopTabSelectedSprite;
    private Sprite shopTabIdleSprite;
    private Sprite shopBuyFaceSprite;
    private Sprite avatarSprite;
    private Sprite coinSprite;
    private Sprite heartSprite;
    private Sprite settingsSprite;
    private Sprite settingsToggleKnobSprite;
    private Sprite settingsToggleTrackSprite;
    private Sprite characterCardSprite;
    private Sprite level25CardSprite;
    private Sprite level30CardSprite;
    private Sprite level50CardSprite;
    private Sprite selectedTabSprite;
    private Sprite plusBadgeSprite;
    private Sprite cubesSprite;
    private Sprite lockSprite;
    private Sprite circleSprite;
    private Sprite[] shopGoldSprites;
    private Sprite[] shopHeartSprites;
    private Sprite[] shopBoosterSprites;
    private Sprite[] characterSkins;
    private Sprite shopBannerGoodsSprite;
    private Button shopGoldTabButton;
    private Button shopBoosterTabButton;
    private Image shopGoldTabImage;
    private Image shopBoosterTabImage;
    private GameObject shopGoldViewport;
    private GameObject shopBoosterViewport;
    private bool shopBoosterSelected;
    private Text coinLabel;
    private Text dailyButtonLabel;
    private GameObject missionBadge;
    private GameObject achievementBadge;
    private GameObject footerBackgroundObject;
    private GameObject settingsPopupRoot;
    private RectTransform settingsWindowRect;
    private Text settingsTitleLabel;
    private Text settingsLanguageValueLabel;
    private Text settingsCodePlaceholderLabel;
    private Text settingsHelpLabel;
    private Text[] settingsRowLabels;
    private Button settingsCloseButton;
    private Button settingsLanguageButton;
    private Button settingsCodeButton;
    private Button settingsHelpButton;
    private int lastSettingsScreenWidth = -1;
    private int lastSettingsScreenHeight = -1;
    private GameObject[] dailyLoginReadyStates;
    private GameObject[] dailyLoginNotReadyStates;
    private GameObject[] dailyLoginReadyToClaimStates;
    private GameObject[] dailyLoginCheckmarks;
#if UNITY_EDITOR
    private bool editorDailyClaimedToday;
    private int editorDailyStreak;
    private string editorDailyStateDate;
#endif
    private GameObject[] tabPages;
    private GameObject homeCanvasObject;
    private Image homeCharacterImage;
    private Image characterPreviewImage;
    private Image characterActionCoinIcon;
    private Text characterPreviewName;
    private Text characterActionLabel;
    private Button characterActionButton;
    private Image[] characterCardFrames;
    private Image[] characterCardPortraits;
    private GameObject[] characterCardCheckMarks;
    private GameObject[] characterCardPrices;
    private GameObject[] characterCardLocks;
    private GameObject[] characterCardUseBadges;
    private Sprite characterCardFrameSprite;
    private Sprite[] characterCardFrameSprites;
    private Sprite[] characterPortraitSprites;
    private Sprite characterActionButtonSprite;
    private Sprite characterArrowLeftSprite;
    private Sprite characterArrowRightSprite;
    private int equippedCharacterIndex;
    private int previewCharacterIndex;
    private bool blueCharacterUnlocked;
    private RectTransform selectedTabBlock;
    private RectTransform playButtonRect;
    private RectTransform playMultiplierBadgeRect;
    private Quaternion playMultiplierBadgeBaseRotation = Quaternion.identity;
    private Text playLevelLabel;
    private Text playMultiplierLabel;
    private Text playDifficultyLabel;
    private int displayedPlayLevel = -1;
    private RectTransform floatingCharacterRect;
    private Vector2 floatingCharacterBasePosition;
    private Vector3 floatingCharacterBaseScale = Vector3.one;
    private CanvasGroup floatingCharacterShadow;
    private RectTransform buttonEffectsOverlay;
    private RectTransform coinHudButtonRect;
    private RectTransform coinHudIconRect;
    private Vector3 coinHudIconBaseScale = Vector3.one;
    private RectTransform coinHudIconOutlineRect;
    private Vector3 coinHudIconOutlineBaseScale = Vector3.one;
    private float coinHudPulseRemaining;
    private int coinCountStart;
    private int coinCountTarget;
    private float coinCountElapsed;
    private float coinCountDuration;
    private bool coinCountAnimating;
    private readonly List<ButtonFeedbackState> buttonFeedbackStates = new List<ButtonFeedbackState>();
    private readonly List<ButtonSparkParticle> buttonSparkParticles = new List<ButtonSparkParticle>();
    private readonly List<CardRotatingLightState> cardRotatingLights = new List<CardRotatingLightState>();
    private readonly List<SettingToggleVisual> settingToggleVisuals = new List<SettingToggleVisual>();
    private bool builtLandscape;
    private float builtScreenAspect;
    private int activeTabIndex;
    private float selectedTabX = 0.5f;
    private float targetTabX = 0.5f;
    private float tabVelocity;
    private RectTransform[] tabIconRects;
    private Text[] bottomTabLabels;
    // Tab order is Home, Shop, Missions, Achievements, Character.
    private static readonly float[] TabCenters = { 0.5f, 0.1f, 0.3f, 0.7f, 0.9f };
    private static readonly string[] CharacterDisplayNames =
    {
        "BÉ HÚT BỤI", "BÉ HÚT BỤI XANH", "BÉ HÚT BỤI HỒNG", "BÉ HÚT BỤI LÁ"
    };
    private static readonly string[] PlayDifficultyLabels =
    {
        "CỰC KỲ DỄ", "KHÁ DỄ", "DỄ", "BÌNH THƯỜNG",
        "KHÁ KHÓ", "KHÓ", "SIÊU KHÓ", "CỰC KỲ KHÓ"
    };

    private float nextDailyMissionRefresh;
    private Text heartCountLabel;
    private Text heartTimerLabel;

    private void Update()
    {
        RefreshResponsiveHomeLayout();
        RefreshInlineFeedback();
        RefreshPlayButtonLevel();
        if (Time.unscaledTime >= nextDailyMissionRefresh)
        {
            nextDailyMissionRefresh = Time.unscaledTime + 1f;
            DailyMissionManager.RefreshDay();
            RefreshDailyState();
            RefreshHearts();
        }
        if (floatingCharacterRect != null)
        {
            float pulse = Mathf.Sin(Time.unscaledTime * 1.8f);
            floatingCharacterRect.anchoredPosition = floatingCharacterBasePosition + Vector2.up * (pulse * 10f);
            floatingCharacterRect.localScale = floatingCharacterBaseScale * (1f + pulse * 0.025f);
            if (floatingCharacterShadow != null)
                floatingCharacterShadow.alpha = 0.58f - pulse * 0.10f;
        }
        UpdateCharacterPreviewAnimation(Time.unscaledTime);
        if (playMultiplierBadgeRect != null)
        {
            float swing = Mathf.Sin(Time.unscaledTime * 1.65f) * 5f;
            playMultiplierBadgeRect.localRotation = playMultiplierBadgeBaseRotation * Quaternion.Euler(0f, 0f, swing);
            PositionResponsivePlayBadge();
        }
        UpdateCoinHudPulse(Time.unscaledDeltaTime);
        UpdateCoinCountAnimation(Time.unscaledDeltaTime);
        UpdateSettingToggleAnimations(Time.unscaledDeltaTime);
        UpdateSettingsPopupLayout();
        UpdateButtonFeedback(Time.unscaledDeltaTime);
        UpdateCharacterSelectionHighlight(Time.unscaledDeltaTime);
        UpdateNavigationIconSelection(Time.unscaledDeltaTime);
        UpdateButtonSparkParticles(Time.unscaledDeltaTime);
        UpdateCardGlows();
        UpdateShopCardLightSweeps();
        UpdateDailyRewardGlows();

        if (selectedTabBlock == null) return;
        targetTabX = GetTabIconCenterInNavigation(activeTabIndex);
        selectedTabX = Mathf.SmoothDamp(selectedTabX, targetTabX, ref tabVelocity, 0.16f);
        SetSelectedTabAnchors(selectedTabBlock, selectedTabX, GetTabIconRect(activeTabIndex));
        UpdateBottomTabTextSizing();
    }

    private void LoadHomeResources()
    {
        font = HomeUIFont.LoadSourceFont();
        roundedFont = font;
        panelSprite = CreatePanelSprite();
        settingsSwitchSprite = CreatePanelSprite(30);
        settingsToggleTrackSprite = Resources.Load<Sprite>("UI/Settings/SettingsToggleTrack");
        if (settingsToggleTrackSprite == null) settingsToggleTrackSprite = settingsSwitchSprite;
        shopCardSprite = Resources.Load<Sprite>("UI/ShopItems/ShopCardFrame");
        if (shopCardSprite == null) shopCardSprite = CreatePanelSprite(23);
        shopTabSelectedSprite = Resources.Load<Sprite>("UI/ShopItems/ShopTabSelected");
        if (shopTabSelectedSprite == null) shopTabSelectedSprite = CreatePanelSprite(25);
        shopTabIdleSprite = Resources.Load<Sprite>("UI/ShopItems/ShopTabIdle");
        if (shopTabIdleSprite == null) shopTabIdleSprite = CreatePanelSprite(25);
        shopBuyFaceSprite = Resources.Load<Sprite>("UI/ShopItems/ShopBuyButton");
        if (shopBuyFaceSprite == null) shopBuyFaceSprite = CreateShopBuyFaceSprite();
        if (softContactShadowSprite == null) softContactShadowSprite = CreateSoftContactShadowSprite();
        if (cardGlowSprite == null) cardGlowSprite = CreateCardGlowSprite();
        if (cardTriangleRaySprite == null) cardTriangleRaySprite = CreateCardTriangleRaySprite();
        if (buttonSparkSprite == null) buttonSparkSprite = CreateButtonSparkSprite();
        if (buttonDotSprite == null) buttonDotSprite = CreateButtonDotSprite();
        selectedTabSprite = Resources.Load<Sprite>("UI/Buttons/SelectedTabBackground");
        if (selectedTabSprite == null)
            selectedTabSprite = CreateSelectedTabSprite();
        plusBadgeSprite = Resources.Load<Sprite>("UI/Buttons/PlusBadgeReference");
        if (plusBadgeSprite == null) plusBadgeSprite = CreatePlusBadgeSprite();
        circleSprite = CreateCircleSprite();
        settingsToggleKnobSprite = Resources.Load<Sprite>("UI/Settings/SettingsToggleKnob");
        if (settingsToggleKnobSprite == null) settingsToggleKnobSprite = circleSprite;
        avatarSprite = LoadIconSprite("UI/HomeIcons/Avatar");
        coinSprite = LoadIconSprite("UI/HomeIcons/Coin");
        heartSprite = LoadIconSprite("UI/HomeIcons/Heart");
        settingsSprite = LoadIconSprite("UI/HomeIcons/Settings");
        characterCardSprite = Resources.Load<Sprite>("UI/TabIcons/Character");
        characterSkins = new Sprite[]
        {
            Resources.Load<Sprite>("UI/HomeRobotCharacterCutout"),
            Resources.Load<Sprite>("UI/CharacterSkins/CharacterBlue"),
            Resources.Load<Sprite>("UI/CharacterSkins/CharacterPinkBow"),
            Resources.Load<Sprite>("UI/CharacterSkins/CharacterGreenLeaf")
        };
        for (int i = 1; i < characterSkins.Length; i++)
            if (characterSkins[i] == null) characterSkins[i] = characterSkins[0];
        equippedCharacterIndex = Mathf.Clamp(PlayerPrefs.GetInt(EquippedCharacterKey, 0), 0, characterSkins.Length - 1);
        previewCharacterIndex = equippedCharacterIndex;
        blueCharacterUnlocked = PlayerPrefs.GetInt(BlueCharacterUnlockedKey, 0) == 1;
        characterCardFrameSprite = Resources.Load<Sprite>("UI/CharacterSkins/CharacterCardFramePurple");
        if (characterCardFrameSprite == null)
            characterCardFrameSprite = Resources.Load<Sprite>("UI/CharacterSkins/CharacterSkinCardFrame");
        if (characterCardFrameSprite == null) characterCardFrameSprite = CreateCharacterSkinCardFrameSprite();
        characterCardFrameSprites = new Sprite[]
        {
            characterCardFrameSprite,
            Resources.Load<Sprite>("UI/CharacterSkins/CharacterCardFrameBlue"),
            Resources.Load<Sprite>("UI/CharacterSkins/CharacterCardFramePink"),
            Resources.Load<Sprite>("UI/CharacterSkins/CharacterCardFrameGreen")
        };
        if (characterCardFrameSprites[1] == null)
            characterCardFrameSprites[1] = Resources.Load<Sprite>("UI/CharacterSkins/CharacterFrameBlue");
        if (characterCardFrameSprites[2] == null)
            characterCardFrameSprites[2] = Resources.Load<Sprite>("UI/CharacterSkins/CharacterFramePink");
        if (characterCardFrameSprites[3] == null)
            characterCardFrameSprites[3] = Resources.Load<Sprite>("UI/CharacterSkins/CharacterFrameGreen");
        characterPortraitSprites = new Sprite[characterSkins.Length];
        for (int i = 0; i < characterPortraitSprites.Length; i++)
            characterPortraitSprites[i] = characterSkins[i];
        for (int i = 0; i < characterCardFrameSprites.Length; i++)
            if (characterCardFrameSprites[i] == null) characterCardFrameSprites[i] = characterCardFrameSprite;
        for (int i = 0; i < characterPortraitSprites.Length; i++)
            if (characterPortraitSprites[i] == null) characterPortraitSprites[i] = GetCharacterSprite(i);
        characterActionButtonSprite = Resources.Load<Sprite>("UI/CharacterSkins/CharacterActionButton");
        characterArrowLeftSprite = Resources.Load<Sprite>("UI/CharacterSkins/CharacterArrowLeft");
        characterArrowRightSprite = Resources.Load<Sprite>("UI/CharacterSkins/CharacterArrowRight");
        level25CardSprite = Resources.Load<Sprite>("UI/LevelCards/Level25Card");
        level30CardSprite = Resources.Load<Sprite>("UI/LevelCards/Level30Card");
        level50CardSprite = Resources.Load<Sprite>("UI/LevelCards/Level50Card");
        cubesSprite = LoadIconSprite("UI/HomeIcons/Cubes");
        lockSprite = LoadIconSprite("UI/HomeIcons/Lock");
        shopGoldSprites = new Sprite[]
        {
            LoadIconSprite("UI/ShopItems/GoldPack100"),
            LoadIconSprite("UI/ShopItems/GoldPack500"),
            LoadIconSprite("UI/ShopItems/GoldPack1200"),
            LoadIconSprite("UI/ShopItems/GoldPack3000"),
            LoadIconSprite("UI/ShopItems/GoldPack7500"),
            LoadIconSprite("UI/ShopItems/GoldPack15000")
        };
        shopHeartSprites = new Sprite[]
        {
            LoadIconSprite("UI/ShopItems/Heart5"),
            LoadIconSprite("UI/ShopItems/Heart20")
        };
        shopBoosterSprites = new Sprite[]
        {
            LoadIconSprite("UI/ShopItems/BoosterMagnifier"),
            LoadIconSprite("UI/ShopItems/BoosterScissors"),
            LoadIconSprite("UI/ShopItems/BoosterShuffle"),
            LoadIconSprite("UI/ShopItems/BoosterLift")
        };
        shopBannerGoodsSprite = LoadIconSprite("UI/ShopItems/ShopBannerGoods");
    }

    private void Start()
    {
        if (gameObject.scene.name != "Home")
        {
            enabled = false;
            return;
        }
#if UNITY_EDITOR
        ResetEditorDailyLoginState();
#endif
        LoadHomeResources();
        if (!BindSceneHome())
        {
            enabled = false;
            return;
        }
        RefreshDailyState();
        RefreshCoins();
        RefreshHearts();
        AchievementManager.Changed += RefreshAchievements;
        RefreshAchievements();
        IAPManager.CoinPackPricesUpdated += RefreshShopPurchaseLabels;
        SubscribeBoosterPurchaseEvents();
        CurrencyManager.MoneyAwarded += HandleMoneyAwarded;
        RefreshShopPurchaseLabels();
        RefreshBoosterShopPrices();
        DailyMissionManager.Changed += RefreshDailyState;
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnMoneyChanged += HandleMoneyChanged;
    }

#if UNITY_EDITOR
    public void BuildSceneUIForEditor()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Build Home UI outside Play Mode.");
        LoadHomeResources();
        BuildHome();
        RefreshAuthoredShopLayout(true);
        HideLegacyHomeCanvases();
        HomeUIFont.ApplyToScene(gameObject.scene);
    }
#endif

    private void OnDestroy()
    {
        AchievementManager.Changed -= RefreshAchievements;
        DailyMissionManager.Changed -= RefreshDailyState;
        IAPManager.CoinPackPricesUpdated -= RefreshShopPurchaseLabels;
        UnsubscribeBoosterPurchaseEvents();
        CurrencyManager.MoneyAwarded -= HandleMoneyAwarded;
        if (shopCardSweepMaterial != null) DestroyUIObject(shopCardSweepMaterial);
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnMoneyChanged -= HandleMoneyChanged;
    }

    private void HideLegacyHomeCanvases()
    {
        if (homeCanvasObject == null) return;

        var canvases = Resources.FindObjectsOfTypeAll<Canvas>();
        foreach (var canvas in canvases)
        {
            if (canvas == null || canvas.gameObject.scene != gameObject.scene) continue;
            if (canvas.gameObject == homeCanvasObject || canvas.transform.IsChildOf(homeCanvasObject.transform)) continue;

            canvas.enabled = false;
            var raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster != null) raycaster.enabled = false;
        }
    }

    private void BuildHome()
    {
        bool landscape = false;
        builtLandscape = landscape;
        builtScreenAspect = 1080f / 1920f;
        if (EventSystem.current == null)
        {
            var eventSystem = new GameObject("Home Event System", typeof(EventSystem), typeof(StandaloneInputModule));
            eventSystem.transform.SetParent(transform, false);
        }
        homeCanvasObject = new GameObject("Home Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        homeCanvasObject.transform.SetParent(transform, false);
        var canvas = homeCanvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120;
        var scaler = homeCanvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 1f;
        if (Application.isPlaying) PortraitDisplay.ConfigureCanvas(canvas);

        var root = Panel(homeCanvasObject.transform, "Home", Color.clear, Vector2.zero, Vector2.one);
        var bg = new GameObject("Robot Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(root.transform, false);
        Stretch(bg.GetComponent<RectTransform>());
        var bgImage = bg.GetComponent<Image>();
        var cleanBackgroundSprite = Resources.Load<Sprite>("UI/HomeRobotBackgroundPortraitClean");
        bgImage.sprite = cleanBackgroundSprite != null
            ? cleanBackgroundSprite
            : Resources.Load<Sprite>("UI/HomeRobotBackgroundPortrait");
        bgImage.color = Color.white;
        bgImage.preserveAspect = true;
        var backgroundFitter = bg.AddComponent<AspectRatioFitter>();
        backgroundFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        backgroundFitter.aspectRatio = bgImage.sprite.rect.width / bgImage.sprite.rect.height;

        var floatingCharacterSprite = Resources.Load<Sprite>("UI/HomeRobotCharacterCutout");
        if (cleanBackgroundSprite != null && floatingCharacterSprite != null)
        {
            var shadowObject = new GameObject("Robot Contact Shadows", typeof(RectTransform), typeof(CanvasGroup));
            shadowObject.transform.SetParent(bg.transform, false);
            Stretch(shadowObject.GetComponent<RectTransform>());
            floatingCharacterShadow = shadowObject.GetComponent<CanvasGroup>();
            floatingCharacterShadow.alpha = 0.58f;
            floatingCharacterShadow.interactable = false;
            floatingCharacterShadow.blocksRaycasts = false;
            AddContactShadow(shadowObject.transform, "Left Wheel Shadow", new Vector2(0.29f, 0.338f), new Vector2(0.16f, 0.028f));
            AddContactShadow(shadowObject.transform, "Right Wheel Shadow", new Vector2(0.50f, 0.338f), new Vector2(0.17f, 0.03f));
            AddContactShadow(shadowObject.transform, "Nozzle Shadow", new Vector2(0.78f, 0.323f), new Vector2(0.21f, 0.032f));

            var floatingCharacter = new GameObject("Floating Robot Character", typeof(RectTransform), typeof(Image));
            floatingCharacter.transform.SetParent(bg.transform, false);
            floatingCharacterRect = floatingCharacter.GetComponent<RectTransform>();
            floatingCharacterRect.anchorMin = new Vector2(0.13f, 0.32f);
            floatingCharacterRect.anchorMax = new Vector2(0.93f, 0.57f);
            floatingCharacterRect.pivot = new Vector2(0.5f, 0.5f);
            floatingCharacterRect.offsetMin = floatingCharacterRect.offsetMax = Vector2.zero;
            floatingCharacterBasePosition = floatingCharacterRect.anchoredPosition;
            floatingCharacterBaseScale = floatingCharacterRect.localScale;

            homeCharacterImage = floatingCharacter.GetComponent<Image>();
            homeCharacterImage.sprite = GetCharacterSprite(equippedCharacterIndex);
            homeCharacterImage.type = Image.Type.Simple;
            homeCharacterImage.preserveAspect = false;
            homeCharacterImage.raycastTarget = false;
        }
        bg.transform.SetAsFirstSibling();

        var homeOnly = Panel(root.transform, "Home Only Content", Color.clear, Vector2.zero, Vector2.one);
        homeOnly.GetComponent<Image>().raycastTarget = false;

        var profile = Button(root.transform, "PROFILE", "ROBOT", landscape ? new Vector2(0.08f, 0.90f) : new Vector2(0.12f, 0.915f), landscape ? new Vector2(0.12f, 0.15f) : new Vector2(0.18f, 0.105f), new Color32(126, 44, 235, 255), ShowCharacter);
        var profileBackground = profile.GetComponent<Image>();
        profileBackground.color = Color.clear;
        profile.GetComponent<Button>().transition = Selectable.Transition.None;
        var profileOutline = profile.GetComponent<Outline>();
        if (profileOutline != null) DestroyUIObject(profileOutline);
        var profileShadow = profile.GetComponent<Shadow>();
        if (profileShadow != null) DestroyUIObject(profileShadow);
        profile.GetComponentInChildren<Text>().text = string.Empty;
        AddButtonIcon(profile, avatarSprite, new Vector2(0.5f, 0.5f), new Vector2(0.94f, 0.94f), true);
        Vector2 coinCenter = landscape ? new Vector2(0.34f, 0.91f) : new Vector2(0.3645f, 0.915f);
        Vector2 coinSize = landscape ? new Vector2(0.25f, 0.064f) : new Vector2(0.24f, 0.034f);
        var coins = Button(root.transform, "COINS", ReferenceHudCoinAmount.ToString(), coinCenter, coinSize, new Color32(255, 249, 239, 255), 42, ShowShop);
        coinHudButtonRect = coins.GetComponent<RectTransform>();
        StyleHudCounter(coins, 42);
        AddButtonIcon(coins, coinSprite, new Vector2(0.075f, 0.5f), new Vector2(0.30f, 1.08f));
        var coinIcon = coins.transform.Find("Button Icon");
        coinHudIconRect = coinIcon as RectTransform;
        if (coinHudIconRect != null) coinHudIconBaseScale = coinHudIconRect.localScale;
        if (coinIcon != null) AddWhiteIconOutline(coinIcon.gameObject, 10f);
        coinHudIconOutlineRect = FindCoinHudIconOutline(coinHudIconRect);
        if (coinHudIconOutlineRect != null) coinHudIconOutlineBaseScale = coinHudIconOutlineRect.localScale;
        SetButtonLabelArea(coins, new Vector2(0.275f, 0.03f), new Vector2(0.79f, 0.97f));
        AddPlusBadge(coins);
        coinLabel = coins.transform.Find("Label").GetComponent<Text>();
        coinLabel.color = new Color32(198, 91, 5, 255);
        coinLabel.alignment = TextAnchor.MiddleCenter;
        float coinTextLift = coins.GetComponent<RectTransform>().rect.height * 0.05f;
        coinLabel.rectTransform.offsetMin = new Vector2(0f, coinTextLift);
        coinLabel.rectTransform.offsetMax = new Vector2(0f, coinTextLift);
        coinLabel.resizeTextMinSize = 23;
        Vector2 energyCenter = landscape ? new Vector2(0.687f, 0.91f) : new Vector2(0.671f, 0.915f);
        Vector2 energySize = landscape ? new Vector2(0.25f, 0.064f) : new Vector2(0.245f, 0.034f);
        var energy = Button(root.transform, "ENERGY", HeartManager.TimerText, energyCenter, energySize, new Color32(255, 246, 242, 255), landscape ? 30 : 32, ShowEnergy);
        StyleHudCounter(energy, landscape ? 30 : 32);
        AddButtonIcon(energy, heartSprite, new Vector2(0.025f, 0.5f), new Vector2(0.36f, 1.20f));
        var heartIcon = energy.transform.Find("Button Icon");
        const float heartTiltDegrees = 8f;
        if (heartIcon != null)
        {
            var heartRotation = Quaternion.Euler(0f, 0f, heartTiltDegrees);
            heartIcon.localRotation = heartRotation;
            AddWhiteIconOutline(heartIcon.gameObject, 10f);
            var heartBorder = energy.transform.Find("Button Icon White Border");
            if (heartBorder != null) heartBorder.localRotation = heartRotation;
            var heartCount = Label(heartIcon, HeartManager.CountText, 48, new Vector2(0.5f, 0.5f), new Vector2(0.62f, 0.68f), Color.white, FontStyle.Bold);
            heartCountLabel = heartCount;
            heartCount.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -heartTiltDegrees);
            heartCount.resizeTextForBestFit = true;
            heartCount.resizeTextMinSize = 44;
            heartCount.resizeTextMaxSize = 48;
            heartCount.horizontalOverflow = HorizontalWrapMode.Overflow;
            heartCount.raycastTarget = false;
            var heartCountOutline = heartCount.gameObject.AddComponent<Outline>();
            heartCountOutline.effectColor = new Color32(150, 20, 36, 210);
            heartCountOutline.effectDistance = new Vector2(1f, -1f);
        }
        SetButtonLabelArea(energy, new Vector2(0.275f, 0.03f), new Vector2(0.79f, 0.97f));
        AddPlusBadge(energy);
        var energyLabel = energy.transform.Find("Label").GetComponent<Text>();
        heartTimerLabel = energyLabel;
        energyLabel.color = new Color32(210, 48, 61, 255);
        energyLabel.alignment = TextAnchor.MiddleCenter;
        float energyTextLift = energy.GetComponent<RectTransform>().rect.height * 0.08f;
        energyLabel.rectTransform.offsetMin = new Vector2(0f, energyTextLift);
        energyLabel.rectTransform.offsetMax = new Vector2(0f, energyTextLift);
        energyLabel.resizeTextMinSize = 20;
        energyLabel.resizeTextMaxSize = 26;
        energyLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
        energyLabel.verticalOverflow = VerticalWrapMode.Overflow;
        var settings = Button(root.transform, "SETTINGS", "", landscape ? new Vector2(0.92f, 0.90f) : new Vector2(0.895f, 0.918f), landscape ? new Vector2(0.085f, 0.15f) : new Vector2(0.115f, 0.065f), Color.white, 41, ShowSettings);
        SetButtonImage(settings, settingsSprite);

        var levelBanner = Button(homeOnly.transform, "LEVEL_BANNER", "ĐẠT ĐẾN CẤP ĐỘ 20", landscape ? new Vector2(0.5f, 0.77f) : new Vector2(0.5f, 0.805f), landscape ? new Vector2(0.55f, 0.09f) : new Vector2(0.86f, 0.067f), Color.white, 36, ShowEvent);
        DestroyUIObject(levelBanner.GetComponent<Outline>());
        DestroyUIObject(levelBanner.GetComponent<Shadow>());
        levelBanner.GetComponent<Image>().color = Color.clear;

        var bannerWhiteBar = Panel(levelBanner.transform, "Banner White Bar", Color.white,
            new Vector2(0f, 0.10f), new Vector2(1f, 0.90f));
        bannerWhiteBar.GetComponent<Image>().raycastTarget = false;
        DestroyUIObject(bannerWhiteBar.GetComponent<Outline>());
        bannerWhiteBar.transform.SetAsFirstSibling();

        // Keep the white rounded backing and place a clean lavender pill inside,
        // with the cube cluster and lock overlapping the ends.
        var capsuleSprite = CreatePanelSprite(32);
        var bannerPill = Panel(levelBanner.transform, "Banner Text Capsule", new Color32(226, 194, 255, 255),
            new Vector2(0.15f, 0.20f), new Vector2(0.87f, 0.80f));
        var pillImage = bannerPill.GetComponent<Image>();
        pillImage.sprite = capsuleSprite;
        pillImage.raycastTarget = false;
        DestroyUIObject(bannerPill.GetComponent<Outline>());
        bannerPill.transform.SetSiblingIndex(1);

        var bannerLabel = levelBanner.transform.Find("Label").GetComponent<Text>();
        if (roundedFont != null) bannerLabel.font = roundedFont;
        bannerLabel.color = new Color32(31, 25, 196, 255);
        bannerLabel.fontStyle = FontStyle.Bold;
        bannerLabel.fontSize = 50;
        bannerLabel.resizeTextForBestFit = true;
        bannerLabel.resizeTextMinSize = 18;
        bannerLabel.resizeTextMaxSize = 50;
        bannerLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
        bannerLabel.verticalOverflow = VerticalWrapMode.Overflow;
        SetButtonLabelArea(levelBanner, new Vector2(0.21f, 0.24f), new Vector2(0.82f, 0.76f));
        AddButtonIcon(levelBanner, lockSprite, new Vector2(0.965f, 0.52f), new Vector2(0.16f, 1.42f));
        var bannerLock = levelBanner.transform.Find("Button Icon");
        if (bannerLock != null) AddWhiteIconOutline(bannerLock.gameObject, 12f);
        Vector2 bannerCubeCenterInHome = landscape ? new Vector2(0.24f, 0.77f) : new Vector2(0.12f, 0.805f);
        Vector2 bannerCubeSizeInHome = landscape ? new Vector2(0.11f, 0.15f) : new Vector2(0.17f, 0.11f);
        var levelBannerRect = levelBanner.GetComponent<RectTransform>();
        Vector2 bannerSizeInHome = levelBannerRect.anchorMax - levelBannerRect.anchorMin;
        Vector2 bannerCubeCenter = new Vector2(
            (bannerCubeCenterInHome.x - levelBannerRect.anchorMin.x) / bannerSizeInHome.x,
            (bannerCubeCenterInHome.y - levelBannerRect.anchorMin.y) / bannerSizeInHome.y);
        Vector2 bannerCubeSize = new Vector2(
            bannerCubeSizeInHome.x / bannerSizeInHome.x,
            bannerCubeSizeInHome.y / bannerSizeInHome.y);
        AddButtonIcon(levelBanner, cubesSprite, bannerCubeCenter, bannerCubeSize, false, "Banner Cube Cluster");
        var bannerCubes = levelBanner.transform.Find("Banner Cube Cluster");
        if (bannerCubes != null) AddWhiteIconOutline(bannerCubes.gameObject, 12f);
        AddLockedLevelCard(homeOnly.transform, "LOCKED_30", 30, landscape ? new Vector2(0.13f, 0.575f) : new Vector2(0.17f, 0.66f), landscape ? new Vector2(0.13f, 0.23f) : new Vector2(0.25f, 0.14f));
        AddLockedLevelCard(homeOnly.transform, "LOCKED_50", 50, landscape ? new Vector2(0.87f, 0.575f) : new Vector2(0.83f, 0.66f), landscape ? new Vector2(0.13f, 0.23f) : new Vector2(0.25f, 0.14f));
        AddLockedLevelCard(homeOnly.transform, "LOCKED_25", 25, landscape ? new Vector2(0.13f, 0.31f) : new Vector2(0.17f, 0.49f), landscape ? new Vector2(0.13f, 0.23f) : new Vector2(0.25f, 0.14f));

        const float footerAspect = 5.10f;
        // Keep the footer full-width while using a slimmer height ratio.
        float referenceNavWidth = landscape ? 0.43f : 1f;
        float referenceAspect = landscape ? 1920f / 1080f : 1080f / 1920f;
        float navHeight = referenceNavWidth * referenceAspect / footerAspect;
        float screenAspect = 1080f / 1920f;
        float compactPortrait = landscape ? 0f : Mathf.InverseLerp(0.48f, 0.75f, screenAspect);
        float playWidth = landscape ? 0.36f : Mathf.Lerp(0.70f, 0.56f, compactPortrait);
        float playHeight = playWidth * screenAspect * (175f / 486f);
        float playCenterY = landscape
            ? 0.34f
            : 0.004f + navHeight + 0.045f + playHeight * 0.5f;
        var playButton = Button(homeOnly.transform, "PLAY", string.Empty, new Vector2(0.5f, playCenterY), new Vector2(playWidth, playHeight), Color.white, 32, Play);
        playButtonRect = playButton.GetComponent<RectTransform>();
        var playImage = playButton.GetComponent<Image>();
        playImage.sprite = Resources.Load<Sprite>("UI/Buttons/PlayButtonExact");
        playImage.type = Image.Type.Simple;
        playImage.preserveAspect = true;
        playImage.color = Color.white;
        var playButtonOutline = playButton.GetComponent<Outline>();
        if (playButtonOutline != null) DestroyUIObject(playButtonOutline);
        var playButtonShadow = playButton.GetComponent<Shadow>();
        if (playButtonShadow != null) DestroyUIObject(playButtonShadow);
        var playLabel = playButton.transform.Find("Label");
        if (playLabel != null) DestroyUIObject(playLabel.gameObject);
        ConfigurePlayButton(playButton.transform);

        tabPages = new GameObject[5];
        tabPages[0] = homeOnly;
        tabPages[0].GetComponent<Image>().raycastTarget = false;
        BuildTabPages(root.transform);
        for (int i = 1; i < tabPages.Length; i++) tabPages[i].SetActive(false);
        var nav = Panel(root.transform, "Bottom Navigation", Color.clear, new Vector2(0f, 0.004f), new Vector2(1f, 0.004f + navHeight));
        var navImage = nav.GetComponent<Image>();
        navImage.raycastTarget = false;
        var footerBackground = new GameObject("Footer Sprite", typeof(RectTransform), typeof(Image));
        footerBackgroundObject = footerBackground;
        footerBackground.transform.SetParent(nav.transform, false);
        var footerRect = footerBackground.GetComponent<RectTransform>();
        footerRect.anchorMin = Vector2.zero;
        footerRect.anchorMax = Vector2.one;
        footerRect.offsetMin = footerRect.offsetMax = Vector2.zero;
        var footerImage = footerBackground.GetComponent<Image>();
        footerImage.sprite = CreateBlankFooterBarSprite(Resources.Load<Sprite>("UI/Buttons/FooterBarBlankGenerated"));
        ConfigureFooterImage(footerImage);
        footerImage.raycastTarget = false;
        footerRect.localScale = Vector3.one;
        var selected = new GameObject("Selected Tab", typeof(RectTransform), typeof(Image));
        selected.transform.SetParent(nav.transform, false);
        selectedTabBlock = selected.GetComponent<RectTransform>();
        selectedTabX = targetTabX = TabCenters[activeTabIndex];
        SetSelectedTabAnchors(selectedTabBlock, selectedTabX, null);
        var selectedImage = selected.GetComponent<Image>();
        selectedImage.sprite = selectedTabSprite;
        selectedImage.type = Image.Type.Simple;
        selectedImage.preserveAspect = false;
        selectedImage.color = Color.white;
        selectedImage.raycastTarget = false;
        selectedTabBlock.localScale = Vector3.one;
        selectedTabBlock.offsetMin = selectedTabBlock.offsetMax = Vector2.zero;
        tabIconRects = new RectTransform[5];
        bottomTabLabels = new Text[5];
        var shopTab = Button(nav.transform, "SHOP", "CỬA HÀNG", new Vector2(TabCenters[1], 0.55f), new Vector2(0.19f, 0.72f), Color.clear, 22, () => SelectTab(1));
        tabIconRects[1] = AddTabIcon(shopTab.transform, "S");
        bottomTabLabels[1] = shopTab.GetComponentInChildren<Text>();
        var missionTab = Button(nav.transform, "MISSIONS", "NHIỆM VỤ", new Vector2(TabCenters[2], 0.55f), new Vector2(0.19f, 0.72f), Color.clear, 22, () => SelectTab(2));
        tabIconRects[2] = AddTabIcon(missionTab.transform, "M");
        bottomTabLabels[2] = missionTab.GetComponentInChildren<Text>();
        missionBadge = AddRewardBadge(missionTab.transform, "Mission Notification");
        var homeTab = Button(nav.transform, "HOME_TAB", "TRANG CHỦ", new Vector2(TabCenters[0], 0.55f), new Vector2(0.19f, 0.72f), Color.clear, 22, () => SelectTab(0));
        tabIconRects[0] = AddTabIcon(homeTab.transform, "H");
        bottomTabLabels[0] = homeTab.GetComponentInChildren<Text>();
        var achievementTab = Button(nav.transform, "ACHIEVEMENTS", "THÀNH TÍCH", new Vector2(TabCenters[3], 0.55f), new Vector2(0.19f, 0.72f), Color.clear, 22, () => SelectTab(3));
        tabIconRects[3] = AddTabIcon(achievementTab.transform, "A");
        achievementBadge = AddRewardBadge(achievementTab.transform, "Achievement Notification");
        bottomTabLabels[3] = achievementTab.GetComponentInChildren<Text>();
        var characterTab = Button(nav.transform, "CHARACTER", "NHÂN VẬT", new Vector2(TabCenters[4], 0.55f), new Vector2(0.19f, 0.72f), Color.clear, 22, () => SelectTab(4));
        tabIconRects[4] = AddTabIcon(characterTab.transform, "R");
        bottomTabLabels[4] = characterTab.GetComponentInChildren<Text>();
        ConfigureFooterTabHitbox(shopTab);
        ConfigureFooterTabHitbox(missionTab);
        ConfigureFooterTabHitbox(homeTab);
        ConfigureFooterTabHitbox(achievementTab);
        ConfigureFooterTabHitbox(characterTab);
        selectedTabX = targetTabX = GetTabIconCenterInNavigation(activeTabIndex);
        SetSelectedTabAnchors(selectedTabBlock, selectedTabX, GetTabIconRect(activeTabIndex));
        tabVelocity = 0f;
        UpdateBottomTabTextSizing();
        footerBackgroundObject.SetActive(true);
        selected.SetActive(true);
        UpdateSelectedFooterContent();
        dailyButtonLabel = missionTab.GetComponentInChildren<Text>();
        var effectsOverlay = new GameObject("Button Effects Overlay", typeof(RectTransform));
        effectsOverlay.transform.SetParent(root.transform, false);
        buttonEffectsOverlay = effectsOverlay.GetComponent<RectTransform>();
        Stretch(buttonEffectsOverlay);
        effectsOverlay.transform.SetAsLastSibling();
    }

    private void BuildTabPages(Transform parent)
    {
        tabPages[1] = BuildShopPage(parent);
        tabPages[2] = BuildDailyMissionsPage(parent);
        tabPages[3] = BuildAchievementsPage(parent);
        tabPages[4] = BuildCharacterPage(parent);
    }

    private GameObject BuildDailyMissionsPage(Transform parent)
    {
        var page = new GameObject("Daily Missions Page", typeof(RectTransform));
        page.transform.SetParent(parent, false);
        Stretch(page.GetComponent<RectTransform>());
        var content = new GameObject("Daily Missions Aspect Content", typeof(RectTransform));
        content.transform.SetParent(page.transform, false);
        Stretch(content.GetComponent<RectTransform>());
        var pageAspect = content.AddComponent<AspectRatioFitter>();
        pageAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        pageAspect.aspectRatio = 941f / 1672f;

        BuildDailyMissionHeaderBanner(page.transform, false);

        BuildDailyLoginPanel(content.transform);
        BuildDailyMissionTaskList(content.transform);
#if UNITY_EDITOR
        PrepareDailyRewardGlowsForEditor();
#endif
        return page;
    }

    private void BuildDailyMissionTaskList(Transform parent)
    {
        const int count = 6;
        const int spacing = 235;
        const int listHeight = count * spacing;
        ScrollRect scroll;
        var viewport = BuildShopViewport(parent, "Daily Mission List",
            new Vector2(0f, 232f / 1672f), new Vector2(1f, 1002f / 1672f), out scroll);
        viewport.GetComponent<Image>().color = Color.clear;
        scroll.scrollSensitivity = 45f;
        var aspect = scroll.content.gameObject.AddComponent<AspectRatioFitter>();
        aspect.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;
        aspect.aspectRatio = 941f / listHeight;

        int index = 0;
        void AddTask(string name, string icon, string title, string description, DailyMissionType type)
        {
            int target = DailyMissionManager.GetTarget(type);
            int reward = DailyMissionManager.GetReward(type);
            var sourceRect = new RectInt(43, index * spacing, 853, 225);
            BuildDailyTaskCard(scroll.content, name, sourceRect, icon, title, description,
                "0/" + target, 0f, false, reward, type);
            var card = (RectTransform)scroll.content.GetChild(index);
            SetDailyMissionChildRect(card, new RectInt(0, 0, 941, listHeight), sourceRect);
            index++;
        }

        AddTask("Break 50 Blocks", "DailyTaskIconBreak50", "Phá 50 khối",
            "Phá 50 khối trong bất kỳ chế độ nào", DailyMissionType.BreakBlocks);
        AddTask("Play 3 Games", "DailyTaskIconPlay3", "Chơi 3 ván",
            "Kết thúc 3 ván (thắng hoặc thua)", DailyMissionType.PlayGames);
        AddTask("Use 5 Boosters", "DailyTaskIconBoosters5", "Dùng 5 booster",
            "Sử dụng 5 booster trong game", DailyMissionType.UseBoosters);
        AddTask("Win 5 Games", "UI/Achievements/AchievementWin50", "Thắng 5 ván",
            "Thắng tổng cộng 5 ván trong ngày", DailyMissionType.WinGames);
        AddTask("Win Streak 3", "UI/Achievements/AchievementLevel20", "Chuỗi thắng 3",
            "Thắng 3 ván liên tiếp trong ngày", DailyMissionType.WinStreak);
        AddTask("Break 1000 Blocks", "DailyTaskIconBreak50", "Phá 1.000 khối",
            "Phá tổng cộng 1.000 khối trong ngày", DailyMissionType.BreakMoreBlocks);
        scroll.verticalNormalizedPosition = 1f;
    }

    private void BuildDailyMissionHeaderBanner(Transform parent, bool landscape)
    {
        BuildLavenderHeaderBanner(parent, "NHIỆM VỤ HẰNG NGÀY",
            Resources.Load<Sprite>("UI/TabIcons/BottomBarMissions"), "Mission Clipboard Icon", landscape);
    }

    private void BuildLavenderHeaderBanner(Transform parent, string titleText, Sprite rightIconSprite, string rightIconName, bool landscape)
    {
        Vector2 bannerCenter = landscape ? new Vector2(0.5f, 0.77f) : new Vector2(0.5f, 0.805f);
        Vector2 bannerSize = landscape ? new Vector2(0.55f, 0.09f) : new Vector2(0.86f, 0.067f);
        var bannerObject = new GameObject(titleText + " Header Banner", typeof(RectTransform));
        bannerObject.transform.SetParent(parent, false);
        var bannerRect = bannerObject.GetComponent<RectTransform>();
        bannerRect.anchorMin = bannerCenter - bannerSize * 0.5f;
        bannerRect.anchorMax = bannerCenter + bannerSize * 0.5f;
        bannerRect.offsetMin = bannerRect.offsetMax = Vector2.zero;

        var whiteBarObject = new GameObject("Banner White Bar", typeof(RectTransform), typeof(Image));
        whiteBarObject.transform.SetParent(bannerObject.transform, false);
        var whiteBarRect = whiteBarObject.GetComponent<RectTransform>();
        whiteBarRect.anchorMin = new Vector2(0f, 0.10f);
        whiteBarRect.anchorMax = new Vector2(1f, 0.90f);
        whiteBarRect.offsetMin = whiteBarRect.offsetMax = Vector2.zero;
        var whiteBarImage = whiteBarObject.GetComponent<Image>();
        whiteBarImage.sprite = panelSprite;
        whiteBarImage.type = Image.Type.Sliced;
        whiteBarImage.color = Color.white;
        whiteBarImage.raycastTarget = false;

        var pillObject = new GameObject("Banner Text Capsule", typeof(RectTransform), typeof(Image));
        pillObject.transform.SetParent(bannerObject.transform, false);
        var pillRect = pillObject.GetComponent<RectTransform>();
        pillRect.anchorMin = new Vector2(0.15f, 0.20f);
        pillRect.anchorMax = new Vector2(0.87f, 0.80f);
        pillRect.offsetMin = pillRect.offsetMax = Vector2.zero;
        var pillImage = pillObject.GetComponent<Image>();
        pillImage.sprite = CreatePanelSprite(32);
        pillImage.type = Image.Type.Sliced;
        pillImage.color = new Color32(226, 194, 255, 255);
        pillImage.raycastTarget = false;

        var titleObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObject.transform.SetParent(bannerObject.transform, false);
        var titleRect = titleObject.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.175f, 0.18f);
        titleRect.anchorMax = new Vector2(0.855f, 0.82f);
        titleRect.offsetMin = titleRect.offsetMax = Vector2.zero;
        var title = titleObject.GetComponent<TextMeshProUGUI>();
        title.text = Localize.Translate(titleText);
        title.color = new Color32(31, 25, 196, 255);
        HomeMissionTypography.Configure(title, 44, 30, TextAnchor.MiddleCenter);
        Vector2 cubeCenterInHome = landscape ? new Vector2(0.24f, 0.77f) : new Vector2(0.12f, 0.805f);
        Vector2 cubeSizeInHome = landscape ? new Vector2(0.11f, 0.15f) : new Vector2(0.17f, 0.11f);
        Vector2 bannerAnchorSize = bannerRect.anchorMax - bannerRect.anchorMin;
        Vector2 cubeCenter = new Vector2(
            (cubeCenterInHome.x - bannerRect.anchorMin.x) / bannerAnchorSize.x,
            (cubeCenterInHome.y - bannerRect.anchorMin.y) / bannerAnchorSize.y);
        Vector2 cubeSize = new Vector2(cubeSizeInHome.x / bannerAnchorSize.x, cubeSizeInHome.y / bannerAnchorSize.y);
        AddDecorativeIcon(bannerObject.transform, "Banner Cube Cluster", cubesSprite, cubeCenter, cubeSize);
        var cubeIcon = bannerObject.transform.Find("Banner Cube Cluster");
        if (cubeIcon != null) AddWhiteIconOutline(cubeIcon.gameObject, 12f);

        var rightIconObject = new GameObject(rightIconName, typeof(RectTransform), typeof(Image));
        rightIconObject.transform.SetParent(bannerObject.transform, false);
        var rightIconRect = rightIconObject.GetComponent<RectTransform>();
        Vector2 clipboardCenter = new Vector2(0.965f, 0.52f);
        Vector2 clipboardSize = new Vector2(0.16f, 1.42f);
        rightIconRect.anchorMin = clipboardCenter - clipboardSize * 0.5f;
        rightIconRect.anchorMax = clipboardCenter + clipboardSize * 0.5f;
        rightIconRect.offsetMin = rightIconRect.offsetMax = Vector2.zero;
        var rightIconImage = rightIconObject.GetComponent<Image>();
        rightIconImage.sprite = rightIconSprite;
        rightIconImage.preserveAspect = true;
        rightIconImage.raycastTarget = false;
        if (rightIconSprite != null) AddWhiteIconOutline(rightIconObject, 12f);
        else Debug.LogError("Missing " + rightIconName + " sprite.");

    }

    private void BuildDailyLoginPanel(Transform parent)
    {
        var panelRect = new RectInt(43, 420, 854, 247);
        int yShift = panelRect.y - 501;
        var panel = CreateDailyMissionContainer(parent, "Daily Login Reward Panel", panelRect);
        var background = panel.gameObject.AddComponent<Image>();
        background.sprite = Resources.Load<Sprite>("UI/DailyMissions/DailyLoginPanelBackground");
        background.type = Image.Type.Simple;
        background.preserveAspect = false;
        background.raycastTarget = false;

        AddDailyMissionText(panel, "Daily Login Panel Title", "PHẦN THƯỞNG\nĐĂNG NHẬP HÀNG NGÀY", panelRect,
            new RectInt(91, panelRect.y + 12, 455, 88), 32, Color.white, FontStyle.Bold, TextAnchor.MiddleLeft, true, true);

        var dayXs = new[] { 79, 163, 247, 326, 420, 503, 584 };
        for (int day = 0; day < dayXs.Length; day++)
            AddDailyLoginDay(panel, panelRect, day, dayXs[day]);

        AddDailyMissionChildImage(panel, "Daily Login Main Chest", "DailyLoginChest", panelRect,
            new RectInt(632, 515 + yShift, 215, 176), false);

        // The last reward tile sits in front of the large chest in the reference composition.
        AddDailyMissionChildImage(panel, "Daily Login Day 7 Chest", "DailyLoginMiniChest", panelRect,
            new RectInt(595, 622 + yShift, 80, 70), false, new Color32(255, 255, 255, 255));

        AddDailyMissionChildImage(panel, "Daily Login Sparkle Left", "DailyLoginSparkle", panelRect,
            new RectInt(535, 548 + yShift, 40, 40), false);
        AddDailyMissionChildImage(panel, "Daily Login Sparkle Above", "DailyLoginSparkle", panelRect,
            new RectInt(603, 521 + yShift, 31, 31), false);
        AddDailyMissionChildImage(panel, "Daily Login Sparkle Right Top", "DailyLoginSparkle", panelRect,
            new RectInt(820, 536 + yShift, 28, 28), false);
        AddDailyMissionChildImage(panel, "Daily Login Sparkle Right", "DailyLoginSparkle", panelRect,
            new RectInt(827, 606 + yShift, 36, 36), false);
    }

    private void AddDailyLoginDay(RectTransform panel, RectInt panelRect, int dayIndex, int sourceX)
    {
        bool complete = false;
        bool selected = dayIndex == 0;
        int yShift = panelRect.y - 501;
        int tileSize = selected ? 88 : 80;
        int tileX = selected ? sourceX - 4 : sourceX;
        int tileY = (selected ? 612 : 617) + yShift;
        var tileRect = new RectInt(tileX, tileY, tileSize, tileSize);
        string tileSprite = selected ? "DailyLoginDayTileSelected" : (complete ? "DailyLoginDayTileComplete" : "DailyLoginDayTileIdle");

        var buttonObject = new GameObject("Daily Login Day " + (dayIndex + 1), typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(panel, false);
        SetDailyMissionChildRect(buttonObject.GetComponent<RectTransform>(), panelRect, tileRect);
        var tileImage = buttonObject.GetComponent<Image>();
        tileImage.sprite = Resources.Load<Sprite>("UI/DailyMissions/" + tileSprite);
        tileImage.type = Image.Type.Simple;
        tileImage.preserveAspect = false;
        tileImage.raycastTarget = true;
        var button = buttonObject.GetComponent<Button>();
        button.targetGraphic = tileImage;
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(PlayButtonSfx);
        button.onClick.AddListener(() => ClaimDailyLoginDay(dayIndex + 1));

        if (complete)
        {
            AddDailyMissionText(panel, "Day " + (dayIndex + 1) + " Checkmark", "✓", panelRect,
                new RectInt(tileX + 13, tileY + 10, tileSize - 26, tileSize - 20), 50, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter, true, false);
        }
        else if (dayIndex < 6)
        {
            Color tint = selected ? Color.white : (Color)new Color32(145, 132, 190, 255);
            AddDailyMissionChildImage(panel, "Day " + (dayIndex + 1) + " Chest Icon", "DailyLoginMiniChest", panelRect,
                new RectInt(tileX + 12, tileY + 14, tileSize - 24, tileSize - 29), false, tint);
        }

        Color labelColor = selected ? (Color)new Color32(255, 213, 67, 255) : Color.white;
        AddDailyMissionText(panel, "Day " + (dayIndex + 1) + " Label", "D " + (dayIndex + 1), panelRect,
            new RectInt(sourceX - 7, 700 + yShift, 93, 29), 19, labelColor, FontStyle.Bold, TextAnchor.MiddleCenter, true, true);
    }

    private void BuildDailyTaskCard(Transform parent, string name, RectInt cardRect, string iconSprite,
        string title, string description, string progressText, float progress, bool completed, int reward, DailyMissionType? missionType)
    {
        var card = CreateDailyMissionContainer(parent, name, cardRect);
        var background = card.gameObject.AddComponent<Image>();
        background.sprite = Resources.Load<Sprite>("UI/DailyMissions/DailyTaskCardBackground");
        background.type = Image.Type.Sliced;
        background.preserveAspect = false;
        background.raycastTarget = false;

        int verticalGrowth = cardRect.height - 165;
        int verticalOffset = verticalGrowth / 2;
        int iconY = cardRect.y + 17 + verticalOffset;
        var missionIcon = Resources.Load<Sprite>(iconSprite)
            ?? Resources.Load<Sprite>("UI/DailyMissions/" + iconSprite);
        AddDailyMissionChildImage(card, name + " Mission Icon", missionIcon, cardRect,
            new RectInt(63, iconY, 143, 136), true);

        int titleY = cardRect.y + 22;
        int descriptionY = cardRect.y + 86;
        int progressY = cardRect.y + 144;
        AddDailyMissionText(card, name + " Title", title, cardRect,
            new RectInt(220, titleY, 351, 58), 44, new Color32(25, 35, 193, 255), FontStyle.Bold, TextAnchor.MiddleLeft, true, true);
        AddDailyMissionText(card, name + " Description", description, cardRect,
            new RectInt(221, descriptionY, 351, 42), 26, new Color32(91, 76, 143, 255), FontStyle.Bold, TextAnchor.MiddleLeft, false, false);

        var progressRect = new RectInt(221, progressY, 342, 46);
        AddDailyMissionChildImage(card, name + " Progress Track", "DailyMissionProgressTrack", cardRect, progressRect, false);
        int fillWidth = Mathf.RoundToInt((progressRect.width - 6) * (missionType.HasValue ? 1f : Mathf.Clamp01(progress)));
        var fillRect = new RectInt(progressRect.x + 3, progressRect.y + 3, Mathf.Max(1, fillWidth), progressRect.height - 6);
        var fillObject = AddDailyMissionChildImage(card, name + " Progress Fill",
            completed ? "DailyMissionProgressFillGreen" : "DailyMissionProgressFillBlue", cardRect, fillRect, false);
        var fillImage = fillObject.GetComponent<Image>();
        fillImage.type = Image.Type.Sliced;
        var progressLabel = AddDailyMissionText(card, name + " Progress Text", progressText, cardRect,
            new RectInt(progressRect.x + 3, progressRect.y + 1, progressRect.width - 6, progressRect.height - 2),
            32, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter, true, true);

        var rewardRect = new RectInt(582, cardRect.y + 23 + verticalOffset, 108, 124);
        var rewardFrame = AddDailyMissionChildImage(card, name + " Reward Frame", "DailyMissionRewardFrame", cardRect, rewardRect, false);
        var rewardIcon = AddDailyMissionChildImage(card, name + " Reward Coin Icon", Resources.Load<Sprite>("UI/ShopItems/Gold100"), cardRect,
            new RectInt(589, cardRect.y + 29 + verticalOffset, 95, 73), true, Color.white);
        var rewardAmount = AddDailyMissionText(card, name + " Reward Amount", reward.ToString(), cardRect,
            new RectInt(590, cardRect.y + 129, 92, 48), 36, new Color32(191, 77, 12, 255), FontStyle.Bold, TextAnchor.MiddleCenter, true, true);

        if (missionType.HasValue)
        {
            var type = missionType.Value;
            var rewardButton = PrepareDailyMissionRewardButton(rewardIcon, () => ClaimDailyMission(type));
            card.gameObject.AddComponent<DailyMissionCardUI>().Bind(type, progressLabel, fillImage, rewardButton,
                rewardFrame, rewardIcon, rewardAmount.gameObject);
        }
    }

    private RectTransform CreateDailyMissionContainer(Transform parent, string name, RectInt sourceRect)
    {
        var container = new GameObject(name, typeof(RectTransform));
        container.transform.SetParent(parent, false);
        var rect = container.GetComponent<RectTransform>();
        SetDailyMissionRect(rect, sourceRect);
        return rect;
    }

    private GameObject AddDailyMissionChildImage(RectTransform parent, string name, string resourceName,
        RectInt parentRect, RectInt childRect, bool preserveAspect)
    {
        return AddDailyMissionChildImage(parent, name, resourceName, parentRect, childRect, preserveAspect, Color.white);
    }

    private GameObject AddDailyMissionChildImage(RectTransform parent, string name, string resourceName,
        RectInt parentRect, RectInt childRect, bool preserveAspect, Color tint)
    {
        return AddDailyMissionChildImage(parent, name,
            Resources.Load<Sprite>("UI/DailyMissions/" + resourceName), parentRect, childRect, preserveAspect, tint);
    }

    private GameObject AddDailyMissionChildImage(RectTransform parent, string name, Sprite sprite,
        RectInt parentRect, RectInt childRect, bool preserveAspect)
    {
        return AddDailyMissionChildImage(parent, name, sprite, parentRect, childRect, preserveAspect, Color.white);
    }

    private GameObject AddDailyMissionChildImage(RectTransform parent, string name, Sprite sprite,
        RectInt parentRect, RectInt childRect, bool preserveAspect, Color tint)
    {
        var imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        SetDailyMissionChildRect(imageObject.GetComponent<RectTransform>(), parentRect, childRect);
        var image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = preserveAspect;
        image.color = tint;
        image.raycastTarget = false;
        if (sprite == null) Debug.LogError("Missing daily missions child sprite: " + name);
        return imageObject;
    }

    private TMP_Text AddDailyMissionText(RectTransform parent, string name, string value, RectInt parentRect,
        RectInt childRect, int size, Color color, FontStyle style, TextAnchor alignment, bool useRoundedFont, bool addOutline)
    {
        var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        SetDailyMissionChildRect(textObject.GetComponent<RectTransform>(), parentRect, childRect);
        var label = textObject.GetComponent<TextMeshProUGUI>();
        label.text = Localize.Translate(value);
        label.color = color;
        HomeMissionTypography.Configure(label, size, Mathf.Max(16, size - 8), alignment, value.Contains("\n"));
        return label;
    }
    private Button PrepareDailyMissionRewardButton(GameObject rewardIcon, Action onClick = null)
    {
        var image = rewardIcon.GetComponent<Image>();
        image.raycastTarget = true;
        var button = rewardIcon.GetComponent<Button>();
        if (button == null) button = rewardIcon.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        if (onClick != null)
        {
            button.onClick.AddListener(PlayButtonSfx);
            button.onClick.AddListener(() => onClick());
        }
        return button;
    }

    private static void SetDailyMissionChildRect(RectTransform rect, RectInt parentRect, RectInt childRect)
    {
        float width = Mathf.Max(1f, parentRect.width);
        float height = Mathf.Max(1f, parentRect.height);
        rect.anchorMin = new Vector2((childRect.x - parentRect.x) / width,
            1f - (childRect.y - parentRect.y + childRect.height) / height);
        rect.anchorMax = new Vector2((childRect.x - parentRect.x + childRect.width) / width,
            1f - (childRect.y - parentRect.y) / height);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private GameObject AddDailyMissionImage(Transform parent, string name, string resourceName, RectInt sourceRect)
    {
        return AddDailyMissionImage(parent, name, Resources.Load<Sprite>("UI/DailyMissions/" + resourceName), sourceRect);
    }

    private GameObject AddDailyMissionImage(Transform parent, string name, Sprite sprite, RectInt sourceRect)
    {
        var imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        SetDailyMissionRect(imageObject.GetComponent<RectTransform>(), sourceRect);
        var image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.color = Color.white;
        image.raycastTarget = false;
        if (sprite == null) Debug.LogError("Missing daily missions sprite: " + name);
        return imageObject;
    }

    private Button AddDailyMissionButton(Transform parent, string name, string resourceName, RectInt sourceRect, Action onClick)
    {
        var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        SetDailyMissionRect(buttonObject.GetComponent<RectTransform>(), sourceRect);
        var image = buttonObject.GetComponent<Image>();
        image.sprite = Resources.Load<Sprite>("UI/DailyMissions/" + resourceName);
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.color = Color.white;
        image.raycastTarget = true;
        var button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(PlayButtonSfx);
        if (onClick != null) button.onClick.AddListener(() => onClick());
        if (image.sprite == null) Debug.LogError("Missing daily missions button sprite: " + resourceName);
        return button;
    }

    private static void SetDailyMissionRect(RectTransform rect, RectInt sourceRect)
    {
        const float sourceWidth = 941f;
        const float sourceHeight = 1672f;
        rect.anchorMin = new Vector2(sourceRect.x / sourceWidth, 1f - (sourceRect.y + sourceRect.height) / sourceHeight);
        rect.anchorMax = new Vector2((sourceRect.x + sourceRect.width) / sourceWidth, 1f - sourceRect.y / sourceHeight);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private GameObject BuildShopPage(Transform parent)
    {
        var page = new GameObject("Shop Page", typeof(RectTransform));
        page.transform.SetParent(parent, false);
        Stretch(page.GetComponent<RectTransform>());
        BuildShopUi(page.transform, false);
        return page;
    }

    private void BuildShopUi(Transform page, bool landscape)
    {
        BuildShopBanner(page, landscape);

        float categoryY = landscape ? 0.666f : 0.742f;
        float screenAspect = 1080f / 1920f;
        float categoryHeight = Mathf.Clamp(0.44f * screenAspect / 4.4f, 0.045f, landscape ? 0.13f : 0.065f);
        shopGoldTabButton = Button(page, "SHOP_GOLD_TAB", "VÀNG",
            new Vector2(0.275f, categoryY), new Vector2(0.44f, categoryHeight), new Color32(93, 51, 215, 255), 34,
            () => SetShopCategory(false));
        shopBoosterTabButton = Button(page, "SHOP_BOOSTER_TAB", "BOOSTER",
            new Vector2(0.725f, categoryY), new Vector2(0.44f, categoryHeight), new Color32(93, 51, 215, 255), 34,
            () => SetShopCategory(true));
        shopGoldTabImage = ConfigureShopCategoryButton(shopGoldTabButton);
        shopBoosterTabImage = ConfigureShopCategoryButton(shopBoosterTabButton);
        StyleShopCategoryButton(shopGoldTabButton);
        StyleShopCategoryButton(shopBoosterTabButton);

        Vector2 viewportMin = landscape ? new Vector2(0.01f, 0.14f) : new Vector2(0.01f, 0.13f);
        float viewportTop = categoryY - categoryHeight * 0.5f;
        Vector2 viewportMax = new Vector2(0.99f, viewportTop);
        ScrollRect goldScroll;
        ScrollRect boosterScroll;
        shopGoldViewport = BuildShopViewport(page, "Gold Shop Viewport", viewportMin, viewportMax, out goldScroll);
        shopBoosterViewport = BuildShopViewport(page, "Booster Shop Viewport", viewportMin, viewportMax, out boosterScroll);

        BuildShopProductGrid(goldScroll, new[]
        {
            new ShopProduct("500 VÀNG", ShopProductKind.Gold, 0),
            new ShopProduct("1.200 VÀNG", ShopProductKind.Gold, 1),
            new ShopProduct("2.500 VÀNG", ShopProductKind.Gold, 2),
            new ShopProduct("6.500 VÀNG", ShopProductKind.Gold, 3),
            new ShopProduct("14.000 VÀNG", ShopProductKind.Gold, 4),
            new ShopProduct("30.000 VÀNG", ShopProductKind.Gold, 5),
            new ShopProduct("5 TIM", ShopProductKind.Heart, 5),
            new ShopProduct("20 TIM", ShopProductKind.Heart, 20)
        }, landscape);
        BuildShopProductGrid(boosterScroll, new[]
        {
            new ShopProduct("KÍNH LÚP", ShopProductKind.Booster, 0),
            new ShopProduct("KÉO", ShopProductKind.Booster, 1),
            new ShopProduct("XÁO TRỘN", ShopProductKind.Booster, 2),
            new ShopProduct("LẤY VẬT THỂ", ShopProductKind.Booster, 3)
        }, landscape);
        SetShopCategory(shopBoosterSelected);
        RefreshShopPurchaseLabels();
#if UNITY_EDITOR
        PrepareShopCardLightSweepsForEditor();
#endif
    }

    private void BuildShopBanner(Transform parent, bool landscape)
    {
        var banner = Button(parent, "SHOP_TITLE_BANNER", "CỬA HÀNG",
            landscape ? new Vector2(0.5f, 0.77f) : new Vector2(0.5f, 0.805f),
            landscape ? new Vector2(0.55f, 0.09f) : new Vector2(0.86f, 0.067f),
            Color.white, 36, () => { });
        DestroyUIObject(banner.GetComponent<Outline>());
        DestroyUIObject(banner.GetComponent<Shadow>());
        banner.GetComponent<Image>().color = Color.clear;

        var whiteBar = Panel(banner.transform, "Banner White Bar", Color.white,
            new Vector2(0f, 0.10f), new Vector2(1f, 0.90f));
        whiteBar.GetComponent<Image>().raycastTarget = false;
        DestroyUIObject(whiteBar.GetComponent<Outline>());
        whiteBar.transform.SetAsFirstSibling();

        var pill = Panel(banner.transform, "Banner Text Capsule", new Color32(226, 194, 255, 255),
            new Vector2(0.15f, 0.20f), new Vector2(0.87f, 0.80f));
        var pillImage = pill.GetComponent<Image>();
        pillImage.sprite = CreatePanelSprite(32);
        pillImage.raycastTarget = false;
        DestroyUIObject(pill.GetComponent<Outline>());
        pill.transform.SetSiblingIndex(1);

        var title = banner.transform.Find("Label").GetComponent<Text>();
        if (roundedFont != null) title.font = roundedFont;
        title.text = Localize.Translate("CỬA HÀNG");
        title.color = new Color32(31, 25, 196, 255);
        title.fontStyle = FontStyle.Bold;
        title.fontSize = 50;
        title.resizeTextForBestFit = true;
        title.resizeTextMinSize = 18;
        title.resizeTextMaxSize = 50;
        title.horizontalOverflow = HorizontalWrapMode.Wrap;
        title.verticalOverflow = VerticalWrapMode.Overflow;
        SetButtonLabelArea(banner, new Vector2(0.21f, 0.24f), new Vector2(0.64f, 0.76f));

        var bannerRect = banner.GetComponent<RectTransform>();
        Vector2 bannerSize = bannerRect.anchorMax - bannerRect.anchorMin;
        Vector2 cubeHomeCenter = landscape ? new Vector2(0.24f, 0.77f) : new Vector2(0.12f, 0.805f);
        Vector2 cubeHomeSize = landscape ? new Vector2(0.11f, 0.15f) : new Vector2(0.17f, 0.11f);
        Vector2 cubeCenter = new Vector2((cubeHomeCenter.x - bannerRect.anchorMin.x) / bannerSize.x,
            (cubeHomeCenter.y - bannerRect.anchorMin.y) / bannerSize.y);
        Vector2 cubeSize = new Vector2(cubeHomeSize.x / bannerSize.x, cubeHomeSize.y / bannerSize.y);
        AddButtonIcon(banner, cubesSprite, cubeCenter, cubeSize, false, "Banner Cube Cluster");
        var cubes = banner.transform.Find("Banner Cube Cluster");
        if (cubes != null) AddWhiteIconOutline(cubes.gameObject, 12f);

        Vector2 goodsSize = landscape ? new Vector2(0.36f, 1.45f) : new Vector2(0.44f, 1.65f);
        AddDecorativeIcon(banner.transform, "Shop Banner Goods", shopBannerGoodsSprite,
            new Vector2(0.86f, 0.50f), goodsSize);
    }

    private void StyleShopCategoryButton(Button button)
    {
        var text = button.transform.Find("Label").GetComponent<Text>();
        if (roundedFont != null) text.font = roundedFont;
        text.fontSize = 40;
        text.resizeTextMinSize = 24;
        text.resizeTextMaxSize = 40;
        text.color = Color.white;
        text.fontStyle = FontStyle.Bold;
        var shadow = text.GetComponent<Shadow>();
        if (shadow != null)
        {
            shadow.effectColor = new Color32(37, 12, 83, 245);
            shadow.effectDistance = new Vector2(0f, -2f);
        }
    }

    private Image ConfigureShopCategoryButton(Button button)
    {
        var hitImage = button.GetComponent<Image>();
        hitImage.sprite = null;
        hitImage.color = Color.clear;
        hitImage.raycastTarget = true;
        button.transition = Selectable.Transition.None;
        var outline = button.GetComponent<Outline>();
        if (outline != null) DestroyUIObject(outline);
        var shadow = button.GetComponent<Shadow>();
        if (shadow != null) DestroyUIObject(shadow);

        var faceObject = new GameObject("Tab Sprite", typeof(RectTransform), typeof(Image));
        faceObject.transform.SetParent(button.transform, false);
        faceObject.transform.SetAsFirstSibling();
        var faceRect = faceObject.GetComponent<RectTransform>();
        faceRect.anchorMin = new Vector2(0f, -0.25f);
        faceRect.anchorMax = new Vector2(1f, 1.25f);
        faceRect.offsetMin = faceRect.offsetMax = Vector2.zero;
        var face = faceObject.GetComponent<Image>();
        face.sprite = shopTabSelectedSprite;
        face.type = Image.Type.Simple;
        face.preserveAspect = false;
        face.color = Color.white;
        face.raycastTarget = false;
        return face;
    }

    private GameObject BuildShopViewport(Transform parent, string name, Vector2 min, Vector2 max, out ScrollRect scroll)
    {
        var viewport = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        viewport.transform.SetParent(parent, false);
        var viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = min;
        viewportRect.anchorMax = max;
        viewportRect.offsetMin = viewportRect.offsetMax = Vector2.zero;
        var viewportImage = viewport.GetComponent<Image>();
        viewportImage.sprite = panelSprite;
        viewportImage.type = Image.Type.Sliced;
        viewportImage.color = new Color32(24, 13, 92, 36);
        viewportImage.raycastTarget = true;

        var content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(viewport.transform, false);
        var contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;

        scroll = viewport.GetComponent<ScrollRect>();
        scroll.content = contentRect;
        scroll.viewport = viewportRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.inertia = true;
        scroll.scrollSensitivity = 1f;

        var scrollbarObject = new GameObject("Vertical Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        scrollbarObject.transform.SetParent(viewport.transform, false);
        var scrollbarRect = scrollbarObject.GetComponent<RectTransform>();
        scrollbarRect.anchorMin = new Vector2(0.981f, 0.035f);
        scrollbarRect.anchorMax = new Vector2(0.998f, 0.965f);
        scrollbarRect.offsetMin = scrollbarRect.offsetMax = Vector2.zero;
        var scrollbarTrack = scrollbarObject.GetComponent<Image>();
        scrollbarTrack.sprite = panelSprite;
        scrollbarTrack.type = Image.Type.Sliced;
        scrollbarTrack.color = new Color32(44, 21, 143, 150);
        var handleObject = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleObject.transform.SetParent(scrollbarObject.transform, false);
        var handleRect = handleObject.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0f, 0.55f);
        handleRect.anchorMax = new Vector2(1f, 1f);
        handleRect.offsetMin = handleRect.offsetMax = Vector2.zero;
        var handleImage = handleObject.GetComponent<Image>();
        handleImage.sprite = panelSprite;
        handleImage.type = Image.Type.Sliced;
        handleImage.color = new Color32(255, 242, 255, 250);
        var scrollbar = scrollbarObject.GetComponent<Scrollbar>();
        scrollbar.handleRect = handleRect;
        scrollbar.targetGraphic = handleImage;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.size = 0.42f;
        scrollbar.value = 1f;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        return viewport;
    }

    private enum ShopProductKind { Gold, Heart, Booster }

    private struct ShopProduct
    {
        public string title;
        public ShopProductKind kind;
        public int index;
        public ShopProduct(string title, ShopProductKind kind, int index)
        {
            this.title = title;
            this.kind = kind;
            this.index = index;
        }
    }

    private void BuildShopProductGrid(ScrollRect scroll, ShopProduct[] products, bool landscape)
    {
        Canvas.ForceUpdateCanvases();
        float canvasHeight = landscape ? 1080f : 1920f;
        float viewportWidth = scroll.viewport.rect.width;
        float viewportHeight = scroll.viewport.rect.height;
        if (viewportWidth <= 0f)
            viewportWidth = 1080f * 0.93f;
        if (viewportHeight <= 0f)
            viewportHeight = canvasHeight * (landscape ? 0.47f : 0.544f);

        float cardWidth = viewportWidth * 0.5f;
        int rows = Mathf.CeilToInt(products.Length / 2f);
        float topInset = viewportHeight * 0.005f;
        float targetVisibleRows = landscape ? 2f : 3.4f;
        float visibleRows = Mathf.Min(rows, targetVisibleRows);
        float fillHeight = (viewportHeight - topInset * 2f) / visibleRows;
        float cardHeight = Mathf.Max(fillHeight, cardWidth / 1.50f);
        scroll.content.sizeDelta = new Vector2(0f, topInset * 2f + rows * cardHeight);

        var frameLayer = CreateShopGridLayer(scroll.content, "Shop Card Frames");
        var itemLayer = CreateShopGridLayer(scroll.content, "Shop Card Items");

        for (int i = 0; i < products.Length; i++)
        {
            int row = i / 2;
            int column = i % 2;
            float xAnchor = column == 0 ? 0.25f : 0.75f;
            float y = -topInset - cardHeight * 0.5f - row * cardHeight;
            var anchor = new Vector2(xAnchor, 1f);
            var cardSize = new Vector2(cardWidth, cardHeight);
            CreateShopCardBackground(frameLayer, anchor, cardSize, y);
            CreateShopProductCard(itemLayer, products[i], anchor, cardSize, y);
        }
    }

    private RectTransform CreateShopGridLayer(Transform parent, string name)
    {
        var layer = new GameObject(name, typeof(RectTransform));
        layer.transform.SetParent(parent, false);
        var rect = layer.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    private void CreateShopCardBackground(Transform parent, Vector2 anchor, Vector2 size, float yOffset)
    {
        var frame = new GameObject("Shop Card Frame", typeof(RectTransform), typeof(Image));
        frame.transform.SetParent(parent, false);
        var frameRect = frame.GetComponent<RectTransform>();
        frameRect.anchorMin = frameRect.anchorMax = anchor;
        frameRect.anchoredPosition = new Vector2(0f, yOffset);
        var frameImage = frame.GetComponent<Image>();
        frameImage.sprite = shopCardSprite;
        frameImage.type = Image.Type.Simple;
        frameImage.preserveAspect = true;
        frameImage.color = Color.white;
        frameImage.raycastTarget = false;
        frameImage.SetNativeSize();
        FitNativeSprite(frameRect, size);
    }

    private void CreateShopProductCard(RectTransform content, ShopProduct product, Vector2 anchor, Vector2 size, float yOffset)
    {
        var card = Panel(content, "Shop Item - " + product.title, new Color32(208, 164, 255, 255), anchor, anchor);
        var cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = cardRect.anchorMax = anchor;
        cardRect.sizeDelta = size;
        cardRect.anchoredPosition = new Vector2(0f, yOffset);
        var cardImage = card.GetComponent<Image>();
        cardImage.sprite = null;
        cardImage.color = Color.clear;
        cardImage.raycastTarget = false;
        card.AddComponent<RectMask2D>();
        var cardOutline = card.GetComponent<Outline>();
        if (cardOutline != null) DestroyUIObject(cardOutline);
        var cardShadow = card.GetComponent<Shadow>();
        if (cardShadow != null) DestroyUIObject(cardShadow);

        if (product.kind == ShopProductKind.Gold) BuildGoldPackArt(card.transform, product.index);
        else if (product.kind == ShopProductKind.Heart) BuildHeartPackArt(card.transform, product.index);
        else BuildBoosterArt(card.transform, product.index);

        var title = Label(card.transform, product.title, 46, new Vector2(0.5f, 0.43f), new Vector2(0.92f, 0.14f), Color.white, FontStyle.Bold);
        if (roundedFont != null) title.font = roundedFont;
        title.resizeTextForBestFit = true;
        title.resizeTextMinSize = 24;
        title.resizeTextMaxSize = 46;
        var titleOutline = title.gameObject.AddComponent<Outline>();
        titleOutline.effectColor = new Color32(21, 12, 160, 245);
        titleOutline.effectDistance = new Vector2(2f, -3f);
        title.raycastTarget = false;

        float buyWidthFraction = 0.92f;
        float buttonAspect = shopBuyFaceSprite != null
            ? shopBuyFaceSprite.rect.width / Mathf.Max(1f, shopBuyFaceSprite.rect.height)
            : 5.5f;
        float buyHeightFraction = Mathf.Min(0.34f, buyWidthFraction * (size.x / Mathf.Max(1f, size.y)) / buttonAspect);
        float buyBottomInset = 0.025f;
        string buyButtonName = product.kind == ShopProductKind.Gold
            ? "BUY_GOLD_PACK_" + (product.index + 1)
            : "BUY " + product.title;
        var buy = Button(card.transform, buyButtonName, "MUA",
            new Vector2(0.5f, buyBottomInset + buyHeightFraction * 0.5f),
            new Vector2(buyWidthFraction, buyHeightFraction), new Color32(40, 224, 22, 255), 43,
            () => HandleShopProductPurchase(product));
        var buyHitImage = buy.GetComponent<Image>();
        buyHitImage.sprite = shopBuyFaceSprite;
        buyHitImage.type = Image.Type.Simple;
        buyHitImage.preserveAspect = true;
        buyHitImage.color = Color.white;
        buyHitImage.raycastTarget = true;
        buy.transition = Selectable.Transition.None;
        var buyOutline = buy.GetComponent<Outline>();
        if (buyOutline != null) DestroyUIObject(buyOutline);
        var buyShadow = buy.GetComponent<Shadow>();
        if (buyShadow != null) DestroyUIObject(buyShadow);
        var buyLabel = buy.transform.Find("Label").GetComponent<Text>();
        if (roundedFont != null) buyLabel.font = roundedFont;
        buyLabel.fontSize = 43;
        buyLabel.resizeTextMinSize = 24;
        buyLabel.resizeTextMaxSize = 43;
        buyLabel.resizeTextForBestFit = true;
        buyLabel.alignment = TextAnchor.MiddleCenter;
        var buyTextShadow = buyLabel.GetComponent<Shadow>();
        if (buyTextShadow != null)
        {
            buyTextShadow.effectColor = new Color32(5, 125, 0, 235);
            buyTextShadow.effectDistance = new Vector2(0f, -2f);
        }
    }

    private void HandleShopProductPurchase(ShopProduct product)
    {
        if (product.kind == ShopProductKind.Gold)
            IAPManager.BuyCoinPack(product.index + 1);
        else if (product.kind == ShopProductKind.Heart)
            BuyHeartPack(product.index);
        else
            ShowShopItemInfo(product.title);
    }

    private void RefreshShopPurchaseLabels()
    {
        if (homeCanvasObject == null) return;
        RefreshHeartShopPrices();

        foreach (var button in homeCanvasObject.GetComponentsInChildren<Button>(true))
        {
            if (!TryGetCoinPackNumber(button.name, out int packNumber)) continue;

            int amount = IAPManager.GetCoinPackGoldAmount(packNumber);
            string amountText = FormatShopGoldAmount(amount);
            string price = NumericPriceText(IAPManager.GetCoinPackLocalizedPrice(packNumber));
            var labelTransform = button.transform.Find("Label");
            var label = labelTransform != null ? labelTransform.GetComponent<Text>() : null;
            if (label == null) label = button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = string.IsNullOrWhiteSpace(price) ? Localize.Text("ĐANG TẢI GIÁ", "LOADING PRICE") : price;
                label.fontStyle = FontStyle.Bold;
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 15;
                label.resizeTextMaxSize = 30;
                label.alignment = TextAnchor.MiddleCenter;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.verticalOverflow = VerticalWrapMode.Overflow;
            }
            else
            {
                var tmpLabel = button.GetComponentInChildren<TMP_Text>(true);
                if (tmpLabel != null)
                {
                    tmpLabel.text = string.IsNullOrWhiteSpace(price) ? Localize.Text("ĐANG TẢI GIÁ", "LOADING PRICE") : price;
                    tmpLabel.fontStyle = FontStyles.Bold;
                    tmpLabel.enableAutoSizing = true;
                    tmpLabel.fontSizeMin = 15f;
                    tmpLabel.fontSizeMax = 30f;
                    tmpLabel.alignment = TextAlignmentOptions.Center;
                    tmpLabel.overflowMode = TextOverflowModes.Overflow;
                }
            }

            UpdateShopGoldAmountTitle(button, amountText);
        }
    }

    private void HandleMoneyAwarded(int amount, int newBalance)
    {
        if (amount <= 0) return;

        int coinCount = Mathf.Clamp(6 + Mathf.FloorToInt(Mathf.Log10(Mathf.Max(1, amount)) * 1.5f), 8, 13);
        int startingBalance = Mathf.Max(0, newBalance - amount);
        float countDuration = 0.60f + Mathf.Max(0, coinCount - 1) * 0.045f;
        StartCoinCountAnimation(startingBalance, newBalance, countDuration);

        PlayCoinAwardEffect(amount, coinCount);
    }

    private void PlayCoinAwardEffect(int amount, int coinCount)
    {
        if (homeCanvasObject == null || coinSprite == null || coinCount <= 0) return;

        if (coinHudButtonRect == null)
            coinHudButtonRect = SceneComponent<RectTransform>("COINS");
        if (coinHudButtonRect == null) return;
        if (coinHudIconRect == null)
        {
            coinHudIconRect = coinHudButtonRect.Find("Button Icon") as RectTransform;
            if (coinHudIconRect != null) coinHudIconBaseScale = coinHudIconRect.localScale;
        }
        if (coinHudIconOutlineRect == null)
        {
            coinHudIconOutlineRect = FindCoinHudIconOutline(coinHudIconRect);
            if (coinHudIconOutlineRect != null) coinHudIconOutlineBaseScale = coinHudIconOutlineRect.localScale;
        }

        EnsureCurrencyFlightOverlay();
        if (buttonEffectsOverlay == null) return;

        RectTransform sourceRect = FindCoinAwardSource(amount);
        if (sourceRect == null) sourceRect = coinHudButtonRect;

        Canvas canvas = sceneHomeCanvas != null ? sceneHomeCanvas : homeCanvasObject.GetComponentInParent<Canvas>();
        Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
        Vector2 startScreen = RectTransformUtility.WorldToScreenPoint(eventCamera, sourceRect.position);
        Vector2 endScreen = RectTransformUtility.WorldToScreenPoint(eventCamera, coinHudButtonRect.position);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(buttonEffectsOverlay, startScreen, eventCamera, out Vector2 startPoint) ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(buttonEffectsOverlay, endScreen, eventCamera, out Vector2 endPoint))
            return;

        PlayCurrencyFlightSound();
        float coinSize = Mathf.Clamp(buttonEffectsOverlay.rect.height * 0.045f, 46f, 62f);
        for (int i = 0; i < coinCount; i++)
        {
            var coinObject = new GameObject("Purchased Coin Flight", typeof(RectTransform), typeof(Image));
            coinObject.transform.SetParent(buttonEffectsOverlay, false);
            coinObject.transform.SetAsLastSibling();

            var coinRect = coinObject.GetComponent<RectTransform>();
            coinRect.anchorMin = coinRect.anchorMax = new Vector2(0.5f, 0.5f);
            coinRect.pivot = new Vector2(0.5f, 0.5f);
            float sizeVariation = UnityEngine.Random.Range(0.90f, 1.10f);
            coinRect.sizeDelta = new Vector2(coinSize * sizeVariation, coinSize * sizeVariation);

            var image = coinObject.GetComponent<Image>();
            image.sprite = coinSprite;
            image.preserveAspect = true;
            image.color = Color.white;
            image.raycastTarget = false;
            image.maskable = false;

            Vector2 scatter = new Vector2(UnityEngine.Random.Range(-22f, 22f), UnityEngine.Random.Range(-14f, 14f));
            float delay = i * 0.045f;
            var flight = coinObject.AddComponent<MoveCoinOnUI>();
            flight.StartFlight(
                startPoint + scatter,
                endPoint,
                UnityEngine.Random.Range(0.42f, 0.60f),
                delay,
                UnityEngine.Random.Range(75f, 145f),
                PulseCoinHudIcon);
        }
    }

    private RectTransform FindCoinAwardSource(int amount)
    {
        if (dailyMissionRewardSource != null && dailyMissionRewardSource.gameObject.activeInHierarchy)
            return dailyMissionRewardSource;
        if (achievementRewardSource != null && achievementRewardSource.gameObject.activeInHierarchy)
            return achievementRewardSource;
        if (shopBoosterViewport != null && shopBoosterViewport.activeInHierarchy)
        {
            const string prefix = "BUY_BOOSTER_OFFER_";
            foreach (var button in shopBoosterViewport.GetComponentsInChildren<Button>(true))
            {
                if (!button.gameObject.activeInHierarchy ||
                    !button.name.StartsWith(prefix, StringComparison.Ordinal) ||
                    !int.TryParse(button.name.Substring(prefix.Length), out int packNumber) ||
                    packNumber < 1 || packNumber > BoosterOffers.Count ||
                    BoosterOffers[packNumber - 1].coins != amount)
                    continue;
                return button.transform as RectTransform;
            }
        }

        if (shopGoldViewport != null && shopGoldViewport.activeInHierarchy)
        {
            foreach (var button in shopGoldViewport.GetComponentsInChildren<Button>(true))
            {
                if (!button.gameObject.activeInHierarchy ||
                    !TryGetCoinPackNumber(button.name, out int packNumber) ||
                    IAPManager.GetCoinPackGoldAmount(packNumber) != amount)
                    continue;
                return button.transform as RectTransform;
            }
        }

        if (outOfHeartPanel != null && outOfHeartPanel.activeInHierarchy)
            return outOfHeartPanel.GetComponent<RectTransform>();

        if (tabPages != null && activeTabIndex >= 0 && activeTabIndex < tabPages.Length && tabPages[activeTabIndex] != null)
            return tabPages[activeTabIndex].GetComponent<RectTransform>();

        return homeCanvasObject != null ? homeCanvasObject.transform.Find("Home") as RectTransform : null;
    }

    private void EnsureCurrencyFlightOverlay()
    {
        if (homeCanvasObject == null) return;

        if (buttonEffectsOverlay == null)
        {
            var overlayObject = new GameObject("Currency Flight Effects", typeof(RectTransform));
            buttonEffectsOverlay = overlayObject.GetComponent<RectTransform>();
        }
        BringCurrencyFlightOverlayToFront();
    }

    private void BringCurrencyFlightOverlayToFront()
    {
        if (buttonEffectsOverlay == null || homeCanvasObject == null) return;

        // Share the canvas coordinate space and draw above sibling purchase panels.
        // Keeping the overlay outside shop viewports also avoids their scroll masks.
        if (buttonEffectsOverlay.parent != homeCanvasObject.transform)
        {
            buttonEffectsOverlay.SetParent(homeCanvasObject.transform, false);
            buttonEffectsOverlay.localScale = Vector3.one;
            buttonEffectsOverlay.localRotation = Quaternion.identity;
            buttonEffectsOverlay.pivot = new Vector2(0.5f, 0.5f);
            Stretch(buttonEffectsOverlay);
        }
        var canvasGroup = buttonEffectsOverlay.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = buttonEffectsOverlay.gameObject.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        buttonEffectsOverlay.SetAsLastSibling();
    }

    private static void PlayCurrencyFlightSound()
    {
        var audio = AudioManager.Instance;
        if (audio != null && audio.coinCollect != null)
            audio.PlaySFX(audio.coinCollect);
    }

    private void PulseCoinHudIcon()
    {
        if (coinHudIconRect == null) return;
        coinHudPulseRemaining = 0.34f;
    }

    private static RectTransform FindCoinHudIconOutline(RectTransform icon)
    {
        if (icon == null || icon.parent == null) return null;
        string iconName = icon.name;
        foreach (Transform sibling in icon.parent)
        {
            if (sibling == icon) continue;
            string siblingName = sibling.name;
            bool identifiesIcon = siblingName.IndexOf(iconName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  siblingName.IndexOf("icon", StringComparison.OrdinalIgnoreCase) >= 0;
            bool identifiesOutline = siblingName.IndexOf("border", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     siblingName.IndexOf("outline", StringComparison.OrdinalIgnoreCase) >= 0;
            if (identifiesIcon && identifiesOutline)
                return sibling as RectTransform;
        }
        return null;
    }

    private void UpdateCoinHudPulse(float deltaTime)
    {
        if (coinHudIconRect == null || coinHudPulseRemaining <= 0f) return;

        coinHudPulseRemaining = Mathf.Max(0f, coinHudPulseRemaining - deltaTime);
        float progress = 1f - coinHudPulseRemaining / 0.34f;
        float scale = 1f + 0.28f * Mathf.Sin(progress * Mathf.PI);
        coinHudIconRect.localScale = coinHudIconBaseScale * scale;
        if (coinHudIconOutlineRect != null)
            coinHudIconOutlineRect.localScale = coinHudIconOutlineBaseScale * scale;
        if (coinHudPulseRemaining <= 0f)
        {
            coinHudIconRect.localScale = coinHudIconBaseScale;
            if (coinHudIconOutlineRect != null)
                coinHudIconOutlineRect.localScale = coinHudIconOutlineBaseScale;
        }
    }

    private void UpdateShopGoldAmountTitle(Button purchaseButton, string amountText)
    {
        Transform stopAt = shopGoldViewport != null ? shopGoldViewport.transform : null;
        for (Transform card = purchaseButton.transform.parent; card != null && card != stopAt; card = card.parent)
        {
            foreach (var text in card.GetComponentsInChildren<Text>(true))
            {
                if (text.transform == purchaseButton.transform || text.transform.IsChildOf(purchaseButton.transform))
                    continue;
                if (text.text.IndexOf("VÀNG", StringComparison.OrdinalIgnoreCase) < 0 &&
                    text.text.IndexOf("GOLD", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                text.text = Localize.Format("{0} VÀNG", "{0} GOLD", amountText);
                return;
            }
            foreach (var text in card.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.transform == purchaseButton.transform || text.transform.IsChildOf(purchaseButton.transform))
                    continue;
                if (text.text.IndexOf("VÀNG", StringComparison.OrdinalIgnoreCase) < 0 &&
                    text.text.IndexOf("GOLD", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                text.text = Localize.Format("{0} VÀNG", "{0} GOLD", amountText);
                return;
            }
        }
    }

    private static bool TryGetCoinPackNumber(string buttonName, out int packNumber)
    {
        const string indexedNamePrefix = "BUY_GOLD_PACK_";
        if (buttonName.StartsWith(indexedNamePrefix, StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(buttonName.Substring(indexedNamePrefix.Length), out packNumber))
            return packNumber >= 1 && packNumber <= 6;

        packNumber = 0;
        if (!buttonName.StartsWith("BUY ", StringComparison.OrdinalIgnoreCase)) return false;

        var digits = new List<char>();
        foreach (char character in buttonName)
            if (char.IsDigit(character)) digits.Add(character);
        if (digits.Count == 0 || !int.TryParse(new string(digits.ToArray()), out int oldDisplayAmount))
            return false;

        // The existing authored cards used these amounts as their stable button names.
        switch (oldDisplayAmount)
        {
            case 100: packNumber = 1; return true;
            case 500: packNumber = 2; return true;
            case 1200: packNumber = 3; return true;
            case 3000: packNumber = 4; return true;
            case 7500: packNumber = 5; return true;
            case 15000: packNumber = 6; return true;
            default: return false;
        }
    }

    private static string FormatShopGoldAmount(int amount)
    {
        return amount.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));
    }

    private void BuildGoldPackArt(Transform card, int index)
    {
        if (shopGoldSprites != null && index >= 0 && index < shopGoldSprites.Length && shopGoldSprites[index] != null)
        {
            var sprite = shopGoldSprites[index];
            AddShopImage(card, "Gold Bundle Art", sprite, new Vector2(0.5f, 0.735f), GetShopArtSize(card, sprite));
        }
    }

    private void BuildHeartPackArt(Transform card, int amount)
    {
        int index = amount == 5 ? 0 : 1;
        if (shopHeartSprites != null && index < shopHeartSprites.Length && shopHeartSprites[index] != null)
        {
            var sprite = shopHeartSprites[index];
            AddShopImage(card, "Heart Bundle Art", sprite, new Vector2(0.5f, 0.735f), GetShopArtSize(card, sprite));
        }
    }

    private Vector2 GetShopArtSize(Transform card, Sprite sprite)
    {
        var cardRect = card as RectTransform;
        float cardAspect = cardRect != null ? cardRect.rect.width / Mathf.Max(1f, cardRect.rect.height) : 1.55f;
        float spriteAspect = sprite != null ? sprite.rect.width / Mathf.Max(1f, sprite.rect.height) : 1.5f;
        float artHeight = Mathf.Min(0.58f, 0.68f * cardAspect / Mathf.Max(0.01f, spriteAspect));
        return new Vector2(0.92f, artHeight);
    }

    private Image AddShopImage(Transform parent, string name, Sprite sprite, Vector2 center, Vector2 size)
    {
        if (sprite == null) return null;
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = center;
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.useSpriteMesh = false;
        image.raycastTarget = false;
        image.SetNativeSize();
        var parentRect = parent as RectTransform;
        if (parentRect != null)
            FitNativeSprite(rect, Vector2.Scale(parentRect.rect.size, size));
        return image;
    }

    private static void FitNativeSprite(RectTransform rect, Vector2 targetSize)
    {
        if (rect == null || rect.sizeDelta.x <= 0f || rect.sizeDelta.y <= 0f) return;
        float scale = Mathf.Min(targetSize.x / rect.sizeDelta.x, targetSize.y / rect.sizeDelta.y);
        rect.localScale = Vector3.one * Mathf.Max(0f, scale);
    }

    private void BuildBoosterArt(Transform card, int index)
    {
        if (shopBoosterSprites != null && index >= 0 && index < shopBoosterSprites.Length && shopBoosterSprites[index] != null)
            AddShopImage(card, "Booster Art", shopBoosterSprites[index], new Vector2(0.5f, 0.72f), new Vector2(0.84f, 0.40f));
    }

    private void SetShopCategory(bool showBooster)
    {
        shopBoosterSelected = showBooster;
        if (shopGoldViewport != null) shopGoldViewport.SetActive(!showBooster);
        if (shopBoosterViewport != null) shopBoosterViewport.SetActive(showBooster);
        SetShopCategoryVisual(shopGoldTabButton, shopGoldTabImage, !showBooster);
        SetShopCategoryVisual(shopBoosterTabButton, shopBoosterTabImage, showBooster);
    }

    private void SetShopCategoryVisual(Button button, Image image, bool selected)
    {
        if (button == null) return;
        string statePrefix = button.name == "SHOP_GOLD_TAB" ? "Gold" : "Booster";
        Transform chosen = button.transform.Find(statePrefix + "Chosen");
        Transform notChosen = button.transform.Find(statePrefix + "NotChoose");
        if (chosen != null && notChosen != null)
        {
            chosen.gameObject.SetActive(selected);
            notChosen.gameObject.SetActive(!selected);
            return;
        }

        // Older generated tabs have one image instead of the authored pair of state objects.
        if (image == null) return;
        image.sprite = selected ? shopTabSelectedSprite : shopTabIdleSprite;
        image.color = Color.white;

        var labelTransform = button.transform.Find("Label");
        var label = labelTransform != null ? labelTransform.GetComponent<Text>() : null;
        if (label == null) return;
        label.color = selected ? Color.white : new Color32(22, 10, 138, 255);
        var textOutline = label.GetComponent<Outline>();
        if (textOutline == null) textOutline = label.gameObject.AddComponent<Outline>();
        textOutline.effectColor = selected
            ? new Color32(87, 24, 155, 225)
            : new Color32(220, 206, 255, 255);
        textOutline.effectDistance = selected ? new Vector2(1f, -2f) : new Vector2(2f, -2f);
    }

    private GameObject BuildTabPage(Transform parent, string name, string title, string subtitle, Color titleColor)
    {
        bool landscape = false;
        var page = Panel(parent, name, new Color32(21, 16, 69, 250), landscape ? new Vector2(0f, 0.19f) : new Vector2(0f, 0.155f), new Vector2(1f, 0.86f));
        Label(page.transform, title, 48, new Vector2(0.5f, 0.87f), new Vector2(0.9f, 0.1f), titleColor, FontStyle.Bold);
        Label(page.transform, subtitle, 27, new Vector2(0.5f, 0.78f), new Vector2(0.86f, 0.07f), Color.white, FontStyle.Normal);
        return page;
    }

    private void AddPageCard(Transform parent, string title, string details, float y, Color color, float x = 0.5f, float width = 0.82f, float height = 0.13f)
    {
        var card = Panel(parent, title, color, new Vector2(x - width * 0.5f, y - height * 0.5f), new Vector2(x + width * 0.5f, y + height * 0.5f));
        var label = Label(card.transform, title + "\n" + details, 29, new Vector2(0.5f, 0.5f), new Vector2(0.92f, 0.9f), Color.white, FontStyle.Bold);
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 20;
    }

    private void SelectTab(int index)
    {
        if (tabPages == null || index < 0 || index >= tabPages.Length) return;
        activeTabIndex = index;
        for (int i = 0; i < tabPages.Length; i++)
            if (tabPages[i] != null) tabPages[i].SetActive(i == index);
        if (index == 3) RefreshAchievements();
        if (index == 4 || index == 0)
        {
            previewCharacterIndex = equippedCharacterIndex;
            UpdateCharacterPageState();
        }
        if (homeCharacterImage != null)
            homeCharacterImage.gameObject.SetActive(index != 4);
        if (floatingCharacterShadow != null)
            floatingCharacterShadow.gameObject.SetActive(index != 4);
        if (index == 0 && homeCharacterImage != null)
            homeCharacterImage.sprite = GetCharacterSprite(equippedCharacterIndex);
        if (footerBackgroundObject != null) footerBackgroundObject.SetActive(true);
        if (selectedTabBlock != null) selectedTabBlock.gameObject.SetActive(true);
        targetTabX = GetTabIconCenterInNavigation(index);
        UpdateBottomTabTextSizing();
        UpdateSelectedFooterContent();
        CloseOutOfHearts();
    }

    private void UpdateSelectedFooterContent()
    {
        if (Application.isPlaying) UpdateNavigationIconSelection(0f);
        if (tabIconRects != null)
        {
            for (int i = 0; i < tabIconRects.Length; i++)
            {
                var icon = tabIconRects[i] != null ? tabIconRects[i].GetComponent<Image>() : null;
                if (icon != null) icon.enabled = true;
            }
        }

        if (bottomTabLabels != null)
        {
            for (int i = 0; i < bottomTabLabels.Length; i++)
            {
                if (bottomTabLabels[i] == null) continue;
                bottomTabLabels[i].enabled = true;
                bottomTabLabels[i].raycastTarget = false;
            }
        }

        if (missionBadge != null)
        {
            foreach (var graphic in missionBadge.GetComponentsInChildren<Graphic>(true))
                graphic.enabled = true;
        }
    }
    private static void ConfigureFooterTabHitbox(Button tab)
    {
        if (tab == null) return;

        var hitboxImage = tab.GetComponent<Image>();
        if (hitboxImage != null)
        {
            hitboxImage.sprite = null;
            hitboxImage.color = Color.clear;
            hitboxImage.raycastTarget = true;
        }

        tab.transition = Selectable.Transition.None;
        tab.targetGraphic = null;

        foreach (var outline in tab.GetComponents<Outline>()) outline.enabled = false;
        foreach (var shadow in tab.GetComponents<Shadow>()) shadow.enabled = false;
        foreach (var label in tab.GetComponentsInChildren<Text>(true)) label.raycastTarget = false;
        foreach (var image in tab.GetComponentsInChildren<Image>(true))
            if (image != hitboxImage) image.raycastTarget = false;
    }

    private RectTransform AddTabIcon(Transform button, string symbol)
    {
        var label = button.Find("Label").GetComponent<Text>();
        var labelRect = label.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 0.01f);
        labelRect.anchorMax = new Vector2(1f, 0.37f);
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        label.fontSize = 30;
        label.resizeTextForBestFit = false;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;

        string iconName = symbol == "S" ? "Shop"
            : symbol == "M" ? "Missions"
            : symbol == "H" ? "Home"
            : symbol == "★" ? "Achievements"
            : symbol == "A" ? "Achievements"
            : "Character";
        var icon = new GameObject(iconName + " Tab Icon", typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(button, false);
        var iconRect = icon.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.25f, 0.43f);
        iconRect.anchorMax = new Vector2(0.75f, 0.84f);
        iconRect.offsetMin = iconRect.offsetMax = Vector2.zero;
        if (iconName == "Home") iconRect.localScale = Vector3.one;
        var image = icon.GetComponent<Image>();
        image.sprite = Resources.Load<Sprite>("UI/TabIcons/BottomBar" + iconName);
        image.preserveAspect = true;
        image.raycastTarget = false;
        if (iconName == "Home")
        {
            var iconShadow = icon.AddComponent<Shadow>();
            iconShadow.effectColor = new Color32(19, 12, 112, 225);
            iconShadow.effectDistance = new Vector2(1.5f, -3f);
        }
        if (image.sprite == null)
            Label(button, symbol, 58, new Vector2(0.5f, 0.70f), new Vector2(0.74f, 0.54f), new Color32(255, 220, 114, 255), FontStyle.Bold);
        return iconRect;
    }

    private GameObject AddRewardBadge(Transform button, string badgeName)
    {
        var badge = Panel(button, badgeName, new Color32(237, 47, 38, 255), new Vector2(0.77f, 0.77f), new Vector2(0.98f, 1.02f));
        var image = badge.GetComponent<Image>();
        image.raycastTarget = false;
        var shadow = badge.AddComponent<Shadow>();
        shadow.effectColor = new Color32(79, 10, 14, 200);
        shadow.effectDistance = new Vector2(0f, -3f);
        var count = Label(badge.transform, string.Empty, 22, new Vector2(0.5f, 0.5f), new Vector2(0.9f, 0.9f), Color.white, FontStyle.Bold);
        count.raycastTarget = false;
        badge.SetActive(false);
        return badge;
    }

    private void RefreshFooterRewardBadges()
    {
        SetRewardBadgeCount(missionBadge, DailyMissionManager.ClaimableCount);
        SetRewardBadgeCount(achievementBadge, AchievementManager.ClaimableCount);
    }

    private static void SetRewardBadgeCount(GameObject badge, int count)
    {
        if (badge == null) return;
        count = Mathf.Max(0, count);
        var label = badge.GetComponentInChildren<Text>(true);
        if (label != null) label.text = count > 0 ? count.ToString(CultureInfo.InvariantCulture) : string.Empty;
        if (count > 0)
            foreach (var graphic in badge.GetComponentsInChildren<Graphic>(true)) graphic.enabled = true;
        badge.SetActive(count > 0);
    }

    private void AddButtonIcon(Button button, Sprite sprite, Vector2 center, Vector2 size, bool behindText = false, string objectName = "Button Icon")
    {
        if (sprite == null) return;
        var icon = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(button.transform, false);
        var rect = icon.GetComponent<RectTransform>();
        rect.anchorMin = center - size * 0.5f;
        rect.anchorMax = center + size * 0.5f;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var image = icon.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        if (behindText) icon.transform.SetAsFirstSibling();
    }

    private void AddDecorativeIcon(Transform parent, string name, Sprite sprite, Vector2 center, Vector2 size)
    {
        if (sprite == null) return;
        var icon = new GameObject(name, typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(parent, false);
        var rect = icon.GetComponent<RectTransform>();
        rect.anchorMin = center - size * 0.5f;
        rect.anchorMax = center + size * 0.5f;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var image = icon.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
    }

    private static void AddWhiteIconOutline(GameObject icon, float thickness)
    {
        if (icon == null) return;
        var image = icon.GetComponent<Image>();
        if (image == null) return;
        var shader = Shader.Find("UI/WhiteSilhouette");
        if (shader == null)
        {
            var fallback = icon.AddComponent<Outline>();
            fallback.effectColor = Color.white;
            fallback.effectDistance = new Vector2(thickness, -thickness);
            fallback.useGraphicAlpha = false;
            return;
        }

        if (whiteSilhouetteMaterial == null)
            whiteSilhouetteMaterial = new Material(shader) { name = "Home Icon White Silhouette" };

        var sourceRect = icon.GetComponent<RectTransform>();
        var border = new GameObject(icon.name + " White Border", typeof(RectTransform), typeof(Image));
        border.transform.SetParent(icon.transform.parent, false);
        var borderRect = border.GetComponent<RectTransform>();
        borderRect.anchorMin = sourceRect.anchorMin;
        borderRect.anchorMax = sourceRect.anchorMax;
        borderRect.pivot = sourceRect.pivot;
        borderRect.localScale = sourceRect.localScale;
        borderRect.offsetMin = sourceRect.offsetMin - Vector2.one * thickness;
        borderRect.offsetMax = sourceRect.offsetMax + Vector2.one * thickness;

        var borderImage = border.GetComponent<Image>();
        borderImage.sprite = image.sprite;
        borderImage.preserveAspect = image.preserveAspect;
        borderImage.material = whiteSilhouetteMaterial;
        borderImage.raycastTarget = false;
        border.transform.SetSiblingIndex(icon.transform.GetSiblingIndex());
    }

    private void SetButtonImage(Button button, Sprite sprite)
    {
        if (sprite == null) return;
        var image = button.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.color = Color.white;
        var label = button.transform.Find("Label");
        if (label != null) label.GetComponent<Text>().text = string.Empty;
    }

    private void AddPlusBadge(Button button)
    {
        const float badgeHeight = 0.72f;
        const float rightInset = 0.025f;
        var buttonRect = button.GetComponent<RectTransform>();
        float buttonWidth = buttonRect.rect.width;
        float buttonHeight = buttonRect.rect.height;
        if (buttonWidth <= 0f) buttonWidth = (buttonRect.anchorMax.x - buttonRect.anchorMin.x) * Screen.width;
        if (buttonHeight <= 0f) buttonHeight = (buttonRect.anchorMax.y - buttonRect.anchorMin.y) * Screen.height;

        float badgeWidth = badgeHeight * buttonHeight / Mathf.Max(1f, buttonWidth);
        float badgeRight = 1f - rightInset;
        float badgeLeft = badgeRight - badgeWidth;
        float badgeBottom = (1f - badgeHeight) * 0.5f;
        var plus = new GameObject("Add Currency", typeof(RectTransform), typeof(Image));
        plus.transform.SetParent(button.transform, false);
        var plusRect = plus.GetComponent<RectTransform>();
        plusRect.anchorMin = new Vector2(badgeLeft, badgeBottom);
        plusRect.anchorMax = new Vector2(badgeRight, 1f - badgeBottom);
        plusRect.offsetMin = plusRect.offsetMax = Vector2.zero;
        var plusImage = plus.GetComponent<Image>();
        plusImage.sprite = plusBadgeSprite;
        plusImage.preserveAspect = true;
        plusImage.useSpriteMesh = true;
        plusImage.raycastTarget = false;

        var valueLabel = button.transform.Find("Label");
        if (valueLabel != null)
        {
            var valueRect = valueLabel.GetComponent<RectTransform>();
            valueRect.anchorMax = new Vector2(Mathf.Min(valueRect.anchorMax.x, badgeLeft - 0.02f), valueRect.anchorMax.y);
        }
    }

    private static void StyleHudCounter(Button button, int textSize)
    {
        var label = button.transform.Find("Label").GetComponent<Text>();
        label.fontSize = textSize;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = Mathf.Max(12, textSize - 4);
        label.resizeTextMaxSize = textSize;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;

        var outline = button.GetComponent<Outline>();
        if (outline != null) outline.effectDistance = new Vector2(1f, -1f);
        var shadow = button.GetComponent<Shadow>();
        if (shadow != null) shadow.effectDistance = new Vector2(0f, -3f);
    }

    private Sprite LoadIconSprite(string resourcePath)
    {
        var sprites = Resources.LoadAll<Sprite>(resourcePath);
        if (sprites != null && sprites.Length > 0) return sprites[0];
        var texture = Resources.Load<Texture2D>(resourcePath);
        if (texture == null)
        {
            Debug.LogWarning("Missing UI icon texture: " + resourcePath);
            return null;
        }
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
    }

    private void ConfigurePlayButton(Transform button)
    {
        if (button == null) return;

        var image = button.GetComponent<Image>();
        var blankTextSprite = Resources.Load<Sprite>("UI/Buttons/PlayButtonExact");
        if (image != null && blankTextSprite != null)
        {
            image.sprite = blankTextSprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = Color.white;
        }

        // The authored button may still have its old, empty Button helper label.
        var legacyLabel = button.Find("Label")?.GetComponent<Text>();
        if (legacyLabel != null)
        {
            legacyLabel.text = string.Empty;
            legacyLabel.raycastTarget = false;
        }

        var multiplierBadge = button.Find("Play Multiplier Badge");
        bool createdMultiplierBadge = false;
        if (multiplierBadge == null)
        {
            if (!Application.isPlaying)
            {
                var badgeObject = new GameObject("Play Multiplier Badge", typeof(RectTransform), typeof(Image));
                badgeObject.transform.SetParent(button, false);
                multiplierBadge = badgeObject.transform;
                createdMultiplierBadge = true;
            }
            else
            {
                Debug.LogError("Missing authored Play Multiplier Badge. Run Tools/Diagnostics/Check Authored Home UI once outside Play Mode.");
            }
        }

        var buttonRect = button as RectTransform;
        var badgeRect = multiplierBadge != null ? multiplierBadge.GetComponent<RectTransform>() : null;
        var badgeImage = multiplierBadge != null ? multiplierBadge.GetComponent<Image>() : null;
        if (createdMultiplierBadge && badgeRect != null && buttonRect != null)
        {
            // Set a default only for a newly authored object; preserve inspector sizing on existing objects.
            Vector2 attachment = new Vector2(0.85f, 0.95f);
            badgeRect.anchorMin = attachment;
            badgeRect.anchorMax = attachment;
            badgeRect.pivot = new Vector2(0.29f, 0.96f);
            badgeRect.sizeDelta = new Vector2(buttonRect.rect.width * 0.27f, buttonRect.rect.height * 0.62f);
            badgeRect.anchoredPosition = Vector2.zero;
            badgeRect.localRotation = Quaternion.identity;
            badgeRect.localScale = Vector3.one;
        }
        if (badgeRect != null)
        {
            playMultiplierBadgeBaseRotation = badgeRect.localRotation;
        }
        if (badgeImage != null)
        {
            badgeImage.sprite = Resources.Load<Sprite>("UI/Buttons/PlayButtonMultiplierTag");
            badgeImage.type = Image.Type.Simple;
            badgeImage.preserveAspect = false;
            badgeImage.color = Color.white;
            badgeImage.raycastTarget = false;
        }

        // Reuse the old label if this button was already configured, then keep it attached to the swinging tag.
        var oldMultiplierLabel = button.Find("Play Multiplier Label");
        if (!Application.isPlaying && oldMultiplierLabel != null && multiplierBadge != null && oldMultiplierLabel.parent != multiplierBadge)
            oldMultiplierLabel.SetParent(multiplierBadge, false);
        playMultiplierBadgeRect = badgeRect;

        playLevelLabel = EnsurePlayButtonLabel(button, "Play Level Label", 54,
            new Vector2(0.49f, 0.53f), new Vector2(0.63f, 0.43f));
        playMultiplierLabel = multiplierBadge != null
            ? EnsurePlayButtonLabel(multiplierBadge, "Play Multiplier Label", 30,
                new Vector2(0.52f, 0.40f), new Vector2(0.48f, 0.20f))
            : null;
        if (playMultiplierLabel != null)
        {
            playMultiplierLabel.text = "x10";
        }
        playDifficultyLabel = EnsurePlayButtonLabel(button, "Play Difficulty Label", 26,
            new Vector2(0.50f, 0.13f), new Vector2(0.50f, 0.18f));
        RefreshPlayButtonLevel();
    }

    private Text EnsurePlayButtonLabel(Transform parent, string objectName, int fontSize, Vector2 center, Vector2 size)
    {
        var existing = parent.Find(objectName);
        GameObject labelObject;
        Text label;
        if (existing != null && existing.TryGetComponent(out label))
        {
            labelObject = existing.gameObject;
        }
        else
        {
            if (Application.isPlaying)
            {
                Debug.LogError("Missing authored text object '" + objectName + "'. Add it in Edit Mode before Play.");
                return null;
            }
            labelObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(parent, false);
            label = labelObject.GetComponent<Text>();

            var rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = center - size * 0.5f;
            rect.anchorMax = center + size * 0.5f;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            label.font = roundedFont != null ? roundedFont : font;
            label.fontSize = fontSize;
            label.fontStyle = FontStyle.Bold;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = Mathf.Max(12, fontSize / 2);
            label.resizeTextMaxSize = fontSize;

            var outline = labelObject.AddComponent<Outline>();
            outline.effectColor = new Color32(69, 22, 119, 190);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            var shadow = labelObject.AddComponent<Shadow>();
            shadow.effectColor = new Color32(49, 12, 93, 190);
            shadow.effectDistance = new Vector2(1f, -3f);
        }
        label.raycastTarget = false;
        return label;
    }
         
    private bool TryGetPlayButtonImageBounds(out Rect imageBounds)
    {
        imageBounds = default;
        if (playButtonRect == null) return false;

        var background = playButtonRect.GetComponent<Image>();
        Rect bounds = playButtonRect.rect;
        if (bounds.width <= 0f || bounds.height <= 0f) return false;
        if (background == null || !background.preserveAspect || background.sprite == null)
        {
            imageBounds = bounds;
            return true;
        }

        float spriteAspect = background.sprite.rect.width / background.sprite.rect.height;
        if (spriteAspect <= 0f) return false;

        Vector2 displayedSize;
        if (bounds.width / bounds.height > spriteAspect)
            displayedSize = new Vector2(bounds.height * spriteAspect, bounds.height);
        else
            displayedSize = new Vector2(bounds.width, bounds.width / spriteAspect);

        imageBounds = new Rect(bounds.center - displayedSize * 0.5f, displayedSize);
        return true;
    }

    private void RefreshPlayButtonLevel()
    {
        int level = GetCurrentPlayerLevel();
        RefreshSpecialLevelCardLocks(level);
        if (playLevelLabel == null) return;
        int difficultyLevel = Mathf.Clamp(level, 0, 100);
        int difficultyIndex = Mathf.Min(PlayDifficultyLabels.Length - 1,
            Mathf.FloorToInt(difficultyLevel * (float)PlayDifficultyLabels.Length / 100f));
        string levelText = Localize.Format("CẤP ĐỘ {0}", "LEVEL {0}", level);
        string difficultyText = Localize.Translate(PlayDifficultyLabels[difficultyIndex]);
        if (displayedPlayLevel == level && playLevelLabel.text == levelText &&
            (playDifficultyLabel == null || playDifficultyLabel.text == difficultyText)) return;
        displayedPlayLevel = level;
        playLevelLabel.text = levelText;
        if (playDifficultyLabel != null)
            playDifficultyLabel.text = difficultyText;
    }

    private void AddMultiplierBadge(Transform parent, bool landscape)
    {
        Vector2 center = landscape ? new Vector2(0.66f, 0.39f) : new Vector2(0.82f, 0.235f);
        Vector2 size = landscape ? new Vector2(0.10f, 0.11f) : new Vector2(0.12f, 0.06f);
        var badge = Panel(parent, "Play Multiplier Badge", new Color32(255, 160, 16, 255), center - size * 0.5f, center + size * 0.5f);
        badge.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0f, 0f, -9f);
        var shadow = badge.AddComponent<Shadow>();
        shadow.effectColor = new Color32(65, 21, 131, 220);
        shadow.effectDistance = new Vector2(0f, -5f);
        badge.GetComponent<Image>().raycastTarget = false;
        var loop = Panel(badge.transform, "Badge Loop", new Color32(255, 215, 77, 255), new Vector2(0.39f, 0.86f), new Vector2(0.61f, 1.02f));
        loop.GetComponent<Image>().raycastTarget = false;
        var tag = Label(badge.transform, "x10", landscape ? 48 : 42, new Vector2(0.5f, 0.5f), new Vector2(0.9f, 0.9f), Color.white, FontStyle.Bold);
        tag.raycastTarget = false;
        tag.resizeTextForBestFit = true;
        tag.resizeTextMinSize = 18;
        tag.resizeTextMaxSize = landscape ? 48 : 42;
    }

    private void AddSymbolIcon(Button button, string symbol, Color color, Vector2 center, Vector2 size)
    {
        var icon = new GameObject("Button Symbol", typeof(RectTransform), typeof(Text));
        icon.transform.SetParent(button.transform, false);
        var rect = icon.GetComponent<RectTransform>();
        rect.anchorMin = center - size * 0.5f;
        rect.anchorMax = center + size * 0.5f;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var text = icon.GetComponent<Text>();
        text.font = font;
        text.text = symbol;
        text.fontSize = 62;
        text.fontStyle = FontStyle.Bold;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        var shadow = icon.AddComponent<Shadow>();
        shadow.effectColor = new Color32(119, 18, 44, 180);
        shadow.effectDistance = new Vector2(1f, -2f);
    }

    private void SetButtonLabelArea(Button button, Vector2 min, Vector2 max)
    {
        var label = button.transform.Find("Label");
        if (label == null) return;
        var rect = label.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private void AddLockBadge(Button button)
    {
        SetButtonLabelArea(button, new Vector2(0.36f, 0.04f), new Vector2(0.98f, 0.48f));
        AddButtonIcon(button, lockSprite, new Vector2(0.18f, 0.26f), new Vector2(0.34f, 0.44f));
    }

    private Button AddLockedLevelCard(Transform parent, string name, int level, Vector2 center, Vector2 size)
    {
        // The card art is copied from the supplied reference screenshot, including
        // its robot, lock, frame, and level label. Keep it as a single sprite.
        var canvas = parent.GetComponentInParent<Canvas>();
        var scaler = canvas != null ? canvas.GetComponent<CanvasScaler>() : null;
        Vector2 reference = scaler != null ? scaler.referenceResolution : new Vector2(1080f, 1920f);
        size.x = size.y * reference.y / reference.x;
        AddLockedLevelCardGlow(parent, name, level, center, size);
        var card = Button(parent, name, string.Empty, center, size, Color.white, 32, () => OpenSpecialLevelPopup(level));
        var cardImage = card.GetComponent<Image>();
        cardImage.sprite = level == 25 ? level25CardSprite : level == 30 ? level30CardSprite : level50CardSprite;
        cardImage.type = Image.Type.Simple;
        cardImage.preserveAspect = true;
        cardImage.useSpriteMesh = true;
        cardImage.color = Color.white;
        cardImage.raycastTarget = true;

        var outline = card.GetComponent<Outline>();
        if (outline != null) DestroyUIObject(outline);
        var shadow = card.GetComponent<Shadow>();
        if (shadow != null) DestroyUIObject(shadow);
        var glowOutline = card.gameObject.AddComponent<Outline>();
        glowOutline.effectColor = new Color32(255, 246, 202, 220);
        glowOutline.effectDistance = new Vector2(6f, -6f);
        glowOutline.useGraphicAlpha = true;
        var label = card.transform.Find("Label");
        if (label != null) DestroyUIObject(label.gameObject);
        return card;
    }

    private void AddLockedLevelCardGlow(Transform parent, string cardName, int level, Vector2 center, Vector2 size,
        bool maskable = false, bool raysFromCenter = false)
    {
        var diffuseGlow = new Color32(255, 248, 224, 255);
        AddLockedLevelCardGlowLayer(parent, cardName + " Glow", diffuseGlow, center, size,
            new Vector2(1.52f, 1.44f), 0.025f, maskable);
        var warmCore = new Color32(255, 224, 144, 235);
        AddLockedLevelCardGlowLayer(parent, cardName + " Glow Core", warmCore, center, size,
            new Vector2(1.23f, 1.18f), 0.01f, maskable);
        AddLockedLevelCardRotatingLight(parent, cardName, center, size, level * 0.73f, maskable,
            raysFromCenter);
    }

    private void AddLockedLevelCardGlowLayer(Transform parent, string objectName, Color color, Vector2 center,
        Vector2 size, Vector2 sizeMultiplier, float downwardOffset, bool maskable = false)
    {
        var glowObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        glowObject.transform.SetParent(parent, false);
        var rect = glowObject.GetComponent<RectTransform>();
        var glowSize = Vector2.Scale(size, sizeMultiplier);
        var glowCenter = center + Vector2.down * (size.y * downwardOffset);
        rect.anchorMin = glowCenter - glowSize * 0.5f;
        rect.anchorMax = glowCenter + glowSize * 0.5f;
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        var image = glowObject.GetComponent<Image>();
        image.sprite = cardGlowSprite;
        image.color = color;
        image.raycastTarget = false;
        image.maskable = maskable;
    }

    private void AddLockedLevelCardRotatingLight(Transform parent, string cardName, Vector2 center,
        Vector2 cardSize, float phase, bool maskable = false, bool raysFromCenter = false)
    {
        float[] rayWidths = { 0.15f, 0.14f, 0.13f, 0.12f, 0.11f, 0.10f, 0.09f, 0.08f };
        float[] rayHeights = { 0.42f, 0.39f, 0.36f, 0.33f, 0.30f, 0.27f, 0.24f, 0.21f };
        Color32[] rayColors =
        {
            new Color32(255, 255, 246, 235),
            new Color32(255, 237, 175, 220),
            new Color32(255, 250, 224, 225),
            new Color32(255, 244, 205, 205),
            new Color32(255, 255, 246, 215),
            new Color32(255, 237, 175, 205),
            new Color32(255, 250, 224, 215),
            new Color32(255, 244, 205, 195)
        };

        if (raysFromCenter)
        {
            // Pin each ray's base to the art center. Longer rays fan outward and
            // remain clipped by the achievement card's RectMask2D.
            for (int i = 0; i < rayWidths.Length; i++)
            {
                float rayPhase = phase + i * (Mathf.PI * 2f / rayWidths.Length);
                float rayLength = Mathf.Lerp(1.08f, 0.82f, i / (float)(rayWidths.Length - 1));
                AddLockedLevelCardLightRay(parent, cardName + " Center Sun Ray " + (i + 1), center,
                    cardSize, rayWidths[i], rayLength, rayColors[i], rayPhase, maskable, true);
            }

            return;
        }

        // Wide, pale rays sit behind the sharp rays at the same orbit positions.
        var broadColor = new Color32(255, 232, 158, 102);
        for (int i = 0; i < rayWidths.Length; i++)
        {
            float rayPhase = phase + i * (Mathf.PI * 2f / rayWidths.Length);
            AddLockedLevelCardLightRay(parent, cardName + " Soft Sun Ray " + (i + 1), center,
                cardSize, rayWidths[i] * 4f, rayHeights[i] * 0.52f, broadColor, rayPhase, maskable);
        }

        for (int i = 0; i < rayWidths.Length; i++)
        {
            float rayPhase = phase + i * (Mathf.PI * 2f / rayWidths.Length);
            AddLockedLevelCardLightRay(parent, cardName + " Sharp Ray " + (i + 1), center,
                cardSize, rayWidths[i], rayHeights[i], rayColors[i], rayPhase, maskable);
        }
    }

    private void AddLockedLevelCardLightRay(Transform parent, string objectName, Vector2 center,
        Vector2 cardSize, float rayWidth, float rayHeight, Color color, float phase, bool maskable = false,
        bool baseAtCenter = false)
    {
        var lightObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        lightObject.transform.SetParent(parent, false);
        var rect = lightObject.GetComponent<RectTransform>();
        var lightSize = Vector2.Scale(cardSize, new Vector2(rayWidth, rayHeight));
        var parentRect = parent as RectTransform;
        if (baseAtCenter && parentRect != null)
        {
            rect.anchorMin = rect.anchorMax = center;
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(lightSize.x * parentRect.rect.width,
                lightSize.y * parentRect.rect.height);
            rect.anchoredPosition = Vector2.zero;
        }
        else
        {
            rect.anchorMin = center - lightSize * 0.5f;
            rect.anchorMax = center + lightSize * 0.5f;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        var image = lightObject.GetComponent<Image>();
        image.sprite = cardTriangleRaySprite;
        image.color = color;
        image.raycastTarget = false;
        image.maskable = maskable;
        rect.localRotation = Quaternion.Euler(0f, 0f, phase * Mathf.Rad2Deg - 90f);
        cardRotatingLights.Add(new CardRotatingLightState
        {
            rect = rect,
            parentRect = parentRect,
            basePosition = rect.anchoredPosition,
            orbitRadius = baseAtCenter ? Vector2.zero : new Vector2(cardSize.x * 0.50f, cardSize.y * 0.48f),
            phase = phase
        });
    }

    private void UpdateCardGlows()
    {
        for (int i = cardRotatingLights.Count - 1; i >= 0; i--)
        {
            var state = cardRotatingLights[i];
            if (state.rect == null || state.parentRect == null)
            {
                cardRotatingLights.RemoveAt(i);
                continue;
            }

            float angle = Time.unscaledTime * 0.82f + state.phase;
            state.rect.anchoredPosition = state.basePosition + new Vector2(
                Mathf.Cos(angle) * state.orbitRadius.x * state.parentRect.rect.width,
                Mathf.Sin(angle) * state.orbitRadius.y * state.parentRect.rect.height);
            // The sprite's tip points upward; rotate it to follow the outward radial direction.
            state.rect.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg - 90f);
        }
    }

    private void AddCubePolish(GameObject cube)
    {
        var image = cube.GetComponent<Image>();
        image.raycastTarget = false;
        var shadow = cube.AddComponent<Shadow>();
        shadow.effectColor = new Color32(54, 24, 107, 210);
        shadow.effectDistance = new Vector2(0f, -5f);
        var gloss = Panel(cube.transform, "Cube Gloss", new Color(1f, 1f, 1f, 0.40f), new Vector2(0.13f, 0.70f), new Vector2(0.87f, 0.88f));
        gloss.GetComponent<Image>().raycastTarget = false;
    }

    private static void ApplyAudioSetting(bool music, bool enabled)
    {
        float volume = enabled ? 1f : 0f;
        if (AudioManager.Instance != null)
        {
            if (music) AudioManager.Instance.SetMusicVolume(volume);
            else AudioManager.Instance.SetSFXVolume(volume);
        }

        if (GameMaster.staticScript != null)
        {
            if (music) GameMaster.MusicOnOff = enabled ? 1 : 0;
            else GameMaster.SoundOnOff = enabled ? 1 : 0;
        }
    }

    private void ShowShop()
    {
        SelectTab(1);
        SetShopCategory(false);
    }

    private void ShowShopItemInfo(string itemName) => ShowShop();

    private void ShowEnergy()
    {
        if (!HeartManager.CanPlay) ShowOutOfHearts();
        else OpenHeartShop();
    }

    private void ShowCharacter()
    {
        SelectTab(4);
    }

    private void ShowAchievements()
    {
        SelectTab(3);
    }

    private void ClaimDailyLoginDay(int day)
    {
        GetDailyLoginState(out bool claimed, out int streak);
        if (claimed || day != (Mathf.Max(1, streak) - 1) % 7 + 1) return;
        ClaimDaily();
    }

    private void ClaimDaily()
    {
        GetDailyLoginState(out bool alreadyClaimed, out int streak);
        if (alreadyClaimed) return;
        int reward = 100 + ((Mathf.Max(1, streak) - 1) % 7) * 50;
#if UNITY_EDITOR
        editorDailyClaimedToday = true;
        editorDailyStreak = Mathf.Max(1, streak);
#else
        PlayerPrefs.SetString(DailyClaimKey, DateTime.Now.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture));
        PlayerPrefs.SetInt(DailyStreakKey, Mathf.Max(1, streak));
        PlayerPrefs.Save();
#endif
        AddCoins(reward);
        RefreshCoins();
        RefreshDailyState();
    }

    private RectTransform dailyMissionRewardSource;

    public void ClaimMissionReward(int missionValue)
    {
        if (!Enum.IsDefined(typeof(DailyMissionType), missionValue)) return;
        var mission = (DailyMissionType)missionValue;
        if (!DailyMissionManager.CanClaim(mission)) return;
        PlayButtonSfx();
        ClaimDailyMission(mission);
    }

    private void ClaimDailyMission(DailyMissionType mission)
    {
        RectTransform previousSource = dailyMissionRewardSource;
        if (tabPages != null && tabPages.Length > 2 && tabPages[2] != null)
            foreach (var card in tabPages[2].GetComponentsInChildren<DailyMissionCardUI>(true))
                if (TrySceneMission(card.name, out var type) && type == mission)
                {
                    dailyMissionRewardSource = SceneSuffixComponent<RectTransform>(card.transform, " Reward Coin Icon");
                    break;
                }
        try
        {
            if (!DailyMissionManager.TryClaim(mission)) return;
            RefreshCoins();
            RefreshDailyState();
        }
        finally { dailyMissionRewardSource = previousSource; }
    }

    private void ShowEvent()
    {
        // The banner has no authored event panel yet.
    }

    private void ShowSettings()
    {
        if (settingsPopupRoot == null)
            settingsPopupRoot = SceneObject("Settings Popup");
        if (settingsPopupRoot == null)
        {
            Debug.LogError("The authored Settings Popup is missing. Run Tools > Home UI > Bake into Home Scene.", this);
            return;
        }

        RefreshSettingsLanguageLabels();
        UpdateSettingsPopupLayout(true);
        settingsPopupRoot.SetActive(true);
        Canvas.ForceUpdateCanvases();      // buộc tính rect sau khi popup hiện
        RefreshSettingToggleVisuals();     // áp lại kích thước knob
    }

    private void RefreshDailyState()
    {
        GetDailyLoginState(out bool claimed, out int streak);
        RefreshDailyLoginTiles(claimed, streak);
        if (dailyButtonLabel != null)
            dailyButtonLabel.text = Localize.Translate(claimed ? "NHIỆM\nVỤ ✓" : "NHIỆM\nVỤ");
        RefreshFooterRewardBadges();
    }

    private void GetDailyLoginState(out bool claimedToday, out int streak)
    {
        string today = DateTime.Now.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
#if UNITY_EDITOR
        if (editorDailyStateDate != today)
            ResetEditorDailyLoginState();
        claimedToday = editorDailyClaimedToday;
        streak = claimedToday ? Mathf.Max(1, editorDailyStreak) : 1;
#else
        string previousClaim = PlayerPrefs.GetString(DailyClaimKey, "");
        bool hasPreviousClaim = DateTime.TryParseExact(previousClaim, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out DateTime lastClaim);
        claimedToday = hasPreviousClaim && lastClaim.Date == DateTime.Now.Date;
        int savedStreak = Mathf.Max(1, PlayerPrefs.GetInt(DailyStreakKey, 0));
        if (claimedToday)
        {
            streak = savedStreak;
        }
        else if (hasPreviousClaim && lastClaim.Date == DateTime.Now.Date.AddDays(-1))
        {
            streak = savedStreak + 1;
        }
        else
        {
            streak = 1;
        }
#endif
    }

#if UNITY_EDITOR
    private void ResetEditorDailyLoginState()
    {
        editorDailyClaimedToday = false;
        editorDailyStreak = 0;
        editorDailyStateDate = DateTime.Now.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
    }
#endif

    private void BindDailyLoginTiles()
    {
        if (tabPages == null || tabPages.Length <= 2 || tabPages[2] == null) return;
        Transform page = tabPages[2].transform;
        if (SceneNode("Daily Login Reward Panel", page) == null) return;

        dailyLoginReadyStates = new GameObject[7];
        dailyLoginNotReadyStates = new GameObject[7];
        dailyLoginReadyToClaimStates = new GameObject[7];
        dailyLoginCheckmarks = new GameObject[7];

        for (int i = 0; i < 7; i++)
        {
            int day = i + 1;
            Transform tile = SceneNode("Daily Login Day " + day, page);
            if (tile == null) continue;

            dailyLoginReadyStates[i] = FindDailyLoginState(tile, "ready");
            dailyLoginNotReadyStates[i] = FindDailyLoginState(tile, "notready");
            dailyLoginReadyToClaimStates[i] = FindDailyLoginState(tile, "readytoclaim");
            dailyLoginCheckmarks[i] = FindDailyLoginState(tile, "checkmark");
        }
        BindDailyRewardGlows();
    }

    private void RefreshDailyLoginTiles(bool claimedToday, int streak)
    {
        if (dailyLoginReadyStates == null) BindDailyLoginTiles();
        if (dailyLoginReadyStates == null) return;

        int activeDay = ((Mathf.Max(1, streak) - 1) % 7) + 1;
        int completedThrough = claimedToday ? activeDay : activeDay - 1;

        for (int i = 0; i < 7; i++)
        {
            int day = i + 1;
            bool completed = day <= completedThrough;
            bool claimable = !claimedToday && day == activeDay;
            if (dailyLoginReadyStates[i] != null) dailyLoginReadyStates[i].SetActive(completed || claimable);
            if (dailyLoginNotReadyStates[i] != null) dailyLoginNotReadyStates[i].SetActive(!completed && !claimable);
            if (dailyLoginReadyToClaimStates[i] != null) dailyLoginReadyToClaimStates[i].SetActive(claimable);
            if (dailyLoginCheckmarks[i] != null) dailyLoginCheckmarks[i].SetActive(completed);
        }
    }

    private static GameObject FindDailyLoginState(Transform tile, string stateName)
    {
        foreach (Transform child in tile.GetComponentsInChildren<Transform>(true))
        {
            if (child == tile) continue;
            string name = child.name.Trim();
            if (string.Equals(name, stateName, StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith(stateName + " (", StringComparison.OrdinalIgnoreCase))
                return child.gameObject;
        }
        return null;
    }

    private void RefreshCoins()
    {
        if (coinCountAnimating) return;
        SetCoinLabelValue(CurrentCoins());
    }

    private void RefreshHearts()
    {
        if (heartCountLabel != null) heartCountLabel.text = HeartManager.CountText;
        if (heartTimerLabel != null) heartTimerLabel.text = Localize.Translate(HeartManager.TimerText);
    }

    private void HandleMoneyChanged(int amount)
    {
        coinCountAnimating = false;
        SetCoinLabelValue(CurrentCoins());
    }

    private void StartCoinCountAnimation(int start, int target, float duration)
    {
        coinCountStart = start;
        coinCountTarget = target;
        coinCountElapsed = 0f;
        coinCountDuration = Mathf.Max(0.1f, duration);
        coinCountAnimating = target > start;
        SetCoinLabelValue(coinCountAnimating ? start : target);
    }

    private void UpdateCoinCountAnimation(float deltaTime)
    {
        if (!coinCountAnimating) return;

        coinCountElapsed += deltaTime;
        float progress = Mathf.Clamp01(coinCountElapsed / coinCountDuration);
        float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
        int value = Mathf.RoundToInt(Mathf.Lerp(coinCountStart, coinCountTarget, easedProgress));
        SetCoinLabelValue(value);

        if (progress >= 1f)
        {
            coinCountAnimating = false;
            SetCoinLabelValue(coinCountTarget);
        }
    }

    private void SetCoinLabelValue(int amount)
    {
        if (coinLabel == null) return;
        coinLabel.text = amount.ToString();
    }

    private static int CurrentCoins()
    {
        return CurrencyManager.Instance != null ? CurrencyManager.Instance.Money : (GameMaster.staticScript != null ? GameMaster.UserGold : PlayerPrefs.GetInt("PlayerMoney", 0));
    }

    private static void AddCoins(int amount)
    {
        CurrencyManager.AwardMoney(amount);
    }

    private void Play()
    {
        if (!HeartManager.CanPlay)
        {
            ShowEnergy();
            return;
        }
        if (HomeController.staticScript != null) HomeController.staticScript.PlayGame();
        else
        {
            PlayButtonSfx();
            SceneManager.LoadScene("Game");
        }
    }

    private sealed class InlineFeedback
    {
        public Text label;
        public string previousText;
        public string sourceMessage;
        public string message;
        public float expiresAt;
    }

    private readonly List<InlineFeedback> inlineFeedback = new List<InlineFeedback>();

    private void ShowInlineFeedback(Text label, string message)
    {
        if (label == null) return;
        InlineFeedback feedback = inlineFeedback.Find(item => item.label == label);
        if (feedback == null)
        {
            feedback = new InlineFeedback { label = label, previousText = label.text };
            inlineFeedback.Add(feedback);
        }
        else if (label.text != feedback.message)
            feedback.previousText = label.text;
        feedback.sourceMessage = message;
        feedback.message = Localize.Translate(message);
        feedback.expiresAt = Time.unscaledTime + 3f;
        label.text = feedback.message;
    }

    private void RefreshInlineFeedback()
    {
        for (int i = inlineFeedback.Count - 1; i >= 0; i--)
        {
            InlineFeedback feedback = inlineFeedback[i];
            if (feedback.label != null && Time.unscaledTime < feedback.expiresAt) continue;
            if (feedback.label != null && feedback.label.text == feedback.message)
                feedback.label.text = Localize.Translate(feedback.previousText);
            inlineFeedback.RemoveAt(i);
        }
    }

    private void RefreshLocalizedInlineFeedback()
    {
        foreach (InlineFeedback feedback in inlineFeedback)
        {
            if (feedback.label == null || Time.unscaledTime >= feedback.expiresAt) continue;
            string translatedMessage = Localize.Translate(feedback.sourceMessage);
            if (feedback.label.text != feedback.message && feedback.label.text != translatedMessage)
                feedback.previousText = feedback.label.text;
            else feedback.previousText = Localize.Translate(feedback.previousText);
            feedback.message = translatedMessage;
            feedback.label.text = translatedMessage;
        }
    }

    private GameObject Panel(Transform parent, string name, Color color, Vector2 min, Vector2 max)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var image = go.GetComponent<Image>();
        image.sprite = panelSprite;
        image.type = Image.Type.Sliced;
        image.color = color;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.32f);
        outline.effectDistance = new Vector2(2f, -2f);
        return go;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static Sprite CreatePanelSprite(int cornerRadius = 14, float pixelsPerUnit = 100f, int size = 64)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Runtime Rounded UI Sprite",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int dx = x < cornerRadius ? cornerRadius - x : (x >= size - cornerRadius ? x - (size - cornerRadius - 1) : 0);
                int dy = y < cornerRadius ? cornerRadius - y : (y >= size - cornerRadius ? y - (size - cornerRadius - 1) : 0);
                bool inCorner = dx > 0 && dy > 0;
                byte alpha = !inCorner || dx * dx + dy * dy <= cornerRadius * cornerRadius ? (byte)255 : (byte)0;
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return Sprite.Create(
            texture,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            pixelsPerUnit,
            0,
            SpriteMeshType.FullRect,
            new Vector4(cornerRadius, cornerRadius, cornerRadius, cornerRadius));
    }

    private static Sprite CreateShopBuyFaceSprite()
    {
        const int size = 96;
        const float radius = 40f;
        float half = (size - 1f) * 0.5f;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Shop Buy Button Green Bevel",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        var pixels = new Color32[size * size];
        var clear = new Color32(255, 255, 255, 0);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float qx = Mathf.Abs(x - half) - (half - radius);
                float qy = Mathf.Abs(y - half) - (half - radius);
                float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f))
                    + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                if (outside > 0f)
                {
                    pixels[y * size + x] = clear;
                    continue;
                }

                float inset = -outside;
                Color color;
                if (inset < 3f)
                    color = new Color32(237, 255, 221, 255);
                else if (inset < 7f)
                    color = new Color32(11, 156, 0, 255);
                else
                {
                    float vertical = Mathf.InverseLerp(8f, size - 9f, y);
                    color = Color32.Lerp(new Color32(20, 204, 0, 255), new Color32(61, 245, 8, 255), vertical);
                    float gloss = Mathf.Exp(-Mathf.Pow((inset - 11f) / 3.2f, 2f))
                        * Mathf.Clamp01((vertical - 0.66f) * 2.7f) * 0.22f;
                    color = Color.Lerp(color, Color.white, gloss);
                }
                pixels[y * size + x] = color;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f,
            0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }

    private static Sprite CreateCircleSprite(int size = 96)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Runtime UI Circle",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        var pixels = new Color32[size * size];
        float radius = size * 0.5f - 1f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - size * 0.5f;
                float dy = y + 0.5f - size * 0.5f;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(radius + 0.75f - distance) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
    }

    private static Sprite CreateSoftContactShadowSprite()
    {
        const int width = 128;
        const int height = 64;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "Soft Robot Contact Shadow",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            float ny = (y + 0.5f - height * 0.5f) / (height * 0.5f);
            for (int x = 0; x < width; x++)
            {
                float nx = (x + 0.5f - width * 0.5f) / (width * 0.5f);
                float distance = Mathf.Sqrt(nx * nx + ny * ny);
                float coverage = Mathf.Pow(Mathf.Clamp01(1f - distance), 2f);
                pixels[y * width + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(coverage * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite CreateCardGlowSprite()
    {
        const int size = 128;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Soft Level Card Glow",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            float ny = (y + 0.5f - size * 0.5f) / (size * 0.5f);
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f - size * 0.5f) / (size * 0.5f);
                float radius = Mathf.Sqrt(nx * nx + ny * ny);
                float edge = Mathf.Clamp01((0.96f - radius) / 0.26f);
                float softEdge = edge * edge * (3f - 2f * edge);
                float coverage = Mathf.Exp(-radius * radius * 1.65f) * softEdge;
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(coverage * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite CreateCardTriangleRaySprite()
    {
        const int width = 64;
        const int height = 128;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "Soft Triangular Card Light Ray",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            float alongRay = (y + 0.5f) / height;
            float halfWidth = Mathf.Lerp(0.42f, 0.018f, alongRay);
            float rayBrightness = Mathf.Lerp(0.82f, 0.38f, alongRay);
            for (int x = 0; x < width; x++)
            {
                float acrossRay = (x + 0.5f) / width;
                float edgeDistance = halfWidth - Mathf.Abs(acrossRay - 0.5f);
                float feather = Mathf.Clamp01(edgeDistance / 0.10f);
                float softEdge = feather * feather * (3f - 2f * feather);
                byte alpha = (byte)Mathf.RoundToInt(softEdge * rayBrightness * 255f);
                pixels[y * width + x] = new Color32(255, 255, 255, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
    }

    private static void AddContactShadow(Transform parent, string name, Vector2 center, Vector2 size)
    {
        var shadow = new GameObject(name, typeof(RectTransform), typeof(Image));
        shadow.transform.SetParent(parent, false);
        var rect = shadow.GetComponent<RectTransform>();
        rect.anchorMin = center - size * 0.5f;
        rect.anchorMax = center + size * 0.5f;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var image = shadow.GetComponent<Image>();
        image.sprite = softContactShadowSprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.color = new Color32(39, 12, 67, 190);
        image.raycastTarget = false;
    }

    private void AddButtonFeedback(Button button)
    {
        var rect = button.GetComponent<RectTransform>();
        var state = new ButtonFeedbackState
        {
            rect = rect,
            baseScale = rect.localScale
        };
        buttonFeedbackStates.Add(state);

        var trigger = button.GetComponent<EventTrigger>();
        if (trigger == null) trigger = button.gameObject.AddComponent<EventTrigger>();
        trigger.triggers = new List<EventTrigger.Entry>();
        AddPointerTrigger(trigger, EventTriggerType.PointerEnter, eventData =>
        {
            if (!button.interactable) return;
            state.targetScale = 0.88f;
            var pointer = eventData as PointerEventData;
            if (pointer != null && pointer.pointerId < 0)
                SpawnButtonBurst(pointer.position, pointer.enterEventCamera, 8);
        });
        AddPointerTrigger(trigger, EventTriggerType.PointerExit, _ => state.targetScale = 1f);
        AddPointerTrigger(trigger, EventTriggerType.PointerDown, _ =>
        {
            if (button.interactable) state.targetScale = 0.78f;
        });
        AddPointerTrigger(trigger, EventTriggerType.PointerUp, eventData =>
        {
            if (!button.interactable) return;
            var pointer = eventData as PointerEventData;
            bool stillOverButton = pointer != null
                && RectTransformUtility.RectangleContainsScreenPoint(rect, pointer.position, pointer.enterEventCamera);
            state.targetScale = stillOverButton ? 0.88f : 1f;
        });
        AddPointerTrigger(trigger, EventTriggerType.PointerClick, eventData =>
        {
            if (!button.interactable) return;
            var pointer = eventData as PointerEventData;
            if (pointer != null) SpawnButtonBurst(pointer.position, pointer.pressEventCamera, 16);
        });
    }

    private static void AddPointerTrigger(EventTrigger trigger, EventTriggerType eventType, Action<BaseEventData> callback)
    {
        var entry = new EventTrigger.Entry { eventID = eventType };
        entry.callback.AddListener(data => callback(data));
        trigger.triggers.Add(entry);
    }

    private void UpdateButtonFeedback(float deltaTime)
    {
        float blend = 1f - Mathf.Exp(-20f * deltaTime);
        for (int i = buttonFeedbackStates.Count - 1; i >= 0; i--)
        {
            var state = buttonFeedbackStates[i];
            if (state.rect == null)
            {
                buttonFeedbackStates.RemoveAt(i);
                continue;
            }

            state.currentScale = Mathf.Lerp(state.currentScale, state.targetScale, blend);
            state.rect.localScale = state.baseScale * state.currentScale;
        }
    }

    private void SpawnButtonBurst(Vector2 screenPosition, Camera eventCamera, int particleCount)
    {
        if (buttonEffectsOverlay == null || buttonSparkSprite == null || buttonDotSprite == null) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(buttonEffectsOverlay, screenPosition, eventCamera, out var localPosition))
            return;

        for (int i = 0; i < particleCount; i++)
        {
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float speed = UnityEngine.Random.Range(170f, 300f);
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var color = ButtonBurstPalette[UnityEngine.Random.Range(0, ButtonBurstPalette.Length)];
            float size = UnityEngine.Random.Range(20f, 32f);

            var particleObject = new GameObject("Button Color Spark", typeof(RectTransform), typeof(Image));
            particleObject.transform.SetParent(buttonEffectsOverlay, false);
            particleObject.transform.SetAsLastSibling();
            var rect = particleObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size * UnityEngine.Random.Range(0.70f, 1.15f), size);
            rect.anchoredPosition = localPosition;
            rect.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));

            var image = particleObject.GetComponent<Image>();
            image.sprite = UnityEngine.Random.value < 0.68f ? buttonSparkSprite : buttonDotSprite;
            image.color = color;
            image.raycastTarget = false;
            image.maskable = false;

            buttonSparkParticles.Add(new ButtonSparkParticle
            {
                rect = rect,
                image = image,
                velocity = direction * speed + Vector2.up * UnityEngine.Random.Range(45f, 100f),
                angularVelocity = UnityEngine.Random.Range(-600f, 600f),
                lifetime = UnityEngine.Random.Range(0.55f, 0.85f),
                baseColor = color,
                baseScale = Vector3.one
            });
        }
    }

    private void UpdateButtonSparkParticles(float deltaTime)
    {
        for (int i = buttonSparkParticles.Count - 1; i >= 0; i--)
        {
            var particle = buttonSparkParticles[i];
            if (particle.rect == null)
            {
                buttonSparkParticles.RemoveAt(i);
                continue;
            }

            particle.age += deltaTime;
            if (particle.age >= particle.lifetime)
            {
                DestroyUIObject(particle.rect.gameObject);
                buttonSparkParticles.RemoveAt(i);
                continue;
            }

            particle.velocity += Vector2.down * (180f * deltaTime);
            particle.rect.anchoredPosition += particle.velocity * deltaTime;
            particle.rect.Rotate(0f, 0f, particle.angularVelocity * deltaTime);
            float progress = particle.age / particle.lifetime;
            float fade = 1f - Mathf.SmoothStep(0.32f, 1f, progress);
            var color = particle.baseColor;
            color.a *= fade;
            particle.image.color = color;
            particle.rect.localScale = particle.baseScale * Mathf.Lerp(1f, 0.35f, progress);
        }
    }

    private static Sprite CreateButtonSparkSprite()
    {
        const int size = 32;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Four Point UI Spark",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            float ny = (y + 0.5f - size * 0.5f) / (size * 0.5f);
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f - size * 0.5f) / (size * 0.5f);
                float radius = Mathf.Sqrt(nx * nx + ny * ny);
                float angle = Mathf.Atan2(ny, nx);
                float armLength = 0.10f + 0.42f * Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 2f)), 2f);
                float alpha = 1f - Mathf.SmoothStep(armLength - 0.035f, armLength + 0.035f, radius);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite CreateButtonDotSprite()
    {
        const int size = 32;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Soft UI Color Dot",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            float ny = (y + 0.5f - size * 0.5f) / (size * 0.5f);
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f - size * 0.5f) / (size * 0.5f);
                float radius = Mathf.Sqrt(nx * nx + ny * ny);
                float alpha = 1f - Mathf.SmoothStep(0.78f, 0.96f, radius);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite CreatePlusBadgeSprite()
    {
        const int size = 128;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Glossy Green Plus Badge",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f;
                float py = y + 0.5f;
                float vertical = Mathf.Clamp01(py / (size - 1f));
                Color pixel = Color.clear;

                // Keep a rounded silhouette with transparent corners. The final mask is
                // applied after all highlights and the plus sign so no layer can create
                // pixels outside the button shape.
                float silhouetteDistance = RoundedRectDistance(px, py, 64f, 65f, 61f, 61f, 34f);
                if (silhouetteDistance >= 0f)
                {
                    pixels[y * size + x] = Color.clear;
                    continue;
                }

                // Soft dark underside, enclosed by the rounded silhouette mask.
                float shadowDistance = RoundedRectDistance(px, py, 64f, 61f, 58f, 58f, 32f);
                float shadowCoverage = 1f - Mathf.SmoothStep(-1.5f, 5.5f, shadowDistance);
                pixel = BlendSpritePixel(pixel, new Color32(21, 42, 75, 255), shadowCoverage * 0.52f);

                // Thin pearly outside rim like the sample, with a slightly darker bottom edge.
                float rimDistance = RoundedRectDistance(px, py, 64f, 66f, 60f, 59f, 33f);
                float rimCoverage = 1f - Mathf.SmoothStep(-0.7f, 0.7f, rimDistance);
                Color rim = Color.Lerp(new Color32(255, 255, 255, 255), new Color32(196, 218, 255, 255),
                    Mathf.Clamp01((0.48f - vertical) * 1.75f));
                pixel = BlendSpritePixel(pixel, rim, rimCoverage);

                // Green body and inset face form the bright rounded-square bevel.
                float bodyDistance = RoundedRectDistance(px, py, 64f, 66f, 57f, 57f, 31f);
                float bodyCoverage = 1f - Mathf.SmoothStep(-0.7f, 0.7f, bodyDistance);
                Color body = Color.Lerp(new Color32(0, 154, 24, 255), new Color32(10, 224, 9, 255), vertical);
                pixel = BlendSpritePixel(pixel, body, bodyCoverage);

                float bevelDistance = RoundedRectDistance(px, py, 64f, 67f, 53f, 53f, 28f);
                float bevelCoverage = 1f - Mathf.SmoothStep(-0.7f, 0.7f, bevelDistance);
                Color bevel = Color.Lerp(new Color32(0, 119, 22, 255), new Color32(35, 237, 29, 255), vertical);
                pixel = BlendSpritePixel(pixel, bevel, bevelCoverage);

                float faceDistance = RoundedRectDistance(px, py, 64f, 68f, 49f, 49f, 25f);
                float faceCoverage = 1f - Mathf.SmoothStep(-0.7f, 0.7f, faceDistance);
                Color face = Color.Lerp(new Color32(0, 224, 7, 255), new Color32(25, 255, 0, 255), vertical);
                float upperSheen = Mathf.Exp(-Mathf.Pow((py - (112f - Mathf.Abs(px - 64f) * 0.10f)) / 5.5f, 2f))
                    * Mathf.Clamp01(1f - Mathf.Abs(px - 64f) / 47f) * 0.34f;
                float leftGlint = Mathf.Exp(-Mathf.Pow((px - 13f) / 2.2f, 2f))
                    * Mathf.Exp(-Mathf.Pow((py - 82f) / 23f, 2f)) * 0.20f;
                face = Color.Lerp(face, Color.white, Mathf.Clamp01(upperSheen + leftGlint));
                pixel = BlendSpritePixel(pixel, face, faceCoverage);

                float outerGlint = Mathf.Clamp01(1f - Mathf.Abs(rimDistance + 1.5f) / 1.5f);
                pixel = BlendSpritePixel(pixel, Color.white, outerGlint * Mathf.Lerp(0.18f, 0.78f, vertical));
                float innerGlint = Mathf.Clamp01(1f - Mathf.Abs(faceDistance + 1.3f) / 1.2f);
                pixel = BlendSpritePixel(pixel, new Color32(183, 255, 142, 255), innerGlint * 0.32f);

                // Give the plus the broad, raised proportions of the supplied badge.
                float verticalBar = RoundedRectDistance(px, py, 64f, 65f, 10.5f, 42f, 10.5f);
                float horizontalBar = RoundedRectDistance(px, py, 64f, 65f, 39f, 10.5f, 10.5f);
                float plusDistance = Mathf.Min(verticalBar, horizontalBar);
                float shadowVertical = RoundedRectDistance(px, py, 64f, 57f, 10.5f, 42f, 10.5f);
                float shadowHorizontal = RoundedRectDistance(px, py, 64f, 57f, 39f, 10.5f, 10.5f);
                float plusShadow = 1f - Mathf.SmoothStep(-1.5f, 4.5f, Mathf.Min(shadowVertical, shadowHorizontal));
                pixel = BlendSpritePixel(pixel, new Color32(0, 61, 15, 255), plusShadow * 0.78f);

                float plusGlow = Mathf.Exp(-Mathf.Pow(Mathf.Max(0f, plusDistance) / 4.5f, 2f)) * 0.24f;
                pixel = BlendSpritePixel(pixel, new Color32(235, 255, 235, 255), plusGlow);
                float plusCoverage = 1f - Mathf.SmoothStep(-0.75f, 0.75f, plusDistance);
                Color plusColor = Color.Lerp(new Color32(224, 250, 231, 255), Color.white, Mathf.Clamp01(vertical));
                pixel = BlendSpritePixel(pixel, plusColor, plusCoverage);

                // Pixels outside the rounded contour are exactly transparent. Keep a
                // sub-pixel antialiased edge only on the inside to avoid a green fringe.
                float edgeCoverage = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(-silhouetteDistance / 0.65f));
                pixel.a *= edgeCoverage;
                pixels[y * size + x] = pixel;
            }
        }

        texture.SetPixels(pixels);
        // Keep the tiny runtime texture readable so Unity can build the tight sprite mesh.
        texture.Apply(false, false);
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.Tight);
    }

    private static float RoundedRectDistance(float x, float y, float centerX, float centerY, float halfWidth, float halfHeight, float radius)
    {
        float qx = Mathf.Abs(x - centerX) - (halfWidth - radius);
        float qy = Mathf.Abs(y - centerY) - (halfHeight - radius);
        float outsideX = Mathf.Max(qx, 0f);
        float outsideY = Mathf.Max(qy, 0f);
        return Mathf.Sqrt(outsideX * outsideX + outsideY * outsideY)
            + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
    }

    private static Color BlendSpritePixel(Color background, Color foreground, float coverage)
    {
        float foregroundAlpha = Mathf.Clamp01(foreground.a * coverage);
        float backgroundAlpha = background.a;
        float outputAlpha = foregroundAlpha + backgroundAlpha * (1f - foregroundAlpha);
        if (outputAlpha <= 0.0001f) return Color.clear;

        Color output = (foreground * foregroundAlpha + background * (backgroundAlpha * (1f - foregroundAlpha))) / outputAlpha;
        output.a = outputAlpha;
        return output;
    }

    private static Sprite CreateSlicedFooterBarSprite(Sprite source)
    {
        if (source == null || source.texture == null) return source;

        const float sourceWidth = 2172f;
        const float sourceHeight = 724f;
        float scaleX = source.texture.width / sourceWidth;
        float scaleY = source.texture.height / sourceHeight;
        var visibleRect = new Rect(20f * scaleX, 155f * scaleY, 2134f * scaleX, 429f * scaleY);
        var border = new Vector4(160f * scaleX, 160f * scaleY, 160f * scaleX, 160f * scaleY);
        return Sprite.Create(
            source.texture,
            visibleRect,
            new Vector2(0.5f, 0.5f),
            600f * scaleX,
            0,
            SpriteMeshType.FullRect,
            border);
    }

    private static Sprite CreateBlankFooterBarSprite(Sprite source)
    {
        if (source == null || source.texture == null) return source;

        var texture = source.texture;
        var panelRect = new Rect(
            texture.width * 0.015f,
            texture.height * 0.15f,
            texture.width * 0.97f,
            texture.height * 0.70f);
        return Sprite.Create(
            texture,
            panelRect,
            new Vector2(0.5f, 0.5f),
            source.pixelsPerUnit,
            0,
            SpriteMeshType.FullRect,
            new Vector4(panelRect.height * (160f / 478f), 0f, panelRect.height * (160f / 478f), 0f));
    }

    private static void ConfigureFooterImage(Image image)
    {
        if (image == null || image.sprite == null) return;
        image.type = Image.Type.Sliced;
        image.preserveAspect = false;

        // Scale the end caps uniformly with the bar's height; extra width only stretches the center.
        float height = image.rectTransform.rect.height;
        if (height <= 0f) return;
        float multiplier = image.sprite.rect.height / (height * image.pixelsPerUnit);
        if (!Mathf.Approximately(image.pixelsPerUnitMultiplier, multiplier))
            image.pixelsPerUnitMultiplier = multiplier;
    }

    private static Sprite CreateSelectedTabSprite()
    {
        const int width = 240;
        const int height = 320;
        const float halfWidth = 115f;
        const float halfHeight = 155f;
        const float cornerRadius = 27f;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "Glossy Violet Selected Tab Background",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        var pixels = new Color32[width * height];
        var top = new Color32(142, 111, 255, 255);
        var bottom = new Color32(62, 28, 218, 255);
        var gloss = new Color32(245, 231, 255, 255);
        var outerRim = new Color32(49, 17, 180, 255);
        var innerRim = new Color32(127, 82, 255, 255);
        var glow = new Color32(179, 132, 255, 255);
        for (int y = 0; y < height; y++)
        {
            float t = y / (float)(height - 1);
            var baseColor = Color.Lerp(bottom, top, t);

            for (int x = 0; x < width; x++)
            {
                float px = Mathf.Abs(x + 0.5f - width * 0.5f) - (halfWidth - cornerRadius);
                float py = Mathf.Abs(y + 0.5f - height * 0.5f) - (halfHeight - cornerRadius);
                float outsideX = Mathf.Max(px, 0f);
                float outsideY = Mathf.Max(py, 0f);
                float distance = Mathf.Sqrt(outsideX * outsideX + outsideY * outsideY)
                    + Mathf.Min(Mathf.Max(px, py), 0f) - cornerRadius;
                float bodyAlpha = 1f - Mathf.SmoothStep(-1f, 1f, distance);
                float outsideDistance = Mathf.Max(0f, distance);
                float glowAlpha = Mathf.Exp(-Mathf.Pow(outsideDistance / 5.5f, 2f)) * 0.24f;
                if (bodyAlpha <= 0f && glowAlpha < 0.005f)
                {
                    pixels[y * width + x] = new Color32(0, 0, 0, 0);
                    continue;
                }

                float u = x / (float)(width - 1);
                float insideDistance = Mathf.Max(0f, -distance);
                float outerBevel = 1f - Mathf.SmoothStep(1f, 6f, insideDistance);
                float brightRim = Mathf.Exp(-Mathf.Pow((insideDistance - 8f) / 2.1f, 2f));
                float innerLine = Mathf.Exp(-Mathf.Pow((insideDistance - 14f) / 2.2f, 2f));
                float topSheen = Mathf.Exp(-Mathf.Pow((t - 0.95f) / 0.055f, 2f)) * 0.32f;
                float softReflection = Mathf.Exp(-Mathf.Pow((t - 0.76f) / 0.20f, 2f)) * 0.10f;
                float sideGlint = Mathf.Exp(-Mathf.Pow((u - 0.15f) / 0.055f, 2f))
                    * Mathf.Exp(-Mathf.Pow((t - 0.72f) / 0.24f, 2f)) * 0.18f;
                var fill = Color.Lerp(baseColor, gloss, Mathf.Clamp01(topSheen + softReflection + sideGlint));
                fill = Color.Lerp(fill, outerRim, outerBevel * 0.82f);
                fill = Color.Lerp(fill, gloss, brightRim * 0.86f);
                fill = Color.Lerp(fill, innerRim, innerLine * 0.56f);
                float alpha = bodyAlpha * Mathf.Clamp01(0.42f + brightRim * 0.34f + outerBevel * 0.20f);
                alpha = Mathf.Max(alpha, glowAlpha);
                pixels[y * width + x] = new Color32(
                    (byte)(fill.r * 255f),
                    (byte)(fill.g * 255f),
                    (byte)(fill.b * 255f),
                    (byte)(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return Sprite.Create(
            texture,
            new Rect(0, 0, width, height),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect);
    }

    private Text Label(Transform parent, string text, int size, Vector2 anchor, Vector2 dimensions, Color color, FontStyle style)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchor - dimensions * 0.5f;
        rect.anchorMax = anchor + dimensions * 0.5f;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var label = go.GetComponent<Text>();
        label.font = font;
        label.text = Localize.Translate(text);
        label.fontSize = size;
        label.fontStyle = style;
        label.color = color;
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        return label;
    }

    private Button Button(Transform parent, string name, string label, Vector2 anchor, Vector2 dimensions, Color color, int size, Action action)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchor - dimensions * 0.5f;
        rect.anchorMax = anchor + dimensions * 0.5f;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var image = go.GetComponent<Image>();
        image.sprite = panelSprite;
        image.type = Image.Type.Sliced;
        image.color = color;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.35f);
        outline.effectDistance = new Vector2(2f, -2f);
        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0.08f, 0.015f, 0.25f, 0.8f);
        shadow.effectDistance = new Vector2(0f, -7f);
        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        // The existing Play handler already plays the button clip itself.
        if (name != "PLAY")
            button.onClick.AddListener(PlayButtonSfx);
        button.onClick.AddListener(() => action());
        AddButtonFeedback(button);
        var text = new GameObject("Label", typeof(RectTransform), typeof(Text));
        text.transform.SetParent(go.transform, false);
        var textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;
        var component = text.GetComponent<Text>();
        component.font = font;
        component.text = Localize.Translate(label);
        component.fontSize = size;
        component.fontStyle = FontStyle.Bold;
        component.color = Color.white;
        component.alignment = TextAnchor.MiddleCenter;
        var textShadow = text.AddComponent<Shadow>();
        textShadow.effectColor = new Color(0.12f, 0.05f, 0.28f, 0.62f);
        textShadow.effectDistance = new Vector2(1f, -2f);
        component.resizeTextForBestFit = true;
        component.resizeTextMinSize = Mathf.Max(16, size - 10);
        component.resizeTextMaxSize = size;
        return button;
    }

    private Button Button(Transform parent, string name, string label, Vector2 anchor, Vector2 dimensions, Color color, Action action)
    {
        return Button(parent, name, label, anchor, dimensions, color, 21, action);
    }

    private static void PlayButtonSfx()
    {
        if (AudioManager.Instance != null && AudioManager.Instance.buttonClick != null)
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.buttonClick);
            return;
        }

        if (GameMaster.staticScript != null)
            GameMaster.PlaySoundClick();
    }

    private void UpdateBottomTabTextSizing()
    {
        if (bottomTabLabels == null) return;

        for (int i = 0; i < bottomTabLabels.Length; i++)
        {
            var label = bottomTabLabels[i];
            if (label == null) continue;

            var labelRect = label.rectTransform;
            bool isSelected = i == activeTabIndex;
            float horizontalInset = isSelected ? 0.10f : 0f;
            labelRect.anchorMin = new Vector2(horizontalInset, 0.01f);
            labelRect.anchorMax = new Vector2(1f - horizontalInset, 0.37f);
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;

            float availableWidth = labelRect.rect.width;
            float availableHeight = labelRect.rect.height;
            if (isSelected && selectedTabBlock != null && selectedTabBlock.rect.width > 0f)
                availableWidth = Mathf.Min(availableWidth, selectedTabBlock.rect.width * 0.82f);
            if (availableWidth <= 0f || availableHeight <= 0f) continue;

            float widthFill = isSelected ? 0.90f : 0.94f;
            // Keep labels on one line and scale them to the actual tab width.
            string[] lines = label.text.Split('\n');
            int longestLine = 1;
            foreach (string line in lines) longestLine = Mathf.Max(longestLine, line.Length);
            float maxFromWidth = availableWidth * widthFill / (longestLine * 0.62f);
            float maxFromHeight = availableHeight * 0.90f / lines.Length;
            int fittingFontSize = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(maxFromWidth, maxFromHeight)), 8, 30);

            label.resizeTextForBestFit = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            var fitter = label.GetComponent<HomeTextFit>();
            if (fitter != null) fitter.SetMaximumFontSize(fittingFontSize, true);
            else if (label.fontSize != fittingFontSize) label.fontSize = fittingFontSize;
        }
    }

    private static void SetSelectedTabAnchors(RectTransform rect, float centerX, RectTransform iconRect)
    {
        var navigationRect = rect.parent as RectTransform;
        var tabButtonRect = iconRect != null ? iconRect.parent as RectTransform : null;
        float width = navigationRect != null && tabButtonRect != null && navigationRect.rect.width > 0f
            ? tabButtonRect.rect.width / navigationRect.rect.width
            : 0.19f;
        width *= 1.10f;
        const float bottomInset = 0f;
        const float topInset = 0f;
        rect.anchorMin = new Vector2(centerX - width * 0.5f, bottomInset);
        rect.anchorMax = new Vector2(centerX + width * 0.5f, 1f - topInset);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private RectTransform GetTabIconRect(int index)
    {
        return tabIconRects != null && index >= 0 && index < tabIconRects.Length
            ? tabIconRects[index]
            : null;
    }

    private float GetTabIconCenterInNavigation(int index)
    {
        if (selectedTabBlock == null || tabIconRects == null || index < 0 || index >= tabIconRects.Length)
            return TabCenters[Mathf.Clamp(index, 0, TabCenters.Length - 1)];

        var iconRect = tabIconRects[index];
        var navigationRect = selectedTabBlock.parent as RectTransform;
        if (iconRect == null || navigationRect == null || navigationRect.rect.width <= 0f)
            return TabCenters[index];

        Vector3 iconCenterWorld = iconRect.TransformPoint(iconRect.rect.center);
        float iconCenterLocalX = navigationRect.InverseTransformPoint(iconCenterWorld).x;
        return Mathf.InverseLerp(navigationRect.rect.xMin, navigationRect.rect.xMax, iconCenterLocalX);
    }
}

