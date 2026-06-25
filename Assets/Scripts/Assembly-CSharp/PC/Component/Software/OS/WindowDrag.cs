using UnityEngine;
using UnityEngine.EventSystems;

public class WindowDrag : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    private RectTransform window;
    private Vector2 offset;

    void Awake()
    {
        // Находим родительское окно (где висит App)
        var app = GetComponentInParent<PC.Component.Software.App>();
        if (app != null)
            window = app.GetComponent<RectTransform>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (window == null) return;

        window.SetAsLastSibling(); // выводим окно поверх других

        // вычисляем смещение курсора
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            window,
            eventData.position,
            null,
            out offset);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (window == null) return;

        Vector2 pos;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            window.parent as RectTransform,
            eventData.position,
            null,
            out pos);

        window.localPosition = pos - offset;
    }

}