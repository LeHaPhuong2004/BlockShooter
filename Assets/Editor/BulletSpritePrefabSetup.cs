#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Imports the tintable projectile artwork and assigns it to the bullet prefab.</summary>
[InitializeOnLoad]
public static class BulletSpritePrefabSetup
{
    private const string SpritePath = "Assets/Resources/GameScreenSkin/Bullet_Pearl.png";
    private const string PrefabPath = "Assets/Prefab/bullet.prefab";
    private const string RequestPath = "Assets/Editor/BulletSpriteSetup.request";
    private const string ReportPath = "Temp/BulletSpriteSetup.result";

    static BulletSpritePrefabSetup() => EditorApplication.delayCall += ApplyIfRequested;

    private static void ApplyIfRequested()
    {
        if (!File.Exists(RequestPath)) return;
        try
        {
            Apply(File.ReadAllText(RequestPath).Contains("speed=1.4") ? 1.4f : 1f);
            File.Delete(RequestPath);
            File.Delete(RequestPath + ".meta");
        }
        catch (Exception exception)
        {
            File.WriteAllText(ReportPath, exception.ToString());
            Debug.LogException(exception);
        }
    }

    [MenuItem("Tools/Game/Apply Bullet Sprite")]
    public static void Apply() => Apply(1f);

    private static void Apply(float speedMultiplier)
    {
        AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(SpritePath) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Bullet sprite texture was not imported.");

        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var bullet = root.GetComponent<Bullet>();
            if (bullet == null) throw new InvalidOperationException("Bullet component is missing.");
            bullet.speed *= speedMultiplier;
            var renderer = bullet.bulletSprite != null ? bullet.bulletSprite : root.GetComponentInChildren<SpriteRenderer>();
            if (renderer == null) renderer = root.AddComponent<SpriteRenderer>();

            // The round art fills roughly 86% of the canvas. Keep a 0.24-unit body
            // without changing the prefab transform, collider or flight trail.
            var source = new Texture2D(2, 2);
            int sourceWidth;
            try
            {
                if (!ImageConversion.LoadImage(source, File.ReadAllBytes(SpritePath)))
                    throw new InvalidOperationException("Cannot read the bullet PNG.");
                sourceWidth = source.width;
                var pixels = source.GetPixels32();
                bool hasTransparent = false, hasOpaque = false;
                foreach (var pixel in pixels)
                {
                    if (pixel.a == 0) hasTransparent = true;
                    if (pixel.a > 200) hasOpaque = true;
                }
                if (!hasTransparent || !hasOpaque)
                    throw new InvalidOperationException("Bullet image must have visible artwork and transparent background.");
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = sourceWidth * 0.86f * Mathf.Abs(renderer.transform.lossyScale.x) / 0.24f;
            importer.alphaIsTransparency = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 512;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            renderer.drawMode = SpriteDrawMode.Simple;
            renderer.color = Color.white;
            renderer.enabled = true;
            if (renderer.sprite == null) throw new InvalidOperationException("Imported bullet sprite is missing.");
            bullet.bulletSprite = renderer;
            if (bullet.bulletTrail != null)
            {
                bullet.bulletTrail.time = Bullet.TrailLifetime;
                bullet.bulletTrail.widthMultiplier = bullet.VisualDiameter;
                bullet.bulletTrail.sortingLayerID = renderer.sortingLayerID;
                bullet.bulletTrail.sortingOrder = renderer.sortingOrder - 1;
            }
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();

        var saved = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<Bullet>();
        if (saved.bulletSprite == null || saved.bulletSprite.sprite == null || !saved.bulletSprite.enabled)
            throw new InvalidOperationException("The saved prefab does not have a visible bullet sprite.");
        if (saved.bulletTrail == null || !Mathf.Approximately(saved.bulletTrail.widthMultiplier, saved.VisualDiameter))
            throw new InvalidOperationException("Trail width must match the visible bullet diameter.");
        File.WriteAllText(ReportPath, $"OK: {PrefabPath} uses {AssetDatabase.GetAssetPath(saved.bulletSprite.sprite)}. " +
            $"Transparent alpha verified; tintable white sprite; bounds {saved.bulletSprite.bounds.size}; " +
            $"diameter: {saved.VisualDiameter}; trail width: {saved.bulletTrail.widthMultiplier}; " +
            $"PPU: {importer.spritePixelsPerUnit}; speed: {saved.speed}; " +
            $"speed multiplier applied: {speedMultiplier}; material: {saved.bulletSprite.sharedMaterial?.name}.");
    }
}
#endif
