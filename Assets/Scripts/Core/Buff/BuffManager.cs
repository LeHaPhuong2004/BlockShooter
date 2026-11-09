using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;

public enum BuffType
{
    None,
    SplitGun,
    TakeBelowGun,
    SuffleGridGuns,
    RevealGun
}

public enum SpecialBuffType
{
    None,
    UnlimitedHeart
}

public class BuffManager : MonoBehaviour
{
    public static BuffManager Instance;

    [Header("Buff Item Data")]
    [SerializeField] private TextAsset playerItemsJson;

    [Header("Buff Amount Text")]
    [SerializeField] private TMP_Text splitGunText;
    [SerializeField] private TMP_Text takeBelowGunText;
    [SerializeField] private TMP_Text suffleGridGunsText;
    [SerializeField] private TMP_Text revealGunText;
    [SerializeField] private TMP_Text unlimitedHeartText;

    private bool waitingForSplitGun;
    private bool waitingForTakeGunBelow;

    private class ShuffleGroup
    {
        public Gun gunA;
        public Gun gunB;

        public int offsetX;
        public int offsetY;

        public bool IsPair => gunB != null;

        public ShuffleGroup(Gun gun)
        {
            gunA = gun;
            gunB = null;

            offsetX = 0;
            offsetY = 0;
        }

        public ShuffleGroup(
            Gun a,
            Gun b,
            int offsetX,
            int offsetY
        )
        {
            gunA = a;
            gunB = b;

            this.offsetX = offsetX;
            this.offsetY = offsetY;
        }
    }

    private void Awake()
    {
        Instance = this;
        BoosterInventory.Initialize(playerItemsJson);
    }

    private void OnEnable()
    {
        BoosterInventory.Changed += UpdateItemTexts;
        UpdateItemTexts();
    }

    private void OnDisable()
    {
        BoosterInventory.Changed -= UpdateItemTexts;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void UpdateItemTexts()
    {
        if (splitGunText != null)
            splitGunText.text =
                BoosterInventory.GetAmount(BuffType.SplitGun).ToString();

        if (takeBelowGunText != null)
            takeBelowGunText.text =
                BoosterInventory.GetAmount(BuffType.TakeBelowGun).ToString();

        if (suffleGridGunsText != null)
            suffleGridGunsText.text =
                BoosterInventory.GetAmount(BuffType.SuffleGridGuns).ToString();

        if (revealGunText != null)
            revealGunText.text =
                BoosterInventory.GetAmount(BuffType.RevealGun).ToString();

        if (unlimitedHeartText != null)
            unlimitedHeartText.text =
                BoosterInventory.GetUnlimitedHeartAmount().ToString();
    }

    public void UseBuff(BuffType buffType)
    {
        switch (buffType)
        {
            case BuffType.SplitGun:
                StartSplitGun();
                break;

            case BuffType.TakeBelowGun:
                StartTakeBelowGun();
                break;

            case BuffType.SuffleGridGuns:
                StartShuffleGridGuns();
                break;

            case BuffType.RevealGun:
                StartRevealGun();
                break;
        }
    }

    private void ConsumeBuff(
        BuffType buffType
    )
    {
        if (!BoosterInventory.TrySpend(buffType)) return;
        DailyMissionManager.RecordBoosterUsed();
        AchievementManager.RecordBoosterUsed();
    }

    public void UseSpecialBuff(
        SpecialBuffType buffType
    )
    {
        switch (buffType)
        {
            case SpecialBuffType.UnlimitedHeart:
                ActivateUnlimitedHeart();
                break;
        }
    }

    private void ConsumeSpecialBuff(
        SpecialBuffType buffType
    )
    {
        if (buffType == SpecialBuffType.UnlimitedHeart)
            BoosterInventory.TrySpendUnlimitedHeart();
    }

    public void StartSplitGun()
    {
        if (BoosterInventory.GetAmount(BuffType.SplitGun) <= 0)
        {
            Debug.Log("BUFF: Split Gun -> Không còn item");
            return;
        }

        waitingForSplitGun = true;
        waitingForTakeGunBelow = false;

        if (BoosterTutorialPanel.Instance != null)
            BoosterTutorialPanel.Instance.Open(BuffType.SplitGun);
    }


    public bool IsWaitingForSplitGun()
    {
        return waitingForSplitGun;
    }


    public void SelectGunForSplit(Gun gun)
    {
        if (!waitingForSplitGun || gun == null)
            return;

        if (gun.chain == null || !gun.chain.IsPair)
            return;

        Gun other = gun.chain.GetOther(gun);

        if (other == null)
            return;

        waitingForSplitGun = false;

        ConsumeBuff(BuffType.SplitGun);

        SplitGunPair(gun);

    }

    public void SelectGunForTakeBelowWithHand(Gun gun, RectTransform handIcon)
    {
        if (!waitingForTakeGunBelow || gun == null)
            return;

        if (!gun.inGrid || gun.lockedInTray)
            return;

        // Gọi trực tiếp logic lấy súng xuống khay khi người chơi thả tay trúng súng
        SelectGunForTakeBelow(gun);
    }
    private void SplitGunPair(
      Gun gun
  )
    {
        if (gun == null)
            return;

        if (gun.chain == null)
            return;

        Gun other =
            gun.chain.GetOther(gun);

        if (other == null)
            return;

        GunChain oldChain =
            gun.chain;

        oldChain.BreakChain();

        gun.chain = null;
        other.chain = null;

        Debug.Log(
            "BUFF: Split Gun -> Đã tách " +
            gun.name +
            " + " +
            other.name
        );

        if (gun.currentHP <= 0)
            gun.DestroyIfEmpty();

        if (other.currentHP <= 0)
            other.DestroyIfEmpty();
    }
    public void StartTakeBelowGun()
    {
        if (BoosterInventory.GetAmount(BuffType.TakeBelowGun) <= 0)
        {
            Debug.Log("BUFF: Take Below Gun -> Không còn item");
            return;
        }

        waitingForTakeGunBelow = true;
        waitingForSplitGun = false;

        if (BoosterTutorialPanel.Instance != null)
            BoosterTutorialPanel.Instance.Open(BuffType.TakeBelowGun);
    }

    public void CancelTargetedBooster()
    {
        waitingForSplitGun = false;
        waitingForTakeGunBelow = false;
    }

    public bool IsWaitingForTakeGunBelow()
    {
        return waitingForTakeGunBelow;
    }

    public void SelectGunForTakeBelow(
        Gun gun
    )
    {
        if (!waitingForTakeGunBelow)
            return;

        if (gun == null)
            return;

        if (!gun.inGrid)
            return;

        if (gun.lockedInTray)
            return;

        GunGridManager manager =
            GunGridManager.Instance;

        if (manager == null)
            return;

        if (
            gun.chain != null &&
            gun.chain.IsPair
        )
        {
            Gun other =
                gun.chain.GetOther(gun);

            if (
                other == null ||
                !other.inGrid
            )
                return;

            if (!TraySlot.CanPlacePairSmart())
            {
                Debug.Log(
                    "BUFF: Take Below Gun -> Không đủ chỗ cho súng đôi"
                );

                return;
            }

            if (gun.mysteryHidden)
                gun.RevealGun();

            if (other.mysteryHidden)
                other.RevealGun();

            waitingForTakeGunBelow = false;

            Debug.Log(
                "BUFF: Take Below Gun -> Lấy cả súng đôi: " +
                gun.name +
                " + " +
                other.name
            );

            manager.Removal.RemovePairFromGridForTray(
                gun,
                other
            );

            bool placed =
                TraySlot.PlacePairToBestSlots(
                    gun,
                    other
                );

            if (!placed)
            {
                Debug.LogWarning(
                    "BUFF: Take Below Gun -> PlacePair thất bại"
                );

                return;
            }

            ConsumeBuff(
                BuffType.TakeBelowGun
            );

            return;
        }

        TraySlot bestSlot =
            TraySlot.FindBestSlotForColor(
                gun.currentColor
            );

        if (bestSlot == null)
        {
            Debug.Log(
                "BUFF: Take Below Gun -> Tray đã đầy"
            );

            return;
        }

        waitingForTakeGunBelow = false;

        if (gun.mysteryHidden)
            gun.RevealGun();

        Debug.Log(
            "BUFF: Take Below Gun -> Lấy súng: " +
            gun.name
        );

        manager.Removal.RemoveSingleGunFromGridForTakeBelow(
            gun
        );

        bestSlot.PlaceGunSmart(
            gun
        );

        ConsumeBuff(
            BuffType.TakeBelowGun
        );
    }

    public void StartShuffleGridGuns()
    {
        if (BoosterInventory.GetAmount(BuffType.SuffleGridGuns) <= 0)
        {
            Debug.Log(
                "BUFF: Shuffle Grid Guns -> Không còn item"
            );

            return;
        }

        GunGridManager manager =
            GunGridManager.Instance;

        if (manager == null)
        {
            Debug.LogError(
                "[BUFF] GunGridManager.Instance = NULL"
            );

            return;
        }

        Gun[,] grid =
            manager.Grid;

        if (grid == null)
            return;

        List<ShuffleGroup> groups =
            new List<ShuffleGroup>();

        HashSet<Gun> checkedGuns =
            new HashSet<Gun>();

        List<Vector2Int> emptySlots =
            new List<Vector2Int>();

        for (
            int y = 0;
            y < manager.rows;
            y++
        )
        {
            for (
                int x = 0;
                x < manager.columns;
                x++
            )
            {
                if (grid[x, y] == null)
                {
                    emptySlots.Add(
                        new Vector2Int(
                            x,
                            y
                        )
                    );
                }
            }
        }

        for (
            int y = 0;
            y < manager.rows;
            y++
        )
        {
            for (
                int x = 0;
                x < manager.columns;
                x++
            )
            {
                Gun gun =
                    grid[x, y];

                if (gun == null)
                    continue;

                if (checkedGuns.Contains(gun))
                    continue;

                if (
                    gun.chain != null &&
                    gun.chain.IsPair
                )
                {
                    Gun other =
                        gun.chain.GetOther(gun);

                    if (
                        other != null &&
                        other.inGrid
                    )
                    {
                        int offsetX =
                            other.gridX -
                            gun.gridX;

                        int offsetY =
                            other.gridY -
                            gun.gridY;

                        if (
                            Mathf.Abs(offsetX) +
                            Mathf.Abs(offsetY) == 1
                        )
                        {
                            groups.Add(
                                new ShuffleGroup(
                                    gun,
                                    other,
                                    offsetX,
                                    offsetY
                                )
                            );

                            checkedGuns.Add(
                                gun
                            );

                            checkedGuns.Add(
                                other
                            );

                            continue;
                        }
                    }
                }

                groups.Add(
                    new ShuffleGroup(
                        gun
                    )
                );

                checkedGuns.Add(
                    gun
                );
            }
        }

        if (groups.Count <= 1)
        {
            Debug.Log(
                "[BUFF] Không đủ Gun để Shuffle."
            );

            return;
        }

        List<Vector2Int> availablePositions =
            new List<Vector2Int>();

        for (
            int y = 0;
            y < manager.rows;
            y++
        )
        {
            for (
                int x = 0;
                x < manager.columns;
                x++
            )
            {
                Vector2Int position =
                    new Vector2Int(
                        x,
                        y
                    );

                if (emptySlots.Contains(position))
                    continue;

                availablePositions.Add(
                    position
                );
            }
        }

        for (
            int i = groups.Count - 1;
            i > 0;
            i--
        )
        {
            int randomIndex =
                Random.Range(
                    0,
                    i + 1
                );

            ShuffleGroup temp =
                groups[i];

            groups[i] =
                groups[randomIndex];

            groups[randomIndex] =
                temp;
        }

        Gun[,] newGrid =
            new Gun[
                manager.columns,
                manager.rows
            ];

        HashSet<Vector2Int> usedPositions =
            new HashSet<Vector2Int>();

        bool success = true;

        foreach (
            ShuffleGroup group
            in groups
        )
        {
            List<Vector2Int> validPositions =
                new List<Vector2Int>();

            foreach (
                Vector2Int position
                in availablePositions
            )
            {
                if (
                    CanPlaceShuffleGroup(
                        group,
                        position,
                        manager,
                        newGrid,
                        usedPositions
                    )
                )
                {
                    validPositions.Add(
                        position
                    );
                }
            }

            if (validPositions.Count == 0)
            {
                success = false;
                break;
            }

            Vector2Int selectedPosition =
                validPositions[
                    Random.Range(
                        0,
                        validPositions.Count
                    )
                ];

            PlaceShuffleGroup(
                group,
                selectedPosition,
                manager,
                newGrid,
                usedPositions
            );
        }

        if (!success)
        {
            Debug.LogWarning(
                "[BUFF] Shuffle thất bại - giữ nguyên Grid."
            );

            return;
        }

        for (
            int y = 0;
            y < manager.rows;
            y++
        )
        {
            for (
                int x = 0;
                x < manager.columns;
                x++
            )
            {
                grid[x, y] = null;
            }
        }

        for (
            int y = 0;
            y < manager.rows;
            y++
        )
        {
            for (
                int x = 0;
                x < manager.columns;
                x++
            )
            {
                Gun gun =
                    newGrid[x, y];

                if (gun == null)
                    continue;

                grid[x, y] = gun;

                gun.gridX = x;
                gun.gridY = y;
                gun.inGrid = true;

                gun.transform.DOKill(true);

                gun.transform.DOMove(
                    manager.GetGridPosition(
                        x,
                        y
                    ),
                    0.35f
                )
                .SetEase(
                    Ease.OutBack
                )
                .SetLink(
                    gun.gameObject,
                    LinkBehaviour.KillOnDestroy
                );
            }
        }

        ConsumeBuff(
            BuffType.SuffleGridGuns
        );

        Debug.Log(
            "[BUFF] Shuffle Grid Guns thành công."
        );
    }

    private bool CanPlaceShuffleGroup(
        ShuffleGroup group,
        Vector2Int startPosition,
        GunGridManager manager,
        Gun[,] newGrid,
        HashSet<Vector2Int> usedPositions
    )
    {
        int x1 =
            startPosition.x;

        int y1 =
            startPosition.y;

        if (
            x1 < 0 ||
            x1 >= manager.columns ||
            y1 < 0 ||
            y1 >= manager.rows
        )
        {
            return false;
        }

        Vector2Int positionA =
            new Vector2Int(
                x1,
                y1
            );

        if (
            usedPositions.Contains(
                positionA
            )
        )
        {
            return false;
        }

        if (group.IsPair)
        {
            int x2 =
                x1 +
                group.offsetX;

            int y2 =
                y1 +
                group.offsetY;

            if (
                x2 < 0 ||
                x2 >= manager.columns ||
                y2 < 0 ||
                y2 >= manager.rows
            )
            {
                return false;
            }

            Vector2Int positionB =
                new Vector2Int(
                    x2,
                    y2
                );

            if (
                usedPositions.Contains(
                    positionB
                )
            )
            {
                return false;
            }

            Gun[,] currentGrid =
                manager.Grid;

            if (
                currentGrid[x1, y1] == null ||
                currentGrid[x2, y2] == null
            )
            {
                return false;
            }

            return true;
        }

        Gun[,] grid =
            manager.Grid;

        if (grid[x1, y1] == null)
            return false;

        return true;
    }

    private void PlaceShuffleGroup(
        ShuffleGroup group,
        Vector2Int startPosition,
        GunGridManager manager,
        Gun[,] newGrid,
        HashSet<Vector2Int> usedPositions
    )
    {
        int x1 =
            startPosition.x;

        int y1 =
            startPosition.y;

        newGrid[x1, y1] =
            group.gunA;

        usedPositions.Add(
            new Vector2Int(
                x1,
                y1
            )
        );

        if (!group.IsPair)
            return;

        int x2 =
            x1 +
            group.offsetX;

        int y2 =
            y1 +
            group.offsetY;

        newGrid[x2, y2] =
            group.gunB;

        usedPositions.Add(
            new Vector2Int(
                x2,
                y2
            )
        );
    }

    public void StartRevealGun()
    {
        if (BoosterInventory.GetAmount(BuffType.RevealGun) <= 0)
        {
            Debug.Log(
                "BUFF: Reveal Gun -> Không còn item"
            );

            return;
        }

        if (GunGridManager.Instance == null)
            return;

        Gun[,] grid =
            GunGridManager.Instance.Grid;

        if (grid == null)
            return;

        int revealedCount = 0;

        for (
            int y = 0;
            y < grid.GetLength(1);
            y++
        )
        {
            for (
                int x = 0;
                x < grid.GetLength(0);
                x++
            )
            {
                Gun gun =
                    grid[x, y];

                if (gun == null)
                    continue;

                if (!gun.mysteryHidden)
                    continue;

                gun.RevealGun();

                revealedCount++;
            }
        }

        if (revealedCount <= 0)
        {
            Debug.Log(
                "BUFF: Reveal Gun -> Không có mystery gun"
            );

            return;
        }

        ConsumeBuff(
            BuffType.RevealGun
        );

        Debug.Log(
            "BUFF: Reveal Gun -> Revealed " +
            revealedCount +
            " mystery guns"
        );
    }

    public void ActivateUnlimitedHeart()
    {
        if (BoosterInventory.GetUnlimitedHeartAmount() <= 0)
        {
            Debug.Log(
                "BUFF: Unlimited Heart -> Không còn item"
            );

            return;
        }

        ConsumeSpecialBuff(
            SpecialBuffType.UnlimitedHeart
        );

        HeartManager.ActivateUnlimited();

        Debug.Log(
            "Unlimited heart"
        );
    }
}
