using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Draws the authored color split in the sprite shader, leaving SpriteMask's
/// stencil available for the board's viewport mask.
/// </summary>
public static class DualBlockSpriteMask
{
    private const string MaterialPath = "GameScreenSkin/DualBlockMaskedSprite";
    private static readonly int MaskTexture = Shader.PropertyToID("_MaskTex");
    private static readonly int MaskUvX = Shader.PropertyToID("_MaskUvX");
    private static readonly int MaskUvY = Shader.PropertyToID("_MaskUvY");
    private static readonly int MaskLocalRect = Shader.PropertyToID("_MaskLocalRect");
    private static readonly int MaskCutoff = Shader.PropertyToID("_MaskCutoff");
    private static Material material;

    /// <summary>Applies only to a spawned dual block; does not alter its template.</summary>
    public static bool Apply(Block block)
    {
        if (block == null || block.dualTopRenderer == null ||
            block.dualBottomRenderer == null ||
            block.dualTopRenderer == block.dualBottomRenderer)
            return false;

        SpriteRenderer top = block.dualTopRenderer;
        SpriteRenderer bottom = block.dualBottomRenderer;
        SpriteMask topMask = top.GetComponent<SpriteMask>();
        SpriteMask bottomMask = bottom.GetComponent<SpriteMask>();
        if (!TryGetMapping(topMask, out Vector4 topUvX, out Vector4 topUvY, out Vector4 topRect) ||
            !TryGetMapping(bottomMask, out Vector4 bottomUvX, out Vector4 bottomUvY, out Vector4 bottomRect))
            return false;

        if (material == null) material = Resources.Load<Material>(MaterialPath);
        if (material == null || material.shader == null || !material.shader.isSupported)
            return false;

        SpriteRenderer body = block.spriteRenderer;
        int layer = body != null ? body.sortingLayerID : top.sortingLayerID;
        int order = body != null ? body.sortingOrder : 0;
        Configure(top, topMask, topUvX, topUvY, topRect, layer, order);
        Configure(bottom, bottomMask, bottomUvX, bottomUvY, bottomRect, layer, order);

        // Configure both halves before removing their old stencil isolation.
        topMask.enabled = false;
        bottomMask.enabled = false;
        SortingGroup group = block.GetComponent<SortingGroup>();
        if (group != null) group.enabled = false;
        return true;
    }

    private static void Configure(SpriteRenderer renderer, SpriteMask mask,
        Vector4 uvX, Vector4 uvY, Vector4 rect, int layer, int order)
    {
        var properties = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(properties);
        properties.SetTexture(MaskTexture, mask.sprite.texture);
        properties.SetVector(MaskUvX, uvX);
        properties.SetVector(MaskUvY, uvY);
        properties.SetVector(MaskLocalRect, rect);
        properties.SetFloat(MaskCutoff, Mathf.Max(0.00001f, mask.alphaCutoff));
        renderer.sharedMaterial = material;
        renderer.SetPropertyBlock(properties);
        renderer.sortingLayerID = layer;
        renderer.sortingOrder = order;
        renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
    }

    // An affine mapping preserves sprite pivots and packed/rotated atlas UVs.
    // Mask and color renderer share the same transform, so no transform changes
    // or assumptions about the mask triangle's orientation are necessary.
    private static bool TryGetMapping(SpriteMask mask,
        out Vector4 uvX, out Vector4 uvY, out Vector4 rect)
    {
        uvX = uvY = rect = Vector4.zero;
        if (mask == null || mask.sprite == null || mask.sprite.texture == null)
            return false;

        Sprite sprite = mask.sprite;
        Vector2[] vertices = sprite.vertices;
        Vector2[] uvs = sprite.uv;
        if (vertices.Length < 3 || vertices.Length != uvs.Length) return false;

        Vector2 a = vertices[0];
        for (int i = 1; i < vertices.Length - 1; i++)
        {
            for (int j = i + 1; j < vertices.Length; j++)
            {
                Vector2 ab = vertices[i] - a;
                Vector2 ac = vertices[j] - a;
                float determinant = ab.x * ac.y - ab.y * ac.x;
                if (Mathf.Abs(determinant) < 0.0000001f) continue;
                Vector2 uvAB = uvs[i] - uvs[0];
                Vector2 uvAC = uvs[j] - uvs[0];
                float ux = (uvAB.x * ac.y - uvAC.x * ab.y) / determinant;
                float uy = (ab.x * uvAC.x - ac.x * uvAB.x) / determinant;
                float vx = (uvAB.y * ac.y - uvAC.y * ab.y) / determinant;
                float vy = (ab.x * uvAC.y - ac.x * uvAB.y) / determinant;
                uvX = new Vector4(ux, uy, uvs[0].x - ux * a.x - uy * a.y, 0f);
                uvY = new Vector4(vx, vy, uvs[0].y - vx * a.x - vy * a.y, 0f);
                Bounds bounds = sprite.bounds;
                rect = new Vector4(bounds.min.x, bounds.min.y, bounds.max.x, bounds.max.y);
                return true;
            }
        }
        return false;
    }
}
