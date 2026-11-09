using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("Food Goal UI")]
    [SerializeField] private TMP_Text amountText;
    [SerializeField] private GameObject tickObject;
    [SerializeField] private Image foodIcon;

    [Header("Food Icons")]
    [SerializeField] private Sprite banhMiIcon;
    [SerializeField] private Sprite friedChickenIcon;

    private void Awake()
    {
        if (tickObject != null)
            tickObject.SetActive(false);
    }

    private void Start()
    {
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.RegisterFoodUIFromUI(
                this
            );
        }
    }

    public void Initialize(
        BlockType foodType,
        int required
    )
    {
        if (foodIcon != null)
        {
            Sprite icon = null;

            if (
                foodType ==
                BlockType.BanhMi
            )
            {
                icon =
                    banhMiIcon;
            }
            else if (
                foodType ==
                BlockType.FriedChicken
            )
            {
                icon =
                    friedChickenIcon;
            }

            foodIcon.sprite =
                icon;

            foodIcon.enabled =
                icon != null;
        }

        if (amountText != null)
        {
            amountText.gameObject.SetActive(
                true
            );

            amountText.text =
                required.ToString();
        }

        if (tickObject != null)
            tickObject.SetActive(false);
    }
    
    public void UpdateUI(
        int collected,
        int required
    )
    {
        int remaining =
            Mathf.Max(
                0,
                required - collected
            );

        Debug.Log(
            $"[UI] UpdateUI: " +
            $"{collected}/{required} " +
            $"-> còn {remaining}"
        );

        if (remaining <= 0)
        {
            if (amountText != null)
            {
                amountText.gameObject.SetActive(
                    false
                );
            }

            if (tickObject != null)
                tickObject.SetActive(
                    true
                );

            return;
        }

        if (amountText != null)
        {
            amountText.gameObject.SetActive(
                true
            );

            amountText.text =
                remaining.ToString();
        }

        if (tickObject != null)
            tickObject.SetActive(
                false
            );
    }
}