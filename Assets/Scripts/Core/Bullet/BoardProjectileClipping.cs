using UnityEngine;

/// <summary>Keeps the muzzle-to-board approach visible while clipping the top and sides.</summary>
public static class BoardProjectileClipping
{
    private static readonly int ClipRect = Shader.PropertyToID("_BoardClipRect");
    private static readonly int ClipEnabled = Shader.PropertyToID("_BoardClipEnabled");
    private static Material spriteMaterial;
    private static MaterialPropertyBlock properties;

    public static bool TryGetFlightBounds(Bullet bullet, GridManager manager, out Bounds bounds)
    {
        if (!BoardViewport.TryGetBounds(manager, out bounds)) return false;
        if (bullet == null) return true;
        float radius = bullet.VisualDiameter / 1.72f;
        if (bullet.bulletTrail != null) radius = Mathf.Max(radius, bullet.bulletTrail.widthMultiplier * 0.5f);
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        min.y = Mathf.Min(min.y, bullet.LaunchPosition.y - radius - 0.03f);
        bounds.SetMinMax(min, max);
        return true;
    }

    public static void Apply(Bullet bullet, GridManager manager)
    {
        if (bullet == null) return;
        if (TryGetFlightBounds(bullet, manager, out Bounds bounds))
        {
            if (spriteMaterial == null)
                spriteMaterial = Resources.Load<Material>("GameScreenSkin/BoardProjectileSprite");
            if (properties == null) properties = new MaterialPropertyBlock();
            // A board SpriteMask would hide the flight from the muzzle up to its bottom edge.
            foreach (SpriteRenderer renderer in bullet.SpriteRenderers ?? bullet.GetComponentsInChildren<SpriteRenderer>(true))
            {
                renderer.maskInteraction = SpriteMaskInteraction.None;
                if (spriteMaterial == null) continue;
                renderer.sharedMaterial = spriteMaterial;
                renderer.GetPropertyBlock(properties);
                properties.SetFloat(ClipEnabled, 1f);
                properties.SetVector(ClipRect, new Vector4(bounds.min.x, bounds.min.y, bounds.max.x, bounds.max.y));
                renderer.SetPropertyBlock(properties);
            }
        }
        RefreshTrail(bullet.bulletTrail, manager);
    }

    public static void RefreshTrail(TrailRenderer trail, GridManager manager)
    {
        if (trail == null) return;
        Bullet owner = trail.GetComponentInParent<Bullet>();
        // A detached trail retains its launch bounds throughout the fade.
        if (owner == null) return;
        bool hasBoard = TryGetFlightBounds(owner, manager, out Bounds bounds);
        if (properties == null) properties = new MaterialPropertyBlock();
        trail.GetPropertyBlock(properties);
        properties.SetFloat(ClipEnabled, hasBoard ? 1f : 0f);
        if (hasBoard)
            properties.SetVector(ClipRect, new Vector4(bounds.min.x, bounds.min.y, bounds.max.x, bounds.max.y));
        // The property block stays on the trail when Bullet.Despawn detaches it to finish fading.
        trail.SetPropertyBlock(properties);
    }
}
