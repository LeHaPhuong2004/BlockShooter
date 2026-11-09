using DG.Tweening;
using UnityEngine;
using System.Collections.Generic;

public class TraySlot : MonoBehaviour
{
    public Transform slotInfo;
    public Transform slotPoint;

    private Gun currentGun;
    public Gun CurrentGun => currentGun;

    public bool IsEmpty() => currentGun == null;
    public bool CanAcceptGun() => currentGun == null;

    public void PlaceGun(Gun newGun)
    {
        PlaceGunSmart(newGun);
    }

    public void PlaceGunSmart(Gun newGun)
    {
        if (newGun == null || currentGun != null) return;

        AssignGunToSlot(newGun, this);
    }

    public void RemoveGun(Gun gun)
    {
        if (currentGun != gun) return;

        currentGun = null;

        if (gun != null)
            gun.SetCurrentTray(null);
    }

    public static TraySlot[] GetSortedSlots()
    {
        TraySlot[] slots = FindObjectsOfType<TraySlot>();

        System.Array.Sort(
            slots,
            (a, b) => a.slotPoint.position.x.CompareTo(b.slotPoint.position.x)
        );

        return slots;
    }

    public static TraySlot FindBestSlotForColor(BlockColorData color)
    {
        TraySlot[] slots = GetSortedSlots();

        if (slots == null || slots.Length == 0) return null;

        List<TraySlot> emptySlots = new List<TraySlot>();

        foreach (TraySlot slot in slots)
        {
            if (slot != null && slot.CurrentGun == null)
                emptySlots.Add(slot);
        }

        if (emptySlots.Count == 0) return null;

        float target = GetColorGridNormalized(color);

        if (target < 0f)
            return emptySlots[0];

        float minX = slots[0].slotPoint.position.x;
        float maxX = slots[slots.Length - 1].slotPoint.position.x;

        TraySlot bestSlot = emptySlots[0];
        float bestScore = float.MaxValue;

        foreach (TraySlot slot in emptySlots)
        {
            if (slot == null || slot.slotPoint == null)
                continue;

            float normalizedX = Mathf.InverseLerp(
                minX,
                maxX,
                slot.slotPoint.position.x
            );

            float score = Mathf.Abs(normalizedX - target);

            if (score < bestScore)
            {
                bestScore = score;
                bestSlot = slot;
            }
        }

        return bestSlot;
    }

    public static TraySlot[] FindBestPairSlots(
        BlockColorData colorA,
        BlockColorData colorB
    )
    {
        TraySlot[] slots = GetSortedSlots();

        if (slots == null || slots.Length < 2)
            return null;

        List<int> pairs = GetConsecutiveEmptyPairs(slots);

        if (pairs.Count == 0)
            return null;

        int bestIndex = FindBestPairIndex(
            slots,
            pairs,
            colorA,
            colorB
        );

        if (bestIndex < 0)
            return null;

        return new TraySlot[]
        {
            slots[bestIndex],
            slots[bestIndex + 1]
        };
    }

    private static List<int> GetConsecutiveEmptyPairs(
        TraySlot[] slots
    )
    {
        List<int> pairs = new List<int>();

        for (int i = 0; i < slots.Length - 1; i++)
        {
            if (
                slots[i] != null &&
                slots[i + 1] != null &&
                slots[i].CurrentGun == null &&
                slots[i + 1].CurrentGun == null
            )
            {
                pairs.Add(i);
            }
        }

        return pairs;
    }

    private static int FindBestPairIndex(
        TraySlot[] slots,
        List<int> pairs,
        BlockColorData colorA,
        BlockColorData colorB
    )
    {
        float posA = GetColorGridNormalized(colorA);
        float posB = GetColorGridNormalized(colorB);

        int hpA = GetColorHP(colorA);
        int hpB = GetColorHP(colorB);

        bool hasA = posA >= 0f && hpA > 0;
        bool hasB = posB >= 0f && hpB > 0;

        if (!hasA && !hasB)
            return pairs[0];

        float minX = slots[0].slotPoint.position.x;
        float maxX = slots[slots.Length - 1].slotPoint.position.x;

        int bestIndex = pairs[0];
        float bestScore = float.MaxValue;

        foreach (int index in pairs)
        {
            float pairX =
                (
                    slots[index].slotPoint.position.x +
                    slots[index + 1].slotPoint.position.x
                ) * 0.5f;

            float pairXNormalized =
                Mathf.InverseLerp(minX, maxX, pairX);

            float score;

            if (hasA && hasB)
            {
                float distanceA =
                    Mathf.Abs(pairXNormalized - posA);

                float distanceB =
                    Mathf.Abs(pairXNormalized - posB);

                int totalHP = hpA + hpB;

                score =
                    (distanceA * hpA +
                     distanceB * hpB) /
                    totalHP;
            }
            else if (hasA)
            {
                score =
                    Mathf.Abs(pairXNormalized - posA);
            }
            else
            {
                score =
                    Mathf.Abs(pairXNormalized - posB);
            }

            score += index * 0.0001f;

            if (score < bestScore)
            {
                bestScore = score;
                bestIndex = index;
            }
        }

        return bestIndex;
    }

    public static bool PlacePairToBestSlots(
        Gun gunA,
        Gun gunB
    )
    {
        if (
            gunA == null ||
            gunB == null ||
            gunA == gunB
        )
        {
            return false;
        }

        TraySlot[] slots = GetSortedSlots();

        if (slots == null || slots.Length < 2)
            return false;

        TraySlot[] bestSlots =
            FindBestPairSlots(
                gunA.currentColor,
                gunB.currentColor
            );

        if (bestSlots != null)
        {
            AssignGunToSlot(gunA, bestSlots[0]);
            AssignGunToSlot(gunB, bestSlots[1]);

            return true;
        }

        if (!CompactTray())
            return false;

        slots = GetSortedSlots();

        bestSlots =
            FindBestPairSlots(
                gunA.currentColor,
                gunB.currentColor
            );

        if (bestSlots == null)
            return false;

        AssignGunToSlot(gunA, bestSlots[0]);
        AssignGunToSlot(gunB, bestSlots[1]);

        return true;
    }

    private static bool CompactTray()
    {
        TraySlot[] slots = GetSortedSlots();

        if (slots == null || slots.Length < 2)
            return false;

        List<Gun> guns = new List<Gun>();

        foreach (TraySlot slot in slots)
        {
            if (slot != null && slot.CurrentGun != null)
                guns.Add(slot.CurrentGun);
        }

        if (guns.Count + 2 > slots.Length)
            return false;

        foreach (TraySlot slot in slots)
            slot.currentGun = null;

        for (int i = 0; i < guns.Count; i++)
            AssignGunToSlot(guns[i], slots[i]);

        return true;
    }

    public bool PlacePairSmart(
        Gun gunA,
        Gun gunB
    )
    {
        return PlacePairToBestSlots(gunA, gunB);
    }

    public static bool CanPlacePairSmart()
    {
        TraySlot[] slots = GetSortedSlots();

        if (slots == null || slots.Length < 2)
            return false;

        // Kiểm tra nhanh xem hiện tại đã có 2 ô trống liên tiếp chưa
        for (int i = 0; i < slots.Length - 1; i++)
        {
            if (
                slots[i].CurrentGun == null &&
                slots[i + 1].CurrentGun == null
            )
            {
                return true;
            }
        }

        // [ĐÃ SỬA]: Nếu chưa có ô liên tiếp, thử dồn khay (Compact) trước khi kết luận hết chỗ
        // Giúp tránh việc các ô trống bị rời rạc dẫn đến việc báo thua oan uổng.
        if (CompactTray())
        {
            slots = GetSortedSlots();
            for (int i = 0; i < slots.Length - 1; i++)
            {
                if (
                    slots[i].CurrentGun == null &&
                    slots[i + 1].CurrentGun == null
                )
                {
                    return true;
                }
            }
        }

        return GetEmptyCount(slots) >= 2;
    }

    private static int GetEmptyCount(
        TraySlot[] slots
    )
    {
        int count = 0;

        foreach (TraySlot slot in slots)
        {
            if (slot != null && slot.CurrentGun == null)
                count++;
        }

        return count;
    }

    private static int GetColorHP(
        BlockColorData color
    )
    {
        if (
            color == null ||
            GridManager.Instance == null
        )
        {
            return 0;
        }

        return GridManager.Instance.GetTotalHPByColor(color);
    }

    private static float GetColorGridNormalized(
        BlockColorData color
    )
    {
        if (
            color == null ||
            GridManager.Instance == null
        )
        {
            return -1f;
        }

        GridManager grid = GridManager.Instance;
        Block[,] blocks = grid.GetGrid();

        if (blocks == null)
            return -1f;

        float totalX = 0f;
        int count = 0;

        for (int y = 0; y < grid.height; y++)
        {
            for (int x = 0; x < grid.width; x++)
            {
                Block block = blocks[x, y];

                if (block == null)
                    continue;

                if (block.blockColor != color)
                    continue;

                totalX += x;
                count++;
            }
        }

        if (count == 0)
            return -1f;

        float averageX =
            totalX / count;

        if (grid.width <= 1)
            return 0f;

        return averageX /
               (grid.width - 1);
    }

    private static void AssignGunToSlot(
        Gun gun,
        TraySlot slot
    )
    {
        if (gun == null || slot == null)
            return;

        if (slot.currentGun != null)
            return;

        if (gun.gameObject == null)
            return;

        if (slot.slotPoint == null)
            return;

        slot.currentGun = gun;

        gun.inGrid = false;
        gun.lockedInTray = true;
        gun.canShoot = false;

        gun.SetCurrentTray(slot);

        gun.transform.SetParent(slot.slotPoint);

        gun.transform.DOKill(true);

        if (gun.gunVisual != null)
            gun.gunVisual.DOKill(true);

        const float moveDuration = 0.9f / 1.4f;
        gun.SetWheelRolling(true);

        Tween moveTween =
            gun.transform
            .DOMove(
                slot.slotPoint.position,
                moveDuration
            )
            .SetEase(
                Ease.OutBack
            )
            .SetLink(
                gun.gameObject,
                LinkBehaviour.KillOnDestroy
            );

        moveTween.OnComplete(() =>
        {
            if (gun != null)
                gun.SetWheelRolling(false);

            if (gun == null)
                return;

            if (slot == null)
                return;

            if (gun.CurrentTray != slot)
                return;

            if (!gun.lockedInTray)
                return;

            if (gun.currentHP > 0)
            {
                gun.canShoot = true;
            }

            if (GunGridManager.Instance != null)
            {
                GunGridManager.Instance.CheckLoseCondition();
            }
        });

        gun.transform
            .DOScale(
                Vector3.one * 0.8f,
                moveDuration
            )
            .SetEase(Ease.OutBack)
            .SetLink(
                gun.gameObject,
                LinkBehaviour.KillOnDestroy
            );
    }

    public static bool IsFull()
    {
        TraySlot[] slots = GetSortedSlots();

        if (slots == null || slots.Length == 0)
            return false;

        int validSlots = 0;

        foreach (TraySlot slot in slots)
        {
            if (slot == null)
                continue;

            validSlots++;

            if (slot.CurrentGun == null)
                return false;
        }

        if (validSlots == 0)
            return false;

        return true;
    }
}
