using UnityEngine;

public sealed partial class HomeExperienceUI
{
    [Header("Authored out-of-hearts panel")]
    [SerializeField] private GameObject outOfHeartPanel;

    private void BindOutOfHeartPanel()
    {
        if (outOfHeartPanel == null)
            outOfHeartPanel = SceneObject("OutOfHeartPanel") ?? SceneObject("OutOfHearPanel");
        if (outOfHeartPanel == null)
            throw new System.InvalidOperationException("Assign the existing out-of-hearts panel in the Home scene.");
        outOfHeartPanel.SetActive(false);
    }

    public void ShowOutOfHearts()
    {
        if (!sceneHomeBound)
        {
            LoadHomeResources();
            if (!BindSceneHome()) return;
        }
        CloseSpecialLevelPopup();
        if (settingsPopupRoot != null) settingsPopupRoot.SetActive(false);
        if (boosterPurchasePopup != null) boosterPurchasePopup.SetActive(false);
        outOfHeartPanel.transform.SetAsLastSibling();
        outOfHeartPanel.SetActive(true);
    }

    public void CloseOutOfHearts()
    {
        if (outOfHeartPanel == null || !outOfHeartPanel.activeSelf) return;
        PlayButtonSfx();
        outOfHeartPanel.SetActive(false);
    }

    public void OpenHeartShop()
    {
        CloseOutOfHearts();
        ShowShop();
    }
}
