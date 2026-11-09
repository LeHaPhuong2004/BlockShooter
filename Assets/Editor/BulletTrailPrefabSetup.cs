#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Applies the trail to the binary prefab through Unity's prefab API.</summary>
[InitializeOnLoad]
public static class BulletTrailPrefabSetup
{
    private const string PrefabPath = "Assets/Prefab/bullet.prefab";
    private const string MaterialPath = "Assets/Prefab/BulletTrail.mat";
    private const string RequestPath = "Assets/Editor/BulletTrailSetup.request";
    private const string ReportPath = "Temp/BulletTrailSetup.result";

    static BulletTrailPrefabSetup() => EditorApplication.delayCall += ApplyIfRequested;

    private static void ApplyIfRequested()
    {
        if (!File.Exists(RequestPath)) return;
        try
        {
            Apply();
            File.Delete(RequestPath);
            File.Delete(RequestPath + ".meta");
            File.WriteAllText(ReportPath, "OK: bullet.prefab has a child TrailRenderer and a saved trail material.");
        }
        catch (Exception exception)
        {
            File.WriteAllText(ReportPath, exception.ToString());
            Debug.LogException(exception);
        }
    }

    [MenuItem("Tools/Game/Apply Bullet Trail")]
    public static void Apply()
    {
        var shader = Shader.Find("BlockBlast/Bullet Trail");
        if (shader == null) throw new InvalidOperationException("Bullet Trail shader was not imported.");
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "Bullet Trail" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        material.shader = shader;
        EditorUtility.SetDirty(material);

        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var bullet = root.GetComponent<Bullet>();
            if (bullet == null) throw new InvalidOperationException("The bullet prefab has no Bullet component.");
            var child = root.transform.Find("Flight Trail");
            if (child == null)
            {
                child = new GameObject("Flight Trail").transform;
                child.SetParent(root.transform, false);
            }
            child.localPosition = Vector3.zero;
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;
            var trail = child.GetComponent<TrailRenderer>();
            if (trail == null) trail = child.gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = material;
            trail.time = Bullet.TrailLifetime;
            trail.minVertexDistance = 0.02f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.45f, 0.65f), new Keyframe(1f, 0f));
            trail.widthMultiplier = bullet.VisualDiameter;
            trail.numCornerVertices = 8;
            trail.numCapVertices = 8;
            trail.alignment = LineAlignment.View;
            trail.textureMode = LineTextureMode.Stretch;
            trail.generateLightingData = false;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.lightProbeUsage = LightProbeUsage.Off;
            trail.reflectionProbeUsage = ReflectionProbeUsage.Off;
            trail.autodestruct = false;
            trail.emitting = false;
            var sprite = bullet.bulletSprite != null ? bullet.bulletSprite : root.GetComponentInChildren<SpriteRenderer>();
            if (sprite != null)
            {
                trail.sortingLayerID = sprite.sortingLayerID;
                trail.sortingOrder = sprite.sortingOrder - 1;
            }
            bullet.bulletTrail = trail;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();

        var saved = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<Bullet>();
        if (saved.bulletTrail == null || saved.bulletTrail.sharedMaterial == null)
            throw new InvalidOperationException("The saved bullet prefab is missing its trail or material.");
        Debug.Log("Bullet trail saved: 0.09s lifetime, width matching bullet diameter, 0.02 vertex spacing, rounded corners and caps.");
    }
}
#endif
