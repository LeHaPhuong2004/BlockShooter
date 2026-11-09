using UnityEngine;
using System.Collections.Generic;

public class GunChainManager
{
    private GunGridManager manager;
    private List<GunChain> chains = new List<GunChain>();

    public GunChainManager(GunGridManager manager)
    {
        this.manager = manager;
    }

    public List<GunChain> GetChains()
    {
        return chains;
    }

    public void CreateRandomChains()
    {
        int totalGuns = manager.CountGuns();
        int targetPairGuns = Mathf.RoundToInt(totalGuns * 0.3f);
        targetPairGuns -= targetPairGuns % 2;

        int pairCount = targetPairGuns / 2;

        for (int i = 0; i < pairCount; i++)
        {
            Gun gunA = GetRandomAvailableGun();

            if (gunA == null)
                break;

            Gun gunB = GetNearbyAvailableGun(gunA);

            if (gunB == null)
                continue;

            CreateChain(gunA, gunB);
        }
    }

    private Gun GetRandomAvailableGun()
    {
        List<Gun> available = new List<Gun>();

        for (int y = 0; y < manager.rows; y++)
        {
            for (int x = 0; x < manager.columns; x++)
            {
                Gun gun = manager.Grid[x, y];

                if (gun == null ||
                    gun.chain != null ||
                    !gun.inGrid ||
                    gun.gunType == GunType.Fire)
                    continue;

                available.Add(gun);
            }
        }

        if (available.Count == 0)
            return null;

        return available[Random.Range(0, available.Count)];
    }

    private Gun GetNearbyAvailableGun(Gun source)
    {
        if (source == null)
            return null;

        List<Gun> candidates = new List<Gun>();

        for (int y = 0; y < manager.rows; y++)
        {
            for (int x = 0; x < manager.columns; x++)
            {
                Gun gun = manager.Grid[x, y];

                if (gun == null ||
                    gun == source ||
                    gun.chain != null ||
                    !gun.inGrid ||
                    gun.gunType == GunType.Fire)
                    continue;

                int dx = Mathf.Abs(
                    gun.gridX - source.gridX
                );

                int dy = Mathf.Abs(
                    gun.gridY - source.gridY
                );

                if (dx == 0 && dy == 1)
                    candidates.Add(gun);
            }
        }

        if (candidates.Count == 0)
            return null;

        return candidates[
            Random.Range(0, candidates.Count)
        ];
    }

    public void CreateChain(Gun gunA, Gun gunB)
    {
        if (!GunGridSpawner.SpecialGunsUnlocked)
            return;

        if (gunA == null || gunB == null)
            return;

        if (
            gunA.gunType == GunType.Fire ||
            gunB.gunType == GunType.Fire
        )
            return;

        if (gunA.chain != null || gunB.chain != null)
            return;

        GameObject obj = new GameObject("GunChain");
        GunChain chain = obj.AddComponent<GunChain>();

        chain.SetGuns(gunA, gunB);

        chains.Add(chain);
    }

    public void RemoveChain(GunChain chain)
    {
        if (chain == null)
            return;

        chains.Remove(chain);

        if (chain.gameObject != null)
            Object.Destroy(chain.gameObject);
    }

    public void RemoveAllChains()
    {
        for (int i = chains.Count - 1; i >= 0; i--)
        {
            GunChain chain = chains[i];

            if (chain != null)
                Object.Destroy(chain.gameObject);
        }

        chains.Clear();
    }
}
