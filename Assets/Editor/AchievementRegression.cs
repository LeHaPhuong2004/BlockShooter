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
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Checks lifetime progress and reward receipts with isolated memory storage.</summary>
[InitializeOnLoad]
public static class AchievementRegression
{
    private const string RequestPath = "Temp/AchievementRegression.request";
    private const string ReportPath = "Temp/AchievementRegression.result";
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static bool running;

    static AchievementRegression() => EditorApplication.update += ProcessRequest;

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

    [MenuItem("Tools/Home UI/Check Achievements")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Run achievement checks outside Play Mode after compilation.");
        var report = new StringBuilder();
        report.AppendLine(ValidateProgressAndClaims());
        report.AppendLine(ValidateInterruptedClaims());
        report.AppendLine(ValidateSavedSceneAchievementBinding());
        report.AppendLine("No PlayerPrefs, player wallet, gameplay event, IAP service, or authored scene save was used.");
        Directory.CreateDirectory("Temp");
        File.WriteAllText(ReportPath, report.ToString());
        Debug.Log(report.ToString());
    }

    public static string ValidateProgressAndClaims()
    {
        IReadOnlyList<AchievementDefinition> catalog = AchievementProgressStore.Definitions;
        Require(catalog.Count == 10 && catalog.Select(item => item.Id).Distinct().Count() == 10,
            "The achievement catalog must contain ten unique stable IDs.");
        Require(catalog.Select(item => item.Id).SequenceEqual(new[]
        {
            "blocks_100", "wins_3", "boosters_5", "blocks_1000", "wins_20",
            "boosters_25", "blocks_5000", "wins_100", "boosters_100", "blocks_20000"
        }), "The persisted achievement ID order changed.");
        Require(catalog.All(item => item.Target > 0 && item.Reward > 0 && !string.IsNullOrWhiteSpace(item.Title)),
            "Every achievement requires a positive target, reward and visible title.");
        Require(catalog.Select(item => item.Type).Distinct().Count() == 3,
            "Only block destruction, booster use and game wins should have achievements.");
        foreach (AchievementType category in Enum.GetValues(typeof(AchievementType)))
        {
            int[] targets = catalog.Where(item => item.Type == category).Select(item => item.Target).ToArray();
            Require(targets.Length >= 3 && targets.Zip(targets.Skip(1), (a, b) => a < b).All(value => value),
                "Targets must increase from easy to hard in category " + category + ".");
        }

        var memory = new MemorySave();
        var progress = memory.LoadStore();
        int gold = 137;
        int awards = 0;
        Action<int> award = amount => { gold += amount; awards++; };
        Require(progress.CompletedCount == 0 && progress.ClaimedCount == 0 && progress.ClaimableCount == 0 &&
            catalog.All(item => progress.GetProgress(item.Id) == 0 && !progress.IsComplete(item.Id) && !progress.CanClaim(item.Id) && !progress.IsClaimed(item.Id)),
            "New lifetime progress is not entirely locked and unclaimed.");
        Require(!progress.TryClaim("blocks_100", () => gold, award) &&
            !progress.TryClaim("missing", () => gold, award) && progress.GetProgress("missing") == 0 &&
            !progress.IsClaimed("missing") && !progress.CanClaim("missing") && gold == 137 && awards == 0 && memory.Writes == 0,
            "An incomplete or unknown achievement granted a reward or changed its save.");

        Repeat(99, progress.RecordBlockDestroyed);
        Require(!progress.IsComplete("blocks_100") && !progress.CanClaim("blocks_100") && progress.CompletedCount == 0,
            "A block achievement completed below its threshold.");
        progress.RecordBlockDestroyed();
        Require(progress.IsComplete("blocks_100") && progress.CanClaim("blocks_100") && !progress.CanClaim("blocks_1000") &&
            progress.GetProgress("blocks_20000") == 100 && progress.GetProgress("boosters_5") == 0,
            "Block totals did not reach exactly their own category thresholds.");
        Repeat(4, progress.RecordBoosterUsed);
        Repeat(2, progress.RecordGameWon);
        Require(!progress.CanClaim("boosters_5") && !progress.CanClaim("wins_3"),
            "A booster or win achievement completed below its threshold.");
        progress.RecordBoosterUsed();
        progress.RecordGameWon();
        Require(progress.CompletedCount == 3 && progress.ClaimedCount == 0 && progress.ClaimableCount == 3 &&
            progress.CanClaim("boosters_5") && progress.CanClaim("wins_3"),
            "Independent gameplay categories or completed/claimed summary counts are incorrect.");

        Require(progress.TryClaim("blocks_100", () => gold, award) && gold == 187 && awards == 1 &&
            progress.IsComplete("blocks_100") && progress.IsClaimed("blocks_100") && !progress.CanClaim("blocks_100") &&
            progress.CompletedCount == 3 && progress.ClaimedCount == 1 && progress.ClaimableCount == 2,
            "Claiming a complete achievement did not award exactly once or changed its lifetime completion.");
        progress = memory.LoadStore();
        int savesBeforeDuplicate = memory.Writes;
        Require(progress.GetProgress("blocks_1000") == 100 && progress.GetProgress("boosters_25") == 5 &&
            progress.GetProgress("wins_20") == 3 && progress.IsClaimed("blocks_100") &&
            !progress.TryClaim("blocks_100", () => gold, award) && awards == 1 && memory.Writes == savesBeforeDuplicate,
            "Reload lost lifetime progress or allowed a second reward.");

        Repeat(19901, progress.RecordBlockDestroyed);
        Repeat(96, progress.RecordBoosterUsed);
        Repeat(98, progress.RecordGameWon);
        Require(progress.CompletedCount == 10 && progress.GetProgress("blocks_100") == 20001 &&
            progress.GetProgress("boosters_5") == 101 && progress.GetProgress("wins_3") == 101,
            "Lifetime totals were capped at an individual target or failed to unlock the hardest achievements.");
        foreach (AchievementDefinition item in catalog)
        {
            bool alreadyClaimed = progress.IsClaimed(item.Id);
            Require(progress.TryClaim(item.Id, () => gold, award) == !alreadyClaimed,
                "Incorrect claim result for " + item.Id + ".");
        }
        Require(gold == 137 + catalog.Sum(item => item.Reward) && awards == 10 &&
            progress.CompletedCount == 10 && progress.ClaimedCount == 10 && progress.ClaimableCount == 0,
            "The ten rewards were not awarded exactly once with independent completed and claimed totals.");
        progress = memory.LoadStore();
        Require(catalog.All(item => progress.IsClaimed(item.Id) && !progress.CanClaim(item.Id) &&
            !progress.TryClaim(item.Id, () => gold, award)) && awards == 10,
            "Completed receipts did not survive a reload.");

        var rollbackMemory = new MemorySave();
        var rollback = rollbackMemory.LoadStore();
        rollbackMemory.FailWrite = 1;
        ExpectFailure(rollback.RecordBlockDestroyed);
        Require(rollback.GetProgress("blocks_100") == 0 && rollbackMemory.LoadStore().GetProgress("blocks_100") == 0,
            "A failed progress save changed lifetime totals in memory or on disk.");
        var saturated = new MemorySave
        {
            Json = "{\"version\":1,\"blocks\":2147483647,\"boosters\":2147483647,\"wins\":2147483647,\"claimedMask\":0}"
        };
        var maxProgress = saturated.LoadStore();
        maxProgress.RecordBlockDestroyed(); maxProgress.RecordBoosterUsed(); maxProgress.RecordGameWon();
        Require(maxProgress.CompletedCount == 10 && saturated.Writes == 0 &&
            catalog.All(item => maxProgress.GetProgress(item.Id) == int.MaxValue),
            "Lifetime counters overflowed or wrote an unchanged saturated total.");
        return "PASS: 10 stable achievements in three increasing categories; exact thresholds, independent lifetime totals, reload persistence, all ten one-time rewards, save rollback and overflow limits.";
    }

    public static string ValidateInterruptedClaims()
    {
        var beforeReceipt = ReadyStore();
        int beforeGold = 200;
        int beforeAwards = 0;
        var progress = beforeReceipt.LoadStore();
        beforeReceipt.FailWrite = beforeReceipt.Writes + 1;
        ExpectFailure(() => progress.TryClaim("blocks_100", () => beforeGold, amount => { beforeGold += amount; beforeAwards++; }));
        Require(beforeGold == 200 && beforeAwards == 0 && progress.CanClaim("blocks_100") &&
            !beforeReceipt.LoadStore().ResumePendingClaim(() => beforeGold, amount => beforeGold += amount),
            "Failure before writing a claim receipt granted or left an unverifiable reward.");

        foreach (int grantedBeforeFailure in new[] { 0, 20, 50 })
        {
            var memory = ReadyStore();
            int gold = 200;
            int granted = 0;
            progress = memory.LoadStore();
            ExpectFailure(() => progress.TryClaim("blocks_100", () => gold, amount =>
            {
                gold += grantedBeforeFailure;
                granted += grantedBeforeFailure;
                throw new IOException("Simulated interrupted wallet grant.");
            }));
            Require(gold == 200 + grantedBeforeFailure && !progress.IsClaimed("blocks_100"),
                "An interrupted grant was prematurely marked complete.");
            progress = memory.LoadStore();
            Require(progress.ResumePendingClaim(() => gold, amount => { gold += amount; granted += amount; }) &&
                gold == 250 && granted == 50 && progress.IsClaimed("blocks_100") &&
                !progress.ResumePendingClaim(() => gold, amount => gold += amount) &&
                !progress.TryClaim("blocks_100", () => gold, amount => gold += amount),
                "Recovery overpaid, lost or repeated a reward interrupted after " + grantedBeforeFailure + " gold.");
        }

        var afterWallet = ReadyStore();
        progress = afterWallet.LoadStore();
        int finalGold = 200;
        int finalAwards = 0;
        afterWallet.FailWrite = afterWallet.Writes + 2;
        ExpectFailure(() => progress.TryClaim("blocks_100", () => finalGold, amount => { finalGold += amount; finalAwards++; }));
        Require(finalGold == 250 && finalAwards == 1 && !progress.IsClaimed("blocks_100"),
            "A failed final receipt write did not preserve its pending reward.");
        progress = afterWallet.LoadStore();
        Require(progress.ResumePendingClaim(() => finalGold, amount => { finalGold += amount; finalAwards++; }) &&
            finalGold == 250 && finalAwards == 1 && progress.IsClaimed("blocks_100") &&
            afterWallet.LoadStore().IsClaimed("blocks_100"),
            "Recovery repeated the wallet grant after a final claim-save failure.");

        var reentrantMemory = ReadyStore();
        progress = reentrantMemory.LoadStore();
        int reentrantGold = 200;
        bool nestedClaim = true;
        bool nestedRecovery = true;
        Require(progress.TryClaim("blocks_100", () => reentrantGold, amount =>
        {
            nestedClaim = progress.TryClaim("blocks_100", () => reentrantGold, value => reentrantGold += value);
            nestedRecovery = progress.ResumePendingClaim(() => reentrantGold, value => reentrantGold += value);
            reentrantGold += amount;
        }) && !nestedClaim && !nestedRecovery && reentrantGold == 250 && progress.ClaimedCount == 1,
            "A wallet callback reentered and duplicated its pending claim.");
        return "PASS: interrupted claims before receipt, before/partway/after wallet grant and final save recover the exact reward once; repeated recovery and reentrant callbacks cannot duplicate it.";
    }

    public static string ValidateAuthoredScene(HomeExperienceUI home)
    {
        if (home == null) throw new ArgumentNullException(nameof(home));
        var all = home.GetComponentsInChildren<Transform>(true);
        var cards = all.Where(node => node.name.StartsWith("Achievement Card - ", StringComparison.Ordinal)).ToArray();
        Require(cards.Length == 10 && cards.All(node => node.gameObject.activeSelf),
            "The authored achievement page needs ten active cards.");
        var catalog = AchievementProgressStore.Definitions;
        Require(cards.Select(node => node.name).SequenceEqual(catalog.Select(item => "Achievement Card - " + item.Id)),
            "Authored achievement cards do not match the ten stable definitions in order.");
        MethodInfo action = typeof(HomeExperienceUI).GetMethod("AchievementButtonAction", PrivateInstance);
        Require(action != null, "AchievementButtonAction is missing.");
        foreach (AchievementDefinition item in catalog)
        {
            Transform card = cards.Single(node => node.name == "Achievement Card - " + item.Id);
            Require(Find<Text>(card, "Achievement Name").text == item.Title,
                "The achievement title is incorrect for " + item.Id + ".");
            Text reward = Find<Text>(card, "Reward Coin Amount");
            Require(int.TryParse(new string(reward.text.Where(char.IsDigit).ToArray()), out int parsedReward) && parsedReward == item.Reward,
                "The authored gold reward differs from the catalog for " + item.Id + ".");
            Button claim = Find<Button>(card, "CLAIM_ACHIEVEMENT_" + item.Id);
            Require(claim != null && claim.gameObject.activeSelf && !claim.interactable &&
                action.Invoke(home, new object[] { claim.name }) is Action,
                "A claim button is missing, initially unlocked, or has no runtime dispatch for " + item.Id + ".");
            Text progress = Find<Text>(card, "Achievement Progress Text");
            string digitsAndSlash = new string(progress.text.Where(character => char.IsDigit(character) || character == '/').ToArray());
            Require(digitsAndSlash == "0/" + item.Target,
                "Authored achievement progress must use a zero-data preview for " + item.Id + ".");
            Transform claimedCheck = Optional<Transform>(card, "Achievement Claimed Check");
            Transform lockedIcon = Optional<Transform>(card, "Achievement Lock Icon");
            Require((claimedCheck == null || !claimedCheck.gameObject.activeSelf) &&
                (lockedIcon == null || lockedIcon.gameObject.activeSelf),
                "Authored claim/lock icons are incorrect for the initial state of " + item.Id + ".");
        }
        Require(Find<Text>(home.transform, "Overall Achievement Count Current").text == "0" &&
            Find<Text>(home.transform, "Overall Achievement Count Total").text == "/10",
            "The authored summary must preview 0/10 completed achievements.");
        ScrollRect scroll = Find<ScrollRect>(home.transform, "Achievement List");
        Require(scroll != null && scroll.vertical && !scroll.horizontal && scroll.content != null && scroll.viewport != null &&
            cards.All(card => card.parent == scroll.content),
            "All ten achievement cards must share the vertical scroll content.");
        var cardRects = cards.Select(card => (RectTransform)card).ToArray();
        Require(cardRects.Select(rect => Mathf.RoundToInt(rect.anchorMax.y * 100000f)).Distinct().Count() == 5 &&
            cardRects.Select(rect => Mathf.RoundToInt(rect.anchorMin.x * 100000f)).Distinct().Count() == 2,
            "Ten achievement cards must occupy five compact rows.");
        var fitter = scroll.content.GetComponent<AspectRatioFitter>();
        Require(cardRects.All(rect => rect.anchorMax.x > rect.anchorMin.x && rect.anchorMax.y > rect.anchorMin.y &&
                rect.anchorMin.x >= 0f && rect.anchorMin.y >= 0f && rect.anchorMax.x <= 1f && rect.anchorMax.y <= 1f) &&
            fitter != null && fitter.aspectMode == AspectRatioFitter.AspectMode.WidthControlsHeight &&
            Mathf.Abs(fitter.aspectRatio - 941f / 2199f) < 0.0001f,
            "The authored scroll content must fit five complete rows at a stable aspect ratio.");
        ValidateIsolatedCardStates(home, cards);
        ValidateIsolatedAchievementScrolling(Find<Transform>(home.transform, "Achievements Page"));
        return "PASS: ten authored cards in five scrolling rows match the catalog and claim actions; isolated copies exercise every locked/ready/claimed state, clamped progress and summary counts. Real raycasts, wheel input and pointer dragging reach the final two cards without changing the authored scene.";
    }

    private static string ValidateSavedSceneAchievementBinding()
    {
        const string scenePath = "Assets/Scenes/Home.unity";
        byte[] savedScene = File.ReadAllBytes(scenePath);
        MethodInfo create = typeof(HomeExperienceUI).GetMethod("CreateAchievementCardView", PrivateInstance);
        MethodInfo bind = typeof(HomeExperienceUI).GetMethod("BindSceneAchievements", PrivateInstance);
        FieldInfo boundCards = typeof(HomeExperienceUI).GetField("achievementCards", PrivateInstance);
        Require(create != null && bind != null && boundCards != null,
            "The runtime achievement card binder is missing.");
        string authoredValidation;
        Scene preview = EditorSceneManager.OpenPreviewScene(scenePath);
        try
        {
            var home = preview.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<HomeExperienceUI>(true)).Single();
            authoredValidation = ValidateAuthoredScene(home);
            Canvas canvas = Find<Canvas>(home.transform, "Home Canvas");
            SetField(home, "homeCanvasObject", canvas.gameObject);
            Transform page = Find<Transform>(home.transform, "Achievements Page");
            int objectsBefore = page.GetComponentsInChildren<Transform>(true).Length;
            foreach (AchievementDefinition item in AchievementProgressStore.Definitions)
            {
                Transform card = Find<Transform>(page, "Achievement Card - " + item.Id);
                Require(create.Invoke(home, new object[] { card, item }) != null,
                    "The saved native achievement card could not be bound: " + item.Id + ".");
            }

            // Outside Play Mode AchievementManager returns its zero-data view
            // before accessing storage. This runs the actual page binder without
            // invoking Home.Start, IAP, gameplay callbacks or the player wallet.
            Require(!Application.isPlaying, "Saved scene binding must run outside Play Mode.");
            bind.Invoke(home, null);
            Require(((System.Collections.ICollection)boundCards.GetValue(home)).Count == 10,
                "The saved native achievement page did not bind all ten cards.");
            Require(page.GetComponentsInChildren<Transform>(true).Length == objectsBefore,
                "Binding the saved page recreated removed achievement decorations.");

            foreach (Transform decoration in page.GetComponentsInChildren<Transform>(true)
                .Where(node => node.name == "Achievement Claimed Check" ||
                    node.name == "Achievement Lock Icon").ToArray())
                UnityEngine.Object.DestroyImmediate(decoration.gameObject);
            int objectsWithoutDecorations = page.GetComponentsInChildren<Transform>(true).Length;
            bind.Invoke(home, null);
            Require(((System.Collections.ICollection)boundCards.GetValue(home)).Count == 10 &&
                page.GetComponentsInChildren<Transform>(true).Length == objectsWithoutDecorations,
                "The saved page could not bind without optional check/lock decorations.");
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
        Require(File.ReadAllBytes(scenePath).SequenceEqual(savedScene),
            "The saved Home scene changed during the isolated reload binding check.");
        return authoredValidation + Environment.NewLine +
            "PASS: reloaded saved Home.unity in an isolated preview; actual runtime view creation and achievement page binding accept all ten native cards, with or without optional check/lock decorations. No decorations were recreated and the scene file was unchanged.";
    }

    private static void ValidateIsolatedAchievementScrolling(Transform page)
    {
        EventSystem previousEventSystem = EventSystem.current;
        UnityEngine.Random.State previousRandom = UnityEngine.Random.state;
        Scene preview = EditorSceneManager.NewPreviewScene();
        RenderTexture target = null;
        RenderTexture previousTarget = RenderTexture.active;
        try
        {
            var cameraObject = new GameObject("Isolated Achievement Scroll Camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, preview);
            var camera = cameraObject.GetComponent<Camera>();
            camera.enabled = false;
            camera.scene = preview;
            camera.orthographic = true;
            camera.orthographicSize = 1672f * 0.5f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 1000f;
            camera.transform.position = new Vector3(0f, 0f, -100f);
            camera.cullingMask = 1 << 31;
            target = new RenderTexture(941, 1672, 24, RenderTextureFormat.ARGB32);
            target.Create();
            camera.targetTexture = target;
            var canvasObject = new GameObject("Isolated Achievement Scroll Regression",
                typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            canvasObject.SetActive(false);
            SceneManager.MoveGameObjectToScene(canvasObject, preview);
            canvasObject.layer = 31;
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10f;
            var copy = UnityEngine.Object.Instantiate(page.gameObject, canvasObject.transform, false)
                .GetComponent<RectTransform>();
            copy.anchorMin = Vector2.zero;
            copy.anchorMax = Vector2.one;
            copy.offsetMin = copy.offsetMax = Vector2.zero;
            copy.gameObject.SetActive(true);
            foreach (var node in copy.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 31;
            var eventObject = new GameObject("Isolated Achievement Event System", typeof(EventSystem));
            SceneManager.MoveGameObjectToScene(eventObject, preview);
            var events = eventObject.GetComponent<EventSystem>();
            EventSystem.current = events;
            canvasObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            Canvas.ForceUpdateCanvases();

            ScrollRect scroll = Find<ScrollRect>(copy, "Achievement List");
            var cards = scroll.content.GetComponentsInChildren<RectTransform>(true)
                .Where(rect => rect.name.StartsWith("Achievement Card - ", StringComparison.Ordinal)).ToArray();
            Require(cards.Length == 10 && scroll.isActiveAndEnabled &&
                scroll.content.rect.height > scroll.viewport.rect.height + 1f,
                "The achievement list must be active and taller than its viewport to reach ten cards.");
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1f;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            var raycaster = canvasObject.GetComponent<GraphicRaycaster>();
            Vector3 firstCenter = cards[0].TransformPoint(cards[0].rect.center);
            Vector3 secondCenter = cards[1].TransformPoint(cards[1].rect.center);
            PointerEventData pointer = AchievementScrollPointer(events, raycaster, scroll,
                RectTransformUtility.WorldToScreenPoint(camera, firstCenter));
            AchievementScrollPointer(events, raycaster, scroll,
                RectTransformUtility.WorldToScreenPoint(camera, (firstCenter + secondCenter) * 0.5f));

            Vector2 beforeWheel = scroll.content.anchoredPosition;
            pointer.scrollDelta = Vector2.down;
            scroll.OnScroll(pointer);
            Require(scroll.content.anchoredPosition.y > beforeWheel.y + 1f,
                "Mouse wheel input did not move the achievement list toward the hidden cards.");

            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1f;
            Canvas.ForceUpdateCanvases();
            Vector2 beforeDrag = scroll.content.anchoredPosition;
            float distance = scroll.content.rect.height - scroll.viewport.rect.height + 8f;
            Vector2 screenOrigin = RectTransformUtility.WorldToScreenPoint(camera,
                scroll.viewport.TransformPoint(Vector3.zero));
            Vector2 screenEnd = RectTransformUtility.WorldToScreenPoint(camera,
                scroll.viewport.TransformPoint(Vector3.up * distance));
            pointer.pressPosition = pointer.position;
            pointer.button = PointerEventData.InputButton.Left;
            scroll.OnInitializePotentialDrag(pointer);
            scroll.OnBeginDrag(pointer);
            pointer.delta = screenEnd - screenOrigin;
            pointer.position += pointer.delta;
            scroll.OnDrag(pointer);
            scroll.OnEndDrag(pointer);
            scroll.StopMovement();
            Canvas.ForceUpdateCanvases();
            Require(scroll.content.anchoredPosition.y > beforeDrag.y + 1f &&
                scroll.verticalNormalizedPosition < 0.001f,
                "Pointer dragging could not reach the bottom of the achievement list.");
            var corners = new Vector3[4];
            foreach (RectTransform card in cards.Skip(8))
            {
                card.GetWorldCorners(corners);
                Require(corners.All(corner =>
                {
                    Vector3 local = scroll.viewport.InverseTransformPoint(corner);
                    Rect visible = scroll.viewport.rect;
                    return local.x >= visible.xMin - 1f && local.x <= visible.xMax + 1f &&
                        local.y >= visible.yMin - 1f && local.y <= visible.yMax + 1f;
                }), "The final achievement card remains clipped after dragging: " + card.name + ".");
            }
        }
        finally
        {
            RenderTexture.active = previousTarget;
            EditorSceneManager.ClosePreviewScene(preview);
            if (target != null)
            {
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
            EventSystem.current = previousEventSystem;
            UnityEngine.Random.state = previousRandom;
        }
    }

    private static PointerEventData AchievementScrollPointer(EventSystem events, GraphicRaycaster raycaster,
        ScrollRect scroll, Vector2 position)
    {
        var pointer = new PointerEventData(events) { position = position };
        var hits = new List<RaycastResult>();
        raycaster.Raycast(pointer, hits);
        if (hits.Count == 0 || hits[0].gameObject.GetComponentInParent<ScrollRect>() != scroll)
        {
            var canvas = raycaster.GetComponent<Canvas>();
            string hitNames = string.Join(", ", hits.Select(hit => hit.gameObject.name + " parent=" +
                (hit.gameObject.transform.parent != null ? hit.gameObject.transform.parent.name : "none")));
            string graphics = string.Join("; ", canvas.GetComponentsInChildren<Graphic>(true)
                .Where(graphic => graphic.raycastTarget).Take(16).Select(graphic => graphic.name +
                    " active=" + graphic.isActiveAndEnabled + " depth=" + graphic.depth +
                    " cull=" + graphic.canvasRenderer.cull + " rect=" + graphic.rectTransform.rect +
                    " world=" + graphic.transform.position));
            throw new InvalidOperationException("A card or transparent gap intercepts achievement list scrolling. " +
                "point=" + position + " screen=" + Screen.width + "x" + Screen.height +
                " canvas=" + canvas.pixelRect + " viewport=" + scroll.viewport.rect +
                " hits=" + hits.Count + " [" + hitNames + "] graphics=[" + graphics + "]");
        }
        pointer.pointerCurrentRaycast = pointer.pointerPressRaycast = hits[0];
        return pointer;
    }

    private static void ValidateIsolatedCardStates(HomeExperienceUI home, Transform[] cards)
    {
        MethodInfo create = typeof(HomeExperienceUI).GetMethod("CreateAchievementCardView", PrivateInstance);
        MethodInfo apply = typeof(HomeExperienceUI).GetMethod("ApplyAchievementCardState", BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo summary = typeof(HomeExperienceUI).GetMethod("ApplyAchievementSummary", PrivateInstance);
        Require(create != null && apply != null && summary != null,
            "The actual achievement card/summary state renderers are missing.");
        Scene preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject("Isolated Achievement State Regression");
            root.SetActive(false);
            SceneManager.MoveGameObjectToScene(root, preview);
            foreach (AchievementDefinition item in AchievementProgressStore.Definitions)
            {
                Transform card = cards.Single(node => node.name == "Achievement Card - " + item.Id);
                Transform copy = UnityEngine.Object.Instantiate(card.gameObject, root.transform, false).transform;
                object view = create.Invoke(home, new object[] { copy, item });
                Button claim = Find<Button>(copy, "CLAIM_ACHIEVEMENT_" + item.Id);
                Text claimLabel = Find<Text>(claim.transform, "Label");
                Text progress = Find<Text>(copy, "Achievement Progress Text");
                RectTransform fill = Find<RectTransform>(copy, "Achievement Progress Fill");
                GameObject check = Optional<Transform>(copy, "Achievement Claimed Check")?.gameObject;
                GameObject locked = Optional<Transform>(copy, "Achievement Lock Icon")?.gameObject;

                apply.Invoke(null, new[] { view, (object)(item.Target - 1), false, false });
                Require(!claim.interactable && string.IsNullOrEmpty(claimLabel.text) &&
                    (locked == null || locked.activeSelf) && (check == null || !check.activeSelf) &&
                    DigitsAndSlash(progress.text) == (item.Target - 1) + "/" + item.Target,
                    "Locked card state is incorrect for " + item.Id + ".");
                apply.Invoke(null, new[] { view, (object)item.Target, false, true });
                Require(claim.interactable && claimLabel.text == "NHẬN" &&
                    (locked == null || !locked.activeSelf) && (check == null || !check.activeSelf) &&
                    DigitsAndSlash(progress.text) == item.Target + "/" + item.Target &&
                    Mathf.Abs(fill.anchorMax.x - 0.975f) < 0.0001f,
                    "Ready card state is incorrect at the exact target for " + item.Id + ".");
                apply.Invoke(null, new[] { view, (object)(item.Target + 123456), false, true });
                Require(claim.interactable && DigitsAndSlash(progress.text) == item.Target + "/" + item.Target &&
                    Mathf.Abs(fill.anchorMax.x - 0.975f) < 0.0001f,
                    "Progress text or fill overflows its target for " + item.Id + ".");
                apply.Invoke(null, new[] { view, (object)item.Target, true, false });
                Require(!claim.interactable && claimLabel.text == "ĐÃ NHẬN" &&
                    (locked == null || !locked.activeSelf) && (check == null || check.activeSelf),
                    "Claimed card state did not disable repeat claims for " + item.Id + ".");
                apply.Invoke(null, new[] { view, (object)(-1), false, true });
                Require(!claim.interactable && (locked == null || locked.activeSelf) && (check == null || !check.activeSelf) &&
                    DigitsAndSlash(progress.text) == "0/" + item.Target && Mathf.Abs(fill.anchorMax.x - 0.025f) < 0.0001f,
                    "A negative/below-target progress value unlocked or underflowed the card for " + item.Id + ".");

                // These decorations are optional in the authored layout. Exercise
                // the actual renderer after all of them have been removed from a
                // disposable copy, including ready and already-claimed states.
                if (check != null) UnityEngine.Object.DestroyImmediate(check);
                if (locked != null) UnityEngine.Object.DestroyImmediate(locked);
                view = create.Invoke(home, new object[] { copy, item });
                apply.Invoke(null, new[] { view, (object)item.Target, false, true });
                Require(claim.interactable && claimLabel.text == "NHẬN",
                    "Removing optional decorations prevented a ready claim for " + item.Id + ".");
                apply.Invoke(null, new[] { view, (object)item.Target, true, false });
                Require(!claim.interactable && claimLabel.text == "ĐÃ NHẬN",
                    "Removing optional decorations prevented a claimed state for " + item.Id + ".");
            }

            var isolatedHome = root.AddComponent<HomeExperienceUI>();
            Text count = NewText(root.transform, "Isolated Count");
            Text total = NewText(root.transform, "Isolated Total");
            var summaryFill = new GameObject("Isolated Summary Fill", typeof(RectTransform)).GetComponent<RectTransform>();
            summaryFill.SetParent(root.transform, false);
            SetField(isolatedHome, "achievementCountCurrent", count);
            SetField(isolatedHome, "achievementCountTotal", total);
            SetField(isolatedHome, "achievementSummaryFill", summaryFill);
            foreach (int completed in new[] { -1, 0, 5, 10, 100 })
            {
                summary.Invoke(isolatedHome, new object[] { completed });
                int expected = Mathf.Clamp(completed, 0, 10);
                Require(count.text == expected.ToString() && total.text == "/10" &&
                    Mathf.Abs(summaryFill.anchorMax.x - (0.025f + expected / 10f * 0.95f)) < 0.0001f,
                    "The completion summary did not clamp/render " + completed + " completed achievements.");
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }

    private static Text NewText(Transform parent, string name)
    {
        var label = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        label.transform.SetParent(parent, false);
        return label;
    }

    private static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, PrivateInstance);
        Require(field != null, "Missing isolated state field " + name + ".");
        field.SetValue(target, value);
    }

    private static string DigitsAndSlash(string value) =>
        new string(value.Where(character => char.IsDigit(character) || character == '/').ToArray());

    private static MemorySave ReadyStore()
    {
        var memory = new MemorySave();
        var store = memory.LoadStore();
        Repeat(100, store.RecordBlockDestroyed);
        return memory;
    }

    private sealed class MemorySave
    {
        public string Json;
        public int Writes;
        public int FailWrite = -1;
        public AchievementProgressStore LoadStore() => new AchievementProgressStore(() => Json, value =>
        {
            Writes++;
            if (Writes == FailWrite) throw new IOException("Simulated achievement persistence failure.");
            Json = value;
        });
    }

    private static T Find<T>(Transform root, string name) where T : Component
    {
        T component = Optional<T>(root, name);
        Require(component != null, "Missing authored " + name + ".");
        return component;
    }

    private static T Optional<T>(Transform root, string name) where T : Component =>
        root.GetComponentsInChildren<Transform>(true)
            .Where(node => node.name == name).Select(node => node.GetComponent<T>()).SingleOrDefault();

    private static void Repeat(int count, Action action) { for (int i = 0; i < count; i++) action(); }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void ExpectFailure(Action action)
    {
        try { action(); }
        catch (IOException) { return; }
        throw new InvalidOperationException("The simulated persistence/grant interruption did not propagate.");
    }
}
#endif
