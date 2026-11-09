using UnityEngine;

public sealed partial class HomeExperienceUI
{
    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        Localize.LanguageChanged -= RefreshLocalizedHome;
        Localize.LanguageChanged += RefreshLocalizedHome;
        if (sceneHomeBound) RefreshLocalizedHome();
    }

    private void OnDisable() => Localize.LanguageChanged -= RefreshLocalizedHome;

    private void RefreshLocalizedHome()
    {
        if (!sceneHomeBound || homeCanvasObject == null) return;
        Localize.Apply(homeCanvasObject.transform);
        RefreshSpecialLevelProgress();
        RefreshPlayButtonLevel();
        RefreshDailyState();
        RefreshHearts();
        RefreshAchievements();
        UpdateCharacterPageState();
        RefreshShopPurchaseLabels();
        RefreshBoosterShopPrices();
        RefreshSettingsLanguageLabels();
        RefreshLocalizedPurchaseResult();
        RefreshLocalizedInlineFeedback();
        foreach (HomeTextFit fitter in homeCanvasObject.GetComponentsInChildren<HomeTextFit>(true))
            fitter.FitNow();
        UpdateBottomTabTextSizing();
    }
}
