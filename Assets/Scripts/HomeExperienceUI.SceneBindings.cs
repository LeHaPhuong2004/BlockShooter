using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class HomeExperienceUI
{
    [Header("Authored Home UI")]
    [SerializeField] private Canvas sceneHomeCanvas;
    private bool sceneHomeBound;

#if UNITY_EDITOR
    public void PreparePlayButtonObjectsForEditor()
    {
        if (Application.isPlaying)
            throw new InvalidOperationException("Prepare the Play button objects outside Play Mode.");
        LoadHomeResources();
        sceneHomeCanvas = SceneComponent<Canvas>("Home Canvas", transform);
        if (sceneHomeCanvas == null)
            throw new InvalidOperationException("The authored Home Canvas is missing.");
        homeCanvasObject = sceneHomeCanvas.gameObject;
        playButtonRect = SceneComponent<RectTransform>("PLAY");
        if (playButtonRect == null)
            throw new InvalidOperationException("The authored PLAY button is missing.");
        ConfigurePlayButton(playButtonRect);
    }
#endif

    private bool BindSceneHome()
    {
        if (sceneHomeBound) return true;
        try
        {
            if (sceneHomeCanvas == null)
                sceneHomeCanvas = SceneComponent<Canvas>("Home Canvas", transform);
            if (sceneHomeCanvas == null)
                throw new InvalidOperationException("Assign the existing Home Canvas to HomeExperienceUI.");
            homeCanvasObject = sceneHomeCanvas.gameObject;
            var homeRaycaster = sceneHomeCanvas.GetComponent<GraphicRaycaster>();
            if (homeRaycaster != null) homeRaycaster.enabled = true;
            var root = SceneNode("Home");
            if (root == null) throw new InvalidOperationException("The existing Home Canvas has no Home root.");

            tabPages = new[]
            {
                SceneObject("Home Only Content"), SceneObject("Shop Page"), SceneObject("Daily Missions Page"),
                SceneObject("Achievements Page"), SceneObject("Character Page")
            };
            foreach (var page in tabPages)
                if (page == null) throw new InvalidOperationException("One of the five authored Home tab pages is missing.");

            var coinButton = SceneNode("COINS");
            coinHudButtonRect = coinButton as RectTransform;
            coinHudIconRect = coinButton != null ? coinButton.Find("Button Icon") as RectTransform : null;
            if (coinHudIconRect != null) coinHudIconBaseScale = coinHudIconRect.localScale;
            coinHudIconOutlineRect = FindCoinHudIconOutline(coinHudIconRect);
            if (coinHudIconOutlineRect != null) coinHudIconOutlineBaseScale = coinHudIconOutlineRect.localScale;
            coinLabel = SceneComponent<Text>("Label", coinButton);
            heartTimerLabel = SceneComponent<Text>("Label", SceneNode("ENERGY"));
            var heartIcon = SceneNode("ENERGY")?.Find("Button Icon");
            heartCountLabel = heartIcon != null ? heartIcon.GetComponentInChildren<Text>(true) : null;
            floatingCharacterRect = SceneComponent<RectTransform>("Floating Robot Character");
            homeCharacterImage = floatingCharacterRect != null ? floatingCharacterRect.GetComponent<Image>() : null;
            if (floatingCharacterRect != null)
            {
                floatingCharacterBasePosition = floatingCharacterRect.anchoredPosition;
                floatingCharacterBaseScale = floatingCharacterRect.localScale;
            }
            floatingCharacterShadow = SceneComponent<CanvasGroup>("Robot Contact Shadows");
            playButtonRect = SceneComponent<RectTransform>("PLAY");
            ConfigurePlayButton(playButtonRect);
            buttonEffectsOverlay = SceneComponent<RectTransform>("Button Effects Overlay");

            var nav = SceneNode("Bottom Navigation");
            selectedTabBlock = SceneComponent<RectTransform>("Selected Tab", nav);
            footerBackgroundObject = SceneObject("Footer Sprite", nav);
            string[] tabNames = { "HOME_TAB", "SHOP", "MISSIONS", "ACHIEVEMENTS", "CHARACTER" };
            tabIconRects = new RectTransform[tabNames.Length];
            bottomTabLabels = new Text[tabNames.Length];
            for (int i = 0; i < tabNames.Length; i++)
            {
                var tab = SceneNode(tabNames[i], nav);
                tabIconRects[i] = tab != null ? SceneSuffixComponent<RectTransform>(tab, " Tab Icon") : null;
                bottomTabLabels[i] = SceneComponent<Text>("Label", tab);
            }
            dailyButtonLabel = bottomTabLabels[2];
            missionBadge = SceneObject("Mission Notification", SceneNode("MISSIONS", nav));
            achievementBadge = SceneObject("Achievement Notification", SceneNode("ACHIEVEMENTS", nav));

            BindOutOfHeartPanel();
            BindSettingsPopup();
            BindSpecialLevelPopups();
            BindSpecialLevelCardLocks();
            if (coinLabel == null || heartTimerLabel == null || heartCountLabel == null || playButtonRect == null ||
                selectedTabBlock == null || Array.Exists(tabIconRects, rect => rect == null))
                throw new InvalidOperationException("The authored Home HUD/navigation is incomplete.");

            shopGoldTabButton = SceneComponent<Button>("SHOP_GOLD_TAB");
            shopBoosterTabButton = SceneComponent<Button>("SHOP_BOOSTER_TAB");
            shopGoldTabImage = SceneComponent<Image>("Tab Sprite", shopGoldTabButton != null ? shopGoldTabButton.transform : null);
            shopBoosterTabImage = SceneComponent<Image>("Tab Sprite", shopBoosterTabButton != null ? shopBoosterTabButton.transform : null);
            PrepareAuthoredShopTab(shopGoldTabButton, shopGoldTabImage);
            PrepareAuthoredShopTab(shopBoosterTabButton, shopBoosterTabImage);
            shopGoldViewport = SceneObject("Gold Shop Viewport");
            shopBoosterViewport = SceneObject("Booster Shop Viewport");
            BindShopCardLightSweeps();
            BindBoosterPurchasePopup();
            // Keep every Shop card at the size authored in Home.unity.
            BindSceneCharacters();
            BindSceneMissions();
            BindDailyLoginTiles();
            BindSceneAchievements();

            buttonFeedbackStates.Clear();
            foreach (var button in homeCanvasObject.GetComponentsInChildren<Button>(true))
            {
                var action = SceneButtonAction(button);
                // Preserve custom Inspector events; generated buttons have no persistent events.
                bool shopCategoryTab = button.name == "SHOP_GOLD_TAB" || button.name == "SHOP_BOOSTER_TAB";
                bool achievementClaimButton = button.name.StartsWith("CLAIM_ACHIEVEMENT_", StringComparison.Ordinal);
                bool shopPurchaseButton = button.name.StartsWith("BUY_BOOSTER_OFFER_", StringComparison.Ordinal) ||
                    TryGetDirectBooster(button.name, out _, out _, out _) || TryGetHeartPackAmount(button.name, out _);
                bool specialLevelButton = action != null &&
                    (button.name.StartsWith("LOCKED_", StringComparison.Ordinal) ||
                     button.name.StartsWith("SPECIAL_LOCK_", StringComparison.Ordinal) ||
                     button.name.StartsWith("SPECIAL_CONFIRM_", StringComparison.Ordinal));
                if (action != null && (shopCategoryTab || specialLevelButton || shopPurchaseButton || achievementClaimButton))
                {
                    // Always bind these authored controls to the active Home UI behavior.
                    button.onClick.RemoveAllListeners();
                    if (TryGetHeartPackAmount(button.name, out _) || achievementClaimButton)
                        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                            button.onClick.SetPersistentListenerState(i, UnityEngine.Events.UnityEventCallState.Off);
                    if (button.name != "PLAY") button.onClick.AddListener(PlayButtonSfx);
                    button.onClick.AddListener(() => action());
                }
                else if (button.onClick.GetPersistentEventCount() == 0 && action != null)
                {
                    button.onClick.RemoveAllListeners();
                    if (button.name != "PLAY") button.onClick.AddListener(PlayButtonSfx);
                    button.onClick.AddListener(() => action());
                }
                AddButtonFeedback(button);
                if (action == null && button.onClick.GetPersistentEventCount() == 0 &&
                    !button.name.StartsWith("Settings ", StringComparison.Ordinal))
                    Debug.LogWarning("Home button has no scene binding: " + button.name, button);
            }
            BindSceneCardLights();
            builtLandscape = false;
            builtScreenAspect = 1080f / 1920f;
            SetShopCategory(false);
            SelectTab(0);
            selectedTabX = targetTabX;
            tabVelocity = 0f;
            if (Application.isPlaying) PortraitDisplay.ConfigureCanvas(sceneHomeCanvas);
            ApplyResponsiveLayout(PortraitDisplay.ContentPixelSize);
            sceneHomeBound = true;
            RefreshLocalizedHome();
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError("Home scene UI could not be bound. " + exception.Message, this);
            return false;
        }
    }

    private static void PrepareAuthoredShopTab(Button button, Image visual)
    {
        if (button == null) return;
        button.enabled = true;
        button.interactable = true;
        button.transition = Selectable.Transition.None;
        // The scroll viewport overlaps the tab's hit area. Raise only its draw/input
        // order; this parent has no layout group and its RectTransform stays authored.
        button.transform.SetAsLastSibling();

        string prefix = button.name == "SHOP_GOLD_TAB" ? "Gold" : "Booster";
        foreach (string suffix in new[] { "Chosen", "NotChoose" })
        {
            Transform state = button.transform.Find(prefix + suffix);
            Image face = state != null ? state.GetComponent<Image>() : null;
            if (face != null) face.raycastTarget = true;
        }

        if (visual != null)
        {
            visual.enabled = true;
            visual.raycastTarget = true;
            if (button.targetGraphic == null) button.targetGraphic = visual;
        }

        if (button.targetGraphic != null) button.targetGraphic.raycastTarget = true;
    }

    private void BindSceneCharacters()
    {
        var page = tabPages[4].transform;
        characterPreviewImage = SceneComponent<Image>("Character Preview Sprite", page);
        BindCharacterPreviewAnimation();
        characterPreviewName = SceneComponent<Text>("Label", SceneNode("Character Name Capsule", page));
        characterActionButton = SceneComponent<Button>("CHARACTER_ACTION", page);
        characterActionLabel = SceneComponent<Text>("Label", characterActionButton != null ? characterActionButton.transform : null);
        characterActionCoinIcon = SceneComponent<Image>("Character Action Coin Icon", page);
        characterCardFrames = new Image[4];
        characterCardPortraits = new Image[4];
        characterCardCheckMarks = new GameObject[4];
        characterCardPrices = new GameObject[4];
        characterCardLocks = new GameObject[4];
        characterCardUseBadges = new GameObject[4];
        for (int i = 0; i < 4; i++)
        {
            var card = SceneNode("Character Skin " + i, page);
            if (card == null) throw new InvalidOperationException("Missing authored Character Skin " + i);
            characterCardFrames[i] = SceneComponent<Image>("Card Frame Sprite", card);
            characterCardPortraits[i] = SceneComponent<Image>("Character Portrait Sprite", card);
            characterCardCheckMarks[i] = SceneObject("Equipped Check Sprite", card);
            characterCardPrices[i] = SceneObject("Blue Skin Price Badge", card);
            characterCardLocks[i] = SceneObject("Locked Skin Badge", card);
            characterCardUseBadges[i] = SceneObject("Use Skin Badge", card);
        }
        BindCharacterSelectionHighlight(page);
    }

    private void BindSceneMissions()
    {
        foreach (var card in tabPages[2].GetComponentsInChildren<RectTransform>(true))
        {
            if (!TrySceneMission(card.name, out var mission)) continue;
            var label = SceneSuffixComponent<TMP_Text>(card, " Progress Text");
            var fill = SceneSuffixComponent<Image>(card, " Progress Fill");
            var track = SceneSuffixComponent<Image>(card, " Progress Track");
            var reward = SceneSuffixComponent<Button>(card, " Reward Coin Icon");
            var rewardFrame = SceneSuffixComponent<Image>(card, " Reward Frame");
            var rewardAmount = SceneSuffixComponent<TMP_Text>(card, " Reward Amount");
            if (label == null || fill == null || track == null || reward == null || rewardFrame == null || rewardAmount == null)
                throw new InvalidOperationException("The authored mission card is incomplete: " + card.name);
            // A saved scene can contain a zero-progress fill; recover its full extent from the track.
            var fillRect = fill.rectTransform;
            float inset = fillRect.anchorMin.x - track.rectTransform.anchorMin.x;
            fillRect.anchorMax = new Vector2(track.rectTransform.anchorMax.x - inset, fillRect.anchorMax.y);
            var ui = card.GetComponent<DailyMissionCardUI>();
            if (ui == null) ui = card.gameObject.AddComponent<DailyMissionCardUI>();
            ui.Bind(mission, label, fill, reward, rewardFrame.gameObject, reward.gameObject, rewardAmount.gameObject);
        }
    }

    private Action SceneButtonAction(Button button)
    {
        var achievementAction = AchievementButtonAction(button.name);
        if (achievementAction != null) return achievementAction;

        var heartAction = HeartShopButtonAction(button.name);
        if (heartAction != null) return heartAction;

        var boosterOfferAction = BoosterOfferButtonAction(button.name);
        if (boosterOfferAction != null) return boosterOfferAction;

        var specialLevelAction = SpecialLevelPopupButtonAction(button);
        if (specialLevelAction != null) return specialLevelAction;

        switch (button.name)
        {
            case "PROFILE": return ShowCharacter;
            case "COINS": return ShowShop;
            case "ENERGY": return ShowEnergy;
            case "SETTINGS": return ShowSettings;
            case "LEVEL_BANNER": return ShowEvent;
            case "PLAY": return Play;
            case "SHOP": return () => SelectTab(1);
            case "MISSIONS": return () => SelectTab(2);
            case "HOME_TAB": return () => SelectTab(0);
            case "ACHIEVEMENTS": return () => SelectTab(3);
            case "CHARACTER": return () => SelectTab(4);
            case "SHOP_GOLD_TAB": return () => SetShopCategory(false);
            case "SHOP_BOOSTER_TAB": return () => SetShopCategory(true);
            case "SHOP_TITLE_BANNER": return () => { };
            case "CHARACTER_ACTION": return ActivateCharacterAction;
            case "CHARACTER_PREVIOUS": return () => CycleCharacterPreview(-1);
            case "CHARACTER_NEXT": return () => CycleCharacterPreview(1);
        }
        if (TryGetCoinPackNumber(button.name, out int packNumber))
            return () => IAPManager.BuyCoinPack(packNumber);
        if (button.name.StartsWith("BUY ", StringComparison.Ordinal))
        {
            string title = button.name.Substring(4);
            return () => ShowShopItemInfo(title);
        }
        const string dailyDayPrefix = "Daily Login Day ";
        if (button.name.StartsWith(dailyDayPrefix, StringComparison.Ordinal) &&
            int.TryParse(button.name.Substring(dailyDayPrefix.Length), out int day))
            return () => ClaimDailyLoginDay(day);
        if (button.name.StartsWith("Character Skin ", StringComparison.Ordinal) && int.TryParse(button.name.Substring(15), out int index))
            return () => SelectCharacterPreview(index);
        if (button.name.EndsWith(" Reward Coin Icon", StringComparison.Ordinal) && button.transform.parent != null &&
            TrySceneMission(button.transform.parent.name, out var mission))
            return () => ClaimDailyMission(mission);
        return null;
    }

    private static bool TrySceneMission(string name, out DailyMissionType mission)
    {
        switch (name)
        {
            case "Break 50 Blocks": mission = DailyMissionType.BreakBlocks; return true;
            case "Play 3 Games": mission = DailyMissionType.PlayGames; return true;
            case "Use 5 Boosters": mission = DailyMissionType.UseBoosters; return true;
            case "Win 5 Games":
            case "Win 2 Games": mission = DailyMissionType.WinGames; return true;
            case "Win Streak 3":
            case "Collect 5 Food": mission = DailyMissionType.WinStreak; return true;
            case "Break 1000 Blocks":
            case "Break 200 Blocks": mission = DailyMissionType.BreakMoreBlocks; return true;
            default: mission = default; return false;
        }
    }

    private void BindSceneCardLights()
    {
        cardRotatingLights.Clear();
        foreach (var rect in homeCanvasObject.GetComponentsInChildren<RectTransform>(true))
        {
            bool softRay = rect.name.Contains(" Soft Sun Ray ");
            bool centerRay = rect.name.Contains(" Center Sun Ray ");
            if (!softRay && !centerRay && !rect.name.Contains(" Sharp Ray ")) continue;
            var parent = rect.parent as RectTransform;
            if (parent == null) continue;
            Vector2 size = rect.anchorMax - rect.anchorMin;
            bool centered = size.sqrMagnitude < 0.0001f;
            int.TryParse(rect.name.Substring(rect.name.LastIndexOf(' ') + 1), out int rayNumber);
            int rayIndex = Mathf.Clamp(rayNumber - 1, 0, 7);
            float rayWidth = (0.15f - rayIndex * 0.01f) * (softRay ? 4f : 1f);
            float rayHeight = (0.42f - rayIndex * 0.03f) * (softRay ? 0.52f : 1f);
            cardRotatingLights.Add(new CardRotatingLightState
            {
                rect = rect, parentRect = parent, basePosition = rect.anchoredPosition,
                orbitRadius = centered ? Vector2.zero : new Vector2(size.x / rayWidth * 0.5f, size.y / rayHeight * 0.48f),
                phase = (rect.localEulerAngles.z + 90f) * Mathf.Deg2Rad
            });
        }
    }

    private Transform SceneNode(string name, Transform scope = null)
    {
        if (scope == null) scope = homeCanvasObject != null ? homeCanvasObject.transform : transform;
        foreach (var node in scope.GetComponentsInChildren<Transform>(true))
            if (node.name == name) return node;
        return null;
    }

    private T SceneComponent<T>(string name, Transform scope = null) where T : Component
    {
        var node = SceneNode(name, scope);
        return node != null ? node.GetComponent<T>() : null;
    }

    private GameObject SceneObject(string name, Transform scope = null) => SceneNode(name, scope)?.gameObject;

    private static T SceneSuffixComponent<T>(Transform scope, string suffix) where T : Component
    {
        foreach (var component in scope.GetComponentsInChildren<T>(true))
            if (component.name.EndsWith(suffix, StringComparison.Ordinal)) return component;
        return null;
    }

#if UNITY_EDITOR
    public bool UpgradeSceneMissionsForEditor()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Upgrade the authored Home outside Play Mode.");
        LoadHomeResources();
        sceneHomeCanvas = SceneComponent<Canvas>("Home Canvas", transform);
        if (sceneHomeCanvas == null) throw new InvalidOperationException("The authored Home Canvas is missing.");
        homeCanvasObject = sceneHomeCanvas.gameObject;
        var page = SceneNode("Daily Missions Page");
        var content = SceneNode("Daily Missions Aspect Content", page);
        if (content == null) throw new InvalidOperationException("The authored mission content is missing.");
        bool changed = false;
        var scroll = SceneComponent<ScrollRect>("Daily Mission List", content);
        if (scroll == null)
        {
            var existingCards = new List<RectTransform>();
            foreach (Transform child in content)
                if (TrySceneMission(child.name, out _)) existingCards.Add((RectTransform)child);
            BuildDailyMissionTaskList(content);
            scroll = SceneComponent<ScrollRect>("Daily Mission List", content);
            foreach (var card in existingCards)
            {
                var template = scroll.content.Find(card.name);
                if (template == null) continue;
                int order = template.GetSiblingIndex();
                card.SetParent(scroll.content, false);
                card.SetSiblingIndex(order);
                DestroyUIObject(template.gameObject);
            }
            changed = true;
        }
        foreach (Transform card in scroll.content)
        {
            if (!TrySceneMission(card.name, out var mission)) continue;
            var legacyClaim = SceneSuffixComponent<Transform>(card, " Claim Button");
            if (legacyClaim != null)
            {
                DestroyUIObject(legacyClaim.gameObject);
                changed = true;
            }
            var rewardIcon = SceneSuffixComponent<Image>(card, " Reward Coin Icon");
            if (rewardIcon != null && rewardIcon.GetComponent<Button>() == null)
            {
                PrepareDailyMissionRewardButton(rewardIcon.gameObject);
                changed = true;
            }
        }
        if (changed) HomeMissionTypography.ApplyToScene(gameObject.scene);
        return changed;
    }

    public void FixSceneLockedCardSizesForEditor()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Fix the authored card layout outside Play Mode.");
        var canvas = SceneComponent<Canvas>("Home Canvas", transform);
        if (canvas == null) throw new InvalidOperationException("The authored Home Canvas is missing.");
        var scaler = canvas.GetComponent<CanvasScaler>();
        Vector2 reference = scaler != null ? scaler.referenceResolution : new Vector2(1080f, 1920f);
        foreach (var card in canvas.GetComponentsInChildren<RectTransform>(true))
        {
            if (!card.name.StartsWith("LOCKED_", StringComparison.Ordinal) || card.GetComponent<Button>() == null) continue;
            float oldWidth = card.anchorMax.x - card.anchorMin.x;
            float height = card.anchorMax.y - card.anchorMin.y;
            if (oldWidth <= 0f || height <= 0f) continue;
            float desiredWidth = height * reference.y / reference.x;
            float ratio = desiredWidth / oldWidth;
            // The card and its light rays are siblings in the saved Home layout.
            foreach (Transform child in card.parent)
            {
                if (child != card && !child.name.StartsWith(card.name + " ", StringComparison.Ordinal)) continue;
                var rect = child as RectTransform;
                if (rect == null) continue;
                float centerX = (rect.anchorMin.x + rect.anchorMax.x) * 0.5f;
                float halfWidth = (rect.anchorMax.x - rect.anchorMin.x) * ratio * 0.5f;
                rect.anchorMin = new Vector2(centerX - halfWidth, rect.anchorMin.y);
                rect.anchorMax = new Vector2(centerX + halfWidth, rect.anchorMax.y);
            }
        }
    }
#endif
}
