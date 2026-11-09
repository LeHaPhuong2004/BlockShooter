using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

/// <summary>Fills a background rectangle by cropping its sprite without stretching the artwork.</summary>
[RequireComponent(typeof(Image))]
public sealed class BackgroundCover : BaseMeshEffect
{
    public override void ModifyMesh(VertexHelper vertices)
    {
        if (!IsActive()) return;
        var image = graphic as Image;
        Sprite sprite = image != null ? (image.overrideSprite != null ? image.overrideSprite : image.sprite) : null;
        if (sprite == null || sprite.rect.height <= 0f) return;
        Rect rect = graphic.GetPixelAdjustedRect();
        if (rect.width <= 0f || rect.height <= 0f) return;

        float imageAspect = sprite.rect.width / sprite.rect.height;
        float screenAspect = rect.width / rect.height;
        Vector4 uv = DataUtility.GetOuterUV(sprite);
        Vector2 center = new Vector2((uv.x + uv.z) * .5f, (uv.y + uv.w) * .5f);
        Vector2 crop = screenAspect > imageAspect
            ? new Vector2(1f, imageAspect / screenAspect)
            : new Vector2(screenAspect / imageAspect, 1f);
        UIVertex vertex = default;
        for (int i = 0; i < vertices.currentVertCount; i++)
        {
            vertices.PopulateUIVertex(ref vertex, i);
            vertex.uv0.x = center.x + (vertex.uv0.x - center.x) * crop.x;
            vertex.uv0.y = center.y + (vertex.uv0.y - center.y) * crop.y;
            vertices.SetUIVertex(vertex, i);
        }
    }
}
