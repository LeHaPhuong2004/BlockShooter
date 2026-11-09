using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Fits narrow phones without moving authored gameplay objects or UI controls.</summary>
public static class ResponsiveGameplayLayout
{
    private sealed class CameraLayout
    {
        public float size;
        public bool gameplay;
    }

    private static ConditionalWeakTable<Camera, CameraLayout> cameras = new ConditionalWeakTable<Camera, CameraLayout>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetLayouts() => cameras = new ConditionalWeakTable<Camera, CameraLayout>();

    public static void Apply(Scene scene, Camera camera, Vector2 screenSize)
    {
        if (camera == null || !camera.orthographic || !scene.IsValid() || !scene.isLoaded) return;
        if (!cameras.TryGetValue(camera, out CameraLayout layout))
        {
            bool gameplay = false;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == "Tray" || root.GetComponentInChildren<GridManager>(true) != null || root.GetComponentInChildren<GunGridManager>(true) != null)
                    gameplay = true;
            cameras.Add(camera, layout = new CameraLayout { size = camera.orthographicSize, gameplay = gameplay });
        }
        if (!layout.gameplay) return;
        Vector2 reference = PortraitDisplay.ReferenceResolutionForScene(scene);
        // The display policy already chooses the content camera's viewport/aspect.
        // Keep the authored board visible on tall phones while leaving tablet framing alone.
        camera.orthographicSize = layout.size * Mathf.Max(1f, reference.x / reference.y / Mathf.Max(.0001f, camera.aspect));
    }
}
