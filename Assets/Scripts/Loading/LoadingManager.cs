using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class LoadingManager : MonoBehaviour
{
    [SerializeField] private Slider slider;
    [SerializeField] private TMP_Text loadingText;
    [SerializeField] private string sceneName = "Home";
    [SerializeField] private float loadSpeed = 0.5f;
    [SerializeField] private float waitAt100 = 1f;
    private Image backgroundImage;

    private void Start()
    {
        ApplyLoadingBackground();
        StartCoroutine(LoadScene());
    }

    private void ApplyLoadingBackground()
    {
        var backgroundSprite = Resources.Load<Sprite>("UI/BG");
        if (backgroundSprite == null) return;

        var images = FindObjectsOfType<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] != null && images[i].gameObject.name == "Image")
            {
                backgroundImage = images[i];
                break;
            }
        }

        if (backgroundImage == null)
        {
            float largestArea = -1f;
            for (int i = 0; i < images.Length; i++)
            {
                var candidate = images[i];
                if (candidate == null || (slider != null &&
                    (candidate.transform == slider.transform || candidate.transform.IsChildOf(slider.transform))))
                    continue;

                var rect = candidate.rectTransform.rect;
                float area = rect.width * rect.height;
                if (area > largestArea)
                {
                    largestArea = area;
                    backgroundImage = candidate;
                }
            }
        }

        if (backgroundImage == null) return;

        var backgroundRect = backgroundImage.rectTransform;
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        backgroundRect.localScale = Vector3.one;
        backgroundImage.sprite = backgroundSprite;
        backgroundImage.color = Color.white;
        backgroundImage.type = Image.Type.Simple;
        ResponsiveSceneLayout.CoverImage(backgroundImage);
        backgroundImage.raycastTarget = false;
        backgroundImage.transform.SetAsFirstSibling();
    }

    private IEnumerator LoadScene()
    {
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        operation.allowSceneActivation = false;

        while (operation.progress < 0.9f)
        {
            float progress = operation.progress / 0.9f;
            slider.value = progress;
            loadingText.text = Mathf.RoundToInt(progress * 100f) + "%";
            yield return null;
        }

        while (slider.value < 1f)
        {
            slider.value = Mathf.MoveTowards(slider.value, 1f, Time.deltaTime * loadSpeed);
            loadingText.text = Mathf.RoundToInt(slider.value * 100f) + "%";
            yield return null;
        }

        slider.value = 1f;
        loadingText.text = "100%";

        yield return new WaitForSeconds(waitAt100);

        operation.allowSceneActivation = true;
    }
}
