using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>A short, tinted splash with reusable, scene-owned drop visuals.</summary>
public sealed class WaterSplashEffect : MonoBehaviour
{
    private const int DropCount = 20;
    private const int PrewarmCount = 8;
    public const int PoolCapacityPerScene = 16;
    private static readonly System.Random Variation = new System.Random();
    private static readonly Dictionary<Scene, SplashPool> Pools = new Dictionary<Scene, SplashPool>();
    private static Sprite dropSprite;
    private static float spriteDiameter;

    public static int CreatedInstances { get; private set; }
    public static int ReusedInstances { get; private set; }
    public static int ActiveInstances { get; private set; }

    private sealed class SplashPool
    {
        internal Transform root;
        internal long sequence;
        internal readonly WaterSplashEffect[] instances = new WaterSplashEffect[PoolCapacityPerScene];
    }

    private struct Drop
    {
        internal SpriteRenderer renderer;
        internal Transform visual;
        internal Vector2 position;
        internal Vector2 velocity;
        internal float lifetime;
        internal float size;
        internal Color color;
        internal bool alive;
    }

    private readonly Drop[] drops = new Drop[DropCount];
    private SplashPool pool;
    private int poolIndex = -1;
    private long playOrder;
    private bool prepared;
    private bool playing;
    private bool configured;
    private int sortingLayer;
    private int sortingOrder;
    private bool boardMask;
    private float elapsed;
    private float gravity;
    private float duration;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPools()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        Pools.Clear();
        dropSprite = null;
        spriteDiameter = 0f;
        CreatedInstances = ReusedInstances = ActiveInstances = 0;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!Application.isPlaying || scene.name != "Game" || !EnsureSprite()) return;
        SplashPool scenePool = GetPool(scene);
        for (int i = 0; i < PrewarmCount; i++)
            if (scenePool.instances[i] == null)
                scenePool.instances[i] = Create(scene, scenePool, i);
    }

    private static void OnSceneUnloaded(Scene scene) => Pools.Remove(scene);

    private static bool EnsureSprite()
    {
        if (dropSprite != null) return true;
        dropSprite = Resources.Load<Sprite>("GameScreenSkin/Bullet_Pearl");
        if (dropSprite == null) return false;
        spriteDiameter = Mathf.Max(dropSprite.bounds.size.x, dropSprite.bounds.size.y) * 0.86f;
        return true;
    }

    private static SplashPool GetPool(Scene scene)
    {
        if (Pools.TryGetValue(scene, out SplashPool scenePool) && scenePool.root != null)
            return scenePool;
        var root = new GameObject("Water Splash Pool");
        SceneManager.MoveGameObjectToScene(root, scene);
        scenePool = new SplashPool { root = root.transform };
        Pools[scene] = scenePool;
        return scenePool;
    }

    private static WaterSplashEffect Create(Scene scene, SplashPool owner, int index)
    {
        var root = new GameObject("Colored Water Splash");
        root.SetActive(false);
        if (scene.IsValid()) SceneManager.MoveGameObjectToScene(root, scene);
        if (owner != null) root.transform.SetParent(owner.root, false);
        var splash = root.AddComponent<WaterSplashEffect>();
        splash.pool = owner;
        splash.poolIndex = index;
        splash.PrepareDrops();
        if (owner != null) CreatedInstances++;
        return splash;
    }

    private static WaterSplashEffect Rent(Scene scene)
    {
        SplashPool scenePool = GetPool(scene);
        WaterSplashEffect oldest = null;
        for (int i = 0; i < scenePool.instances.Length; i++)
        {
            WaterSplashEffect splash = scenePool.instances[i];
            // Unity objects can also be removed directly by scene cleanup or Editor probes.
            if (splash == null)
            {
                splash = Create(scene, scenePool, i);
                scenePool.instances[i] = splash;
                splash.playOrder = ++scenePool.sequence;
                return splash;
            }
            if (!splash.playing)
            {
                ReusedInstances++;
                splash.playOrder = ++scenePool.sequence;
                return splash;
            }
            if (oldest == null || splash.playOrder < oldest.playOrder) oldest = splash;
        }
        // Very large impact bursts reuse the oldest visual, keeping rendering work bounded.
        // Damage, ammunition and hit processing are independent of this cosmetic effect.
        ReusedInstances++;
        oldest.playOrder = ++scenePool.sequence;
        return oldest;
    }

    public static void Play(Block block, Vector3 impact, Vector2 direction, Color tint)
    {
        if (block == null || !EnsureSprite()) return;
        var source = block.blockType == BlockType.Bubble ? block.typeIconRenderer : block.spriteRenderer;
        if (source == null) return;
        Scene scene = block.gameObject.scene;
        if (Application.isPlaying && (!scene.IsValid() || !scene.isLoaded)) return;
        GridManager manager = block.GetComponentInParent<GridManager>();
        if (manager != null && manager.IsExiting) return;
        bool clipToBoard = manager != null && BoardViewport.TryGetBounds(manager, out _);
        // Preview regressions own and destroy their fixtures directly; never retain them in a pool.
        WaterSplashEffect splash = Application.isPlaying ? Rent(scene) : Create(scene, null, -1);
        splash.transform.position = new Vector3(impact.x, impact.y, source.transform.position.z - 0.01f);
        splash.Initialize(source, direction, tint, clipToBoard);
        splash.gameObject.SetActive(true);
    }

    private void PrepareDrops()
    {
        if (prepared) return;
        for (int i = 0; i < drops.Length; i++)
        {
            var child = new GameObject("Water Drop");
            var visual = child.transform;
            visual.SetParent(transform, false);
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = dropSprite;
            renderer.enabled = false;
            drops[i].renderer = renderer;
            drops[i].visual = visual;
        }
        prepared = true;
    }


private void Initialize(SpriteRenderer source, Vector2 direction, Color tint, bool clipToBoard)
    {
        PrepareDrops();
        elapsed = duration = 0f;

        float scale = 0.5f;
        gravity = 7.5f * scale;

        tint.a = 1f;
        Color.RGBToHSV(tint, out float hue, out float saturation, out float value);

        if (saturation > 0.12f)
            tint = Color.HSVToRGB(
                hue,
                Mathf.Max(saturation, 0.78f),
                Mathf.Max(value, 0.92f)
            );

        direction = direction.sqrMagnitude > 0.001f
            ? direction.normalized
            : Vector2.up;

        int layer = source.sortingLayerID;
        int order = source.sortingOrder + 2;

        bool updateSorting =
            !configured ||
            layer != sortingLayer ||
            order != sortingOrder;

        bool updateMask =
            !configured ||
            boardMask != clipToBoard;

        for (int i = 0; i < drops.Length; i++)
        {
            ref Drop drop = ref drops[i];

            if (updateSorting)
            {
                drop.renderer.sortingLayerID = layer;
                drop.renderer.sortingOrder = order;
            }

            if (updateMask)
            {
                drop.renderer.maskInteraction =
                    clipToBoard
                        ? SpriteMaskInteraction.VisibleInsideMask
                        : SpriteMaskInteraction.None;
            }

            float angle =
                (i + Range(-0.35f, 0.35f)) *
                Mathf.PI * 2f /
                drops.Length;

            Vector2 outward = new Vector2(
                Mathf.Cos(angle),
                Mathf.Sin(angle)
            );

            bool brightHighlight = i % 5 == 0;

            drop.color = Color.Lerp(
                tint,
                Color.white,
                brightHighlight
                    ? Range(0.42f, 0.62f)
                    : Range(0.08f, 0.24f)
            );

            drop.lifetime = Range(0.42f, 0.68f);

            bool largeDrop = i % 4 == 0;

            float diameter = largeDrop
                ? Range(0.22f, 0.34f)
                : Range(0.11f, 0.22f);

            drop.size =
                diameter *
                scale /
                spriteDiameter;

            drop.velocity =
                (
                    outward * Range(1.6f, 3.8f)
                    - direction * 0.55f
                    + Vector2.up * Range(0.35f, 0.85f)
                ) * scale;

            drop.position = Vector2.zero;

            drop.visual.SetLocalPositionAndRotation(
                Vector3.zero,
                Quaternion.identity
            );

            drop.visual.localScale =
                new Vector3(
                    drop.size,
                    drop.size * 1.55f,
                    1f
                );

            drop.renderer.color = drop.color;

            if (!drop.alive)
                drop.renderer.enabled = true;

            drop.alive = true;

            duration = Mathf.Max(
                duration,
                drop.lifetime
            );
        }

        sortingLayer = layer;
        sortingOrder = order;
        boardMask = clipToBoard;
        configured = true;

        if (!playing && pool != null)
            ActiveInstances++;

        playing = true;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (!playing || dt <= 0f) return;
        elapsed += dt;
        if (elapsed >= duration)
        {
            if (pool != null) gameObject.SetActive(false);
            else if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
            return;
        }

        for (int i = 0; i < drops.Length; i++)
        {
            ref Drop drop = ref drops[i];
            if (!drop.alive) continue;
            float progress = elapsed / drop.lifetime;
            if (progress >= 1f)
            {
                drop.alive = false;
                drop.renderer.enabled = false;
                continue;
            }
            drop.velocity.y -= gravity * dt;
            drop.position += drop.velocity * dt;
            drop.visual.SetLocalPositionAndRotation(drop.position,
                Quaternion.Euler(0f, 0f, Mathf.Atan2(drop.velocity.y, drop.velocity.x) * Mathf.Rad2Deg - 90f));
            float size = drop.size * Mathf.Lerp(1f, 0.48f, progress);
            drop.visual.localScale = new Vector3(size, size * 1.55f, 1f);
            Color color = drop.color;
            color.a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 0.98f, progress));
            drop.renderer.color = color;
        }
    }

    private void OnDisable()
    {
        if (playing && pool != null) ActiveInstances = Mathf.Max(0, ActiveInstances - 1);
        playing = false;
    }

    private void OnDestroy()
    {
        if (pool != null && poolIndex >= 0 && ReferenceEquals(pool.instances[poolIndex], this))
            pool.instances[poolIndex] = null;
    }

    private static float Range(float min, float max) => min + (float)Variation.NextDouble() * (max - min);
}
