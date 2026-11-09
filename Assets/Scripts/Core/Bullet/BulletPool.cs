using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Scene-owned projectile storage; a retired shot keeps its trail until it fades.</summary>
public sealed class BulletPool : MonoBehaviour
{
    private const int MaxRetainedPerPrefab = 48;
    private static BulletPool current;
    private readonly Dictionary<Bullet, Stack<Bullet>> available = new Dictionary<Bullet, Stack<Bullet>>();
    private readonly List<Retiring> retiring = new List<Retiring>(32);
    private bool exiting;
    private struct Retiring { public Bullet bullet; public Bullet prefab; public float readyAt; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => current = null;

    internal static Bullet Rent(Bullet prefab, Vector3 position, Quaternion rotation)
    {
        Scene scene = SceneManager.GetActiveScene();
        if (current == null || current.gameObject.scene != scene || current.exiting)
        {
            var root = new GameObject("Projectile Pool");
            SceneManager.MoveGameObjectToScene(root, scene);
            current = root.AddComponent<BulletPool>();
        }
        BulletPool pool = current;
        if (!pool.available.TryGetValue(prefab, out Stack<Bullet> stack))
        {
            stack = new Stack<Bullet>(16);
            pool.available.Add(prefab, stack);
        }
        Bullet bullet = null;
        while (stack.Count > 0 && bullet == null) bullet = stack.Pop();
        if (bullet == null)
        {
            bullet = Instantiate(prefab, position, rotation, pool.transform);
            bullet.SetPool(pool, prefab);
        }
        else
        {
            bullet.transform.SetPositionAndRotation(position, rotation);
            bullet.gameObject.SetActive(true);
        }
        bullet.enabled = true;
        return bullet;
    }

    internal bool Retire(Bullet bullet, Bullet prefab, float delay)
    {
        if (exiting || !isActiveAndEnabled || !gameObject.scene.isLoaded) return false;
        retiring.Add(new Retiring { bullet = bullet, prefab = prefab, readyAt = Time.time + delay });
        return true;
    }

    private void Update()
    {
        if (Time.deltaTime <= 0f || exiting) return;
        for (int i = retiring.Count - 1; i >= 0; i--)
        {
            Retiring entry = retiring[i];
            if (entry.bullet != null && Time.time < entry.readyAt) continue;
            retiring[i] = retiring[retiring.Count - 1];
            retiring.RemoveAt(retiring.Count - 1);
            if (entry.bullet == null) continue;
            if (entry.prefab == null || !available.TryGetValue(entry.prefab, out Stack<Bullet> stack) || stack.Count >= MaxRetainedPerPrefab)
            { Destroy(entry.bullet.gameObject); continue; }
            entry.bullet.gameObject.SetActive(false);
            entry.bullet.transform.SetParent(transform, false);
            stack.Push(entry.bullet);
        }
    }

    private void OnDisable() { exiting = true; }
    private void OnDestroy()
    {
        exiting = true;
        retiring.Clear(); available.Clear();
        if (current == this) current = null;
    }
}
