using UnityEngine;
using UnityEngine.EventSystems;

public class WindowDrag : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    // Отступы от краёв рабочей области, за которые НЕЛЬЗЯ вытащить заголовок.
    // Снизу отступ больше — чтобы заголовок не уходил за панель задач.
    private const float SideInset = 8f;
    private const float TopInset = 8f;
    private const float BottomInset = 64f;

    // Перетаскиваемый элемент — тот, на котором висит WindowDrag (обычно «Title»).
    private RectTransform handle;
    // Окно, которое реально двигается (корень приложения).
    private RectTransform window;
    private RectTransform parentRect;
    private Canvas canvas;
    private Camera uiCamera;
    private Vector2 pointerOffset;

    private static readonly Vector3[] cornerBuffer = new Vector3[4];
    private static readonly Vector3[] windowCornerBuffer = new Vector3[4];

    void Awake()
    {
        handle = transform as RectTransform;

        var app = GetComponentInParent<PC.Component.Software.App>();
        if (app != null)
            window = app.GetComponent<RectTransform>();

        // Если корень-приложение не найден — двигаем сам заголовок.
        if (window == null)
            window = handle;

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

        // Сначала ставим окно под курсор, затем жёстко удерживаем ЗАГОЛОВОК внутри.
        window.anchoredPosition = localPointerPos - pointerOffset;
        ClampHandleInside();
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
    /// Удерживает ЗАГОЛОВОК (handle) целиком в пределах рабочей области по всем
    /// сторонам; снизу — с отступом под панель задач. Считает реальные мировые
    /// углы заголовка и переводит их в координаты родителя, поэтому корректно
    /// работает при любом pivot/anchor и любом режиме канваса.
    /// </summary>
    private void ClampHandleInside()
    {
        Rect pr = parentRect.rect;

        var rt = handle != null ? handle : window;
        rt.GetWorldCorners(cornerBuffer);
        window.GetWorldCorners(windowCornerBuffer);

        // Углы заголовка в координатах родителя: [0] — низ-лево, [2] — верх-право.
        Vector2 titleBL = parentRect.InverseTransformPoint(cornerBuffer[0]);
        Vector2 titleTR = parentRect.InverseTransformPoint(cornerBuffer[2]);
        // Углы всего окна (для боковых границ).
        Vector2 winBL = parentRect.InverseTransformPoint(windowCornerBuffer[0]);
        Vector2 winTR = parentRect.InverseTransformPoint(windowCornerBuffer[2]);

        float leftBound = pr.xMin + SideInset;
        float rightBound = pr.xMax - SideInset;
        float bottomBound = pr.yMin + BottomInset;
        float topBound = pr.yMax - TopInset;

        Vector2 shift = Vector2.zero;

        float winW = winTR.x - winBL.x;
        float titleH = titleTR.y - titleBL.y;

        // Горизонталь — держим КОРПУС окна (он шире заголовка), чтобы за боковые
        // края не вылезало вообще ничего.
        bool fitsX = winW <= (rightBound - leftBound);
        if (fitsX)
        {
            if (winBL.x < leftBound) shift.x += leftBound - winBL.x;
            else if (winTR.x > rightBound) shift.x -= winTR.x - rightBound;
        }

        // Вертикаль — держим ЗАГОЛОВОК: его верх не за экран, низ не под таскбар.
        bool fitsY = titleH <= (topBound - bottomBound);
        if (fitsY)
        {
            if (titleBL.y < bottomBound) shift.y += bottomBound - titleBL.y;
            else if (titleTR.y > topBound) shift.y -= titleTR.y - topBound;
        }

        // Заголовок жёстко привязан к окну, поэтому сдвиг в локальных единицах
        // родителя равен смещению anchoredPosition окна.
        if (shift != Vector2.zero)
            window.anchoredPosition += shift;
    }
}
