using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

public class SettingManager : MonoBehaviour
{
    [Header("Toggle")]
    [SerializeField] private Toggle mussicToggle;
    [SerializeField] private Toggle sfxToggle;
    [SerializeField] private Toggle vibrationToggle;
    [SerializeField] private Toggle iconAccesstionceToggle;
    [SerializeField] private Toggle visualAccessbilityToggle;

    private void Start()
    {
        LoadSettings();

        mussicToggle.onValueChanged.AddListener(SetMussic);
        sfxToggle.onValueChanged.AddListener(SetSFX);
        vibrationToggle.onValueChanged.AddListener(SetVibration);
        iconAccesstionceToggle.onValueChanged.AddListener(SetIconAccesstionce);
        visualAccessbilityToggle.onValueChanged.AddListener(SetVisualAccessbility);
    }

    private void LoadSettings()
    {
        mussicToggle.isOn = PlayerPrefs.GetInt("Mussic", 1) == 1;
        sfxToggle.isOn = PlayerPrefs.GetInt("SFX", 1) == 1;
        vibrationToggle.isOn = PlayerPrefs.GetInt("Vibration", 1) == 1;
        iconAccesstionceToggle.isOn = PlayerPrefs.GetInt("IconAccesstionce", 1) == 1;
        visualAccessbilityToggle.isOn = PlayerPrefs.GetInt("VisualAccessbility", 1) == 1;
    }

    private void SetMussic(bool value)
    {
        PlayerPrefs.SetInt("Mussic", value ? 1 : 0);
        PlayerPrefs.Save();
    }
    private void SetSFX(bool value)
    {
        PlayerPrefs.SetInt("SFX", value ? 1 : 0);
        PlayerPrefs.Save();
    }
    private void SetVibration(bool value)
    {
        PlayerPrefs.SetInt("Vibration", value ? 1 : 0);
        PlayerPrefs.Save();
    }
    private void SetIconAccesstionce(bool value)
    {
        PlayerPrefs.SetInt("IconAccesstionce", value ? 1 : 0);
        PlayerPrefs.Save();
    }
    private void SetVisualAccessbility(bool value)
    {
        PlayerPrefs.SetInt("VisualAccessbility", value ? 1 : 0);
        PlayerPrefs.Save();
    }
}
