using UnityEngine;
using UnityEngine.UI;

public sealed partial class HomeExperienceUI
{
    private Image characterChoosingImage;
    private RectTransform characterChoosingRect;
    private bool characterChoosingVisible;
    private Vector3 characterChoosingVelocity;
    private float characterChoosingPulse;

    // Keep the authored alignment and color when scripts reload during Play.
    [SerializeField, HideInInspector] private RectTransform characterChoosingLayoutSource;
    [SerializeField, HideInInspector] private Vector3 characterChoosingCenterOffset;
    [SerializeField, HideInInspector] private Color characterChoosingBaseColor;

    private void BindCharacterSelectionHighlight(Transform page)
    {
        characterChoosingImage = SceneComponent<Image>("IsChoosing", page);
        characterChoosingRect = characterChoosingImage != null ? characterChoosingImage.rectTransform : null;
        characterChoosingVisible = false;
        characterChoosingVelocity = Vector3.zero;
        if (characterChoosingImage == null) return;
        characterChoosingImage.raycastTarget = false;
        characterChoosingRect.gameObject.SetActive(true);
    }

    private void UpdateCharacterSelectionHighlight(float deltaTime, bool instant = false)
    {
        if (characterChoosingRect == null || characterCardFrames == null || characterCardFrames.Length == 0) return;
        Transform parent = characterChoosingRect.parent;
        if (parent == null || !parent.gameObject.activeInHierarchy)
        {
            characterChoosingVisible = false;
            characterChoosingVelocity = Vector3.zero;
            if (characterChoosingLayoutSource == characterChoosingRect)
                characterChoosingImage.color = characterChoosingBaseColor;
            return;
        }

        int index = Mathf.Clamp(previewCharacterIndex, 0, characterCardFrames.Length - 1);
        Image selected = characterCardFrames[index];
        if (characterChoosingLayoutSource != characterChoosingRect)
            Canvas.ForceUpdateCanvases();
        if (selected == null || selected.rectTransform.rect.width <= 0f || selected.rectTransform.rect.height <= 0f) return;

        if (characterChoosingLayoutSource != characterChoosingRect)
        {
            Vector3 authoredCenter = parent.InverseTransformPoint(characterChoosingRect.TransformPoint(characterChoosingRect.rect.center));
            Vector3 nearestCenter = parent.InverseTransformPoint(selected.rectTransform.TransformPoint(selected.rectTransform.rect.center));
            float nearestDistance = float.PositiveInfinity;
            foreach (Image frame in characterCardFrames)
            {
                if (frame == null) continue;
                Vector3 center = parent.InverseTransformPoint(frame.rectTransform.TransformPoint(frame.rectTransform.rect.center));
                float distance = (authoredCenter - center).sqrMagnitude;
                if (distance >= nearestDistance) continue;
                nearestDistance = distance;
                nearestCenter = center;
            }
            characterChoosingCenterOffset = authoredCenter - nearestCenter;
            characterChoosingBaseColor = characterChoosingImage.color;
            characterChoosingLayoutSource = characterChoosingRect;
        }

        Vector3 targetCenter = parent.InverseTransformPoint(selected.rectTransform.TransformPoint(selected.rectTransform.rect.center))
            + characterChoosingCenterOffset;
        Vector3 targetPosition = targetCenter - characterChoosingRect.localRotation *
            Vector3.Scale(characterChoosingRect.rect.center, characterChoosingRect.localScale);
        if (!characterChoosingVisible || instant)
        {
            characterChoosingRect.localPosition = targetPosition;
            characterChoosingVelocity = Vector3.zero;
            if (!characterChoosingVisible) characterChoosingPulse = 0f;
        }
        else if (deltaTime > 0f)
        {
            characterChoosingRect.localPosition = Vector3.SmoothDamp(characterChoosingRect.localPosition,
                targetPosition, ref characterChoosingVelocity, .12f, float.PositiveInfinity, deltaTime);
        }
        characterChoosingVisible = true;
        characterChoosingPulse += Mathf.Max(0f, deltaTime);
        Color color = characterChoosingBaseColor;
        color.a *= .95f + .05f * Mathf.Cos(characterChoosingPulse * 4f);
        characterChoosingImage.color = color;
    }
}
