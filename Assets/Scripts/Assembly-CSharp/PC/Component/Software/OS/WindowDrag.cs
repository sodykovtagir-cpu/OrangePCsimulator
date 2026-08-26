using UnityEngine;
using UnityEngine.EventSystems;

public class WindowDrag : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    private RectTransform window;
    private RectTransform parentRect;
    private Canvas canvas;
    private Camera uiCamera;
    private Vector2 pointerOffset;

    void Awake()
    {
        var app = GetComponentInParent<PC.Component.Software.App>();
        if (app != null)
            window = app.GetComponent<RectTransform>();

        if (window != null)
        {
            parentRect = window.parent as RectTransform;
            canvas = window.GetComponentInParent<Canvas>();

            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                uiCamera = canvas.worldCamera;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (window == null) return;

        window.SetAsLastSibling();

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            window, eventData.position, uiCamera, out pointerOffset);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!enabled || window == null || parentRect == null) return;

        Vector2 localPointerPos;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect, eventData.position, uiCamera, out localPointerPos))
            return;

        window.anchoredPosition = localPointerPos - pointerOffset;
    }
}