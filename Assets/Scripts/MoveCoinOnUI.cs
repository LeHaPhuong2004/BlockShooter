using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Moves a UI coin along a short arc, typically from a shop reward to the HUD balance.
/// </summary>
public class MoveCoinOnUI : MonoBehaviour
{
    private RectTransform rect;
    private Image image;
    private Vector2 startPosition;
    private Vector2 endPosition;
    private Vector2 controlOffset;
    private float duration = 0.55f;
    private float delay;
    private float elapsed;
    private Action onArrived;
    private bool initialized;

    /// <summary>
    /// Starts a coin flight in the anchored-position space of this object's UI parent.
    /// </summary>
    public void StartFlight(Vector2 start, Vector2 end, float flightDuration, float startDelay,
        float arcHeight, Action arrived = null)
    {
        rect = GetComponent<RectTransform>();
        image = GetComponent<Image>();
        if (rect == null)
        {
            Destroy(gameObject);
            return;
        }

        startPosition = start;
        endPosition = end;
        duration = Mathf.Max(0.12f, flightDuration);
        delay = Mathf.Max(0f, startDelay);
        elapsed = 0f;
        controlOffset = Vector2.up * (arcHeight * 2f);
        onArrived = arrived;
        rect.anchoredPosition = startPosition;
        initialized = true;
    }

    // Preserve the legacy entry point used by older scene scripts.
    public void StartMove(GameObject container, Vector2 start, Vector2 end, int coin,
        Vector2 velocity, float timeToMove, int target)
    {
        StartFlight(start, end, timeToMove, 0f, Mathf.Clamp(velocity.magnitude * 0.08f, 40f, 180f));
    }

    private void Update()
    {
        if (!initialized || rect == null) return;

        elapsed += Time.unscaledDeltaTime;
        if (elapsed < delay) return;

        float progress = Mathf.Clamp01((elapsed - delay) / duration);
        float inverse = 1f - progress;
        Vector2 midpoint = (startPosition + endPosition) * 0.5f + controlOffset;
        rect.anchoredPosition = inverse * inverse * startPosition
            + 2f * inverse * progress * midpoint
            + progress * progress * endPosition;

        // Keep the coin at full opacity and size while it flies so its highlights stay crisp.
        if (image != null)
        {
            Color color = image.color;
            color.a = 1f;
            image.color = color;
        }

        rect.localScale = Vector3.one;
        if (progress < 1f) return;

        onArrived?.Invoke();
        Destroy(gameObject);
    }
}
