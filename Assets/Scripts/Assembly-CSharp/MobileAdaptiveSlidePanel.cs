using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MobileAdaptiveSlidePanel : MonoBehaviour, IDragHandler, IEndDragHandler
{
    public enum SnapEdge
    {
        Left,
        Right,
        Top,
        Bottom
    }

    private enum EdgeAnchorSlot
    {
        Free = 0,
        StartCorner = 1,
        EndCorner = 2
    }

    [Header("ID сохранения")]
    [SerializeField]
    private string controlId = "FunctionsPanel";

    [Header("Родительская область")]
    [SerializeField]
    private RectTransform parentRect;

    [Header("Настройки панели")]
    [SerializeField]
    private float edgePadding = 8f;

    [SerializeField]
    private float animationTime = 0.2f;

    [SerializeField]
    private bool startOpened = false;

    [SerializeField]
    private bool forceOpenedInEditMode = true;

    [Header("Reset позиция")]
    [SerializeField]
    private bool useManualResetPosition = true;

    [SerializeField]
    private SnapEdge resetEdge = SnapEdge.Right;

    [SerializeField]
    [Range(0f, 1f)]
    private float resetT = 0.5f;

    [Header("Умная привязка панели к углам")]
    [SerializeField]
    private bool useSmartEdgeAnchors = true;

    [SerializeField]
    [Range(0.02f, 0.35f)]
    private float cornerSnapZone = 0.16f;

    [Header("Визуал")]
    [SerializeField]
    private bool counterRotateIcons = true;

    [SerializeField]
    private bool fadeWhenClosed = true;

    [SerializeField]
    private bool disableRaycastWhenClosed = true;

    [Header("Если не заполнить, скрипт сам найдёт Button внутри панели")]
    [SerializeField]
    private RectTransform[] iconRects;

    private readonly Vector3[] corners = new Vector3[4];
    private Vector3 authoredScale;
    private bool scaleCaptured;
    private Rect lastBounds;
    private Vector2 lastPanelSize;
    private bool lastEditMode;
    private bool layoutApplied;
    private RectTransform rect;
    private CanvasGroup canvasGroup;

    private SnapEdge edge;
    private float edgeT;
    private EdgeAnchorSlot edgeAnchorSlot = EdgeAnchorSlot.Free;

    private SnapEdge defaultEdge;
    private float defaultEdgeT;
    private EdgeAnchorSlot defaultSlot = EdgeAnchorSlot.Free;

    private bool opened;
    private Coroutine animationRoutine;
    private Coroutine reapplyRoutine;

    private string KeyEdge => "mobile_adaptive_panel_" + controlId + "_edge";
    private string KeyT => "mobile_adaptive_panel_" + controlId + "_t";
    private string KeySlot => "mobile_adaptive_panel_" + controlId + "_slot";

    private void Awake()
    {
        EnsureRefs();
        CollectIconsIfNeeded();

        CalculateDefaultPosition();

        opened = startOpened;

        LoadPosition();
        ApplyInstant();
    }

    /// <summary>
    /// Рантайм-установка на картах, где хотбар был свёрстан без этого компонента
    /// (Medium — единственная сцена, где он был добавлен вручную). Awake уже
    /// отработал на AddComponent, поэтому после назначения id/области повторяем
    /// загрузку сохранённой раскладки.
    /// </summary>
    public void Init(string id, RectTransform parent)
    {
        controlId = id;
        if (parent != null) parentRect = parent;

        EnsureRefs();
        CollectIconsIfNeeded();
        CalculateDefaultPosition();

        opened = startOpened;

        LoadPosition();
        ApplyInstant();
    }

    private void OnEnable()
    {
        if (!MobileCustomizeManager.EditMode)
        {
            StartCoroutine(LoadAndApplyNextFrame());
        }
        else
        {
            ReapplyNextFrame();
        }
    }

    private IEnumerator LoadAndApplyNextFrame()
    {
        yield return null;

        LoadPosition();
        ApplyInstant();

        yield return new WaitForEndOfFrame();

        LoadPosition();
        ApplyInstant();
    }

    private void LateUpdate()
    {
        if (!EnsureRefs()) return;
        // No global Canvas rebuild every frame while editing. Refit only when
        // layout/viewport actually changes (including orientation/SetResolution).
        if (!layoutApplied || lastBounds != parentRect.rect || lastPanelSize != rect.rect.size ||
            lastEditMode != MobileCustomizeManager.EditMode)
        {
            if (animationRoutine != null) StopCoroutine(animationRoutine);
            animationRoutine = null;
            ApplyInstant();
        }
    }

    private bool EnsureRefs()
    {
        if (rect == null)
            rect = GetComponent<RectTransform>();

        if (rect == null)
        {
            Debug.LogError(name + ": MobileAdaptiveSlidePanel должен висеть на UI объекте с RectTransform.");
            return false;
        }

        if (!scaleCaptured)
        {
            authoredScale = rect.localScale;
            scaleCaptured = true;
        }
        if (parentRect == null)
            parentRect = rect.parent as RectTransform;

        if (parentRect == null)
        {
            Canvas canvas = GetComponentInParent<Canvas>();

            if (canvas != null)
                parentRect = canvas.transform as RectTransform;
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();

            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        return parentRect != null;
    }

    private void CollectIconsIfNeeded()
    {
        if (iconRects != null && iconRects.Length > 0)
            return;

        Button[] buttons = GetComponentsInChildren<Button>(true);
        List<RectTransform> list = new List<RectTransform>();

        foreach (Button button in buttons)
        {
            if (button == null)
                continue;

            RectTransform rt = button.transform as RectTransform;

            if (rt == null)
                continue;

            if (rt == rect)
                continue;

            list.Add(rt);
        }

        iconRects = list.ToArray();
    }

    private void CalculateDefaultPosition()
    {
        if (!EnsureRefs())
            return;

        if (useManualResetPosition)
        {
            defaultEdge = resetEdge;
            defaultEdgeT = Mathf.Clamp01(resetT);
            defaultSlot = GetSlotFromT(defaultEdgeT);

            edge = defaultEdge;
            edgeT = defaultEdgeT;
            edgeAnchorSlot = defaultSlot;

            return;
        }

        Vector3 worldCenter = rect.TransformPoint(rect.rect.center);
        Vector2 localPoint = parentRect.InverseTransformPoint(worldCenter);

        SnapEdge calculatedEdge;
        float rawT;

        CalculateNearestEdge(localPoint, out calculatedEdge, out rawT);

        defaultEdge = calculatedEdge;
        defaultEdgeT = Mathf.Clamp01(rawT);
        defaultSlot = GetSlotFromT(defaultEdgeT);

        edge = defaultEdge;
        edgeT = defaultEdgeT;
        edgeAnchorSlot = defaultSlot;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!MobileCustomizeManager.EditMode)
            return;

        if (!EnsureRefs())
            return;

        Vector2 localPoint;

        bool ok = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect,
            eventData.position,
            eventData.pressEventCamera,
            out localPoint
        );

        if (!ok)
            return;

        SnapEdge newEdge;
        float rawT;

        CalculateNearestEdge(localPoint, out newEdge, out rawT);

        edge = newEdge;
        edgeT = Mathf.Clamp01(rawT);
        edgeAnchorSlot = GetSlotFromT(rawT);

        ApplyOpenPosition();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!MobileCustomizeManager.EditMode)
            return;

        SavePosition();
    }

    private void CalculateNearestEdge(Vector2 localPoint, out SnapEdge resultEdge, out float rawT)
    {
        Rect r = parentRect.rect;

        float leftDist = Mathf.Abs(localPoint.x - r.xMin);
        float rightDist = Mathf.Abs(localPoint.x - r.xMax);
        float bottomDist = Mathf.Abs(localPoint.y - r.yMin);
        float topDist = Mathf.Abs(localPoint.y - r.yMax);

        resultEdge = SnapEdge.Left;
        float min = leftDist;

        if (rightDist < min)
        {
            min = rightDist;
            resultEdge = SnapEdge.Right;
        }

        if (bottomDist < min)
        {
            min = bottomDist;
            resultEdge = SnapEdge.Bottom;
        }

        if (topDist < min)
        {
            resultEdge = SnapEdge.Top;
        }

        if (resultEdge == SnapEdge.Left || resultEdge == SnapEdge.Right)
            rawT = Mathf.InverseLerp(r.yMin, r.yMax, localPoint.y);
        else
            rawT = Mathf.InverseLerp(r.xMin, r.xMax, localPoint.x);

        rawT = Mathf.Clamp01(rawT);
    }

    private EdgeAnchorSlot GetSlotFromT(float rawT)
    {
        if (!useSmartEdgeAnchors)
            return EdgeAnchorSlot.Free;

        if (rawT <= cornerSnapZone)
            return EdgeAnchorSlot.StartCorner;

        if (rawT >= 1f - cornerSnapZone)
            return EdgeAnchorSlot.EndCorner;

        return EdgeAnchorSlot.Free;
    }

    private float GetEffectiveT()
    {
        if (!useSmartEdgeAnchors)
            return ClampEdgeT(edge, edgeT);

        switch (edgeAnchorSlot)
        {
            case EdgeAnchorSlot.StartCorner:
                return 0f;

            case EdgeAnchorSlot.EndCorner:
                return 1f;

            default:
                return ClampEdgeT(edge, edgeT);
        }
    }

    private float ClampEdgeT(SnapEdge targetEdge, float t)
    {
        if (!EnsureRefs())
            return Mathf.Clamp01(t);

        if (edgeAnchorSlot == EdgeAnchorSlot.StartCorner)
            return 0f;

        if (edgeAnchorSlot == EdgeAnchorSlot.EndCorner)
            return 1f;

        Rect parent = parentRect.rect;

        float panelLength = GetPanelLengthAlongEdge(targetEdge);

        float parentLength =
            targetEdge == SnapEdge.Left || targetEdge == SnapEdge.Right
                ? parent.height
                : parent.width;

        if (parentLength <= 0f)
            return 0.5f;

        float half = panelLength * 0.5f + edgePadding;

        if (half * 2f >= parentLength)
            return 0.5f;

        float min = half / parentLength;
        float max = 1f - min;

        return Mathf.Clamp(t, min, max);
    }

    private float GetPanelLengthAlongEdge(SnapEdge targetEdge)
    {
        if (!EnsureRefs())
            return 0f;

        Rect bounds = MeasureBounds();
        return targetEdge == SnapEdge.Left || targetEdge == SnapEdge.Right ? bounds.height : bounds.width;
    }

    private Vector2 GetAnchor()
    {
        float t = GetEffectiveT();

        switch (edge)
        {
            case SnapEdge.Left:
                return new Vector2(0f, t);

            case SnapEdge.Right:
                return new Vector2(1f, t);

            case SnapEdge.Bottom:
                return new Vector2(t, 0f);

            case SnapEdge.Top:
                return new Vector2(t, 1f);
        }

        return new Vector2(1f, 0.5f);
    }

    private float GetRotationForEdge()
    {
        switch (edge)
        {
            case SnapEdge.Bottom:
                return 0f;

            case SnapEdge.Top:
                return 180f;

            case SnapEdge.Left:
                return -90f;

            case SnapEdge.Right:
                return 90f;
        }

        return 0f;
    }

    private Vector2 GetPivotForEdge()
    {
        /*
         * Панель поворачивается целиком.
         * Чтобы она нормально стояла в углах, pivot должен меняться.
         *
         * StartCorner:
         * Left/Right = низ края.
         * Top/Bottom = левый край.
         *
         * EndCorner:
         * Left/Right = верх края.
         * Top/Bottom = правый край.
         */

        if (!useSmartEdgeAnchors || edgeAnchorSlot == EdgeAnchorSlot.Free)
        {
            return new Vector2(0.5f, 0f);
        }

        if (edgeAnchorSlot == EdgeAnchorSlot.StartCorner)
        {
            switch (edge)
            {
                case SnapEdge.Bottom:
                    return new Vector2(0f, 0f);

                case SnapEdge.Top:
                    return new Vector2(1f, 0f);

                case SnapEdge.Left:
                    return new Vector2(1f, 0f);

                case SnapEdge.Right:
                    return new Vector2(0f, 0f);
            }
        }

        if (edgeAnchorSlot == EdgeAnchorSlot.EndCorner)
        {
            switch (edge)
            {
                case SnapEdge.Bottom:
                    return new Vector2(1f, 0f);

                case SnapEdge.Top:
                    return new Vector2(0f, 0f);

                case SnapEdge.Left:
                    return new Vector2(0f, 0f);

                case SnapEdge.Right:
                    return new Vector2(1f, 0f);
            }
        }

        return new Vector2(0.5f, 0f);
    }

    // Bounds are measured in the selected viewport, NOT assumed to be in
    // rect.parent's coordinate system. Functions/Panel is nested in the HUD.
    private Rect MeasureBounds()
    {
        rect.GetWorldCorners(corners);
        Vector3 first = parentRect.InverseTransformPoint(corners[0]);
        float minX = first.x, maxX = first.x, minY = first.y, maxY = first.y;
        for (int i = 1; i < corners.Length; i++)
        {
            Vector3 p = parentRect.InverseTransformPoint(corners[i]);
            minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
            minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
        }
        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    private Vector2 ToAnchoredDelta(Vector2 viewportDelta)
    {
        return rect.parent.InverseTransformVector(parentRect.TransformVector(viewportDelta));
    }

    private Rect PaddedViewport()
    {
        Rect r = parentRect.rect;
        float pad = Mathf.Clamp(edgePadding, 0f, Mathf.Min(r.width, r.height) * 0.49f);
        return Rect.MinMaxRect(r.xMin + pad, r.yMin + pad, r.xMax - pad, r.yMax - pad);
    }

    private Vector2 GetOpenAnchoredPosition()
    {
        Rect bounds = MeasureBounds();
        Rect view = PaddedViewport();
        // Corner pivots remain meaningful, but also get the edge inset along
        // the tangent; no corner is allowed to bypass the final bounds check.
        float dx = Mathf.Clamp(0f, view.xMin - bounds.xMin, view.xMax - bounds.xMax);
        float dy = Mathf.Clamp(0f, view.yMin - bounds.yMin, view.yMax - bounds.yMax);
        switch (edge)
        {
            case SnapEdge.Left: dx = view.xMin - bounds.xMin; break;
            case SnapEdge.Right: dx = view.xMax - bounds.xMax; break;
            case SnapEdge.Bottom: dy = view.yMin - bounds.yMin; break;
            case SnapEdge.Top: dy = view.yMax - bounds.yMax; break;
        }
        return rect.anchoredPosition + ToAnchoredDelta(new Vector2(dx, dy));
    }

    private Vector2 GetClosedAnchoredPosition()
    {
        Rect bounds = MeasureBounds();
        Vector2 shift = Vector2.zero;
        switch (edge)
        {
            case SnapEdge.Left: shift.x = -(bounds.width + edgePadding * 2f); break;
            case SnapEdge.Right: shift.x = bounds.width + edgePadding * 2f; break;
            case SnapEdge.Bottom: shift.y = -(bounds.height + edgePadding * 2f); break;
            case SnapEdge.Top: shift.y = bounds.height + edgePadding * 2f; break;
        }
        return GetOpenAnchoredPosition() + ToAnchoredDelta(shift);
    }

    private void ConfigureForEdge()
    {
        if (!EnsureRefs()) return;
        // Rebuild only this toolbar, after the layout group/content fitter has
        // resolved its ACTUAL width. Never shrink the already-fitted scale.
        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        rect.localScale = authoredScale;
        rect.pivot = GetPivotForEdge();
        float z = GetRotationForEdge();
        rect.localRotation = Quaternion.Euler(0f, 0f, z);
        ApplyIconsCounterRotation(z);
        Rect bounds = MeasureBounds();
        Rect view = PaddedViewport();
        float fit = Mathf.Min(1f, Mathf.Min(view.width / Mathf.Max(0.001f, bounds.width),
                                          view.height / Mathf.Max(0.001f, bounds.height)));
        rect.localScale = authoredScale * fit;
        // Re-evaluate t with fitted dimensions, rather than an unscaled 550px
        // toolbar on the 500-unit short edge.
        Vector2 anchor = GetAnchor();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        lastBounds = parentRect.rect;
        lastPanelSize = rect.rect.size;
        lastEditMode = MobileCustomizeManager.EditMode;
        layoutApplied = true;
    }

    private void ApplyIconsCounterRotation(float panelRotationZ)
    {
        if (!counterRotateIcons)
            return;

        CollectIconsIfNeeded();

        if (iconRects == null)
            return;

        float counterZ = -panelRotationZ;

        for (int i = 0; i < iconRects.Length; i++)
        {
            if (iconRects[i] == null)
                continue;

            iconRects[i].localRotation = Quaternion.Euler(0f, 0f, counterZ);
        }
    }

    private void ApplyOpenPosition()
    {
        ConfigureForEdge();

        rect.anchoredPosition = GetOpenAnchoredPosition();

        SetVisible(true);
    }

    private void ApplyClosedPosition()
    {
        ConfigureForEdge();

        rect.anchoredPosition = GetClosedAnchoredPosition();

        SetVisible(false);
    }

    private void ApplyInstant()
    {
        if (MobileCustomizeManager.EditMode && forceOpenedInEditMode)
        {
            ApplyOpenPosition();
            return;
        }

        if (opened)
            ApplyOpenPosition();
        else
            ApplyClosedPosition();
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup == null)
            return;

        if (fadeWhenClosed)
            canvasGroup.alpha = visible ? 1f : 0f;
        else
            canvasGroup.alpha = 1f;

        if (disableRaycastWhenClosed)
        {
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }
    }

    public void Toggle()
    {
        if (MobileCustomizeManager.EditMode)
            return;

        SetOpened(!opened, true);
    }

    public void Open()
    {
        SetOpened(true, true);
    }

    public void Close()
    {
        SetOpened(false, true);
    }

    public void SetOpened(bool value, bool animated)
    {
        opened = value;

        if (animationRoutine != null)
            StopCoroutine(animationRoutine);

        if (animated)
            animationRoutine = StartCoroutine(AnimateTo(opened));
        else
            ApplyInstant();
    }

    private IEnumerator AnimateTo(bool open)
    {
        ConfigureForEdge();

        Vector2 start = rect.anchoredPosition;
        Vector2 target = open ? GetOpenAnchoredPosition() : GetClosedAnchoredPosition();

        float startAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;
        float targetAlpha = open ? 1f : 0f;

        if (canvasGroup != null)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        float time = 0f;

        while (time < animationTime)
        {
            time += Time.unscaledDeltaTime;

            float t = animationTime <= 0f ? 1f : time / animationTime;
            t = Mathf.Clamp01(t);

            float smooth = t * t * (3f - 2f * t);

            rect.anchoredPosition = Vector2.Lerp(start, target, smooth);

            if (canvasGroup != null)
            {
                if (fadeWhenClosed)
                    canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, smooth);
                else
                    canvasGroup.alpha = 1f;
            }

            yield return null;
        }

        rect.anchoredPosition = target;

        if (canvasGroup != null)
        {
            if (fadeWhenClosed)
                canvasGroup.alpha = targetAlpha;
            else
                canvasGroup.alpha = 1f;

            if (disableRaycastWhenClosed)
            {
                canvasGroup.interactable = open;
                canvasGroup.blocksRaycasts = open;
            }
        }
    }

    public void SavePosition()
    {
        if (string.IsNullOrEmpty(controlId))
            return;

        PlayerPrefs.SetInt(KeyEdge, (int)edge);
        PlayerPrefs.SetFloat(KeyT, edgeT);
        PlayerPrefs.SetInt(KeySlot, (int)edgeAnchorSlot);

        PlayerPrefs.Save();

        Debug.Log(
            "SAVED ROTATING PANEL " + controlId +
            " edge=" + edge +
            " t=" + edgeT +
            " slot=" + edgeAnchorSlot +
            " anchor=" + rect.anchorMin +
            " pivot=" + rect.pivot
        );
    }

    public void LoadPosition()
    {
        if (string.IsNullOrEmpty(controlId))
            return;

        if (PlayerPrefs.HasKey(KeyEdge))
            edge = (SnapEdge)PlayerPrefs.GetInt(KeyEdge);

        if (PlayerPrefs.HasKey(KeyT))
            edgeT = PlayerPrefs.GetFloat(KeyT);

        if (PlayerPrefs.HasKey(KeySlot))
            edgeAnchorSlot = (EdgeAnchorSlot)PlayerPrefs.GetInt(KeySlot);
        else
            edgeAnchorSlot = GetSlotFromT(edgeT);

        edgeT = Mathf.Clamp01(edgeT);

        ApplyInstant();

        Debug.Log(
            "LOADED ROTATING PANEL " + controlId +
            " edge=" + edge +
            " t=" + edgeT +
            " slot=" + edgeAnchorSlot +
            " anchor=" + rect.anchorMin +
            " pivot=" + rect.pivot
        );
    }

    public void ResetPosition()
    {
        if (string.IsNullOrEmpty(controlId))
            return;

        PlayerPrefs.DeleteKey(KeyEdge);
        PlayerPrefs.DeleteKey(KeyT);
        PlayerPrefs.DeleteKey(KeySlot);

        if (useManualResetPosition)
        {
            edge = resetEdge;
            edgeT = Mathf.Clamp01(resetT);
            edgeAnchorSlot = GetSlotFromT(edgeT);
        }
        else
        {
            edge = defaultEdge;
            edgeT = Mathf.Clamp01(defaultEdgeT);
            edgeAnchorSlot = defaultSlot;
        }

        ApplyInstant();
        ReapplyNextFrame();

        PlayerPrefs.Save();

        Debug.Log(
            "RESET ROTATING PANEL " + controlId +
            " edge=" + edge +
            " t=" + edgeT +
            " slot=" + edgeAnchorSlot +
            " anchor=" + rect.anchorMin +
            " pivot=" + rect.pivot
        );
    }

    private void ReapplyNextFrame()
    {
        if (!gameObject.activeInHierarchy)
            return;

        if (reapplyRoutine != null)
            StopCoroutine(reapplyRoutine);

        reapplyRoutine = StartCoroutine(ReapplyCoroutine());
    }

    private IEnumerator ReapplyCoroutine()
    {
        yield return null;

        ApplyInstant();

        yield return new WaitForEndOfFrame();

        ApplyInstant();
    }
}