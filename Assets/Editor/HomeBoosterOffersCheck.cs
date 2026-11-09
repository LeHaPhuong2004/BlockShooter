#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Checks the authored booster offers and retained direct purchases without changing Home.</summary>
public static class HomeBoosterOffersCheck
{
    private const string AtlasPath = "Assets/UI/shop-parts-v3.png";
    private const string AtlasGuid = "0cf5c810df2aecf4aa0b6d2b3f9da702";
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    public static string Validate(HomeExperienceUI home)
    {
        if (home == null) throw new ArgumentNullException(nameof(home));
        ScrollRect scroll = FindScroll(home);
        var packages = scroll.content.Find("Booster Offer Packages") as RectTransform;
        Require(packages != null, "Booster Offer Packages is missing from booster scroll content.");
        Require(AssetDatabase.AssetPathToGUID(AtlasPath) == AtlasGuid, "The offer source atlas GUID changed.");
        Require(packages.childCount == 5, "Expected exactly five booster package rows.");
        MethodInfo actionForButton = typeof(HomeExperienceUI).GetMethod("SceneButtonAction", PrivateInstance);
        Require(actionForButton != null, "SceneButtonAction is missing.");

        var report = new StringBuilder();
        float previousBottom = float.PositiveInfinity;
        int checkedLabels = 0;
        for (int index = 0; index < 5; index++)
        {
            string rowName = "Booster Offer " + (index + 1);
            var row = packages.GetChild(index) as RectTransform;
            Require(row != null && row.name == rowName, "Offer rows must appear in order: " + rowName);
            Require(row.gameObject.activeSelf, rowName + " is inactive.");
            Require(row.localScale == Vector3.one, rowName + " has a stretched transform scale.");
            Image frame = row.GetComponent<Image>();
            ValidateAtlasImage(frame, index, rowName + " frame");
            Require(row.rect.width > 1f && row.rect.height > 1f, rowName + " has no resolved size.");
            float expectedAspect = frame.sprite.rect.width / frame.sprite.rect.height;
            Require(Mathf.Abs(row.rect.width / row.rect.height - expectedAspect) < 0.015f,
                rowName + " no longer matches its original frame aspect ratio.");
            Require(row.rect.width <= scroll.content.rect.width + 1f, rowName + " exceeds its scroll content width.");

            var definition = HomeExperienceUI.BoosterOffers[index];
            Text title = Label(row, "Offer Title");
            Require(title.text == definition.title, rowName + " title does not match its offer definition.");
            ValidateNumber(Label(row, "Coin Amount"), definition.coins, rowName + " coin amount");
            for (int booster = 1; booster <= 4; booster++)
            {
                Text quantity = Label(row, "Booster Quantity " + booster);
                string compact = quantity.text.Replace(" ", string.Empty).Replace("\u00d7", "x").ToLowerInvariant();
                Require(compact == "x" + definition.boosterCount, rowName + " has an incorrect booster quantity.");
            }
            Transform heart = row.Find("Unlimited Heart Caption");
            Require((heart != null) == definition.unlimitedHeart, rowName + " unlimited-heart caption is incorrect.");
            if (heart != null)
            {
                string duration = definition.unlimitedHeartSeconds >= 3600
                    ? (definition.unlimitedHeartSeconds / 3600) + " GIỜ"
                    : (definition.unlimitedHeartSeconds / 60) + " PHÚT";
                Require(Label(row, "Unlimited Heart Caption").text == duration, rowName + " heart duration is incorrect.");
            }
            ValidateAtlasImage(ImageAt(row, "Discount Badge"), 9 - index, rowName + " discount badge");

            Transform oldPrice = row.Find("Old Price");
            Require(oldPrice != null && oldPrice.GetComponent<Image>() != null, rowName + " old-price holder is missing.");
            ValidateNumber(Label(oldPrice, "Old Price Label"), definition.oldPrice, rowName + " old price");
            Require(ImageAt(oldPrice, "Old Price Strike").sprite != null, rowName + " old-price strike has no sprite.");

            Transform buyTransform = row.Find("BUY_BOOSTER_OFFER_" + (index + 1));
            Require(buyTransform != null, rowName + " buy control is missing.");
            Button buy = buyTransform.GetComponent<Button>();
            Require(buy != null && buy.enabled && buy.interactable, rowName + " buy button cannot be clicked.");
            Require(buy.targetGraphic != null && buy.targetGraphic.raycastTarget, rowName + " buy button has no raycast target.");
            ValidateNumber(Label(buyTransform, "Label"), definition.price, rowName + " purchase price");
            Require(actionForButton.Invoke(home, new object[] { buy }) is Action,
                rowName + " has no runtime button action.");
            Require(row.GetComponentsInChildren<Button>(true).Length == 1, rowName + " has unexpected buttons.");
            foreach (Graphic graphic in row.GetComponentsInChildren<Graphic>(true))
                Require(graphic.raycastTarget == (graphic == buy.targetGraphic),
                    rowName + " has an unexpected raycast target on " + graphic.name + ".");

            int expectedLabels = definition.unlimitedHeart ? 9 : 8;
            int labelCount = row.GetComponentsInChildren<Text>(true).Length;
            Require(labelCount == expectedLabels, rowName + " has unexpected or missing text labels.");
            checkedLabels += labelCount;
            foreach (Transform child in row)
                CheckContained(row, child as RectTransform, rowName);
            foreach (Transform child in oldPrice)
                CheckContained(oldPrice as RectTransform, child as RectTransform, rowName + " old price");
            CheckContained(buy.transform as RectTransform, Label(buyTransform, "Label").rectTransform, rowName + " buy button");

            Bounds rowBounds = RelativeBounds(packages, row);
            Require(rowBounds.max.y <= previousBottom + 1f, rowName + " overlaps the previous offer row.");
            previousBottom = rowBounds.min.y;
            Bounds contentBounds = RelativeBounds(scroll.content, row);
            Require(contentBounds.min.y >= scroll.content.rect.yMin - 1f &&
                contentBounds.max.y <= scroll.content.rect.yMax + 1f, rowName + " exceeds the scroll content height.");
            report.AppendLine("PASS: " + rowName + " frame, labels, discount, price and runtime action; " +
                row.rect.width.ToString("0.0", CultureInfo.InvariantCulture) + " x " +
                row.rect.height.ToString("0.0", CultureInfo.InvariantCulture) + " UI units.");
        }
        report.AppendLine("PASS: five original atlas frames, " + checkedLabels +
            " editable labels and five buy controls; all text and controls remain inside their frames.");
        ValidateDirectPurchases(home, scroll, packages, actionForButton, report);
        report.AppendLine("Validation did not invoke purchase actions, bind Home, change currency or save the scene.");
        return report.ToString();
    }

    private static void ValidateDirectPurchases(HomeExperienceUI home, ScrollRect scroll, RectTransform packages,
        MethodInfo actionForButton, StringBuilder report)
    {
        var direct = scroll.content.Find("Booster Direct Purchases") as RectTransform;
        Require(direct != null && direct.gameObject.activeSelf,
            "The original Booster Direct Purchases group is missing or inactive.");
        var frames = direct.Find("Shop Card Frames") as RectTransform;
        var items = direct.Find("Shop Card Items") as RectTransform;
        Require(frames != null && items != null, "The original direct-purchase frame/item layers are missing.");
        var cards = items.Cast<Transform>().Where(child => child.GetComponentsInChildren<Button>(true).Length > 0)
            .Select(child => child as RectTransform).ToArray();
        Transform[] nativeFrames = NativeFrames(frames);
        Require(cards.Length == 4 && nativeFrames.Length == 4,
            "Expected four original direct-purchase cards and four native frames. Items: " +
            string.Join(", ", items.Cast<Transform>().Select(child => child.name)));
        float actualDirectTop = float.NegativeInfinity;
        for (int index = 0; index < cards.Length; index++)
        {
            RectTransform card = cards[index];
            Require(card != null && card.gameObject.activeSelf, "An original direct booster card is inactive.");
            var frame = nativeFrames[index] as RectTransform;
            Image frameImage = frame != null ? frame.GetComponent<Image>() : null;
            Require(frameImage != null && frameImage.sprite != null,
                card.name + " has lost its original frame sprite.");
            Require(!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(frameImage.sprite)),
                card.name + " frame sprite is not a saved asset.");
            Image art = card.GetComponentsInChildren<Image>(true).FirstOrDefault(image => image.sprite != null);
            Require(art != null && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(art.sprite)),
                card.name + " has lost its original booster icon sprite.");
            Require(card.GetComponentsInChildren<Text>(true).Any(label => !string.IsNullOrEmpty(label.text)),
                card.name + " has lost its original text.");
            Button[] buttons = card.GetComponentsInChildren<Button>(true);
            Require(buttons.Length == 1, card.name + " must retain exactly one original buy button.");
            Button buy = buttons[0];
            Require(buy.enabled && buy.interactable && buy.targetGraphic != null && buy.targetGraphic.raycastTarget,
                card.name + " original buy button cannot be clicked.");
            Require(buy.GetComponent<Image>() != null && buy.GetComponent<Image>().sprite != null,
                card.name + " has lost its original buy-button sprite.");
            Label(buy.transform, "Label");
            Require(actionForButton.Invoke(home, new object[] { buy }) is Action,
                card.name + " has no runtime purchase action.");
            CheckContained(scroll.content, card, card.name);
            CheckContained(scroll.content, frame, card.name + " frame");
            actualDirectTop = Mathf.Max(actualDirectTop, RelativeBounds(scroll.content, card).max.y,
                RelativeBounds(scroll.content, frame).max.y);
            report.AppendLine("PASS: retained " + card.name + " with its original frame, icon, title, price label and buy action.");
        }
        Bounds finalOffer = RelativeBounds(scroll.content, packages.GetChild(4) as RectTransform);
        Require(actualDirectTop <= finalOffer.min.y + 1f,
            "The direct booster purchases must follow the five offer rows without overlap.");
        ValidateCompactDirectSpacing(scroll.content, nativeFrames, report);
        report.AppendLine("PASS: all " + cards.Length + " original direct purchases remain below the five offers and inside the scroll content.");
    }

    private static void ValidateCompactDirectSpacing(RectTransform content, Transform[] frames, StringBuilder report)
    {
        Bounds[] bounds = frames.Select(frame => RelativeBounds(content, frame as RectTransform)).ToArray();
        float tolerance = Mathf.Max(1f, content.rect.width * 0.001f);
        float maximumGap = content.rect.width * 0.03f + tolerance;
        float topGap = bounds[1].min.x - bounds[0].max.x;
        float bottomGap = bounds[3].min.x - bounds[2].max.x;
        float leftGap = bounds[0].min.y - bounds[2].max.y;
        float rightGap = bounds[1].min.y - bounds[3].max.y;
        foreach (float gap in new[] { topGap, bottomGap, leftGap, rightGap })
            Require(gap >= -tolerance && gap <= maximumGap,
                "The direct booster frames must have small positive gaps in their two-column grid.");
        Require(Mathf.Abs(bounds[0].max.y - bounds[1].max.y) <= tolerance &&
            Mathf.Abs(bounds[2].max.y - bounds[3].max.y) <= tolerance &&
            Mathf.Abs(bounds[0].min.x - bounds[2].min.x) <= tolerance &&
            Mathf.Abs(bounds[1].min.x - bounds[3].min.x) <= tolerance,
            "The direct booster frames are not aligned in two columns and two rows.");
        for (int first = 0; first < bounds.Length; first++)
            for (int second = first + 1; second < bounds.Length; second++)
            {
                float overlapX = Mathf.Min(bounds[first].max.x, bounds[second].max.x) -
                    Mathf.Max(bounds[first].min.x, bounds[second].min.x);
                float overlapY = Mathf.Min(bounds[first].max.y, bounds[second].max.y) -
                    Mathf.Max(bounds[first].min.y, bounds[second].min.y);
                Require(overlapX <= tolerance || overlapY <= tolerance,
                    "Two direct booster frames overlap.");
            }
        report.AppendLine("PASS: compact direct booster grid; column gap " +
            topGap.ToString("0.0", CultureInfo.InvariantCulture) + ", row gap " +
            leftGap.ToString("0.0", CultureInfo.InvariantCulture) + " UI units; frames do not overlap.");
    }

    /// <summary>Optional exact comparison while the restoration helper still has the backup open.</summary>
    public static string ValidateDirectPurchasesAgainstBackup(RectTransform restored, RectTransform originalContent)
    {
        Require(restored != null && originalContent != null, "Both restored and backup direct-purchase content are required.");
        Transform originalItems = originalContent.Find("Shop Card Items");
        Transform restoredItems = restored.Find("Shop Card Items");
        Transform originalFrames = originalContent.Find("Shop Card Frames");
        Transform restoredFrames = restored.Find("Shop Card Frames");
        Require(originalItems != null && restoredItems != null && originalFrames != null && restoredFrames != null,
            "The original and restored direct-purchase item/frame layers are required.");
        Transform[] sourceCards = PurchaseCards(originalItems);
        Transform[] targetCards = PurchaseCards(restoredItems);
        Transform[] sourceFrames = NativeFrames(originalFrames);
        Transform[] targetFrames = NativeFrames(restoredFrames);
        Require(sourceCards.Length == 4 && targetCards.Length == 4 && sourceFrames.Length == 4 && targetFrames.Length == 4,
            "The original and restored direct-purchase groups must each retain four native cards and four native frames.");
        for (int index = 0; index < sourceCards.Length; index++)
        {
            CompareOriginalNode(targetCards[index], sourceCards[index], true);
            CompareOriginalNode(targetFrames[index], sourceFrames[index], true);
        }
        return "PASS: all four restored direct booster cards and native frames retain their original child geometry, sprites, labels and button settings from the scene backup; blank offer templates are excluded.";
    }

    private static Transform[] PurchaseCards(Transform layer) => layer.Cast<Transform>()
        .Where(node => node.GetComponentsInChildren<Button>(true).Length > 0).ToArray();

    private static Transform[] NativeFrames(Transform layer) => layer.Cast<Transform>()
        .Where(node =>
        {
            var image = node.GetComponent<Image>();
            return image != null && image.sprite != null && AssetDatabase.GetAssetPath(image.sprite) != AtlasPath;
        }).ToArray();

    private static void CompareOriginalNode(Transform restored, Transform original, bool allowLayoutPosition)
    {
        string description = "Original direct purchases: " + original.name;
        Require(restored.name == original.name, description + " name changed.");
        Require(restored.gameObject.activeSelf == original.gameObject.activeSelf, description + " active state changed.");
        Require(restored.childCount == original.childCount, description + " child count changed.");
        var targetRect = restored as RectTransform;
        var sourceRect = original as RectTransform;
        Require(targetRect != null && sourceRect != null, description + " rectangle is missing.");
        Require((targetRect.pivot - sourceRect.pivot).sqrMagnitude < 0.000001f &&
            (targetRect.sizeDelta - sourceRect.sizeDelta).sqrMagnitude < 0.0001f &&
            (targetRect.localScale - sourceRect.localScale).sqrMagnitude < 0.000001f &&
            Quaternion.Angle(targetRect.localRotation, sourceRect.localRotation) < 0.001f,
            description + " authored size, scale, pivot or rotation changed.");
        if (!allowLayoutPosition)
            Require((targetRect.anchorMin - sourceRect.anchorMin).sqrMagnitude < 0.000001f &&
                (targetRect.anchorMax - sourceRect.anchorMax).sqrMagnitude < 0.000001f &&
                (targetRect.anchoredPosition - sourceRect.anchoredPosition).sqrMagnitude < 0.0001f,
                description + " authored child placement changed.");
        Image originalImage = original.GetComponent<Image>();
        Image restoredImage = restored.GetComponent<Image>();
        Require((originalImage == null) == (restoredImage == null), description + " image component changed.");
        if (originalImage != null)
            Require(restoredImage.sprite == originalImage.sprite && restoredImage.color == originalImage.color &&
                restoredImage.type == originalImage.type && restoredImage.preserveAspect == originalImage.preserveAspect &&
                restoredImage.raycastTarget == originalImage.raycastTarget,
                description + " original image settings changed.");
        Text originalText = original.GetComponent<Text>();
        Text restoredText = restored.GetComponent<Text>();
        Require((originalText == null) == (restoredText == null), description + " text component changed.");
        if (originalText != null)
            Require((restoredText.text == originalText.text || UpdatedDirectPurchasePrice(restoredText, originalText)) && restoredText.font == originalText.font &&
                restoredText.color == originalText.color && MatchingFontSize(restoredText, originalText) &&
                restoredText.fontStyle == originalText.fontStyle && restoredText.alignment == originalText.alignment,
                description + " original label settings changed.");
        Button originalButton = original.GetComponent<Button>();
        Button restoredButton = restored.GetComponent<Button>();
        Require((originalButton == null) == (restoredButton == null), description + " button component changed.");
        if (originalButton != null)
            Require(restoredButton.enabled == originalButton.enabled && restoredButton.interactable == originalButton.interactable &&
                restoredButton.transition == originalButton.transition &&
                restoredButton.onClick.GetPersistentEventCount() == originalButton.onClick.GetPersistentEventCount(),
                description + " original button settings changed.");
        for (int index = 0; index < original.childCount; index++)
            CompareOriginalNode(restored.GetChild(index), original.GetChild(index), false);
    }

    private static bool UpdatedDirectPurchasePrice(Text restored, Text original)
    {
        return restored.name == "Label" && original.name == "Label" &&
            restored.transform.parent.GetComponent<Button>() != null &&
            restored.transform.parent.name.StartsWith("BUY ", StringComparison.Ordinal) &&
            restored.text == BoosterPurchaseTransaction.UnitPrice.ToString();
    }

    private static bool MatchingFontSize(Text restored, Text original)
    {
        var restoredFit = restored.GetComponent<HomeTextFit>();
        var originalFit = original.GetComponent<HomeTextFit>();
        if (restoredFit == null || originalFit == null) return restored.fontSize == original.fontSize;
        // HomeTextFit computes the rendered size from the active canvas. Its authored limits
        // must match even when one of the compared shop tabs is currently inactive.
        var restoredSettings = new SerializedObject(restoredFit);
        var originalSettings = new SerializedObject(originalFit);
        return restoredSettings.FindProperty("maximumFontSize").intValue == originalSettings.FindProperty("maximumFontSize").intValue &&
            restoredSettings.FindProperty("singleLine").boolValue == originalSettings.FindProperty("singleLine").boolValue;
    }

    /// <summary>Renders a detached clone of both booster sections in a temporary preview scene.</summary>
    public static void CapturePreview(HomeExperienceUI home, string path)
    {
        if (string.IsNullOrEmpty(path)) throw new ArgumentException("A preview path is required.", nameof(path));
        ScrollRect scroll = FindScroll(home);
        var source = scroll.content;
        Require(source != null && source.rect.width > 1f && source.rect.height > 1f,
            "The booster content must have a resolved size before capturing its preview.");
        Scene preview = default;
        RenderTexture target = null;
        Texture2D pixels = null;
        RenderTexture previous = RenderTexture.active;
        try
        {
            preview = EditorSceneManager.NewPreviewScene();
            var cameraObject = new GameObject("Booster Offers Preview Camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, preview);
            var camera = cameraObject.GetComponent<Camera>();
            camera.scene = preview;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(20, 8, 55, 255);
            camera.orthographic = true;
            camera.orthographicSize = 836f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 1000f;
            camera.transform.position = new Vector3(0f, 0f, -100f);
            camera.cullingMask = 1 << 31;
            target = new RenderTexture(941, 1672, 24, RenderTextureFormat.ARGB32);
            target.Create();
            camera.targetTexture = target;

            var canvasObject = new GameObject("Booster Offers Preview Canvas", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(canvasObject, preview);
            canvasObject.layer = 31;
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10f;
            canvas.sortingOrder = 100;
            canvasObject.SetActive(false);
            var cloneObject = UnityEngine.Object.Instantiate(source.gameObject, canvasObject.transform, false);
            cloneObject.name = "Booster Shop Content Preview";
            // A cloned layout component would retain the original ScrollRect reference.
            var clonedLayout = cloneObject.GetComponent<BoosterOfferListLayout>();
            if (clonedLayout != null) UnityEngine.Object.DestroyImmediate(clonedLayout);
            cloneObject.SetActive(true);
            var clone = cloneObject.GetComponent<RectTransform>();
            clone.anchorMin = clone.anchorMax = clone.pivot = new Vector2(0.5f, 0.5f);
            clone.sizeDelta = source.rect.size;
            clone.anchoredPosition = Vector2.zero;
            float scale = Mathf.Min(911f / source.rect.width, 1572f / source.rect.height);
            clone.localScale = Vector3.one * scale;
            clone.localRotation = Quaternion.identity;
            foreach (Transform node in clone.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 31;
            canvasObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            pixels.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0);
            pixels.Apply();
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
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

    private static ScrollRect FindScroll(HomeExperienceUI home)
    {
        if (home == null) throw new ArgumentNullException(nameof(home));
        var scroll = home.GetComponentsInChildren<ScrollRect>(true)
            .SingleOrDefault(item => item.name == "Booster Shop Viewport");
        Require(scroll != null && scroll.content != null && scroll.viewport != null,
            "The existing Booster Shop Viewport scroll/content is missing.");
        return scroll;
    }

    private static Text Label(Transform parent, string name)
    {
        Transform node = parent.Find(name);
        Text text = node != null ? node.GetComponent<Text>() : null;
        Require(text != null && text.enabled && text.gameObject.activeSelf, parent.name + " is missing text: " + name);
        Require(!string.IsNullOrWhiteSpace(text.text), parent.name + "/" + name + " is empty.");
        return text;
    }

    private static Image ImageAt(Transform parent, string name)
    {
        Transform node = parent.Find(name);
        Image image = node != null ? node.GetComponent<Image>() : null;
        Require(image != null && image.enabled && image.gameObject.activeSelf, parent.name + " is missing image: " + name);
        return image;
    }

    private static void ValidateAtlasImage(Image image, int index, string description)
    {
        Require(image != null && image.sprite != null, description + " has no sprite.");
        Require(AssetDatabase.GetAssetPath(image.sprite) == AtlasPath && image.sprite.name == "shop-parts-v3_" + index,
            description + " does not use the expected original atlas slice.");
        Require(image.color == Color.white && image.type == Image.Type.Simple,
            description + " changes the original frame color or image type.");
    }

    private static void ValidateNumber(Text text, int expected, string description)
    {
        string digits = new string(text.text.Where(char.IsDigit).ToArray());
        Require(int.TryParse(digits, out int amount) && amount == expected, description + " is incorrect.");
    }

    private static void CheckContained(RectTransform parent, RectTransform child, string description)
    {
        Require(parent != null && child != null, description + " has a missing UI rectangle.");
        Bounds bounds = RelativeBounds(parent, child);
        const float tolerance = 1f;
        Require(bounds.min.x >= parent.rect.xMin - tolerance && bounds.max.x <= parent.rect.xMax + tolerance &&
            bounds.min.y >= parent.rect.yMin - tolerance && bounds.max.y <= parent.rect.yMax + tolerance,
            description + ": " + child.name + " extends outside " + parent.name + ". Parent: " +
            parent.rect + "; child bounds: " + bounds.min + " to " + bounds.max);
    }

    private static Bounds RelativeBounds(RectTransform parent, RectTransform child)
    {
        var corners = new Vector3[4];
        child.GetLocalCorners(corners);
        Vector3 first = parent.InverseTransformPoint(child.TransformPoint(corners[0]));
        var bounds = new Bounds(first, Vector3.zero);
        for (int index = 1; index < corners.Length; index++)
            bounds.Encapsulate(parent.InverseTransformPoint(child.TransformPoint(corners[index])));
        return bounds;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Booster offers: " + message);
    }
}
#endif
