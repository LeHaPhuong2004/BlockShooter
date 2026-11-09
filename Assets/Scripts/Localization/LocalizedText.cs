using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Refreshes an authored label when its page or popup is opened.</summary>
[DisallowMultipleComponent]
public sealed class LocalizedText : MonoBehaviour
{
    private void OnEnable()
    {
        Localize.LanguageChanged -= Refresh;
        Localize.LanguageChanged += Refresh;
        Refresh();
    }

    private void OnDisable() => Localize.LanguageChanged -= Refresh;

    public void Refresh()
    {
        if (TryGetComponent(out Text legacy)) Localize.ApplyLabel(legacy);
        if (TryGetComponent(out TMP_Text modern)) Localize.ApplyLabel(modern);
    }
}
