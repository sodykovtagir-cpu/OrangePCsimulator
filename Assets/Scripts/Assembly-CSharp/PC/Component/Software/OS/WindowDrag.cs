using UnityEngine;
using UnityEngine.EventSystems;

public class WindowDrag : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    // Отступы рабочей области, за пределы которой окно утащить нельзя.
    // Снизу отступ больше — чтобы заголовок окна не уходил за панель задач.
    private const float EdgeInset = 8f;
    private const float BottomInset = 60f;

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

        // Развёрнутое (максимизированное) окно перетаскивать нельзя.
        if (IsStretched())
            return;

        Vector2 localPointerPos;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect, eventData.position, uiCamera, out localPointerPos))
            return;

        Vector2 target = localPointerPos - pointerOffset;
        window.anchoredPosition = ClampToParent(target);
    }

    /// <summary>
    /// Окно считается «растянутым» (maximized), если его якоря занимают
    /// практически весь родительский прямоугольник. Такое окно не двигаем.
    /// </summary>
    private bool IsStretched()
    {
        return (window.anchorMax.x - window.anchorMin.x) > 0.99f
            && (window.anchorMax.y - window.anchorMin.y) > 0.99f;
    }

    /// <summary>
    /// Ограничивает позицию окна так, чтобы оно целиком (вместе с заголовком)
    /// оставалось в пределах родителя и не уходило за панель задач.
    /// Работает в координатах anchoredPosition (учитывает pivot окна).
    /// </summary>
    private Vector2 ClampToParent(Vector2 pos)
    {
        Rect pr = parentRect.rect;
        Rect wr = window.rect;

        Vector2 pivot = window.pivot;

        // Допустимый диапазон позиции якоря (pivot) окна внутри родителя.
        float minX = pr.xMin + EdgeInset + pivot.x * wr.width;
        float maxX = pr.xMax - EdgeInset - (1f - pivot.x) * wr.width;
        float minY = pr.yMin + BottomInset + pivot.y * wr.height;
        float maxY = pr.yMax - EdgeInset - (1f - pivot.y) * wr.height;

        // Если окно больше рабочей области — хотя бы не даём ему уехать полностью.
        if (minX > maxX) { float c = (minX + maxX) * 0.5f; minX = maxX = c; }
        if (minY > maxY) { float c = (minY + maxY) * 0.5f; minY = maxY = c; }

        return new Vector2(
            Mathf.Clamp(pos.x, minX, maxX),
            Mathf.Clamp(pos.y, minY, maxY));
    }
}
