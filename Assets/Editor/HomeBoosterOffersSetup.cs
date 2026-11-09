#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Authors only the existing Booster shop content; keeps the user's tabs and other pages.</summary>
[InitializeOnLoad]
public static class HomeBoosterOffersSetup
{
    private const string ScenePath = "Assets/Scenes/Home.unity";
    private const string AtlasPath = "Assets/UI/shop-parts-v3.png";
    private const string RequestPath = "Assets/Editor/HomeBoosterOffersSetup.request";
    private const string ReportPath = "Temp/HomeBoosterOffersSetup.result";
    private const string OutputDirectory = "output/shop-extraction";
    private static bool running;

    static HomeBoosterOffersSetup() => EditorApplication.update += ProcessRequest;

    private static void ProcessRequest()
    {
        if (running || !File.Exists(RequestPath) || EditorApplication.isCompiling ||
            EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        running = true;
        try { Prepare(); }
        catch (Exception exception)
        {
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

    [MenuItem("Tools/Home UI/Install Booster Offer Packages")]
    public static void Prepare()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before authoring the Booster offers.");
        var sprites = AssetDatabase.LoadAllAssetsAtPath(AtlasPath).OfType<Sprite>()
            .ToDictionary(sprite => sprite.name);
        for (int i = 0; i <= 12; i++)
            if (!sprites.ContainsKey("shop-parts-v3_" + i))
                throw new InvalidOperationException("Missing authored atlas slice shop-parts-v3_" + i);

        Directory.CreateDirectory(OutputDirectory);
        string backup = OutputDirectory + "/Home-before-booster-offers.unity.backup";
        if (!File.Exists(backup)) File.Copy(ScenePath, backup);
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            var home = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<HomeExperienceUI>(true)).Single();
            var viewport = home.GetComponentsInChildren<ScrollRect>(true)
                .Single(scroll => scroll.name == "Booster Shop Viewport");
            var content = viewport.content;
            if (content == null || viewport.viewport == null)
                throw new InvalidOperationException("Existing Booster scroll content or viewport is missing.");

            Undo.RegisterFullObjectHierarchyUndo(content.gameObject, "Install Booster offer packages");
            var directPurchases = PreserveDirectPurchases(viewport, backup);
            ArchiveOriginalOfferTemplates(content, directPurchases);
            var packages = content.Find("Booster Offer Packages") as RectTransform;
            if (packages == null)
            {
                packages = new GameObject("Booster Offer Packages", typeof(RectTransform))
                    .GetComponent<RectTransform>();
                packages.SetParent(content, false);
                Stretch(packages);
                var sourceFont = HomeUIFont.LoadSourceFont();
                for (int i = 0; i < HomeExperienceUI.BoosterOffers.Count; i++)
                    BuildRow(packages, i, sprites, sourceFont);
            }
            packages.SetAsFirstSibling();
            directPurchases.SetAsLastSibling();

            var layout = content.GetComponent<BoosterOfferListLayout>();
            if (layout == null) layout = content.gameObject.AddComponent<BoosterOfferListLayout>();
            content.anchoredPosition = Vector2.zero;
            layout.Configure(viewport, packages, directPurchases);
            Canvas.ForceUpdateCanvases();
            layout.RefreshLayout();
            viewport.horizontal = false;
            viewport.vertical = true;
            viewport.verticalNormalizedPosition = 1f;
            viewport.scrollSensitivity = Mathf.Max(viewport.scrollSensitivity, 35f);
            viewport.StopMovement();
            string validation = HomeBoosterOffersCheck.Validate(home);
            EditorUtility.SetDirty(content);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Unity could not save the Booster packages in Home.unity.");
            string result = "PASS: Five offer packages followed by all " +
                directPurchases.GetComponentsInChildren<Button>(true).Length + " original direct Booster purchase controls.\n" + validation;
            File.WriteAllText(ReportPath, result);
            Debug.Log(result);
            try { HomeBoosterOffersCheck.CapturePreview(home, OutputDirectory + "/booster-tab-preview.png"); }
            catch (Exception exception) { Debug.LogWarning("Booster preview capture: " + exception.Message); }
        }
        finally { if (openedHere) EditorSceneManager.CloseScene(scene, true); }
    }

    private static void ArchiveOriginalOfferTemplates(RectTransform content, RectTransform directPurchases)
    {
        // The backup also contains the user's empty offer mockups. Keep these objects
        // in the scene, hidden, while the completed offer rows are shown above the shop cards.
        var oldArt = directPurchases.GetComponentsInChildren<Image>(true)
            .Where(image => image.sprite != null && AssetDatabase.GetAssetPath(image.sprite) == AtlasPath).ToArray();
        if (oldArt.Length == 0) return;
        var archive = content.Find("Original Booster Offer Templates") as RectTransform;
        if (archive == null)
        {
            archive = new GameObject("Original Booster Offer Templates", typeof(RectTransform)).GetComponent<RectTransform>();
            archive.SetParent(content, false);
            archive.anchorMin = archive.anchorMax = archive.pivot = new Vector2(0f, 1f);
            archive.sizeDelta = directPurchases.rect.size;
        }
        archive.gameObject.SetActive(false);
        foreach (var image in oldArt)
            if (image.transform.IsChildOf(directPurchases)) image.transform.SetParent(archive, false);
    }

    private static RectTransform PreserveDirectPurchases(ScrollRect viewport, string backup)
    {
        var content = viewport.content;
        var existing = content.Find("Booster Direct Purchases") as RectTransform;
        Canvas.ForceUpdateCanvases();
        if (content.Find("Shop Card Items") != null)
        {
            // When installing on an untouched shop, move the existing objects together.
            Vector2 authoredSize = content.rect.size;
            var originalChildren = Enumerable.Range(0, content.childCount)
                .Select(index => content.GetChild(index))
                .Where(child => child.name != "Booster Offer Packages").ToArray();
            var group = new GameObject("Booster Direct Purchases", typeof(RectTransform))
                .GetComponent<RectTransform>();
            group.SetParent(content, false);
            group.anchorMin = group.anchorMax = group.pivot = new Vector2(0f, 1f);
            group.sizeDelta = authoredSize;
            foreach (var child in originalChildren) child.SetParent(group, false);
            return group;
        }

        // Recover the exact authored subtree removed by the first installation.
        if (!File.Exists(backup))
            throw new FileNotFoundException("Original Booster purchase scene backup is missing.", backup);
        const string recoveryPath = "Assets/Editor/__BoosterOffersRecoverySource.unity";
        if (File.Exists(recoveryPath))
            throw new IOException("Temporary Booster recovery scene already exists: " + recoveryPath);
        Scene sourceScene = default;
        bool created = false;
        try
        {
            File.Copy(backup, recoveryPath);
            created = true;
            AssetDatabase.ImportAsset(recoveryPath, ImportAssetOptions.ForceSynchronousImport);
            sourceScene = EditorSceneManager.OpenPreviewScene(recoveryPath);
            var sourceHome = sourceScene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<HomeExperienceUI>(true)).Single();
            var sourceScroll = sourceHome.GetComponentsInChildren<ScrollRect>(true)
                .Single(scroll => scroll.name == "Booster Shop Viewport");
            // A preview scene has no Game View to size a screen-space canvas.
            // Resolve the backup at its authored reference size; the fixed-size original
            // cards will be scaled together to the live viewport after restoration.
            var sourceCanvas = sourceScroll.GetComponentInParent<Canvas>(true);
            var liveCanvas = viewport.GetComponentInParent<Canvas>(true);
            if (sourceCanvas != null && liveCanvas != null)
            {
                sourceCanvas.renderMode = RenderMode.WorldSpace;
                var scaler = sourceCanvas.GetComponent<CanvasScaler>();
                ((RectTransform)sourceCanvas.transform).sizeDelta = scaler != null
                    ? scaler.referenceResolution : ((RectTransform)liveCanvas.transform).rect.size;
                sourceCanvas.enabled = true;
            }
            for (Transform ancestor = sourceScroll.transform; ancestor != null; ancestor = ancestor.parent)
                ancestor.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            sourceScroll.content.ForceUpdateRectTransforms();
            Vector2 authoredSize = sourceScroll.content.rect.size;
            if (authoredSize.x <= 1f || authoredSize.y <= 1f ||
                sourceScroll.content.Find("Shop Card Items") == null ||
                sourceScroll.content.Find("Shop Card Frames") == null)
                throw new InvalidOperationException("The backup has no complete original Booster purchase content. Size: " +
                    authoredSize + "; children: " + string.Join(", ", sourceScroll.content.Cast<Transform>().Select(child => child.name)));

            var group = existing;
            if (group == null)
            {
                var recovered = UnityEngine.Object.Instantiate(sourceScroll.content.gameObject, content, false);
                recovered.name = "Booster Direct Purchases";
                Undo.RegisterCreatedObjectUndo(recovered, "Restore original Booster purchases");
                group = recovered.GetComponent<RectTransform>();
                group.anchorMin = group.anchorMax = group.pivot = new Vector2(0f, 1f);
                group.sizeDelta = authoredSize;
                group.anchoredPosition = Vector2.zero;
                group.localScale = Vector3.one;
                group.localRotation = Quaternion.identity;
                recovered.SetActive(true);
            }
            CheckRecovery(sourceScroll.content, group, sourceScene);
            group.sizeDelta = authoredSize;
            return group;
        }
        finally
        {
            if (sourceScene.IsValid()) EditorSceneManager.ClosePreviewScene(sourceScene);
            if (created) AssetDatabase.DeleteAsset(recoveryPath);
        }
    }

    private static void CheckRecovery(RectTransform source, RectTransform recovered, Scene sourceScene)
    {
        string comparison = HomeBoosterOffersCheck.ValidateDirectPurchasesAgainstBackup(recovered, source);
        var restoredTexts = recovered.GetComponentsInChildren<Text>(true);
        var restoredImages = recovered.GetComponentsInChildren<Image>(true);
        foreach (var component in recovered.GetComponentsInChildren<Component>(true))
        {
            if (component == null) continue;
            var serialized = new SerializedObject(component);
            var property = serialized.GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                var value = property.objectReferenceValue;
                GameObject referencedObject = value is Component target ? target.gameObject : value as GameObject;
                if (referencedObject != null && referencedObject.scene == sourceScene)
                    throw new InvalidOperationException("Recovered purchase retains a reference to the temporary backup: " + component.name);
            }
        }
        File.WriteAllText("Temp/HomeBoosterDirectRecovery.result", comparison + "\n" +
            "Preserved " + restoredTexts.Length + " text labels, " + restoredImages.Length + " images and " +
            recovered.GetComponentsInChildren<Button>(true).Length + " purchase buttons; no temporary scene references.");
    }

    private static void BuildRow(Transform parent, int index,
        System.Collections.Generic.IDictionary<string, Sprite> sprites, Font font)
    {
        var offer = HomeExperienceUI.BoosterOffers[index];
        var row = Image(parent, "Booster Offer " + (index + 1), sprites["shop-parts-v3_" + index]);
        row.preserveAspect = true;
        Color outline = index == 4 ? new Color32(142, 0, 149, 255) : new Color32(15, 5, 151, 255);
        Text(row.transform, "Offer Title", offer.title, new Rect(24, 6, 604, 49), 36,
            font, outline, TextAnchor.MiddleLeft);
        Text(row.transform, "Coin Amount", offer.coins.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN")) + " VÀNG",
            new Rect(16, 156, 178, 30), 26, font, new Color32(181, 0, 12, 255));

        float[] centers = index < 2 ? new[] { 230f, 325f, 425f, 523f } : new[] { 225f, 320f, 414f, 510f };
        for (int j = 0; j < 4; j++)
            Text(row.transform, "Booster Quantity " + (j + 1), "x" + offer.boosterCount,
                new Rect(centers[j] - 37.5f, 156, 75, 30), 29, font, new Color32(15, 5, 151, 255));
        if (offer.unlimitedHeart)
            Text(row.transform, "Unlimited Heart Caption", offer.unlimitedHeartSeconds >= 3600
                    ? (offer.unlimitedHeartSeconds / 3600) + " GIỜ" : (offer.unlimitedHeartSeconds / 60) + " PHÚT",
                new Rect(544, 155, 110, 31), 23,
                font, new Color32(181, 0, 12, 255));

        var badge = Image(row.transform, "Discount Badge", sprites["shop-parts-v3_" + (9 - index)]);
        SetRect(badge.rectTransform, new Rect(653, 5, 87, 78));
        var oldPrice = Image(row.transform, "Old Price", sprites["shop-parts-v3_11"]);
        SetRect(oldPrice.rectTransform, new Rect(645, 77, 166, 44));
        var oldLabel = Text(oldPrice.transform, "Old Price Label", Price(offer.oldPrice), new Rect(0, 0, 830, 190),
            25, font, new Color32(36, 14, 75, 210));
        Stretch(oldLabel.rectTransform);
        oldLabel.color = new Color32(238, 234, 255, 255);
        var strike = Image(oldPrice.transform, "Old Price Strike", sprites["shop-parts-v3_12"]);
        strike.preserveAspect = false;
        strike.rectTransform.anchorMin = new Vector2(0.02f, 0.29f);
        strike.rectTransform.anchorMax = new Vector2(0.98f, 0.71f);
        strike.rectTransform.offsetMin = strike.rectTransform.offsetMax = Vector2.zero;

        var buy = Image(row.transform, "BUY_BOOSTER_OFFER_" + (index + 1), sprites["shop-parts-v3_10"]);
        SetRect(buy.rectTransform, new Rect(639, 116, 177, 68));
        buy.raycastTarget = true;
        var button = buy.gameObject.AddComponent<Button>();
        button.targetGraphic = buy;
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        var price = Text(buy.transform, "Label", Price(offer.price), new Rect(0, 0, 830, 190), 31,
            font, new Color32(0, 112, 0, 255));
        price.rectTransform.anchorMin = new Vector2(0.05f, 0.08f);
        price.rectTransform.anchorMax = new Vector2(0.95f, 0.92f);
        price.rectTransform.offsetMin = price.rectTransform.offsetMax = Vector2.zero;
    }

    private static string Price(int amount) =>
        amount.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN"));

    private static Image Image(Transform parent, string name, Sprite sprite)
    {
        var gameObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        gameObject.transform.SetParent(parent, false);
        var image = gameObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = Color.white;
        image.type = UnityEngine.UI.Image.Type.Simple;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private static Text Text(Transform parent, string name, string value, Rect bounds, int size,
        Font font, Color outline, TextAnchor alignment = TextAnchor.MiddleCenter)
    {
        var gameObject = new GameObject(name, typeof(RectTransform), typeof(Text));
        gameObject.transform.SetParent(parent, false);
        var label = gameObject.GetComponent<Text>();
        SetRect(label.rectTransform, bounds);
        label.text = value;
        label.font = font;
        label.fontSize = size;
        label.fontStyle = FontStyle.Bold;
        label.color = Color.white;
        label.alignment = alignment;
        label.raycastTarget = false;
        label.supportRichText = false;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        var edge = gameObject.AddComponent<Outline>();
        edge.effectColor = outline;
        edge.effectDistance = new Vector2(1.6f, -1.6f);
        HomeUIFont.ConfigureText(label, font, true);
        label.GetComponent<HomeTextFit>().SetMaximumFontSize(size, true);
        return label;
    }

    private static void SetRect(RectTransform rect, Rect source)
    {
        rect.anchorMin = new Vector2(source.xMin / 830f, 1f - source.yMax / 190f);
        rect.anchorMax = new Vector2(source.xMax / 830f, 1f - source.yMin / 190f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
#endif
