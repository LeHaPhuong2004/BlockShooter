using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Expands through portrait iPad proportions and caps wider displays with black padding.</summary>
[DefaultExecutionOrder(-10000)]
public sealed class PortraitDisplay : MonoBehaviour
{
    private static PortraitDisplay instance;
    private static readonly Dictionary<Canvas, int> overlaySortingOrders = new Dictionary<Canvas, int>();
    private readonly List<Canvas> canvases = new List<Canvas>();
    private readonly List<Camera> cameras = new List<Camera>();
    private Vector2 lastScreenSize;

    public static Vector2 ReferenceScreenSize => new Vector2(1170f, 2532f);
    public const float MaximumContentAspect = 3f / 4f;

    // Keep phone-authored dimensions as the height reference; wider displays gain logical width.
    public static Vector2 ReferenceResolutionForScene(Scene scene) =>
        new Vector2(1080f, 1080f * ReferenceScreenSize.y / ReferenceScreenSize.x);

    public static float ContentAspect => ContentPixelSize.x / ContentPixelSize.y;

    public static Vector2 ContentPixelSize
    {
        get
        {
            Vector2 screen = new Vector2(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));
            return Vector2.Scale(screen, CalculateViewport(screen, ReferenceScreenSize).size);
        }
    }

    public static Rect CalculateViewport(Vector2 screenSize, Vector2 referenceSize)
    {
        screenSize = Vector2.Max(Vector2.one, screenSize);
        float width = Mathf.Min(1f, screenSize.y * MaximumContentAspect / screenSize.x);
        return new Rect((1f - width) * .5f, 0f, width, 1f);
    }

    public static void LockPortraitOrientation()
    {
        Screen.autorotateToPortrait = true;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = false;
        Screen.autorotateToLandscapeRight = false;
        Screen.orientation = ScreenOrientation.Portrait;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        overlaySortingOrders.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        LockPortraitOrientation();
        if (instance != null)
        {
            instance.InitializeRuntime();
            return;
        }
        // Reuse an existing runtime manager when entering Play with scene/domain reload disabled.
        foreach (PortraitDisplay display in Resources.FindObjectsOfTypeAll<PortraitDisplay>())
        {
            if (display == null || !display.gameObject.scene.IsValid() || !display.gameObject.scene.isLoaded) continue;
            instance = display;
            display.enabled = true;
            display.InitializeRuntime();
            return;
        }
        new GameObject("Portrait Display").AddComponent<PortraitDisplay>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        if (!Application.isPlaying || (instance != null && instance != this)) return;
        instance = this;
        InitializeRuntime();
    }

    private void InitializeRuntime()
    {
        if (!Application.isPlaying) return;
        DontDestroyOnLoad(gameObject);
        RemoveLegacyPadding();
        // Remove first so script reload and repeated initialization cannot duplicate subscriptions.
        UnsubscribeSceneEvents();
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
        for (int i = 0; i < SceneManager.sceneCount; i++)
            RegisterScene(SceneManager.GetSceneAt(i));
        RefreshLayout();
    }

    private static void RemoveLegacyPadding()
    {
        foreach (Canvas canvas in Resources.FindObjectsOfTypeAll<Canvas>())
        {
            if (canvas == null || canvas.name != "Portrait Background" ||
                !canvas.gameObject.scene.IsValid() || !canvas.gameObject.scene.isLoaded) continue;
            // Hide immediately, including its raycaster, while Unity defers destruction to frame end.
            foreach (GraphicRaycaster raycaster in canvas.GetComponentsInChildren<GraphicRaycaster>(true))
                raycaster.enabled = false;
            foreach (Graphic graphic in canvas.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
            canvas.gameObject.SetActive(false);
            Destroy(canvas.gameObject);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => RefreshScene(scene);

    private void OnActiveSceneChanged(Scene previous, Scene next) => RefreshScene(next);

    private void OnSceneUnloaded(Scene scene)
    {
        PruneSceneReferences();
        RefreshLayout();
    }

    public static void RefreshScene(Scene scene)
    {
        if (instance == null || !scene.IsValid() || !scene.isLoaded) return;
        instance.RegisterScene(scene);
        instance.RefreshLayout();
    }

    private void RegisterScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
            {
                if (!camera.enabled || !camera.gameObject.activeInHierarchy || camera.targetTexture != null) continue;
                if (!cameras.Contains(camera)) cameras.Add(camera);
            }
            foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
                ConfigureCanvas(canvas);
        }
    }

    public static void ConfigureCanvas(Canvas canvas)
    {
        if (instance == null || canvas == null || canvas.GetComponentInParent<SceneBackgroundExtension>() != null || !canvas.isRootCanvas ||
            canvas.renderMode == RenderMode.WorldSpace || canvas.transform.IsChildOf(instance.transform)) return;
        Camera camera = FindSceneCamera(canvas.gameObject.scene);
        if (camera == null) return;
        if (!instance.cameras.Contains(camera)) instance.cameras.Add(camera);
        if (!instance.canvases.Contains(canvas)) instance.canvases.Add(canvas);
        ApplyCanvasLayout(canvas, camera, ReferenceResolutionForScene(canvas.gameObject.scene),
            new Vector2(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height)));
    }

    public static void ApplyCanvasLayout(Canvas canvas, Camera camera, Vector2 referenceSize, Vector2 screenSize)
    {
        if (canvas == null || camera == null || canvas.GetComponentInParent<SceneBackgroundExtension>() != null) return;
        referenceSize = Vector2.Max(Vector2.one, referenceSize);
        screenSize = Vector2.Max(Vector2.one, screenSize);
        ApplyCameraLayout(camera, referenceSize, screenSize);
        // Preserve overlay precedence exactly once when converting to a camera canvas.
        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            if (!overlaySortingOrders.TryGetValue(canvas, out int authoredOrder))
            {
                authoredOrder = canvas.sortingOrder;
                overlaySortingOrders.Add(canvas, authoredOrder);
            }
            canvas.sortingOrder = Mathf.Min(32760, authoredOrder + 10000);
        }
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = Mathf.Clamp(1f, camera.nearClipPlane + 0.01f, camera.farClipPlane - 0.01f);
        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            if (canvas.name == "Home Canvas")
            {
                scaler.referenceResolution = new Vector2(1080f, 1920f);
                scaler.matchWidthOrHeight = 1f;
            }
        }
        // Home retains its authored 1080x1920 height and scaler metadata. All other
        // root canvases retain the logical iPhone reference used by their authored UI.
        Vector2 layoutReference = canvas.name == "Home Canvas"
            ? Vector2.Max(Vector2.one, scaler.referenceResolution) : referenceSize;
        if (canvas.name != "Home Canvas") scaler.referenceResolution = referenceSize;
        Vector2 contentSize = Vector2.Scale(screenSize, CalculateViewport(screenSize, referenceSize).size);
        float scale = contentSize.y / layoutReference.y;
        if (canvas.name == "Canvas" && (canvas.gameObject.scene.name == "Game" || canvas.gameObject.scene.name == "Game 1"))
            scale = Mathf.Min(scale, contentSize.x / layoutReference.x);
        else if (canvas.name == "Home Canvas")
            scale = Mathf.Min(scale, contentSize.x / (layoutReference.y * ReferenceScreenSize.x / ReferenceScreenSize.y));
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = scale;
        canvas.scaleFactor = scale;
    }

    private static void ApplyCameraLayout(Camera camera, Vector2 referenceSize, Vector2 screenSize)
    {
        camera.rect = CalculateViewport(screenSize, referenceSize);
        Vector2 contentSize = Vector2.Scale(screenSize, camera.rect.size);
        camera.aspect = contentSize.x / contentSize.y;
    }

    private static Camera FindSceneCamera(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return null;
        Camera fallback = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
            {
                if (!camera.enabled || !camera.gameObject.activeInHierarchy || camera.targetTexture != null) continue;
                if (camera.CompareTag("MainCamera")) return camera;
                if (fallback == null) fallback = camera;
            }
        }
        return fallback;
    }

    private void LateUpdate()
    {
        if (new Vector2(Screen.width, Screen.height) != lastScreenSize) RefreshLayout();
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused) return;
        LockPortraitOrientation();
        RefreshLayout();
    }

    private void RefreshLayout()
    {
        lastScreenSize = new Vector2(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));
        PruneSceneReferences();
        foreach (Camera camera in cameras)
            ApplyCameraLayout(camera, ReferenceResolutionForScene(camera.gameObject.scene), lastScreenSize);
        foreach (Canvas canvas in canvases)
        {
            if (canvas.renderMode == RenderMode.WorldSpace || !canvas.isRootCanvas ||
                canvas.GetComponentInParent<SceneBackgroundExtension>() != null) continue;
            Camera camera = canvas.worldCamera != null ? canvas.worldCamera : FindSceneCamera(canvas.gameObject.scene);
            if (camera != null)
                ApplyCanvasLayout(canvas, camera, ReferenceResolutionForScene(canvas.gameObject.scene), lastScreenSize);
        }
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            Camera camera = FindSceneCamera(scene);
            if (camera != null) ResponsiveSceneLayout.Apply(scene, camera, lastScreenSize);
        }
    }

    private void PruneSceneReferences()
    {
        cameras.RemoveAll(camera => camera == null || !camera.gameObject.scene.IsValid() || !camera.gameObject.scene.isLoaded);
        canvases.RemoveAll(canvas => canvas == null || !canvas.gameObject.scene.IsValid() || !canvas.gameObject.scene.isLoaded);
        var staleOverlays = new List<Canvas>();
        foreach (Canvas canvas in overlaySortingOrders.Keys)
            if (canvas == null || !canvas.gameObject.scene.IsValid() || !canvas.gameObject.scene.isLoaded)
                staleOverlays.Add(canvas);
        foreach (Canvas canvas in staleOverlays) overlaySortingOrders.Remove(canvas);
    }

    private void UnsubscribeSceneEvents()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    private void OnDisable()
    {
        UnsubscribeSceneEvents();
        if (instance == this) instance = null;
    }

    private void OnDestroy()
    {
        UnsubscribeSceneEvents();
        if (instance == this) instance = null;
    }
}
