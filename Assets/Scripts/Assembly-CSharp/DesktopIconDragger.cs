using UnityEngine;
using UnityEngine.EventSystems;

namespace PC.Component.Software
{
    public class DesktopIconDragger : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler, IEventSystemHandler
    {
        private RectTransform rectTransform;
        private RectTransform parentRect;
        private Vector2 dragOffset;
        private Camera eventCamera;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            var parent = transform.parent;
            if (parent != null)
                parentRect = parent.GetComponent<RectTransform>();
            
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
                eventCamera = canvas.worldCamera;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (rectTransform == null || parentRect == null || eventData == null) return;
            
            Vector2 localMousePos;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect, eventData.position, eventCamera, out localMousePos))
            {
                dragOffset = localMousePos - rectTransform.anchoredPosition;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (rectTransform == null || parentRect == null || eventData == null) return;

            Vector2 localMousePos;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect, eventData.position, eventCamera, out localMousePos))
            {
                rectTransform.anchoredPosition = localMousePos - dragOffset;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
        }
    }
}
