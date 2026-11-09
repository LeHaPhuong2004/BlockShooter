using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Shared player language for authored UI and runtime messages.</summary>
public static class Localize
{
    public const string LanguagePreferenceKey = "home_settings_language";
    public const int Vietnamese = 0;
    public const int English = 1;
    private static int language = -1;
    private static TMP_FontAsset multilingualFont;

    public static int Language => language < 0
        ? language = Normalize(PlayerPrefs.GetInt(LanguagePreferenceKey, Vietnamese)) : language;
    public static bool IsEnglish => Language == English;
    public static event Action LanguageChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        language = -1;
        multilingualFont = null;
        LanguageChanged = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplyLoadedScenes();
    }

    public static void SetLanguage(int value, bool persist = true)
    {
        value = Normalize(value);
        bool changed = Language != value;
        language = value;
        if (persist)
        {
            PlayerPrefs.SetInt(LanguagePreferenceKey, value);
            PlayerPrefs.Save();
        }
        if (Application.isPlaying) ApplyLoadedScenes();
        if (changed) LanguageChanged?.Invoke();
    }

    public static string Text(string vietnamese, string english) => IsEnglish ? english : vietnamese;

    public static string Format(string vietnamese, string english, params object[] values) =>
        string.Format(CultureInfo.InvariantCulture, Text(vietnamese, english), values);

    public static string Translate(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        string translated = LocalizeCatalog.Translate(value, IsEnglish);
        if (translated != value) return translated;
        // Saved level labels are authored before a level is loaded. Runtime
        // writers use Format; these fallbacks also cover unopened scene labels.
        if (TryNumberSuffix(value, "CẤP ĐỘ ", out string level) ||
            TryNumberSuffix(value, "LEVEL ", out level) || TryNumberSuffix(value, "LV ", out level))
            return Format("CẤP ĐỘ {0}", "LEVEL {0}", level);
        if (TryNumberSuffix(value, "HOÀN THÀNH CẤP ĐỘ ", out level) ||
            TryNumberSuffix(value, "COMPLETED LEVEL ", out level))
            return Format("HOÀN THÀNH CẤP ĐỘ {0}", "COMPLETED LEVEL {0}", level);
        return value;
    }

    public static void Apply(Transform root)
    {
        if (root == null) return;
        foreach (Text label in root.GetComponentsInChildren<Text>(true)) ApplyLabel(label);
        foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(true)) ApplyLabel(label);
    }

    internal static void ApplyLabel(Text label)
    {
        if (label == null) return;
        InputField input = label.GetComponentInParent<InputField>();
        if (input != null && input.textComponent == label) return;
        string translated = Translate(label.text);
        if (label.text != translated) label.text = translated;
        if (HasLetters(label.text) && label.GetComponent<LocalizedText>() == null)
            label.gameObject.AddComponent<LocalizedText>();
    }

    internal static void ApplyLabel(TMP_Text label)
    {
        if (label == null) return;
        TMP_InputField input = label.GetComponentInParent<TMP_InputField>();
        if (input != null && input.textComponent == label) return;
        string translated = Translate(label.text);
        if (label.text != translated) label.text = translated;
        if (label.font != null && MissingLetters(label.font, translated))
        {
            if (multilingualFont == null)
                multilingualFont = Resources.Load<TMP_FontAsset>(HomeUIFont.AssetPath);
            if (multilingualFont != null && !MissingLetters(multilingualFont, translated))
                label.font = multilingualFont;
        }
        if (HasLetters(label.text) && label.GetComponent<LocalizedText>() == null)
            label.gameObject.AddComponent<LocalizedText>();
    }

    private static bool MissingLetters(TMP_FontAsset font, string value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        foreach (char character in value)
            if (char.IsLetter(character) && !font.HasCharacter(character, searchFallbacks: true, tryAddCharacter: false))
                return true;
        return false;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (GameObject root in scene.GetRootGameObjects()) Apply(root.transform);
    }

    private static void ApplyLoadedScenes()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.isLoaded) OnSceneLoaded(scene, LoadSceneMode.Additive);
        }
    }

    private static bool HasLetters(string value)
    {
        if (value == null) return false;
        foreach (char character in value) if (char.IsLetter(character)) return true;
        return false;
    }

    private static bool TryNumberSuffix(string value, string prefix, out string number)
    {
        number = null;
        if (!value.StartsWith(prefix, StringComparison.Ordinal)) return false;
        string suffix = value.Substring(prefix.Length).Trim();
        if (!int.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)) return false;
        number = suffix;
        return true;
    }

    private static int Normalize(int value) => value == English ? English : Vietnamese;
}
