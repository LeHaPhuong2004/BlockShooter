
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class BoosterTutorialPanel : MonoBehaviour
{
    public static BoosterTutorialPanel Instance;

    [Header("Targeting UI")]
    [SerializeField] private GameObject targetingRoot;
    [SerializeField] private RectTransform targetReticle;
    [SerializeField] private Image targetReticleImage;

    [Header("Secondary Icon")]
    [SerializeField] private Image secondaryIcon;
    [SerializeField] private Sprite scissorsSprite;
    [SerializeField] private Sprite handSprite;

    [Header("Animation Settings")]
    [SerializeField] private float finishDuration = 0.25f;

    private BuffType currentBuff = BuffType.None;

    private Canvas canvas;
    private Camera worldCamera;

    private bool targeting;
    private bool pointerHeld;
    private bool busy;

    private int armedFrame;

    public bool IsOpen => targeting;

    private void Awake()
    {
        Instance = this;

        if (targetingRoot != null)
        {
            canvas = targetingRoot.GetComponentInParent<Canvas>();
            targetingRoot.SetActive(false);
        }

        worldCamera = Camera.main;

        if (targetReticle != null && targetReticleImage == null)
        {
            targetReticleImage = targetReticle.GetComponent<Image>();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // ==========================================
    // UPDATE
    // ==========================================

    private void Update()
    {
        if (!targeting || busy)
            return;

        // Không nhận lại click vừa kích hoạt booster.
        if (Time.frameCount < armedFrame)
            return;

        HandleInput();
    }

    // ==========================================
    // OPEN TARGETING
    // ==========================================

    public void Open(BuffType buffType)
    {
        if (targetingRoot == null ||
            targetReticle == null ||
            secondaryIcon == null)
        {
            Debug.LogError(
                "[Booster] Thiếu TargetingRoot, TargetReticle hoặc SecondaryIcon!"
            );

            if (BuffManager.Instance != null)
                BuffManager.Instance.CancelTargetedBooster();

            return;
        }

        if (canvas == null)
        {
            canvas = targetingRoot.GetComponentInParent<Canvas>();
        }

        if (canvas == null)
        {
            Debug.LogError("[Booster] Không tìm thấy Canvas!");

            if (BuffManager.Instance != null)
                BuffManager.Instance.CancelTargetedBooster();

            return;
        }

        currentBuff = buffType;

        targeting = true;
        pointerHeld = false;
        busy = false;

        targetingRoot.SetActive(true);
        targetingRoot.transform.SetAsLastSibling();

        // Hiển thị tâm ngắm.
        targetReticle.gameObject.SetActive(true);
        targetReticle.DOKill();

        if (targetReticleImage != null)
        {
            targetReticleImage.enabled = true;
            targetReticleImage.raycastTarget = false;
            targetReticleImage.color = Color.white;
        }

        // Hiển thị icon phụ bằng Sprite.
        secondaryIcon.gameObject.SetActive(true);
        secondaryIcon.DOKill();

        secondaryIcon.enabled = true;
        secondaryIcon.raycastTarget = false;
        secondaryIcon.color = Color.white;

        // Không thay đổi RectTransform hoặc Scale của SecondaryIcon.
        // Giữ nguyên kích thước đã thiết lập trong Inspector.

        SetIconSprite(buffType);

        // Đặt tâm ngắm tại vị trí chuột hiện tại.
        MoveReticle(Input.mousePosition);

        // Bỏ qua click kích hoạt booster.
        armedFrame = Time.frameCount + 1;

        Debug.Log(
            "[Booster] Targeting started: " + buffType
        );
    }

    // ==========================================
    // SET ICON SPRITE
    // ==========================================

    private void SetIconSprite(BuffType buffType)
    {
        if (secondaryIcon == null)
            return;

        Sprite selectedSprite = null;

        if (buffType == BuffType.SplitGun)
        {
            selectedSprite = scissorsSprite;
        }
        else if (buffType == BuffType.TakeBelowGun)
        {
            selectedSprite = handSprite;
        }

        if (selectedSprite == null)
        {
            Debug.LogError(
                "[Booster] Chưa gán Sprite cho booster: " + buffType
            );

            secondaryIcon.enabled = false;
            return;
        }

        secondaryIcon.sprite = selectedSprite;
        secondaryIcon.enabled = true;

        Debug.Log(
            "[Booster] Set icon sprite: " + buffType
        );
    }

    // ==========================================
    // INPUT
    // ==========================================

    private void HandleInput()
    {
        // MOBILE TOUCH
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            switch (touch.phase)
            {
                case TouchPhase.Began:

                    pointerHeld = true;
                    MoveReticle(touch.position);

                    break;

                case TouchPhase.Moved:
                case TouchPhase.Stationary:

                    if (pointerHeld)
                        MoveReticle(touch.position);

                    break;

                case TouchPhase.Ended:

                    if (pointerHeld)
                    {
                        pointerHeld = false;

                        MoveReticle(touch.position);
                        ResolveTarget(touch.position);
                    }

                    break;

                case TouchPhase.Canceled:

                    if (pointerHeld)
                    {
                        pointerHeld = false;
                        InvalidTarget();
                    }

                    break;
            }

            return;
        }

        // PC MOUSE

        // Luôn đi theo chuột dù không giữ chuột.
        MoveReticle(Input.mousePosition);

        if (Input.GetMouseButtonDown(0))
        {
            pointerHeld = true;
        }

        if (pointerHeld && Input.GetMouseButtonUp(0))
        {
            pointerHeld = false;

            Vector2 releasePosition = Input.mousePosition;

            MoveReticle(releasePosition);
            ResolveTarget(releasePosition);
        }

        if (pointerHeld && !Application.isFocused)
        {
            pointerHeld = false;
            InvalidTarget();
        }
    }

    // ==========================================
    // MOVE RETICLE
    // ==========================================

    private void MoveReticle(Vector2 screenPosition)
    {
        if (targetReticle == null || canvas == null)
            return;

        RectTransform parentRect =
            targetReticle.parent as RectTransform;

        if (parentRect == null)
            return;

        Camera uiCamera = null;

        if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            uiCamera = canvas.worldCamera != null
                ? canvas.worldCamera
                : Camera.main;
        }

        bool converted =
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect,
                screenPosition,
                uiCamera,
                out Vector2 localPosition
            );

        if (converted)
        {
            targetReticle.anchoredPosition = localPosition;
        }
    }

    // ==========================================
    // RESOLVE TARGET
    // ==========================================

    private void ResolveTarget(Vector2 screenPosition)
    {
        if (!targeting || busy)
            return;

        Debug.Log("[Booster] Checking target...");

        Gun targetGun =
            FindGunAtScreenPosition(screenPosition);

        if (targetGun == null)
        {
            InvalidTarget();
            return;
        }

        if (BuffManager.Instance == null)
        {
            InvalidTarget();
            return;
        }

        // SPLIT GUN
        if (currentBuff == BuffType.SplitGun)
        {
            if (targetGun.chain == null ||
                !targetGun.chain.IsPair)
            {
                InvalidTarget();
                return;
            }

            busy = true;
            targeting = false;
            pointerHeld = false;

            Debug.Log("[Booster] Valid SplitGun target!");

            BuffManager.Instance.SelectGunForSplit(targetGun);

            FinishTargetingAnimation();

            return;
        }

        // TAKE BELOW GUN
        if (currentBuff == BuffType.TakeBelowGun)
        {
            if (!targetGun.inGrid ||
                targetGun.lockedInTray)
            {
                InvalidTarget();
                return;
            }

            busy = true;
            targeting = false;
            pointerHeld = false;

            Debug.Log("[Booster] Valid TakeBelowGun target!");

            BuffManager.Instance.SelectGunForTakeBelow(targetGun);

            FinishTargetingAnimation();

            return;
        }

        InvalidTarget();
    }

    // ==========================================
    // INVALID TARGET
    // ==========================================

    private void InvalidTarget()
    {
        if (busy)
            return;

        Debug.Log(
            "[Booster] Invalid target - Cancel booster!"
        );

        busy = true;
        targeting = false;
        pointerHeld = false;

        if (BuffManager.Instance != null)
        {
            BuffManager.Instance.CancelTargetedBooster();
        }

        FinishTargetingAnimation();
    }

    // ==========================================
    // CANCEL
    // ==========================================

    public void Cancel()
    {
        InvalidTarget();
    }

    // ==========================================
    // FINISH ANIMATION
    // ==========================================

    private void FinishTargetingAnimation()
    {
        if (targetingRoot == null)
        {
            CloseAfterAction();
            return;
        }

        if (targetReticle != null)
            targetReticle.DOKill();

        if (targetReticleImage != null)
            targetReticleImage.DOKill();

        if (secondaryIcon != null)
            secondaryIcon.DOKill();

        Sequence sequence = DOTween.Sequence()
            .SetUpdate(true);

        if (targetReticle != null)
        {
            sequence.Join(
                targetReticle.DOScale(
                    Vector3.zero,
                    finishDuration
                ).SetEase(Ease.InBack)
            );
        }

        if (targetReticleImage != null)
        {
            sequence.Join(
                targetReticleImage.DOFade(
                    0f,
                    finishDuration
                )
            );
        }

        if (secondaryIcon != null)
        {
            // Chỉ làm mờ icon, không thay đổi kích thước.
            sequence.Join(
                secondaryIcon.DOFade(
                    0f,
                    finishDuration
                )
            );
        }

        sequence.OnComplete(CloseAfterAction);
    }

    // ==========================================
    // CLOSE
    // ==========================================

    public void CloseAfterAction()
    {
        currentBuff = BuffType.None;

        targeting = false;
        pointerHeld = false;
        busy = false;

        if (targetingRoot != null)
        {
            targetingRoot.SetActive(false);
        }

        ResetTargetingUI();
    }

    // ==========================================
    // RESET UI
    // ==========================================

    private void ResetTargetingUI()
    {
        if (targetReticle != null)
        {
            targetReticle.DOKill();

            targetReticle.localScale = Vector3.one;
            targetReticle.anchoredPosition = Vector2.zero;
        }

        if (targetReticleImage != null)
        {
            targetReticleImage.DOKill();

            targetReticleImage.color = Color.white;
            targetReticleImage.enabled = true;
        }

        if (secondaryIcon != null)
        {
            secondaryIcon.DOKill();

            secondaryIcon.color = Color.white;
            secondaryIcon.enabled = true;

            // Không reset Scale hoặc kích thước của SecondaryIcon.
        }
    }

    // ==========================================
    // FIND GUN
    // ==========================================

    private Gun FindGunAtScreenPosition(Vector2 screenPosition)
    {
        Camera cam = worldCamera != null
            ? worldCamera
            : Camera.main;

        if (cam == null)
        {
            Debug.LogError(
                "[Booster] Không tìm thấy Camera.main!"
            );

            return null;
        }

        Ray ray = cam.ScreenPointToRay(screenPosition);

        Plane gamePlane = new Plane(
            Vector3.forward,
            Vector3.zero
        );

        if (!gamePlane.Raycast(ray, out float distance))
            return null;

        Vector3 worldPoint = ray.GetPoint(distance);

        Collider2D[] hits =
            Physics2D.OverlapPointAll(worldPoint);

        foreach (Collider2D hit in hits)
        {
            Gun gun = hit.GetComponentInParent<Gun>();

            if (gun != null)
                return gun;
        }

        return null;
    }
}
