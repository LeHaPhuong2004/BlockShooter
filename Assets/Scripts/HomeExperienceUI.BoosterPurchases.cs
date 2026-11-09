using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class HomeExperienceUI
{
    [SerializeField] private GameObject boosterPurchasePopup;
    private Text purchaseTitle;
    private Text purchaseDescription;
    private Text purchaseReward;
    private Text purchaseBalance;
    private Image purchaseRewardIcon;
    private bool directPurchaseInProgress;
    private Func<string> localizedPurchaseDescription;
    private Func<string> localizedPurchaseReward;
    private Func<string> localizedPurchaseBalance;
    private bool localizedPurchaseSuccess;
    private bool hasPurchaseResult;
    private string purchaseSourceDescription;
    private string purchaseSourceReward;
    private string purchaseSourceBalance;

    private static bool TryGetDirectBooster(string name, out BuffType type, out string title, out int icon)
    {
        type = BuffType.RevealGun;
        title = string.Empty;
        icon = 0;
        switch (name)
        {
            case "BUY KÍNH LÚP": title = "Kính lúp"; return true;
            case "BUY KÉO": type = BuffType.SplitGun; title = "Kéo"; icon = 1; return true;
            case "BUY XÁO TRỘN": type = BuffType.SuffleGridGuns; title = "Xáo trộn"; icon = 2; return true;
            case "BUY LẤY VẬT THỂ": type = BuffType.TakeBelowGun; title = "Lấy vật thể"; icon = 3; return true;
            default: return false;
        }
    }

    private void SubscribeBoosterPurchaseEvents()
    {
        IAPManager.BoosterPackPricesUpdated += RefreshBoosterShopPrices;
        IAPManager.BoosterPackPurchased += HandleBoosterPackPurchased;
        IAPManager.BoosterPackPurchaseFailed += HandleBoosterPackFailed;
        IAPManager.BoosterPackPurchaseDeferred += HandleBoosterPackDeferred;
        IAPManager.BoosterPurchaseStateChanged += RefreshBoosterShopPrices;
        IAPManager.DeliverPendingBoosterPurchaseNotifications();
    }

    private void UnsubscribeBoosterPurchaseEvents()
    {
        IAPManager.BoosterPackPricesUpdated -= RefreshBoosterShopPrices;
        IAPManager.BoosterPackPurchased -= HandleBoosterPackPurchased;
        IAPManager.BoosterPackPurchaseFailed -= HandleBoosterPackFailed;
        IAPManager.BoosterPackPurchaseDeferred -= HandleBoosterPackDeferred;
        IAPManager.BoosterPurchaseStateChanged -= RefreshBoosterShopPrices;
    }

    private void BindBoosterPurchasePopup()
    {
        if (boosterPurchasePopup == null) boosterPurchasePopup = SceneObject("Booster Purchase Popup");
        if (boosterPurchasePopup == null) return;
        Transform scope = boosterPurchasePopup.transform;
        purchaseTitle = SceneComponent<Text>("Purchase Title", scope);
        purchaseDescription = SceneComponent<Text>("Purchase Description", scope);
        purchaseReward = SceneComponent<Text>("Purchase Reward", scope);
        purchaseBalance = SceneComponent<Text>("Purchase Balance", scope);
        purchaseRewardIcon = SceneComponent<Image>("Purchase Reward Icon", scope);
    }

    private void RefreshBoosterShopPrices()
    {
        if (shopBoosterViewport == null) return;
        foreach (var button in shopBoosterViewport.GetComponentsInChildren<Button>(true))
        {
            var label = button.transform.Find("Label")?.GetComponent<Text>();
            if (label == null) continue;
            if (TryGetDirectBooster(button.name, out _, out _, out _))
            {
                label.text = FormatPurchasePrice(BoosterPurchaseTransaction.UnitPrice);
                continue;
            }
            const string prefix = "BUY_BOOSTER_OFFER_";
            if (!button.name.StartsWith(prefix, StringComparison.Ordinal) ||
                !int.TryParse(button.name.Substring(prefix.Length), out int number) ||
                number < 1 || number > BoosterOffers.Count) continue;
            var offer = BoosterOffers[number - 1];
            string price = NumericPriceText(IAPManager.GetBoosterPackLocalizedPrice(number));
            label.text = string.IsNullOrWhiteSpace(price) ? FormatPurchasePrice(offer.price) : price;
            button.interactable = !IAPManager.IsBoosterPurchaseInProgress;
            Transform row = button.transform.parent;
            var title = row.Find("Offer Title")?.GetComponent<Text>();
            if (title != null) title.text = Localize.Translate(offer.title);
            var coins = row.Find("Coin Amount")?.GetComponent<Text>();
            if (coins != null) coins.text = Localize.Format("{0} VÀNG", "{0} GOLD", FormatPurchasePrice(offer.coins));
            var oldPrice = row.Find("Old Price");
            string currency = IAPManager.GetBoosterPackCurrencyCode(number);
            if (oldPrice != null)
            {
                var oldPriceLabel = oldPrice.Find("Old Price Label")?.GetComponent<Text>();
                if (oldPriceLabel != null) oldPriceLabel.text = FormatPurchasePrice(offer.oldPrice);
                oldPrice.gameObject.SetActive(string.IsNullOrEmpty(currency) || currency.Equals("VND", StringComparison.OrdinalIgnoreCase));
            }
            var duration = row.Find("Unlimited Heart Caption")?.GetComponent<Text>();
            if (duration != null) duration.text = FormatUnlimitedDuration(offer.unlimitedHeartSeconds);
        }
    }

    private static string FormatPurchasePrice(int amount) =>
        amount.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));

    private static string NumericPriceText(string price)
    {
        if (string.IsNullOrWhiteSpace(price)) return string.Empty;
        var number = new System.Text.StringBuilder(price.Length);
        foreach (char character in price)
            if (char.IsDigit(character) || character == '.' || character == ',' || char.IsWhiteSpace(character))
                number.Append(character);
        return number.ToString().Trim();
    }

    private static string FormatUnlimitedDuration(int seconds)
    {
        int amount = seconds >= 3600 ? seconds / 3600 : seconds / 60;
        return seconds >= 3600
            ? Localize.Format("{0} GIỜ", amount == 1 ? "{0} HOUR" : "{0} HOURS", amount)
            : Localize.Format("{0} PHÚT", amount == 1 ? "{0} MINUTE" : "{0} MINUTES", amount);
    }

    private void BuyDirectBooster(BuffType type, string title, int iconIndex)
    {
        if (directPurchaseInProgress || (boosterPurchasePopup != null && boosterPurchasePopup.activeSelf)) return;
        directPurchaseInProgress = true;
        try
        {
            BoosterPurchaseResult result = BoosterPurchaseTransaction.TryBuy(type, BoosterPurchaseTransaction.UnitPrice,
                () => CurrencyManager.Balance, CurrencyManager.TrySpendMoney, BoosterInventory.AddAmount, CurrencyManager.AwardMoney);
            if (result == BoosterPurchaseResult.Success)
            {
                coinCountAnimating = false;
                RefreshCoins();
                int balance = CurrencyManager.Balance;
                ShowLocalizedBoosterPurchaseResult(true, () => Localize.Translate(title) + " ×1", () => "+1 BOOSTER",
                    () => Localize.Format("Số dư: {0} vàng", "Balance: {0} gold", FormatPurchasePrice(balance)), BoosterIcon(iconIndex));
            }
            else if (result == BoosterPurchaseResult.InsufficientGold)
            {
                int balance = CurrencyManager.Balance;
                ShowLocalizedBoosterPurchaseResult(false,
                    () => Localize.Translate("Bạn cần 200 vàng để mua " + title.ToLowerInvariant() + "."),
                    () => Localize.Translate("KHÔNG ĐỦ VÀNG"),
                    () => Localize.Format("Số dư: {0} vàng", "Balance: {0} gold", balance), coinSprite);
            }
            else
                ShowLocalizedBoosterPurchaseResult(false,
                    () => Localize.Translate("Chưa lưu được vật phẩm. Vàng đã được hoàn lại."),
                    () => Localize.Translate("VUI LÒNG THỬ LẠI"), () => string.Empty, BoosterIcon(iconIndex));
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            ShowLocalizedBoosterPurchaseResult(false,
                () => Localize.Translate("Chưa thể hoàn tất giao dịch. Vui lòng thử lại sau."),
                () => Localize.Translate("CHƯA THỂ MUA"), () => string.Empty, BoosterIcon(iconIndex));
        }
        finally { directPurchaseInProgress = false; }
    }

    private Sprite BoosterIcon(int index) => shopBoosterSprites != null && index >= 0 && index < shopBoosterSprites.Length
        ? shopBoosterSprites[index] : null;

    private void BuyBoosterOffer(int packNumber)
    {
        if (IAPManager.IsBoosterPurchaseInProgress) return;
        if (!IAPManager.BuyBoosterPack(packNumber))
            ShowLocalizedBoosterPurchaseResult(false,
                () => Localize.Translate("Cửa hàng chưa sẵn sàng. Vui lòng thử lại sau."),
                () => Localize.Translate("CHƯA THỂ MUA"), () => string.Empty, shopBannerGoodsSprite);
        RefreshBoosterShopPrices();
    }

    private void HandleBoosterPackPurchased(int packNumber)
    {
        if (packNumber < 1 || packNumber > BoosterOffers.Count) return;
        var offer = BoosterOffers[packNumber - 1];
        RefreshBoosterShopPrices();
        RefreshHearts();
        ShowLocalizedBoosterPurchaseResult(true, () =>
        {
            string description = Localize.Translate(offer.title) + "\n" + Localize.Translate("Kính lúp") + ", "
                + Localize.Translate("Kéo") + ", " + Localize.Translate("Xáo trộn") + ", "
                + Localize.Translate("Lấy vật thể") + " ×" + offer.boosterCount;
            if (offer.unlimitedHeart)
                description += "\n" + Localize.Text("Tim vô hạn: ", "Unlimited hearts: ")
                    + FormatUnlimitedDuration(offer.unlimitedHeartSeconds).ToLowerInvariant();
            return description;
        }, () => Localize.Format("+{0} VÀNG", "+{0} GOLD", FormatPurchasePrice(offer.coins)),
            () => Localize.Translate("Vật phẩm đã được thêm vào kho"), shopBannerGoodsSprite);
    }

    private void HandleBoosterPackFailed(int packNumber, string message)
    {
        RefreshBoosterShopPrices();
        ShowLocalizedBoosterPurchaseResult(false,
            () => Localize.Translate(string.IsNullOrWhiteSpace(message) ? "Giao dịch chưa hoàn tất." : message),
            () => Localize.Translate("CHƯA THỂ MUA"), () => string.Empty, shopBannerGoodsSprite);
    }

    private void HandleBoosterPackDeferred(int packNumber)
    {
        RefreshBoosterShopPrices();
        ShowLocalizedBoosterPurchaseResult(false,
            () => Localize.Translate("Giao dịch đang chờ phê duyệt. Vật phẩm sẽ được nhận sau khi thanh toán hoàn tất."),
            () => Localize.Translate("ĐANG CHỜ DUYỆT"), () => string.Empty, shopBannerGoodsSprite);
    }

    private void ShowLocalizedBoosterPurchaseResult(bool success, Func<string> description, Func<string> reward, Func<string> balance, Sprite icon)
    {
        ShowBoosterPurchaseResult(success, description(), reward(), balance(), icon);
        localizedPurchaseSuccess = success;
        localizedPurchaseDescription = description;
        localizedPurchaseReward = reward;
        localizedPurchaseBalance = balance;
    }

    private void RefreshLocalizedPurchaseResult()
    {
        if (boosterPurchasePopup == null || !boosterPurchasePopup.activeSelf || !hasPurchaseResult) return;
        string description = localizedPurchaseDescription != null ? localizedPurchaseDescription() : TranslatePurchaseText(purchaseSourceDescription ?? purchaseDescription?.text);
        string reward = localizedPurchaseReward != null ? localizedPurchaseReward() : TranslatePurchaseText(purchaseSourceReward ?? purchaseReward?.text);
        string balance = localizedPurchaseBalance != null ? localizedPurchaseBalance() : TranslatePurchaseText(purchaseSourceBalance ?? purchaseBalance?.text);
        if (purchaseTitle != null) purchaseTitle.text = localizedPurchaseSuccess
            ? Localize.Translate("MUA THÀNH CÔNG") : reward;
        if (purchaseDescription != null) purchaseDescription.text = description;
        if (purchaseReward != null) purchaseReward.text = localizedPurchaseSuccess ? reward : string.Empty;
        if (purchaseBalance != null) purchaseBalance.text = balance;
    }

    private static string TranslatePurchaseText(string source)
    {
        if (string.IsNullOrEmpty(source)) return string.Empty;
        string translated = Localize.Translate(source);
        if (translated != source) return translated;
        const string vietnameseBalance = "Số dư: ";
        const string englishBalance = "Balance: ";
        string amount = source.StartsWith(vietnameseBalance, StringComparison.Ordinal) ? source.Substring(vietnameseBalance.Length)
            : source.StartsWith(englishBalance, StringComparison.Ordinal) ? source.Substring(englishBalance.Length) : null;
        if (amount != null)
        {
            if (amount.EndsWith(" vàng", StringComparison.Ordinal)) amount = amount.Substring(0, amount.Length - 5);
            else if (amount.EndsWith(" gold", StringComparison.Ordinal)) amount = amount.Substring(0, amount.Length - 5);
            return Localize.Format("Số dư: {0} vàng", "Balance: {0} gold", amount);
        }
        return source;
    }

    private void ShowBoosterPurchaseResult(bool success, string description, string reward, string balance, Sprite icon)
    {
        BindBoosterPurchasePopup();
        if (boosterPurchasePopup == null) throw new InvalidOperationException("The authored Booster Purchase Popup is missing.");
        localizedPurchaseDescription = null;
        localizedPurchaseReward = null;
        localizedPurchaseBalance = null;
        localizedPurchaseSuccess = success;
        hasPurchaseResult = true;
        purchaseSourceDescription = description;
        purchaseSourceReward = reward;
        purchaseSourceBalance = balance;
        CloseOutOfHearts();
        purchaseTitle.text = success ? Localize.Translate("MUA THÀNH CÔNG") : TranslatePurchaseText(reward);
        purchaseTitle.color = success ? new Color32(255, 234, 109, 255) : Color.white;
        purchaseDescription.text = TranslatePurchaseText(description);
        purchaseReward.text = success ? TranslatePurchaseText(reward) : "";
        purchaseBalance.text = TranslatePurchaseText(balance);
        purchaseRewardIcon.sprite = icon;
        purchaseRewardIcon.enabled = icon != null;
        boosterPurchasePopup.transform.SetAsLastSibling();
        boosterPurchasePopup.SetActive(true);
        BringCurrencyFlightOverlayToFront();
    }

    private void HideBoosterPurchasePopup()
    {
        if (boosterPurchasePopup != null) boosterPurchasePopup.SetActive(false);
        RefreshBoosterShopPrices();
    }

#if UNITY_EDITOR
    public void PrepareBoosterPurchasesForEditor()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Prepare Booster purchases outside Play Mode.");
        LoadHomeResources();
        sceneHomeCanvas = SceneComponent<Canvas>("Home Canvas", transform);
        if (sceneHomeCanvas == null) throw new InvalidOperationException("The authored Home Canvas is missing.");
        homeCanvasObject = sceneHomeCanvas.gameObject;
        shopBoosterViewport = SceneObject("Booster Shop Viewport");
        BindBoosterPurchasePopup();
        if (boosterPurchasePopup == null) BuildBoosterPurchasePopupForEditor();
        BindBoosterPurchasePopup();
        RefreshBoosterShopPrices();
        foreach (var text in shopBoosterViewport.GetComponentsInChildren<Text>(true))
        {
            if (text.name == "Unlimited Heart Caption")
            {
                text.GetComponent<HomeTextFit>()?.SetMaximumFontSize(23, true);
                continue;
            }
        }
        boosterPurchasePopup.SetActive(false);
    }

    private void BuildBoosterPurchasePopupForEditor()
    {
        boosterPurchasePopup = new GameObject("Booster Purchase Popup", typeof(RectTransform), typeof(Image));
        boosterPurchasePopup.transform.SetParent(homeCanvasObject.transform, false);
        Stretch(boosterPurchasePopup.GetComponent<RectTransform>());
        var dimmer = boosterPurchasePopup.GetComponent<Image>();
        dimmer.color = new Color32(7, 2, 28, 215);
        dimmer.raycastTarget = true;
        Sprite frame = Resources.Load<Sprite>("UI/ShopItems/ShopCardFrame");
        var window = PurchaseImage(boosterPurchasePopup.transform, "Booster Purchase Window", frame,
            new Vector2(0.08f, 0.28f), new Vector2(0.92f, 0.72f));
        window.type = frame != null && frame.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
        window.preserveAspect = false;
        window.color = new Color32(65, 28, 153, 255);
        var border = window.gameObject.AddComponent<Outline>();
        border.effectColor = new Color32(205, 162, 255, 255);
        border.effectDistance = new Vector2(3f, -3f);
        PurchaseText(window.transform, "Purchase Title", "MUA THÀNH CÔNG", 45,
            new Vector2(0.05f, 0.82f), new Vector2(0.95f, 0.95f), new Color32(255, 234, 109, 255));
        PurchaseImage(window.transform, "Purchase Reward Icon", BoosterIcon(0),
            new Vector2(0.32f, 0.52f), new Vector2(0.68f, 0.80f));
        PurchaseText(window.transform, "Purchase Description", "Kính lúp ×1", 30,
            new Vector2(0.06f, 0.35f), new Vector2(0.94f, 0.51f), Color.white, false);
        PurchaseText(window.transform, "Purchase Reward", "+1 BOOSTER", 33,
            new Vector2(0.06f, 0.265f), new Vector2(0.94f, 0.345f), new Color32(255, 227, 99, 255));
        PurchaseText(window.transform, "Purchase Balance", "Đã nhận vào kho của bạn", 24,
            new Vector2(0.06f, 0.19f), new Vector2(0.94f, 0.255f), new Color32(227, 216, 255, 255));
        PurchaseButton(window.transform, "PURCHASE_RESULT_OK", "ĐỒNG Ý", shopBuyFaceSprite,
            new Vector2(0.20f, 0.045f), new Vector2(0.80f, 0.17f), 34);
        var closeImage = SceneComponent<Image>("Settings Close Button");
        PurchaseButton(window.transform, "PURCHASE_RESULT_CLOSE", "×", closeImage != null ? closeImage.sprite : shopTabIdleSprite,
            new Vector2(0.89f, 0.90f), new Vector2(1.025f, 1.045f), 42);
        boosterPurchasePopup.SetActive(false);
    }

    private Image PurchaseImage(Transform parent, string name, Sprite sprite, Vector2 min, Vector2 max)
    {
        var child = new GameObject(name, typeof(RectTransform), typeof(Image));
        child.transform.SetParent(parent, false);
        SetShopRect(child.GetComponent<RectTransform>(), min, max);
        var image = child.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private Text PurchaseText(Transform parent, string name, string value, int size, Vector2 min, Vector2 max,
        Color color, bool singleLine = true)
    {
        var label = Label(parent, value, size, (min + max) * 0.5f, max - min, color, FontStyle.Bold);
        label.name = name;
        label.raycastTarget = false;
        label.alignment = TextAnchor.MiddleCenter;
        var outline = label.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color32(30, 8, 89, 255);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        HomeUIFont.ConfigureText(label, font, singleLine);
        label.GetComponent<HomeTextFit>().SetMaximumFontSize(size, singleLine);
        return label;
    }

    private void PurchaseButton(Transform parent, string name, string value, Sprite sprite, Vector2 min, Vector2 max, int size)
    {
        var image = PurchaseImage(parent, name, sprite, min, max);
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        PurchaseText(image.transform, "Label", value, size, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.92f), Color.white);
    }
#endif
}
