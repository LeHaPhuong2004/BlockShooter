using DG.Tweening;
using TMPro;
using UnityEngine;
using System.Collections.Generic;

public enum GunType
{
    Normal,
    Mystery,
    Fire

}

public class Gun : MonoBehaviour
{
    public bool inGrid = true;
    public bool lockedInTray = false;

    [Header("Grid")]
    public int gridX;
    public int gridY;

    [HideInInspector]
    public GunChain chain;

    [Header("Gun Ammo")]
    public int maxHP;
    public int currentHP;
    public TMP_Text hpText;

    [Header("Gun Visual")]
    public SpriteRenderer gunSprite;
    public Transform gunVisual;
    public Sprite questionMarkSprite;

    private Sprite originalSprite;
    private bool isRevealed;
    public bool mysteryHidden;

    [Header("Gun")]
    public Bullet bulletPrefab;
    public Transform firePoint;
    public BlockColorData currentColor;
    public float fireRate = 0.5f;

    [Header("Tray")]
    private TraySlot currentTray;
    public TraySlot CurrentTray => currentTray;

    public Vector3 homePosition;
    public Vector3 homeScale;

    [SerializeField]
    private bool _canShoot = false;

    [Header("Gun Type")]
    public GunType gunType = GunType.Normal;

    [Header("Effect")]
    public GameObject effect;

    public bool canShoot
    {
        get => _canShoot;
        set
        {
            if (_canShoot == value)
                return;

            _canShoot = value;
        }
    }

    private float nextFireTime;
    private bool isDragging;
    private Vector3 originalScale;
    private Vector3 originalVisualScale;
    private GunRemovalManager removal;
    private Animator[] wheelAnimators;
    private Sequence recoilSequence;
    private RaycastHit2D[] raycastHits = new RaycastHit2D[32];
    private readonly Dictionary<Collider2D, Block> colliderBlocks = new Dictionary<Collider2D, Block>(128);
    private readonly Dictionary<Block, VisiblePoint> visiblePoints = new Dictionary<Block, VisiblePoint>(128);

    private BoxCollider2D bulletCollider;
    private Vector2 bulletHalfSize;

    private readonly Dictionary<BlockGroup, ShootTarget> plannedGroups = new Dictionary<BlockGroup, ShootTarget>();
    private readonly HashSet<BlockGroup> candidateGroupSet = new HashSet<BlockGroup>();
    private readonly List<GroupCandidate> candidateGroups = new List<GroupCandidate>(32);
    private readonly List<BlockCandidate> candidateBlocks = new List<BlockCandidate>(16);
    private System.Func<Vector2, Vector2, bool> planningClear;
    private BlockGroup planningGroup;
    private int targetQueryDepth;
    private static readonly Unity.Profiling.ProfilerMarker TargetSearchProfiler = new Unity.Profiling.ProfilerMarker("Gun.TargetSearch");
    private static readonly System.Comparison<GroupCandidate> CompareGroups = (a, b) =>
        a.lowerBound != b.lowerBound ? a.lowerBound.CompareTo(b.lowerBound) : a.order.CompareTo(b.order);
    private static readonly System.Comparison<BlockCandidate> CompareBlocks = (a, b) =>
        a.lowerBound != b.lowerBound ? a.lowerBound.CompareTo(b.lowerBound) : a.order.CompareTo(b.order);

    private struct VisiblePoint { public bool visible; public Vector2 point; }
    private struct GroupCandidate { public BlockGroup group; public float lowerBound; public int order; }
    private struct BlockCandidate { public Block block; public Vector2 point; public float lowerBound; public int order; }

    private void BeginTargetQuery()
    {
        if (targetQueryDepth++ == 0) { visiblePoints.Clear(); plannedGroups.Clear(); }
    }
    private void EndTargetQuery() { targetQueryDepth--; }
    private bool TryVisiblePoint(Block block, out Vector2 point)
    {
        point = Vector2.zero;
        if (block == null || block.IsDestroyed) return false;
        if (targetQueryDepth > 0 && visiblePoints.TryGetValue(block, out VisiblePoint saved))
        { point = saved.point; return saved.visible; }
        bool visible = BoardViewport.TryGetTargetPoint(block, out point);
        if (targetQueryDepth > 0) visiblePoints[block] = new VisiblePoint { visible = visible, point = point };
        return visible;
    }
    private bool IsPlanningSegmentClear(Vector2 start, Vector2 end) => IsSegmentClear(start, end, planningGroup);
    private bool CacheBulletCollider()
    {
        if (bulletCollider != null && bulletCollider.enabled)
            return true;

        if (bulletPrefab == null)
            return false;

        bulletCollider = bulletPrefab.GetComponentInChildren<BoxCollider2D>(true);

        if (bulletCollider == null)
            return false;

        Vector3 scale = bulletCollider.transform.lossyScale;

        bulletHalfSize = new Vector2(
            Mathf.Abs(bulletCollider.size.x * scale.x) * 0.5f,
            Mathf.Abs(bulletCollider.size.y * scale.y) * 0.5f
        );

        return bulletHalfSize.x > 0f && bulletHalfSize.y > 0f;
    }
    private void Awake()
    {
        if (gunSprite != null)
            gunSprite.color = Color.white;

        originalScale = transform.localScale;

        if (gunVisual != null)
            originalVisualScale = gunVisual.localScale;

        if (effect != null)
            effect.SetActive(false);

        SetWheelRolling(false);
    }

    private void Start()
    {
        homePosition = transform.position;
        homeScale = transform.localScale;

        UpdateHPText();

        GunGridManager manager =
            GunGridManager.Instance;

        if (manager != null)
            removal = manager.Removal;
    }

    private void Update()
    {
        if (Time.timeScale <= 0f) return;
        UpdateMysteryRow();

        if (isDragging)
            return;

        if (!canShoot)
        {
            if (currentHP > 0)
                return;
        }

        if (currentHP <= 0)
        {
            canShoot = false;
            return;
        }

        if (Time.time < nextFireTime)
            return;

        bool shot = Shoot();

        nextFireTime =
            shot
                ? Time.time + fireRate
                : Time.time + 0.05f;
    }

    private void UpdateMysteryRow()
    {
        if (!mysteryHidden)
            return;

        if (!inGrid)
            return;

        GunGridManager manager =
            GunGridManager.Instance;

        if (manager == null)
            return;

        int topRow =
            manager.rows - 1;

        if (gridY == topRow)
            RevealGun();
    }

    private void OnDestroy()
    {
        SetWheelRolling(false);

        transform.DOKill(false);

        if (gunVisual != null)
            gunVisual.DOKill(false);
    }

    private void OnDisable()
    {
        SetWheelRolling(false);
    }

    public void SetWheelRolling(bool rolling)
    {
        if (wheelAnimators == null || wheelAnimators.Length == 0)
            CacheWheelAnimators();

        if (wheelAnimators == null)
            return;

        foreach (Animator wheelAnimator in wheelAnimators)
        {
            if (wheelAnimator != null)
                wheelAnimator.speed = rolling ? 1f : 0f;
        }
    }

    private void CacheWheelAnimators()
    {
        Animator[] childAnimators =
            GetComponentsInChildren<Animator>(true);

        List<Animator> foundWheelAnimators =
            new List<Animator>();

        foreach (Animator childAnimator in childAnimators)
        {
            if (childAnimator == null)
                continue;

            string objectName =
                childAnimator.gameObject.name.ToLowerInvariant();

            string controllerName =
                childAnimator.runtimeAnimatorController != null
                    ? childAnimator.runtimeAnimatorController.name.ToLowerInvariant()
                    : string.Empty;

            if (objectName.Contains("wheel") || controllerName.Contains("wheel"))
                foundWheelAnimators.Add(childAnimator);
        }

        wheelAnimators = foundWheelAnimators.ToArray();
    }

    public void SetColor(BlockColorData color)
    {
        currentColor = color;

        if (gunSprite != null && color != null)
        {
            originalSprite = gunSprite.sprite;

            gunSprite.sprite = originalSprite;
            gunSprite.color = Color.white;
            gunSprite.enabled = true;
        }

        UpdateEffectVisual(
            EffectType.Fire,
            gunType == GunType.Fire && !mysteryHidden
        );
    }
    public void SetGunType(GunType type)
    {
        gunType = type;

        UpdateEffectVisual(
            EffectType.Fire,
            gunType == GunType.Fire && !mysteryHidden
        );
    }

    private void UpdateEffectVisual(EffectType type, bool active)
    {
        if (effect == null)
            return;

        SpriteRenderer renderer =
            effect.GetComponentInChildren<SpriteRenderer>();

        if (renderer == null)
            return;

        if (EffectManager.Instance != null)
            renderer.sprite =
                EffectManager.Instance.GetEffectSprite(type);

        effect.SetActive(active);
    }
    public void HideGun()
    {
        if (gunSprite == null || questionMarkSprite == null)
            return;

        if (originalSprite == null)
            return;

        isRevealed = false;
        mysteryHidden = true;

        gunSprite.sprite = questionMarkSprite;
        gunSprite.color = Color.white;
        gunSprite.enabled = true;

        if (effect != null)
            effect.SetActive(false);
        if (hpText != null)
            hpText.gameObject.SetActive(false);
        
    }

    public bool HasValidShootTarget()
    {
        if (currentHP <= 0)
            return false;

        if (currentColor == null)
            return false;

        if (bulletPrefab == null)
            return false;

        if (firePoint == null)
            return false;

        if (!lockedInTray)
            return false;

        ShootTarget result =
            FindBestGroupTarget();

        if (result.block == null)
            return false;

        if (result.path == null || result.path.Count < 2)
            return false;

        Block target = result.block;

        if (target == null)
            return false;

        if (target.IsDestroyed || !BoardViewport.IsBlockVisible(target))
            return false;

        if (target.group == null)
            return false;

        if (target.group.IsDead())
            return false;

        if (!IsPathClear(result.path, target.group))
            return false;

        if (!target.group.CanReserveDamage(
            1,
            currentColor
        ))
            return false;

        return true;
    }
    public void RevealGun()
    {
        if (gunSprite == null || originalSprite == null)
            return;

        isRevealed = true;
        mysteryHidden = false;

        gunSprite.sprite = originalSprite;

        gunSprite.color = Color.white;

        gunSprite.enabled = true;

        UpdateEffectVisual(EffectType.Fire, gunType == GunType.Fire && !mysteryHidden);

        if (hpText != null)
        {
            hpText.gameObject.SetActive(true);
            hpText.text = currentHP.ToString();
        }
    }
    private bool Shoot()
    {
        if (currentHP <= 0)
        {
            Debug.LogError(
                "[SHOOT DEBUG] " +
                name +
                " | HP = 0 trước khi Shoot()"
            );

            canShoot = false;
            Invoke(nameof(DestroyGun), 0.05f);
            return false;
        }

        if (currentColor == null || firePoint == null)
            return false;

        if (bulletPrefab == null)
            return false;


        ShootTarget result =
            FindBestGroupTarget();

        if (result.block == null)
            return false;

        if (result.path == null || result.path.Count < 2)
            return false;

        Block target = result.block;

        if (target == null)
            return false;

        if (target.IsDestroyed || !BoardViewport.IsBlockVisible(target))
            return false;

        if (target.group == null)
            return false;

        if (target.group.IsDead())
            return false;

        if (!IsPathClear(result.path, target.group))
            return false;

        if (target.group.groupType == BlockType.Dual)
            return DualGunVolley.TryFire(this, target.group);

        if (!target.group.CanReserveDamage(1, currentColor))
        {

            return false;
        }


        target.group.ReserveDamage(
            1,
            currentColor
        );

        FireReservedShot(result);
        return true;
    }

    internal Bullet FireReservedShot(ShootTarget result)
    {
        Bullet bullet = Bullet.Spawn(bulletPrefab, firePoint.position, Quaternion.identity);

        bullet.Initialize(
            result.block,
            currentColor,
            result.point,
            result.path
        );

        bullet.SetFireBullet(
            gunType == GunType.Fire
        );

        currentHP--;
        nextFireTime = Time.time + fireRate;

        UpdateHPText();


        PlayShootAnimation();

        if (currentHP <= 0)
        {
            canShoot = false;


            Invoke(nameof(DestroyGun), 0.15f);
        }

        return bullet;
    }
    public void CheckMysteryRow()
    {
        if (!mysteryHidden)
            return;

        if (!inGrid)
            return;

        GunGridManager manager =
            GunGridManager.Instance;

        if (manager == null)
            return;

        int topRow =
            manager.rows - 1;

        if (
            gridX >= 0 &&
            gridX < manager.columns &&
            gridY == topRow &&
            manager.Grid[gridX, topRow] == this
        )
        {
            RevealGun();
        }
    }

    private void HideHPText()
    {
        if (hpText != null)
            hpText.gameObject.SetActive(false);
    }

    private void ShowHPText()
    {
        if (hpText != null)
        {
            hpText.gameObject.SetActive(true);
            hpText.text = currentHP.ToString();
        }
    }

    public void SetHP(int hp)
    {
        maxHP = hp;
        currentHP = hp;

        UpdateHPText();
    }

    private void UpdateHPText()
    {
        if (hpText != null) hpText.SetText("{0}", currentHP);
    }

    public void SetCurrentTray(TraySlot tray)
    {
        currentTray = tray;
    }

    public void ReturnToStart()
    {
        if (lockedInTray)
            return;

        SetWheelRolling(false);
        canShoot = false;

        transform.DOKill(true);

        if (gunVisual != null)
            gunVisual.DOKill(true);

        transform.SetParent(null);

        transform.DOMove(homePosition, 0.3f)
            .SetEase(Ease.OutBack)
            .SetLink(
                gameObject,
                LinkBehaviour.KillOnDestroy
            );

        transform.DOScale(originalScale, 0.3f)
            .SetEase(Ease.OutBack)
            .SetLink(
                gameObject,
                LinkBehaviour.KillOnDestroy
            );

        if (gunVisual != null)
        {
            gunVisual.DOScale(
                originalVisualScale,
                0.3f
            )
            .SetEase(Ease.OutBack)
            .SetLink(
                gameObject,
                LinkBehaviour.KillOnDestroy
            );
        }
    }

    internal struct ShootTarget
    {
        public Block block;
        public Vector2 point;
        public List<Vector2> path;

        public ShootTarget(
            Block block,
            Vector2 point,
            List<Vector2> path
        )
        {
            this.block = block;
            this.point = point;
            this.path = path;
        }
    }

    internal bool CanParticipateInDualVolley(bool requireReady)
    {
        if (!isActiveAndEnabled || inGrid || !lockedInTray || currentTray == null || currentTray.CurrentGun != this ||
            isDragging || mysteryHidden ||
            currentHP <= 0 || currentColor == null || bulletPrefab == null || firePoint == null)
            return false;
        return !requireReady || (canShoot && Time.time >= nextFireTime);
    }

    internal bool TryPlanDualShot(BlockGroup targetGroup, bool ignoreReservations, out ShootTarget result)
    {
        result = new ShootTarget(null, Vector2.zero, null);
        if (targetGroup == null || targetGroup.groupType != BlockType.Dual || targetGroup.IsDead())
            return false;
        if (!ignoreReservations && !targetGroup.CanReserveDamage(1, currentColor))
            return false;
        if (!FindPathToGroup(targetGroup, out Block block, out Vector2 point, out List<Vector2> path) ||
            !IsPathClear(path, targetGroup))
            return false;
        result = new ShootTarget(block, point, path);
        return true;
    }



    private bool IsPathClear(List<Vector2> path, BlockGroup targetGroup)
    {
        if (path == null || path.Count < 2 || targetGroup == null) return false;
        BeginTargetQuery();
        try
        {
            for (int i = 1; i < path.Count; i++)
                if (!IsSegmentClear(path[i - 1], path[i], targetGroup)) return false;
            return true;
        }
        finally { EndTargetQuery(); }
    }

    private bool IsSegmentClear(Vector2 start, Vector2 end, BlockGroup targetGroup)
    {
        Vector2 delta = end - start;
        float distance = delta.magnitude;

        if (distance <= 0.0001f)
            return true;

        if (!CacheBulletCollider())
            return false;

        Vector2 direction = delta / distance;

        float angle = Mathf.Atan2(
            direction.y,
            direction.x
        ) * Mathf.Rad2Deg;

        var filter = new ContactFilter2D();

        filter.SetLayerMask(
            Physics2D.DefaultRaycastLayers
        );

        filter.useTriggers =
            Physics2D.queriesHitTriggers;

        int count;

        while (true)
        {
            count = Physics2D.BoxCast(
                start,
                bulletHalfSize * 2f,
                angle,
                direction,
                filter,
                raycastHits,
                distance
            );

            if (count < raycastHits.Length)
                break;

            System.Array.Resize(
                ref raycastHits,
                raycastHits.Length * 2
            );
        }

        for (int i = 0; i < count; i++)
        {
            Collider2D collider = raycastHits[i].collider;

            if (collider == null)
                continue;

            if (!colliderBlocks.TryGetValue(
                    collider,
                    out Block block
                ))
            {
                block =
                    collider.GetComponent<Block>() ??
                    collider.GetComponentInParent<Block>();

                if (block != null)
                    colliderBlocks[collider] = block;
            }

            if (
                block == null ||
                block.gameObject.scene != gameObject.scene ||
                block.IsDestroyed ||
                block.group == targetGroup ||
                !TryVisiblePoint(block, out _)
            )
                continue;

            return false;
        }

        return true;
    }
    private ShootTarget FindBestGroupTarget() => FindBestTarget(false);

    private ShootTarget FindBestTarget(bool ignoreReservations)
    {
        if (firePoint == null || currentColor == null) return new ShootTarget(null, Vector2.zero, null);
        using (TargetSearchProfiler.Auto())
        {
            Physics2D.SyncTransforms();
            BeginTargetQuery();
            try
            {
                Vector2 start = firePoint.position;
                candidateGroups.Clear(); candidateGroupSet.Clear();
                GridManager grid = GridManager.Instance;
                if (grid != null && grid.gameObject.scene == gameObject.scene)
                {
                    foreach (BlockGroup group in grid.Groups) AddCandidateGroup(group, start);
                    // Legacy/test boards can populate cells without registering their groups.
                    if (grid.Groups.Count == 0 && grid.grid != null)
                        foreach (Block block in grid.grid) if (block != null) AddCandidateGroup(block.group, start);
                }
                else
                {
                    // Live boards use Groups above; retain standalone legacy fixture support.
                    foreach (Block block in FindObjectsByType<Block>(FindObjectsSortMode.None))
                        if (block != null && block.gameObject.scene == gameObject.scene) AddCandidateGroup(block.group, start);
                }
                candidateGroups.Sort(CompareGroups);
                ShootTarget best = new ShootTarget(null, Vector2.zero, null);
                float bestScore = float.PositiveInfinity;
                foreach (GroupCandidate candidate in candidateGroups)
                {
                    if (candidate.lowerBound >= bestScore * bestScore) break;
                    BlockGroup group = candidate.group;
                    if (!ignoreReservations && !group.CanReserveDamage(1, currentColor)) continue;
                    if (group.groupType == BlockType.Dual && !(ignoreReservations
                        ? DualGunVolley.HasPotentialPair(this, group) : DualGunVolley.CanFire(this, group))) continue;
                    if (!FindPathToGroup(group, out Block block, out Vector2 point, out List<Vector2> path)) continue;
                    float score = GetPathScore(path);
                    if (score >= bestScore) continue;
                    bestScore = score; best = new ShootTarget(block, point, path);
                }
                return best;
            }
            finally { EndTargetQuery(); }
        }
    }

    private void AddCandidateGroup(BlockGroup group, Vector2 start)
    {
        if (group == null || !candidateGroupSet.Add(group) || group.IsDead()) return;
        float lowerBound = float.PositiveInfinity;
        foreach (Block block in group.blocks)
        {
            if (!BlockMatchesColorWithoutVisibility(block) || !TryVisiblePoint(block, out Vector2 point)) continue;
            lowerBound = Mathf.Min(lowerBound, (point - start).sqrMagnitude);
        }
        if (!float.IsPositiveInfinity(lowerBound))
            candidateGroups.Add(new GroupCandidate { group = group, lowerBound = lowerBound, order = candidateGroups.Count });
    }
    private float GetPathScore(
        List<Vector2> path
    )
    {
        if (
            path == null ||
            path.Count == 0
        )
            return float.MaxValue;

        float score = 0f;
        Vector2 previous = firePoint.position;

        foreach (Vector2 point in path)
        {
            score += Vector2.Distance(
                previous,
                point
            );

            previous = point;
        }

        return score;
    }

    private bool FindPathToGroup(BlockGroup targetGroup, out Block targetBlock, out Vector2 targetPoint, out List<Vector2> path)
    {
        targetBlock = null; targetPoint = Vector2.zero; path = null;
        if (targetGroup == null) return false;
        BeginTargetQuery();
        try
        {
            if (plannedGroups.TryGetValue(targetGroup, out ShootTarget saved))
            { targetBlock = saved.block; targetPoint = saved.point; path = saved.path; return targetBlock != null; }
            Vector2 start = firePoint != null ? (Vector2)firePoint.position : (Vector2)transform.position;
            candidateBlocks.Clear();
            foreach (Block block in targetGroup.blocks)
                if (BlockMatchesColorWithoutVisibility(block) && TryVisiblePoint(block, out Vector2 point))
                    candidateBlocks.Add(new BlockCandidate { block = block, point = point, lowerBound = (point - start).sqrMagnitude, order = candidateBlocks.Count });
            candidateBlocks.Sort(CompareBlocks);
            if (planningClear == null) planningClear = IsPlanningSegmentClear;
            BlockGroup previousGroup = planningGroup;
            planningGroup = targetGroup;
            float bestLength = float.PositiveInfinity;
            GunPathPlanner.BeginGroup();
            try
            {
                foreach (BlockCandidate candidate in candidateBlocks)
                {
                    if (candidate.lowerBound >= bestLength * bestLength) break;
                    GridManager grid = candidate.block.GetComponentInParent<GridManager>();
                    if (!GunPathPlanner.TryFind(start, candidate.point, grid, planningClear, out List<Vector2> route, targetGroup)) continue;
                    float length = GetPathScore(route);
                    if (length >= bestLength) continue;
                    bestLength = length; targetBlock = candidate.block; targetPoint = candidate.point; path = route;
                }
            }
            finally { GunPathPlanner.EndGroup(); planningGroup = previousGroup; }
            plannedGroups[targetGroup] = new ShootTarget(targetBlock, targetPoint, path);
            return targetBlock != null && path != null && path.Count >= 2;
        }
        finally { EndTargetQuery(); }
    }
    private void PlayShootAnimation()
    {
        if (gunVisual == null) return;
        if (recoilSequence == null || !recoilSequence.IsActive())
        {
            gunVisual.DOKill(true);
            gunVisual.localScale = originalVisualScale;
            recoilSequence = DOTween.Sequence().SetAutoKill(false).SetTarget(gunVisual);
            recoilSequence.Append(gunVisual.DOScale(originalVisualScale * .85f, .06f));
            recoilSequence.Append(gunVisual.DOScale(originalVisualScale, .12f).SetEase(Ease.OutBack));
            recoilSequence.SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        }
        gunVisual.localScale = originalVisualScale;
        recoilSequence.Restart();
    }
    private void DestroyGun()
    {
        if (this == null)
            return;

        canShoot = false;

        if (
            chain != null &&
            chain.IsPair
        )
        {
            Gun other =
                chain.GetOther(this);

            GunChain currentChain =
                chain;

            if (
                other != null &&
                other.currentHP > 0
            )
            {
                return;
            }

            if (other != null)
            {
                other.canShoot = false;
                other.lockedInTray = false;

                if (other.inGrid)
                {
                    if (removal != null)
                        removal.RemoveGun(other);
                }
                else if (other.CurrentTray != null)
                {
                    other.CurrentTray.RemoveGun(other);
                    other.SetCurrentTray(null);
                }
            }

            lockedInTray = false;

            if (inGrid)
            {
                if (removal != null)
                    removal.RemoveGun(this);
            }
            else if (CurrentTray != null)
            {
                CurrentTray.RemoveGun(this);
                SetCurrentTray(null);
            }

            if (currentChain != null)
                currentChain.PlayDestroyAnimation();

            if (other != null)
                other.chain = null;

            chain = null;

            if (other != null)
            {
                Destroy(
                    other.gameObject,
                    0.22f
                );
            }

            Destroy(
                gameObject,
                0.22f
            );

            return;
        }

        lockedInTray = false;

        if (inGrid)
        {
            if (removal != null)
                removal.RemoveGun(this);
        }
        else if (CurrentTray != null)
        {
            CurrentTray.RemoveGun(this);
            SetCurrentTray(null);
        }

        Transform visual =
            gunVisual != null
                ? gunVisual
                : transform;

        if (visual == null)
        {
            Destroy(gameObject);
            return;
        }

        visual.DOKill(true);

        visual.DOScale(
            Vector3.zero,
            0.2f
        )
        .SetEase(Ease.InBack)
        .SetLink(
            gameObject,
            LinkBehaviour.KillOnDestroy
        )
        .OnComplete(() =>
        {
            if (this != null)
                Destroy(gameObject);
        });
    }
    public void DestroyIfEmpty()
    {
        if (currentHP > 0)
            return;

        DestroyGun();
    }
    public int GetHP()
    {
        return currentHP;
    }

    private bool BlockMatchesColor(Block block) => BlockMatchesColorWithoutVisibility(block) && TryVisiblePoint(block, out _);

    private bool BlockMatchesColorWithoutVisibility(Block block)
    {
        if (block == null || block.IsDestroyed || block.gameObject.scene != gameObject.scene || block.group == null) return false;
        if (gunType == GunType.Fire ? block.group.groupType != BlockType.Frozen : block.group.groupType == BlockType.Frozen) return false;
        if (block.blockType == BlockType.Dual)
            return (block.dualTopColor == currentColor || block.dualBottomColor == currentColor) && block.group.GetDualColorHP(currentColor) > 0;
        return block.blockColor == currentColor;
    }

    public bool HasPotentialShootTarget()
    {
        if (currentHP <= 0)
            return false;

        if (currentColor == null)
            return false;

        if (bulletPrefab == null)
            return false;

        if (firePoint == null)
            return false;

        if (!lockedInTray)
            return false;

        ShootTarget result =
            FindBestGroupTargetIgnoreReserve();

        if (result.block == null)
            return false;

        if (result.path == null || result.path.Count < 2)
            return false;

        Block target = result.block;

        if (target == null)
            return false;

        if (target.IsDestroyed || !BoardViewport.IsBlockVisible(target))
            return false;

        if (target.group == null)
            return false;

        if (target.group.IsDead())
            return false;

        if (!IsPathClear(result.path, target.group))
            return false;

        return true;
    }
    private ShootTarget FindBestGroupTargetIgnoreReserve() => FindBestTarget(true);
}
