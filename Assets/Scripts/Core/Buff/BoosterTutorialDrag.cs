//using UnityEngine;
//using UnityEngine.EventSystems;

//public class BoosterTutorialDrag : MonoBehaviour,
//    IBeginDragHandler, IDragHandler, IEndDragHandler
//{
//    public void OnBeginDrag(PointerEventData eventData)
//    {
//        Debug.Log("[BoosterDrag] BeginDrag");

//        if (BoosterTutorialPanel.Instance != null)
//            BoosterTutorialPanel.Instance.BeginDrag(eventData);
//        else
//            Debug.LogError("[BoosterDrag] Không tìm thấy BoosterTutorialPanel.Instance");
//    }

//    public void OnDrag(PointerEventData eventData)
//    {
//        if (BoosterTutorialPanel.Instance != null)
//            BoosterTutorialPanel.Instance.Drag(eventData);
//    }

//    public void OnEndDrag(PointerEventData eventData)
//    {
//        Debug.Log("[BoosterDrag] EndDrag");

//        if (BoosterTutorialPanel.Instance != null)
//            BoosterTutorialPanel.Instance.EndDrag(eventData);
//    }
//}