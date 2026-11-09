using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Security;
using UnityEngine.SceneManagement;

public partial class IAPManager
{
    private const string BoosterJournalKey = "iap_booster_purchase_journal_v1";
    private static readonly string[] DefaultBoosterProductIds =
    {
        "hypertech.block.shoot.booster_pack_1",
        "hypertech.block.shoot.booster_pack_2",
        "hypertech.block.shoot.booster_pack_3",
        "hypertech.block.shoot.booster_pack_4",
        "hypertech.block.shoot.booster_pack_5"
    };

    [Header("Booster consumables (must match products in the store console)")]
    [SerializeField] private string[] boosterProductIds = (string[])DefaultBoosterProductIds.Clone();

    public static event Action BoosterPackPricesUpdated;
    public static event Action<int> BoosterPackPurchased;
    public static event Action<int, string> BoosterPackPurchaseFailed;
    public static event Action<int> BoosterPackPurchaseDeferred;
    public static event Action BoosterPurchaseStateChanged;
    public static bool IsBoosterPurchaseInProgress => staticScript != null && staticScript.activeBoosterPack > 0;

    private static IAPBoosterPurchaseJournal boosterJournal;
    private static readonly Queue<int> pendingBoosterPurchaseNotifications = new Queue<int>();
    private bool boosterStoreConnected;
    private bool purchasesFetchedForConnection;
    private int activeBoosterPack;
    private readonly HashSet<string> boosterGrantsInProgress = new HashSet<string>();

    private static IAPBoosterPurchaseJournal BoosterJournal => boosterJournal ?? (boosterJournal =
        new IAPBoosterPurchaseJournal(() => PlayerPrefs.GetString(BoosterJournalKey, ""), json =>
        {
            PlayerPrefs.SetString(BoosterJournalKey, json);
            PlayerPrefs.Save();
        }));

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetBoosterPurchaseState()
    {
        boosterJournal = null;
        pendingBoosterPurchaseNotifications.Clear();
        BoosterPackPricesUpdated = null;
        BoosterPackPurchased = null;
        BoosterPackPurchaseFailed = null;
        BoosterPackPurchaseDeferred = null;
        BoosterPurchaseStateChanged = null;
        SceneManager.sceneLoaded -= ResumeBoosterGrantsAfterSceneLoad;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterBoosterGrantRecovery()
    {
        SceneManager.sceneLoaded += ResumeBoosterGrantsAfterSceneLoad;
    }

    private static void ResumeBoosterGrantsAfterSceneLoad(Scene scene, LoadSceneMode mode)
    {
        // GameMaster owns the canonical wallet. Wait for its Awake before
        // recovering, so Loading scenes cannot write a separate wallet value.
        if (GameMaster.staticScript == null) return;
        try
        {
            BoosterJournal.ResumeIncomplete(() => CurrencyManager.Balance, CurrencyManager.AwardMoney,
                GrantBoosterInventory, HeartManager.ActivateUnlimitedUntil, NotifyBoosterPackPurchased);
        }
        catch (Exception exception) { Debug.LogError("IAP: Cannot resume booster rewards: " + exception.Message); }
    }

    public static string GetBoosterPackProductId(int packNumber)
    {
        int index = packNumber - 1;
        if (index < 0 || index >= DefaultBoosterProductIds.Length) return string.Empty;
        if (staticScript != null && staticScript.boosterProductIds != null &&
            index < staticScript.boosterProductIds.Length && !string.IsNullOrWhiteSpace(staticScript.boosterProductIds[index]))
            return staticScript.boosterProductIds[index].Trim();
        return DefaultBoosterProductIds[index];
    }

    public static bool IsBoosterPackAvailable(int packNumber)
    {
        if (staticScript == null || !staticScript.boosterStoreConnected || staticScript.storeController == null ||
            !HasValidBoosterProductIds()) return false;
        string productId = GetBoosterPackProductId(packNumber);
        if (string.IsNullOrEmpty(productId)) return false;
        Product product = staticScript.storeController.GetProductById(productId);
        return product != null && product.availableToPurchase;
    }

    public static string GetBoosterPackLocalizedPrice(int packNumber)
    {
        if (!IsBoosterPackAvailable(packNumber)) return string.Empty;
        Product product = staticScript.storeController.GetProductById(GetBoosterPackProductId(packNumber));
        return product.metadata != null ? product.metadata.localizedPriceString : string.Empty;
    }

    public static string GetBoosterPackCurrencyCode(int packNumber)
    {
        if (!IsBoosterPackAvailable(packNumber)) return string.Empty;
        Product product = staticScript.storeController.GetProductById(GetBoosterPackProductId(packNumber));
        return product.metadata != null ? product.metadata.isoCurrencyCode : string.Empty;
    }

    // false means that no store purchase was started. It never grants a
    // preview reward or reports success for an unavailable/cancelled purchase.
    public static bool BuyBoosterPack(int packNumber)
    {
        if (packNumber < 1 || packNumber > HomeExperienceUI.BoosterOffers.Count ||
            IsBoosterPurchaseInProgress || !IsBoosterPackAvailable(packNumber)) return false;

        IAPManager manager = staticScript;
        manager.activeBoosterPack = packNumber;
        NotifyBoosterPurchaseStateChanged();
        if (manager.waitingUI != null) manager.waitingUI.SetActive(true);
        try
        {
            manager.storeController.PurchaseProduct(GetBoosterPackProductId(packNumber));
            return true;
        }
        catch (Exception exception)
        {
            manager.FailBoosterPurchase(packNumber, "Không thể kết nối cửa hàng. Vui lòng thử lại.");
            Debug.LogError("IAP: Cannot start booster purchase: " + exception.Message);
            return false;
        }
    }

    private void AddBoosterPackProducts(List<ProductDefinition> products)
    {
        if (!HasValidBoosterProductIds())
        {
            Debug.LogError("IAP: Booster product IDs must be unique and separate from coin/remove-ads products.");
            return;
        }
        var registeredIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (ProductDefinition definition in products) registeredIds.Add(definition.id);
        for (int pack = 1; pack <= DefaultBoosterProductIds.Length; pack++)
        {
            string id = GetBoosterPackProductId(pack);
            if (!registeredIds.Add(id))
            {
                Debug.LogError("IAP: Each booster pack requires a distinct product ID: " + id);
                continue;
            }
            products.Add(new ProductDefinition(id, ProductType.Consumable));
        }
    }

    private void FetchPendingBoosterPurchases()
    {
        if (purchasesFetchedForConnection || storeController == null) return;
        purchasesFetchedForConnection = true;
        // Unity IAP routes fetched, unacknowledged transactions back through
        // OnPurchasePending. Completed consumables are never granted here.
        storeController.ProcessPendingOrdersOnPurchasesFetched(true);
        storeController.FetchPurchases();
    }

    private void OnPurchasesFetched(Orders orders) { }
    private void OnPurchasesFetchFailed(PurchasesFetchFailureDescription description)
    {
        purchasesFetchedForConnection = false;
        Debug.LogWarning("IAP: Could not fetch pending purchases: " + description);
    }

    private void OnBoosterStoreUnavailable()
    {
        boosterStoreConnected = false;
        if (activeBoosterPack > 0)
            FailBoosterPurchase(activeBoosterPack, "Mất kết nối cửa hàng. Vui lòng thử lại.");
        NotifyBoosterPackPricesUpdated();
    }

    private int GetBoosterPackNumber(Order order)
    {
        if (order == null || order.CartOrdered == null || !HasValidBoosterProductIds()) return 0;
        foreach (CartItem item in order.CartOrdered.Items())
        {
            for (int pack = 1; pack <= DefaultBoosterProductIds.Length; pack++)
                if (string.Equals(item.Product.definition.id, GetBoosterPackProductId(pack), StringComparison.Ordinal)) return pack;
        }
        return 0;
    }

    private static bool HasValidBoosterProductIds()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal) { PRODUCT_REMOVE_ADS };
        for (int pack = 1; pack <= CoinPackGoldAmounts.Length; pack++) ids.Add(GetCoinPackProductId(pack));
        for (int pack = 1; pack <= DefaultBoosterProductIds.Length; pack++)
            if (!ids.Add(GetBoosterPackProductId(pack))) return false;
        return true;
    }

    private bool TryHandleBoosterPurchase(PendingOrder order)
    {
        int packNumber = GetBoosterPackNumber(order);
        if (packNumber == 0) return false;

        string transactionId = order.Info != null ? order.Info.TransactionID : string.Empty;
        if (string.IsNullOrEmpty(transactionId))
        {
            FailBoosterPurchase(packNumber, "Chưa xác minh được giao dịch. Vui lòng thử lại sau.");
            return true;
        }
        string grantId = GetBoosterPackProductId(packNumber) + ":" + transactionId;
        if (!boosterGrantsInProgress.Add(grantId)) return true;
        try
        {
            if (BoosterJournal.IsComplete(grantId))
            {
                CompleteBoosterWaitingState(packNumber);
                ConfirmBoosterPurchase(order);
                return true;
            }

            // Only a one-item, one-unit package order is initiated by this UI.
            // Refuse malformed/mixed carts rather than acknowledge ungranted items.
            int itemCount = 0;
            foreach (CartItem item in order.CartOrdered.Items())
            {
                itemCount++;
                if (item.Quantity != 1 || !string.Equals(item.Product.definition.id,
                    GetBoosterPackProductId(packNumber), StringComparison.Ordinal))
                    throw new InvalidOperationException("Unexpected booster purchase cart.");
            }
            if (itemCount != 1 || !ValidateBoosterReceipt(order, GetBoosterPackProductId(packNumber)))
            {
                FailBoosterPurchase(packNumber, "Không thể xác minh giao dịch. Chưa cộng vật phẩm.");
                return true;
            }

            if (GameMaster.staticScript == null)
            {
                FailBoosterPurchase(packNumber, "Đang tải dữ liệu người chơi. Vui lòng mở lại cửa hàng.");
                return true;
            }

            var offer = HomeExperienceUI.BoosterOffers[packNumber - 1];
            bool granted = BoosterJournal.Fulfill(grantId, packNumber, offer.coins, offer.boosterCount,
                offer.unlimitedHeartSeconds, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), CurrencyManager.Balance,
                HeartManager.UnlimitedUntilUtc, () => CurrencyManager.Balance, CurrencyManager.AwardMoney,
                GrantBoosterInventory, HeartManager.ActivateUnlimitedUntil);

            CompleteBoosterWaitingState(packNumber);
            if (granted)
            {
                NotifyBoosterPackPurchased(packNumber);
                LogBoosterPurchase(order, packNumber);
            }
            // The store is acknowledged only after every saved reward is applied.
            ConfirmBoosterPurchase(order);
        }
        catch (Exception exception)
        {
            Debug.LogError("IAP: Booster purchase is still pending: " + exception.Message);
            FailBoosterPurchase(packNumber, "Đang xử lý giao dịch. Vật phẩm sẽ được khôi phục khi mở lại trò chơi.");
        }
        finally { boosterGrantsInProgress.Remove(grantId); }
        return true;
    }

    private void ConfirmBoosterPurchase(PendingOrder order)
    {
        try { storeController.ConfirmPurchase(order); }
        catch (Exception exception)
        {
            // Rewards are already saved. A later store replay can acknowledge
            // this receipt without changing the wallet or showing failure.
            Debug.LogWarning("IAP: Booster rewards saved; store confirmation will retry: " + exception.Message);
        }
    }

    private static bool ValidateBoosterReceipt(PendingOrder order, string productId)
    {
#if UNITY_EDITOR
        // Unity's Fake Store supplies test orders only in the editor.
        return true;
#elif UNITY_ANDROID || UNITY_IOS || UNITY_STANDALONE_OSX
        try
        {
#if UNITY_IOS || UNITY_STANDALONE_OSX
            // StoreKit 2 verifies transactions before the native purchase
            // callback. Its legacy app-receipt validator returns an empty
            // list, so match identities in the native transaction JWS here.
            // https://docs.unity.com/en-us/iap/receipt-validation
            if (!Purchasing.Utilities.StoreKitSelector.UseStoreKit1())
                return MatchStoreKit2Transaction(order, productId);
#endif
            if (order.Info == null || string.IsNullOrEmpty(order.Info.Receipt)) return false;
#if UNITY_ANDROID
            var validator = new CrossPlatformValidator(GooglePlayTangle.Data(), Application.identifier);
#else
            var validator = new CrossPlatformValidator(null, AppleTangle.Data(), Application.identifier, Application.identifier);
#endif
            foreach (IPurchaseReceipt receipt in validator.Validate(order.Info.Receipt))
            {
                if (!string.Equals(receipt.productID, productId, StringComparison.Ordinal) ||
                    !string.Equals(receipt.transactionID, order.Info.TransactionID, StringComparison.Ordinal)) continue;
                if (receipt is GooglePlayReceipt google && google.purchaseState != GooglePurchaseState.Purchased) return false;
                return true;
            }
        }
        catch (Exception exception) { Debug.LogWarning("IAP: Booster receipt validation failed: " + exception.Message); }
        return false;
#else
        Debug.LogWarning("IAP: Booster receipt verification is unavailable on this platform.");
        return false;
#endif
    }

    [Serializable]
    private sealed class AppleBoosterTransaction
    {
        public string transactionId;
        public string bundleId;
        public string productId;
        public long revocationDate;
        public int quantity;
    }

    private static bool MatchStoreKit2Transaction(PendingOrder order, string productId)
    {
        string jws = order.Info != null && order.Info.Apple != null ? order.Info.Apple.jwsRepresentation : null;
        if (string.IsNullOrEmpty(jws)) return false;
        string[] parts = jws.Split('.');
        if (parts.Length != 3) return false;
        string payload = parts[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + ((4 - payload.Length % 4) % 4), '=');
        var transaction = JsonUtility.FromJson<AppleBoosterTransaction>(
            System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
        return transaction != null && transaction.quantity == 1 && transaction.revocationDate <= 0 &&
            string.Equals(transaction.bundleId, Application.identifier, StringComparison.Ordinal) &&
            string.Equals(transaction.productId, productId, StringComparison.Ordinal) &&
            string.Equals(transaction.transactionId, order.Info.TransactionID, StringComparison.Ordinal);
    }

    private static void GrantBoosterInventory(string grantId, int count)
    {
        BoosterInventory.GrantBundleOnce(grantId, count);
    }

    private static void LogBoosterPurchase(PendingOrder order, int packNumber)
    {
        try
        {
            GameMaster.LogEventAction("purchase_success_booster_pack_" + packNumber);
            GameMaster.LogEventAction("A_Purchase_Success");
            foreach (CartItem item in order.CartOrdered.Items())
                if (item.Product.metadata != null)
                    GameMaster.LogEventPurchase(item.Product.metadata.localizedPrice, item.Product.metadata.isoCurrencyCode);
        }
        catch (Exception exception) { Debug.LogException(exception); }
    }

    private void NotifyBoosterFailed(FailedOrder order)
    {
        int pack = GetBoosterPackNumber(order);
        if (pack == 0) return;
        string message = order.FailureReason == PurchaseFailureReason.UserCancelled
            ? "Đã hủy mua gói."
            : "Mua gói chưa thành công. Vui lòng thử lại.";
        FailBoosterPurchase(pack, message);
    }

    private void NotifyBoosterDeferred(DeferredOrder order)
    {
        int pack = GetBoosterPackNumber(order);
        if (pack == 0) return;
        CompleteBoosterWaitingState(pack);
        InvokeSafely(BoosterPackPurchaseDeferred, pack);
    }

    private void FailBoosterPurchase(int pack, string reason)
    {
        CompleteBoosterWaitingState(pack);
        if (BoosterPackPurchaseFailed == null) return;
        string displayReason = Localize.Translate(reason);
        foreach (Action<int, string> listener in BoosterPackPurchaseFailed.GetInvocationList())
        {
            try { listener(pack, displayReason); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }

    private void CompleteBoosterWaitingState(int pack)
    {
        if (activeBoosterPack == pack) activeBoosterPack = 0;
        if (waitingUI != null && activeBoosterPack == 0) waitingUI.SetActive(false);
        // Replayed receipts also finish the waiting state, even though no second
        // reward/success notification is emitted for that transaction.
        NotifyBoosterPurchaseStateChanged();
    }

    private static void NotifyBoosterPurchaseStateChanged()
    {
        if (BoosterPurchaseStateChanged == null) return;
        foreach (Action listener in BoosterPurchaseStateChanged.GetInvocationList())
        {
            try { listener(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }

    public static void DeliverPendingBoosterPurchaseNotifications()
    {
        while (BoosterPackPurchased != null && pendingBoosterPurchaseNotifications.Count > 0)
            InvokeSafely(BoosterPackPurchased, pendingBoosterPurchaseNotifications.Dequeue());
    }

    private static void NotifyBoosterPackPurchased(int pack)
    {
        if (BoosterPackPurchased == null) pendingBoosterPurchaseNotifications.Enqueue(pack);
        else InvokeSafely(BoosterPackPurchased, pack);
    }

    private static void NotifyBoosterPackPricesUpdated()
    {
        if (BoosterPackPricesUpdated == null) return;
        foreach (Action listener in BoosterPackPricesUpdated.GetInvocationList())
        {
            try { listener(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }

    private static void InvokeSafely(Action<int> listeners, int pack)
    {
        if (listeners == null) return;
        foreach (Action<int> listener in listeners.GetInvocationList())
        {
            try { listener(pack); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }
}
