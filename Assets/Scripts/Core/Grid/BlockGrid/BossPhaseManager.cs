using System.Collections.Generic;
using UnityEngine;

public class BossPhaseManager : MonoBehaviour
{
    public static BossPhaseManager Instance;

    public bool bossActive = true;

    private List<BlockColorData> phaseColors =
        new List<BlockColorData>();

    private List<int> phaseHP =
        new List<int>();

    private BlockGroup bossGroup;

    private int currentPhase = 0;
    private BlockColorData currentColor;

    private int currentPhaseHP;
    private int totalBossHP;
    private int remainingBossHP;

    private void Awake()
    {
        Instance = this;
    }

    public void CreatePhases()
    {
        phaseColors.Clear();
        phaseHP.Clear();

        GridManager grid =
            GridManager.Instance;

        if (grid == null)
            return;

        totalBossHP = 0;

        foreach (
            BlockColorData color
            in grid.colors
        )
        {
            if (color == null)
                continue;

            int totalHP =
                grid.GetTotalHPByColor(
                    color
                );

            if (totalHP <= 0)
                continue;

            phaseColors.Add(
                color
            );

            phaseHP.Add(
                totalHP
            );

            totalBossHP +=
                totalHP;
        }

        remainingBossHP =
            totalBossHP;
    }

    public void RegisterBoss(
        BlockGroup group
    )
    {
        if (group == null)
            return;

        bossGroup =
            group;

        bossActive =
            true;

        CreatePhases();

        if (phaseColors.Count == 0)
        {
            bossActive = false;
            return;
        }

        currentPhase = 0;

        StartPhase();
    }

    private void StartPhase()
    {
        if (
            currentPhase < 0 ||
            currentPhase >= phaseColors.Count
        )
        {
            FinishBoss();
            return;
        }

        currentColor =
            phaseColors[
                currentPhase
            ];

        currentPhaseHP =
            phaseHP[
                currentPhase
            ];

        ApplyPhaseToBoss();

        UpdateBossHP();
    }

    private void ApplyPhaseToBoss()
    {
        if (bossGroup == null)
            return;

        bossGroup.groupColor =
            currentColor;

        bossGroup.SetType(
            BlockType.Boss
        );

        foreach (
            Block block
            in bossGroup.blocks
        )
        {
            if (block == null)
                continue;

            if (block.IsDestroyed)
                continue;

            block.SetColor(
                currentColor
            );

            block.SetBlockType(
                BlockType.Boss
            );
        }
    }

    public bool CanDamage(
        BlockColorData color
    )
    {
        if (!bossActive)
            return false;

        if (bossGroup == null)
            return false;

        if (color == null)
            return false;

        return color ==
            currentColor;
    }

    public void Damage(
        BlockColorData color,
        int damage
    )
    {
        if (!bossActive)
            return;

        if (bossGroup == null)
            return;

        if (color == null)
            return;

        if (
            color !=
            currentColor
        )
        {
            return;
        }

        if (damage <= 0)
            return;

        int actualDamage =
            Mathf.Min(
                damage,
                currentPhaseHP
            );

        if (actualDamage <= 0)
            return;

        currentPhaseHP -=
            actualDamage;

        remainingBossHP -=
            actualDamage;

        remainingBossHP =
            Mathf.Max(
                0,
                remainingBossHP
            );

        UpdateBossHP();

        if (currentPhaseHP <= 0)
        {
            NextPhase();
        }
    }

    private void UpdateBossHP()
    {
        if (bossGroup == null)
            return;

        foreach (
            Block block
            in bossGroup.blocks
        )
        {
            if (block == null)
                continue;

            if (block.IsDestroyed)
                continue;

            block.SetBossHP(
                remainingBossHP
            );
        }
    }

    private void NextPhase()
    {
        currentPhase++;

        if (
            currentPhase >=
            phaseColors.Count
        )
        {
            FinishBoss();
            return;
        }

        StartPhase();
    }

    private void FinishBoss()
    {
        bossActive = false;

        currentColor = null;

        currentPhaseHP = 0;

        remainingBossHP = 0;

        if (bossGroup != null)
        {
            bossGroup.ClearBossHP();
        }
    }

    public BlockColorData GetCurrentColor()
    {
        return currentColor;
    }

    public int GetCurrentHP()
    {
        return remainingBossHP;
    }

    public int GetCurrentPhaseHP()
    {
        return currentPhaseHP;
    }

    public int GetTotalBossHP()
    {
        return totalBossHP;
    }

    public int GetCurrentPhase()
    {
        return currentPhase;
    }

    public BlockGroup GetBossGroup()
    {
        return bossGroup;
    }
}