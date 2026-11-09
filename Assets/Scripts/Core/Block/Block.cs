using TMPro;
using UnityEngine;
using DG.Tweening;

public class Block : MonoBehaviour
{
    public BlockColorData blockColor;

    [HideInInspector]
    public BlockGroup group;

    [Header("Block Type")]
    public int frozenHP = 3;
    public int reinforceHP = 6;
    public BlockType blockType = BlockType.Normal;

    [Header("Grid")]
    public int gridX;
    public int gridY;

    [Header("HP")]
    public int hp;
    public int pendingDamage = 0;

    private bool isDestroyed = false;
    public bool IsDestroyed => isDestroyed;

    [Header("Visual")]
    public TMP_Text hpText;
    public SpriteRenderer spriteRenderer;
    public SpriteRenderer typeIconRenderer;

    [Header("Type Icons")]
    public Sprite frozenIcon;
    public Sprite bombIcon;
    public Sprite reinforcedIcon;
    public Sprite mysteryIcon;
    public Sprite lockedIcon;
    public Sprite countdownIcon;
    public Sprite rainbowIcon;
    public Sprite bubbleIcon;
    public Sprite stoneIcon;
    public Sprite banhMiIcon;

    [Header("Dual")]
    public SpriteRenderer dualTopRenderer;
    public SpriteRenderer dualBottomRenderer;
    public TMP_Text dualTopHPText;
    public TMP_Text dualBottomHPText;

    public BlockColorData dualTopColor;
    public BlockColorData dualBottomColor;

    public int dualTopHP;
    public int dualBottomHP;

    private Vector3 baseScale;
    public Vector3 BaseScale => baseScale;

    public int gridWidth = 1;
    public int gridHeight = 1;

    public bool IsBoss => blockType == BlockType.Boss;

    public void SetGridSize(int width, int height)
    {
        gridWidth = width;
        gridHeight = height;
    }

    private void Awake()
    {
        baseScale = transform.localScale;
    }

    public int GetInitialRequiredHP()
    {
        if (
            blockType == BlockType.Stone ||
            blockType == BlockType.Bubble ||
            blockType == BlockType.BanhMi ||
            blockType == BlockType.FriedChicken
        )
        {
            return 0;
        }

        if (blockType == BlockType.Dual)
        {
            return dualTopHP + dualBottomHP;
        }

        return hp;
    }

    public void UpdateHP()
    {
        if (blockType == BlockType.Dual)
        {
            UpdateDualHPTexts();
            return;
        }

        if (hpText == null)
            return;

        if (
            blockType == BlockType.Stone ||
            blockType == BlockType.Bubble ||
            blockType == BlockType.BanhMi ||
            blockType == BlockType.FriedChicken
        )
        {
            hpText.text = "";
            return;
        }

        if (group != null)
        {
            hpText.text = group.totalHP.ToString();
        }
        else
        {
            hpText.text = hp.ToString();
        }
    }

    public void SetHPText(int value)
    {
        if (blockType == BlockType.Dual)
        {
            UpdateDualHPTexts();
            return;
        }

        if (hpText == null)
            return;

        if (
            blockType == BlockType.Stone ||
            blockType == BlockType.Bubble ||
            blockType == BlockType.BanhMi ||
            blockType == BlockType.FriedChicken
        )
        {
            hpText.text = "";
            return;
        }

        hpText.text = value.ToString();
    }

    public void SetColor(BlockColorData color)
    {
        blockColor = color;
        hp = color != null ? color.defaultHP : 0;

        isDestroyed = false;

        if (spriteRenderer != null && color != null)
        {
            if (color.icon != null)
            {
                spriteRenderer.sprite = color.icon;
                spriteRenderer.color = Color.white;
            }
            else
            {
                spriteRenderer.sprite = null;
                spriteRenderer.color = color.color;
            }
        }

        if (blockType == BlockType.Bubble)
            SetWaterBackgroundHidden(true);

        UpdateHP();
    }

    public void SetDual(
        BlockColorData topColor,
        BlockColorData bottomColor
    )
    {
        blockType = BlockType.Dual;

        dualTopColor = topColor;
        dualBottomColor = bottomColor;

        dualTopHP = 0;
        dualBottomHP = 0;

        hp = dualTopHP + dualBottomHP;

        isDestroyed = false;

        if (spriteRenderer != null)
            spriteRenderer.enabled = false;

        if (typeIconRenderer != null)
            typeIconRenderer.enabled = false;

        if (dualTopRenderer != null)
        {
            dualTopRenderer.enabled = true;

            if (topColor != null)
            {
                dualTopRenderer.sprite = topColor.icon;

                dualTopRenderer.color =
                    topColor.icon != null
                    ? Color.white
                    : topColor.color;
            }
        }

        if (dualBottomRenderer != null)
        {
            dualBottomRenderer.enabled = true;

            if (bottomColor != null)
            {
                dualBottomRenderer.sprite = bottomColor.icon;

                dualBottomRenderer.color =
                    bottomColor.icon != null
                    ? Color.white
                    : bottomColor.color;
            }
        }

        UpdateDualHPTexts();
    }

    private void UpdateDualHPTexts()
    {
        DualBlockHealthDisplay.Refresh(this);

        bool groupAlive =
            group == null || !group.IsDead();

        if (dualTopRenderer != null)
            dualTopRenderer.enabled = groupAlive;

        if (dualBottomRenderer != null)
            dualBottomRenderer.enabled = groupAlive;
    }

    private void SetWaterBackgroundHidden(bool hidden)
    {
        if (spriteRenderer == null)
            return;

        Color color = spriteRenderer.color;

        color.a = hidden ? 0f : 1f;

        spriteRenderer.color = color;
        spriteRenderer.enabled = !hidden;
    }

    private void OnEnable()
    {
        if (blockType == BlockType.Bubble)
            SetWaterBackgroundHidden(true);

        if (Application.isPlaying)
            BoardBlockClipping.Bind(this);
    }

    public void SetBlockType(BlockType type)
    {
        bool wasWater = blockType == BlockType.Bubble;

        blockType = type;

        HideTypeIcon();

        if (wasWater || blockType == BlockType.Bubble)
        {
            SetWaterBackgroundHidden(
                blockType == BlockType.Bubble
            );
        }

        switch (blockType)
        {
            case BlockType.Frozen:
                ShowTypeIcon(frozenIcon, 0.5f);
                break;

            case BlockType.Stone:
                ShowTypeIcon(stoneIcon, 1f);
                break;

            case BlockType.Reinforced:
                ShowTypeIcon(reinforcedIcon, 1f);
                break;

            case BlockType.Mystery:
                if (group == null || !group.IsMysteryRevealed)
                    ShowTypeIcon(mysteryIcon, 1f);
                break;

            case BlockType.Locked:
                ShowTypeIcon(lockedIcon, 1f);
                break;

            case BlockType.Countdown:
                ShowTypeIcon(countdownIcon, 1f);
                break;

            case BlockType.Bubble:
                ShowTypeIcon(bubbleIcon, 1f);
                break;

            case BlockType.BanhMi:
                if (spriteRenderer != null)
                {
                    spriteRenderer.sprite = banhMiIcon;
                    spriteRenderer.color = Color.white;
                }
                break;

            case BlockType.Boss:
                break;

            case BlockType.Dual:
                if (spriteRenderer != null)
                    spriteRenderer.enabled = false;

                if (typeIconRenderer != null)
                    typeIconRenderer.enabled = false;

                UpdateDualHPTexts();
                break;

            case BlockType.Normal:
                break;
        }

        UpdateHP();
    }

    private void HideTypeIcon()
    {
        if (typeIconRenderer == null)
            return;

        typeIconRenderer.enabled = false;

        Color color = typeIconRenderer.color;
        color.a = 1f;

        typeIconRenderer.color = color;
    }

    public void ShowTypeIcon(Sprite icon, float alpha)
    {
        if (typeIconRenderer == null)
            return;

        if (icon == null)
            return;

        typeIconRenderer.enabled = true;
        typeIconRenderer.sprite = icon;

        Color color = typeIconRenderer.color;
        color.a = alpha;

        typeIconRenderer.color = color;
    }

    public void SetTypeIconVisible(bool visible)
    {
        if (typeIconRenderer == null)
            return;

        typeIconRenderer.enabled = visible;
    }

    public void UpdateTypeVisual()
    {
        SetBlockType(blockType);
    }

    public void TakeDamage(
        int damage,
        BlockColorData attackColor
    )
    {                                           
        if (IsDestroyed)
            return;

        if (
            blockType == BlockType.Stone ||
            blockType == BlockType.Bubble ||
            blockType == BlockType.BanhMi
        )
        {
            return;
        }

        if (attackColor == null)
            return;

        if (damage <= 0)
            return;

        if (group == null)
            return;

        if (blockType == BlockType.Dual)
        {
            group.TakeDualDamage(
                this,
                damage,
                attackColor
            );

            return;
        }

        if (group.groupType == BlockType.Boss)
        {
            if (BossPhaseManager.Instance == null)
                return;

            if (!BossPhaseManager.Instance.bossActive)
                return;

            if (!BossPhaseManager.Instance.CanDamage(attackColor))
                return;

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayGroupHitSound();
            }

            BossPhaseManager.Instance.Damage(
                attackColor,
                damage
            );

            return;
        }

        if (group.IsDead())
            return;

        if (group.groupType == BlockType.Bubble)
            return;

        group.RevealMystery();

        // Đã sửa: sử dụng âm thanh luân phiên
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayGroupHitSound();
        }

        group.TakeDamage(damage);
    }

    private void OnDestroy()
    {
        if (transform != null)
        {
            transform.DOKill(false);
        }
    }

    public void SetBossHP(int hp)
    {
        if (hpText != null)
        {
            hpText.text = hp.ToString();
        }
    }

    public void ClearBossHP()
    {
        if (hpText != null)
        {
            hpText.text = "";
        }
    }
}