#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>Authors reusable popup sprite slices in the Editor while retaining existing sprite identities.</summary>
[InitializeOnLoad]
public static class GamePopupArtSetup
{
    private const string RequestPath = "Temp/GamePopupArtSetup.request";
    private const string ResultPath = "Temp/GamePopupArtSetup.result";
    private const string SettingsPath = "Assets/UI/Popup/Settings_Atlas_Cut.png";
    private const string VictoryPath = "Assets/UI/Popup/VictoryRewards_Atlas_Cut.png";
    private static bool running;

    static GamePopupArtSetup() => EditorApplication.update += Poll;

    private static void Poll()
    {
        if (running || !File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        running = true;
        try { EnsureSprites(); }
        catch (Exception exception)
        {
            File.WriteAllText(ResultPath, exception.ToString());
            Debug.LogException(exception);
        }
        finally { File.Delete(RequestPath); running = false; }
    }

    [MenuItem("Tools/Game/Prepare Authored Popup Artwork")]
    public static void EnsureSprites()
    {
        var report = new StringBuilder();
        var factories = new SpriteDataProviderFactories();
        factories.Init();
        AddSprites(SettingsPath, factories, report, new[]
        {
            Slice("SettingsPanel", 16, 573, 678, 881),
            Slice("SettingsRow", 31, 412, 667, 122),
            Slice("SettingsCloseRed", 705, 1212, 168, 169),
            Slice("SettingsCloseX", 895, 1236, 100, 105),
            Slice("SettingsHome", 704, 964, 156, 148),
            Slice("SettingsMusic", 879, 964, 128, 147),
            Slice("SettingsVibration", 704, 778, 143, 127),
            Slice("SettingsSound", 873, 783, 136, 124),
            Slice("SettingsToggleOn", 725, 412, 269, 123),
            Slice("SettingsToggleOff", 722, 266, 274, 126),
            Slice("SettingsToggleKnob", 789, 111, 136, 139)
        });
        AddSprites(VictoryPath, factories, report, new[]
        {
            Slice("VictoryCoins", 90, 409, 175, 138),
            Slice("VictoryCharacterRed", 33, 209, 260, 198),
            Slice("VictoryCharacterBox", 321, 209, 206, 198),
            Slice("VictoryCharacterBlue", 567, 209, 220, 198)
        });
        AssetDatabase.SaveAssets();
        report.AppendLine("PASS: popup sprites prepared; no scenes or prefabs were modified.");
        File.WriteAllText(ResultPath, report.ToString());
    }

    private static SpriteRect Slice(string name, int x, int y, int width, int height) => new SpriteRect
    {
        name = name,
        rect = new Rect(x, y, width, height),
        alignment = SpriteAlignment.Center,
        pivot = new Vector2(.5f, .5f),
        border = Vector4.zero,
        spriteID = GUID.Generate()
    };

    private static void AddSprites(string path, SpriteDataProviderFactories factories, StringBuilder report, IEnumerable<SpriteRect> additions)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null || importer.textureType != TextureImporterType.Sprite)
            throw new InvalidOperationException("Expected a sprite texture: " + path);

        var priorSprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(sprite => sprite.name, sprite => LocalId(sprite));
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        var rects = provider.GetSpriteRects().ToList();
        bool convertingSingle = importer.spriteImportMode != SpriteImportMode.Multiple;
        if (convertingSingle)
        {
            // A Single-to-Multiple conversion changes Unity's fixed 21300000 local ID.
            // Retain the original full-image sprite and first ensure no authored assets depend on that old ID.
            var dependents = AssetDatabase.GetAllAssetPaths()
                .Where(candidate => candidate.StartsWith("Assets/", StringComparison.Ordinal) && candidate != path &&
                    (candidate.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) || candidate.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) || candidate.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)))
                .Where(candidate => AssetDatabase.GetDependencies(candidate, false).Contains(path))
                .ToArray();
            report.AppendLine(path + " original single-sprite dependents=" + dependents.Length);
            if (dependents.Length > 0)
                throw new InvalidOperationException("Preserve existing references before slicing " + path + ": " + string.Join(", ", dependents));
            importer.spriteImportMode = SpriteImportMode.Multiple;
            provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
        }

        int added = 0;
        foreach (SpriteRect addition in additions)
        {
            if (rects.Any(existing => existing.name == addition.name)) continue;
            rects.Add(addition);
            added++;
        }

        if (convertingSingle || added > 0)
        {
            provider.SetSpriteRects(rects.ToArray());
            var nameProvider = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (nameProvider != null)
            {
                var pairs = nameProvider.GetNameFileIdPairs().ToList();
                foreach (SpriteRect rect in rects)
                    if (!pairs.Any(pair => pair.GetFileGUID() == rect.spriteID)) pairs.Add(new SpriteNameFileIdPair(rect.name, rect.spriteID));
                nameProvider.SetNameFileIdPairs(pairs);
            }
            provider.Apply();
            importer.SaveAndReimport();
        }

        var after = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(sprite => sprite.name);
        foreach (var prior in priorSprites)
        {
            if (!after.TryGetValue(prior.Key, out Sprite sprite)) throw new InvalidOperationException("Lost existing sprite " + prior.Key);
            if (!convertingSingle && LocalId(sprite) != prior.Value) throw new InvalidOperationException("Changed existing local file ID for " + prior.Key);
            report.AppendLine("PRESERVED " + prior.Key + " localID=" + LocalId(sprite) + (convertingSingle ? " (full image retained; previously unreferenced Single sprite)" : ""));
        }
        foreach (SpriteRect addition in additions)
        {
            if (!after.TryGetValue(addition.name, out Sprite sprite)) throw new InvalidOperationException("Missing authored sprite " + addition.name);
            report.AppendLine("SPRITE " + addition.name + " rect=" + sprite.rect + " localID=" + LocalId(sprite));
        }
        report.AppendLine(path + " added=" + added + " sprites=" + after.Count);
    }

    private static long LocalId(UnityEngine.Object asset)
    {
        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long id))
            throw new InvalidOperationException("Cannot read asset identity for " + asset.name);
        return id;
    }
}
#endif
