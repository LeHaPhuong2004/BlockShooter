using UnityEngine;
using System.Runtime.CompilerServices;

/// <summary>The playable area is the SpriteMask authored on the block grid.</summary>
public static class BoardViewport
{
    private sealed class References { public Collider2D collider; public GridManager grid; }
    private static ConditionalWeakTable<Block, References> references = new ConditionalWeakTable<Block, References>();
    private static GridManager boundsOwner;
    private static SpriteMask boundsMask;
    private static Sprite boundsSprite;
    private static Matrix4x4 boundsMatrix;
    private static Bounds cachedBounds;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCaches()
    {
        references = new ConditionalWeakTable<Block, References>();
        boundsOwner = null; boundsMask = null; boundsSprite = null;
    }

    private static References GetReferences(Block block)
    {
        if (!references.TryGetValue(block, out References entry))
        {
            entry = new References();
            references.Add(block, entry);
        }
        if (entry.collider == null) entry.collider = block.GetComponentInChildren<Collider2D>();
        if (entry.grid == null || !block.transform.IsChildOf(entry.grid.transform))
            entry.grid = block.GetComponentInParent<GridManager>();
        return entry;
    }

    internal static GridManager GetGrid(Block block) => block != null ? GetReferences(block).grid : null;

    public static bool TryGetBounds(GridManager manager, out Bounds bounds)
    {
        bounds = default;
        if (manager == null) return false;
        if (boundsOwner != manager || boundsMask == null)
        {
            boundsOwner = manager;
            boundsMask = manager.GetComponent<SpriteMask>();
            boundsSprite = null;
        }
        SpriteMask mask = boundsMask;
        if (mask == null || !mask.enabled || mask.sprite == null) return false;
        Matrix4x4 matrix = mask.transform.localToWorldMatrix;
        if (boundsSprite == mask.sprite && boundsMatrix == matrix)
        {
            bounds = cachedBounds;
            return bounds.size.x > 0f && bounds.size.y > 0f;
        }
        Bounds local = mask.sprite.bounds;
        Transform area = mask.transform;
        bounds = new Bounds(area.TransformPoint(new Vector3(local.min.x, local.min.y)), Vector3.zero);
        bounds.Encapsulate(area.TransformPoint(new Vector3(local.min.x, local.max.y)));
        bounds.Encapsulate(area.TransformPoint(new Vector3(local.max.x, local.min.y)));
        bounds.Encapsulate(area.TransformPoint(new Vector3(local.max.x, local.max.y)));
        boundsSprite = mask.sprite; boundsMatrix = matrix; cachedBounds = bounds;
        return bounds.size.x > 0f && bounds.size.y > 0f;
    }

    public static bool IsBlockVisible(Block block) => TryGetTargetPoint(block, out _);

    public static bool TryGetTargetPoint(Block block, out Vector2 point)
    {
        point = Vector2.zero;
        if (block == null || block.IsDestroyed || !block.gameObject.activeInHierarchy) return false;
        References entry = GetReferences(block);
        Collider2D collider = entry.collider;
        point = collider != null && collider.enabled ? (Vector2)collider.bounds.center : (Vector2)block.transform.position;
        GridManager manager = entry.grid;
        if (!TryGetBounds(manager, out Bounds board)) return true;

        Bounds visual = default;
        bool hasVisual = false;
        AddVisual(block.spriteRenderer, ref visual, ref hasVisual);
        AddVisual(block.typeIconRenderer, ref visual, ref hasVisual);
        AddVisual(block.dualTopRenderer, ref visual, ref hasVisual);
        AddVisual(block.dualBottomRenderer, ref visual, ref hasVisual);
        if (!hasVisual)
        {
            // Collider-only fixtures and legacy blocks have no visual references.
            if (block.spriteRenderer != null || block.typeIconRenderer != null ||
                block.dualTopRenderer != null || block.dualBottomRenderer != null || collider == null)
                return false;
            visual = collider.bounds;
        }

        Vector3 min = Vector3.Max(board.min, visual.min);
        Vector3 max = Vector3.Min(board.max, visual.max);
        if (collider != null && collider.enabled)
        {
            min = Vector3.Max(min, collider.bounds.min);
            max = Vector3.Min(max, collider.bounds.max);
        }
        if (max.x - min.x <= 0.0001f || max.y - min.y <= 0.0001f) return false;

        // Aim at the exposed part, including a block whose center is still above the mask.
        float insetX = Mathf.Min(0.005f, (max.x - min.x) * 0.25f);
        float insetY = Mathf.Min(0.005f, (max.y - min.y) * 0.25f);
        point = new Vector2(Mathf.Clamp(point.x, min.x + insetX, max.x - insetX),
            Mathf.Clamp(point.y, min.y + insetY, max.y - insetY));
        return true;
    }

    private static void AddVisual(SpriteRenderer renderer, ref Bounds bounds, ref bool any)
    {
        if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
            renderer.sprite == null || renderer.color.a <= 0f) return;
        if (!any) { bounds = renderer.bounds; any = true; }
        else bounds.Encapsulate(renderer.bounds);
    }
}
