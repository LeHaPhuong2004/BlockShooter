using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class Bullet : MonoBehaviour
{
    public const float TrailLifetime = 0.09f;

    public BlockColorData bulletColor;
    public float speed = 14f;

    [Header("Visual")]
    public SpriteRenderer bulletSprite;

    [Header("Flight Trail")]
    public TrailRenderer bulletTrail;

    [Header("Fire Visual")]
    public SpriteRenderer fireOverlay;

    public Vector2 LaunchPosition { get; private set; }

    // The circular artwork occupies 86% of the transparent sprite canvas.
    public float VisualDiameter => bulletSprite != null && bulletSprite.sprite != null
        ? Mathf.Min(bulletSprite.sprite.bounds.size.x * Mathf.Abs(bulletSprite.transform.lossyScale.x),
            bulletSprite.sprite.bounds.size.y * Mathf.Abs(bulletSprite.transform.lossyScale.y)) * 0.86f
        : 0.24f;

    private Block target;
    private BlockGroup targetGroup;
    private GridManager targetGrid;
    private DualBulletPair dualPair;
    private Vector2 targetPoint;

    private List<Vector2> path;
    private int pathIndex;

    private bool initialized;
    private bool hitTarget;
    private bool damageResolved;
    private bool despawning;
    private BulletPool pool;
    private Bullet sourcePrefab;
    private SpriteRenderer[] spriteRenderers;
    private Collider2D[] colliders;
    private bool[] colliderEnabled;
    private readonly Gradient trailGradient = new Gradient();
    private readonly GradientColorKey[] trailColors = new GradientColorKey[2];
    private static readonly GradientAlphaKey[] TrailAlpha =
    { new GradientAlphaKey(.8f, 0), new GradientAlphaKey(.35f, .5f), new GradientAlphaKey(0, 1) };
    private static readonly HashSet<Bullet> active = new HashSet<Bullet>();
    internal SpriteRenderer[] SpriteRenderers => spriteRenderers;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetActive() => active.Clear();
    public static Bullet Spawn(Bullet prefab, Vector3 position, Quaternion rotation) => Application.isPlaying
        ? BulletPool.Rent(prefab, position, rotation) : Instantiate(prefab, position, rotation);
    internal void SetPool(BulletPool owner, Bullet prefab) { pool = owner; sourcePrefab = prefab; }
    private void OnEnable() => active.Add(this);
    private void OnDisable() => active.Remove(this);
    internal static bool HasActive(Scene scene)
    {
        if (!Application.isPlaying)
        {
            foreach (var bullet in FindObjectsByType<Bullet>(FindObjectsInactive.Exclude))
                if (bullet.isActiveAndEnabled && bullet.gameObject.scene == scene) return true;
            return false;
        }
        foreach (var bullet in active)
            if (bullet != null && bullet.isActiveAndEnabled && bullet.gameObject.scene == scene) return true;
        return false;
    }

    private void CachePoolComponents()
    {
        if (spriteRenderers == null) spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        if (colliders != null) return;
        colliders = GetComponentsInChildren<Collider2D>(true);
        colliderEnabled = new bool[colliders.Length];
        for (int i = 0; i < colliders.Length; i++) colliderEnabled[i] = colliders[i].enabled;
    }

    private void Awake()
    {
        CachePoolComponents();
        if (bulletSprite == null)
            bulletSprite = GetComponentInChildren<SpriteRenderer>();

        if (bulletTrail == null)
            bulletTrail = GetComponentInChildren<TrailRenderer>();
        if (bulletTrail != null)
        {
            bulletTrail.emitting = false;
            bulletTrail.Clear();
        }

        if (fireOverlay == null)
        {
            SpriteRenderer[] renderers =
                GetComponentsInChildren<SpriteRenderer>();

            foreach (SpriteRenderer sr in renderers)
            {
                if (sr != bulletSprite)
                {
                    fireOverlay = sr;
                    break;
                }
            }
        }

        if (fireOverlay != null)
            fireOverlay.enabled = false;
    }

    public void Initialize(
        Block targetBlock,
        BlockColorData color,
        Vector2 targetPosition,
        List<Vector2> bulletPath)
    {
        LaunchPosition = transform.position;
        CachePoolComponents();
        enabled = true;
        dualPair = null;
        if (bulletSprite != null) bulletSprite.enabled = true;
        for (int i = 0; i < colliders.Length; i++) if (colliders[i] != null) colliders[i].enabled = colliderEnabled[i];
        var body = GetComponent<Rigidbody2D>();
        if (body != null) { body.linearVelocity = Vector2.zero; body.angularVelocity = 0; }
        target = targetBlock;
        targetGrid = BoardViewport.GetGrid(targetBlock);

        if (targetBlock != null)
            targetGroup = targetBlock.group;
        else
            targetGroup = null;

        bulletColor = color;
        targetPoint = targetPosition;

        SetBulletColor();

        if (path == null) path = new List<Vector2>(8);
        else path.Clear();
        if (bulletPath != null) path.AddRange(bulletPath);

        if (BoardViewport.TryGetBounds(targetGrid, out Bounds board))
        {
            // Keep the approach below the board so the gun can enter through an open lane.
            // Only the final aim point must be inside the visible board.
            float approachMinY = Mathf.Min(LaunchPosition.y, board.min.y);
            for (int i = 1; i < path.Count; i++)
                path[i] = new Vector2(Mathf.Clamp(path[i].x, board.min.x, board.max.x),
                    Mathf.Clamp(path[i].y, approachMinY, board.max.y));
            if (BoardViewport.TryGetTargetPoint(targetBlock, out Vector2 visiblePoint))
            {
                targetPoint = visiblePoint;
                if (path.Count > 1) path[path.Count - 1] = visiblePoint;
            }
        }

        pathIndex = 0;
        initialized = true;
        hitTarget = false;
        damageResolved = false;
        despawning = false;

        if (bulletTrail != null)
        {
            bulletTrail.time = TrailLifetime;
            bulletTrail.widthMultiplier = VisualDiameter;
            Color tint = bulletColor != null ? bulletColor.color : Color.white;
            trailColors[0] = new GradientColorKey(Color.Lerp(tint, Color.white, .35f), 0);
            trailColors[1] = new GradientColorKey(tint, 1);
            trailGradient.SetKeys(trailColors, TrailAlpha);
            bulletTrail.colorGradient = trailGradient;
            bulletTrail.enabled = true;
            if (bulletSprite != null)
            {
                bulletTrail.sortingLayerID = bulletSprite.sortingLayerID;
                bulletTrail.sortingOrder = bulletSprite.sortingOrder - 1;
            }
            bulletTrail.Clear();
            bulletTrail.emitting = true;
        }
        BoardProjectileClipping.Apply(this, targetGrid);
    }

    public void SetFireBullet(bool isFire)
    {
        if (fireOverlay == null)
            return;

        fireOverlay.enabled = isFire;
    }

    public static void PairDualShots(Bullet first, Bullet second)
    {
        if (first == null || second == null || first.targetGroup == null || first.targetGroup.groupType != BlockType.Dual ||
            first.targetGroup != second.targetGroup) return;
        var pairObject = new GameObject("Dual Bullet Pair");
        if (first.gameObject.scene.IsValid())
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(pairObject, first.gameObject.scene);
        var pair = pairObject.AddComponent<DualBulletPair>();
        pair.Bind(first, second, first.targetGroup);
        first.dualPair = second.dualPair = pair;
    }

    internal void CancelDualShot()
    {
        if (hitTarget) return;
        hitTarget = true;
        damageResolved = true;
        Despawn();
    }

    private void SetBulletColor()
    {
        if (bulletSprite == null)
            bulletSprite = GetComponentInChildren<SpriteRenderer>();

        if (bulletSprite == null)
            return;

        if (bulletColor == null)
            return;

        Color tint = Color.Lerp(bulletColor.color, Color.white, 0.08f);
        tint.a = 1f;
        bulletSprite.color = tint;
    }

    private void Update()
    {
        if (!initialized || hitTarget || Time.deltaTime <= 0f)
            return;

        if (!HasVisibleTarget())
        {
            ReleaseReservation();

            CheckLoseCondition();

            Despawn();
            return;
        }

        if (path == null || path.Count < 2)
        {
            ReleaseReservation();

            CheckLoseCondition();

            Despawn();
            return;
        }

        MoveAlongPath();
    }

    private bool HasVisibleTarget() => target != null && !target.IsDestroyed &&
        target.group != null && !target.group.IsDead() && BoardViewport.IsBlockVisible(target);

    private bool IsInsideBoard(Vector2 position)
    {
        if (!BoardViewport.TryGetBounds(targetGrid, out Bounds board)) return true;
        return position.x >= board.min.x && position.x <= board.max.x &&
            position.y >= board.min.y && position.y <= board.max.y;
    }

    private void MoveAlongPath()
    {
        if (pathIndex >= path.Count)
        {
            HitTarget(target);
            return;
        }

        Vector2 currentPosition = transform.position;
        Vector2 destination = path[pathIndex];

        Vector2 direction = destination - currentPosition;
        float distance = direction.magnitude;

        if (distance <= 0.2f)
        {
            transform.position = destination;
            pathIndex++;

            if (pathIndex >= path.Count)
                HitTarget(target);

            return;
        }

        direction.Normalize();

        float moveDistance =
            speed * Time.deltaTime;

        if (moveDistance >= distance)
        {
            transform.position = destination;
            pathIndex++;

            if (pathIndex >= path.Count)
                HitTarget(target);

            return;
        }

        transform.position +=
            (Vector3)(direction * moveDistance);

        RotateTowards(direction);
    }

    private void MoveDirectlyToTarget()
    {
        Vector2 currentPosition = transform.position;
        Vector2 direction = targetPoint - currentPosition;
        float distance = direction.magnitude;

        if (distance <= 0.2f)
        {
            HitTarget(target);
            return;
        }

        direction.Normalize();

        float moveDistance =
            speed * Time.deltaTime;

        if (moveDistance >= distance)
        {
            transform.position = targetPoint;
            HitTarget(target);
            return;
        }

        transform.position +=
            (Vector3)(direction * moveDistance);

        RotateTowards(direction);
    }

    private void RotateTowards(Vector2 direction)
    {
        float angle =
            Mathf.Atan2(
                direction.y,
                direction.x
            ) * Mathf.Rad2Deg;

        transform.rotation =
            Quaternion.Euler(
                0f,
                0f,
                angle
            );
    }

    private void OnTriggerEnter2D(
        Collider2D collision
    )
    {
        if (hitTarget)
            return;

        Block hitBlock =
            collision.GetComponent<Block>();

        if (hitBlock == null)
        {
            hitBlock =
                collision.GetComponentInParent<Block>();
        }

        if (hitBlock == null)
            return;

        // A group may continue above the mask. Do not hit one of its hidden members,
        // or an exposed block's collider while the projectile is still outside the board.
        if (!BoardViewport.IsBlockVisible(hitBlock) || !IsInsideBoard(transform.position))
            return;

        if (target == null)
            return;

        if (hitBlock == target)
        {
            HitTarget(hitBlock);
            return;
        }

        if (
            targetGroup != null &&
            hitBlock.group == targetGroup
        )
        {
            HitTarget(hitBlock);
            return;
        }
    }

    private void HitTarget(
        Block blockToDamage
    )
    {
        if (hitTarget)
            return;

        hitTarget = true;

        Block blockToUse =
            blockToDamage != null
                ? blockToDamage
                : target;

        if (
            blockToUse != null &&
            blockToUse.group != null &&
            !blockToUse.group.IsDead() &&
            BoardViewport.IsBlockVisible(blockToUse) &&
            IsInsideBoard(transform.position)
        )
        {
            damageResolved = true;

            if (dualPair != null)
            {
                dualPair.Arrive(this, blockToUse, transform.position, transform.right);
                Despawn();
                return;
            }

            Color splashColor = bulletColor != null ? bulletColor.color :
                blockToUse.blockColor != null ? blockToUse.blockColor.color : Color.cyan;
            WaterSplashEffect.Play(blockToUse, transform.position, transform.right, splashColor);

            blockToUse.TakeDamage(
                1,
                bulletColor
            );

            CheckLoseConditionDelayed();
        }
        else
        {
            ReleaseReservation();

            CheckLoseConditionDelayed();
        }

        Despawn();
    }

    private void Despawn()
    {
        if (despawning) return;
        despawning = true;
        if (!Application.isPlaying)
        {
            if (bulletTrail != null && bulletTrail.transform != transform)
                DestroyImmediate(bulletTrail.gameObject);
            DestroyImmediate(gameObject);
            return;
        }
        initialized = false;
        enabled = false;
        if (pool != null && sourcePrefab != null)
        {
            if (bulletTrail != null) bulletTrail.emitting = false;
            foreach (var renderer in spriteRenderers) if (renderer != null) renderer.enabled = false;
            foreach (var collider in colliders) if (collider != null) collider.enabled = false;
            // The pair stores the impact/color; it no longer owns this reusable projectile after arrival.
            target = null; targetGroup = null; targetGrid = null; dualPair = null;
            if (pool.Retire(this, sourcePrefab, bulletTrail != null ? bulletTrail.time + .05f : 0f)) return;
        }
        // Let the existing trail fade in place instead of disappearing at impact.
        // Scene unload still destroys it with the scene, so it cannot leak into the menu.
        if (bulletTrail != null && bulletTrail.transform != transform)
        {
            bulletTrail.emitting = false;
            bulletTrail.transform.SetParent(null, true);
            Destroy(bulletTrail.gameObject, bulletTrail.time + 0.05f);
            bulletTrail = null;
        }
        Destroy(gameObject);
    }

    private void ReleaseReservation()
    {
        if (damageResolved)
            return;

        if (targetGroup == null)
            return;

        damageResolved = true;

        if (dualPair != null)
        {
            dualPair.Cancel();
            return;
        }

        targetGroup.ReleaseDamage(
            1,
            bulletColor
        );
    }

    private void CheckLoseCondition()
    {
        if (GunGridManager.Instance == null)
            return;

        GunGridManager.Instance.CheckLoseCondition();
    }

    private void CheckLoseConditionDelayed()
    {
        // The manager schedules the stable-state check. A callback owned by this
        // projectile would be canceled as soon as it despawns at impact.
        CheckLoseCondition();
    }

    private void OnDestroy()
    {
        active.Remove(this);
        despawning = true;
        if (!damageResolved)
            ReleaseReservation();

        StopAllCoroutines();
    }
}
