using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
public class NavigationManager : MonoBehaviour
{
    public static NavigationManager Instance;

    [SerializeField] private GameObject winPanel;
    [SerializeField] private GameObject losePanel;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text coinText;

    public GameObject settingPanel;
    public GameObject homePanel;
    public GameObject shopPanel;
    public GameObject dailyMissonPanel;
    public GameObject rankingPanel;
    public GameObject eventPanel;

    public Button playButton;
    public GameObject middlePart;
    public GameObject reward;

    public GameObject navBotHome;
    public GameObject navBotShop;
    public GameObject navBotRanking;
    public GameObject panelOutOfHeart;
    private void Start()
    {
        RefreshLocalizedLevelLabel();

        if (CurrencyManager.Instance != null)
            UpdateCoinText(CurrencyManager.Instance.Money);
    }
    [Header("Panel Delay")]
    [SerializeField] private float panelDelay = 1.5f;



    private Coroutine panelCoroutine;

    private void Awake()
    {
        Instance = this;

        if (winPanel != null)
            winPanel.SetActive(false);

        if (losePanel != null)
            losePanel.SetActive(false);
    }

    public void ShowWinPanel()
    {
        if (panelCoroutine != null)
            StopCoroutine(panelCoroutine);

        panelCoroutine =
            StartCoroutine(
                ShowWinPanelDelayed()
            );
    }

    public void OnSettingButton()
    {
        if (GamePauseUI.Instance != null && GamePauseUI.Instance.gameObject.scene == gameObject.scene)
        {
            GamePauseUI.Instance.Pause();
            return;
        }
        AudioManager.Instance.PlaySFX(AudioManager.Instance.buttonClick);
        settingPanel.SetActive(true);

    }

    public void OnCloseOutOfHeartButton()
    {
        if (GamePauseUI.Instance != null && GamePauseUI.Instance.gameObject.scene == gameObject.scene)
        {
            GamePauseUI.Instance.ReturnHomeFromOutOfHearts();
            return;
        }
        HomeExperienceUI home = FindHomeUI();
        if (home != null)
        {
            home.CloseOutOfHearts();
            return;
        }
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(AudioManager.Instance.buttonClick);
        if (panelOutOfHeart != null) panelOutOfHeart.SetActive(false);
    }
    public void OnOpenOutOfHeartButton()
    {
        if (GamePauseUI.Instance != null && GamePauseUI.Instance.gameObject.scene == gameObject.scene)
        {
            GamePauseUI.Instance.PresentOutOfHearts();
            return;
        }
        HomeExperienceUI home = FindHomeUI();
        if (home != null)
        {
            home.ShowOutOfHearts();
            return;
        }
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(AudioManager.Instance.buttonClick);
        if (panelOutOfHeart != null) panelOutOfHeart.SetActive(true);
    }

    private HomeExperienceUI FindHomeUI()
    {
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            HomeExperienceUI home = root.GetComponentInChildren<HomeExperienceUI>(true);
            if (home != null) return home;
        }
        return null;
    }
    public void OnCloseSettingButton()
    {
        if (GamePauseUI.Instance != null && GamePauseUI.Instance.gameObject.scene == gameObject.scene)
        {
            GamePauseUI.Instance.Resume();
            return;
        }
        AudioManager.Instance.PlaySFX(AudioManager.Instance.buttonClick);
        settingPanel?.SetActive(false);

    }
    public void HideUI()
    {
        playButton.gameObject.SetActive(false);
        reward.gameObject.SetActive(false);
        middlePart.gameObject.SetActive(false);
    }
    public void UnHideUI()
    {

        playButton.gameObject.SetActive(true);
        reward.gameObject.SetActive(true);
        middlePart.gameObject.SetActive(true);
    }
    public void OnOpenHome()
    {
        AudioManager.Instance.PlaySFX(AudioManager.Instance.buttonClick);
        UnHideUI();
        rankingPanel.SetActive(false);
        shopPanel.SetActive(false);
        navBotHome.SetActive(true);
        navBotShop.SetActive(false);
        navBotRanking.SetActive(false);
    }
    public void OnOpenShopButton()
    {
        AudioManager.Instance.PlaySFX(AudioManager.Instance.buttonClick);
        HideUI();
        shopPanel.SetActive(true);
        rankingPanel.SetActive(false);
        navBotHome.SetActive(false);
        navBotShop.SetActive(true);
        navBotRanking.SetActive(false);
    }
    public void OnCloseShopButton()
    {
        AudioManager.Instance.PlaySFX(AudioManager.Instance.buttonClick);
        shopPanel.SetActive(false);

    }
    public void OnOpenDailyMissonButton()
    {
        AudioManager.Instance.PlaySFX(AudioManager.Instance.buttonClick);
        dailyMissonPanel.SetActive(true);
    }
    public void OnCloseDailyMissonButton()
    {
        AudioManager.Instance.PlaySFX(AudioManager.Instance.buttonClick);
        dailyMissonPanel.SetActive(false);
    }
    public void OnOpenRankingButton()
    {
        AudioManager.Instance.PlaySFX(AudioManager.Instance.buttonClick);
        rankingPanel.SetActive(true);
        shopPanel.SetActive(false);

        HideUI();
        navBotHome.SetActive(false);
        navBotShop.SetActive(false);
        navBotRanking.SetActive(true);
    }
    public void OnCloseRankingButton()
    {
        AudioManager.Instance.PlaySFX(AudioManager.Instance.buttonClick);
        rankingPanel.SetActive(false);
    }
    public void OnOpenEventButton()
    {
        AudioManager.Instance.PlaySFX(AudioManager.Instance.buttonClick);
        eventPanel.SetActive(true);
    }
    public void OnCloseEventButton()
    {
        AudioManager.Instance.PlaySFX(AudioManager.Instance.buttonClick);
        eventPanel.SetActive(false);
    }

    private IEnumerator ShowWinPanelDelayed()
    {
        yield return new WaitForSeconds(panelDelay);

        if (losePanel != null)
            losePanel.SetActive(false);

        if (winPanel != null)
            winPanel.SetActive(true);

        if (GamePauseUI.Instance != null)
            GamePauseUI.Instance.PresentVictory();

        panelCoroutine = null;
    }

    public void HideWinPanel()
    {
        if (winPanel != null)
            winPanel.SetActive(false);
    }

    public void ShowLosePanel()
    {
        Debug.Log(
            "===== SHOW LOSE PANEL CALLED ====="
        );

        if (panelCoroutine != null)
            StopCoroutine(panelCoroutine);

        panelCoroutine =
            StartCoroutine(
                ShowLosePanelDelayed()
            );
    }

    private IEnumerator ShowLosePanelDelayed()
    {
        Debug.Log(
            "[LOSE PANEL] Đang chờ " +
            panelDelay +
            " giây..."
        );

        yield return new WaitForSeconds(panelDelay);

        // The board may change while the panel delay runs (last impacts, collapse or a booster).
        if (GunGridManager.Instance == null || !GunGridManager.Instance.CanPresentLosePanel())
        {
            panelCoroutine = null;
            yield break;
        }

        if (winPanel != null)
            winPanel.SetActive(false);

        if (losePanel == null)
        {
            Debug.LogError(
                "[LOSE PANEL] losePanel = NULL!"
            );

            panelCoroutine = null;
            yield break;
        }

        if (GridManager.Instance != null)
            GridManager.Instance.RecordDailyGameFinished();
        losePanel.SetActive(true);

        if (GamePauseUI.Instance != null)
            GamePauseUI.Instance.PresentLoss();

        Debug.Log(
            "[LOSE PANEL] ĐÃ SET ACTIVE TRUE"
        );

        panelCoroutine = null;
    }

    public void HideLosePanel()
    {
        if (losePanel != null)
            losePanel.SetActive(false);
    }

    public void OnPlayAgainButton()
    {
        if (GamePauseUI.Instance != null)
            GamePauseUI.Instance.Restart();
    }
    public void OnPlayGame()
    {
        if (!HeartManager.CanPlay)
        {
            OnOpenOutOfHeartButton();
            return;
        }
        AudioManager.Instance.PlaySFX(AudioManager.Instance.buttonClick);
        if (GridManager.Instance != null) GridManager.Instance.PrepareForSceneExit();
        SceneManager.LoadScene("Game");
    }
    public void OnNextButton()
    {
        if (!HeartManager.CanPlay)
        {
            OnOpenOutOfHeartButton();
            return;
        }
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(AudioManager.Instance.buttonClick);
        Debug.Log(
            "===== NEXT BUTTON ====="
        );

        if (LevelManager.Instance == null)
        {
            Debug.LogError(
                "NEXT: LevelManager.Instance = NULL"
            );

            return;
        }

        Debug.Log(
            $"NEXT: CurrentLevel = " +
            $"{LevelManager.Instance.CurrentLevel}"
        );

        if (!LevelManager.Instance.HasNextLevel())
        {
            Debug.LogWarning(
                "NEXT: Không có level tiếp theo."
            );

            return;
        }

        if (GamePauseUI.Instance != null)
            GamePauseUI.Instance.PrepareForNextLevel();
        HideWinPanel();

        LevelManager.Instance.LoadCurrentLevel();

        Debug.Log(
            "NEXT: Đã gọi LoadCurrentLevel()"
        );
    }
    private void OnEnable()
    {
        Localize.LanguageChanged += RefreshLocalizedLevelLabel;
        RefreshLocalizedLevelLabel();
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnMoneyChanged += UpdateCoinText;
    }

    private void OnDisable()
    {
        Localize.LanguageChanged -= RefreshLocalizedLevelLabel;
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnMoneyChanged -= UpdateCoinText;
    }

    private void RefreshLocalizedLevelLabel()
    {
        if (levelText != null && LevelManager.Instance != null)
            levelText.text = Localize.Format("CẤP ĐỘ {0}", "LEVEL {0}", LevelManager.Instance.CurrentLevel);
    }

    public void UpdateCoinText(int amount)
    {
        if (coinText != null)
            coinText.text = amount.ToString("N0");
    }
}
