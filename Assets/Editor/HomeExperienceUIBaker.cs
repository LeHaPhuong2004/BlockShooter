#if UNITY_EDITOR
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class HomeExperienceUIBaker
{
    private const string HomeScenePath = "Assets/Scenes/Home.unity";
    private const string BakeRequestPath = "Assets/Editor/HomeExperienceUIBake.request";
    // Keep this targeted path separate so rebuilding Settings does not touch other popups.
    private const string SettingsBakeRequestPath = "Assets/Editor/HomeExperienceUISettingsBake.request";
    private const string GeneratedSpriteFolder = "Assets/Resources/GeneratedHomeUI";

    static HomeExperienceUIBaker()
    {
        EditorApplication.delayCall += BakeIfRequested;
        EditorApplication.delayCall += BakeSettingsIfRequested;
    }

    [MenuItem("Tools/Home UI/Bake into Home Scene")]
    public static void BakeHomeSceneCommand()
    {
        BakeHomeScene();
    }

    [MenuItem("Tools/Home UI/Bake Settings Popup into Home Scene")]
    public static void BakeSettingsPopupCommand()
    {
        BakeSettingsPopup();
    }

    private static void BakeIfRequested()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += BakeIfRequested;
            return;
        }
        if (!File.Exists(BakeRequestPath)) return;
        try
        {
            BakeHomeScene();
            File.Delete(BakeRequestPath);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private static void BakeSettingsIfRequested()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += BakeSettingsIfRequested;
            return;
        }
        if (!File.Exists(SettingsBakeRequestPath)) return;
        try
        {
            BakeSettingsPopup();
            File.Delete(SettingsBakeRequestPath);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private static void BakeSettingsPopup()
    {
        Scene homeScene = SceneManager.GetSceneByPath(HomeScenePath);
        if (!homeScene.IsValid() || !homeScene.isLoaded)
            homeScene = EditorSceneManager.OpenScene(HomeScenePath, OpenSceneMode.Additive);
        if (!homeScene.IsValid() || !homeScene.isLoaded)
            throw new InvalidOperationException("Could not open the Home scene for Settings UI baking.");

        EditorSceneManager.SetActiveScene(homeScene);
        HomeExperienceUI controller = null;
        foreach (var root in homeScene.GetRootGameObjects())
        {
            controller = root.GetComponentInChildren<HomeExperienceUI>(true);
            if (controller != null) break;
        }

        if (controller == null)
        {
            var controllerObject = new GameObject("Home Experience UI");
            SceneManager.MoveGameObjectToScene(controllerObject, homeScene);
            controller = controllerObject.AddComponent<HomeExperienceUI>();
        }

        if (controller.transform.Find("Home Canvas") == null)
            controller.BuildSceneUIForEditor();

        controller.BuildSettingsPopupObjectsForEditor();
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(homeScene);
        if (!EditorSceneManager.SaveScene(homeScene))
            throw new InvalidOperationException("Unity could not save the Settings popup to the Home scene.");

        Selection.activeGameObject = controller.gameObject;
        Debug.Log("Settings Popup GameObjects were baked into Assets/Scenes/Home.unity.");
    }

    private static void BakeHomeScene()
    {
        Scene homeScene = SceneManager.GetSceneByPath(HomeScenePath);
        if (!homeScene.IsValid() || !homeScene.isLoaded)
            homeScene = EditorSceneManager.OpenScene(HomeScenePath, OpenSceneMode.Additive);
        if (!homeScene.IsValid() || !homeScene.isLoaded)
            throw new InvalidOperationException("Could not open the Home scene for UI baking.");

        EditorSceneManager.SetActiveScene(homeScene);
        HomeExperienceUI controller = null;
        foreach (var root in homeScene.GetRootGameObjects())
        {
            controller = root.GetComponentInChildren<HomeExperienceUI>(true);
            if (controller != null) break;
        }

        if (controller == null)
        {
            var controllerObject = new GameObject("Home Experience UI");
            SceneManager.MoveGameObjectToScene(controllerObject, homeScene);
            controller = controllerObject.AddComponent<HomeExperienceUI>();
        }

        if (controller.transform.Find("Home Canvas") == null)
        {
            controller.BuildSceneUIForEditor();
        }

        controller.BuildSettingsPopupObjectsForEditor();
        controller.BuildSpecialLevelPopupObjectsForEditor();

        PersistRuntimeVisualAssets(homeScene);
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(homeScene);
        if (!EditorSceneManager.SaveScene(homeScene))
            throw new InvalidOperationException("Unity could not save the baked Home scene.");

        Selection.activeGameObject = controller.gameObject;
        Debug.Log("Home UI objects were baked into Assets/Scenes/Home.unity.");
    }

    private static void PersistRuntimeVisualAssets(Scene scene)
    {
        if (!AssetDatabase.IsValidFolder(GeneratedSpriteFolder))
            AssetDatabase.CreateFolder("Assets/Resources", "GeneratedHomeUI");

        var spriteMap = new System.Collections.Generic.Dictionary<Sprite, Sprite>();
        var materialMap = new System.Collections.Generic.Dictionary<Material, Material>();
        foreach (var image in Resources.FindObjectsOfTypeAll<Image>())
        {
            if (image == null || image.gameObject.scene != scene || image.sprite == null) continue;
            Sprite source = image.sprite;
            if (EditorUtility.IsPersistent(source)) continue;
            if (!spriteMap.TryGetValue(source, out Sprite savedSprite))
            {
                savedSprite = SaveRuntimeSprite(source);
                spriteMap.Add(source, savedSprite);
            }
            image.sprite = savedSprite;
            if (image.material != null && !EditorUtility.IsPersistent(image.material))
            {
                if (!materialMap.TryGetValue(image.material, out Material savedMaterial))
                {
                    savedMaterial = SaveRuntimeMaterial(image.material);
                    materialMap.Add(image.material, savedMaterial);
                }
                image.material = savedMaterial;
            }
            EditorUtility.SetDirty(image);
        }

        Font fallbackFont = HomeUIFont.LoadSourceFont();
        foreach (var text in Resources.FindObjectsOfTypeAll<Text>())
        {
            if (text == null || text.gameObject.scene != scene) continue;
            text.font = fallbackFont;
            EditorUtility.SetDirty(text);
        }

        HomeUIFont.ApplyToScene(scene);

        AssetDatabase.SaveAssets();
    }

    private static Sprite SaveRuntimeSprite(Sprite source)
    {
        Texture2D texture = source.texture;
        if (texture == null) throw new InvalidOperationException("Generated sprite has no texture: " + source.name);

        int x = Mathf.RoundToInt(source.rect.x);
        int y = Mathf.RoundToInt(source.rect.y);
        int width = Mathf.RoundToInt(source.rect.width);
        int height = Mathf.RoundToInt(source.rect.height);
        if (width <= 0 || height <= 0)
            throw new InvalidOperationException("Generated sprite has an invalid rectangle: " + source.name);

        var crop = new Texture2D(width, height, TextureFormat.RGBA32, false);
        try
        {
            if (texture.isReadable)
            {
                crop.SetPixels(texture.GetPixels(x, y, width, height));
                crop.Apply();
            }
            else
            {
                RenderTexture previous = RenderTexture.active;
                RenderTexture temporary = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
                try
                {
                    Graphics.Blit(texture, temporary);
                    RenderTexture.active = temporary;
                    crop.ReadPixels(new Rect(x, y, width, height), 0, 0);
                    crop.Apply();
                }
                finally
                {
                    RenderTexture.active = previous;
                    RenderTexture.ReleaseTemporary(temporary);
                }
            }

            byte[] png = crop.EncodeToPNG();
            return ImportRuntimeSprite(source, texture, png);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(crop);
        }
    }

    private static Sprite ImportRuntimeSprite(Sprite source, Texture2D texture, byte[] png)
    {
        string hash;
        using (var sha = SHA256.Create())
        {
            byte[] digest = sha.ComputeHash(png);
            hash = BitConverter.ToString(digest, 0, 6).Replace("-", string.Empty).ToLowerInvariant();
        }
        string safeName = Regex.Replace(string.IsNullOrEmpty(source.name) ? "Sprite" : source.name,
            "[^A-Za-z0-9_-]", "_");
        string assetPath = GeneratedSpriteFolder + "/" + safeName + "_" + hash + ".png";
        File.WriteAllBytes(assetPath, png);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Could not configure generated sprite: " + assetPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = texture.filterMode;
        importer.wrapMode = texture.wrapMode;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.spritePixelsPerUnit = source.pixelsPerUnit;
        var spriteSettings = new TextureImporterSettings();
        importer.ReadTextureSettings(spriteSettings);
        spriteSettings.spriteAlignment = (int)SpriteAlignment.Custom;
        spriteSettings.spritePivot = new Vector2(source.pivot.x / source.rect.width, source.pivot.y / source.rect.height);
        spriteSettings.spriteBorder = source.border;
        importer.SetTextureSettings(spriteSettings);
        importer.SaveAndReimport();

        var result = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (result == null) throw new InvalidOperationException("Generated sprite asset did not import: " + assetPath);
        return result;
    }

    private static Material SaveRuntimeMaterial(Material source)
    {
        const string assetPath = GeneratedSpriteFolder + "/HomeIconWhiteSilhouette.mat";
        var saved = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        if (saved != null) return saved;

        saved = new Material(source) { name = source.name };
        AssetDatabase.CreateAsset(saved, assetPath);
        return saved;
    }
}
#endif
