using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Sprites;
using UnityEngine.UI;

/// <summary>Fills the space beside the content viewport with opaque black padding.</summary>
[DefaultExecutionOrder(-9000)]
public sealed class SceneBackgroundExtension : MonoBehaviour
{
    private const string RootName = "Scene Background Extension";
    [SerializeField] private Canvas extensionCanvas;
    [SerializeField] private RawImage left;
    [SerializeField] private RawImage right;
    [SerializeField] private Image leftClear;
    [SerializeField] private Image rightClear;
    [SerializeField] private Camera sceneCamera;
    [SerializeField] private Camera previewCamera;
    [SerializeField] private Image imageSource;
    [SerializeField] private SpriteRenderer spriteSource;
    [SerializeField] private Vector2 screenSize;
    [SerializeField] private Rect viewport;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RemoveOrphanedExtensions()
    {
        // Find native canvases too, including overlays left by earlier script versions.
        foreach (Canvas canvas in Resources.FindObjectsOfTypeAll<Canvas>())
        {
            if (canvas == null || !canvas.isRootCanvas || canvas.name != RootName) continue;
#if UNITY_EDITOR
            if (UnityEditor.EditorUtility.IsPersistent(canvas)) continue;
#endif
            Scene owner = canvas.gameObject.scene;
            if (owner.IsValid() && owner.isLoaded) continue;
            canvas.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(canvas.gameObject);
            else DestroyImmediate(canvas.gameObject);
        }
    }

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    private static void ScheduleOrphanCleanup() => UnityEditor.EditorApplication.delayCall += RemoveOrphanedExtensions;
#endif

    public static void Apply(Scene scene, Camera camera, Vector2 screenSize, Rect viewport)
    {
        if (!scene.IsValid() || !scene.isLoaded || camera == null) return;
        SceneBackgroundExtension extension = Find(scene);
        bool hasSides = viewport.xMin > .0001f || viewport.xMax < .9999f;
        if (!hasSides)
        {
            if (extension != null) extension.gameObject.SetActive(false);
            return;
        }
        if (extension == null) extension = Create(scene);
        extension.sceneCamera = camera;
        extension.screenSize = Vector2.Max(Vector2.one, screenSize);
        extension.viewport = viewport;
        extension.SelectSource(scene);
        extension.gameObject.SetActive(true);
        extension.extensionCanvas.enabled = !Application.isPlaying || scene == SceneManager.GetActiveScene();
#if UNITY_EDITOR
        // Overlay canvases render over every scene, including the live Game View.
        // Preview padding is enabled only while explicitly compositing its image.
        if (UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(scene))
            extension.extensionCanvas.enabled = false;
#endif
        extension.GetComponent<GraphicRaycaster>().enabled = extension.extensionCanvas.enabled;
        extension.SetStripBounds(extension.leftClear.rectTransform, extension.left.rectTransform,
            Vector2.zero, new Vector2(Mathf.Clamp01(viewport.xMin), 1f));
        extension.SetStripBounds(extension.rightClear.rectTransform, extension.right.rectTransform,
            new Vector2(Mathf.Clamp01(viewport.xMax), 0f), Vector2.one);
        extension.UpdateSamples();
    }

    private static SceneBackgroundExtension Find(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            SceneBackgroundExtension extension = root.GetComponent<SceneBackgroundExtension>();
            if (extension != null) return extension;
        }
        return null;
    }

    private static SceneBackgroundExtension Create(Scene scene)
    {
        int uiLayer = LayerMask.NameToLayer("UI");
        var root = new GameObject(RootName, typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        // Keep scene ownership so closing a preview or unloading a scene destroys this canvas.
        root.layer = uiLayer >= 0 ? uiLayer : 0;
        root.SetActive(false);
        SceneManager.MoveGameObjectToScene(root, scene);
        var extension = root.AddComponent<SceneBackgroundExtension>();
        extension.extensionCanvas = root.GetComponent<Canvas>();
        extension.extensionCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        extension.extensionCanvas.sortingOrder = short.MaxValue;
        extension.leftClear = extension.CreateClear("Left Scene Clear Color");
        extension.left = extension.CreateStrip("Left Scene Background");
        extension.rightClear = extension.CreateClear("Right Scene Clear Color");
        extension.right = extension.CreateStrip("Right Scene Background");
        return extension;
    }

    private Image CreateClear(string name)
    {
        var child = new GameObject(name, typeof(RectTransform), typeof(Image));
        child.layer = gameObject.layer;
        child.transform.SetParent(transform, false);
        var image = child.GetComponent<Image>();
        image.raycastTarget = false;
        child.AddComponent<BackgroundEdgeOverlap>().extendRight = name.StartsWith("Left");
        return image;
    }

    private RawImage CreateStrip(string name)
    {
        var child = new GameObject(name, typeof(RectTransform), typeof(RawImage));
        child.layer = gameObject.layer;
        child.transform.SetParent(transform, false);
        var image = child.GetComponent<RawImage>();
        image.raycastTarget = true;
        child.AddComponent<BackgroundEdgeOverlap>().extendRight = name.StartsWith("Left");
        return image;
    }

    private void SetStripBounds(RectTransform backing, RectTransform strip, Vector2 min, Vector2 max)
    {
        foreach (RectTransform rect in new[] { backing, strip })
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.gameObject.SetActive(max.x - min.x > .0001f);
        }
    }

    private void SelectSource(Scene scene)
    {
        imageSource = null;
        spriteSource = null;
        int best = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root == gameObject) continue;
            foreach (Image image in root.GetComponentsInChildren<Image>(true))
            {
                if (!image.isActiveAndEnabled || image.sprite == null) continue;
                bool canvasChild = image.transform.parent != null && image.transform.parent.GetComponent<Canvas>() != null;
                int score = image.name == "Robot Background" ? 100
                    : canvasChild && (image.name == "BG" || image.name == "Background") ? 80
                    : scene.name == "Start" && canvasChild && (image.name == "Panel" || image.name == "Image") ? 90 : 0;
                if (score <= best) continue;
                best = score;
                imageSource = image;
            }
        }
        if (imageSource != null) return;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
                if (renderer.enabled && renderer.gameObject.activeInHierarchy)
                    if (renderer.sprite != null && (renderer.name == "BG" || renderer.name == "Background") && renderer.transform.parent == null)
                    {
                        spriteSource = renderer;
                        return;
                    }
    }

    private void LateUpdate()
    {
        if (extensionCanvas != null && extensionCanvas.enabled) UpdateSamples();
    }

    private void UpdateSamples()
    {
        if (sceneCamera == null || left == null || right == null) return;
        Color clear = Color.black;
        leftClear.color = rightClear.color = clear;
        Sprite sprite = imageSource != null
            ? (imageSource.overrideSprite != null ? imageSource.overrideSprite : imageSource.sprite)
            : spriteSource != null ? spriteSource.sprite : null;
        if (sprite == null)
        {
            left.texture = right.texture = Texture2D.whiteTexture;
            left.color = right.color = clear;
            return;
        }
        left.texture = right.texture = sprite.texture;
        left.color = right.color = Color.black;
        float inset = .5f / Mathf.Max(1f, screenSize.x * viewport.width);
        SetSample(left, sprite, inset);
        SetSample(right, sprite, 1f - inset);
    }

    private void SetSample(RawImage strip, Sprite sprite, float x)
    {
        Vector2 bottom = SampleUv(sprite, new Vector2(x, 0f));
        Vector2 top = SampleUv(sprite, new Vector2(x, 1f));
        strip.uvRect = new Rect(bottom.x, bottom.y, 0f, top.y - bottom.y);
    }

    private Vector2 SampleUv(Sprite sprite, Vector2 point)
    {
        Vector4 outer = DataUtility.GetOuterUV(sprite);
        Vector2 normalized;
        if (imageSource != null)
        {
            RectTransform rect = imageSource.rectTransform;
            Canvas canvas = imageSource.canvas;
            Camera uiCamera = canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null : canvas != null && canvas.worldCamera != null ? canvas.worldCamera : sceneCamera;
            Vector2 screenPoint = sceneCamera.ViewportToScreenPoint(point);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPoint, uiCamera, out Vector2 local))
                return new Vector2(outer.x, outer.y);
            Rect bounds = imageSource.GetPixelAdjustedRect();
            if (bounds.width <= 0f || bounds.height <= 0f) return new Vector2(outer.x, outer.y);
            normalized = new Vector2((local.x - bounds.xMin) / bounds.width, (local.y - bounds.yMin) / bounds.height);
            BackgroundCover cover = imageSource.GetComponent<BackgroundCover>();
            if (cover != null && cover.isActiveAndEnabled)
            {
                float imageAspect = sprite.rect.width / sprite.rect.height;
                float rectAspect = bounds.width / bounds.height;
                Vector2 crop = rectAspect > imageAspect
                    ? new Vector2(1f, imageAspect / rectAspect) : new Vector2(rectAspect / imageAspect, 1f);
                normalized = Vector2.one * .5f + Vector2.Scale(normalized - Vector2.one * .5f, crop);
            }
        }
        else
        {
            Ray ray = sceneCamera.ViewportPointToRay(point);
            var plane = new Plane(spriteSource.transform.forward, spriteSource.transform.position);
            if (!plane.Raycast(ray, out float distance)) return new Vector2(outer.x, outer.y);
            Vector3 local = spriteSource.transform.InverseTransformPoint(ray.GetPoint(distance));
            Bounds bounds = sprite.bounds;
            normalized = new Vector2((local.x - bounds.min.x) / bounds.size.x, (local.y - bounds.min.y) / bounds.size.y);
            if (spriteSource.flipX) normalized.x = 1f - normalized.x;
            if (spriteSource.flipY) normalized.y = 1f - normalized.y;
        }
        return new Vector2(Mathf.LerpUnclamped(outer.x, outer.z, normalized.x), Mathf.LerpUnclamped(outer.y, outer.w, normalized.y));
    }

    private static Color ImageTint(Image image)
    {
        Color color = image.color;
        for (Transform node = image.transform; node != null; node = node.parent)
        {
            foreach (CanvasGroup group in node.GetComponents<CanvasGroup>())
            {
                if (!group.isActiveAndEnabled) continue;
                color.a *= group.alpha;
                if (group.ignoreParentGroups) return color;
            }
        }
        return color;
    }

    /// <summary>Composites only the two side strips into a preview camera's existing target.</summary>
    public static void RenderPreview(Scene scene)
    {
        SceneBackgroundExtension extension = Find(scene);
        if (extension == null || !extension.gameObject.activeInHierarchy ||
            extension.sceneCamera == null || extension.sceneCamera.targetTexture == null) return;
        extension.UpdateSamples();
        if (extension.previewCamera == null)
        {
            var child = new GameObject("Scene Edge Preview Camera", typeof(Camera));
            child.layer = extension.gameObject.layer;
            SceneManager.MoveGameObjectToScene(child, scene);
            extension.previewCamera = child.GetComponent<Camera>();
            extension.previewCamera.enabled = false;
        }
        Camera camera = extension.previewCamera;
        // Keep the camera outside its own Canvas hierarchy: camera canvases follow
        // their camera, so parenting this camera beneath one would create a cycle.
        if (camera.transform.parent != null) camera.transform.SetParent(null, true);
        Canvas overlay = extension.extensionCanvas;
        bool oldEnabled = overlay.enabled;
        RenderMode oldMode = overlay.renderMode;
        Camera oldCamera = overlay.worldCamera;
        float oldDistance = overlay.planeDistance;
        try
        {
            // Render the auxiliary UI on a distant plane. Existing scene geometry
            // remains behind this camera without toggling any Canvas/TMP components.
            camera.transform.SetPositionAndRotation(new Vector3(0f, 0f, 10000f), Quaternion.identity);
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 10f;
            camera.orthographic = true;
            camera.orthographicSize = extension.screenSize.y * .5f;
            camera.rect = new Rect(0f, 0f, 1f, 1f);
            camera.aspect = extension.screenSize.x / extension.screenSize.y;
            camera.clearFlags = CameraClearFlags.Depth;
            camera.cullingMask = 1 << extension.gameObject.layer;
            camera.targetTexture = extension.sceneCamera.targetTexture;
            overlay.enabled = true;
            overlay.renderMode = RenderMode.ScreenSpaceCamera;
            overlay.worldCamera = camera;
            overlay.planeDistance = 1f;
            Canvas.ForceUpdateCanvases();
#if UNITY_EDITOR
            camera.scene = scene;
#endif
            camera.Render();
        }
        finally
        {
            camera.targetTexture = null;
            overlay.renderMode = oldMode;
            overlay.worldCamera = oldCamera;
            overlay.planeDistance = oldDistance;
            overlay.enabled = oldEnabled;
            Canvas.ForceUpdateCanvases();
        }
    }

    private void OnDestroy()
    {
        if (previewCamera == null) return;
        if (Application.isPlaying) Destroy(previewCamera.gameObject);
        else DestroyImmediate(previewCamera.gameObject);
    }
}
