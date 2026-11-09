using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>Keeps authored offer art in proportion while the shop viewport changes size.</summary>
[RequireComponent(typeof(RectTransform))]
public sealed class BoosterOfferListLayout : MonoBehaviour
{
    [SerializeField] private ScrollRect scroll;
    [SerializeField] private RectTransform packages;
    [SerializeField] private RectTransform directPurchases;
    [SerializeField] private Vector2 directPurchaseAuthoredSize;
    private bool applying;
    private float previousViewportWidth = -1f;

    public void Configure(ScrollRect target, RectTransform offerPackages, RectTransform originalPurchases = null)
    {
        previousViewportWidth = -1f;
        scroll = target;
        packages = offerPackages;
        if (originalPurchases != null)
        {
            directPurchases = originalPurchases;
            directPurchaseAuthoredSize = originalPurchases.rect.size;
        }
        RefreshLayout();
    }

    public void RefreshLayout()
    {
        if (Application.isPlaying || applying || scroll == null || scroll.viewport == null || packages == null) return;
        float viewportWidth = scroll.viewport.rect.width;
        if (viewportWidth <= 1f) return;
        if (Mathf.Approximately(previousViewportWidth, viewportWidth)) return;
        applying = true;
        try
        {
            float sidePadding = viewportWidth * 0.012f;
            // Keep the authored scrollbar to the right of the offer cards.
            float rightPadding = viewportWidth * 0.026f;
            float width = viewportWidth - sidePadding - rightPadding;
            float gap = width * 0.008f;
            float top = gap;
            foreach (RectTransform row in packages)
            {
                var image = row.GetComponent<Image>();
                if (image == null || image.sprite == null) continue;
                float height = width * image.sprite.rect.height / image.sprite.rect.width;
                row.anchorMin = row.anchorMax = new Vector2(0f, 1f);
                row.pivot = new Vector2(0f, 1f);
                row.localScale = Vector3.one;
                row.anchoredPosition = new Vector2(sidePadding, -top);
                row.sizeDelta = new Vector2(width, height);
                top += height + gap;
            }
            if (directPurchases != null && directPurchaseAuthoredSize.x > 1f)
            {
                // Keep the original artwork, card sizes and controls while tightening
                // the two-column grid instead of retaining its old wide gutters.
                directPurchases.anchorMin = directPurchases.anchorMax = new Vector2(0f, 1f);
                directPurchases.pivot = new Vector2(0f, 1f);
                directPurchases.sizeDelta = directPurchaseAuthoredSize;
                directPurchases.localScale = Vector3.one;
                directPurchases.ForceUpdateRectTransforms();
                CompactDirectPurchaseGrid(directPurchases);
                // Fit the purchase cards themselves, preserving their original offsets
                // without adding the blank space used by the old offer templates.
                Bounds bounds = MeasureDirectPurchases(directPurchases);
                float scale = width / Mathf.Max(1f, bounds.size.x);
                directPurchases.localScale = Vector3.one * scale;
                directPurchases.anchoredPosition = new Vector2(sidePadding - bounds.min.x * scale,
                    -top - bounds.max.y * scale);
                top += bounds.size.y * scale + gap;
            }
            var content = (RectTransform)transform;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, top);
            packages.anchorMin = Vector2.zero;
            packages.anchorMax = Vector2.one;
            packages.offsetMin = packages.offsetMax = Vector2.zero;
            previousViewportWidth = viewportWidth;
        }
        finally { applying = false; }
    }

    private static void CompactDirectPurchaseGrid(RectTransform group)
    {
        var itemLayer = group.Find("Shop Card Items");
        var frameLayer = group.Find("Shop Card Frames");
        if (itemLayer == null || frameLayer == null) return;
        var cards = new List<RectTransform>();
        var frames = new List<RectTransform>();
        foreach (RectTransform card in itemLayer)
            if (card.gameObject.activeSelf && card.GetComponentsInChildren<Button>(true).Length > 0) cards.Add(card);
        foreach (RectTransform frame in frameLayer)
        {
            var image = frame.GetComponent<Image>();
            if (frame.gameObject.activeSelf && image != null && image.sprite != null) frames.Add(frame);
        }
        if (cards.Count == 0 || cards.Count != frames.Count) return;
        float cellWidth = 0f;
        float cellHeight = 0f;
        foreach (var frame in frames)
        {
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(group, frame);
            cellWidth = Mathf.Max(cellWidth, bounds.size.x);
            cellHeight = Mathf.Max(cellHeight, bounds.size.y);
        }
        float gap = cellWidth * 0.016f;
        for (int i = 0; i < cards.Count; i++)
        {
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(group, frames[i]);
            Vector3 center = new Vector3((i % 2) * (cellWidth + gap) + bounds.extents.x,
                -(i / 2) * (cellHeight + gap) - bounds.extents.y, bounds.center.z);
            Vector3 delta = group.TransformVector(center - bounds.center);
            frames[i].position += delta;
            cards[i].position += delta;
        }
    }

    public static Bounds MeasureDirectPurchases(RectTransform group)
    {
        var bounds = new Bounds();
        bool hasBounds = false;
        foreach (string name in new[] { "Shop Card Frames", "Shop Card Items" })
        {
            var layer = group.Find(name);
            if (layer == null) continue;
            foreach (RectTransform card in layer)
            {
                if (!card.gameObject.activeSelf) continue;
                if (name == "Shop Card Items" && card.GetComponentsInChildren<Button>(true).Length == 0) continue;
                Bounds cardBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(group, card);
                if (!hasBounds) { bounds = cardBounds; hasBounds = true; }
                else bounds.Encapsulate(cardBounds);
            }
        }
        return bounds;
    }
}
