using UnityEngine;
using UnityEngine.UI;

/// <summary>Closes subpixel viewport seams visually without extending the side strip's input rectangle.</summary>
public sealed class BackgroundEdgeOverlap : BaseMeshEffect
{
    public bool extendRight;

    public override void ModifyMesh(VertexHelper vertices)
    {
        if (!IsActive()) return;
        Rect rect = graphic.GetPixelAdjustedRect();
        float pixel = 1f / Mathf.Max(.0001f, graphic.canvas != null ? graphic.canvas.scaleFactor : 1f);
        UIVertex vertex = default;
        for (int i = 0; i < vertices.currentVertCount; i++)
        {
            vertices.PopulateUIVertex(ref vertex, i);
            if (extendRight && vertex.position.x > rect.center.x) vertex.position.x += pixel;
            else if (!extendRight && vertex.position.x < rect.center.x) vertex.position.x -= pixel;
            vertices.SetUIVertex(vertex, i);
        }
    }
}
