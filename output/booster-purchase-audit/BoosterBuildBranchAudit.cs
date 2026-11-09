using System;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Security;
internal static class BoosterBuildBranchAudit {
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

}