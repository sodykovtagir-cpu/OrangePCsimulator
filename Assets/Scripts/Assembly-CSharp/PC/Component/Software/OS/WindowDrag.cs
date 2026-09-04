using UnityEngine;
using UnityEngine.EventSystems;

public class WindowDrag : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    // Отступы от краёв рабочей области, за которые окно утащить нельзя.
    // Снизу отступ больше — чтобы окно (и заголовок) не уходило за панель задач.
    private const float SideInset = 16f;
    private const float TopInset = 16f;
    private const float BottomInset = 64f;

    private RectTransform window;
    private RectTransform parentRect;
    private Canvas canvas;
    private Camera uiCamera;
    private Vector2 pointerOffset;

    private static readonly Vector3[] cornerBuffer = new Vector3[4];

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

        // Сначала ставим окно под курсор, затем жёстко удерживаем его внутри.
        window.anchoredPosition = localPointerPos - pointerOffset;
        ClampInsideParent();
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
    /// Удерживает окно ЦЕЛИКОМ в пределах родителя по всем четырём сторонам
    /// (снизу — с учётом панели задач). Считает реальные мировые углы окна и
    /// переводит их в координаты родителя, поэтому корректно работает при любом
    /// pivot/anchor и любом масштабе канваса (WorldSpace и ScreenSpaceOverlay).
    /// </summary>
    private void ClampInsideParent()
    {
        Rect pr = parentRect.rect;

        window.GetWorldCorners(cornerBuffer);
        // [0] — низ-лево, [2] — верх-право.
        Vector2 bl = parentRect.InverseTransformPoint(cornerBuffer[0]);
        Vector2 tr = parentRect.InverseTransformPoint(cornerBuffer[2]);

        float leftBound = pr.xMin + SideInset;
        float rightBound = pr.xMax - SideInset;
        float bottomBound = pr.yMin + BottomInset;
        float topBound = pr.yMax - TopInset;

        Vector2 shift = Vector2.zero;

        // Если окно шире/выше доступной области — по этой оси не зажимаем,
        // чтобы два противоположных ограничителя не конфликтовали.
        bool fitsX = (tr.x - bl.x) <= (rightBound - leftBound);
        bool fitsY = (tr.y - bl.y) <= (topBound - bottomBound);

        if (fitsX)
        {
            if (bl.x < leftBound) shift.x += leftBound - bl.x;
            else if (tr.x > rightBound) shift.x -= tr.x - rightBound;
        }

        if (fitsY)
        {
            if (bl.y < bottomBound) shift.y += bottomBound - bl.y;
            else if (tr.y > topBound) shift.y -= tr.y - topBound;
        }

        // Сдвиг в локальных единицах родителя равен смещению anchoredPosition.
        if (shift != Vector2.zero)
            window.anchoredPosition += shift;
    }
}
