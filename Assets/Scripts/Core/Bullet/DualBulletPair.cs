using System.Collections;
using UnityEngine;

/// <summary>Commits a dual volley after both shots arrive, showing each point of damage separately.</summary>
public sealed class DualBulletPair : MonoBehaviour
{
    private readonly Bullet[] bullets = new Bullet[2];
    private readonly BlockColorData[] colors = new BlockColorData[2];
    private readonly Block[] hitBlocks = new Block[2];
    private readonly Vector3[] impacts = new Vector3[2];
    private readonly Vector2[] directions = new Vector2[2];
    private readonly bool[] arrived = new bool[2];
    private readonly bool[] pending = { true, true };
    private BlockGroup group;
    private bool finished;
    private bool resolving;
    private static readonly System.Collections.Generic.HashSet<DualBulletPair> active = new System.Collections.Generic.HashSet<DualBulletPair>();
    private static readonly WaitForSeconds HitInterval = new WaitForSeconds(.06f);
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetActive() => active.Clear();
    private void OnEnable() => active.Add(this);
    private void OnDisable() => active.Remove(this);

    public static bool HasPendingShots()
    {
        if (!Application.isPlaying)
        {
            foreach (DualBulletPair pair in FindObjectsByType<DualBulletPair>(FindObjectsInactive.Exclude))
                if (!pair.finished) return true;
            return false;
        }
        foreach (DualBulletPair pair in active)
            if (!pair.finished) return true;
        return false;
    }

    public void Bind(Bullet first, Bullet second, BlockGroup targetGroup)
    {
        bullets[0] = first;
        bullets[1] = second;
        colors[0] = first.bulletColor;
        colors[1] = second.bulletColor;
        group = targetGroup;
    }

    public void Arrive(Bullet bullet, Block block, Vector3 impact, Vector2 direction)
    {
        if (finished || resolving) return;
        int index = bullet == bullets[0] ? 0 : bullet == bullets[1] ? 1 : -1;
        if (index < 0 || arrived[index]) return;
        arrived[index] = true;
        // Arrived projectiles may return to the pool before the other color arrives.
        // Never let a later cancellation cancel a newly rented shot on that same object.
        bullets[index] = null;
        hitBlocks[index] = block;
        impacts[index] = impact;
        directions[index] = direction;
        if (!arrived[0] || !arrived[1]) return;
        resolving = true;
        if (Application.isPlaying) StartCoroutine(ApplyHits());
    }

    private IEnumerator ApplyHits()
    {
        if (finished || !arrived[0] || !arrived[1]) yield break;
        if (!ApplyHit(0)) { Cancel(); yield break; }
        // A rendered interval exposes N-1 before the second color exposes N-2.
        yield return HitInterval;
        if (finished) yield break;
        if (!ApplyHit(1)) { Cancel(); yield break; }
        finished = true;
        CheckLoseCondition();
        Dispose();
    }

    private bool ApplyHit(int index)
    {
        if (group == null || group.IsDead()) return false;
        Block block = hitBlocks[index];
        if (block == null || block.group != group || !BoardViewport.IsBlockVisible(block))
        {
            block = null;
            foreach (Block member in group.blocks)
                if (member != null && member.group == group && BoardViewport.IsBlockVisible(member))
                { block = member; break; }
        }
        if (block == null) return false;
        pending[index] = false;
        WaterSplashEffect.Play(block, impacts[index], directions[index], colors[index].color);
        block.TakeDamage(1, colors[index]);
        return true;
    }

    public void Cancel()
    {
        if (finished) return;
        finished = true;
        StopAllCoroutines();
        ReleasePending();
        foreach (Bullet bullet in bullets)
            if (bullet != null) bullet.CancelDualShot();
        CheckLoseCondition();
        Dispose();
    }

    private void ReleasePending()
    {
        for (int i = 0; i < pending.Length; i++)
        {
            if (!pending[i]) continue;
            pending[i] = false;
            if (group != null) group.ReleaseDamage(1, colors[i]);
        }
    }

    private static void CheckLoseCondition()
    {
        if (Application.isPlaying && GridManager.Instance != null && !GridManager.Instance.IsExiting &&
            GunGridManager.Instance != null && GunGridManager.Instance.isActiveAndEnabled)
            GunGridManager.Instance.CheckLoseCondition();
    }

    private void Dispose()
    {
        if (Application.isPlaying) Destroy(gameObject);
        else DestroyImmediate(gameObject);
    }

    private void OnDestroy()
    {
        active.Remove(this);
        if (!finished) ReleasePending();
    }
}
