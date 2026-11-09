using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

public class GunGridManager : MonoBehaviour
{
    public static GunGridManager Instance;

    [Header("Gun Prefabs")]
    public Gun[] gunPrefabs;

    [Header("Grid")]
    public int columns = 4;
    public int rows = 6;
    public float spacingX = 1.2f;
    public float spacingY = 1.2f;
    [Range(0.5f, 1f)] public float verticalSpacingMultiplier = 0.8f;
    public Transform gridOrigin;

    private const int MAX_COLUMNS = 4;
    private const int MAX_ROWS = 6;

    private GunGridSpawner spawner;
    private Gun[,] grid;
    private GunGridCollapse collapse;
    private GunRemovalManager removal;
    private GunColorSelector selector;
    private GunChainManager chain;

    private bool loseTriggered;
    private const float LossPollInterval = 0.1f;
    private const float BlockedTraySettleTime = 0.25f;
    private float nextLossCheckAt;
    private float blockedTraySince = -1f;
    private readonly HashSet<Gun> emptyGunsPendingRemoval = new HashSet<Gun>();

    public GunRemovalManager Removal => removal;
    public GunChainManager Chain => chain;
    public Gun[,] Grid => grid;

    public void ResetLoseState()
    {
        loseTriggered = false;
        blockedTraySince = -1f;
        nextLossCheckAt = 0f;
        emptyGunsPendingRemoval.Clear();
        Debug.Log("===== RESET LOSE STATE =====");
    }

    public void EnsureGridCapacity(int requiredGuns)
    {
        columns = MAX_COLUMNS;
        rows = MAX_ROWS;

        if (grid == null ||
            grid.GetLength(0) != columns ||
            grid.GetLength(1) != rows)
        {
            grid = new Gun[columns, rows];
        }
    }

    public int GetGridCapacity()
    {
        return columns * rows;
    }

    private void Awake()
    {
        Instance = this;
        loseTriggered = false;

        columns = MAX_COLUMNS;
        rows = MAX_ROWS;

        grid = new Gun[columns, rows];

        selector = new GunColorSelector(this);
        collapse = new GunGridCollapse(this);
        chain = new GunChainManager(this);
        spawner = new GunGridSpawner(this, selector);
        removal = new GunRemovalManager(this, collapse, spawner);
    }

    private void Start()
    {
        if (GridManager.Instance != null &&
            GridManager.Instance.gameMode == GameMode.Level)
        {
            return;
        }

        spawner.SpawnInitialGuns();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying || loseTriggered) return;
        if (Time.timeScale <= 0f)
        {
            blockedTraySince = -1f;
            return;
        }
        if (Time.time < nextLossCheckAt) return;
        EvaluateLoseCondition(Time.time);
    }

    private void OnDisable()
    {
        blockedTraySince = -1f;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public Gun[,] GetGrid()
    {
        return grid;
    }

    public int CountGuns()
    {
        int count = 0;

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                if (grid[x, y] != null)
                    count++;
            }
        }

        return count;
    }
    private void CheckGridPairCannotFit(TraySlot[] slots) => CheckLoseCondition();
    public List<Vector2Int> GetEmptySlots()
    {
        List<Vector2Int> slots = new List<Vector2Int>();

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                if (grid[x, y] == null)
                    slots.Add(new Vector2Int(x, y));
            }
        }

        return slots;
    }

    public Vector2Int FindEmptySlot()
    {
        List<Vector2Int> slots = GetEmptySlots();

        if (slots.Count == 0)
        {
            return new Vector2Int(-1, -1);
        }

        return slots[Random.Range(0, slots.Count)];
    }

    public Vector3 GetGridPosition(int x, int y)
    {
        float offsetX = (columns - 1) * spacingX / 2f;
        float offsetY = (rows - 1) * spacingY / 2f;

        float gridYOffset = -0.5f;

        // Keep the top row fixed and bring the lower rows closer to it.
        float topRowY = offsetY + gridYOffset;
        float rowSpacing = spacingY * verticalSpacingMultiplier;

        return gridOrigin.position +
            new Vector3(
                x * spacingX - offsetX,
                topRowY - (rows - 1 - y) * rowSpacing,
                0
            );
    }

    public void ReloadGuns()
    {
        ResetLoseState();

        // 1. Dọn dẹp súng trong Tray
        TraySlot[] slots = TraySlot.GetSortedSlots();

        if (slots != null)
        {
            foreach (TraySlot slot in slots)
            {
                if (slot == null)
                    continue;

                Gun gun = slot.CurrentGun;

                if (gun != null)
                {
                    ClearGunChain(gun);

                    gun.transform.DOKill(false);

                    if (gun.gunVisual != null)
                        gun.gunVisual.DOKill(false);

                    LineRenderer[] lines =
                        gun.GetComponentsInChildren<LineRenderer>(true);

                    foreach (LineRenderer line in lines)
                    {
                        if (line != null)
                            Destroy(line.gameObject);
                    }

                    Destroy(gun.gameObject);
                }

                slot.RemoveGun(gun);
            }
        }

        // 2. Dọn dẹp súng trên Grid
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                Gun gun = grid[x, y];

                if (gun != null)
                {
                    ClearGunChain(gun);

                    gun.transform.DOKill(false);

                    if (gun.gunVisual != null)
                        gun.gunVisual.DOKill(false);

                    LineRenderer[] lines =
                        gun.GetComponentsInChildren<LineRenderer>(true);

                    foreach (LineRenderer line in lines)
                    {
                        if (line != null)
                            Destroy(line.gameObject);
                    }

                    Destroy(gun.gameObject);
                }

                grid[x, y] = null;
            }
        }

        // Giữ grid cố định 4 x 6
        columns = MAX_COLUMNS;
        rows = MAX_ROWS;

        grid = new Gun[columns, rows];

        if (spawner != null)
            spawner.SpawnInitialGuns();

        Debug.Log(
            $"===== RESET GUN COMPLETE ===== GUNS = {CountGuns()}"
        );
    }

    private void ClearGunChain(Gun gun)
    {
        if (gun.chain != null)
        {
            gun.chain.BreakChain();
            gun.chain = null;
        }
    }

    // Calls from projectile/placement callbacks request an early poll; they do not
    // restart a coroutine or postpone a pending decision indefinitely.
    public void CheckLoseCondition()
    {
        if (!loseTriggered) nextLossCheckAt = 0f;
    }

    private void EvaluateLoseCondition(float now)
    {
        nextLossCheckAt = now + LossPollInterval;
        if (loseTriggered) return;
        if (Time.timeScale <= 0f || !IsBlockedTray())
        {
            blockedTraySince = -1f;
            return;
        }
        if (blockedTraySince < 0f)
        {
            blockedTraySince = now;
            return;
        }
        if (now - blockedTraySince < BlockedTraySettleTime) return;

        loseTriggered = true;
        blockedTraySince = -1f;
        if (NavigationManager.Instance != null) NavigationManager.Instance.ShowLosePanel();
    }

    // The loss panel has a delay. Revalidate before presenting it in case a
    // booster, arriving gun, or newly exposed block has made another move possible.
    public bool CanPresentLosePanel()
    {
        if (!loseTriggered) return false;
        if (Time.timeScale > 0f && IsBlockedTray()) return true;
        loseTriggered = false;
        blockedTraySince = -1f;
        nextLossCheckAt = 0f;
        return false;
    }

    private bool IsBlockedTray()
    {
        GridManager board = GridManager.Instance;
        if (board == null || board.gameObject.scene != gameObject.scene || board.IsExiting || board.grid == null)
            return false;
        bool hasLiveGroup = false;
        foreach (BlockGroup group in board.Groups)
            if (group != null && !group.IsDead() && group.AliveCount() > 0)
            { hasLiveGroup = true; break; }
        if (!hasLiveGroup) return false;
        if (HasWorkInFlight(board)) return false;

        var localSlots = new List<TraySlot>();
        foreach (TraySlot slot in FindObjectsByType<TraySlot>(FindObjectsInactive.Exclude))
            if (slot != null && slot.gameObject.scene == gameObject.scene && slot.gameObject.activeInHierarchy)
                localSlots.Add(slot);
        if (localSlots.Count == 0) return false;

        int emptySlots = 0;
        int occupiedSlots = 0;
        foreach (TraySlot slot in localSlots)
        {
            if (slot.slotPoint == null) return false;
            Gun gun = slot.CurrentGun;
            if (gun == null) { emptySlots++; continue; }
            occupiedSlots++;
            if (!gun.isActiveAndEnabled || gun.gameObject.scene != gameObject.scene ||
                gun.CurrentTray != slot || !gun.lockedInTray || gun.inGrid ||
                Vector3.SqrMagnitude(gun.transform.position - slot.slotPoint.position) > 0.0004f)
                return false;

            if (gun.currentHP <= 0)
            {
                Gun other = gun.chain != null && gun.chain.IsPair ? gun.chain.GetOther(gun) : null;
                // One spent half remains in a linked pair until its partner is empty.
                if (other != null && other.currentHP > 0) continue;
                if (Application.isPlaying && emptyGunsPendingRemoval.Add(gun)) gun.DestroyIfEmpty();
                return false;
            }
            if (!gun.canShoot)
            {
                if (DOTween.IsTweening(gun.transform)) return false;
                // A seated gun with no movement left should not remain disabled if
                // its old arrival callback was canceled while the tray changed.
                gun.canShoot = true;
            }
        }
        if (occupiedSlots == 0) return false;

        // Reservations belong to live projectiles or a pending dual volley. With
        // neither left and the board settled, any remaining reservation is orphaned.
        foreach (BlockGroup group in board.Groups)
            if (group != null && group.pendingDamage > 0) group.ReleaseDamage(group.pendingDamage);

        if (emptySlots != 0 && !OnlyUnplaceablePairsRemain(emptySlots)) return false;

        foreach (TraySlot slot in localSlots)
        {
            Gun gun = slot.CurrentGun;
            if (gun != null && gun.currentHP > 0 && gun.HasPotentialShootTarget()) return false;
        }
        return true;
    }

    private bool HasWorkInFlight(GridManager board)
    {
        if (board.isProcessingCollapse || board.isProcessingExplosion || DOTween.IsTweening(board)) return true;
        BuffManager buff = BuffManager.Instance;
        if (buff != null && buff.gameObject.scene == gameObject.scene &&
            (buff.IsWaitingForSplitGun() || buff.IsWaitingForTakeGunBelow())) return true;
        if (Bullet.HasActive(gameObject.scene)) return true;
        return DualBulletPair.HasPendingShots();
    }

    private bool OnlyUnplaceablePairsRemain(int emptySlots)
    {
        if (emptySlots != 1 || grid == null) return false;
        bool hasPair = false;
        foreach (Gun gun in grid)
        {
            if (gun == null || !gun.inGrid || !gun.gameObject.activeInHierarchy) continue;
            if (gun.chain == null || !gun.chain.IsPair) return false;
            hasPair = true;
        }
        return hasPair;
    }

    public void CheckPairCannotPlaceLose(Gun gunA, Gun gunB)
    {
        if (gunA == null || gunB == null || !gunA.inGrid || !gunB.inGrid) return;
        CheckLoseCondition();
    }
}
