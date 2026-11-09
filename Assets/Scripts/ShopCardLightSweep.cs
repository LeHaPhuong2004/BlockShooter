using UnityEngine;
using UnityEngine.UI;

/// <summary>Supplies card-local coordinates without changing the source sprite or its input area.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public sealed class ShopCardLightSweep : BaseMeshEffect
{
    private static readonly int SweepTime = Shader.PropertyToID("_SweepTime");
    [SerializeField, Range(0f, 1f)] private float phase;

    public void Configure(Image source, Material material, float cycleOffset)
    {
        var image = (Image)graphic;
        image.sprite = source.overrideSprite != null ? source.overrideSprite : source.sprite;
        image.type = source.type;
        image.preserveAspect = source.preserveAspect;
        image.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
        image.fillCenter = source.fillCenter;
        image.maskable = source.maskable;
        image.color = Color.white;
        image.raycastTarget = false;
        image.material = material;
        image.enabled = true;
        phase = Mathf.Repeat(cycleOffset, 1f);
        EnableCoordinates();
        image.SetVerticesDirty();
    }

    public void SetTime(float unscaledTime)
    {
        if (!IsActive() || graphic == null || graphic.canvasRenderer.cull) return;
        // A stencil Mask may supply a material instance of its own.
        Material material = graphic.materialForRendering;
        if (material != null) material.SetFloat(SweepTime, unscaledTime);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        EnableCoordinates();
    }

    protected override void OnCanvasHierarchyChanged()
    {
        base.OnCanvasHierarchyChanged();
        EnableCoordinates();
    }

    private void EnableCoordinates()
    {
        Canvas canvas = graphic != null ? graphic.canvas : null;
        if (canvas != null && (canvas.additionalShaderChannels & AdditionalCanvasShaderChannels.TexCoord1) == 0)
            canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
    }

    public override void ModifyMesh(VertexHelper vertices)
    {
        if (!IsActive() || vertices.currentVertCount == 0) return;
        UIVertex vertex = default;
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i < vertices.currentVertCount; i++)
        {
            vertices.PopulateUIVertex(ref vertex, i);
            min = Vector2.Min(min, vertex.position);
            max = Vector2.Max(max, vertex.position);
        }
        Vector2 size = Vector2.Max(max - min, Vector2.one * .001f);
        for (int i = 0; i < vertices.currentVertCount; i++)
        {
            vertices.PopulateUIVertex(ref vertex, i);
            vertex.uv1 = new Vector4((vertex.position.x - min.x) / size.x,
                (vertex.position.y - min.y) / size.y, phase, 0f);
            vertices.SetUIVertex(vertex, i);
        }
    }
}
