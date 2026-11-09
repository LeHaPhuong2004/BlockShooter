using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

public class GunGridCollapse
{
    private GunGridManager manager;

    public GunGridCollapse(GunGridManager manager)
    {
        this.manager = manager;
    }

    public void CollapseColumn(
        int x,
        int removedY
    )
    {
        if (
            x < 0 ||
            x >= manager.columns ||
            removedY < 0 ||
            removedY >= manager.rows
        )
            return;

        int targetY = removedY;

        for (
            int y = removedY + 1;
            y < manager.rows;
            y++
        )
        {
            Gun gun = manager.Grid[x, y];

            if (gun == null)
                continue;

            manager.Grid[x, y] = null;
            manager.Grid[x, targetY] = gun;

            gun.gridX = x;
            gun.gridY = targetY;
            gun.inGrid = true;

            if (gun.gameObject == null)
                continue;

            Transform target = gun.transform;

            if (target == null)
                continue;

            target.DOKill(true);

            target.DOMove(
                manager.GetGridPosition(
                    x,
                    targetY
                ),
                0.25f
            )
            .SetEase(Ease.OutBack)
            .SetLink(
                gun.gameObject,
                LinkBehaviour.KillOnDestroy
            );

            if (targetY == manager.rows - 1)
                gun.RevealGun();

            targetY++;
        }

        for (
            int y = targetY;
            y < manager.rows;
            y++
        )
        {
            manager.Grid[x, y] = null;
        }
    }

    public void CollapseColumnUp(
        int x,
        int removeY
    )
    {
        if (
            x < 0 ||
            x >= manager.columns ||
            removeY < 0 ||
            removeY >= manager.rows
        )
            return;

        for (
            int y = removeY - 1;
            y >= 0;
            y--
        )
        {
            Gun gun = manager.Grid[x, y];

            if (gun == null)
                continue;

            manager.Grid[x, y] = null;
            manager.Grid[x, y + 1] = gun;

            gun.gridX = x;
            gun.gridY = y + 1;
            gun.inGrid = true;

            if (gun.gameObject == null)
                continue;

            Transform target = gun.transform;

            if (target == null)
                continue;

            target.DOKill(true);

            target.DOMove(
                manager.GetGridPosition(
                    x,
                    y + 1
                ),
                0.25f
            )
            .SetEase(Ease.OutBack)
            .SetLink(
                gun.gameObject,
                LinkBehaviour.KillOnDestroy
            );

            if (y + 1 == manager.rows - 1)
                gun.RevealGun();
        }

        manager.Grid[x, 0] = null;
    }

    public void CollapseColumnAfterPairRemoval(int x, int pairBottomY)
    {
        if (
            x < 0 ||
            x >= manager.columns ||
            pairBottomY < 0 ||
            pairBottomY + 1 >= manager.rows
        )
            return;

        // Các súng nằm DƯỚI cặp súng đôi
        // được đẩy lên 2 ô.
        for (
            int y = pairBottomY - 1;
            y >= 0;
            y--
        )
        {
            Gun gun = manager.Grid[x, y];

            if (gun == null)
                continue;

            int newY = y + 2;

            manager.Grid[x, y] = null;
            manager.Grid[x, newY] = gun;

            gun.gridX = x;
            gun.gridY = newY;
            gun.inGrid = true;

            if (gun.gameObject == null)
                continue;

            Transform target = gun.transform;

            if (target == null)
                continue;

            target.DOKill(true);

            target.DOMove(
                manager.GetGridPosition(
                    x,
                    newY
                ),
                0.25f
            )
            .SetEase(Ease.OutBack)
            .SetLink(
                gun.gameObject,
                LinkBehaviour.KillOnDestroy
            );

            if (newY == manager.rows - 1)
                gun.RevealGun();
        }

        // Hai ô cũ ở phía dưới được để trống
        manager.Grid[x, 0] = null;
        manager.Grid[x, 1] = null;
    }
}