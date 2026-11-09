using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>One font family for both legacy UI and TMP text throughout Home.</summary>
public static class HomeUIFont
{
    public const string AssetPath = "Font/blockblast_font";

    public static Font LoadSourceFont()
    {
        var asset = Resources.Load<TMP_FontAsset>(AssetPath);
        // Static TMP atlases can omit their sourceFontFile reference in builds.
        var source = asset != null ? asset.sourceFontFile : null;
        if (source == null) source = Resources.Load<Font>("Font/Baloo2-ExtraBold");
        if (source == null)
            throw new System.InvalidOperationException("Cannot load the source font for Resources/" + AssetPath);
        return source;
    }

    public static void ConfigureText(Text text, Font source = null, bool? singleLine = null)
    {
        text.font = source != null ? source : LoadSourceFont();
        // Baloo's line metrics include generous padding above and below the glyphs.
        // Truncate can discard a whole line even when its visible glyphs fit the HUD.
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.alignByGeometry = true;
        HomeTextFit.Configure(text, singleLine);
    }

    public static int ApplyToScene(Scene scene)
    {
        HomeMissionTypography.ApplyToScene(scene);
        var source = LoadSourceFont();
        var asset = Resources.Load<TMP_FontAsset>(AssetPath);
        int count = 0;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var text in root.GetComponentsInChildren<Text>(true))
            {
                ConfigureText(text, source);
                ApplyMissionLayout(text);
                count++;
            }
            if (asset == null) continue;
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                text.font = asset;
                count++;
            }
        }
        return count;
    }

    private static void ApplyMissionLayout(Text text)
    {
        var fitter = text.GetComponent<HomeTextFit>();
        var parent = text.transform.parent as RectTransform;
        if (parent == null) return;
        if (text.name == "Label" && (parent.name == "COINS" || parent.name == "ENERGY"))
            fitter.SetMaximumFontSize(parent.name == "COINS" ? 42 : 26, true);
        else if (text.name == "Label" && parent.name.EndsWith(" Header Banner"))
        {
            text.rectTransform.anchorMin = new Vector2(0.175f, 0.22f);
            text.rectTransform.anchorMax = new Vector2(0.855f, 0.78f);
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            fitter.SetMaximumFontSize(42, true);
        }
        else if (text.name == "Daily Login Panel Title") fitter.SetMaximumFontSize(28, false);
        else if (parent.Find(parent.name + " Progress Track") != null)
        {
            if (text.name.EndsWith(" Title"))
            {
                SetVerticalRect(text.rectTransform, 28, 40);
                fitter.SetMaximumFontSize(36, true);
                SetVerticalRect(parent.Find(parent.name + " Progress Track") as RectTransform, 122, 37);
                SetVerticalRect(parent.Find(parent.name + " Progress Fill") as RectTransform, 125, 31);
            }
            else if (text.name.EndsWith(" Description"))
            {
                SetVerticalRect(text.rectTransform, 79, 24);
                fitter.SetMaximumFontSize(20, true);
            }
            else if (text.name.EndsWith(" Progress Text"))
            {
                SetVerticalRect(text.rectTransform, 123, 35);
                fitter.SetMaximumFontSize(26, true);
            }
            else if (text.name.EndsWith(" Reward Amount")) fitter.SetMaximumFontSize(30, true);
        }
        fitter.FitNow();
    }

    private static void SetVerticalRect(RectTransform rect, float top, float height)
    {
        if (rect == null) return;
        rect.anchorMin = new Vector2(rect.anchorMin.x, 1f - (top + height) / 185f);
        rect.anchorMax = new Vector2(rect.anchorMax.x, 1f - top / 185f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
