using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class BoosterIconFX : MonoBehaviour, IPointerClickHandler
{
    [Header("Bắt buộc")]
    [SerializeField] private RectTransform icon;
    [SerializeField] private Image iconImage;

    [Header("Tùy chọn")]
    [SerializeField] private Image glow;
    [SerializeField] private ParticleSystem burst;

    [Header("Hiệu ứng bấm")]
    [SerializeField] private float punchScale = 0.25f;
    [SerializeField] private float shakeAngle = 12f;
    [SerializeField] private float duration = 0.35f;
    [SerializeField] private Color flashColor = Color.white;

    [Header("Hiệu ứng Selected")]
    [SerializeField] private float selectedJumpHeight = 15f;
    [SerializeField] private float selectedJumpDuration = 0.6f;
    [SerializeField] private float selectedGlowAlpha = 1f;
    [SerializeField] private float selectedGlowScale = 1.25f;

    private Vector3 baseScale;
    private Vector2 baseAnchoredPosition;
    private Quaternion baseRotation;

    private Sequence clickSequence;
    private Sequence selectedSequence;

    private bool selected = false;
    private static BoosterIconFX currentSelected;

    private void Awake()
    {
        if (icon == null)
            icon = transform as RectTransform;

        if (iconImage == null && icon != null)
            iconImage = icon.GetComponent<Image>();

        baseScale = icon.localScale;
        baseAnchoredPosition = icon.anchoredPosition;
        baseRotation = icon.localRotation;

        if (glow != null)
        {
            glow.raycastTarget = false;
            glow.gameObject.SetActive(false);

            Color c = glow.color;
            c.a = 0f;
            glow.color = c;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Play();
    }


    public void Play()
    {
        if (currentSelected != null && currentSelected != this)
            currentSelected.SetSelected(false);

        SetSelected(true);
        PlayClickEffect();

        if (BuffManager.Instance == null)
            return;

        BuffType buffType = BuffType.None;

        if (gameObject.name.Contains("Split") ||
            gameObject.name.Contains("Scissor"))
        {
            buffType = BuffType.SplitGun;
        }
        else if (gameObject.name.Contains("TakeBelow") ||
                 gameObject.name.Contains("Hand"))
        {
            buffType = BuffType.TakeBelowGun;
        }

        if (buffType != BuffType.None)
            BuffManager.Instance.UseBuff(buffType);
    }

    public void SetSelected(bool value)
    {
        // Tránh gọi lại hàm nếu trạng thái không thay đổi
        if (selected == value && value == true) return;

        selected = value;

        if (selected)
        {
            currentSelected = this;
            StartSelectedEffect();
        }
        else
        {
            if (currentSelected == this)
                currentSelected = null;

            StopSelectedEffect();
        }
    }

    private void StartSelectedEffect()
    {
        selectedSequence?.Kill();

        // Xử lý hiện Glow
        if (glow != null)
        {
            glow.gameObject.SetActive(true);
            glow.DOKill();

            Color c = glow.color;
            c.a = 0f;
            glow.color = c;
            glow.rectTransform.localScale = Vector3.one;

            glow.DOFade(selectedGlowAlpha, 0.2f).SetUpdate(true).SetLink(gameObject);
            glow.rectTransform.DOScale(Vector3.one * selectedGlowScale, 0.2f).SetUpdate(true).SetLink(gameObject);
        }

        // Tạo hiệu ứng nhún nhảy vô hạn bằng Yoyo
        selectedSequence = DOTween.Sequence()
            .SetUpdate(true)
            .SetLink(gameObject);

        selectedSequence.Append(
            icon.DOAnchorPosY(baseAnchoredPosition.y + selectedJumpHeight, selectedJumpDuration * 0.5f)
                .SetEase(Ease.OutQuad)
        );
        selectedSequence.Append(
            icon.DOAnchorPosY(baseAnchoredPosition.y, selectedJumpDuration * 0.5f)
                .SetEase(Ease.InQuad)
        );

        selectedSequence.SetLoops(-1, LoopType.Yoyo);
    }
    private void PlayClickEffect()
    {
        if (icon == null)
            return;

        clickSequence?.Kill();

        icon.DOKill();

        clickSequence = DOTween.Sequence()
            .SetUpdate(true)
            .SetLink(gameObject);

        clickSequence.Append(
            icon.DOScale(baseScale * (1f + punchScale), duration * 0.25f)
                .SetEase(Ease.OutQuad)
        );

        clickSequence.Append(
            icon.DOScale(baseScale, duration * 0.35f)
                .SetEase(Ease.OutBack)
        );

        clickSequence.Join(
            icon.DORotate(
                new Vector3(0f, 0f, shakeAngle),
                duration * 0.1f
            ).SetEase(Ease.OutSine)
        );

        clickSequence.Append(
            icon.DORotateQuaternion(baseRotation, duration * 0.2f)
                .SetEase(Ease.OutSine)
        );

        if (burst != null)
            burst.Play();

        if (iconImage != null)
        {
            iconImage.DOKill();
            iconImage.DOColor(flashColor, duration * 0.2f)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    if (iconImage != null)
                        iconImage.DOColor(Color.white, duration * 0.3f)
                            .SetUpdate(true);
                });
        }
    }
    private void StopSelectedEffect()
    {
        selectedSequence?.Kill();
        selectedSequence = null;

        // Trả icon về vị trí cũ một cách mượt mà
        icon.DOAnchorPosY(baseAnchoredPosition.y, 0.15f).SetUpdate(true);
        icon.DOScale(baseScale, 0.15f).SetUpdate(true);
        icon.DORotateQuaternion(baseRotation, 0.15f).SetUpdate(true);

        // Ẩn Glow
        if (glow != null)
        {
            glow.DOKill();
            glow.DOFade(0f, 0.15f).SetUpdate(true).OnComplete(() =>
            {
                if (!selected)
                {
                    glow.gameObject.SetActive(false);
                    glow.rectTransform.localScale = Vector3.one;
                }
            });
        }
    }

    private void OnDisable()
    {
        if (currentSelected == this)
            currentSelected = null;

        clickSequence?.Kill();
        selectedSequence?.Kill();
    }
}