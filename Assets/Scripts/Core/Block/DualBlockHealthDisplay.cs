using TMPro;
using UnityEngine;

/// <summary>Uses one centered label for the shared HP of a dual group.</summary>
public static class DualBlockHealthDisplay
{
    public static void Refresh(Block block)
    {
        if (block == null) return;
        TMP_Text label = block.hpText != null ? block.hpText :
            block.dualTopHPText != null ? block.dualTopHPText : block.dualBottomHPText;
        if (label == null) return;
        block.hpText = label;
        bool layoutChanged = label.transform.parent != block.transform || label.margin != Vector4.zero ||
            label.alignment != TextAlignmentOptions.Center || label.rectTransform.localPosition != Vector3.zero ||
            label.rectTransform.localRotation != Quaternion.identity;
        if (label.transform.parent != block.transform)
            label.transform.SetParent(block.transform, false);
        RectTransform rect = label.rectTransform;
        bool styleChanged = MatchNormalText(block, label);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localPosition = Vector3.zero;
        rect.localRotation = Quaternion.identity;
        // The authored half labels use asymmetric margins to position their digits.
        // A shared center label must clear those margins as well as move its transform.
        label.margin = Vector4.zero;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        string value = (block.group != null ? block.group.totalHP : block.dualTopHP + block.dualBottomHP).ToString();
        bool valueChanged = label.text != value;
        if (valueChanged) label.text = value;
        label.enabled = true;
        label.gameObject.SetActive(true);
        if (block.dualTopHPText != null && block.dualTopHPText != label)
            block.dualTopHPText.gameObject.SetActive(false);
        if (block.dualBottomHPText != null && block.dualBottomHPText != label)
            block.dualBottomHPText.gameObject.SetActive(false);
        if (label is TextMeshPro worldText)
        {
            Renderer renderer = worldText.renderer;
            renderer.enabled = true;
            int top = block.dualTopRenderer != null ? block.dualTopRenderer.sortingOrder : 0;
            int bottom = block.dualBottomRenderer != null ? block.dualBottomRenderer.sortingOrder : 0;
            renderer.sortingOrder = Mathf.Max(renderer.sortingOrder, Mathf.Max(top, bottom) + 1);
        }
        if (layoutChanged || valueChanged || styleChanged)
        {
            label.ForceMeshUpdate(true, true);
            block.GetComponent<BoardBlockClipping>()?.RefreshClipping();
        }
    }

    private static bool MatchNormalText(Block block, TMP_Text label)
    {
        GridManager grid = block.GetComponentInParent<GridManager>();
        Block normal = grid != null ? grid.blockPrefab : null;
        TMP_Text reference = normal != null ? normal.hpText : null;
        if (reference == null || reference == label) return false;

        // Normal HP is nested beneath its visual. Preserve that effective size
        // after moving the dual label directly beneath its block root.
        Vector3 relativeScale = Vector3.one;
        Transform current = reference.transform;
        while (current != null && current != normal.transform)
        {
            relativeScale = Vector3.Scale(relativeScale, current.localScale);
            current = current.parent;
        }
        if (current != normal.transform) return false;
        Vector3 blockScale = block.BaseScale;
        if (Mathf.Abs(blockScale.x) < .0001f || Mathf.Abs(blockScale.y) < .0001f)
            blockScale = block.transform.localScale;
        Vector3 normalScale = normal.transform.localScale;
        Vector3 scale = new Vector3(
            Mathf.Abs(blockScale.x) > .0001f ? Mathf.Abs(relativeScale.x * normalScale.x / blockScale.x) : 1f,
            Mathf.Abs(blockScale.y) > .0001f ? Mathf.Abs(relativeScale.y * normalScale.y / blockScale.y) : 1f,
            Mathf.Abs(blockScale.z) > .0001f ? Mathf.Abs(relativeScale.z * normalScale.z / blockScale.z) : 1f);
        bool changed = label.rectTransform.localScale != scale || !Mathf.Approximately(label.fontSize, reference.fontSize) ||
            label.fontStyle != reference.fontStyle || label.fontWeight != reference.fontWeight || label.enableAutoSizing != reference.enableAutoSizing;
        label.rectTransform.localScale = scale;
        label.fontSize = reference.fontSize;
        label.fontStyle = reference.fontStyle;
        label.fontWeight = reference.fontWeight;
        label.enableAutoSizing = reference.enableAutoSizing;
        if (reference.enableAutoSizing)
        {
            changed |= !Mathf.Approximately(label.fontSizeMin, reference.fontSizeMin) ||
                !Mathf.Approximately(label.fontSizeMax, reference.fontSizeMax);
            label.fontSizeMin = reference.fontSizeMin;
            label.fontSizeMax = reference.fontSizeMax;
        }
        return changed;
    }
}
