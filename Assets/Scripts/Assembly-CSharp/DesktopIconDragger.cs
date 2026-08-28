using UnityEngine;
using UnityEngine.EventSystems;

namespace PC.Component.Software
{
    public class DesktopIconDragger : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler, IEventSystemHandler
    {
        private RectTransform rectTransform;
        private Canvas parentCanvas;
        private RectTransform canvasRect;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas != null)
                canvasRect = parentCanvas.GetComponent<RectTransform>();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (rectTransform == null || parentCanvas == null) return;

            if (canvasRect == null)
            {
                parentCanvas = GetComponentInParent<Canvas>();
                if (parentCanvas != null)
                    canvasRect = parentCanvas.GetComponent<RectTransform>();
            }

            if (canvasRect == null) return;

            Vector2 localPos;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, eventData.position, eventData.pressEventCamera, out localPos))
            {
                rectTransform.anchoredPosition = localPos;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
        }
    }
}
