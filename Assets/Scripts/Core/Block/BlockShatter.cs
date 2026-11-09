using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;

public class BlockShatter : MonoBehaviour
{
    private const int FragmentColumns = 2;
    private const int FragmentRows = 2;
    private const float FragmentDurationMin = 1.05f;
    private const float FragmentDurationMax = 1.25f;

    public static void Shatter(Block block, float force)
    {
        if (block != null)
            Shatter(block.spriteRenderer, force);
    }

    public static void ShatterFrozen(Block block, float force)
    {
        if (block != null)
            Shatter(block.typeIconRenderer, force);
    }

    public static void ShatterReinforced(Block block, float force)
    {
        if (block != null)
            Shatter(block.typeIconRenderer, force);
    }

    public static void Shatter(SpriteRenderer source, float force)
    {
        if (source == null || force <= 0f)
            return;

        Sprite sprite = source.sprite;
        Bounds spriteBounds = sprite != null
            ? sprite.bounds
            : new Bounds(Vector3.zero, Vector3.one);
        if (spriteBounds.size.x <= 0f || spriteBounds.size.y <= 0f)
            return;

        Texture texture = sprite != null ? sprite.texture : Texture2D.whiteTexture;
        if (texture == null)
            return;

        Color sourceColor = source.color;
        Vector2[,] fractureGrid = BuildFractureGrid(spriteBounds);
        Vector2 worldSpriteSize = new Vector2(
            spriteBounds.size.x * Mathf.Abs(source.transform.lossyScale.x),
            spriteBounds.size.y * Mathf.Abs(source.transform.lossyScale.y));
        float worldSize = Mathf.Max(worldSpriteSize.x, worldSpriteSize.y);
        Vector2 spriteCenter = spriteBounds.center;

        for (int y = 0; y < FragmentRows; y++)
        {
            for (int x = 0; x < FragmentColumns; x++)
            {
                Vector2 bottomLeft = fractureGrid[x, y];
                Vector2 bottomRight = fractureGrid[x + 1, y];
                Vector2 topLeft = fractureGrid[x, y + 1];
                Vector2 topRight = fractureGrid[x + 1, y + 1];

                if (Random.value < 0.5f)
                {
                    SpawnTriangle(source, sprite, texture, spriteBounds,
                        bottomLeft, bottomRight, topRight, spriteCenter, worldSize, sourceColor, force);
                    SpawnTriangle(source, sprite, texture, spriteBounds,
                        bottomLeft, topRight, topLeft, spriteCenter, worldSize, sourceColor, force);
                }
                else
                {
                    SpawnTriangle(source, sprite, texture, spriteBounds,
                        bottomLeft, bottomRight, topLeft, spriteCenter, worldSize, sourceColor, force);
                    SpawnTriangle(source, sprite, texture, spriteBounds,
                        bottomRight, topRight, topLeft, spriteCenter, worldSize, sourceColor, force);
                }
            }
        }
    }

    private static Vector2[,] BuildFractureGrid(Bounds bounds)
    {
        var points = new Vector2[FragmentColumns + 1, FragmentRows + 1];
        float cellWidth = bounds.size.x / FragmentColumns;
        float cellHeight = bounds.size.y / FragmentRows;

        for (int y = 0; y <= FragmentRows; y++)
        {
            for (int x = 0; x <= FragmentColumns; x++)
            {
                float jitterX = x == 0 || x == FragmentColumns
                    ? 0f
                    : Random.Range(-0.12f, 0.12f) * cellWidth;
                float jitterY = y == 0 || y == FragmentRows
                    ? 0f
                    : Random.Range(-0.12f, 0.12f) * cellHeight;
                points[x, y] = new Vector2(
                    bounds.min.x + x * cellWidth + jitterX,
                    bounds.min.y + y * cellHeight + jitterY);
            }
        }

        return points;
    }

    private static void SpawnTriangle(
        SpriteRenderer source,
        Sprite sprite,
        Texture texture,
        Bounds spriteBounds,
        Vector2 a,
        Vector2 b,
        Vector2 c,
        Vector2 spriteCenter,
        float worldSize,
        Color sourceColor,
        float force)
    {
        Vector2 shardCenter = (a + b + c) / 3f;
        var piece = new GameObject("Block Shard");
        if (source.gameObject.scene.IsValid())
            SceneManager.MoveGameObjectToScene(piece, source.gameObject.scene);

        piece.transform.position = source.transform.TransformPoint(shardCenter);
        piece.transform.rotation = source.transform.rotation;
        piece.transform.localScale = source.transform.lossyScale;

        var mesh = new Mesh { name = "Block Shard Mesh" };
        mesh.vertices = new[]
        {
            (Vector3)(a - shardCenter),
            (Vector3)(b - shardCenter),
            (Vector3)(c - shardCenter)
        };
        mesh.uv = new[]
        {
            GetSpriteUv(sprite, texture, spriteBounds, a, source.flipX, source.flipY),
            GetSpriteUv(sprite, texture, spriteBounds, b, source.flipX, source.flipY),
            GetSpriteUv(sprite, texture, spriteBounds, c, source.flipX, source.flipY)
        };
        mesh.colors = new[] { Color.white, Color.white, Color.white };
        mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back };
        mesh.triangles = new[] { 0, 1, 2 };
        mesh.RecalculateBounds();

        var filter = piece.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        var renderer = piece.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = source.sharedMaterial;
        renderer.sortingLayerID = source.sortingLayerID;
        renderer.sortingOrder = source.sortingOrder + 1;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        var properties = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(properties);
        properties.SetTexture("_MainTex", texture);
        properties.SetColor("_Color", sourceColor);
        renderer.SetPropertyBlock(properties);

        Vector2 localDirection = shardCenter - spriteCenter;
        if (localDirection.sqrMagnitude < 0.0001f)
            localDirection = Random.insideUnitCircle;
        Vector3 direction = source.transform.TransformDirection(localDirection.normalized);
        float horizontalSign = Mathf.Approximately(direction.x, 0f)
            ? (Random.value < 0.5f ? -1f : 1f)
            : Mathf.Sign(direction.x);
        direction = new Vector3(horizontalSign, Random.Range(0.12f, 0.32f), 0f);
        direction.Normalize();

        float duration = Random.Range(FragmentDurationMin, FragmentDurationMax);
        float forceScale = Mathf.Sqrt(Mathf.Max(0.2f, force));
        float launchSpeed = Random.Range(0.9f, 1.5f) * Mathf.Max(worldSize, 0.25f) * 1.25f * forceScale;
        Vector3 velocity = direction * launchSpeed;
        velocity.y += Random.Range(0.7f, 1.15f) * Mathf.Max(worldSize, 0.25f) * 3f * forceScale;
        const float gravity = 8.5f;
        const float fadeStartRatio = 0.82f;
        Vector3 startPosition = piece.transform.position;
        Vector3 startRotation = piece.transform.eulerAngles;
        float spinSpeed = Random.Range(-850f, 850f) * forceScale;

        Tween flight = DOTween.To(() => 0f, elapsed =>
        {
            if (piece == null)
                return;

            piece.transform.position = startPosition + velocity * elapsed
                + Vector3.down * (0.5f * gravity * elapsed * elapsed);
            piece.transform.rotation = Quaternion.Euler(
                startRotation + new Vector3(0f, 0f, spinSpeed * elapsed));

            float fadeProgress = Mathf.InverseLerp(duration * fadeStartRatio, duration, elapsed);
            Color faded = sourceColor;
            faded.a *= 1f - fadeProgress;
            properties.SetColor("_Color", faded);
            renderer.SetPropertyBlock(properties);
        }, duration, duration).SetEase(Ease.Linear);
        flight.SetLink(piece, LinkBehaviour.KillOnDestroy);
        flight.OnKill(() =>
        {
            if (piece != null)
                Destroy(piece);
            if (mesh != null)
                Destroy(mesh);
        });
    }

    private static Vector2 GetSpriteUv(
        Sprite sprite,
        Texture texture,
        Bounds bounds,
        Vector2 localPosition,
        bool flipX,
        bool flipY)
    {
        if (sprite == null)
            return new Vector2(
                (localPosition.x - bounds.min.x) / bounds.size.x,
                (localPosition.y - bounds.min.y) / bounds.size.y);

        float normalizedX = (localPosition.x - bounds.min.x) / bounds.size.x;
        float normalizedY = (localPosition.y - bounds.min.y) / bounds.size.y;
        if (flipX) normalizedX = 1f - normalizedX;
        if (flipY) normalizedY = 1f - normalizedY;

        Rect textureRect = sprite.textureRect;
        return new Vector2(
            (textureRect.x + normalizedX * textureRect.width) / texture.width,
            (textureRect.y + normalizedY * textureRect.height) / texture.height);
    }
}
