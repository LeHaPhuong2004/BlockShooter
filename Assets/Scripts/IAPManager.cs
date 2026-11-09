using System;
using System.Collections.Generic;
//using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Security;
using Unity.Services.Core;
using Unity.Services.Core.Environments;

using UnityEngine.Purchasing.Extension;

[DefaultExecutionOrder(100)]
public partial class IAPManager : MonoBehaviour
{
    public static IAPManager staticScript;
    public static event Action CoinPackPricesUpdated;

    public static string PRODUCT_REMOVE_ADS = "hypertech.block.shoot.remove.ads";

    //for package coin
    public static string PRODUCT_COIN_PACK_1 = "hypertech.block.shoot.coin_pack_1"; // 0.99 usd - 500 coin
    public static string PRODUCT_COIN_PACK_2 = "hypertech.block.shoot.coin_pack_2"; // 1.99 usd - 1200 coin
    public static string PRODUCT_COIN_PACK_3 = "hypertech.block.shoot.coin_pack_3"; // 3.99 usd - 2500 coin

    public static string PRODUCT_COIN_PACK_4 = "hypertech.block.shoot.coin_pack_4"; // 7.99 usd - 6500 coin
    public static string PRODUCT_COIN_PACK_5 = "hypertech.block.shoot.coin_pack_5"; // 14.99 usd - 14000 coin
    public static string PRODUCT_COIN_PACK_6 = "hypertech.block.shoot.coin_pack_6"; // 29.99 usd - 30000 coin

    private static readonly int[] CoinPackGoldAmounts = { 500, 1200, 2500, 6500, 14000, 30000 };

    // Sử dụng StoreController chuẩn v5 (Không dùng interface IStoreController nữa)
    private StoreController storeController;
    //private IExtensionProvider storeExtensionProvider; // Vẫn cần để Restore trên iOS

    public GameObject waitingUI;
    public GameObject btnRemoveAds;

    private string environment = "production";

    // Dùng HashSet để lưu các Transaction ID đã được xử lý, chống cộng trùng lặp
    private static HashSet<string> processedTransactions = new HashSet<string>();

    void Awake()
    {
        // Let GameMaster remove duplicate persistent roots first, then prefer
        // its IAP component so a scene-local copy cannot own the shared store.
        var persistentOwner = GameMaster.staticScript != null
            ? GameMaster.staticScript.GetComponentInChildren<IAPManager>(true) : null;
        if (persistentOwner != null && persistentOwner != this && persistentOwner.enabled &&
            persistentOwner.gameObject.activeInHierarchy)
        {
            enabled = false;
            return;
        }
        if (staticScript != null && staticScript != this)
        {
            // Several legacy scene objects can contain this component, but Unity
            // IAP shares its purchase service. Only one may subscribe and fulfill.
            enabled = false;
            return;
        }
        staticScript = this;
    }

    async void Start()
    {
        if (staticScript != this) return;
        try
        {
            var options = new InitializationOptions().SetEnvironmentName(environment);

            // 1. Khởi tạo Unity Services
            await UnityServices.InitializeAsync(options);
            if (this == null || staticScript != this) return;

            if (storeController == null)
            {
                InitializePurchasingAsync();
            }
        }
        catch (Exception exception)
        {
            Debug.LogError($"IAP: Error create Unity Services - {exception.Message}");
        }
    }

    public static void SetWaitingUI(GameObject wait)
    {
        if (staticScript != null) staticScript.waitingUI = wait;
    }

    public static void SetBtnRemoveAds(GameObject btn)
    {
        if (staticScript != null) staticScript.btnRemoveAds = btn;
    }


    private async void InitializePurchasingAsync()
    {
        if (staticScript != this || storeController != null) return;

        try
        {
            storeController = UnityIAPServices.StoreController();

            storeController.OnStoreConnected += OnStoreConnected;
            storeController.OnStoreDisconnected += OnStoreDisconnected;
            storeController.OnProductsFetched += OnProductsFetched;
            storeController.OnProductsFetchFailed += OnProductsFetchFailed;
            storeController.OnPurchasePending += OnPurchasePending;
            storeController.OnPurchaseFailed += OnPurchaseFailed;
            storeController.OnPurchaseConfirmed += OnPurchaseConfirmed;
            storeController.OnPurchaseDeferred += OnPurchaseDeferred;
            storeController.OnPurchasesFetched += OnPurchasesFetched;
            storeController.OnPurchasesFetchFailed += OnPurchasesFetchFailed;

            Debug.Log("IAP: Connecting to Store...");
            await storeController.Connect();
        }
        catch (Exception e)
        {
            Debug.LogError($"IAP: Error initializing IAP - {e}");
            storeController = null;
            if (waitingUI != null) waitingUI.SetActive(false);
        }
    }

    private bool IsInitialized()
    {
        return storeController != null;
    }

    #region Connection & Fetch Data (replace old OnInitialized)

    private void OnStoreConnected()
    {
        boosterStoreConnected = true;
        purchasesFetchedForConnection = false;
        Debug.Log("IAP: Connect Store success!");

        var products = new List<ProductDefinition>
        {
            new ProductDefinition(PRODUCT_REMOVE_ADS, ProductType.NonConsumable),
            new ProductDefinition(PRODUCT_COIN_PACK_1, ProductType.Consumable),
            new ProductDefinition(PRODUCT_COIN_PACK_2, ProductType.Consumable),
            new ProductDefinition(PRODUCT_COIN_PACK_3, ProductType.Consumable),
            new ProductDefinition(PRODUCT_COIN_PACK_4, ProductType.Consumable),
            new ProductDefinition(PRODUCT_COIN_PACK_5, ProductType.Consumable),
            new ProductDefinition(PRODUCT_COIN_PACK_6, ProductType.Consumable),
        };

        AddBoosterPackProducts(products);
        storeController.FetchProducts(products);
    }

    private void OnStoreDisconnected(StoreConnectionFailureDescription description)
    {
        OnBoosterStoreUnavailable();
        Debug.LogWarning($"IAP: Disconnect to Store. Error detail: {description}");
    }

    private void OnProductsFetched(List<Product> products)
    {
        Debug.Log("IAP: Get product infor success!");

        UpdatePriceInfoFromStore(products);
        UpdateShopPrices();
        CoinPackPricesUpdated?.Invoke();
        NotifyBoosterPackPricesUpdated();
        FetchPendingBoosterPurchases();
    }

    private void OnProductsFetchFailed(ProductFetchFailed failureDescription)
    {
        Debug.LogError($"IAP: Error when get infor : {failureDescription}");
        if (waitingUI != null) waitingUI.SetActive(false);
        NotifyBoosterPackPricesUpdated();
    }

    private void UpdatePriceInfoFromStore(List<Product> products)
    {
        Product product = products.Find(p => string.Equals(p.definition.id, PRODUCT_REMOVE_ADS, StringComparison.Ordinal));

        if (product != null)
        {
            string price = product.metadata.localizedPriceString;
            if (!string.IsNullOrEmpty(price) && !price.Equals(GameMaster.IapPriceRemoveAds))
            {
                GameMaster.IapPriceRemoveAds = price;
            }
        }

        Product productPack1 = products.Find(p => string.Equals(p.definition.id, PRODUCT_COIN_PACK_1, StringComparison.Ordinal));

        if (productPack1 != null)
        {
            string price = productPack1.metadata.localizedPriceString;
            if (!string.IsNullOrEmpty(price) && !price.Equals(GameMaster.IapPricePack1))
            {
                GameMaster.IapPricePack1 = price;
            }
        }

        Product productPack2 = products.Find(p => string.Equals(p.definition.id, PRODUCT_COIN_PACK_2, StringComparison.Ordinal));

        if (productPack2 != null)
        {
            string price = productPack2.metadata.localizedPriceString;
            if (!string.IsNullOrEmpty(price) && !price.Equals(GameMaster.IapPricePack2))
            {
                GameMaster.IapPricePack2 = price;
            }
        }

        Product productPack3 = products.Find(p => string.Equals(p.definition.id, PRODUCT_COIN_PACK_3, StringComparison.Ordinal));

        if (productPack3 != null)
        {
            string price = productPack3.metadata.localizedPriceString;
            if (!string.IsNullOrEmpty(price) && !price.Equals(GameMaster.IapPricePack3))
            {
                GameMaster.IapPricePack3 = price;
            }
        }

        Product productPack4 = products.Find(p => string.Equals(p.definition.id, PRODUCT_COIN_PACK_4, StringComparison.Ordinal));

        if (productPack4 != null)
        {
            string price = productPack4.metadata.localizedPriceString;
            if (!string.IsNullOrEmpty(price) && !price.Equals(GameMaster.IapPricePack4))
            {
                GameMaster.IapPricePack4 = price;
            }
        }

        Product productPack5 = products.Find(p => string.Equals(p.definition.id, PRODUCT_COIN_PACK_5, StringComparison.Ordinal));

        if (productPack5 != null)
        {
            string price = productPack5.metadata.localizedPriceString;
            if (!string.IsNullOrEmpty(price) && !price.Equals(GameMaster.IapPricePack5))
            {
                GameMaster.IapPricePack5 = price;
            }
        }

        Product productPack6 = products.Find(p => string.Equals(p.definition.id, PRODUCT_COIN_PACK_6, StringComparison.Ordinal));

        if (productPack6 != null)
        {
            string price = productPack6.metadata.localizedPriceString;
            if (!string.IsNullOrEmpty(price) && !price.Equals(GameMaster.IapPricePack6))
            {
                GameMaster.IapPricePack6 = price;
            }
        }
    }

    #endregion

    #region Purchase Flow

    public static void BuyNoADS()
    {
        GameMaster.PlaySoundClick();
        if (staticScript.waitingUI != null) staticScript.waitingUI.SetActive(true);

        staticScript.BuyProductID(PRODUCT_REMOVE_ADS);
    }

    public static void BuyCoinPack1Gold() => BuyCoinPack(1);
    public static void BuyCoinPack2Gold() => BuyCoinPack(2);
    public static void BuyCoinPack3Gold() => BuyCoinPack(3);
    public static void BuyCoinPack4Gold() => BuyCoinPack(4);
    public static void BuyCoinPack5Gold() => BuyCoinPack(5);
    public static void BuyCoinPack6Gold() => BuyCoinPack(6);

    public static void BuyCoinPack(int packNumber)
    {
        if (staticScript == null)
        {
            Debug.LogError("IAP purchase failed: IAPManager is missing from the active scene.");
            return;
        }

        string productId = GetCoinPackProductId(packNumber);
        if (string.IsNullOrEmpty(productId))
        {
            Debug.LogError($"IAP purchase failed: invalid coin pack number {packNumber}.");
            return;
        }

        if (staticScript.waitingUI != null) staticScript.waitingUI.SetActive(true);
        staticScript.BuyProductID(productId);
    }

    public static int GetCoinPackGoldAmount(int packNumber)
    {
        int index = packNumber - 1;
        return index >= 0 && index < CoinPackGoldAmounts.Length ? CoinPackGoldAmounts[index] : 0;
    }

    public static string GetCoinPackLocalizedPrice(int packNumber)
    {
        if (GameMaster.staticScript == null) return string.Empty;

        switch (packNumber)
        {
            case 1: return GameMaster.IapPricePack1;
            case 2: return GameMaster.IapPricePack2;
            case 3: return GameMaster.IapPricePack3;
            case 4: return GameMaster.IapPricePack4;
            case 5: return GameMaster.IapPricePack5;
            case 6: return GameMaster.IapPricePack6;
            default: return string.Empty;
        }
    }

    private static string GetCoinPackProductId(int packNumber)
    {
        switch (packNumber)
        {
            case 1: return PRODUCT_COIN_PACK_1;
            case 2: return PRODUCT_COIN_PACK_2;
            case 3: return PRODUCT_COIN_PACK_3;
            case 4: return PRODUCT_COIN_PACK_4;
            case 5: return PRODUCT_COIN_PACK_5;
            case 6: return PRODUCT_COIN_PACK_6;
            default: return null;
        }
    }

    private static void GrantCoinPackWithoutHomeController(int packNumber)
    {
        int amount = GetCoinPackGoldAmount(packNumber);
        if (amount <= 0) return;

        CurrencyManager.AwardMoney(amount);
    }

    private void BuyProductID(string productId)
    {
        if (IsInitialized())
        {
            Debug.Log($"IAP: Start purchase - {productId}");
            try
            {
                storeController.PurchaseProduct(productId);
            }
            catch (Exception e)
            {
                Debug.LogError($"IAP: Error when call PurchaseProduct: {e.Message}");
                if (waitingUI != null) waitingUI.SetActive(false);
            }
        }
        else
        {
            Debug.LogError("BuyProductID FAIL. IAP Not initialized.");
            if (waitingUI != null) waitingUI.SetActive(false);
        }
    }

    // Thay thế hàm ProcessPurchase cũ với cơ chế chặn trùng lặp Transaction ID
    private void OnPurchasePending(PendingOrder pendingOrder)
    {
        if (TryHandleBoosterPurchase(pendingOrder)) return;
        Debug.Log("IAP: OnPurchasePending - verify transaction");

        // Lấy Transaction ID để kiểm tra chống gọi lặp
        string transactionId = pendingOrder.Info != null ? pendingOrder.Info.TransactionID : string.Empty;

        if (!string.IsNullOrEmpty(transactionId) && processedTransactions.Contains(transactionId))
        {
            Debug.LogWarning($"IAP: Transaction {transactionId} already processed. Skipping duplicate call.");
            if (waitingUI != null) waitingUI.SetActive(false);
            storeController.ConfirmPurchase(pendingOrder);
            return;
        }

        bool validPurchase = true;
        string receipt = pendingOrder.Info.Receipt;

#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS || UNITY_STANDALONE_OSX)
        var validator = new CrossPlatformValidator(GooglePlayTangle.Data(), AppleTangle.Data(), Application.identifier);

        try
        {
            if (!string.IsNullOrEmpty(receipt))
            {
                var result = validator.Validate(receipt);
                Debug.Log("IAP: Receipt is valid.");
            }
            else
            {
                validPurchase = false;
                Debug.LogWarning("IAP: Receipt is null or empty.");
            }
        }
        catch (IAPSecurityException)
        {
            Debug.LogWarning("IAP: Invalid receipt, not unlocking content");
            validPurchase = false;
        }
#endif

        if (validPurchase)
        {
            // Đánh dấu Transaction đã xử lý NGAY TRƯỚC KHI cộng thưởng
            if (!string.IsNullOrEmpty(transactionId))
            {
                processedTransactions.Add(transactionId);
            }

            foreach (CartItem item in pendingOrder.CartOrdered.Items())
            {
                if (string.Equals(item.Product.definition.id, PRODUCT_REMOVE_ADS, StringComparison.Ordinal))
                {
                    GameMaster.HaveNoAds = 1;
                    GameMaster.RemoveAllAds();

                    if (HomeController.staticScript != null) HomeController.staticScript.OnPurchaseRemoveAdsSuccess();

                    if (btnRemoveAds != null) btnRemoveAds.SetActive(false);

                    GameMaster.LogEventAction("A_Purchase_Success");

                    decimal price = (decimal)item.Product.metadata.localizedPrice;
                    string currency = item.Product.metadata.isoCurrencyCode;

                    GameMaster.LogEventPurchase(price, currency);

                    Debug.Log("IAP: Purchase Remove Ads Success!");
                }
                else if (string.Equals(item.Product.definition.id, PRODUCT_COIN_PACK_1, StringComparison.Ordinal))
                {
                    if (HomeController.staticScript != null) HomeController.staticScript.OnPurchasePack1Success();
                    else GrantCoinPackWithoutHomeController(1);
                    GameMaster.LogEventAction("purchase_success_coin_pack_1");
                    GameMaster.LogEventAction("A_Purchase_Success");

                    decimal price = (decimal)item.Product.metadata.localizedPrice;
                    string currency = item.Product.metadata.isoCurrencyCode;

                    GameMaster.LogEventPurchase(price, currency);
                }
                else if (string.Equals(item.Product.definition.id, PRODUCT_COIN_PACK_2, StringComparison.Ordinal))
                {
                    if (HomeController.staticScript != null) HomeController.staticScript.OnPurchasePack2Success();
                    else GrantCoinPackWithoutHomeController(2);
                    GameMaster.LogEventAction("purchase_success_coin_pack_2");
                    GameMaster.LogEventAction("A_Purchase_Success");

                    decimal price = (decimal)item.Product.metadata.localizedPrice;
                    string currency = item.Product.metadata.isoCurrencyCode;

                    GameMaster.LogEventPurchase(price, currency);
                }
                else if (string.Equals(item.Product.definition.id, PRODUCT_COIN_PACK_3, StringComparison.Ordinal))
                {
                    if (HomeController.staticScript != null) HomeController.staticScript.OnPurchasePack3Success();
                    else GrantCoinPackWithoutHomeController(3);
                    GameMaster.LogEventAction("purchase_success_coin_pack_3");
                    GameMaster.LogEventAction("A_Purchase_Success");

                    decimal price = (decimal)item.Product.metadata.localizedPrice;
                    string currency = item.Product.metadata.isoCurrencyCode;

                    GameMaster.LogEventPurchase(price, currency);
                }
                else if (string.Equals(item.Product.definition.id, PRODUCT_COIN_PACK_4, StringComparison.Ordinal))
                {
                    if (HomeController.staticScript != null) HomeController.staticScript.OnPurchasePack4Success();
                    else GrantCoinPackWithoutHomeController(4);
                    GameMaster.LogEventAction("purchase_success_coin_pack_4");
                    GameMaster.LogEventAction("A_Purchase_Success");

                    decimal price = (decimal)item.Product.metadata.localizedPrice;
                    string currency = item.Product.metadata.isoCurrencyCode;

                    GameMaster.LogEventPurchase(price, currency);
                }
                else if (string.Equals(item.Product.definition.id, PRODUCT_COIN_PACK_5, StringComparison.Ordinal))
                {
                    if (HomeController.staticScript != null) HomeController.staticScript.OnPurchasePack5Success();
                    else GrantCoinPackWithoutHomeController(5);
                    GameMaster.LogEventAction("purchase_success_coin_pack_5");
                    GameMaster.LogEventAction("A_Purchase_Success");

                    decimal price = (decimal)item.Product.metadata.localizedPrice;
                    string currency = item.Product.metadata.isoCurrencyCode;

                    GameMaster.LogEventPurchase(price, currency);
                }
                else if (string.Equals(item.Product.definition.id, PRODUCT_COIN_PACK_6, StringComparison.Ordinal))
                {
                    if (HomeController.staticScript != null) HomeController.staticScript.OnPurchasePack6Success();
                    else GrantCoinPackWithoutHomeController(6);
                    GameMaster.LogEventAction("purchase_success_coin_pack_6");
                    GameMaster.LogEventAction("A_Purchase_Success");

                    decimal price = (decimal)item.Product.metadata.localizedPrice;
                    string currency = item.Product.metadata.isoCurrencyCode;

                    GameMaster.LogEventPurchase(price, currency);
                }
            }
        }
        else
        {
            Debug.LogError("IAP: Purchase is invalid");
            GameMaster.LogEventAction("A_Purchase_Invalid");
        }

        if (waitingUI != null) waitingUI.SetActive(false);

        storeController.ConfirmPurchase(pendingOrder);
    }

    private void OnPurchaseConfirmed(Order order)
    {
        if (order is ConfirmedOrder confirmedOrder)
        {
            Debug.Log("IAP: Purchase confirm success");
            GameMaster.LogEventAction("A_Purchase_Confirm_success");
        }
        else if (order is FailedOrder failedOrder)
        {
            Debug.Log("IAP: Purchase confirm failed");
            GameMaster.LogEventAction("A_Purchase_Confirm_failed");
        }
    }

    private void OnPurchaseDeferred(DeferredOrder deferredOrder)
    {
        NotifyBoosterDeferred(deferredOrder);
        Debug.Log("IAP: Purchase is deferred");
        if (waitingUI != null) waitingUI.SetActive(false);
    }

    private void OnPurchaseFailed(FailedOrder failedOrder)
    {
        NotifyBoosterFailed(failedOrder);

        if (waitingUI != null) waitingUI.SetActive(false);

        GameMaster.LogEventAction("A_Purchase_Failed");
    }


    #endregion

    #region Restore Purchases

    public static void AutoRestorePurchase()
    {
        if (staticScript != null) staticScript.RestorePurchases();
    }

    public void RestorePurchases()
    {
        if (!IsInitialized())
        {
            Debug.LogWarning("RestorePurchases FAIL. Not initialized.");
            return;
        }

#if UNITY_IOS || UNITY_STANDALONE_OSX
        Debug.Log("RestorePurchases started ...");

        try
        {
            UnityIAPServices.StoreController().RestoreTransactions((success, error) =>
            {
                Debug.Log("Restore status: " + success);
                if (!success)
                {
                    Debug.LogError("Restore failed with error: " + error);
                }
            });
        }
        catch (Exception e)
        {
            Debug.LogError($"Error when Restore on iOS: {e.Message}");
        }
#else
        Debug.Log("RestorePurchases PASS. Not supported on this platform. Current = " + Application.platform);
#endif
    }

    #endregion

    private void OnDestroy()
    {
        if (storeController != null)
        {
            storeController.OnStoreConnected -= OnStoreConnected;
            storeController.OnStoreDisconnected -= OnStoreDisconnected;
            storeController.OnProductsFetched -= OnProductsFetched;
            storeController.OnProductsFetchFailed -= OnProductsFetchFailed;
            storeController.OnPurchasePending -= OnPurchasePending;
            storeController.OnPurchaseFailed -= OnPurchaseFailed;

            storeController.OnPurchaseConfirmed -= OnPurchaseConfirmed;
            storeController.OnPurchaseDeferred -= OnPurchaseDeferred;
            storeController.OnPurchasesFetched -= OnPurchasesFetched;
            storeController.OnPurchasesFetchFailed -= OnPurchasesFetchFailed;

        }
        if (staticScript == this)
        {
            staticScript = null;
            activeBoosterPack = 0;
            NotifyBoosterPurchaseStateChanged();
        }
    }

    public void UpdateShopPrices()
    {
        if (HomeController.staticScript == null) return;
        HomeController.staticScript.UpdateIAPPrices(GameMaster.IapPriceRemoveAds, GameMaster.IapPricePack1,
            GameMaster.IapPricePack2,
            GameMaster.IapPricePack3,
            GameMaster.IapPricePack4,
            GameMaster.IapPricePack5,
            GameMaster.IapPricePack6
            );
    }
}
