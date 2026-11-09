using UnityEngine;
using UnityEngine.EventSystems;

public class GunDrag : MonoBehaviour, IPointerClickHandler
{
    private Gun gun;
    private GunRemovalManager removal;
    private GunGridSpawner spawner;
    private GunGridCollapse collapse;

    public GunRemovalManager Removal => removal;

    private void Awake()
    {
        gun = GetComponent<Gun>();
    }


    public void OnPointerClick(PointerEventData eventData)
    {
        if (Time.timeScale <= 0f || gun == null)
            return;

        if (BoosterTutorialPanel.Instance != null &&
            BoosterTutorialPanel.Instance.IsOpen)
            return;

        TryMoveGunToTray();
    }


    private void TryMoveGunToTray()
    {
        if (gun.lockedInTray || !gun.inGrid)
            return;

        GunGridManager manager = GunGridManager.Instance;

        if (manager == null)
            return;

        if (gun.chain != null && gun.chain.IsPair)
        {
            Gun other = gun.chain.GetOther(gun);

            if (other == null)
                return;

            if (IsGunBlocked(gun) || IsGunBlocked(other))
                return;

            if (!TraySlot.CanPlacePairSmart())
            {
                Debug.Log(
                    "[GunDrag] Súng đôi không thể vào Tray -> KIỂM TRA THUA"
                );

                manager.CheckPairCannotPlaceLose(
                    gun,
                    other
                );

                return;
            }

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
                    "[GunDrag] PlacePair thất bại sau khi RemovePair."
                );
            }

            return;
        }

        if (IsGunBlocked(gun))
            return;

        TraySlot bestSlot =
            TraySlot.FindBestSlotForColor(
                gun.currentColor
            );

        if (bestSlot == null)
            return;

        manager.Removal.RemoveGunFromGrid(
            gun
        );

        bestSlot.PlaceGunSmart(
            gun
        );
    }

    private bool IsGunBlocked(Gun targetGun)
    {
        if (targetGun == null)
            return true;

        if (!targetGun.inGrid)
            return false;

        GunGridManager manager =
            GunGridManager.Instance;

        if (manager == null)
            return false;

        Gun[,] grid =
            manager.GetGrid();

        if (grid == null)
            return false;

        int x = targetGun.gridX;
        int y = targetGun.gridY;

        if (
            x < 0 ||
            x >= manager.columns ||
            y < 0 ||
            y >= manager.rows
        )
        {
            return true;
        }

        for (
            int checkY = y + 1;
            checkY < manager.rows;
            checkY++
        )
        {
            Gun above =
                grid[x, checkY];

            if (above == null)
                continue;

            if (
                targetGun.chain != null &&
                targetGun.chain.IsPair
            )
            {
                Gun other =
                    targetGun.chain.GetOther(
                        targetGun
                    );

                if (above == other)
                    continue;
            }

            return true;
        }

        return false;
    }
}
