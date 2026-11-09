using UnityEngine;
using UnityEngine.UI;

/// <summary>Golden reward aura and four-point glints, drawn behind existing reward artwork.</summary>
[AddComponentMenu("UI/Effects/Reward Icon Glow")]
[DisallowMultipleComponent]
public sealed class RewardIconGlow : MaskableGraphic
{
    private const int Segments = 16;
    private const float Tau = Mathf.PI * 2f;
    private static readonly Vector2[] Circle = CreateCircle();
    private static readonly Vector2[] Star =
    {
        new Vector2(0f, 1f), new Vector2(.18f, .18f),
        new Vector2(1f, 0f), new Vector2(.18f, -.18f),
        new Vector2(0f, -1f), new Vector2(-.18f, -.18f),
        new Vector2(-1f, 0f), new Vector2(-.18f, .18f)
    };
    [SerializeField, Range(0f, 1f)] private float phase;
    [SerializeField, HideInInspector] private Graphic rewardSource;
    private float animationTime;

    public override Texture mainTexture => Texture2D.whiteTexture;
    public Graphic RewardSource => rewardSource;

    public void BindSource(Graphic source, float cycleOffset)
    {
        rewardSource = source;
        Configure(cycleOffset);
    }

    public void Configure(float cycleOffset)
    {
        phase = Mathf.Repeat(cycleOffset, 1f);
        raycastTarget = false;
        maskable = true;
        SetVerticesDirty();
    }

    public void SetTime(float unscaledTime)
    {
        if (!IsActive() || canvasRenderer.cull || Mathf.Approximately(animationTime, unscaledTime)) return;
        animationTime = unscaledTime;
        SetVerticesDirty();
    }

    protected override void OnEnable()
    {
        raycastTarget = false;
        base.OnEnable();
    }

    protected override void OnPopulateMesh(VertexHelper vertices)
    {
        vertices.Clear();
        Rect bounds = GetPixelAdjustedRect();
        if (bounds.width <= 0f || bounds.height <= 0f) return;
        float pulse = .5f + .5f * Mathf.Sin((animationTime / 1.8f + phase) * Tau);
        float brightness = .58f + .42f * pulse;
        Vector2 radius = bounds.size * (.44f + .045f * pulse);
        AddAura(vertices, bounds.center, radius, brightness);

        float size = Mathf.Min(bounds.width, bounds.height);
        AddStar(vertices, bounds.min + Vector2.Scale(bounds.size, new Vector2(.17f, .77f)),
            size * .105f, Glint(animationTime * .68f + phase + .08f));
        AddStar(vertices, bounds.min + Vector2.Scale(bounds.size, new Vector2(.83f, .73f)),
            size * .088f, Glint(animationTime * .76f + phase + .48f));
        AddStar(vertices, bounds.min + Vector2.Scale(bounds.size, new Vector2(.77f, .19f)),
            size * .074f, Glint(animationTime * .61f + phase + .77f));
    }

    private void AddAura(VertexHelper vertices, Vector2 center, Vector2 radius, float brightness)
    {
        int first = vertices.currentVertCount;
        AddVertex(vertices, center, new Color(1f, .91f, .40f, .34f * brightness));
        for (int ring = 0; ring < 3; ring++)
        {
            float fraction = ring == 0 ? .24f : ring == 1 ? .65f : 1f;
            float alpha = ring == 0 ? .30f : ring == 1 ? .14f : 0f;
            Color tint = new Color(1f, ring == 0 ? .84f : .65f, .10f, alpha * brightness);
            for (int i = 0; i < Segments; i++)
                AddVertex(vertices, center + Vector2.Scale(Circle[i], radius) * fraction, tint);
        }
        for (int i = 0; i < Segments; i++)
        {
            int next = (i + 1) % Segments;
            vertices.AddTriangle(first, first + 1 + i, first + 1 + next);
            for (int ring = 0; ring < 2; ring++)
            {
                int inner = first + 1 + ring * Segments;
                int outer = inner + Segments;
                vertices.AddTriangle(inner + i, outer + i, outer + next);
                vertices.AddTriangle(inner + i, outer + next, inner + next);
            }
        }
    }

    private void AddStar(VertexHelper vertices, Vector2 center, float radius, float pulse)
    {
        int first = vertices.currentVertCount;
        radius *= .72f + .28f * pulse;
        float alpha = .06f + .94f * pulse;
        AddVertex(vertices, center, new Color(1f, 1f, .88f, alpha));
        for (int i = 0; i < Star.Length; i++)
            AddVertex(vertices, center + Star[i] * (radius * .43f), new Color(1f, .94f, .51f, alpha * .86f));
        for (int i = 0; i < Star.Length; i++)
            AddVertex(vertices, center + Star[i] * radius, new Color(1f, .72f, .13f, 0f));
        for (int i = 0; i < Star.Length; i++)
        {
            int next = (i + 1) % Star.Length;
            vertices.AddTriangle(first, first + 1 + i, first + 1 + next);
            vertices.AddTriangle(first + 1 + i, first + 9 + i, first + 9 + next);
            vertices.AddTriangle(first + 1 + i, first + 9 + next, first + 1 + next);
        }
    }

    private void AddVertex(VertexHelper vertices, Vector2 position, Color tint)
    {
        vertices.AddVert(position, tint * color, new Vector2(.5f, .5f));
    }

    private static float Glint(float cycle)
    {
        float pulse = .5f + .5f * Mathf.Sin(cycle * Tau);
        return pulse * pulse * pulse;
    }

    private static Vector2[] CreateCircle()
    {
        var directions = new Vector2[Segments];
        for (int i = 0; i < Segments; i++)
        {
            float angle = i * Tau / Segments;
            directions[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }
        return directions;
    }
}
