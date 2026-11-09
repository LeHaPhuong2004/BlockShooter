using System;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class HomeExperienceUI
{
    private bool heartPurchaseInProgress;

    private static bool TryGetHeartPackAmount(string buttonName, out int amount)
    {
        switch (buttonName)
        {
            case "BUY 5 TIM": amount = 5; return true;
            case "BUY 20 TIM": amount = 20; return true;
            default: amount = 0; return false;
        }
    }

    private Action HeartShopButtonAction(string buttonName) =>
        TryGetHeartPackAmount(buttonName, out int amount) ? (Action)(() => BuyHeartPack(amount)) : null;

    private void RefreshHeartShopPrices()
    {
        if (shopGoldViewport == null) return;
        foreach (var button in shopGoldViewport.GetComponentsInChildren<Button>(true))
        {
            if (!TryGetHeartPackAmount(button.name, out int amount)) continue;
            var label = button.transform.Find("Label")?.GetComponent<Text>();
            if (label != null)
                label.text = FormatPurchasePrice(HeartPurchaseTransaction.GetPrice(amount));
        }
    }

    private void BuyHeartPack(int amount)
    {
        if (!Application.isPlaying || heartPurchaseInProgress) return;
        heartPurchaseInProgress = true;
        try
        {
            HeartPurchaseResult result = HeartPurchaseTransaction.TryBuy(amount, () => CurrencyManager.Balance,
                CurrencyManager.TrySpendMoney, HeartManager.AddHearts, CurrencyManager.RefundMoney);
            if (result == HeartPurchaseResult.Success)
            {
                coinCountAnimating = false;
                RefreshCoins();
                RefreshHearts();
                var buy = SceneNode("BUY " + amount + " TIM", shopGoldViewport != null ? shopGoldViewport.transform : null);
                var art = buy != null && buy.parent != null ? buy.parent.Find("Heart Bundle Art") : null;
                PlayHeartPurchaseEffect(amount, (art != null ? art : buy) as RectTransform);
            }
            else if (result == HeartPurchaseResult.InsufficientGold)
                ShowHeartPurchaseFailure("KHÔNG ĐỦ VÀNG", amount);
            else
                ShowHeartPurchaseFailure("CHƯA THỂ MUA", amount);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            ShowHeartPurchaseFailure("CHƯA THỂ MUA", amount);
        }
        finally { heartPurchaseInProgress = false; }
    }

    private void ShowHeartPurchaseFailure(string message, int amount)
    {
        var button = SceneNode("BUY " + amount + " TIM", shopGoldViewport != null ? shopGoldViewport.transform : null);
        ShowInlineFeedback(button != null ? button.Find("Label")?.GetComponent<Text>() : null, message);
    }

    private void PlayHeartPurchaseEffect(int amount, RectTransform source)
    {
        if (amount <= 0 || homeCanvasObject == null || heartSprite == null) return;
        var energy = SceneNode("ENERGY") as RectTransform;
        if (energy == null) return;
        var target = energy.Find("Button Icon") as RectTransform;
        if (target == null) target = energy;

        EnsureCurrencyFlightOverlay();
        if (buttonEffectsOverlay == null) return;

        Canvas canvas = sceneHomeCanvas != null ? sceneHomeCanvas : homeCanvasObject.GetComponentInParent<Canvas>();
        Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera : null;
        Vector2 sourceScreen = RectTransformUtility.WorldToScreenPoint(eventCamera, (source != null ? source : energy).position);
        Vector2 targetScreen = RectTransformUtility.WorldToScreenPoint(eventCamera, target.position);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(buttonEffectsOverlay, sourceScreen, eventCamera, out Vector2 start) ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(buttonEffectsOverlay, targetScreen, eventCamera, out Vector2 end)) return;

        PlayCurrencyFlightSound();
        int count = Mathf.Clamp(amount, 5, 12);
        float size = Mathf.Clamp(buttonEffectsOverlay.rect.height * 0.043f, 44f, 60f);
        for (int i = 0; i < count; i++)
        {
            var heart = new GameObject("Purchased Heart Flight", typeof(RectTransform), typeof(Image));
            heart.transform.SetParent(buttonEffectsOverlay, false);
            var rect = heart.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.one * (size * UnityEngine.Random.Range(0.9f, 1.1f));
            var image = heart.GetComponent<Image>();
            image.sprite = heartSprite;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.maskable = false;
            var scatter = new Vector2(UnityEngine.Random.Range(-22f, 22f), UnityEngine.Random.Range(-14f, 14f));
            heart.AddComponent<MoveCoinOnUI>().StartFlight(start + scatter, end,
                UnityEngine.Random.Range(0.42f, 0.60f), i * 0.045f,
                UnityEngine.Random.Range(75f, 145f), RefreshHearts);
        }
    }
}
