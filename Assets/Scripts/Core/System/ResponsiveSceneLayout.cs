using System.Runtime.CompilerServices;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Extends scene backgrounds and anchored content to the available display.</summary>
public static class ResponsiveSceneLayout
{
    private sealed class BackgroundState
    {
        public Vector3 scale;
    }

    private static ConditionalWeakTable<SpriteRenderer, BackgroundState> backgrounds =
        new ConditionalWeakTable<SpriteRenderer, BackgroundState>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => backgrounds = new ConditionalWeakTable<SpriteRenderer, BackgroundState>();

    public static void Apply(Scene scene, Camera camera, Vector2 screenSize)
    {
        if (!scene.IsValid() || !scene.isLoaded || camera == null) return;
        screenSize = Vector2.Max(Vector2.one, screenSize);
        Rect viewport = PortraitDisplay.CalculateViewport(screenSize, PortraitDisplay.ReferenceScreenSize);
        Vector2 contentSize = Vector2.Scale(screenSize, viewport.size);
        ResponsiveGameplayLayout.Apply(scene, camera, contentSize);
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.GetComponent<SceneBackgroundExtension>() != null) continue;
            foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
            {
                if (canvas.name == "Canvas" && canvas.isRootCanvas &&
                    (scene.name == "Game" || scene.name == "Game 1"))
                    PreserveGameForeground(canvas);
                else if (canvas.name == "Home Canvas" && canvas.isRootCanvas && scene.name == "Home")
                    PreserveHomeForeground(canvas);
            }
            foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
                if (renderer.sprite != null && (renderer.name == "BG" || renderer.name == "Background") &&
                    renderer.transform.parent == null)
                    CoverWorldBackground(renderer, camera, contentSize);

            foreach (Image image in root.GetComponentsInChildren<Image>(true))
                if (IsScreenBackground(image, scene.name)) CoverImage(image);

            foreach (HomeExperienceUI home in root.GetComponentsInChildren<HomeExperienceUI>(true))
                home.ApplyResponsiveLayout(contentSize);
        }
        Canvas.ForceUpdateCanvases();
        SceneBackgroundExtension.Apply(scene, camera, screenSize, viewport);
    }

    private static void PreserveHomeForeground(Canvas canvas)
    {
        var home = canvas.transform.Find("Home") as RectTransform;
        if (home == null) return;
        var content = home.Find("Home Content") as RectTransform;
        if (content == null)
        {
            content = new GameObject("Home Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(home, false);
            content.anchorMin = new Vector2(.5f, 0f);
            content.anchorMax = new Vector2(.5f, 1f);
            content.sizeDelta = new Vector2(1920f * PortraitDisplay.ReferenceScreenSize.x /
                PortraitDisplay.ReferenceScreenSize.y, 0f);
            content.anchoredPosition = Vector2.zero;
        }
        var children = new Transform[home.childCount];
        for (int i = 0; i < children.Length; i++) children[i] = home.GetChild(i);
        foreach (Transform child in children)
        {
            if (child == content) continue;
            if (child.name == "Robot Background")
            {
                var foreground = new Transform[child.childCount];
                for (int i = 0; i < foreground.Length; i++) foreground[i] = child.GetChild(i);
                foreach (Transform artwork in foreground)
                    if (artwork.name == "Floating Robot Character" || artwork.name == "Robot Contact Shadows")
                        ReparentAuthored(artwork, content);
                continue;
            }
            ReparentAuthored(child, content);
        }
    }

    private static void ReparentAuthored(Transform child, Transform parent)
    {
        if (child is RectTransform rect)
        {
            Vector2 min = rect.anchorMin, max = rect.anchorMax, pivot = rect.pivot, size = rect.sizeDelta;
            Vector3 position = rect.anchoredPosition3D, scale = rect.localScale;
            Quaternion rotation = rect.localRotation;
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition3D = position;
            rect.localScale = scale;
            rect.localRotation = rotation;
        }
        else child.SetParent(parent, false);
    }

    private static void PreserveGameForeground(Canvas canvas)
    {
        var content = canvas.transform.Find("Game Content") as RectTransform;
        if (content == null)
        {
            var children = new Transform[canvas.transform.childCount];
            for (int i = 0; i < children.Length; i++) children[i] = canvas.transform.GetChild(i);
            content = new GameObject("Game Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(canvas.transform, false);
            content.anchorMin = new Vector2(.5f, 0f);
            content.anchorMax = new Vector2(.5f, 1f);
            content.sizeDelta = new Vector2(1080f, 0f);
            content.anchoredPosition = Vector2.zero;
            foreach (Transform child in children)
            {
                if (child is RectTransform rect)
                {
                    Vector2 min = rect.anchorMin, max = rect.anchorMax, pivot = rect.pivot, size = rect.sizeDelta;
                    Vector3 position = rect.anchoredPosition3D, scale = rect.localScale;
                    Quaternion rotation = rect.localRotation;
                    rect.SetParent(content, false);
                    rect.anchorMin = min;
                    rect.anchorMax = max;
                    rect.pivot = pivot;
                    rect.sizeDelta = size;
                    rect.anchoredPosition3D = position;
                    rect.localScale = scale;
                    rect.localRotation = rotation;
                }
                else child.SetParent(content, false);
            }
        }
        // HUD and booster labels are decorative, including world-space TMP counters.
        foreach (TMP_Text label in content.GetComponentsInChildren<TMP_Text>(true))
            label.raycastTarget = false;
    }

    private static bool IsScreenBackground(Image image, string sceneName)
    {
        if (image.sprite == null) return false;
        if (image.name == "Robot Background") return true;
        if (image.transform.parent == null || image.transform.parent.GetComponent<Canvas>() == null) return false;
        return image.name == "BG" || image.name == "Background" ||
            (sceneName == "Start" && (image.name == "Panel" || image.name == "Image"));
    }

    public static void CoverImage(Image image)
    {
        if (image == null || image.sprite == null) return;
        var fitter = image.GetComponent<AspectRatioFitter>();
        if (fitter != null) fitter.enabled = false;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.raycastTarget = false;
        if (image.GetComponent<BackgroundCover>() == null) image.gameObject.AddComponent<BackgroundCover>();
        image.SetVerticesDirty();
    }

    private static void CoverWorldBackground(SpriteRenderer renderer, Camera camera, Vector2 screenSize)
    {
        if (!camera.orthographic) return;
        if (!backgrounds.TryGetValue(renderer, out BackgroundState state))
        {
            state = new BackgroundState { scale = renderer.transform.localScale };
            backgrounds.Add(renderer, state);
        }
        renderer.transform.localScale = state.scale;
        Bounds bounds = renderer.bounds;
        if (bounds.size.x <= 0f || bounds.size.y <= 0f) return;
        float height = camera.orthographicSize * 2f;
        float width = height * screenSize.x / screenSize.y;
        float multiplier = Mathf.Max(1f, Mathf.Max(width / bounds.size.x, height / bounds.size.y));
        renderer.transform.localScale = Vector3.Scale(state.scale, new Vector3(multiplier, multiplier, 1f));
        bounds = renderer.bounds;
        renderer.transform.position += new Vector3(camera.transform.position.x - bounds.center.x,
            camera.transform.position.y - bounds.center.y, 0f);
    }
}
