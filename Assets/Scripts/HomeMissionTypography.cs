using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Fixed mission text boxes with TextMeshPro's built-in Auto Size.</summary>
public static class HomeMissionTypography
{
    public static void Configure(TMP_Text text, int maximum, int minimum, TextAnchor alignment, bool multiline = false)
    {
        text.font = Resources.Load<TMP_FontAsset>(HomeUIFont.AssetPath);
        if (text.font == null) throw new System.InvalidOperationException("Missing blockblast_font TMP asset.");
        text.fontStyle = FontStyles.Normal; // The supplied face is already ExtraBold.
        text.enableAutoSizing = true;
        text.fontSize = maximum;
        text.fontSizeMin = minimum;
        text.fontSizeMax = maximum;
        text.textWrappingMode = multiline ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.alignment = alignment == TextAnchor.MiddleLeft ? TextAlignmentOptions.MidlineLeft
            : alignment == TextAnchor.MiddleRight ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.Midline;
        text.margin = new Vector4(2f, 0f, 2f, 0f);
        text.raycastTarget = false;
    }

    public static void ApplyToScene(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            // Also remove the retired card from older baked Home layouts, whose
            // containers may predate the current scroll-view hierarchy.
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text == null) continue;
                if (!InsideMissions(text.transform) || !text.text.Contains("10.000")
                    || !text.text.StartsWith("Đạt")) continue;
                if (text.transform.parent != null)
                {
                    var retiredCard = text.transform.parent.gameObject;
                    retiredCard.SetActive(false);
                    Object.DestroyImmediate(retiredCard);
                }
            }
            foreach (var legacy in root.GetComponentsInChildren<Text>(true))
            {
                if (legacy == null) continue;
                bool header = legacy.name == "Label" && legacy.transform.parent != null
                    && legacy.transform.parent.name.EndsWith(" Header Banner");
                if (!header && !InsideMissions(legacy.transform)) continue;
                var go = legacy.gameObject;
                string value = legacy.text;
                if (InsideMissions(legacy.transform) && value.StartsWith("Đạt") && value.Contains("10.000"))
                {
                    var retiredCard = legacy.transform.parent.gameObject;
                    retiredCard.SetActive(false);
                    Object.DestroyImmediate(retiredCard);
                    continue;
                }
                Color color = legacy.color;
                TextAnchor alignment = legacy.alignment;
                foreach (var effect in go.GetComponents<BaseMeshEffect>())
                {
                    effect.enabled = false;
                    Object.DestroyImmediate(effect);
                }
                Object.DestroyImmediate(legacy);
                var text = go.AddComponent<TextMeshProUGUI>();
                text.text = value;
                text.color = color;
                Configure(text, 32, 20, alignment, value.Contains("\n"));
            }

            foreach (var scroll in root.GetComponentsInChildren<ScrollRect>(true))
            {
                if ((!scroll.name.Contains("Daily") || !scroll.name.Contains("Mission")) || scroll.content == null) continue;
                var retired = scroll.content.Find("Reach 10000 Points");
                if (retired != null)
                {
                    retired.gameObject.SetActive(false);
                    Object.DestroyImmediate(retired.gameObject);
                }
                int count = scroll.content.childCount;
                if (count == 0) continue;
                int height = count * 235;
                var aspect = scroll.content.GetComponent<AspectRatioFitter>();
                if (aspect != null) aspect.aspectRatio = 941f / height;
                for (int i = 0; i < count; i++)
                {
                    var card = (RectTransform)scroll.content.GetChild(i);
                    card.anchorMin = new Vector2(43f / 941f, 1f - (i * 235f + 225f) / height);
                    card.anchorMax = new Vector2(896f / 941f, 1f - i * 235f / height);
                    card.offsetMin = card.offsetMax = Vector2.zero;
                    LayoutCard(card);
                }
            }

            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                var parent = text.transform.parent;
                if (text.name == "Label" && parent != null && parent.name.EndsWith(" Header Banner"))
                {
                    var rect = text.rectTransform;
                    rect.anchorMin = new Vector2(0.175f, 0.18f);
                    rect.anchorMax = new Vector2(0.855f, 0.82f);
                    rect.offsetMin = rect.offsetMax = Vector2.zero;
                    Configure(text, 44, 30, TextAnchor.MiddleCenter);
                }
                else if (text.name == "Daily Login Panel Title")
                {
                    text.rectTransform.anchorMin = new Vector2(48f / 854f, 1f - 100f / 247f);
                    text.rectTransform.anchorMax = new Vector2(503f / 854f, 1f - 12f / 247f);
                    text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
                    Configure(text, 32, 22, TextAnchor.MiddleLeft, true);
                }
                else if (InsideMissions(text.transform) && text.name.EndsWith(" Label")) Configure(text, 22, 18, TextAnchor.MiddleCenter);
            }
        }
    }

    private static bool InsideMissions(Transform current)
    {
        while (current != null)
        {
            if (current.name == "Daily Missions Page") return true;
            current = current.parent;
        }
        return false;
    }

    private static void LayoutCard(RectTransform card)
    {
        Set(card, " Mission Icon", 20, 47, 143, 136);
        Set(card, " Title", 177, 22, 351, 58, 44, 32);
        Set(card, " Description", 178, 86, 351, 42, 26, 20);
        Set(card, " Progress Track", 178, 144, 342, 46);
        Set(card, " Progress Fill", 181, 147, 336, 40);
        Set(card, " Progress Text", 181, 145, 336, 44, 32, 26);
        Set(card, " Reward Frame", 539, 53, 108, 124);
        Set(card, " Reward Coin Icon", 546, 59, 95, 73);
        Set(card, " Reward Amount", 547, 129, 92, 48, 36, 28);
        if (!TryGetMission(card.name, out var type)) return;
        var rewardFrame = card.Find(card.name + " Reward Frame");
        var rewardIcon = card.Find(card.name + " Reward Coin Icon");
        var rewardAmount = card.Find(card.name + " Reward Amount");
        if (rewardIcon == null) return;
        var iconImage = rewardIcon.GetComponent<Image>();
        if (iconImage == null) return;
        var button = rewardIcon.GetComponent<Button>();
        if (button == null) button = rewardIcon.gameObject.AddComponent<Button>();
        button.targetGraphic = iconImage;
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        iconImage.raycastTarget = true;
        var ui = card.GetComponent<DailyMissionCardUI>();
        if (ui == null) ui = card.gameObject.AddComponent<DailyMissionCardUI>();
        ui.Bind(type, card.Find(card.name + " Progress Text").GetComponent<TMP_Text>(),
            card.Find(card.name + " Progress Fill").GetComponent<Image>(), button,
            rewardFrame != null ? rewardFrame.gameObject : null, rewardIcon.gameObject,
            rewardAmount != null ? rewardAmount.gameObject : null);
    }

    private static void Set(RectTransform card, string suffix, float x, float y, float width, float height, int max = 0, int min = 0)
    {
        var rect = card.Find(card.name + suffix) as RectTransform;
        if (rect == null) return;
        rect.anchorMin = new Vector2(x / 853f, 1f - (y + height) / 225f);
        rect.anchorMax = new Vector2((x + width) / 853f, 1f - y / 225f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        if (max <= 0) return;
        var text = rect.GetComponent<TMP_Text>();
        if (text != null) Configure(text, max, min, suffix == " Title" || suffix == " Description"
            ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter);
    }

    private static bool TryGetMission(string name, out DailyMissionType type)
    {
        switch (name)
        {
            case "Break 50 Blocks": type = DailyMissionType.BreakBlocks; return true;
            case "Play 3 Games": type = DailyMissionType.PlayGames; return true;
            case "Use 5 Boosters": type = DailyMissionType.UseBoosters; return true;
            case "Win 2 Games":
            case "Win 5 Games": type = DailyMissionType.WinGames; return true;
            case "Collect 5 Food":
            case "Win Streak 3": type = DailyMissionType.WinStreak; return true;
            case "Break 200 Blocks":
            case "Break 1000 Blocks": type = DailyMissionType.BreakMoreBlocks; return true;
            default: type = default; return false;
        }
    }
}
