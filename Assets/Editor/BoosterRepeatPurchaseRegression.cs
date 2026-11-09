#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Exercises repeat-purchase UI and heart timers without a store or player data.</summary>
[InitializeOnLoad]
public static class BoosterRepeatPurchaseRegression
{
    private const string RequestPath = "Temp/BoosterRepeatPurchaseRegression.request";
    private const string ReportPath = "Temp/BoosterRepeatPurchaseRegression.result";
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
    private static bool running;

    static BoosterRepeatPurchaseRegression() => EditorApplication.update += ProcessRequest;

    private static void ProcessRequest()
    {
        if (running || !File.Exists(RequestPath) || EditorApplication.isCompiling ||
            EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        running = true;
        try { Run(); }
        catch (Exception exception)
        {
            Directory.CreateDirectory("Temp");
            File.WriteAllText(ReportPath, exception.ToString());
            Debug.LogException(exception);
        }
        finally
        {
            File.Delete(RequestPath);
            running = false;
        }
    }

    [MenuItem("Tools/Home UI/Check Repeated Booster Purchases")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Run Booster regressions outside Play Mode after compilation.");
        var report = new StringBuilder();
        report.AppendLine(ValidateRepeatPurchaseControls());
        report.AppendLine(ValidateHeartCountdown());
        report.AppendLine(HomeBoosterPurchaseSetup.ValidateInventoryLogic());
        report.AppendLine(HomeBoosterPurchaseSetup.ValidatePurchaseTransactions());
        report.AppendLine(HomeBoosterPurchaseSetup.ValidateStoreJournal());
        report.AppendLine("No real IAP service, receipt grant, PlayerPrefs, authored scene object, or scene save was used.");
        Directory.CreateDirectory("Temp");
        File.WriteAllText(ReportPath, report.ToString());
        Debug.Log(report.ToString());
    }

    public static string ValidateRepeatPurchaseControls()
    {
        IAPManager previousManager = IAPManager.staticScript;
        FieldInfo stateEvent = StaticField(typeof(IAPManager), "BoosterPurchaseStateChanged");
        object previousListeners = stateEvent.GetValue(null);
        Scene preview = default;
        Action refresh = null;
        try
        {
            // Isolate the event as well as the objects: an unrelated Editor
            // subscriber must never see these simulated transaction states.
            stateEvent.SetValue(null, null);
            IAPManager.staticScript = null;
            preview = EditorSceneManager.NewPreviewScene();
            GameObject managerObject = InactiveRoot(preview, "Regression Canonical IAP");
            var manager = managerObject.AddComponent<IAPManager>();
            Invoke(manager, "Awake");
            Require(IAPManager.staticScript == manager && manager.enabled,
                "The first manager did not become the canonical purchase owner.");

            GameObject duplicateObject = InactiveRoot(preview, "Regression Duplicate IAP");
            var siblingMarker = new GameObject("Preserved Shared Object Content");
            siblingMarker.transform.SetParent(duplicateObject.transform, false);
            var duplicate = duplicateObject.AddComponent<IAPManager>();
            Invoke(duplicate, "Awake");
            Require(IAPManager.staticScript == manager && !duplicate.enabled &&
                duplicateObject != null && siblingMarker != null,
                "A duplicate manager replaced the owner or destroyed shared scene content.");

            GameObject homeObject = InactiveRoot(preview, "Regression Home");
            var home = homeObject.AddComponent<HomeExperienceUI>();
            var viewport = new GameObject("Booster Shop Viewport", typeof(RectTransform));
            viewport.transform.SetParent(homeObject.transform, false);
            SetField(home, "shopBoosterViewport", viewport);
            Button[] purchases = Enumerable.Range(1, 5)
                .Select(pack => CreateBuyButton(viewport.transform, "BUY_BOOSTER_OFFER_" + pack)).ToArray();
            MethodInfo refreshMethod = InstanceMethod(typeof(HomeExperienceUI), "RefreshBoosterShopPrices");
            refresh = (Action)Delegate.CreateDelegate(typeof(Action), home, refreshMethod);
            IAPManager.BoosterPurchaseStateChanged += refresh;
            refresh();
            RequireButtons(purchases, true, "Initial bundle purchase buttons");
            Require(purchases.All(button => !string.IsNullOrWhiteSpace(button.transform.Find("Label").GetComponent<Text>().text)),
                "The actual price refresh did not populate all five purchase labels.");

            var waiting = new GameObject("Regression Waiting Overlay");
            waiting.transform.SetParent(managerObject.transform, false);
            waiting.SetActive(false);
            manager.waitingUI = waiting;

            // Reproduce the callback ordering from the bug: a stale manager
            // completes before the manager that owns the purchase. Its refresh
            // must keep the buttons locked until the owner completes as well.
            BeginWaiting(manager, waiting, 3);
            RequireButtons(purchases, false, "First bundle in progress");
            SetField(duplicate, "activeBoosterPack", 3);
            Invoke(duplicate, "CompleteBoosterWaitingState", 3);
            Require(IAPManager.IsBoosterPurchaseInProgress && waiting.activeSelf,
                "A stale manager cleared the canonical transaction or its overlay.");
            RequireButtons(purchases, false, "Stale duplicate callback before canonical completion");
            Invoke(manager, "CompleteBoosterWaitingState", 3);
            Require(!IAPManager.IsBoosterPurchaseInProgress && !waiting.activeSelf,
                "Receipt completion did not release the canonical waiting state.");
            RequireButtons(purchases, true, "Canonical completion without a second reward notification");

            // The exact same pack and another pack must both be purchasable
            // again. Exercise the same completion method used by journal replay.
            foreach (int pack in new[] { 3, 5 })
            {
                BeginWaiting(manager, waiting, pack);
                RequireButtons(purchases, false, "Repeated pack " + pack + " in progress");
                Invoke(manager, "CompleteBoosterWaitingState", pack == 5 ? 4 : 2);
                Require(IAPManager.IsBoosterPurchaseInProgress && waiting.activeSelf,
                    "An unrelated pack completion unlocked the active purchase.");
                RequireButtons(purchases, false, "Unrelated receipt while pack " + pack + " is active");
                Invoke(manager, "CompleteBoosterWaitingState", pack);
                RequireButtons(purchases, true, "Repeated pack " + pack + " completed");
                Require(!IAPManager.IsBoosterPurchaseInProgress && !waiting.activeSelf,
                    "A repeated purchase left its waiting overlay active.");
            }

            // Closing a result panel must repair stale interactable flags even
            // when there is no later price callback from the store.
            var popup = new GameObject("Regression Purchase Result");
            popup.transform.SetParent(homeObject.transform, false);
            popup.SetActive(true);
            SetField(home, "boosterPurchasePopup", popup);
            foreach (Button button in purchases) button.interactable = false;
            Invoke(home, "HideBoosterPurchasePopup");
            Require(!popup.activeSelf, "The success panel did not close.");
            RequireButtons(purchases, true, "Closing the success panel");

            // Inactive test objects never run Start (and therefore never connect
            // IAP). Invoke the destroy callback explicitly to test ownership.
            Invoke(duplicate, "OnDestroy");
            UnityEngine.Object.DestroyImmediate(duplicate);
            Require(IAPManager.staticScript == manager && duplicateObject != null && siblingMarker != null,
                "Destroying a duplicate manager cleared the owner or shared objects.");
            Invoke(manager, "OnDestroy");
            Require(IAPManager.staticScript == null,
                "Destroying the canonical manager did not clear its own reference.");
            RequireButtons(purchases, true, "Canonical manager teardown");
            return "PASS: single IAP owner; duplicate Awake/teardown preserves shared objects; all five real UI buttons unlock after stale/duplicate completion, repeated same/different packs, and popup dismissal.";
        }
        finally
        {
            if (refresh != null) IAPManager.BoosterPurchaseStateChanged -= refresh;
            try
            {
                if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            }
            finally
            {
                IAPManager.staticScript = previousManager;
                stateEvent.SetValue(null, previousListeners);
            }
        }
    }

    public static string ValidateHeartCountdown()
    {
        const long now = 1000;
        Require(new HeartState().GetTimerText(now) == "FULL", "Regular full hearts must display FULL.");
        var halfHour = new HeartState();
        halfHour.ActivateUnlimited(now, 1800);
        Require(halfHour.GetTimerText(now) == "30:00", "A 30-minute reward did not display 30:00.");
        Require(halfHour.GetTimerText(now + 1) == "29:59", "Unlimited hearts did not count down each second.");
        Require(!halfHour.LoseHeart(now + 1) && halfHour.hearts == HeartState.MaxHearts,
            "Unlimited time consumed a normal heart.");
        Require(halfHour.GetTimerText(now + 1799) == "00:01", "The final unlimited second is incorrect.");
        Require(halfHour.GetTimerText(now + 1800) == "FULL" && !halfHour.IsUnlimited(now + 1800),
            "Expired unlimited time did not return to normal full hearts.");
        var hour = new HeartState();
        hour.ActivateUnlimited(now, 3600);
        Require(hour.GetTimerText(now) == "01:00:00", "A one-hour reward did not display its hours.");
        Require(hour.GetTimerText(now + 1) == "59:59", "The one-hour countdown boundary is incorrect.");
        var twoHours = new HeartState();
        twoHours.ActivateUnlimited(now, 7200);
        Require(twoHours.GetTimerText(now) == "02:00:00", "A two-hour reward did not display its hours.");
        Require(twoHours.GetTimerText(now + 3601) == "59:59", "Offline elapsed time was not subtracted.");
        var extended = new HeartState();
        extended.ActivateUnlimited(now, 1800);
        extended.ActivateUnlimited(now, 3600);
        Require(extended.GetTimerText(now) == "01:30:00", "Repeated rewards did not extend remaining unlimited time.");
        var depleted = new HeartState { hearts = 4, nextHeartAt = now + HeartState.RefillSeconds, unlimitedUntil = now + 5 };
        Require(depleted.GetTimerText(now) == "00:05", "Unlimited countdown lost priority over heart recovery.");
        Require(depleted.GetTimerText(now + 5) == "14:55", "Expiry did not restore the normal recovery countdown.");
        return "PASS: 13 isolated heart checks: 30/60/120 minutes, per-second/offline countdown, extension, expiry and normal recovery.";
    }

    private static GameObject InactiveRoot(Scene scene, string name)
    {
        var root = new GameObject(name);
        root.SetActive(false);
        SceneManager.MoveGameObjectToScene(root, scene);
        return root;
    }

    private static Button CreateBuyButton(Transform viewport, string name)
    {
        var row = new GameObject(name + " Row", typeof(RectTransform));
        row.transform.SetParent(viewport, false);
        var control = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        control.transform.SetParent(row.transform, false);
        var label = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        label.transform.SetParent(control.transform, false);
        var button = control.GetComponent<Button>();
        button.targetGraphic = control.GetComponent<Image>();
        return button;
    }

    private static void BeginWaiting(IAPManager manager, GameObject overlay, int pack)
    {
        SetField(manager, "activeBoosterPack", pack);
        overlay.SetActive(true);
        InstanceMethod(typeof(IAPManager), "NotifyBoosterPurchaseStateChanged", PrivateStatic).Invoke(null, null);
    }

    private static void RequireButtons(Button[] buttons, bool expected, string context)
    {
        Require(buttons.Length == 5 && buttons.All(button => button.interactable == expected),
            context + ": expected all five buttons to be " + (expected ? "enabled." : "locked."));
    }

    private static MethodInfo InstanceMethod(Type type, string name, BindingFlags flags = PrivateInstance)
    {
        return type.GetMethod(name, flags) ?? throw new MissingMethodException(type.FullName, name);
    }

    private static FieldInfo StaticField(Type type, string name)
    {
        return type.GetField(name, PrivateStatic) ?? throw new MissingFieldException(type.FullName, name);
    }

    private static void SetField(object instance, string name, object value)
    {
        FieldInfo field = instance.GetType().GetField(name, PrivateInstance)
            ?? throw new MissingFieldException(instance.GetType().FullName, name);
        field.SetValue(instance, value);
    }

    private static object Invoke(object instance, string name, params object[] arguments) =>
        InstanceMethod(instance.GetType(), name).Invoke(instance, arguments);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Booster repeat-purchase regression: " + message);
    }
}
#endif
