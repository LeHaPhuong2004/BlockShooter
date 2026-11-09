using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class HomeExperienceUI
{
    private Vector2 goldShopLayoutSize;
    private Vector2 boosterShopLayoutSize;

    // Editor authoring utility. Runtime must preserve the RectTransforms saved in the scene.
    private void RefreshAuthoredShopLayout(bool force = false)
    {
        if (Application.isPlaying) return;
        LayoutAuthoredShop(shopGoldViewport, ref goldShopLayoutSize, force);
        LayoutAuthoredShop(shopBoosterViewport, ref boosterShopLayoutSize, force);
    }

    private static void LayoutAuthoredShop(GameObject viewportObject, ref Vector2 previousSize, bool force)
    {
        if (viewportObject == null) return;
        var scroll = viewportObject.GetComponent<ScrollRect>();
        if (scroll == null || scroll.viewport == null || scroll.content == null) return;
        var offerLayout = scroll.content.GetComponent<BoosterOfferListLayout>();
        if (offerLayout != null)
        {
            offerLayout.RefreshLayout();
            return;
        }
        Vector2 viewportSize = scroll.viewport.rect.size;
        if (viewportSize.x <= 1f || viewportSize.y <= 1f) return;
        if (!force && (viewportSize - previousSize).sqrMagnitude < 0.25f) return;
        previousSize = viewportSize;

        var itemLayer = scroll.content.Find("Shop Card Items") as RectTransform;
        var frameLayer = scroll.content.Find("Shop Card Frames") as RectTransform;
        if (itemLayer == null || frameLayer == null) return;
        var cards = new List<RectTransform>();
        foreach (Transform child in itemLayer)
            if (child.name.StartsWith("Shop Item - ", StringComparison.Ordinal)) cards.Add((RectTransform)child);
        if (cards.Count == 0) return;

        float position = force ? 1f : Mathf.Clamp01(scroll.verticalNormalizedPosition);
        float cellWidth = viewportSize.x * 0.5f;
        float cardWidth = cellWidth * 0.94f;
        float cardHeight = cardWidth * (491f / 425f);
        float gap = cardHeight * 0.025f;
        int rows = Mathf.CeilToInt(cards.Count / 2f);
        var content = scroll.content;
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1f);
        content.localScale = Vector3.one;
        content.sizeDelta = new Vector2(0f, rows * (cardHeight + gap) + gap);
        content.ForceUpdateRectTransforms();
        SetShopRect(itemLayer, Vector2.zero, Vector2.one);
        SetShopRect(frameLayer, Vector2.zero, Vector2.one);

        for (int i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            var anchor = new Vector2(i % 2 == 0 ? 0.25f : 0.75f, 1f);
            var center = new Vector2(0f, -gap - cardHeight * 0.5f - (i / 2) * (cardHeight + gap));
            SetShopCell(card, anchor, center, new Vector2(cardWidth, cardHeight));
            if (i < frameLayer.childCount)
            {
                var frame = (RectTransform)frameLayer.GetChild(i);
                SetShopCell(frame, anchor, center, new Vector2(cardWidth, cardHeight));
                var frameImage = frame.GetComponent<Image>();
                if (frameImage != null) frameImage.preserveAspect = false;
            }

            foreach (Transform child in card)
            {
                var rect = child as RectTransform;
                if (rect == null) continue;
                if (child.name == "Gold Bundle Art" || child.name == "Heart Bundle Art" || child.name == "Booster Art")
                {
                    SetShopRect(rect, new Vector2(0.10f, 0.34f), new Vector2(0.90f, 0.92f));
                    var image = rect.GetComponent<Image>();
                    if (image != null) image.preserveAspect = true;
                }
                else if (child.name == "Label")
                    SetShopRect(rect, new Vector2(0.06f, 0.23f), new Vector2(0.94f, 0.33f));
                else if (child.GetComponent<Button>() != null)
                {
                    SetShopRect(rect, new Vector2(0.09f, 0.06f), new Vector2(0.91f, 0.22f));
                    var image = rect.GetComponent<Image>();
                    if (image != null) image.preserveAspect = true;
                }
            }
        }
        scroll.verticalNormalizedPosition = float.IsNaN(position) ? 1f : position;
    }

    private static void SetShopCell(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        rect.ForceUpdateRectTransforms();
    }

    private static void SetShopRect(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.ForceUpdateRectTransforms();
    }

#if UNITY_EDITOR
    public void FixSceneShopLayoutForEditor()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Fix the authored shop outside Play Mode.");
        homeCanvasObject = SceneComponent<Canvas>("Home Canvas", transform).gameObject;
        shopGoldViewport = SceneObject("Gold Shop Viewport");
        shopBoosterViewport = SceneObject("Booster Shop Viewport");
        Canvas.ForceUpdateCanvases();
        RefreshAuthoredShopLayout(true);
    }
#endif
}
