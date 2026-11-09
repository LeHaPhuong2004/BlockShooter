
using System.Collections.Generic;
using UnityEngine;

public class GunGridSpawner
{
    public const int SpecialGunUnlockLevel = 25;
    public static bool SpecialGunsUnlocked => LevelManager.Instance != null &&
        LevelManager.Instance.CurrentLevel >= SpecialGunUnlockLevel;

    private GunGridManager manager;
    private GunColorSelector selector;

    public bool infiniteMode = false;
    private float mysteryGunChance = 0.4f;

    private class GroupGunData
    {
        public BlockGroup group;
        public BlockColorData color;
        public int totalHP;
        public bool isFire;

        public GroupGunData(BlockGroup group, BlockColorData color, int totalHP, bool isFire = false)
        {
            this.group = group;
            this.color = color;
            this.totalHP = totalHP;
            this.isFire = isFire;
        }
    }

    private List<GroupGunData> groupGunData = new List<GroupGunData>();

    private bool CanSpawnMystery()
    {
        return SpecialGunsUnlocked;
    }

    private void CalculateTotalHPByGroup()
    {
        groupGunData.Clear();

        if (GridManager.Instance == null)
            return;

        Block[,] grid = GridManager.Instance.grid;

        if (grid == null)
            return;

        HashSet<BlockGroup> countedGroups = new HashSet<BlockGroup>();

        for (int y = 0; y < GridManager.Instance.height; y++)
        {
            for (int x = 0; x < GridManager.Instance.width; x++)
            {
                Block block = grid[x, y];

                if (block == null || block.IsDestroyed)
                    continue;

                BlockGroup group = block.group;

                if (group == null)
                {
                    if (block.blockType == BlockType.Dual)
                    {
                        if (block.dualTopColor != null && block.dualTopHP > 0)
                            groupGunData.Add(new GroupGunData(null, block.dualTopColor, block.dualTopHP));

                        if (block.dualBottomColor != null && block.dualBottomHP > 0)
                            groupGunData.Add(new GroupGunData(null, block.dualBottomColor, block.dualBottomHP));

                        continue;
                    }

                    if (block.blockColor != null && block.hp > 0)
                        groupGunData.Add(new GroupGunData(null, block.blockColor, block.hp));

                    continue;
                }

                if (countedGroups.Contains(group))
                    continue;

                countedGroups.Add(group);
                AddGroupData(group);
            }
        }
    }

    private void AddGroupData(BlockGroup group)
    {
        if (group == null)
            return;

        if (group.groupType == BlockType.Dual)
        {
            // Each dual block mirrors its group's budgets for display. Count the
            // group's two color budgets once, regardless of how many blocks it has.
            if (group.TryGetDualColors(out BlockColorData first, out BlockColorData second))
            {
                int firstHP = group.GetDualColorHP(first);
                int secondHP = group.GetDualColorHP(second);
                if (firstHP > 0) groupGunData.Add(new GroupGunData(group, first, firstHP));
                if (secondHP > 0) groupGunData.Add(new GroupGunData(group, second, secondHP));
            }
            return;
        }

        bool isFire =
            group.groupType == BlockType.Frozen;

        if (group.groupType == BlockType.Reinforced)
        {
            BlockColorData color = null;

            foreach (Block block in group.blocks)
            {
                if (block == null || block.IsDestroyed)
                    continue;

                if (block.blockType != BlockType.Dual &&
                    block.blockColor != null)
                {
                    color = block.blockColor;
                    break;
                }
            }

            if (color == null)
                return;

            int totalHP = group.totalHP;

            int reinforceHP =
                group.GetReinforceRemaining();

            int requiredHP =
                totalHP + reinforceHP;

            if (requiredHP > 0)
            {
                groupGunData.Add(
                    new GroupGunData(
                        group,
                        color,
                        requiredHP,
                        false
                    )
                );

                Debug.Log(
                    "[REINFORCED AMMO] COLOR=" +
                    color.name +
                    " | NORMAL HP=" +
                    totalHP +
                    " | REINFORCE HP=" +
                    reinforceHP +
                    " | TOTAL AMMO=" +
                    requiredHP
                );
            }

            return;
        }

        Dictionary<BlockColorData, int> colorHP =
            new Dictionary<BlockColorData, int>();

        foreach (Block block in group.blocks)
        {
            if (block == null || block.IsDestroyed)
                continue;

            if (block.blockType == BlockType.Dual)
            {
                if (block.dualTopColor != null &&
                    block.dualTopHP > 0)
                {
                    AddColorHP(
                        colorHP,
                        block.dualTopColor,
                        block.dualTopHP
                    );
                }

                if (block.dualBottomColor != null &&
                    block.dualBottomHP > 0)
                {
                    AddColorHP(
                        colorHP,
                        block.dualBottomColor,
                        block.dualBottomHP
                    );
                }

                continue;
            }

            if (block.blockColor != null &&
                block.hp > 0)
            {
                AddColorHP(
                    colorHP,
                    block.blockColor,
                    block.hp
                );
            }
        }

        foreach (KeyValuePair<BlockColorData, int> pair in colorHP)
        {
            if (pair.Value <= 0)
                continue;

            groupGunData.Add(
                new GroupGunData(
                    group,
                    pair.Key,
                    pair.Value,
                    isFire
                )
            );
        }
    }

    private void SetMaxColorHP(Dictionary<BlockColorData, int> colorHP, BlockColorData color, int hp)
    {
        if (color == null || hp <= 0)
            return;

        if (!colorHP.ContainsKey(color))
        {
            colorHP[color] = hp;
            return;
        }

        colorHP[color] = Mathf.Max(colorHP[color], hp);
    }

    private void AddColorHP(Dictionary<BlockColorData, int> colorHP, BlockColorData color, int hp)
    {
        if (color == null || hp <= 0)
            return;

        if (!colorHP.ContainsKey(color))
            colorHP[color] = 0;

        colorHP[color] += hp;
    }
    
    private List<int> SplitHPIntoGuns(int totalHP, int gunCount)
    {
        List<int> result = new List<int>();

        if (totalHP <= 0 || gunCount <= 0)
            return result;

        gunCount = Mathf.Min(gunCount, totalHP);

        int baseHP = totalHP / gunCount;
        int remainder = totalHP % gunCount;

        for (int i = 0; i < gunCount; i++)
            result.Add(baseHP + (i < remainder ? 1 : 0));

        return result;
    }

    public GunGridSpawner(GunGridManager manager, GunColorSelector selector)
    {
        this.manager = manager;
        this.selector = selector;
    }

    public void SpawnInitialGuns()
    {
        if (manager == null)
            return;

        CalculateTotalHPByGroup();

        if (groupGunData.Count == 0)
        {
            Debug.LogError("[GUN SPAWN] groupGunData EMPTY!");
            return;
        }
        manager.EnsureGridCapacity(groupGunData.Count);
        Dictionary<BlockColorData, int> normalColorHP =
            new Dictionary<BlockColorData, int>();

        Dictionary<BlockColorData, int> fireColorHP =
            new Dictionary<BlockColorData, int>();

        foreach (GroupGunData data in groupGunData)
        {
            if (data == null || data.color == null || data.totalHP <= 0)
                continue;

            Dictionary<BlockColorData, int> target =
                data.isFire ? fireColorHP : normalColorHP;

            if (!target.ContainsKey(data.color))
                target[data.color] = 0;

            target[data.color] += data.totalHP;
        }

        List<Vector2Int> availableSlots =
            manager.GetEmptySlots();

        Debug.Log(
            "[GUN SPAWN] Empty Slots = " +
            availableSlots.Count
        );

        int normalTotalHP =
            GetDictionaryTotal(normalColorHP);

        int fireTotalHP =
            GetDictionaryTotal(fireColorHP);

        int totalHP =
            normalTotalHP + fireTotalHP;

        Debug.Log(
            "[GUN SPAWN] NormalHP=" +
            normalTotalHP +
            " | FireHP=" +
            fireTotalHP +
            " | TotalHP=" +
            totalHP
        );

        if (availableSlots.Count == 0)
        {
            Debug.LogError(
                "[GUN SPAWN] manager.GetEmptySlots() RETURNED 0!"
            );
            return;
        }

        int normalGunCount = 0;
        int fireGunCount = 0;

        int requiredTypes = normalColorHP.Count + fireColorHP.Count;
        if (requiredTypes > availableSlots.Count)
        {
            Debug.LogError("[GUN SPAWN] Not enough slots for all required color/type combinations: " +
                requiredTypes + " required, " + availableSlots.Count + " available.");
            return;
        }

        if (normalTotalHP > 0 && fireTotalHP > 0)
        {
            int maxGunCount =
                Mathf.Min(
                    totalHP,
                    availableSlots.Count
                );

            // Preserve at least one gun for every required color in both sets;
            // otherwise proportional allocation can silently omit a rare color.
            normalGunCount = Mathf.Clamp(
                Mathf.RoundToInt(
                    maxGunCount *
                    ((float)normalTotalHP / totalHP)
                ),
                normalColorHP.Count,
                maxGunCount - fireColorHP.Count
            );

            fireGunCount =
                maxGunCount - normalGunCount;
        }
        else if (normalTotalHP > 0)
        {
            normalGunCount =
                Mathf.Min(
                    normalTotalHP,
                    availableSlots.Count
                );
        }
        else if (fireTotalHP > 0)
        {
            fireGunCount =
                Mathf.Min(
                    fireTotalHP,
                    availableSlots.Count
                );
        }

        Debug.Log(
            "[GUN COUNT] Normal=" +
            normalGunCount +
            " | Fire=" +
            fireGunCount +
            " | Slots=" +
            availableSlots.Count
        );

        SpawnGunSet(
     normalColorHP,
     normalGunCount,
     false,
     availableSlots
 );

        SpawnGunSet(
            fireColorHP,
            fireGunCount,
            true,
            availableSlots
        );

        CreateInitialPairs();
        ApplyMysteryGuns();

        DebugSpawnedGuns(
            normalColorHP,
            fireColorHP
        );
    }

    private void ApplyMysteryGuns()
    {
        if (!SpecialGunsUnlocked) return;

        List<Gun> candidates = new List<Gun>();

        for (int y = 0; y < manager.rows - 1; y++)
        {
            for (int x = 0; x < manager.columns; x++)
            {
                Gun gun = manager.Grid[x, y];

                if (gun == null)
                    continue;

                if (gun.chain != null && gun.chain.IsPair)
                    continue;

                candidates.Add(gun);
            }
        }

        int mysteryCount =
            Mathf.RoundToInt(
                candidates.Count *
                mysteryGunChance
            );

        for (int i = 0; i < candidates.Count; i++)
        {
            int randomIndex =
                Random.Range(
                    i,
                    candidates.Count
                );

            Gun temp = candidates[i];
            candidates[i] = candidates[randomIndex];
            candidates[randomIndex] = temp;
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            if (i < mysteryCount)
                candidates[i].HideGun();
            else
                candidates[i].RevealGun();
        }
    }

    private Vector2Int GetBestRandomSlotForColor(
        BlockColorData color,
        List<Vector2Int> availableSlots
    )
    {
        if (color == null || availableSlots == null || availableSlots.Count == 0)
            return new Vector2Int(-1, -1);

        List<Vector2Int> preferredSlots =
            new List<Vector2Int>();

        int bestRowCount = int.MaxValue;

        for (int i = 0; i < availableSlots.Count; i++)
        {
            Vector2Int slot = availableSlots[i];

            int sameColorCount =
                CountColorInRow(
                    color,
                    slot.y
                );

            if (sameColorCount < bestRowCount)
            {
                bestRowCount = sameColorCount;
                preferredSlots.Clear();
                preferredSlots.Add(slot);
            }
            else if (sameColorCount == bestRowCount)
            {
                preferredSlots.Add(slot);
            }
        }

        if (preferredSlots.Count == 0)
            return availableSlots[Random.Range(0, availableSlots.Count)];

        return preferredSlots[
            Random.Range(
                0,
                preferredSlots.Count
            )
        ];
    }

    private int CountColorInRow(BlockColorData color, int row)
    {
        if (color == null)
            return 0;

        if (row < 0 || row >= manager.rows)
            return 0;

        int count = 0;

        for (int x = 0; x < manager.columns; x++)
        {
            Gun gun = manager.Grid[x, row];

            if (gun == null)
                continue;

            if (gun.currentColor == color)
                count++;
        }

        return count;
    }

    private void ShuffleColors(List<BlockColorData> colors)
    {
        for (int i = 0; i < colors.Count; i++)
        {
            int randomIndex =
                Random.Range(
                    i,
                    colors.Count
                );

            BlockColorData temp = colors[i];
            colors[i] = colors[randomIndex];
            colors[randomIndex] = temp;
        }
    }

   
    private void SpawnGunSet(
    Dictionary<BlockColorData, int> colorHP,
    int gunCount,
    bool isFire,
    List<Vector2Int> availableSlots
)
    {
        if (
            colorHP == null ||
            colorHP.Count == 0 ||
            gunCount <= 0 ||
            availableSlots == null ||
            availableSlots.Count == 0
        )
        {
            return;
        }

        int totalHP =
            GetDictionaryTotal(colorHP);

        if (totalHP <= 0)
            return;

        gunCount =
            Mathf.Min(
                gunCount,
                availableSlots.Count
            );

        if (gunCount <= 0)
            return;

        List<BlockColorData> colors =
            new List<BlockColorData>(
                colorHP.Keys
            );

        ShuffleColors(colors);

        Dictionary<BlockColorData, int> gunCountByColor =
            new Dictionary<BlockColorData, int>();

        foreach (BlockColorData color in colors)
            gunCountByColor[color] = 0;

        if (gunCount >= colors.Count)
        {
            foreach (BlockColorData color in colors)
                gunCountByColor[color] = 1;

            int remainingGuns =
                gunCount -
                colors.Count;

            while (remainingGuns > 0)
            {
                BlockColorData bestColor = null;
                float bestRatio = float.MinValue;

                foreach (BlockColorData color in colors)
                {
                    float ideal =
                        (float)colorHP[color] /
                        totalHP *
                        gunCount;

                    float ratio =
                        ideal -
                        gunCountByColor[color];

                    if (ratio > bestRatio)
                    {
                        bestRatio = ratio;
                        bestColor = color;
                    }
                }

                if (bestColor == null)
                    break;

                gunCountByColor[bestColor]++;
                remainingGuns--;
            }
        }
        else
        {
            List<BlockColorData> sortedColors =
                new List<BlockColorData>(colors);

            sortedColors.Sort(
                (a, b) =>
                    colorHP[b].CompareTo(
                        colorHP[a]
                    )
            );

            for (
                int i = 0;
                i < gunCount &&
                i < sortedColors.Count;
                i++
            )
            {
                gunCountByColor[
                    sortedColors[i]
                ] = 1;
            }
        }

        foreach (BlockColorData color in colors)
        {
            int count =
                gunCountByColor[color];

            if (count <= 0)
                continue;

            List<int> gunHPs =
                SplitHPIntoGuns(
                    colorHP[color],
                    count
                );
            Debug.Log(
    "[AMMO SPLIT] " +
    (isFire ? "FIRE" : "NORMAL") +
    " | COLOR=" +
    color.name +
    " | TOTAL=" +
    colorHP[color] +
    " | GUN COUNT=" +
    count +
    " | GUN HP=[" +
    string.Join(",", gunHPs) +
    "]"
);
            foreach (int gunHP in gunHPs)
            {
                if (availableSlots.Count == 0)
                    return;

                Vector2Int slot =
                    GetBestRandomSlotForColor(
                        color,
                        availableSlots
                    );

                if (slot.x < 0)
                    return;

                SpawnGun(
                    slot.x,
                    slot.y,
                    color,
                    gunHP,
                    isFire
                );

                Gun spawnedGun =
                    manager.Grid[
                        slot.x,
                        slot.y
                    ];

                if (spawnedGun != null)
                    availableSlots.Remove(slot);
            }
        }
    }


    private void DebugSpawnedGuns(
        Dictionary<BlockColorData, int> normalExpected,
        Dictionary<BlockColorData, int> fireExpected
    )
    {
        Dictionary<BlockColorData, int> normalActual =
            new Dictionary<BlockColorData, int>();

        Dictionary<BlockColorData, int> fireActual =
            new Dictionary<BlockColorData, int>();

        if (manager != null && manager.Grid != null)
        {
            for (int y = 0; y < manager.rows; y++)
            {
                for (int x = 0; x < manager.columns; x++)
                {
                    Gun gun = manager.Grid[x, y];

                    if (gun == null || gun.currentColor == null)
                        continue;

                    Dictionary<BlockColorData, int> target =
                        gun.gunType == GunType.Fire
                            ? fireActual
                            : normalActual;

                    if (!target.ContainsKey(gun.currentColor))
                        target[gun.currentColor] = 0;

                    target[gun.currentColor] += gun.currentHP;
                }
            }
        }

        Debug.Log("========== ACTUAL GUNS ==========");

        HashSet<BlockColorData> colors =
            new HashSet<BlockColorData>();

        foreach (var pair in normalExpected)
            colors.Add(pair.Key);

        foreach (var pair in fireExpected)
            colors.Add(pair.Key);

        foreach (BlockColorData color in colors)
        {
            int normalExpectedHP =
                normalExpected.ContainsKey(color)
                    ? normalExpected[color]
                    : 0;

            int fireExpectedHP =
                fireExpected.ContainsKey(color)
                    ? fireExpected[color]
                    : 0;

            int normalActualHP =
                normalActual.ContainsKey(color)
                    ? normalActual[color]
                    : 0;

            int fireActualHP =
                fireActual.ContainsKey(color)
                    ? fireActual[color]
                    : 0;

            Debug.Log(
                $"COLOR={color.name} | " +
                $"NORMAL Expected={normalExpectedHP} Actual={normalActualHP} Diff={normalActualHP - normalExpectedHP} | " +
                $"FIRE Expected={fireExpectedHP} Actual={fireActualHP} Diff={fireActualHP - fireExpectedHP}"
            );
        }

        Debug.Log("=================================");
    }

    private int GetDictionaryTotal(
        Dictionary<BlockColorData, int> data
    )
    {
        int total = 0;

        foreach (KeyValuePair<BlockColorData, int> pair in data)
            total += pair.Value;

        return total;
    }

    

    public void SpawnReplacementSingle()
    {
        if (!infiniteMode)
            return;

        SpawnReplacementSingleAtBottom(
            selector.GetRandomColor(),
            GetRandomGunHP()
        );
    }

    public void SpawnReplacementSingle(
        int x,
        int y,
        BlockColorData color,
        int hp
    )
    {
        if (y != 0)
            return;

        SpawnGun(x, y, color, hp);

        Gun gun = manager.Grid[x, y];

        if (gun == null)
            return;

        if (gun.chain != null && gun.chain.IsPair)
        {
            gun.RevealGun();
            return;
        }

        if (CanSpawnMystery() && Random.value < mysteryGunChance)
            gun.HideGun();
        else
            gun.RevealGun();
    }

    public void SpawnReplacementSingle(
        BlockColorData color,
        int hp
    )
    {
        SpawnReplacementSingleAtBottom(
            color,
            hp
        );
    }

    public int GetRandomGunHP()
    {
        return Random.Range(5, 16);
    }

    public void SpawnGun(
        int x,
        int y,
        BlockColorData color,
        int hp,
        bool isFire = false
    )
    {
        if (
            manager.gunPrefabs == null ||
            manager.gunPrefabs.Length == 0
        )
        {
            return;
        }

        if (color == null || hp <= 0)
            return;

        if (
            x < 0 ||
            x >= manager.columns ||
            y < 0 ||
            y >= manager.rows
        )
        {
            return;
        }

        if (manager.Grid[x, y] != null)
            return;

        Gun prefab =
            GetPrefabForColor(
                color,
                isFire
            );

        if (prefab == null)
        {
            Debug.LogError(
                "[GUN SPAWN] Không tìm thấy prefab " +
                (isFire ? "FIRE" : "NORMAL") +
                " cho màu: " +
                color.name
            );

            return;
        }

        Gun gun =
            Object.Instantiate(
                prefab,
                manager.GetGridPosition(x, y),
                Quaternion.identity,
                manager.transform
            );

        gun.currentColor = color;
        gun.SetColor(color);

        gun.SetGunType(
            isFire
                ? GunType.Fire
                : GunType.Normal
        );

        gun.SetHP(hp);

        gun.gridX = x;
        gun.gridY = y;
        gun.inGrid = true;
        gun.lockedInTray = false;

        gun.SetCurrentTray(null);
        gun.RevealGun();

        manager.Grid[x, y] = gun;

        Debug.Log(
            "[GUN SPAWN] " +
            (isFire ? "FIRE" : "NORMAL") +
            " | COLOR=" +
            color.name +
            " | HP=" +
            hp +
            " | POS=(" +
            x +
            "," +
            y +
            ")"
        );
    }

    public void SpawnReplacementPair()
    {
        if (!infiniteMode)
            return;

        List<Vector2Int> slots =
            manager.GetEmptySlots();

        if (slots.Count < 2)
            return;

        Vector2Int slotA =
            new Vector2Int(-1, -1);

        Vector2Int slotB =
            new Vector2Int(-1, -1);

        for (int i = 0; i < slots.Count; i++)
        {
            for (int j = i + 1; j < slots.Count; j++)
            {
                int dx =
                    Mathf.Abs(
                        slots[i].x -
                        slots[j].x
                    );

                int dy =
                    Mathf.Abs(
                        slots[i].y -
                        slots[j].y
                    );

                if (dx <= 2 && dy <= 2)
                {
                    slotA = slots[i];
                    slotB = slots[j];
                    break;
                }
            }

            if (slotA.x >= 0)
                break;
        }

        if (slotA.x < 0)
            return;

        BlockColorData colorA =
            selector.GetRandomColor();

        BlockColorData colorB =
            selector.GetRandomColor();

        int safety = 0;

        while (
            colorB == colorA &&
            safety < 20
        )
        {
            colorB = selector.GetRandomColor();
            safety++;
        }

        if (colorA == null || colorB == null)
            return;

        if (colorA == colorB)
            return;

        SpawnGun(
            slotA.x,
            slotA.y,
            colorA,
            GetRandomGunHP()
        );

        SpawnGun(
            slotB.x,
            slotB.y,
            colorB,
            GetRandomGunHP()
        );

        Gun gunA =
            manager.Grid[
                slotA.x,
                slotA.y
            ];

        Gun gunB =
            manager.Grid[
                slotB.x,
                slotB.y
            ];

        if (gunA != null && gunB != null)
        {
            manager.Chain.CreateChain(
                gunA,
                gunB
            );

            gunA.RevealGun();
            gunB.RevealGun();
        }
    }

    public void SpawnReplacementSingleAtBottom(
        BlockColorData color,
        int hp
    )
    {
        if (color == null)
            return;

        List<Vector2Int> bottomSlots =
            new List<Vector2Int>();

        for (int x = 0; x < manager.columns; x++)
        {
            if (manager.Grid[x, 0] == null)
            {
                bottomSlots.Add(
                    new Vector2Int(x, 0)
                );
            }
        }

        if (bottomSlots.Count == 0)
            return;

        Vector2Int slot =
            bottomSlots[
                Random.Range(
                    0,
                    bottomSlots.Count
                )
            ];

        SpawnGun(
            slot.x,
            0,
            color,
            hp
        );

        Gun gun =
            manager.Grid[
                slot.x,
                0
            ];

        if (gun == null)
            return;

        if (gun.chain != null && gun.chain.IsPair)
        {
            gun.RevealGun();
            return;
        }

        if (CanSpawnMystery() && Random.value < mysteryGunChance)
            gun.HideGun();
        else
            gun.RevealGun();
    }

    public void SpawnReplacementSingleAtBottom(int x)
    {
        if (!infiniteMode)
            return;

        if (x < 0 || x >= manager.columns)
            return;

        if (manager.Grid[x, 0] != null)
            return;

        BlockColorData color =
            selector.GetRandomColor();

        if (color == null)
            return;

        SpawnGun(
            x,
            0,
            color,
            GetRandomGunHP()
        );

        Gun gun = manager.Grid[x, 0];

        if (gun == null)
            return;

        if (gun.chain != null && gun.chain.IsPair)
        {
            gun.RevealGun();
            return;
        }

        if (CanSpawnMystery() && Random.value < mysteryGunChance)
            gun.HideGun();
        else
            gun.RevealGun();
    }

    public void SpawnReplacementPairAtBottom(
     int x1,
     int x2
 )
    {
        if (!infiniteMode)
            return;

        if (
            x1 < 0 ||
            x1 >= manager.columns ||
            x2 < 0 ||
            x2 >= manager.columns
        )
        {
            return;
        }

        if (x1 == x2)
        {
            SpawnReplacementSingleAtBottom(x1);
            return;
        }

        if (
            manager.Grid[x1, 0] != null ||
            manager.Grid[x2, 0] != null
        )
        {
            return;
        }

        BlockColorData colorA =
            selector.GetRandomColor();

        BlockColorData colorB =
            selector.GetRandomColor();

        int safety = 0;

        while (
            colorB == colorA &&
            safety < 20
        )
        {
            colorB = selector.GetRandomColor();
            safety++;
        }

        if (colorA == null || colorB == null)
            return;

        if (colorA == colorB)
            return;

        SpawnGun(
            x1,
            0,
            colorA,
            GetRandomGunHP()
        );

        SpawnGun(
            x2,
            0,
            colorB,
            GetRandomGunHP()
        );

        Gun gunA =
            manager.Grid[x1, 0];

        Gun gunB =
            manager.Grid[x2, 0];

        if (gunA != null && gunB != null)
        {
            manager.Chain.CreateChain(
                gunA,
                gunB
            );

            gunA.RevealGun();
            gunB.RevealGun();
        }
    }

    public void SpawnReplacementPairSameColumn(int x)
    {
        if (!infiniteMode)
            return;

        if (x < 0 || x >= manager.columns)
            return;

        if (
            manager.Grid[x, 0] != null ||
            manager.Grid[x, 1] != null
        )
        {
            return;
        }

        BlockColorData colorA =
            selector.GetRandomColor();

        BlockColorData colorB =
            selector.GetRandomColor();

        int safety = 0;

        while (
            colorB == colorA &&
            safety < 20
        )
        {
            colorB = selector.GetRandomColor();
            safety++;
        }

        if (colorA == null || colorB == null)
            return;

        if (colorA == colorB)
            return;

        SpawnGun(
            x,
            0,
            colorA,
            GetRandomGunHP()
        );

        SpawnGun(
            x,
            1,
            colorB,
            GetRandomGunHP()
        );

        Gun gunA =
            manager.Grid[x, 0];

        Gun gunB =
            manager.Grid[x, 1];

        if (gunA != null && gunB != null)
        {
            manager.Chain.CreateChain(
                gunA,
                gunB
            );

            gunA.RevealGun();
            gunB.RevealGun();
        }
    }

    private Gun GetPrefabForColor(BlockColorData color, bool isFire) { if (color == null) return null; foreach (Gun prefab in manager.gunPrefabs) { if (prefab == null) continue; if (prefab.currentColor != color) continue; return prefab; } return null; }

    private void CreateInitialPairs()
    {
        if (!SpecialGunsUnlocked) return;

        if (manager == null || manager.Grid == null)
            return;

        List<Gun> available = new List<Gun>();

        for (int y = 0; y < manager.rows; y++)
        {
            for (int x = 0; x < manager.columns; x++)
            {
                Gun gun = manager.Grid[x, y];

                if (gun == null)
                    continue;

                if (!gun.inGrid)
                    continue;

                if (gun.chain != null && gun.chain.IsPair)
                    continue;

                available.Add(gun);
            }
        }

        int pairCount = Mathf.FloorToInt(available.Count * 0.3f / 2f);

        for (int i = 0; i < pairCount; i++)
        {
            List<Gun> possibleA = new List<Gun>();

            foreach (Gun gun in available)
            {
                if (gun == null)
                    continue;

                if (gun.chain != null && gun.chain.IsPair)
                    continue;

                possibleA.Add(gun);
            }

            if (possibleA.Count == 0)
                break;

            Gun gunA =
                possibleA[
                    Random.Range(
                        0,
                        possibleA.Count
                    )
                ];

            List<Gun> nearby = new List<Gun>();

            foreach (Gun candidate in available)
            {
                if (candidate == null || candidate == gunA)
                    continue;

                if (candidate.chain != null && candidate.chain.IsPair)
                    continue;

                if (candidate.currentColor == null)
                    continue;

                if (gunA.currentColor == null)
                    continue;

                if (candidate.currentColor == gunA.currentColor)
                    continue;

                int dx =
                    Mathf.Abs(
                        candidate.gridX -
                        gunA.gridX
                    );

                int dy =
                    Mathf.Abs(
                        candidate.gridY -
                        gunA.gridY
                    );

                if (dx + dy == 1)
                    nearby.Add(candidate);
            }

            if (nearby.Count == 0)
            {
                available.Remove(gunA);
                i--;
                continue;
            }

            Gun gunB =
                nearby[
                    Random.Range(
                        0,
                        nearby.Count
                    )
                ];

            manager.Chain.CreateChain(
                gunA,
                gunB
            );

            gunA.RevealGun();
            gunB.RevealGun();

            available.Remove(gunA);
            available.Remove(gunB);

            Debug.Log(
                "[INITIAL PAIR] " +
                gunA.currentColor.name +
                " | " +
                gunB.currentColor.name +
                " | (" +
                gunA.gridX +
                "," +
                gunA.gridY +
                ") <-> (" +
                gunB.gridX +
                "," +
                gunB.gridY +
                ")"
            );
        }
    }
}
