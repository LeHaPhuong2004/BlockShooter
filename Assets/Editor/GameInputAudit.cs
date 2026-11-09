#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class GameInputAudit
{
    private const string Request = "Temp/GameInputAudit.request";
    private const string Result = "Temp/GameInputAudit.result";
    private static bool running;
    static GameInputAudit() { EditorApplication.update += Poll; }
    private static void Poll()
    {
        if (running || !File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        running = true;
        try { Run(); }
        catch (Exception exception) { Debug.LogException(exception); }
        finally { File.Delete(Request); running = false; }
    }
    [MenuItem("Tools/Diagnostics/Audit Game Input")]
    public static void Run()
    {
        Scene preview = default;
        RenderTexture previewTarget = null;
        RenderTexture oldTarget = RenderTexture.active;
        var report = new StringBuilder();
        try
        {
            report.AppendLine("playing=" + EditorApplication.isPlaying + " paused=" + EditorApplication.isPaused + " timeScale=" + Time.timeScale + " screen=" + Screen.width + "x" + Screen.height + " current=" + ObjectPath(EventSystem.current));
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                report.AppendLine("LIVE SCENE " + scene.path + " loaded=" + scene.isLoaded + " roots=" + scene.rootCount);
            }
            foreach (EventSystem system in Resources.FindObjectsOfTypeAll<EventSystem>().Where(IsSceneObject)) DumpEventSystem(system, report);
            Transform[] objects;
            if (EditorApplication.isPlaying)
                objects = Resources.FindObjectsOfTypeAll<Transform>().Where(IsSceneObject).ToArray();
            else
            {
                preview = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Game.unity");
                objects = preview.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
                report.AppendLine("SAVED GAME PREVIEW " + preview.handle);
            }
            Camera camera = objects.Select(t => t.GetComponent<Camera>()).FirstOrDefault(c => c != null && c.CompareTag("MainCamera"));
            if (!EditorApplication.isPlaying && camera != null)
            {
                foreach (Canvas canvas in objects.Select(t => t.GetComponent<Canvas>()).Where(c => c != null && c.isRootCanvas && c.renderMode != RenderMode.WorldSpace))
                    PortraitDisplay.ApplyCanvasLayout(canvas, camera, PortraitDisplay.ReferenceResolutionForScene(preview), new Vector2(Screen.width, Screen.height));
                report.AppendLine("Applied current portrait layout to isolated preview only.");
                // GraphicRaycaster ignores depth=-1 graphics until the preview geometry renders.
                previewTarget = new RenderTexture(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height), 24);
                previewTarget.Create();
                camera.scene = preview;
                camera.targetTexture = previewTarget;
                Canvas.ForceUpdateCanvases();
                camera.Render();
            }
            if (camera != null) report.AppendLine("CAMERA " + ObjectPath(camera) + " enabled=" + camera.enabled + " rect=" + camera.rect + " pixel=" + camera.pixelRect + " position=" + camera.transform.position + " size=" + camera.orthographicSize + " raycasters=" + string.Join(",", camera.GetComponents<BaseRaycaster>().Select(c => c.GetType().Name + ":" + c.enabled)));
            foreach (EventSystem system in objects.Select(t => t.GetComponent<EventSystem>()).Where(s => s != null)) DumpEventSystem(system, report);
            foreach (Canvas canvas in objects.Select(t => t.GetComponent<Canvas>()).Where(c => c != null))
            {
                report.AppendLine("CANVAS " + ObjectPath(canvas) + " active=" + canvas.gameObject.activeInHierarchy + " enabled=" + canvas.enabled + " root=" + canvas.isRootCanvas + " render=" + canvas.renderMode + " order=" + canvas.sortingOrder + " override=" + canvas.overrideSorting + " camera=" + ObjectPath(canvas.worldCamera) + " rect=" + ((RectTransform)canvas.transform).rect);
                foreach (GraphicRaycaster raycaster in canvas.GetComponents<GraphicRaycaster>()) report.AppendLine("  RAYCASTER enabled=" + raycaster.enabled + " camera=" + ObjectPath(raycaster.eventCamera) + " priority=" + raycaster.sortOrderPriority + "/" + raycaster.renderOrderPriority + " blocking=" + raycaster.blockingObjects + " mask=" + raycaster.blockingMask.value);
                IList<Graphic> registeredGraphics = GraphicRegistry.GetRaycastableGraphicsForCanvas(canvas);
                for (int registeredIndex = 0; registeredIndex < registeredGraphics.Count; registeredIndex++)
                {
                    Graphic registered = registeredGraphics[registeredIndex];
                    report.AppendLine("  REGISTERED RAYCAST GRAPHIC " + ObjectPath(registered) + " type=" + registered.GetType().Name + " raycast=" + registered.raycastTarget + " hasCanvasRenderer=" + (registered.canvasRenderer != null));
                }
            }
            foreach (CanvasGroup group in objects.Select(t => t.GetComponent<CanvasGroup>()).Where(c => c != null))
                report.AppendLine("GROUP " + ObjectPath(group) + " active=" + group.gameObject.activeInHierarchy + " alpha=" + group.alpha + " interactable=" + group.interactable + " blocks=" + group.blocksRaycasts + " ignoreParent=" + group.ignoreParentGroups);
            foreach (Graphic graphic in objects.Select(t => t.GetComponent<Graphic>()).Where(g => g != null && g.gameObject.activeInHierarchy && g.enabled && g.raycastTarget))
            {
                RectTransform rect = graphic.rectTransform;
                var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                Camera eventCamera = graphic.canvas != null && graphic.canvas.renderMode != RenderMode.ScreenSpaceOverlay ? graphic.canvas.worldCamera : null;
                Vector2 min = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[0]);
                Vector2 max = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[2]);
                string depth = graphic.canvasRenderer == null ? "noCanvasRenderer" : graphic.depth.ToString();
                report.AppendLine("GRAPHIC " + ObjectPath(graphic) + " type=" + graphic.GetType().Name + " alpha=" + graphic.color.a + " depth=" + depth + " screen=" + min + ".." + max + " canvas=" + ObjectPath(graphic.canvas) + " click=" + ObjectPath(ExecuteEvents.GetEventHandler<IPointerClickHandler>(graphic.gameObject)));
            }
            Canvas.ForceUpdateCanvases();
            EventSystem sampleSystem = objects.Select(t => t.GetComponent<EventSystem>()).FirstOrDefault(system => system != null && system.isActiveAndEnabled) ?? EventSystem.current;
            if (sampleSystem != null)
            {
                var points = new List<KeyValuePair<string, Vector2>>();
                points.Add(new KeyValuePair<string, Vector2>("center", new Vector2(Screen.width * .5f, Screen.height * .5f)));
                points.Add(new KeyValuePair<string, Vector2>("lower quarter", new Vector2(Screen.width * .5f, Screen.height * .25f)));
                points.Add(new KeyValuePair<string, Vector2>("upper quarter", new Vector2(Screen.width * .5f, Screen.height * .75f)));
                foreach (Button button in objects.Select(t => t.GetComponent<Button>()).Where(b => b != null && b.gameObject.activeInHierarchy))
                {
                    Canvas canvas = button.GetComponentInParent<Canvas>();
                    Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                    points.Add(new KeyValuePair<string, Vector2>("button " + Path(button.transform) + " interactable=" + button.IsInteractable(), RectTransformUtility.WorldToScreenPoint(eventCamera, ((RectTransform)button.transform).TransformPoint(((RectTransform)button.transform).rect.center))));
                }
                if (camera != null)
                    foreach (GunDrag gun in objects.Select(t => t.GetComponent<GunDrag>()).Where(g => g != null && g.gameObject.activeInHierarchy).Take(15))
                        points.Add(new KeyValuePair<string, Vector2>("gun " + Path(gun.transform), camera.WorldToScreenPoint(gun.transform.position)));
                foreach (var point in points)
                {
                    var results = new List<RaycastResult>();
                    var pointer = new PointerEventData(sampleSystem) { position = point.Value };
                    if (EditorApplication.isPlaying) sampleSystem.RaycastAll(pointer, results);
                    else
                    {
                        // BaseRaycaster.OnEnable is not invoked for non-ExecuteAlways preview
                        // components; query the real raycasters directly as the other regressions do.
                        foreach (BaseRaycaster raycaster in objects.Select(t => t.GetComponent<BaseRaycaster>()).Where(r => r != null && r.isActiveAndEnabled))
                        {
                            report.AppendLine("  PREVIEW QUERY " + ObjectPath(raycaster) + " for " + point.Key);
                            raycaster.Raycast(pointer, results);
                        }
                    }
                    report.AppendLine("SAMPLE " + point.Key + " @" + point.Value + " hits=" + results.Count);
                    foreach (var hit in results.Take(8)) report.AppendLine("  HIT " + ObjectPath(hit.gameObject) + " module=" + hit.module.GetType().Name + " depth=" + hit.depth + " order=" + hit.sortingOrder + " distance=" + hit.distance + " click=" + ObjectPath(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit.gameObject)));
                }
            }
            if (!EditorApplication.isPlaying)
                foreach (string path in new[] { "Assets/Scenes/Start.unity", "Assets/Scenes/Home.unity" }) DumpSceneEventSystems(path, report);
            File.WriteAllText(Result, report.ToString());
        }
        catch (Exception exception) { File.WriteAllText(Result, report + "\nFAIL\n" + exception); throw; }
        finally
        {
            RenderTexture.active = oldTarget;
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            if (previewTarget != null) { previewTarget.Release(); UnityEngine.Object.DestroyImmediate(previewTarget); }
        }
    }
    private static void DumpSceneEventSystems(string path, StringBuilder report)
    {
        Scene scene = default;
        try
        {
            scene = EditorSceneManager.OpenPreviewScene(path);
            report.AppendLine("OTHER SAVED SCENE " + path);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                report.AppendLine("ROOT " + ObjectPath(root) + " active=" + root.activeSelf + " scripts=" + string.Join(",", root.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().Name)));
                foreach (EventSystem system in root.GetComponentsInChildren<EventSystem>(true))
                {
                    DumpEventSystem(system, report);
                    for (Transform ancestor = system.transform.parent; ancestor != null; ancestor = ancestor.parent)
                        report.AppendLine("  EVENT ANCESTOR " + ObjectPath(ancestor) + " scripts=" + string.Join(",", ancestor.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().Name)));
                }
            }
        }
        finally { if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene); }
    }
    private static void DumpEventSystem(EventSystem system, StringBuilder report)
    {
        report.AppendLine("EVENTSYSTEM " + ObjectPath(system) + " active=" + system.gameObject.activeInHierarchy + " enabled=" + system.enabled + " current=" + (system == EventSystem.current) + " input=" + ObjectPath(system.currentInputModule) + " selected=" + ObjectPath(system.currentSelectedGameObject) + " pointerOver=" + system.IsPointerOverGameObject());
        foreach (BaseInputModule module in system.GetComponents<BaseInputModule>()) report.AppendLine("  MODULE " + module.GetType().Name + " active=" + module.isActiveAndEnabled + " input=" + ObjectPath(module.input));
    }
    private static bool IsSceneObject(Component value) => value != null && value.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(value);
    private static string ObjectPath(UnityEngine.Object value)
    {
        if (value == null) return "null";
        GameObject go = value as GameObject ?? (value as Component)?.gameObject;
        return go == null ? value.name : Path(go.transform) + " [scene=" + go.scene.name + ";id=" + go.GetInstanceID() + "]";
    }
    private static string Path(Transform transform) => transform == null ? "null" : transform.parent == null ? transform.name : Path(transform.parent) + "/" + transform.name;
}
#endif
