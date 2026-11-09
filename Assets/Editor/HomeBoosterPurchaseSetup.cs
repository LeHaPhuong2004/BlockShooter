#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Updates purchase labels and authors a result popup without rebuilding the user's shop.</summary>
[InitializeOnLoad]
public static class HomeBoosterPurchaseSetup
{
    private const string ScenePath = "Assets/Scenes/Home.unity";
    private const string RequestPath = "Assets/Editor/HomeBoosterPurchaseSetup.request";
    private const string ReportPath = "Temp/HomeBoosterPurchaseSetup.result";
    private const string PreviewPath = "Temp/HomeBoosterPurchaseSetup-preview.png";
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static bool running;

    static HomeBoosterPurchaseSetup() => EditorApplication.update += ProcessRequest;

    private static void ProcessRequest()
    {
        if (running || !File.Exists(RequestPath) || EditorApplication.isCompiling ||
            EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        running = true;
        try { Prepare(); }
        catch (Exception exception)
        {
            Directory.CreateDirectory("Temp");
            File.WriteAllText(ReportPath, exception.ToString());
            Debug.LogException(exception);
        }
        finally
        {
            File.Delete(RequestPath);
            File.Delete(RequestPath + ".meta");
            running = false;
        }
    }

    [MenuItem("Tools/Home UI/Prepare Booster Purchases")]
    public static void Prepare()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before preparing Booster purchases.");
        Directory.CreateDirectory("Temp");
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            var home = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<HomeExperienceUI>(true)).Single();
            var priorObjects = SceneObjects(scene);
            var priorShop = CaptureShopObjects(home);
            var report = new StringBuilder();
            Undo.RegisterFullObjectHierarchyUndo(home.gameObject, "Prepare Booster purchases");
            home.PrepareBoosterPurchasesForEditor();
            Canvas.ForceUpdateCanvases();

            var retained = new HashSet<int>(SceneObjects(scene));
            Require(priorObjects.All(retained.Contains), "Purchase setup removed existing Home scene objects.");
            Require(priorShop.SequenceEqual(CaptureShopObjects(home)),
                "Purchase setup changed the existing Booster offer/direct-card hierarchy.");
            report.AppendLine("PASS: all original Home objects, five offer rows and four direct Booster cards retained.");
            report.AppendLine(HomeBoosterOffersCheck.Validate(home));
            report.AppendLine(ValidatePurchaseObjects(home));
            report.AppendLine(ValidateInventoryLogic());
            report.AppendLine(ValidatePurchaseTransactions());
            report.AppendLine(ValidateStoreCatalog());
            report.AppendLine(ValidateStoreJournal());

            EditorUtility.SetDirty(home);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save the authored Booster purchases in Home.unity.");
            CapturePopupPreview(home, PreviewPath);
            HomeBoosterOffersCheck.CapturePreview(home, "Temp/HomeBoosterPurchaseSetup-shop-preview.png");
            report.AppendLine("PASS: isolated success/failure popup display and both dismiss actions; Home.unity saved and previews captured in Temp.");
            report.AppendLine("Isolated transaction/inventory checks did not read or write PlayerPrefs or contact a store.");
            File.WriteAllText(ReportPath, report.ToString());
            Debug.Log(report.ToString());
        }
        finally { if (openedHere) EditorSceneManager.CloseScene(scene, true); }
    }

    public static string ValidatePurchaseObjects(HomeExperienceUI home)
    {
        var nodes = home.GetComponentsInChildren<Transform>(true);
        var canvas = nodes.Single(node => node.name == "Home Canvas");
        var popups = nodes.Where(node => node.name == "Booster Purchase Popup").ToArray();
        Require(popups.Length == 1, "Expected one authored Booster Purchase Popup.");
        var popup = popups[0];
        Require(popup.parent == canvas && !popup.gameObject.activeSelf,
            "The purchase result popup must be inactive directly under Home Canvas.");
        var dimmer = popup.GetComponent<Image>();
        Require(dimmer != null && dimmer.raycastTarget && dimmer.color.a > 0f,
            "The purchase result popup requires a blocking dimmer.");
        var window = popup.Find("Booster Purchase Window") as RectTransform;
        Require(window != null && window.GetComponent<Image>() != null,
            "The authored purchase result window is missing.");
        Text title = Find<Text>(popup, "Purchase Title");
        Require(title != null && title.text == "MUA THÀNH CÔNG", "The result title is not the success sample.");
        foreach (string labelName in new[] { "Purchase Description", "Purchase Reward", "Purchase Balance" })
        {
            Text label = Find<Text>(popup, labelName);
            Require(label != null && !string.IsNullOrWhiteSpace(label.text) && !label.raycastTarget,
                "The result popup label is missing or intercepts clicks: " + labelName);
        }
        Require(Find<Image>(popup, "Purchase Reward Icon") != null, "The result reward icon is missing.");
        Button[] controls = popup.GetComponentsInChildren<Button>(true);
        Require(controls.Length == 2, "Expected the result popup's OK and close controls only.");
        MethodInfo actionForButton = typeof(HomeExperienceUI).GetMethod("SceneButtonAction", PrivateInstance);
        Require(actionForButton != null, "SceneButtonAction is missing.");
        foreach (string name in new[] { "PURCHASE_RESULT_OK", "PURCHASE_RESULT_CLOSE" })
        {
            var button = controls.SingleOrDefault(control => control.name == name);
            Require(button != null && button.enabled && button.interactable &&
                button.targetGraphic != null && button.targetGraphic.raycastTarget,
                "The result popup control cannot be clicked: " + name);
            Require(actionForButton.Invoke(home, new object[] { button }) is Action,
                "The result popup control has no runtime action: " + name);
        }

        var direct = nodes.Single(node => node.name == "Booster Direct Purchases");
        var purchases = direct.GetComponentsInChildren<Button>(true);
        Require(purchases.Length == 4, "Expected exactly four direct Booster buy controls.");
        foreach (var button in purchases)
        {
            Text label = button.transform.Find("Label")?.GetComponent<Text>();
            Require(label != null && label.text == "200", button.name + " must display the numeric price 200 per Booster.");
            Require(actionForButton.Invoke(home, new object[] { button }) is Action,
                button.name + " has no runtime purchase action.");
        }
        return "PASS: four direct Booster buttons display 200; the inactive success panel has editable labels and two bound dismiss controls.";
    }

    /// <summary>Uses memory-backed stores exclusively; the player's inventory is never loaded.</summary>
    public static string ValidateInventoryLogic()
    {
        const string legacy = "{\"SplitGun\":2,\"TakeBelowGun\":3,\"SuffleGridGuns\":4,\"RevealGun\":5,\"UnlimitedHeart\":1}";
        string json = "";
        var inventory = new BoosterInventoryStore(() => json, saved => json = saved, () => legacy);
        Require(inventory.GetAmount(BuffType.SplitGun) == 2 && inventory.GetAmount(BuffType.TakeBelowGun) == 3 &&
            inventory.GetAmount(BuffType.SuffleGridGuns) == 4 && inventory.GetAmount(BuffType.RevealGun) == 5 &&
            inventory.UnlimitedHeartAmount == 1, "Legacy inventory migration changed existing rewards.");
        inventory.AddAmount(BuffType.RevealGun, 1);
        inventory = new BoosterInventoryStore(() => json, saved => json = saved);
        Require(inventory.GetAmount(BuffType.RevealGun) == 6 && inventory.GetAmount(BuffType.SplitGun) == 2,
            "A direct reward did not persist to the correct Booster type.");
        Require(inventory.TrySpend(BuffType.RevealGun), "Purchased Booster cannot be consumed.");
        inventory = new BoosterInventoryStore(() => json, saved => json = saved);
        Require(inventory.GetAmount(BuffType.RevealGun) == 5, "Gameplay consumption did not persist.");
        Require(inventory.GrantBundleOnce("isolated-receipt-1", 4, 1), "First bundle receipt was rejected.");
        inventory = new BoosterInventoryStore(() => json, saved => json = saved);
        Require(!inventory.GrantBundleOnce("isolated-receipt-1", 4, 1), "A duplicate receipt granted a second bundle after reload.");
        Require(inventory.GetAmount(BuffType.SplitGun) == 6 && inventory.GetAmount(BuffType.TakeBelowGun) == 7 &&
            inventory.GetAmount(BuffType.SuffleGridGuns) == 8 && inventory.GetAmount(BuffType.RevealGun) == 9 &&
            inventory.UnlimitedHeartAmount == 2, "Bundle receipt did not preserve and increment all rewards exactly once.");
        Require(!inventory.TrySpend(BuffType.SplitGun, 99), "Inventory allowed spending unavailable Boosters.");
        return "PASS: isolated legacy migration, correct Booster rewards, persisted gameplay consumption and receipt replay protection.";
    }

    public static string ValidatePurchaseTransactions()
    {
        string json = "";
        var inventory = new BoosterInventoryStore(() => json, saved => json = saved);
        int balance = 600;
        int spends = 0;
        int refunds = 0;
        Func<int, bool> spend = amount =>
        {
            if (amount > balance) return false;
            balance -= amount;
            spends++;
            return true;
        };
        Action<int> refund = amount => { balance += amount; refunds++; };
        var purchased = BoosterPurchaseTransaction.TryBuy(BuffType.RevealGun, 200, () => balance, spend,
            (type, amount) => inventory.AddAmount(type, amount), refund);
        Require(balance == 400 && spends == 1 && refunds == 0 && inventory.GetAmount(BuffType.RevealGun) == 1,
            "Buying a Booster must spend exactly 200 gold and grant one correct Booster.");
        inventory = new BoosterInventoryStore(() => json, saved => json = saved);
        Require(inventory.GetAmount(BuffType.RevealGun) == 1, "The purchased Booster did not survive storage reload.");

        balance = 199;
        int beforeSpends = spends;
        string beforeJson = json;
        var rejected = BoosterPurchaseTransaction.TryBuy(BuffType.SplitGun, 200, () => balance, spend,
            (type, amount) => inventory.AddAmount(type, amount), refund);
        Require(!Equals(purchased, rejected) && balance == 199 && spends == beforeSpends && json == beforeJson,
            "Insufficient gold must not spend currency, grant an item or report a successful purchase.");

        balance = 400;
        var failed = BoosterPurchaseTransaction.TryBuy(BuffType.SplitGun, 200, () => balance, spend,
            (type, amount) => throw new IOException("Isolated simulated inventory save failure."), refund);
        Require(!Equals(purchased, failed) && balance == 400 && refunds == 1 && json == beforeJson,
            "A failed inventory save must refund the spent gold and reject the purchase.");
        return "PASS: isolated 200-gold purchase, one-item durable reward, insufficient-gold rejection and inventory-save refund.";
    }

    public static string ValidateStoreCatalog()
    {
        var ids = Enumerable.Range(1, 5).Select(IAPManager.GetBoosterPackProductId).ToArray();
        Require(ids.All(id => !string.IsNullOrWhiteSpace(id)) && ids.Distinct(StringComparer.Ordinal).Count() == 5,
            "The five Booster IAP products must have unique IDs.");
        Require(HomeExperienceUI.BoosterOffers.Count == 5, "The Booster catalog must contain five packages.");
        int[] expectedPrices = { 22000, 45000, 109000, 229000, 399000 };
        int[] expectedHeartSeconds = { 0, 0, 1800, 3600, 7200 };
        for (int index = 0; index < expectedPrices.Length; index++)
        {
            Require(HomeExperienceUI.BoosterOffers[index].price == expectedPrices[index],
                "A Booster package price differs from the authored shop.");
            Require(HomeExperienceUI.BoosterOffers[index].unlimitedHeartSeconds == expectedHeartSeconds[index],
                "A Booster package has an incorrect unlimited-heart duration.");
        }
        return "PASS: five distinct store product IDs, authored package prices and explicit finite unlimited-heart durations.";
    }

    public static string ValidateStoreJournal()
    {
        string journalJson = "";
        string inventoryJson = "";
        var inventory = new BoosterInventoryStore(() => inventoryJson, saved => inventoryJson = saved);
        var journal = new IAPBoosterPurchaseJournal(() => journalJson, saved => journalJson = saved);
        int gold = 500;
        long unlimitedUntil = 0;
        Action<string, int> grant = (receipt, count) => inventory.GrantBundleOnce(receipt, count);
        Action<long> activate = until => unlimitedUntil = Math.Max(unlimitedUntil, until);
        Require(journal.Fulfill("isolated-journal-1", 3, 20000, 8, 1800, 1000, gold, unlimitedUntil,
            () => gold, amount => gold += amount, grant, activate), "The first journal receipt was rejected.");
        Require(gold == 20500 && inventory.GetAmount(BuffType.SplitGun) == 8 && unlimitedUntil == 2800,
            "A verified package did not grant its gold, Boosters and finite unlimited-heart time.");
        journal = new IAPBoosterPurchaseJournal(() => journalJson, saved => journalJson = saved);
        Require(!journal.Fulfill("isolated-journal-1", 3, 20000, 8, 1800, 1000, gold, unlimitedUntil,
            () => gold, amount => gold += amount, grant, activate) && gold == 20500 &&
            inventory.GetAmount(BuffType.SplitGun) == 8 && unlimitedUntil == 2800,
            "A completed store receipt was rewarded twice after reload.");

        bool interrupted = false;
        try
        {
            journal.Fulfill("isolated-journal-2", 2, 6000, 6, 0, 1000, gold, unlimitedUntil,
                () => gold, amount => gold += amount,
                (receipt, count) => throw new IOException("Isolated interrupted inventory grant."), activate);
        }
        catch (IOException) { interrupted = true; }
        Require(interrupted && gold == 26500 && !journal.IsComplete("isolated-journal-2"),
            "The interrupted package was incorrectly completed.");
        journal = new IAPBoosterPurchaseJournal(() => journalJson, saved => journalJson = saved);
        int resumed = 0;
        journal.ResumeIncomplete(() => gold, amount => gold += amount, grant, activate, pack => resumed++);
        Require(resumed == 1 && gold == 26500 && inventory.GetAmount(BuffType.SplitGun) == 14 &&
            unlimitedUntil == 2800 && journal.IsComplete("isolated-journal-2"),
            "An interrupted package did not resume exactly once without duplicating saved currency.");

        string interruptedJournal = "";
        bool failGoldCheckpoint = true;
        var interruptedSave = new IAPBoosterPurchaseJournal(() => interruptedJournal, saved =>
        {
            if (failGoldCheckpoint && saved.Contains("\"goldApplied\":true"))
                throw new IOException("Isolated interrupted journal checkpoint.");
            interruptedJournal = saved;
        });
        try
        {
            interruptedSave.Fulfill("isolated-journal-3", 1, 1000, 4, 0, 1000, gold, unlimitedUntil,
                () => gold, amount => gold += amount, grant, activate);
        }
        catch (IOException) { }
        Require(gold == 27500 && inventory.GetAmount(BuffType.SplitGun) == 14,
            "The interrupted gold checkpoint did not stop before granting Boosters.");
        failGoldCheckpoint = false;
        interruptedSave = new IAPBoosterPurchaseJournal(() => interruptedJournal, saved => interruptedJournal = saved);
        interruptedSave.ResumeIncomplete(() => gold, amount => gold += amount, grant, activate);
        Require(gold == 27500 && inventory.GetAmount(BuffType.SplitGun) == 18,
            "Resuming after a saved wallet update duplicated gold or lost the Booster reward.");
        return "PASS: isolated verified-store fulfillment, receipt replay rejection and recovery across inventory/wallet checkpoint interruptions.";
    }

    private static int[] SceneObjects(Scene scene) => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
        .Select(node => node.gameObject.GetInstanceID()).OrderBy(id => id).ToArray();

    private static int[] CaptureShopObjects(HomeExperienceUI home)
    {
        var scroll = home.GetComponentsInChildren<ScrollRect>(true).Single(item => item.name == "Booster Shop Viewport");
        return scroll.content.GetComponentsInChildren<Transform>(true)
            .Select(node => node.gameObject.GetInstanceID()).OrderBy(id => id).ToArray();
    }

    private static T Find<T>(Transform parent, string name) where T : Component =>
        parent.GetComponentsInChildren<T>(true).SingleOrDefault(component => component.name == name);

    private static void CapturePopupPreview(HomeExperienceUI home, string path)
    {
        Transform source = home.GetComponentsInChildren<Transform>(true).Single(node => node.name == "Booster Purchase Popup");
        Scene preview = default;
        RenderTexture target = null;
        Texture2D pixels = null;
        RenderTexture previous = RenderTexture.active;
        try
        {
            preview = EditorSceneManager.NewPreviewScene();
            var cameraObject = new GameObject("Booster Purchase Preview Camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, preview);
            var camera = cameraObject.GetComponent<Camera>();
            camera.scene = preview;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(44, 15, 90, 255);
            camera.orthographic = true;
            camera.orthographicSize = 960f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 1000f;
            camera.transform.position = new Vector3(0f, 0f, -100f);
            camera.cullingMask = 1 << 31;
            target = new RenderTexture(1080, 1920, 24, RenderTextureFormat.ARGB32);
            target.Create();
            camera.targetTexture = target;
            var canvasObject = new GameObject("Booster Purchase Preview Canvas", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(canvasObject, preview);
            canvasObject.layer = 31;
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10f;
            canvas.sortingOrder = 100;
            canvasObject.SetActive(false);
            var clone = UnityEngine.Object.Instantiate(source.gameObject, canvasObject.transform, false);
            clone.name = "Booster Purchase Success Preview";
            foreach (Transform node in clone.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 31;
            ValidatePopupInteraction(preview, canvasObject, clone);
            canvasObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            pixels.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(path, pixels.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            if (target != null)
            {
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
            if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
        }
    }

    private static void ValidatePopupInteraction(Scene preview, GameObject canvas, GameObject popup)
    {
        var host = new GameObject("Isolated Booster Purchase UI Test");
        SceneManager.MoveGameObjectToScene(host, preview);
        host.SetActive(false);
        var home = host.AddComponent<HomeExperienceUI>();
        home.enabled = false;
        var type = typeof(HomeExperienceUI);
        type.GetField("homeCanvasObject", PrivateInstance).SetValue(home, canvas);
        type.GetField("boosterPurchasePopup", PrivateInstance).SetValue(home, popup);
        var show = type.GetMethod("ShowBoosterPurchaseResult", PrivateInstance);
        var actionForButton = type.GetMethod("SceneButtonAction", PrivateInstance);
        Require(show != null && actionForButton != null, "The result popup interaction methods are missing.");
        Sprite icon = Find<Image>(popup.transform, "Purchase Reward Icon").sprite;
        object[] sample = { true, "Kính lúp ×1", "+1 BOOSTER", "Đã nhận vào kho của bạn", icon };
        show.Invoke(home, sample);
        Require(popup.activeSelf && Find<Text>(popup.transform, "Purchase Title").text == "MUA THÀNH CÔNG" &&
            Find<Text>(popup.transform, "Purchase Reward").text == "+1 BOOSTER",
            "The successful purchase did not display the success title and reward.");
        var okay = Find<Button>(popup.transform, "PURCHASE_RESULT_OK");
        var close = Find<Button>(popup.transform, "PURCHASE_RESULT_CLOSE");
        ((Action)actionForButton.Invoke(home, new object[] { okay }))();
        Require(!popup.activeSelf, "The result popup's OK action did not dismiss it.");
        show.Invoke(home, new object[] { false, "Bạn cần 200 vàng.", "KHÔNG ĐỦ VÀNG", "Số dư: 199 vàng", icon });
        Require(popup.activeSelf && Find<Text>(popup.transform, "Purchase Title").text == "KHÔNG ĐỦ VÀNG" &&
            Find<Text>(popup.transform, "Purchase Reward").text == "",
            "A rejected purchase incorrectly displayed success or granted-reward text.");
        ((Action)actionForButton.Invoke(home, new object[] { close }))();
        Require(!popup.activeSelf, "The result popup's close action did not dismiss it.");
        show.Invoke(home, sample);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
