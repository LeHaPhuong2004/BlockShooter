#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Sizes the water droplet to one tile while preserving the existing sprite asset reference.</summary>
[InitializeOnLoad]
public static class WaterDropSpriteSetup
{
    private const string SpritePath = "Assets/Resources/BlockColorData/BlockSprite/Bubble.png";
    private const string RequestPath = "Assets/Editor/WaterDropSpriteSetup.request";
    private const string ReportPath = "Temp/WaterDropSpriteSetup.result";

    static WaterDropSpriteSetup() => EditorApplication.delayCall += ApplyIfRequested;

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

    [MenuItem("Tools/Game/Apply Water Drop Sprite")]
    public static void Apply()
    {
        AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
        importer.GetSourceTextureWidthAndHeight(out _, out int height);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        // The drop fills 91% of the source height; its visible height is one tile.
        importer.spritePixelsPerUnit = height * 0.91f;
        importer.alphaIsTransparency = true;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.maxTextureSize = 512;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        if (sprite == null) throw new InvalidOperationException("Water drop sprite failed to import.");

        int references = 0;
        var prefabDetails = new System.Text.StringBuilder();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;
            foreach (var block in prefab.GetComponentsInChildren<Block>(true))
            {
                if (block.bubbleIcon != sprite) continue;
                references++;
                prefabDetails.AppendLine($"Prefab: {path}");
                var probe = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var water = probe.GetComponentInChildren<Block>(true);
                    water.SetBlockType(BlockType.Bubble);
                    if (water.spriteRenderer == null || water.spriteRenderer.enabled || water.spriteRenderer.color.a != 0f ||
                        water.typeIconRenderer == null || !water.typeIconRenderer.enabled || water.typeIconRenderer.sprite != sprite)
                        throw new InvalidOperationException("Water block must show only its droplet icon: " + path);
                    water.SetBlockType(BlockType.Normal);
                    if (!water.spriteRenderer.enabled || water.spriteRenderer.color.a != 1f)
                        throw new InvalidOperationException("Normal block background must be restored: " + path);
                    prefabDetails.AppendLine("PASS: water background disabled, alpha=0, droplet visible; normal background restored.");
                }
                finally { PrefabUtility.UnloadPrefabContents(probe); }
                foreach (var renderer in block.GetComponentsInChildren<SpriteRenderer>(true))
                    prefabDetails.AppendLine($"  {renderer.name}: sprite={renderer.sprite?.name}; color={renderer.color}; shader={renderer.sharedMaterial?.shader.name}; main={renderer == block.spriteRenderer}; icon={renderer == block.typeIconRenderer}");
            }
        }
        if (references == 0) throw new InvalidOperationException("No block prefabs reference the water drop sprite.");
        int refreshed = 0;
        var details = new System.Text.StringBuilder();
        foreach (var block in Resources.FindObjectsOfTypeAll<Block>())
        {
            if (EditorUtility.IsPersistent(block) || !block.gameObject.scene.IsValid() || block.blockType != BlockType.Bubble) continue;
            float before = block.spriteRenderer != null ? block.spriteRenderer.color.a : -1f;
            block.UpdateTypeVisual();
            refreshed++;
            details.AppendLine($"{block.name}: background alpha {before} -> {block.spriteRenderer?.color.a}");
            foreach (var renderer in block.GetComponentsInChildren<SpriteRenderer>(true))
                details.AppendLine($"  {renderer.name}: sprite={renderer.sprite?.name}; color={renderer.color}; shader={renderer.sharedMaterial?.shader.name}; main={renderer == block.spriteRenderer}; icon={renderer == block.typeIconRenderer}");
        }
        File.WriteAllText(ReportPath, $"OK: {SpritePath}; {references} existing block prefab references preserved. " +
            $"PPU: {importer.spritePixelsPerUnit}; sprite bounds: {sprite.bounds.size}; visible height approximately 1 tile. " +
            $"Refreshed {refreshed} scene water blocks.\n{prefabDetails}\n{details}");
    }
}
#endif
