#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Uses the mystery vacuum body on guns, with their existing wheels and nozzle.</summary>
[InitializeOnLoad]
public static class MysteryVacuumSpriteSetup
{
    private const string SpritePath = "Assets/Resources/GameScreenSkin/Vacuum_Mystery.png";
    private const string ReferencePath = "Assets/Resources/GameScreenSkin/Vacuum_Blue.png";
    private const string RequestPath = "Assets/Editor/MysteryVacuumSpriteSetup.request";
    private const string ReportPath = "Temp/MysteryVacuumSpriteSetup.result";

    static MysteryVacuumSpriteSetup() => EditorApplication.delayCall += ApplyIfRequested;

    private static void ApplyIfRequested()
    {
        if (!File.Exists(RequestPath)) return;
        try
        {
            Apply();
            File.Delete(RequestPath);
            File.Delete(RequestPath + ".meta");
        }
        catch (Exception exception)
        {
            File.WriteAllText(ReportPath, exception.ToString());
            Debug.LogException(exception);
        }
    }

    [MenuItem("Tools/Game/Apply Mystery Vacuum Sprite")]
    public static void Apply()
    {
        AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceSynchronousImport);
        var reference = AssetDatabase.LoadAllAssetsAtPath(ReferencePath).OfType<Sprite>().First();
        var importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
        importer.GetSourceTextureWidthAndHeight(out int width, out _);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = width / reference.bounds.size.x;
        importer.spritePivot = new Vector2(0.5f, 0.5f);
        importer.alphaIsTransparency = true;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.maxTextureSize = 512;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        if (sprite == null) throw new InvalidOperationException("Mystery vacuum sprite was not imported.");

        int updated = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || prefab.GetComponentsInChildren<Gun>(true).Length == 0) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var gun in root.GetComponentsInChildren<Gun>(true))
                    gun.questionMarkSprite = sprite;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            foreach (var gun in AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<Gun>(true))
            {
                if (gun.questionMarkSprite != sprite)
                    throw new InvalidOperationException("Mystery sprite was not saved: " + path);
                updated++;
            }
        }
        if (updated == 0) throw new InvalidOperationException("No gun prefabs were found.");
        AssetDatabase.SaveAssets();
        File.WriteAllText(ReportPath, $"OK: {updated} gun prefab components use {SpritePath}. " +
            $"Body bounds: {sprite.bounds.size}; reference body bounds: {reference.bounds.size}. Existing wheels/nozzle preserved.");
    }
}
#endif
