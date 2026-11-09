using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Controls the popup GameObjects authored in the Game scene.</summary>
public sealed class GamePauseUI : MonoBehaviour
{
    public static GamePauseUI Instance { get; private set; }

    [Header("Authored popup roots")]
    [SerializeField] private GameObject overlay;
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private GameObject confirmationPanel;
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private GameObject lossPanel;
    [SerializeField] private GameObject outOfHeartPanel;

    [Header("Selection and labels")]
    [SerializeField] private Button pauseButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private TMP_Text confirmationMessage;
    [SerializeField] private TMP_Text settingsLevelLabel;
    [SerializeField] private TMP_Text victoryLevelLabel;
    [SerializeField] private TMP_Text lossLevelLabel;
    [Header("Victory reward")]
    [SerializeField] private TMP_Text victoryRewardAmountLabel;
    [SerializeField] private Button victoryDoubleRewardButton;
    [SerializeField] private Button victoryClaimRewardButton;
    [SerializeField] private TMP_Text victoryRewardStatusLabel;
    private VictoryRewardClaim victoryReward = new VictoryRewardClaim();
    private int victoryRewardLevel = -1;
    private string victoryRewardFeedback;

    [Header("Simulated reward ad until an Ad Unit ID is configured")]
    [SerializeField] private GameObject simulatedAdPanel;
    [SerializeField] private TMP_Text simulatedAdCountdownLabel;
    [SerializeField] private Button simulatedAdClaimButton;
    [SerializeField] private Button simulatedAdCloseButton;
    private const float SimulatedAdDuration = 5f;
    private VictoryRewardClaim simulatedAdRequest;
    private float simulatedAdRemaining;

    [Header("Authored settings states")]
    [SerializeField] private GameObject musicOn;
    [SerializeField] private GameObject musicOff;
    [SerializeField] private GameObject soundOn;
    [SerializeField] private GameObject soundOff;
    [SerializeField] private GameObject vibrationOn;
    [SerializeField] private GameObject vibrationOff;

    private bool paused;
    private bool leavingScene;
    private float previousTimeScale = 1f;
    private string pendingScene;
    private GameObject activePanel;
    private GameObject panelBeforeConfirmation;
    private bool wasPausedBeforeConfirmation;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInstance() => Instance = null;

    private void Awake()
    {
        Instance = this;
        HidePopupRoots();
    }

    private void Start() => RefreshSettings();

    private void Update() => TickSimulatedVictoryAd(Time.unscaledDeltaTime);

    private void OnEnable()
    {
        Localize.LanguageChanged += RefreshLocalizedLabels;
        RefreshLocalizedLabels();
    }

    public void Pause()
    {
        if (paused || leavingScene || !CanPresent(menuPanel)) return;
        if (!PauseTime()) return;
        PlayClick();
        RefreshSettings();
        SetLevelLabel(settingsLevelLabel, CurrentLevel());
        ShowPanel(menuPanel);
        Select(continueButton);
    }

    public void Resume()
    {
        if (!paused || leavingScene || activePanel != menuPanel) return;
        PlayClick();
        pendingScene = null;
        HidePopupRoots();
        RestoreTime();
        Select(pauseButton);
    }

    public void PresentVictory()
    {
        if (leavingScene || !CanPresent(victoryPanel) || !PauseTime()) return;
        pendingScene = null;
        int completedLevel = Mathf.Max(1, CurrentLevel() - 1);
        if (victoryRewardLevel != completedLevel)
        {
            victoryReward.CancelAd();
            HideSimulatedVictoryAd();
            victoryReward = new VictoryRewardClaim();
            victoryRewardLevel = completedLevel;
            victoryRewardFeedback = null;
        }
        RefreshVictoryLabels();
        ShowPanel(victoryPanel);
        Select(null);
    }

    public void PresentLoss()
    {
        if (!HeartManager.CanPlay)
        {
            PresentOutOfHearts();
            return;
        }
        if (leavingScene || !CanPresent(lossPanel) || !PauseTime()) return;
        pendingScene = null;
        SetLevelLabel(lossLevelLabel, CurrentLevel());
        ShowPanel(lossPanel);
        Select(null);
    }

    public void PresentOutOfHearts()
    {
        if (leavingScene) return;
        if (outOfHeartPanel == null)
        {
            Debug.LogError("Assign the authored out-of-hearts panel in the Game scene.", this);
            return;
        }
        if (!PauseTime()) return;
        pendingScene = null;
        panelBeforeConfirmation = null;
        ShowPanel(outOfHeartPanel);
        Select(null);
    }

    public void ReturnHomeFromOutOfHearts()
    {
        if (leavingScene || activePanel != outOfHeartPanel) return;
        LoadScene("Home");
    }

    public void Restart() => RequestSceneChange(gameObject.scene.name);
    public void ExitToMenu() => RequestSceneChange("Home");

    public void RequestSceneChange(string sceneName)
    {
        if (leavingScene || string.IsNullOrEmpty(sceneName)) return;
        // A confirmed result has already finished the heart transaction.
        if (!HeartManager.RoundInProgress)
        {
            if (sceneName != "Home" && !HeartManager.CanPlay)
            {
                PresentOutOfHearts();
                return;
            }
            if (!PauseTime()) return;
            LoadScene(sceneName);
            return;
        }
        if (activePanel == confirmationPanel || !CanPresent(confirmationPanel)) return;
        wasPausedBeforeConfirmation = paused;
        panelBeforeConfirmation = activePanel;
        if (!PauseTime()) return;
        PlayClick();
        pendingScene = sceneName;
        RefreshConfirmationMessage();
        ShowPanel(confirmationPanel);
        Select(null);
    }

    private void RefreshConfirmationMessage()
    {
        if (string.IsNullOrEmpty(pendingScene)) return;
        string action = pendingScene == "Home" ? Localize.Text("về trang chủ", "Return home") : Localize.Text("chơi lại", "Retry this round");
        if (confirmationMessage != null)
        {
            if (HeartManager.IsUnlimited)
                confirmationMessage.text = Localize.Format("Bạn muốn {0}?\nTim vô hạn đang hoạt động.",
                    "{0}?\nUnlimited hearts are active.", action);
            else if (!HeartManager.RoundInProgress)
                confirmationMessage.text = HeartManager.CanPlay
                    ? Localize.Format("Bạn muốn {0}?\nVán đã kết thúc, không trừ thêm tim.",
                        "{0}?\nThe round has ended. No extra heart will be spent.", action)
                    : Localize.Format("Bạn đã hết tim.\nChờ hồi tim ở trang chủ: {0}.",
                        "You are out of hearts.\nWait for hearts to refill at Home: {0}.", HeartManager.TimerText);
            else
                confirmationMessage.text = Localize.Text("Bạn sẽ mất 1 tim.", "You will lose 1 heart.")
                    + (pendingScene != "Home" && HeartManager.Hearts == 1
                        ? Localize.Text("\nBạn cần thêm tim để chơi lại.", "\nYou need more hearts to retry.") : "");
        }
    }

    private void RefreshVictoryLabels()
    {
        // Progression advances before NavigationManager presents the result.
        if (victoryLevelLabel != null)
            victoryLevelLabel.text = Localize.Format("HOÀN THÀNH CẤP ĐỘ {0}", "LEVEL {0} COMPLETE", Mathf.Max(1, CurrentLevel() - 1));
        RefreshVictoryRewardUI();
    }

    private void RefreshVictoryRewardUI()
    {
        if (victoryRewardAmountLabel != null)
            victoryRewardAmountLabel.text = VictoryRewardClaim.BaseAmount.ToString();
        if (victoryDoubleRewardButton != null)
        {
            victoryDoubleRewardButton.interactable = !victoryReward.Claimed && !victoryReward.AdPending;
            SetVictoryButtonText(victoryDoubleRewardButton, "Label", Localize.Text("NHẬN X2", "CLAIM X2"));
            SetVictoryButtonText(victoryDoubleRewardButton, "Ad Caption", Localize.Text("XEM QUẢNG CÁO", "WATCH AD"));
        }
        if (victoryClaimRewardButton != null)
        {
            victoryClaimRewardButton.interactable = !victoryReward.AdPending;
            SetVictoryButtonText(victoryClaimRewardButton, "Label", victoryReward.Claimed
                ? Localize.Text("TIẾP TỤC", "CONTINUE") : Localize.Text("NHẬN & TIẾP", "CLAIM & NEXT"));
        }
        if (victoryRewardStatusLabel != null)
            victoryRewardStatusLabel.text = victoryReward.AdPending
                ? Localize.Text("ĐANG CHỜ QUẢNG CÁO...", "WAITING FOR AD...")
                : Localize.Translate(victoryRewardFeedback);
    }

    private static void SetVictoryButtonText(Button button, string name, string text)
    {
        Transform child = button.transform.Find(name);
        TMP_Text label = child != null ? child.GetComponent<TMP_Text>() : null;
        if (label != null) label.text = text;
    }

    public void ClaimVictoryRewardAndContinue()
    {
        if (!Application.isPlaying || leavingScene || activePanel != victoryPanel || victoryReward.AdPending) return;
        victoryReward.TryClaimBase(CurrencyManager.AwardMoney);
        RefreshVictoryRewardUI();
        ContinueAfterVictory();
    }

    public void ClaimDoubleVictoryReward()
    {
        if (!Application.isPlaying || leavingScene || activePanel != victoryPanel || !victoryReward.TryBeginAd()) return;
        PlayClick();
        victoryRewardFeedback = null;
        RefreshVictoryRewardUI();
        VictoryRewardClaim request = victoryReward;
        if (Application.isEditor || GameMaster.staticScript == null || !GameMaster.staticScript.HasConfiguredRewardedAdUnit)
        {
            if (StartSimulatedVictoryAd(request)) return;
        }
        bool started = GameMaster.staticScript != null && GameMaster.staticScript.TryShowRewardedAd(
            () => CompleteVictoryAd(request, true), () => CompleteVictoryAd(request, false));
        if (started) return;
        request.CancelAd();
        victoryRewardFeedback = "Quảng cáo chưa sẵn sàng. Bạn có thể nhận 100 hoặc thử lại.";
        RefreshVictoryRewardUI();
    }

    private bool StartSimulatedVictoryAd(VictoryRewardClaim request)
    {
        if (simulatedAdPanel == null || request != victoryReward || !request.AdPending) return false;
        simulatedAdRequest = request;
        simulatedAdRemaining = SimulatedAdDuration;
        simulatedAdPanel.transform.SetAsLastSibling();
        Localize.Apply(simulatedAdPanel.transform);
        simulatedAdPanel.SetActive(true);
        RefreshSimulatedVictoryAdUI();
        return true;
    }

    private void TickSimulatedVictoryAd(float deltaTime)
    {
        if (simulatedAdRequest == null || simulatedAdPanel == null || !simulatedAdPanel.activeSelf) return;
        simulatedAdRemaining = Mathf.Max(0f, simulatedAdRemaining - Mathf.Max(0f, deltaTime));
        RefreshSimulatedVictoryAdUI();
    }

    private void RefreshSimulatedVictoryAdUI()
    {
        if (simulatedAdPanel == null || !simulatedAdPanel.activeSelf) return;
        if (simulatedAdCountdownLabel != null)
            simulatedAdCountdownLabel.text = simulatedAdRemaining > 0f
                ? Localize.Format("Thời gian còn lại: {0}s", "Time remaining: {0}s", Mathf.CeilToInt(simulatedAdRemaining))
                : Localize.Text("Đã xem xong. Bạn có thể nhận x2!", "Finished. You can claim x2!");
        if (simulatedAdClaimButton != null)
        {
            simulatedAdClaimButton.interactable = simulatedAdRequest != null && simulatedAdRemaining <= 0f;
            SetVictoryButtonText(simulatedAdClaimButton, "Label", Localize.Text("NHẬN X2", "CLAIM X2"));
        }
    }

    public void FinishSimulatedVictoryAd()
    {
        if (!Application.isPlaying || simulatedAdRequest == null || simulatedAdRemaining > 0f) return;
        VictoryRewardClaim request = simulatedAdRequest;
        HideSimulatedVictoryAd();
        CompleteVictoryAd(request, true);
    }

    public void CancelSimulatedVictoryAd()
    {
        if (simulatedAdRequest == null) return;
        PlayClick();
        VictoryRewardClaim request = simulatedAdRequest;
        HideSimulatedVictoryAd();
        CompleteVictoryAd(request, false);
    }

    private void HideSimulatedVictoryAd()
    {
        if (simulatedAdPanel != null) simulatedAdPanel.SetActive(false);
        simulatedAdRequest = null;
        simulatedAdRemaining = 0f;
    }

    private void CompleteVictoryAd(VictoryRewardClaim request, bool earned)
    {
        if (this == null || leavingScene || request != victoryReward || activePanel != victoryPanel) return;
        if (!request.AdPending) return;
        bool claimed = request.CompleteAd(earned, CurrencyManager.AwardMoney);
        victoryRewardFeedback = claimed ? null : "Chưa nhận thưởng x2. Bạn có thể thử lại hoặc nhận 100.";
        RefreshVictoryRewardUI();
        if (claimed) ContinueAfterVictory();
    }

    private void RefreshLocalizedLabels()
    {
        if (activePanel == null) return;
        if (activePanel == menuPanel) SetLevelLabel(settingsLevelLabel, CurrentLevel());
        else if (activePanel == victoryPanel) RefreshVictoryLabels();
        else if (activePanel == lossPanel) SetLevelLabel(lossLevelLabel, CurrentLevel());
        else if (activePanel == confirmationPanel) RefreshConfirmationMessage();
        RefreshSimulatedVictoryAdUI();
    }

    public void ConfirmLeave()
    {
        if (string.IsNullOrEmpty(pendingScene) || leavingScene) return;
        HeartManager.AbandonRound();
        string destination = pendingScene;
        pendingScene = null;
        if (destination != "Home" && !HeartManager.CanPlay)
        {
            PresentOutOfHearts();
            return;
        }
        LoadScene(destination);
    }

    public void CancelLeave()
    {
        if (leavingScene || activePanel != confirmationPanel) return;
        PlayClick();
        pendingScene = null;
        if (wasPausedBeforeConfirmation && panelBeforeConfirmation != null)
        {
            ShowPanel(panelBeforeConfirmation);
            Select(panelBeforeConfirmation == menuPanel ? continueButton : null);
        }
        else
        {
            HidePopupRoots();
            RestoreTime();
            Select(pauseButton);
        }
        panelBeforeConfirmation = null;
    }

    public void NextLevel()
    {
        ClaimVictoryRewardAndContinue();
    }

    private void ContinueAfterVictory()
    {
        if (leavingScene || activePanel != victoryPanel) return;
        if (LevelManager.Instance != null && !LevelManager.Instance.HasNextLevel())
        {
            LoadScene("Home");
            return;
        }
        if (NavigationManager.Instance == null) return;
        // NavigationManager checks hearts and level availability before closing the popup.
        NavigationManager.Instance.OnNextButton();
    }

    public void PrepareForNextLevel()
    {
        victoryReward.CancelAd();
        HideSimulatedVictoryAd();
        pendingScene = null;
        panelBeforeConfirmation = null;
        HidePopupRoots();
        RestoreTime();
        Select(null);
    }

    // Retained for replay validation and full scene cleanup on retry.
    private void LoadScene(string sceneName)
    {
        if (!paused || leavingScene) return;
        PlayClick();
        leavingScene = true;
        if (sceneName == gameObject.scene.name && LevelManager.Instance != null)
            LevelManager.Instance.PrepareRestartCurrentLevel();
        HidePopupRoots();
        RestoreTime();
        if (GridManager.Instance != null) GridManager.Instance.PrepareForSceneExit();
        SceneManager.LoadScene(sceneName);
    }

    public void ToggleMusic()
    {
        bool enabled = !ReadSetting("Mussic", MusicDefault());
        SaveSetting("Mussic", enabled);
        ApplyAudioSetting(true, enabled);
        SetState(musicOn, musicOff, enabled);
        PlayClick();
    }

    public void ToggleSound()
    {
        bool enabled = !ReadSetting("SFX", SoundDefault());
        if (!enabled) PlayClick();
        SaveSetting("SFX", enabled);
        ApplyAudioSetting(false, enabled);
        SetState(soundOn, soundOff, enabled);
        if (enabled) PlayClick();
    }

    public void ToggleVibration()
    {
        bool enabled = !ReadSetting("Vibration", 1);
        SaveSetting("Vibration", enabled);
        SetState(vibrationOn, vibrationOff, enabled);
        PlayClick();
    }

    private void RefreshSettings()
    {
        bool music = ReadSetting("Mussic", MusicDefault());
        bool sound = ReadSetting("SFX", SoundDefault());
        bool vibration = ReadSetting("Vibration", 1);
        SetState(musicOn, musicOff, music);
        SetState(soundOn, soundOff, sound);
        SetState(vibrationOn, vibrationOff, vibration);
        ApplyAudioSetting(true, music);
        ApplyAudioSetting(false, sound);
    }

    private static bool ReadSetting(string key, int defaultValue) => PlayerPrefs.GetInt(key, defaultValue) == 1;
    private static int MusicDefault() => GameMaster.staticScript != null ? GameMaster.MusicOnOff : 1;
    private static int SoundDefault() => GameMaster.staticScript != null ? GameMaster.SoundOnOff : 1;

    private static void SaveSetting(string key, bool enabled)
    {
        PlayerPrefs.SetInt(key, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    private static void ApplyAudioSetting(bool music, bool enabled)
    {
        float volume = enabled ? 1f : 0f;
        if (AudioManager.Instance != null)
        {
            if (music) AudioManager.Instance.SetMusicVolume(volume);
            else AudioManager.Instance.SetSFXVolume(volume);
        }
        if (GameMaster.staticScript != null)
        {
            if (music) GameMaster.MusicOnOff = enabled ? 1 : 0;
            else GameMaster.SoundOnOff = enabled ? 1 : 0;
        }
    }

    private static void SetState(GameObject on, GameObject off, bool enabled)
    {
        if (on != null) on.SetActive(enabled);
        if (off != null) off.SetActive(!enabled);
    }

    private bool CanPresent(GameObject panel)
    {
        if (overlay != null && panel != null) return true;
        Debug.LogError("Game popup references must be assigned in the Game scene.", this);
        return false;
    }

    private bool PauseTime()
    {
        if (paused) return true;
        if (Time.timeScale <= 0f) return false;
        previousTimeScale = Time.timeScale;
        paused = true;
        Time.timeScale = 0f;
        return true;
    }

    private void RestoreTime()
    {
        if (!paused) return;
        Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
        paused = false;
    }

    private void ShowPanel(GameObject panel)
    {
        if (panel != victoryPanel)
        {
            victoryReward.CancelAd();
            HideSimulatedVictoryAd();
        }
        SetPanelActive(menuPanel, panel);
        SetPanelActive(confirmationPanel, panel);
        SetPanelActive(victoryPanel, panel);
        SetPanelActive(lossPanel, panel);
        SetPanelActive(outOfHeartPanel, panel);
        activePanel = panel;
        if (overlay != null) overlay.SetActive(panel != outOfHeartPanel);
    }

    private static void SetPanelActive(GameObject panel, GameObject selected)
    {
        if (panel != null) panel.SetActive(panel == selected);
    }

    private void HidePopupRoots()
    {
        HideSimulatedVictoryAd();
        SetPanelActive(menuPanel, null);
        SetPanelActive(confirmationPanel, null);
        SetPanelActive(victoryPanel, null);
        SetPanelActive(lossPanel, null);
        SetPanelActive(outOfHeartPanel, null);
        if (overlay != null) overlay.SetActive(false);
        activePanel = null;
    }

    private static int CurrentLevel() => LevelManager.Instance != null ? Mathf.Max(1, LevelManager.Instance.CurrentLevel) : 1;

    private static void SetLevelLabel(TMP_Text label, int level)
    {
        if (label != null) label.text = Localize.Format("CẤP ĐỘ {0}", "LEVEL {0}", level);
    }

    private static void Select(Button button)
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(button != null ? button.gameObject : null);
    }

    private static void PlayClick()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(AudioManager.Instance.buttonClick);
    }

    private void OnDisable()
    {
        Localize.LanguageChanged -= RefreshLocalizedLabels;
        victoryReward.CancelAd();
        HideSimulatedVictoryAd();
        RestoreTime();
        HidePopupRoots();
    }

    private void OnDestroy()
    {
        RestoreTime();
        if (Instance == this) Instance = null;
    }
}
