using UnityEngine;
using UnityEngine.UI;

/// <summary>Fits visible glyph bounds, rather than the font's padded line height, to a Home label.</summary>
[ExecuteAlways, RequireComponent(typeof(Text))]
public sealed class HomeTextFit : BaseMeshEffect
{
    [SerializeField] private int maximumFontSize;
    [SerializeField] private bool singleLine;
    private Text label;
    private TextGenerator generator;
    private string lastText;
    private Vector2 lastSize;
    private Font lastFont;
    private FontStyle lastStyle;
    private int lastAppliedSize;
    private float lastScale;
    private bool dirty = true;

    public static void Configure(Text text, bool? forceSingleLine = null)
    {
        var fitter = text.GetComponent<HomeTextFit>();
        if (fitter == null) fitter = text.gameObject.AddComponent<HomeTextFit>();
        fitter.label = text;
        if (text.resizeTextForBestFit)
            fitter.maximumFontSize = text.resizeTextMaxSize;
        else if (fitter.maximumFontSize <= 0)
            fitter.maximumFontSize = text.fontSize;
        if (forceSingleLine.HasValue) fitter.singleLine = forceSingleLine.Value;
        text.resizeTextForBestFit = false;
        fitter.dirty = true;
        fitter.FitNow();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        label = GetComponent<Text>();
        dirty = true;
        Canvas.willRenderCanvases += FitNow;
    }

    protected override void OnDisable()
    {
        Canvas.willRenderCanvases -= FitNow;
        base.OnDisable();
    }
    protected override void OnRectTransformDimensionsChange()
    {
        dirty = true;
        base.OnRectTransformDimensionsChange();
    }
    private void LateUpdate() => FitNow();

    public void SetMaximumFontSize(int size, bool forceSingleLine)
    {
        if (maximumFontSize == size && singleLine == forceSingleLine) return;
        maximumFontSize = size;
        singleLine = forceSingleLine;
        dirty = true;
    }

    public void FitNow()
    {
        if (label == null || label.font == null || !label.isActiveAndEnabled) return;
        var size = label.rectTransform.rect.size;
        if (size.x <= 0f || size.y <= 0f) return;
        if (label.resizeTextForBestFit)
        {
            maximumFontSize = label.resizeTextMaxSize;
            label.resizeTextForBestFit = false;
            dirty = true;
        }
        else if (lastAppliedSize > 0 && label.fontSize != lastAppliedSize)
        {
            maximumFontSize = label.fontSize;
            dirty = true;
        }
        if (!dirty && lastText == label.text && lastSize == size && lastFont == label.font
            && lastStyle == label.fontStyle && Mathf.Approximately(lastScale, label.pixelsPerUnit)) return;
        maximumFontSize = Mathf.Max(6, maximumFontSize > 0 ? maximumFontSize : label.fontSize);
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.horizontalOverflow = singleLine ? HorizontalWrapMode.Overflow : HorizontalWrapMode.Wrap;
        label.alignByGeometry = true;

        if (generator == null) generator = new TextGenerator();
        int low = 6;
        int high = maximumFontSize;
        int best = low;
        while (low <= high)
        {
            int candidate = (low + high) / 2;
            var bounds = Measure(candidate);
            if (bounds.x <= size.x - 4f && bounds.y <= size.y - 4f)
            {
                best = candidate;
                low = candidate + 1;
            }
            else high = candidate - 1;
        }
        label.fontSize = best;
        lastAppliedSize = best;
        lastText = label.text;
        lastSize = size;
        lastFont = label.font;
        lastStyle = label.fontStyle;
        lastScale = label.pixelsPerUnit;
        dirty = false;
    }

    private Vector2 Measure(int size)
    {
        var settings = label.GetGenerationSettings(label.rectTransform.rect.size);
        settings.fontSize = size;
        settings.resizeTextForBestFit = false;
        generator.Populate(label.text, settings);
        return GlyphSize(generator, label.pixelsPerUnit);
    }

    public static Vector2 GlyphSize(TextGenerator text, float pixelsPerUnit)
    {
        var vertices = text.verts;
        int count = vertices.Count - 4; // UI.Text's generator appends an unused quad.
        if (count <= 0) return Vector2.zero;
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        for (int i = 0; i < count; i++)
        {
            Vector2 point = vertices[i].position;
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }
        return (max - min) / Mathf.Max(0.01f, pixelsPerUnit);
    }

    public override void ModifyMesh(VertexHelper mesh)
    {
        if (!IsActive() || label == null || mesh.currentVertCount == 0) return;
        // Legacy Text's vertical alignment still uses line metrics. Center the actual
        // glyph mesh in its own row so Baloo's extra font padding cannot shift it down.
        float min = float.MaxValue;
        float max = float.MinValue;
        var vertex = new UIVertex();
        for (int i = 0; i < mesh.currentVertCount; i++)
        {
            mesh.PopulateUIVertex(ref vertex, i);
            min = Mathf.Min(min, vertex.position.y);
            max = Mathf.Max(max, vertex.position.y);
        }
        var rect = label.rectTransform.rect;
        int verticalAlignment = (int)label.alignment / 3;
        float offset = verticalAlignment == 0 ? rect.yMax - 2f - max
            : verticalAlignment == 2 ? rect.yMin + 2f - min
            : rect.center.y - (min + max) * 0.5f;
        for (int i = 0; i < mesh.currentVertCount; i++)
        {
            mesh.PopulateUIVertex(ref vertex, i);
            vertex.position.y += offset;
            mesh.SetUIVertex(vertex, i);
        }
    }

    protected override void OnDestroy()
    {
        if (generator != null) ((System.IDisposable)generator).Dispose();
        base.OnDestroy();
    }
}
