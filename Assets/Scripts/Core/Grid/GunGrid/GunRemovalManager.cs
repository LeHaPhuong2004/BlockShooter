using UnityEngine;

public class GunRemovalManager
{
    private GunGridManager manager;
    private GunGridCollapse collapse;
    private GunGridSpawner spawner;

    public GunRemovalManager(GunGridManager manager, GunGridCollapse collapse, GunGridSpawner spawner)
    {
        this.manager = manager;
        this.collapse = collapse;
        this.spawner = spawner;
    }

    public void RemoveGun(Gun gun)
    {
        if (gun == null || !gun.inGrid)
            return;

        bool isPair = gun.chain != null && gun.chain.IsPair;
        GunChain oldChain = gun.chain;
        Gun other = isPair ? oldChain.GetOther(gun) : null;

        if (!isPair)
        {
            int x = gun.gridX;
            int y = gun.gridY;

            RemoveGunFromGridOnly(gun);
            collapse.CollapseColumn(x, y);
            spawner.SpawnReplacementSingle();
            return;
        }

        int x1 = gun.gridX;
        int y1 = gun.gridY;
        int x2 = -1;
        int y2 = -1;

        if (other != null)
        {
            x2 = other.gridX;
            y2 = other.gridY;
        }

        RemoveGunFromGridOnly(gun);

        if (other != null)
            RemoveGunFromGridOnly(other);

        collapse.CollapseColumn(x1, y1);

        if (other != null && x2 != x1)
            collapse.CollapseColumn(x2, y2);

        spawner.SpawnReplacementPair();
    }

    public void TakeGunToTray(Gun gun)
    {
        if (gun == null || !gun.inGrid)
            return;

        GunChain chain = gun.chain;

        if (chain == null || !chain.IsPair)
        {
            int x = gun.gridX;
            int y = gun.gridY;

            RemoveGunFromGridOnly(gun);

            collapse.CollapseColumnUp(x, y);

            spawner.SpawnReplacementSingleAtBottom(x);
            return;
        }

        Gun other = chain.GetOther(gun);

        if (other == null)
            return;

        int x1 = gun.gridX;
        int y1 = gun.gridY;
        int x2 = other.gridX;
        int y2 = other.gridY;

        RemoveGunFromGridOnly(gun);
        RemoveGunFromGridOnly(other);

        gun.chain = chain;
        other.chain = chain;

        if (x1 == x2)
        {
            int pairBottomY = Mathf.Min(y1, y2);

            collapse.CollapseColumnAfterPairRemoval(
                x1,
                pairBottomY
            );

            spawner.SpawnReplacementPairSameColumn(x1);
        }
        else
        {
            collapse.CollapseColumnUp(x1, y1);
            collapse.CollapseColumnUp(x2, y2);

            spawner.SpawnReplacementPairAtBottom(x1, x2);
        }
    }

    public void RemovePairFromGridForTray(Gun gunA, Gun gunB)
    {
        if (gunA == null || gunB == null)
            return;

        int x1 = gunA.gridX;
        int y1 = gunA.gridY;
        int x2 = gunB.gridX;
        int y2 = gunB.gridY;

        GunChain chain = gunA.chain;

        RemoveGunFromGridOnly(gunA);
        RemoveGunFromGridOnly(gunB);

        gunA.chain = chain;
        gunB.chain = chain;
        if (x1 == x2)
        {
            int pairBottomY = Mathf.Min(y1, y2);

            collapse.CollapseColumnAfterPairRemoval(
                x1,
                pairBottomY
            );

            spawner.SpawnReplacementPairSameColumn(x1);
        }
        else
        {
            collapse.CollapseColumnUp(x1, y1);
            collapse.CollapseColumnUp(x2, y2);
            spawner.SpawnReplacementPairAtBottom(x1, x2);
        }
    }

    private void RemoveGunFromGridOnly(Gun gun)
    {
        if (gun == null)
            return;

        int x = gun.gridX;
        int y = gun.gridY;

        if (x >= 0 && x < manager.columns && y >= 0 && y < manager.rows && manager.Grid[x, y] == gun)
        {
            manager.Grid[x, y] = null;
        }

        gun.inGrid = false;
    }

    public void RemoveGunFromGrid(Gun gun)
    {
        if (gun == null || !gun.inGrid)
            return;

        int x = gun.gridX;
        int y = gun.gridY;

        bool isPair = gun.chain != null && gun.chain.IsPair;
        GunChain chain = gun.chain;
        Gun other = isPair ? chain.GetOther(gun) : null;

        if (!isPair)
        {
            RemoveGunFromGridOnly(gun);
            collapse.CollapseColumnUp(x, y);
            spawner.SpawnReplacementSingleAtBottom(x);
            return;
        }

        int x2 = -1;
        int y2 = -1;

        if (other != null)
        {
            x2 = other.gridX;
            y2 = other.gridY;
        }

        RemoveGunFromGridOnly(gun);

        if (other != null)
            RemoveGunFromGridOnly(other);

        manager.Chain.RemoveChain(chain);

        collapse.CollapseColumnUp(x, y);

        if (other != null && x2 != x)
            collapse.CollapseColumnUp(x2, y2);

        spawner.SpawnReplacementPairAtBottom(x2, y2);
    }

    public void RemoveSingleGunFromGridForTakeBelow(Gun gun)
    {
        if (gun == null || !gun.inGrid)
            return;

        int x = gun.gridX;
        int y = gun.gridY;

        RemoveGunFromGridOnly(gun);

        collapse.CollapseColumnUp(x, y);

        spawner.SpawnReplacementSingleAtBottom(x);
    }
}