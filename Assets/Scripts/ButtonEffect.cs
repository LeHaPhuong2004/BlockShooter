using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ButtonEffect : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private Transform target;
    private Vector3 originScale;
    private Vector3 targetScale;

    private float pressScale = 0.8f;
    private float scaleSpeed = 10f;

    private void Awake()
    {
        target = transform;
        originScale = target.localScale;
        targetScale = originScale;
    }
    private void Update()
    {
        if(target.localScale != targetScale)
        {
            target.localScale = Vector3.Lerp(target.localScale, targetScale, Time.deltaTime * scaleSpeed);
        }
    }
    public void OnPointerDown(PointerEventData eventData)
    {
        targetScale = originScale * pressScale;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        targetScale = originScale;
    }


}
