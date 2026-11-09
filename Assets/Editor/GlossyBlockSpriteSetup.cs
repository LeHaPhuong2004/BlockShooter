#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>Imports the ten glossy block sprites and connects the existing color data.</summary>
[InitializeOnLoad]
public static class GlossyBlockSpriteSetup
{
    private const string RequestPath = "Assets/Editor/GlossyBlockSpriteSetup.request";
    private const string ReportPath = "Temp/GlossyBlockSpriteSetup.result";
    private static readonly string[] Colors = { "Blue", "Red", "Green", "Yellow", "Orange", "Pink", "Purple", "LightBlue", "Brown", "White" };

    static GlossyBlockSpriteSetup() => EditorApplication.delayCall += ApplyIfRequested;

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

    [MenuItem("Tools/Game/Apply Glossy Block Sprites")]
    public static void Apply()
    {
        var report = new StringBuilder();
        var factories = new SpriteDataProviderFactories();
        factories.Init();
        foreach (string colorName in Colors)
        {
            string dataName = colorName == "Purple" ? "Violet" : colorName == "LightBlue" ? "SkyBlue" : colorName;
            string path = $"Assets/Resources/GameScreenSkin/Block_{colorName}.png";
            var data = AssetDatabase.LoadAssetAtPath<BlockColorData>($"Assets/Resources/BlockColorData/Data/{dataName}.asset");
            if (data == null) throw new InvalidOperationException("Missing color data: " + dataName);
            // Keep the existing tile width, including sprites already referenced in prefabs.
            float tileWidth = data.icon != null ? data.icon.bounds.size.x : 1f;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = width / tileWidth;
            importer.alphaIsTransparency = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 512;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            if (importer.spriteImportMode == SpriteImportMode.Multiple)
            {
                var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
                provider.InitSpriteEditorDataProvider();
                var rects = provider.GetSpriteRects();
                if (rects.Length != 1) throw new InvalidOperationException("Expected one block sprite in " + path);
                rects[0].rect = new Rect(0, 0, width, height);
                rects[0].alignment = SpriteAlignment.Center;
                rects[0].pivot = new Vector2(0.5f, 0.5f);
                provider.SetSpriteRects(rects);
                provider.Apply();
            }
            else importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
            var sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
            if (sprite == null) throw new InvalidOperationException("Missing imported sprite: " + path);
            data.icon = sprite;
            EditorUtility.SetDirty(data);
            report.AppendLine($"PASS {dataName}: {path}; bounds={sprite.bounds.size}; PPU={importer.spritePixelsPerUnit}");
        }
        AssetDatabase.SaveAssets();

        int refreshed = 0;
        foreach (var block in Resources.FindObjectsOfTypeAll<Block>())
        {
            if (EditorUtility.IsPersistent(block) || !block.gameObject.scene.IsValid()) continue;
            // Refresh artwork only; SetColor would reset the block's remaining HP.
            if (block.blockType == BlockType.Dual)
            {
                RefreshRenderer(block.dualTopRenderer, block.dualTopColor);
                RefreshRenderer(block.dualBottomRenderer, block.dualBottomColor);
            }
            else if (block.blockType != BlockType.BanhMi && block.blockType != BlockType.FriedChicken)
                RefreshRenderer(block.spriteRenderer, block.blockColor);
            refreshed++;
        }
        report.AppendLine($"OK: all 10 colors saved; refreshed {refreshed} scene blocks without changing HP or gameplay color values.");
        File.WriteAllText(ReportPath, report.ToString());
    }

    private static void RefreshRenderer(SpriteRenderer renderer, BlockColorData data)
    {
        if (renderer == null || data == null || data.icon == null) return;
        float alpha = renderer.color.a;
        renderer.sprite = data.icon;
        renderer.color = new Color(1f, 1f, 1f, alpha);
    }
}
#endif
