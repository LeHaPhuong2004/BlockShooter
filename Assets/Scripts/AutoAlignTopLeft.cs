using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AutoAlignTopLeft : MonoBehaviour
{

    // Start is called before the first frame update
    void Start()
    {
        float ratio = 1f / PortraitDisplay.ContentAspect;

        if (ratio > 1.95f)
        {
            this.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -70f);
        }
        else if (ratio < 1.1f)
        {
            //for landscape
            float delta = (1920f / ratio) / 2f  - 720f;
            this.GetComponent<RectTransform>().anchoredPosition = new Vector2(delta, 0f);
        }
        else
        {
            //float delta = (1920f / ratio) / 2f  -  540f;
            this.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 0f);
        }
    }

    private void OnRectTransformDimensionsChange()
    {
        float ratio = 1f / PortraitDisplay.ContentAspect;

        if (ratio > 1.95f)
        {
            this.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -70f);
        }
        else if (ratio < 1.1f)
        {
            //for landscape
            float delta = (1920f / ratio) / 2f  -  720f;
            this.GetComponent<RectTransform>().anchoredPosition = new Vector2(delta, 0f);
        }
        else
        {
            //float delta = (1920f / ratio) / 2f  -  540f;
            this.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 0f);
        }
    }
}
