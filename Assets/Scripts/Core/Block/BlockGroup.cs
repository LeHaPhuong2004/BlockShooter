using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;

public class BlockGroup
{
    public List<Block> blocks =
        new List<Block>();

    public int totalHP;
    public int pendingDamage;

    public BlockType groupType =
        BlockType.Normal;

    public BlockColorData groupColor;

    [Header("Special HP")]
    public int frozenHP = 3;
    public int reinforceHP = 6;

    private int frozenRemaining;
    private int reinforceRemaining;
    private bool mysteryRevealed;

    public bool IsMysteryRevealed => mysteryRevealed;

    public int FrozenRemaining =>
        frozenRemaining;

    public int ReinforceRemaining =>
        reinforceRemaining;

    public bool isBoss;
    public int bossHPDivider = 4;

    private Dictionary<BlockColorData, int> pendingDamageByColor =
        new Dictionary<BlockColorData, int>();

    private int untypedPendingDamage;

    private readonly Dictionary<BlockColorData, int> dualHPByColor =
        new Dictionary<BlockColorData, int>();
    private bool dualHPInitialized;
    public int InitialDualHP { get; private set; }

    public void SetType(
        BlockType type
    )
    {
        if (groupType != type)
        {
            dualHPInitialized = false;
            dualHPByColor.Clear();
            InitialDualHP = 0;
        }
        groupType =
            type;
    }

    public void InitializedType()
    {
        frozenRemaining = 0;
        reinforceRemaining = 0;
        mysteryRevealed = false;

        pendingDamage = 0;
        untypedPendingDamage = 0;
        pendingDamageByColor.Clear();

        if (groupType == BlockType.Dual && !dualHPInitialized)
            InitializeDualHP();

        switch (groupType)
        {
            case BlockType.Reinforced:
                reinforceRemaining = reinforceHP;
                break;

            case BlockType.Frozen:
            case BlockType.Mystery:
            case BlockType.Locked:
            case BlockType.Countdown:
            case BlockType.Boss:
            case BlockType.Bubble:
            case BlockType.Stone:
            case BlockType.Dual:
            case BlockType.Normal:
                break;
        }

        UpdateTypeVisuals();
        UpdateAllTexts();
    }

    public void Add(
        Block block
    )
    {
        if (block == null)
            return;

        if (blocks.Contains(block))
            return;

        blocks.Add(block);

        block.group =
            this;

        int blockHP =
            block.GetInitialRequiredHP();

        if (isBoss)
        {
            blockHP =
                Mathf.Max(
                    0,
                    blockHP
                );
        }

        if (groupType == BlockType.Dual && dualHPInitialized)
            SyncDualBlockHP();
        else
            totalHP += blockHP;

        UpdateAllTexts();
    }

    public void NormalizeBossHP()
    {
        if (groupType != BlockType.Boss)
            return;

        totalHP =
            Mathf.Max(
                1,
                totalHP / 4
            );

        UpdateAllTexts();
    }

    public void Remove(
        Block block
    )
    {
        if (block == null)
            return;

        if (!blocks.Contains(block))
            return;

        block.transform.DOKill(true);

        blocks.Remove(block);

        if (block.group == this)
        {
            block.group = null;
        }

        UpdateAllTexts();
    }

    public bool IsDead()
    {
        if (groupType == BlockType.Boss)
        {
            if (
                BossPhaseManager.Instance != null &&
                BossPhaseManager.Instance.bossActive
            )
            {
                return false;
            }
        }

        if (totalHP > 0)
            return false;

        if (reinforceRemaining > 0)
            return false;

        return true;
    }
    public int AliveCount()
    {
        int count = 0;

        foreach (
            Block block
            in blocks
        )
        {
            if (
                block != null &&
                !block.IsDestroyed
            )
            {
                count++;
            }
        }

        return count;
    }

    public int GetAvailableHP()
    {
        int specialHP =
            GetSpecialRemaining();

        return Mathf.Max(
            0,
            totalHP +
            specialHP -
            pendingDamage
        );
    }

    public int GetAvailableHP(
        BlockColorData color
    )
    {
        if (color == null)
            return 0;

        if (groupType == BlockType.Dual)
        {
            int colorHP =
                GetDualColorHP(color);

            int colorPending =
                GetPendingDamageForColor(color);

            colorPending +=
                untypedPendingDamage;

            return Mathf.Max(
                0,
                colorHP -
                colorPending
            );
        }

        bool hasColor = false;

        foreach (Block block in blocks)
        {
            if (block == null)
                continue;

            if (block.IsDestroyed)
                continue;

            if (block.blockType == BlockType.Dual)
                continue;

            if (block.blockColor == color)
            {
                hasColor = true;
                break;
            }
        }

        if (!hasColor)
            return 0;

        return GetAvailableHP();
    }

    private int GetSpecialRemaining()
    {
        if (groupType == BlockType.Reinforced)
            return reinforceRemaining;

        return 0;
    }

    private int GetPendingDamageForColor(
        BlockColorData color
    )
    {
        if (color == null)
            return 0;

        if (
            pendingDamageByColor.TryGetValue(
                color,
                out int value
            )
        )
        {
            return value;
        }

        return 0;
    }

    public int GetDualColorHP(
        BlockColorData color
    )
    {
        if (color == null)
            return 0;

        if (dualHPInitialized)
            return dualHPByColor.TryGetValue(color, out int remaining) ? remaining : 0;

        int hp = 0;

        foreach (Block block in blocks)
        {
            if (block == null)
                continue;

            if (block.IsDestroyed)
                continue;

            if (block.blockType != BlockType.Dual)
                continue;

            if (
                block.dualTopColor == color &&
                block.dualTopHP > 0
            )
            {
                hp +=
                    block.dualTopHP;
            }

            if (
                block.dualBottomColor == color &&
                block.dualBottomHP > 0
            )
            {
                hp +=
                    block.dualBottomHP;
            }
        }

        return hp;
    }

    public bool TryGetDualColors(out BlockColorData first, out BlockColorData second)
    {
        first = null;
        second = null;
        if (groupType != BlockType.Dual) return false;
        foreach (Block block in blocks)
        {
            if (block == null || block.IsDestroyed) continue;
            if (block.dualTopColor == null || block.dualBottomColor == null ||
                block.dualTopColor == block.dualBottomColor) continue;
            first = block.dualTopColor;
            second = block.dualBottomColor;
            return true;
        }
        return false;
    }

    public bool CanReserveDualPair()
    {
        return dualHPInitialized && !IsDead() && TryGetDualColors(out BlockColorData first, out BlockColorData second) &&
            GetAvailableHP(first) > 0 && GetAvailableHP(second) > 0;
    }

    public bool TryReserveDualPair()
    {
        if (!CanReserveDualPair() || !TryGetDualColors(out BlockColorData first, out BlockColorData second))
            return false;
        // Reserve the complete pair before either projectile is created.
        ReserveDamage(1, first);
        ReserveDamage(1, second);
        return true;
    }

    private void InitializeDualHP()
    {
        if (!TryGetDualColors(out BlockColorData first, out BlockColorData second)) return;
        int halfHP = Random.Range(10, 16);
        dualHPByColor.Clear();
        dualHPByColor.Add(first, halfHP);
        dualHPByColor.Add(second, halfHP);
        dualHPInitialized = true;
        InitialDualHP = totalHP = halfHP * 2;
        SyncDualBlockHP();
    }

    private void SyncDualBlockHP()
    {
        foreach (Block block in blocks)
        {
            if (block == null || block.blockType != BlockType.Dual) continue;
            block.dualTopHP = GetDualColorHP(block.dualTopColor);
            block.dualBottomHP = GetDualColorHP(block.dualBottomColor);
            block.hp = totalHP;
        }
    }

    public bool CanReserveDamage(
        int damage
    )
    {
        if (damage <= 0)
            return false;

        if (IsDead())
            return false;

        return GetAvailableHP() >=
            damage;
    }

    public bool CanReserveDamage(
        int damage,
        BlockColorData color
    )
    {
        if (damage <= 0)
            return false;

        if (IsDead())
            return false;

        return GetAvailableHP(color) >=
            damage;
    }

    public void ReserveDamage(
        int damage
    )
    {
        if (damage <= 0)
            return;

        if (!CanReserveDamage(damage))
            return;

        pendingDamage +=
            damage;

        untypedPendingDamage +=
            damage;
    }

    public void ReserveDamage(
        int damage,
        BlockColorData color
    )
    {
        if (damage <= 0)
            return;

        if (color == null)
            return;

        if (!CanReserveDamage(
            damage,
            color
        ))
        {
            return;
        }

        pendingDamage +=
            damage;

        if (!pendingDamageByColor.ContainsKey(color))
        {
            pendingDamageByColor[color] = 0;
        }

        pendingDamageByColor[color] +=
            damage;
    }

    public void ReleaseDamage(
        int damage
    )
    {
        if (damage <= 0)
            return;

        int released =
            Mathf.Min(
                damage,
                pendingDamage
            );

        pendingDamage =
            Mathf.Max(
                0,
                pendingDamage - released
            );

        int untypedReleased =
            Mathf.Min(
                released,
                untypedPendingDamage
            );

        untypedPendingDamage =
            Mathf.Max(
                0,
                untypedPendingDamage -
                untypedReleased
            );

        int remaining =
            released -
            untypedReleased;

        if (remaining <= 0)
            return;

        List<BlockColorData> colors =
            new List<BlockColorData>(
                pendingDamageByColor.Keys
            );

        foreach (
            BlockColorData color
            in colors
        )
        {
            if (remaining <= 0)
                break;

            if (
                !pendingDamageByColor.TryGetValue(
                    color,
                    out int colorPending
                )
            )
            {
                continue;
            }

            int remove =
                Mathf.Min(
                    remaining,
                    colorPending
                );

            colorPending -=
                remove;

            remaining -=
                remove;

            if (colorPending <= 0)
            {
                pendingDamageByColor.Remove(
                    color
                );
            }
            else
            {
                pendingDamageByColor[color] =
                    colorPending;
            }
        }
    }

    public void ReleaseDamage(
        int damage,
        BlockColorData color
    )
    {
        if (damage <= 0)
            return;

        if (color == null)
        {
            ReleaseDamage(damage);
            return;
        }

        int colorPending =
            GetPendingDamageForColor(
                color
            );

        int released =
            Mathf.Min(
                damage,
                colorPending
            );

        if (released > 0)
        {
            colorPending -=
                released;

            if (colorPending <= 0)
            {
                pendingDamageByColor.Remove(
                    color
                );
            }
            else
            {
                pendingDamageByColor[color] =
                    colorPending;
            }

            pendingDamage =
                Mathf.Max(
                    0,
                    pendingDamage - released
                );
        }
    }

    private int GetRemainingDualHP()
    {
        if (dualHPInitialized) return totalHP;
        int hp = 0;

        foreach (Block block in blocks)
        {
            if (block == null)
                continue;

            if (block.blockType != BlockType.Dual)
                continue;

            hp +=
                block.dualTopHP;

            hp +=
                block.dualBottomHP;
        }

        return hp;
    }

    public void TakeDualDamage(
        Block targetBlock,
        int damage,
        BlockColorData attackColor
    )
    {
        if (damage <= 0)
            return;

        if (attackColor == null)
            return;

        int availableColorHP =
            GetDualColorHP(
                attackColor
            );

        if (availableColorHP <= 0)
        {
            ReleaseDamage(
                damage,
                attackColor
            );

            return;
        }

        int actualDamage =
            Mathf.Min(
                damage,
                availableColorHP
            );

        ReleaseDamage(
            actualDamage,
            attackColor
        );

        bool hit = false;
        if (dualHPInitialized)
        {
            dualHPByColor[attackColor] = availableColorHP - actualDamage;
            hit = true;
        }
        else foreach (
            Block block
            in blocks
        )
        {
            if (block == null)
                continue;

            if (block.blockType != BlockType.Dual)
                continue;

            if (
                block.dualTopColor == attackColor &&
                block.dualTopHP > 0
            )
            {
                block.dualTopHP =
                    Mathf.Max(
                        0,
                        block.dualTopHP -
                        actualDamage
                    );

                hit = true;

                block.UpdateHP();

                break;
            }

            if (
                block.dualBottomColor == attackColor &&
                block.dualBottomHP > 0
            )
            {
                block.dualBottomHP =
                    Mathf.Max(
                        0,
                        block.dualBottomHP -
                        actualDamage
                    );

                hit = true;

                block.UpdateHP();

                break;
            }
        }

        if (!hit)
            return;

        totalHP =
            Mathf.Max(
                0,
                totalHP -
                actualDamage
            );

        if (dualHPInitialized) SyncDualBlockHP();

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayGroupHitSound();
        }

        UpdateAllTexts();

        PlayHitEffect();

        bool hasDual =
            false;

        bool allDualEmpty =
            true;

        foreach (
            Block block
            in blocks
        )
        {
            if (block == null)
                continue;

            if (block.blockType != BlockType.Dual)
                continue;

            hasDual = true;

            if (
                block.dualTopHP > 0 ||
                block.dualBottomHP > 0
            )
            {
                allDualEmpty = false;
                break;
            }
        }

        if (!hasDual)
            return;

        if (!allDualEmpty)
            return;

        foreach (
            Block block
            in blocks
        )
        {
            if (block == null)
                continue;

            if (block.blockType != BlockType.Dual)
                continue;

            if (block.dualTopRenderer != null)
            {
                BlockShatter.Shatter(
                    block.dualTopRenderer,
                    1f
                );
            }

            if (block.dualBottomRenderer != null)
            {
                BlockShatter.Shatter(
                    block.dualBottomRenderer,
                    1f
                );
            }
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(
                AudioManager.Instance
                    .groupBreakSound
            );
        }

        totalHP = 0;

        pendingDamage = 0;
        untypedPendingDamage = 0;
        pendingDamageByColor.Clear();

        if (GridManager.Instance != null)
        {
            GridManager.Instance.DestroyGroupFromGroup(
                this
            );
        }
    }

    public void RevealMystery()
    {
        if (groupType != BlockType.Mystery || mysteryRevealed || IsDead())
            return;

        mysteryRevealed = true;

        foreach (Block block in blocks)
        {
            if (block != null && block.blockType == BlockType.Mystery)
                block.SetTypeIconVisible(false);
        }
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0)
            return;

        if (IsDead())
            return;

        ReleaseDamage(damage);

        int remainingDamage = damage;

        if (groupType == BlockType.Reinforced)
        {
            if (reinforceRemaining > 0)
            {
                int damageToReinforced =
                    Mathf.Min(
                        remainingDamage,
                        reinforceRemaining
                    );

                reinforceRemaining -=
                    damageToReinforced;

                remainingDamage -=
                    damageToReinforced;

#if GAMEPLAY_DIAGNOSTICS
                Debug.Log(
                    $"[REINFORCED] Remaining = {reinforceRemaining}"
                );
#endif

                PlayHitEffect();

                if (reinforceRemaining <= 0)
                {
                    reinforceRemaining = 0;

#if GAMEPLAY_DIAGNOSTICS
                    Debug.Log(
                        "[REINFORCED] BROKEN"
                    );
#endif

                    ShatterReinforced();
                }

                UpdateTypeVisuals();

                if (remainingDamage <= 0)
                {
                    UpdateAllTexts();
                    return;
                }
            }
        }

        if (remainingDamage > 0)
        {
            totalHP =
                Mathf.Max(
                    0,
                    totalHP -
                    remainingDamage
                );

#if GAMEPLAY_DIAGNOSTICS
            Debug.Log(
                $"[GROUP HP] {totalHP}"
            );
#endif
        }

        UpdateAllTexts();

        PlayHitEffect();

        if (
            totalHP <= 0 &&
            reinforceRemaining <= 0
        )
        {
            pendingDamage = 0;
            untypedPendingDamage = 0;
            pendingDamageByColor.Clear();

            if (GridManager.Instance != null)
            {
                GridManager.Instance
                    .DestroyGroupFromGroup(
                        this
                    );
            }
        }
    }
    private void ShatterFrozen()
    {
        foreach (
            Block block
            in blocks
        )
        {
            if (block == null)
                continue;

            if (block.IsDestroyed)
                continue;

            BlockShatter.ShatterFrozen(
                block,
                1f
            );
        }
    }

    private void ShatterReinforced()
    {
        foreach (
            Block block
            in blocks
        )
        {
            if (block == null)
                continue;

            if (block.IsDestroyed)
                continue;

            BlockShatter.ShatterReinforced(
                block,
                1f
            );
        }
    }

    public void UpdateTypeVisuals()
    {
        foreach (Block block in blocks)
        {
            if (block == null)
                continue;

            block.blockType =
                groupType;

            switch (groupType)
            {
                case BlockType.Frozen:

                    block.SetBlockType(
                        BlockType.Frozen
                    );

                    block.SetTypeIconVisible(
                        true
                    );

                    break;

                case BlockType.Reinforced:

                    block.SetBlockType(
                        BlockType.Reinforced
                    );

                    block.SetTypeIconVisible(
                        reinforceRemaining > 0
                    );

                    break;

                case BlockType.Mystery:

                    block.SetBlockType(
                        BlockType.Mystery
                    );

                    break;

                case BlockType.Locked:

                    block.SetBlockType(
                        BlockType.Locked
                    );

                    break;

                case BlockType.Countdown:

                    block.SetBlockType(
                        BlockType.Countdown
                    );

                    break;

                case BlockType.Boss:

                    block.SetBlockType(
                        BlockType.Boss
                    );

                    break;

                case BlockType.Bubble:

                    block.SetBlockType(
                        BlockType.Bubble
                    );

                    break;

                case BlockType.Stone:

                    block.SetBlockType(
                        BlockType.Stone
                    );

                    break;

                case BlockType.Dual:

                    block.SetBlockType(
                        BlockType.Dual
                    );

                    break;

                case BlockType.Normal:

                    block.SetBlockType(
                        BlockType.Normal
                    );

                    break;
            }
        }
    }
    public void UpdateAllTexts()
    {
        foreach (
            Block block
            in blocks
        )
        {
            if (block == null)
                continue;

            if (block.blockType == BlockType.Dual)
            {
                block.UpdateHP();
                continue;
            }

            block.SetHPText(
                totalHP
            );
        }
    }

    public void SetBossHP(
        int value
    )
    {
        foreach (
            Block block
            in blocks
        )
        {
            if (block == null)
                continue;

            block.SetBossHP(
                value
            );
        }
    }

    public void ClearBossHP()
    {
        foreach (
            Block block
            in blocks
        )
        {
            if (block == null)
                continue;

            block.ClearBossHP();
        }
    }

    public bool Contains(
        Block block
    )
    {
        return
            block != null &&
            blocks.Contains(block);
    }

    public void PlayHitEffect()
    {
        if (
            blocks == null ||
            blocks.Count == 0
        )
        {
            return;
        }

        foreach (
            Block block
            in blocks
        )
        {
            if (block == null)
                continue;

            if (block.gameObject == null)
                continue;

            Transform target =
                block.transform;

            if (target == null)
                continue;

            target.DOKill(false);

            Vector3 originalScale =
                block.BaseScale;

            target.localScale =
                originalScale;

            Tween scaleUp =
                target.DOScale(
                    originalScale * 1.12f,
                    0.06f
                )
                .SetEase(
                    Ease.OutQuad
                )
                .SetLink(
                    block.gameObject,
                    LinkBehaviour.KillOnDestroy
                );

            Tween scaleDown =
                target.DOScale(
                    originalScale,
                    0.09f
                )
                .SetEase(
                    Ease.OutBack
                )
                .SetLink(
                    block.gameObject,
                    LinkBehaviour.KillOnDestroy
                );

            Sequence blockSequence =
                DOTween.Sequence();

            blockSequence.Append(
                scaleUp
            );

            blockSequence.Append(
                scaleDown
            );

            blockSequence.SetTarget(target).SetLink(
                block.gameObject,
                LinkBehaviour.KillOnDestroy
            );
        }
    }
    public int GetReinforceRemaining()
    {
        return groupType == BlockType.Reinforced
            ? reinforceRemaining
            : 0;
    }
}
