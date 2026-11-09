using UnityEngine;

public sealed partial class HomeExperienceUI
{
    private const float SelectedNavigationIconScale = 1.16f;

    // Retain authored scales across a Play Mode script reload without recapturing the zoom.
    [SerializeField, HideInInspector] private RectTransform[] navigationScaleSources;
    [SerializeField, HideInInspector] private Vector3[] navigationAuthoredScales;

    private void UpdateNavigationIconSelection(float deltaTime, bool instant = false)
    {
        if (tabIconRects == null) return;
        if (navigationScaleSources == null || navigationAuthoredScales == null ||
            navigationScaleSources.Length != tabIconRects.Length || navigationAuthoredScales.Length != tabIconRects.Length)
        {
            navigationScaleSources = new RectTransform[tabIconRects.Length];
            navigationAuthoredScales = new Vector3[tabIconRects.Length];
        }

        float blend = 1f - Mathf.Exp(-18f * Mathf.Max(0f, deltaTime));
        for (int index = 0; index < tabIconRects.Length; index++)
        {
            RectTransform icon = tabIconRects[index];
            if (icon == null) continue;
            bool captured = navigationScaleSources[index] != icon;
            if (captured)
            {
                navigationScaleSources[index] = icon;
                navigationAuthoredScales[index] = icon.localScale;
            }
            Vector3 target = navigationAuthoredScales[index] * (index == activeTabIndex ? SelectedNavigationIconScale : 1f);
            icon.localScale = captured || instant ? target : Vector3.Lerp(icon.localScale, target, blend);
        }
    }
}
