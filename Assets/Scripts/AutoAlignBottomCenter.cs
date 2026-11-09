using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.UI;

public class AutoAlignBottomCenter : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        float ratio = 1f / PortraitDisplay.ContentAspect;

        if (ratio > 1.9f)
        {
            this.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 30f);
        }
        else
        {
            this.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 0f);
        }
    }

    private void OnRectTransformDimensionsChange()
    {
        float ratio = 1f / PortraitDisplay.ContentAspect;

        if (ratio > 1.9f)
        {
            this.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 30f);
        }
        else
        {
            this.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 0f);
        }

    }
}
