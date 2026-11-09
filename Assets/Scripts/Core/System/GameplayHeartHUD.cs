using TMPro;
using UnityEngine;

/// <summary>Updates the heart counter authored on the gameplay HUD.</summary>
public sealed class GameplayHeartHUD : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    private float nextRefresh;

    private void OnEnable() => RefreshCount();
    private void Start() => RefreshCount();

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        RefreshCount();
    }

    private void RefreshCount()
    {
        nextRefresh = Time.unscaledTime + 0.5f;
        if (label != null) label.text = HeartManager.CountText;
    }
}
