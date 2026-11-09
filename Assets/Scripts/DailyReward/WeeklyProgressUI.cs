using UnityEngine;
using UnityEngine.UI;

public class WeeklyProgressUI : MonoBehaviour
{
    [Header("Progress")]
    public Image fillImage;
    public int currentPoint = 20;
    public int maxPoint = 50;

    [Header("Reward Points")]
    public RewardPointUI[] rewardPoints;

    private void Start()
    {
        UpdateProgress();
    }

    public void UpdateProgress()
    {
        fillImage.fillAmount = Mathf.Clamp01((float)currentPoint / maxPoint);

        foreach (RewardPointUI point in rewardPoints)
        {
            point.UpdateState(currentPoint);
        }
    }
}