using UnityEngine;

public sealed partial class HomeExperienceUI
{
    [Header("Play multiplier attachment")]
    [SerializeField] private Vector2 playMultiplierAttachmentUv = new Vector2(.88f, .95f);
    [SerializeField] private Vector2 playMultiplierHookUv = new Vector2(.29f, .96f);

    public void ApplyResponsiveLayout(Vector2 screenSize)
    {
        if (sceneHomeCanvas == null) sceneHomeCanvas = SceneComponent<Canvas>("Home Canvas", transform);
        if (sceneHomeCanvas == null) return;
        homeCanvasObject = sceneHomeCanvas.gameObject;
        ApplyHomeScreenEdges(screenSize);
        if (footerBackgroundObject == null) footerBackgroundObject = SceneObject("Footer Sprite");
        if (footerBackgroundObject != null) ConfigureFooterImage(footerBackgroundObject.GetComponent<UnityEngine.UI.Image>());
        PositionResponsivePlayBadge();
    }

    private void ApplyHomeScreenEdges(Vector2 screenSize)
    {
        var root = sceneHomeCanvas.transform.Find("Home") as RectTransform;
        var content = root != null ? root.Find("Home Content") as RectTransform : null;
        if (content == null) return;

        // Edge hosts retain the phone-sized coordinate space of their children.
        // Only their placement changes, so artwork and animated light rays keep
        // the same dimensions and local animation origins on a wider display.
        float width = content.rect.width;
        float aspect = Mathf.Min(PortraitDisplay.MaximumContentAspect, screenSize.x / Mathf.Max(1f, screenSize.y));
        float phoneAspect = PortraitDisplay.ReferenceScreenSize.x / PortraitDisplay.ReferenceScreenSize.y;
        float screenWidth = width * Mathf.Max(1f, aspect / phoneAspect);
        RectTransform edges = HomeEdgeHost(content, "Home Screen Edges", .5f, screenWidth);
        RectTransform left = HomeEdgeHost(edges, "Left HUD Edge", 0f, width);
        RectTransform right = HomeEdgeHost(edges, "Right HUD Edge", 1f, width);
        MoveHomeEdgeChild(content.Find("PROFILE"), left);
        MoveHomeEdgeChild(content.Find("SETTINGS"), right);
        MoveHomeEdgeChild(content.Find("Bottom Navigation"), edges);

        var page = content.Find("Home Only Content") as RectTransform;
        if (page != null)
        {
            RectTransform levelEdges = HomeEdgeHost(page, "Level Screen Edges", .5f, screenWidth);
            RectTransform leftLevels = HomeEdgeHost(levelEdges, "Left Level Edge", 0f, width);
            RectTransform rightLevels = HomeEdgeHost(levelEdges, "Right Level Edge", 1f, width);
            levelEdges.SetAsFirstSibling();
            for (int i = page.childCount - 1; i >= 0; i--)
            {
                Transform child = page.GetChild(i);
                string name = child.name;
                if (name == "LOCKED_30" || name.StartsWith("LOCKED_30 ", System.StringComparison.Ordinal) ||
                    name == "LOCKED_25" || name.StartsWith("LOCKED_25 ", System.StringComparison.Ordinal))
                    MoveHomeEdgeChild(child, leftLevels, true);
                else if (name == "LOCKED_50" || name.StartsWith("LOCKED_50 ", System.StringComparison.Ordinal))
                    MoveHomeEdgeChild(child, rightLevels, true);
            }
        }

        if (selectedTabBlock != null)
        {
            SetSelectedTabAnchors(selectedTabBlock, selectedTabX, GetTabIconRect(activeTabIndex));
            UpdateBottomTabTextSizing();
        }
    }

    private static RectTransform HomeEdgeHost(RectTransform parent, string name, float side, float width)
    {
        var host = parent.Find(name) as RectTransform;
        if (host == null)
        {
            host = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            host.SetParent(parent, false);
        }
        host.anchorMin = new Vector2(side, 0f);
        host.anchorMax = new Vector2(side, 1f);
        host.pivot = new Vector2(side, .5f);
        host.sizeDelta = new Vector2(width, 0f);
        host.anchoredPosition = Vector2.zero;
        return host;
    }

    private static void MoveHomeEdgeChild(Transform child, RectTransform parent, bool firstSibling = false)
    {
        if (!(child is RectTransform rect) || child.parent == parent) return;
        Vector2 min = rect.anchorMin, max = rect.anchorMax, pivot = rect.pivot, size = rect.sizeDelta;
        Vector3 position = rect.anchoredPosition3D, scale = rect.localScale;
        Quaternion rotation = rect.localRotation;
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition3D = position;
        rect.localScale = scale;
        rect.localRotation = rotation;
        if (firstSibling) rect.SetAsFirstSibling();
    }

    private void PositionResponsivePlayBadge()
    {
        if (playButtonRect == null) playButtonRect = SceneComponent<RectTransform>("PLAY");
        if (playButtonRect == null) return;
        if (playMultiplierBadgeRect == null)
            playMultiplierBadgeRect = playButtonRect.Find("Play Multiplier Badge") as RectTransform;
        if (playMultiplierBadgeRect == null || !TryGetPlayButtonImageBounds(out Rect bounds)) return;

        // Keep the visual hook on the rendered button art even when the badge's
        // authored pivot differs from that hook or the hanging tag is swinging.
        Vector2 attachment = new Vector2(Mathf.Clamp01(playMultiplierAttachmentUv.x),
            Mathf.Clamp01(playMultiplierAttachmentUv.y));
        Vector2 hook = new Vector2(Mathf.Clamp01(playMultiplierHookUv.x), Mathf.Clamp01(playMultiplierHookUv.y));
        Rect badgeBounds = playMultiplierBadgeRect.rect;
        Vector3 hookWorld = playMultiplierBadgeRect.TransformPoint(badgeBounds.min + Vector2.Scale(badgeBounds.size, hook));
        Vector3 local = playButtonRect.InverseTransformPoint(hookWorld);
        local.x = bounds.xMin + bounds.width * attachment.x;
        local.y = bounds.yMin + bounds.height * attachment.y;
        playMultiplierBadgeRect.position += playButtonRect.TransformPoint(local) - hookWorld;
    }

    private void RefreshResponsiveHomeLayout()
    {
        if (sceneHomeCanvas != null) PositionResponsivePlayBadge();
    }
}
