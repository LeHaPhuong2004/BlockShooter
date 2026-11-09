using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>Updates a mission card without rebuilding the Home canvas.</summary>
public sealed class DailyMissionCardUI : MonoBehaviour
{
    private DailyMissionType mission;
    private TMP_Text progressText;
    private Image progressFill;
    private Button rewardButton;
    private GameObject[] rewardVisuals;
    private Vector2 fullFillAnchor;
    private bool configured;

    public void Bind(DailyMissionType type, TMP_Text label, Image fill, Button button, params GameObject[] visuals)
    {
        configured = false;
        if (label == null || fill == null || button == null) return;
        mission = type;
        progressText = label;
        progressFill = fill;
        rewardButton = button;
        rewardVisuals = visuals != null && visuals.Length > 0 ? visuals : new[] { button.gameObject };
        fullFillAnchor = fill.rectTransform.anchorMax;
        configured = true;
        Refresh();
    }

    private void OnEnable()
    {
        DailyMissionManager.Changed += Refresh;
        Refresh();
    }

    private void OnDisable() => DailyMissionManager.Changed -= Refresh;

    private void Refresh()
    {
        if (!configured || progressText == null || progressFill == null || rewardButton == null) return;
        int target = DailyMissionManager.GetTarget(mission);
        int current = Mathf.Clamp(DailyMissionManager.GetProgress(mission), 0, target);
        bool complete = current >= target;
        bool claimed = DailyMissionManager.IsClaimed(mission);
        progressText.text = current + "/" + target;
        RectTransform rect = progressFill.rectTransform;
        rect.anchorMax = new Vector2(Mathf.Lerp(rect.anchorMin.x, fullFillAnchor.x, current / (float)target), rect.anchorMax.y);
        progressFill.enabled = current > 0;
        progressFill.sprite = Resources.Load<Sprite>("UI/DailyMissions/DailyMissionProgressFill" + (complete ? "Green" : "Blue"));
        foreach (var visual in rewardVisuals)
            if (visual != null) visual.SetActive(!claimed);
        rewardButton.gameObject.SetActive(!claimed);
        rewardButton.interactable = complete && !claimed;
    }
}
