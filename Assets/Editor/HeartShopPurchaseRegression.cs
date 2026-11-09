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

/// <summary>Checks gold-for-heart purchases using memory stores and disposable preview scenes.</summary>
[InitializeOnLoad]
public static class HeartShopPurchaseRegression
{
    private const string RequestPath = "Temp/HeartShopPurchaseRegression.request";
    private const string ReportPath = "Temp/HeartShopPurchaseRegression.result";
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static bool running;

    static HeartShopPurchaseRegression() => EditorApplication.update += ProcessRequest;

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
        finally { File.Delete(RequestPath); running = false; }
    }

    [MenuItem("Tools/Home UI/Check Heart Shop Purchases")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Run heart purchase checks outside Play Mode after compilation.");
        var report = new StringBuilder();
        report.AppendLine(ValidateTransactions());
        report.AppendLine(ValidatePurchasedHeartState());
        report.AppendLine(ValidateControlsAndFlights());
        report.AppendLine(InspectAuthoredHome());
        report.AppendLine("No real purchase, player wallet, PlayerPrefs, audible playback, authoring helper, or scene save was used.");
        Directory.CreateDirectory("Temp");
        File.WriteAllText(ReportPath, report.ToString());
        Debug.Log(report.ToString());
    }

    public static string ValidateTransactions()
    {
        Require(HeartPurchaseTransaction.GetPrice(5) == 2000 && HeartPurchaseTransaction.GetPrice(20) == 8000,
            "The 5/20-heart prices must be exactly 2,000/8,000 gold.");
        const long now = 1000;
        int balance = 10000, spends = 0, grants = 0, refunds = 0;
        var state = new HeartState();
        Func<int, bool> spend = price => { spends++; balance -= price; return true; };
        Action<int> grant = amount => { grants++; state.AddHearts(now, amount); };
        Action<int> refund = amount => { refunds++; balance += amount; };
        Require(HeartPurchaseTransaction.TryBuy(5, () => balance, spend, grant, refund) == HeartPurchaseResult.Success &&
            balance == 8000 && state.hearts == 10 && spends == 1 && grants == 1 && refunds == 0,
            "The 5-heart purchase did not debit/grant exactly once.");
        Require(HeartPurchaseTransaction.TryBuy(20, () => balance, spend, grant, refund) == HeartPurchaseResult.Success &&
            balance == 0 && state.hearts == 30 && spends == 2 && grants == 2 && refunds == 0,
            "The 20-heart purchase did not debit/grant exactly once after another purchase.");

        balance = 6000;
        for (int i = 0; i < 2; i++)
            Require(HeartPurchaseTransaction.TryBuy(5, () => balance, spend, grant, refund) == HeartPurchaseResult.Success,
                "The same heart package could not be purchased again.");
        Require(balance == 2000 && state.hearts == 40 && spends == 4 && grants == 4 && refunds == 0,
            "Repeated purchases duplicated or lost a debit/reward callback.");

        balance = 1999;
        int previousHearts = state.hearts, previousSpends = spends, previousGrants = grants;
        Require(HeartPurchaseTransaction.TryBuy(5, () => balance, spend, grant, refund) == HeartPurchaseResult.InsufficientGold &&
            balance == 1999 && state.hearts == previousHearts && spends == previousSpends && grants == previousGrants && refunds == 0,
            "Insufficient gold invoked a debit, reward or refund callback.");
        balance = 8000;
        int rejectedSpends = 0;
        Require(HeartPurchaseTransaction.TryBuy(20, () => balance, price => { rejectedSpends++; return false; }, grant, refund)
            == HeartPurchaseResult.InsufficientGold && rejectedSpends == 1 && balance == 8000 &&
            state.hearts == previousHearts && grants == previousGrants && refunds == 0,
            "A rejected wallet debit still granted hearts or retried its callback.");

        int invalidCallbacks = 0;
        foreach (int amount in new[] { 0, -1, 1, 10, int.MaxValue })
        {
            Require(HeartPurchaseTransaction.GetPrice(amount) == 0, "An unsupported heart amount has a price.");
            Require(HeartPurchaseTransaction.TryBuy(amount, () => { invalidCallbacks++; return 10000; },
                price => { invalidCallbacks++; return true; }, value => invalidCallbacks++, value => invalidCallbacks++)
                == HeartPurchaseResult.Failed && invalidCallbacks == 0,
                "An invalid heart package invoked wallet/reward callbacks.");
        }

        balance = 5000;
        int failedGrants = 0;
        previousSpends = spends;
        Require(HeartPurchaseTransaction.TryBuy(5, () => balance, spend,
            amount => { failedGrants++; throw new IOException("Isolated failed heart save."); }, refund)
            == HeartPurchaseResult.Failed && balance == 5000 && failedGrants == 1 && spends == previousSpends + 1 &&
            refunds == 1 && state.hearts == previousHearts && grants == previousGrants,
            "A failed heart grant did not refund exactly once or changed the reward state.");
        return "PASS: exact 2,000/8,000-gold purchases, repeat purchases, insufficient/rejected debits, unsupported amounts and one-refund failed grants; no duplicated callbacks.";
    }

    public static string ValidatePurchasedHeartState()
    {
        const long now = 1000;
        var five = new HeartState();
        five.AddHearts(now, 5);
        Require(five.hearts == 10 && five.nextHeartAt == 0 && five.GetTimerText(now) == "FULL",
            "Buying five hearts at full capacity did not retain all ten hearts.");
        five.Advance(now + 100000);
        Require(five.hearts == 10 && five.nextHeartAt == 0, "Offline recovery clamped or increased purchased overflow.");
        var twenty = new HeartState();
        twenty.AddHearts(now, 20);
        twenty.AddHearts(now, 5);
        Require(twenty.hearts == 30 && twenty.SecondsToNextHeart(now) == 0,
            "Twenty purchased hearts or subsequent purchases were capped at five.");
        string saved = JsonUtility.ToJson(twenty);
        twenty = JsonUtility.FromJson<HeartState>(saved);
        twenty.Advance(now + 10);
        Require(twenty.hearts == 30, "Purchased overflow did not survive state serialization.");

        var consumed = new HeartState { hearts = 6 };
        Require(consumed.LoseHeart(now) && consumed.hearts == 5 && consumed.nextHeartAt == 0,
            "Consuming overflow started regeneration while the wallet was still full.");
        Require(consumed.LoseHeart(now + 1) && consumed.hearts == 4 &&
            consumed.nextHeartAt == now + 1 + HeartState.RefillSeconds && consumed.GetTimerText(now + 1) == "15:00",
            "Regeneration did not start after consuming below five hearts.");
        consumed.Advance(now + 1 + HeartState.RefillSeconds);
        Require(consumed.hearts == 5 && consumed.GetTimerText(now + 1 + HeartState.RefillSeconds) == "FULL",
            "Natural regeneration did not stop at five.");
        var depleted = new HeartState { hearts = 1, nextHeartAt = now + HeartState.RefillSeconds };
        depleted.Advance(now + 2 * HeartState.RefillSeconds);
        Require(depleted.hearts == 3 && depleted.nextHeartAt == now + 3 * HeartState.RefillSeconds,
            "Normal elapsed heart recovery changed after allowing overflow.");
        depleted.Advance(now + 100 * HeartState.RefillSeconds);
        Require(depleted.hearts == 5 && depleted.nextHeartAt == 0, "Natural recovery exceeded its five-heart limit.");
        var unlimited = new HeartState { unlimitedUntil = now + 1800 };
        unlimited.AddHearts(now, 20);
        Require(unlimited.hearts == 25 && unlimited.GetTimerText(now) == "30:00" && !unlimited.LoseHeart(now),
            "Buying finite hearts changed unlimited duration or consumed protected hearts.");
        unlimited.GetTimerText(now + 1800);
        Require(unlimited.hearts == 25 && !unlimited.IsUnlimited(now + 1800),
            "Unlimited expiry lost purchased finite hearts.");
        int before = unlimited.hearts;
        unlimited.AddHearts(now + 1800, 0);
        unlimited.AddHearts(now + 1800, -1);
        Require(unlimited.hearts == before, "Invalid additions changed the heart balance.");
        return "PASS: 5/20-heart rewards and repeats preserve overflow across time and serialization; spending/recovery resumes below five; unlimited countdown and purchased stock remain intact.";
    }

    public static string ValidateControlsAndFlights()
    {
        Scene preview = EditorSceneManager.NewPreviewScene();
        AudioManager previousAudio = AudioManager.Instance;
        UnityEngine.Random.State previousRandom = UnityEngine.Random.state;
        try
        {
            AudioManager.Instance = null; // Inspect visual effects without playing the user's audio.
            var root = new GameObject("Heart Purchase Regression");
            root.SetActive(false);
            SceneManager.MoveGameObjectToScene(root, preview);
            var home = root.AddComponent<HomeExperienceUI>();
            home.enabled = false;
            RectTransform canvasRect = Child(root.transform, "Home Canvas", Vector2.zero);
            Canvas canvas = canvasRect.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasRect.sizeDelta = new Vector2(1080, 1920);
            Set(home, "homeCanvasObject", canvas.gameObject);
            Set(home, "sceneHomeCanvas", canvas);
            var gold = Child(canvasRect, "Gold Shop Viewport", Vector2.zero);
            Set(home, "shopGoldViewport", gold.gameObject);
            Button five = BuyButton(gold, "BUY 5 TIM", new Vector2(-260, -200));
            Button twenty = BuyButton(gold, "BUY 20 TIM", new Vector2(260, -200));
            Button other = BuyButton(gold, "UNRELATED", Vector2.zero);
            other.transform.Find("Label").GetComponent<Text>().text = "UNCHANGED";
            Invoke(home, "RefreshHeartShopPrices");
            Require(five.transform.Find("Label").GetComponent<Text>().text == "2.000" &&
                twenty.transform.Find("Label").GetComponent<Text>().text == "8.000" &&
                other.transform.Find("Label").GetComponent<Text>().text == "UNCHANGED",
                "Actual heart-price refresh produced incorrect labels or changed an unrelated button.");
            foreach (Button button in new[] { five, twenty })
            {
                var direct = Invoke(home, "HeartShopButtonAction", button.name) as Action;
                var scene = Invoke(home, "SceneButtonAction", button) as Action;
                Require(direct != null && scene != null && direct.Method == scene.Method,
                    button.name + " uses an information popup instead of the heart purchase action.");
            }

            RectTransform hud = Child(canvasRect, "Home", Vector2.zero);
            RectTransform coins = Child(hud, "COINS", new Vector2(-350, 650));
            RectTransform coinIcon = Child(coins, "Button Icon", Vector2.zero);
            RectTransform energy = Child(hud, "ENERGY", new Vector2(200, 650));
            RectTransform energyIcon = Child(energy, "Button Icon", new Vector2(-20, 0));
            Set(home, "coinHudButtonRect", coins);
            Set(home, "coinHudIconRect", coinIcon);
            Sprite heartSprite = LoadSprite("Assets/Resources/UI/HomeIcons/Heart.png");
            Sprite coinSprite = LoadSprite("Assets/Resources/UI/HomeIcons/Coin.png");
            Set(home, "heartSprite", heartSprite);
            Set(home, "coinSprite", coinSprite);
            var popup = Child(canvasRect, "Booster Purchase Popup", Vector2.zero);
            foreach (string name in new[] { "Purchase Title", "Purchase Description", "Purchase Reward", "Purchase Balance" })
                Child(popup, name, Vector2.zero).gameObject.AddComponent<Text>();
            Child(popup, "Purchase Reward Icon", Vector2.zero).gameObject.AddComponent<Image>();
            Set(home, "boosterPurchasePopup", popup.gameObject);
            popup.gameObject.SetActive(false);
            RectTransform booster = Child(canvasRect, "Booster Shop Viewport", Vector2.zero);
            Button boosterBuy = BuyButton(booster, "BUY_BOOSTER_OFFER_1", new Vector2(300, -400));
            Set(home, "shopBoosterViewport", booster.gameObject);
            root.SetActive(true);
            Canvas.ForceUpdateCanvases();

            foreach (int amount in new[] { 5, 20 })
            {
                RectTransform source = amount == 5 ? five.transform as RectTransform : twenty.transform as RectTransform;
                Invoke(home, "PlayHeartPurchaseEffect", amount, source);
                var overlay = Get<RectTransform>(home, "buttonEffectsOverlay");
                MoveCoinOnUI[] flights = overlay.GetComponentsInChildren<MoveCoinOnUI>(true);
                Require(flights.Length >= 5 && flights.Length <= amount,
                    "The heart purchase did not create a visible bounded flight effect.");
                CheckFlightOverlay(canvasRect, overlay);
                CheckFlights(flights, overlay, source, energyIcon, heartSprite);
                Require(!popup.gameObject.activeSelf, "Heart flights unexpectedly opened a success popup.");
                foreach (MoveCoinOnUI flight in flights) UnityEngine.Object.DestroyImmediate(flight.gameObject);
            }

            int reward = HomeExperienceUI.BoosterOffers[0].coins;
            Invoke(home, "HandleMoneyAwarded", reward, reward + 500);
            var coinOverlay = Get<RectTransform>(home, "buttonEffectsOverlay");
            MoveCoinOnUI[] coinFlights = coinOverlay.GetComponentsInChildren<MoveCoinOnUI>(true);
            Require(coinFlights.Length > 0, "A booster package's gold reward did not create coin flights.");
            CheckFlights(coinFlights, coinOverlay, boosterBuy.transform as RectTransform, coins, coinSprite);
            Invoke(home, "ShowBoosterPurchaseResult", true, "Isolated package", "+1000 VÀNG", "Test only", coinSprite);
            CheckFlightOverlay(canvasRect, coinOverlay);
            Require(popup.gameObject.activeSelf && coinOverlay.GetSiblingIndex() > popup.GetSiblingIndex(),
                "The booster success popup covers the coin flights.");
            return "PASS: actual 2.000/8.000 numeric labels and purchase actions; heart flights target ENERGY without a success popup; booster gold starts at its package; effects stay above popups and do not block clicks.";
        }
        finally
        {
            try { EditorSceneManager.ClosePreviewScene(preview); }
            finally { AudioManager.Instance = previousAudio; UnityEngine.Random.state = previousRandom; }
        }
    }

    public static string InspectAuthoredHome()
    {
        Scene preview = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Home.unity");
        try
        {
            var nodes = preview.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            var home = nodes.Select(node => node.GetComponent<HomeExperienceUI>()).Single(component => component != null);
            Transform canvas = nodes.Single(node => node.name == "Home Canvas");
            Transform gold = nodes.Single(node => node.name == "Gold Shop Viewport");
            Set(home, "homeCanvasObject", canvas.gameObject);
            Set(home, "shopGoldViewport", gold.gameObject);
            var report = new StringBuilder("PASS: read-only authored Home preview: ");
            Button[] buttons = new[] { "BUY 5 TIM", "BUY 20 TIM" }.Select(name =>
                gold.GetComponentsInChildren<Button>(true).Single(candidate => candidate.name == name)).ToArray();
            Text[] labels = buttons.Select(button => button.transform.Find("Label")?.GetComponent<Text>()).ToArray();
            Require(labels.All(label => label != null), "An authored heart button is missing its Label.");
            string[] savedLabels = labels.Select(label => label.text).ToArray();
            Invoke(home, "RefreshHeartShopPrices");
            for (int index = 0; index < buttons.Length; index++)
            {
                Button button = buttons[index];
                string name = button.name;
                Text label = labels[index];
                int callbacks = button.onClick.GetPersistentEventCount();
                var action = Invoke(home, "SceneButtonAction", button) as Action;
                var heartAction = Invoke(home, "HeartShopButtonAction", name) as Action;
                Require(action != null && heartAction != null && action.Method == heartAction.Method,
                    name + " is not routed to a heart purchase in the authored scene.");
                Require(label.text == (name == "BUY 5 TIM" ? "2.000" : "8.000"),
                    name + " has an incorrect runtime gold price.");
                report.Append(name + " [saved='" + savedLabels[index] + "', runtime='" + label.text + "', Inspector callbacks=" + callbacks + "]; ");
            }
            var managers = nodes.Select(node => node.GetComponent<AudioManager>()).Where(manager => manager != null).ToArray();
            if (managers.Length == 0) report.Append("no scene-local AudioManager (persistent audio is external to Home).");
            foreach (var manager in managers)
            {
                bool active = manager.enabled && manager.gameObject.activeInHierarchy;
                report.Append("AudioManager '" + manager.name + "' active=" + active + ", coinCollect=" +
                    (manager.coinCollect != null ? manager.coinCollect.name : "MISSING") + ". ");
                Require(!active || manager.coinCollect != null, "An active authored AudioManager has no coinCollect clip.");
            }
            return report.ToString();
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }

    private static void CheckFlightOverlay(RectTransform canvas, RectTransform overlay)
    {
        CanvasGroup group = overlay.GetComponent<CanvasGroup>();
        Require(overlay.parent == canvas && overlay.GetSiblingIndex() == canvas.childCount - 1 &&
            group != null && !group.blocksRaycasts && !group.interactable,
            "Currency effects are clipped, below a sibling panel, or blocking clicks.");
    }

    private static void CheckFlights(MoveCoinOnUI[] flights, RectTransform overlay, RectTransform source,
        RectTransform target, Sprite sprite)
    {
        Require(RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay,
            RectTransformUtility.WorldToScreenPoint(null, source.position), null, out Vector2 start), "Cannot resolve flight source.");
        Require(RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay,
            RectTransformUtility.WorldToScreenPoint(null, target.position), null, out Vector2 end), "Cannot resolve flight target.");
        foreach (MoveCoinOnUI flight in flights)
        {
            Image image = flight.GetComponent<Image>();
            Require(image != null && image.sprite == sprite && !image.raycastTarget && !image.maskable &&
                image.color == Color.white && Get<bool>(flight, "initialized") &&
                Vector2.Distance(Get<Vector2>(flight, "startPosition"), start) <= 28f &&
                Vector2.Distance(Get<Vector2>(flight, "endPosition"), end) < 0.1f,
                "A reward flight has the wrong icon, origin, destination or input/clipping settings.");
        }
    }

    private static RectTransform Child(Transform parent, string name, Vector2 position)
    {
        var child = new GameObject(name, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        var rect = child.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(160, 100);
        rect.anchoredPosition = position;
        return rect;
    }

    private static Button BuyButton(Transform parent, string name, Vector2 position)
    {
        RectTransform row = Child(parent, name + " Row", Vector2.zero);
        RectTransform rect = Child(row, name, position);
        var image = rect.gameObject.AddComponent<Image>();
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        Child(rect, "Label", Vector2.zero).gameObject.AddComponent<Text>();
        return button;
    }

    private static Sprite LoadSprite(string path)
    {
        Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
        Require(sprite != null, "Missing actual HUD sprite at " + path);
        return sprite;
    }

    private static FieldInfo Field(object instance, string name) => instance.GetType().GetField(name, PrivateInstance)
        ?? throw new MissingFieldException(instance.GetType().FullName, name);
    private static void Set(object instance, string name, object value) => Field(instance, name).SetValue(instance, value);
    private static T Get<T>(object instance, string name) => (T)Field(instance, name).GetValue(instance);
    private static object Invoke(object instance, string name, params object[] arguments) =>
        (instance.GetType().GetMethod(name, PrivateInstance) ?? throw new MissingMethodException(instance.GetType().FullName, name))
        .Invoke(instance, arguments);
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Heart purchase regression: " + message);
    }
}
#endif
