using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RewardPointUI : MonoBehaviour
{
    [Header("References")]
    public Image chestIcon;
    public TMP_Text pointText;

    [Header("Config")]
    public int requiredPoint;

    private void Start()
    {
        // Tự hiển thị mốc điểm của mình
        if (pointText != null)
            pointText.text = requiredPoint.ToString();
    }

    public void UpdateState(int currentPoint)
    {
        if (chestIcon == null) return;

        bool unlocked = currentPoint >= requiredPoint;
        chestIcon.color = unlocked ? Color.white : new Color(0.4f, 0.4f, 0.4f, 1f);
    }
}