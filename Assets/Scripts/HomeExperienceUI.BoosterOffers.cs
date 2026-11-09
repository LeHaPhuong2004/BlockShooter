using System;
using System.Collections.Generic;

public sealed partial class HomeExperienceUI
{
    public sealed class BoosterOfferDefinition
    {
        public readonly string title;
        public readonly int coins;
        public readonly int boosterCount;
        public readonly int price;
        public readonly int oldPrice;
        public readonly int discount;
        public readonly bool unlimitedHeart;
        public readonly int unlimitedHeartSeconds;

        public BoosterOfferDefinition(string title, int coins, int boosterCount, int price,
            int oldPrice, int discount, bool unlimitedHeart, int unlimitedHeartSeconds = 0)
        {
            this.title = title;
            this.coins = coins;
            this.boosterCount = boosterCount;
            this.price = price;
            this.oldPrice = oldPrice;
            this.discount = discount;
            this.unlimitedHeart = unlimitedHeart;
            this.unlimitedHeartSeconds = unlimitedHeart ? unlimitedHeartSeconds : 0;
        }
    }

    public static IReadOnlyList<BoosterOfferDefinition> BoosterOffers { get; } = Array.AsReadOnly(new[]
    {
        new BoosterOfferDefinition("GÓI KHỞI ĐẦU", 1000, 4, 22000, 25000, 10, false),
        new BoosterOfferDefinition("GÓI NHỎ", 6000, 6, 45000, 56000, 20, false),
        new BoosterOfferDefinition("GÓI TRUNG ĐỊNH", 20000, 8, 109000, 155000, 30, true, 30 * 60),
        new BoosterOfferDefinition("GÓI LỚN", 50000, 10, 229000, 382000, 40, true, 60 * 60),
        new BoosterOfferDefinition("GÓI ĐẶC BIỆT", 100000, 15, 399000, 999000, 60, true, 120 * 60)
    });

    private Action BoosterOfferButtonAction(string buttonName)
    {
        if (buttonName == "PURCHASE_RESULT_OK" || buttonName == "PURCHASE_RESULT_CLOSE") return HideBoosterPurchasePopup;
        if (TryGetDirectBooster(buttonName, out BuffType type, out string title, out int icon))
            return () => BuyDirectBooster(type, title, icon);
        const string prefix = "BUY_BOOSTER_OFFER_";
        if (string.IsNullOrEmpty(buttonName) || !buttonName.StartsWith(prefix, StringComparison.Ordinal))
            return null;
        if (!int.TryParse(buttonName.Substring(prefix.Length), out int number) ||
            number < 1 || number > BoosterOffers.Count)
            return null;
        return () => BuyBoosterOffer(number);
    }

}
