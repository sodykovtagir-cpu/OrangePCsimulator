using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MobileFreeDraggable : MonoBehaviour, IDragHandler, IEndDragHandler
{
    [Header("Уникальный ID элемента")]
    [SerializeField]
    private string controlId;

    public string ControlId
    {
        get { return controlId; }
    }

    // Main (Medium) has these components authored in the scene. Other room
    // scenes share the same HUD names but lack the draggable scripts entirely.
    // Install only the missing ones; never replace an authored placement.
    public static void EnsureRoomHudLayout()
    {
        foreach (var root in FindObjectsOfType<Transform>())
        {
            if (root == null || root.name != "Mobile" || !root.gameObject.activeInHierarchy)
                continue;
            var bounds = root as RectTransform;
            if (bounds == null) continue;
            EnsureControl(root.Find("Functions/Icon"), "PanelOpen", bounds);
            EnsureControl(root.Find("Pause"), "MenuButton", bounds);
            EnsureControl(root.Find("Fixed Joystick"), "Joystick", bounds);
            EnsureControl(root.Find("Run"), "Run", bounds);
            // Medium has the sliding hotbar authored in the scene; the other
            // rooms only had the static panel. Without this component the
            // saved FunctionsPanel layout has nothing to attach to there.
            EnsureAdaptivePanel(root.Find("Functions/Panel"), "FunctionsPanel", bounds);
        }
    }

    private static void EnsureControl(Transform target, string id, RectTransform bounds)
    {
        if (target == null || target.GetComponent<MobileFreeDraggable>() != null) return;
        var control = target.gameObject.AddComponent<MobileFreeDraggable>();
        control.Init(id, bounds);
    }

    private static void EnsureAdaptivePanel(Transform target, string id, RectTransform bounds)
    {
        if (target == null || target.GetComponent<MobileAdaptiveSlidePanel>() != null) return;
        var panel = target.gameObject.AddComponent<MobileAdaptiveSlidePanel>();
        panel.Init(id, bounds);
    }

    /// <summary>
    /// Инициализация для панелей, которые создаются кодом (стрелки сетки):
    /// controlId и область, внутри которой элемент двигается. После Init
    /// сразу подтягиваем сохранённую позицию — Awake уже отработал.
    /// </summary>
    public void Init(string id, RectTransform parent)
    {
        controlId = id;
        if (parent != null) parentRect = parent;
        EnsureRefs();
        LoadPosition();
    }

    [Header("Родительская область. В меню = PreviewMobileControlsPanel, в игре = MobileControlsPanel")]
    [SerializeField]
    private RectTransform parentRect;

    [Header("Умные anchors")]
    [SerializeField]
    private bool useSmartAnchors = true;

    [SerializeField]
    [Range(0.02f, 0.4f)]
    private float edgeSnapZone = 0.16f;

    private RectTransform rect;

    private Vector2 defaultAnchorMin;
    private Vector2 defaultAnchorMax;
    private Vector2 defaultAnchoredPosition;

    private Button button;
    private bool defaultInteractable;

    // Старые ключи, чтобы можно было подхватить прежнюю раскладку.
    private string OldKeyX => "mobile_free_" + controlId + "_x";
    private string OldKeyY => "mobile_free_" + controlId + "_y";

    // Новые ключи: сохраняем anchor + offset.
    private string KeyAnchorX => "mobile_free_anchor_" + controlId + "_ax";
    private string KeyAnchorY => "mobile_free_anchor_" + controlId + "_ay";
    private string KeyPosX => "mobile_free_anchor_" + controlId + "_px";
    private string KeyPosY => "mobile_free_anchor_" + controlId + "_py";

    private void Awake()
    {
        EnsureRefs();

        if (rect != null)
        {
            defaultAnchorMin = rect.anchorMin;
            defaultAnchorMax = rect.anchorMax;
            defaultAnchoredPosition = rect.anchoredPosition;
        }

        button = GetComponent<Button>();

        if (button != null)
            defaultInteractable = button.interactable;

        LoadPosition();
    }

    private void OnEnable()
    {
        if (!MobileCustomizeManager.EditMode)
            StartCoroutine(ApplyPositionNextFrame());
    }

    private IEnumerator ApplyPositionNextFrame()
    {
        yield return null;
        LoadPosition();

        yield return new WaitForEndOfFrame();
        LoadPosition();
    }

    private void Update()
    {
        if (button != null)
            button.interactable = !MobileCustomizeManager.EditMode && defaultInteractable;
    }

    private bool EnsureRefs()
    {
        if (rect == null)
            rect = GetComponent<RectTransform>();

        if (rect == null)
        {
            Debug.LogWarning(name + ": MobileFreeDraggable должен висеть на UI-объекте с RectTransform.");
            return false;
        }

        if (parentRect == null)
        {
            Transform p = rect.parent;

            if (p != null)
                parentRect = p as RectTransform;
        }

        if (parentRect == null)
        {
            Canvas canvas = GetComponentInParent<Canvas>();

            if (canvas != null)
                parentRect = canvas.transform as RectTransform;
        }

        if (parentRect == null)
        {
            Debug.LogWarning(name + ": Parent Rect не найден. Укажи Parent Rect вручную.");
            return false;
        }

        return true;
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

        Vector2 normalized = LocalToNormalized(localPoint);
        normalized = ClampNormalized(normalized);

        // Во время перетаскивания двигаем просто по normalized-центру.
        rect.anchorMin = normalized;
        rect.anchorMax = normalized;
        rect.anchoredPosition = Vector2.zero;
        ClampAnchoredPosition();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!MobileCustomizeManager.EditMode)
            return;

        if (useSmartAnchors)
            SnapAnchorKeepingPosition();

        SavePosition();
    }

    private Vector2 LocalToNormalized(Vector2 localPoint)
    {
        if (!EnsureRefs())
            return new Vector2(0.5f, 0.5f);

        Rect r = parentRect.rect;

        float x = Mathf.InverseLerp(r.xMin, r.xMax, localPoint.x);
        float y = Mathf.InverseLerp(r.yMin, r.yMax, localPoint.y);

        return new Vector2(x, y);
    }

    private Vector2 ClampNormalized(Vector2 normalized)
    {
        if (!EnsureRefs())
            return normalized;

        Rect parent = parentRect.rect;
        Rect self = rect.rect;

        float minX = 0f;
        float maxX = 1f;
        float minY = 0f;
        float maxY = 1f;

        if (parent.width > 0f)
        {
            minX = self.width * rect.pivot.x / parent.width;
            maxX = 1f - self.width * (1f - rect.pivot.x) / parent.width;
        }

        if (parent.height > 0f)
        {
            minY = self.height * rect.pivot.y / parent.height;
            maxY = 1f - self.height * (1f - rect.pivot.y) / parent.height;
        }

        normalized.x = Mathf.Clamp(normalized.x, minX, maxX);
        normalized.y = Mathf.Clamp(normalized.y, minY, maxY);

        return normalized;
    }

    private Vector2 GetCurrentNormalizedPosition()
    {
        if (!EnsureRefs())
            return new Vector2(0.5f, 0.5f);

        Vector3 worldCenter = rect.TransformPoint(rect.rect.center);
        Vector2 localPoint = parentRect.InverseTransformPoint(worldCenter);

        return LocalToNormalized(localPoint);
    }

    private Vector2 GetCurrentPivotLocalInParent()
    {
        if (!EnsureRefs())
            return Vector2.zero;

        return parentRect.InverseTransformPoint(rect.position);
    }

    private Vector2 AnchorToLocalPoint(Vector2 anchor)
    {
        if (!EnsureRefs())
            return Vector2.zero;

        Rect r = parentRect.rect;

        float x = Mathf.Lerp(r.xMin, r.xMax, anchor.x);
        float y = Mathf.Lerp(r.yMin, r.yMax, anchor.y);

        return new Vector2(x, y);
    }

    // Выбираем якорь по КРАЮ панели, а не по её центру. Иначе широкая панель
    // (стрелки сетки), пристыкованная к правому краю, оставалась с якорем 0.5 и
    // большим pixel-offset; на другом размере экрана/превью этот offset уводил
    // её за экран. Теперь «прижато к правому краю» = anchor.x = 1 и малый offset.
    private Vector2 GetSmartAnchor(Vector2 normalizedPivot)
    {
        float ax = 0.5f;
        float ay = 0.5f;

        if (!EnsureRefs())
            return new Vector2(ax, ay);

        Rect parent = parentRect.rect;
        Rect self = rect.rect;

        if (parent.width > 0f)
        {
            float width = self.width / parent.width;
            float minX = normalizedPivot.x - width * rect.pivot.x;
            float maxX = minX + width;

            if (minX <= edgeSnapZone)
                ax = 0f;
            else if (maxX >= 1f - edgeSnapZone)
                ax = 1f;
        }

        if (parent.height > 0f)
        {
            float height = self.height / parent.height;
            float minY = normalizedPivot.y - height * rect.pivot.y;
            float maxY = minY + height;

            if (minY <= edgeSnapZone)
                ay = 0f;
            else if (maxY >= 1f - edgeSnapZone)
                ay = 1f;
        }

        return new Vector2(ax, ay);
    }

    private void SnapAnchorKeepingPosition()
    {
        if (!EnsureRefs())
            return;

        // Клампим именно pivot (как в OnDrag), а не центр: ClampNormalized
        // рассчитан на pivot. Раньше сюда передавался центр, а финальная позиция
        // бралась из НЕклампнутого pivot — панель могла выехать за экран.
        Vector2 pivotLocal = GetCurrentPivotLocalInParent();
        Vector2 normalizedPivot = ClampNormalized(LocalToNormalized(pivotLocal));
        Vector2 clampedPivotLocal = AnchorToLocalPoint(normalizedPivot);

        Vector2 newAnchor = GetSmartAnchor(normalizedPivot);
        Vector2 anchorLocal = AnchorToLocalPoint(newAnchor);

        rect.anchorMin = newAnchor;
        rect.anchorMax = newAnchor;
        rect.anchoredPosition = clampedPivotLocal - anchorLocal;
        ClampAnchoredPosition();

        Debug.Log("SMART ANCHOR " + controlId + " anchor=" + newAnchor + " pos=" + rect.anchoredPosition);
    }

    // Жёсткая граница экрана: после загрузки/снапа/сброса панель не может
    // оказаться за пределами parentRect, даже если сохранённая раскладка была
    // записана на другом размере окна превью или старым багом.
    private void ClampAnchoredPosition()
    {
        if (!EnsureRefs())
            return;

        Rect parent = parentRect.rect;
        Rect self = rect.rect;

        if (parent.width <= 0f || parent.height <= 0f)
            return;

        Vector2 anchor = (rect.anchorMin + rect.anchorMax) * 0.5f;
        Vector2 anchorLocal = AnchorToLocalPoint(anchor);
        Vector2 pivotLocal = anchorLocal + rect.anchoredPosition;

        float minPivotX = parent.xMin + self.width * rect.pivot.x;
        float maxPivotX = parent.xMax - self.width * (1f - rect.pivot.x);
        float minPivotY = parent.yMin + self.height * rect.pivot.y;
        float maxPivotY = parent.yMax - self.height * (1f - rect.pivot.y);

        // Панель больше области — центрируем саму панель (не pivot), а не
        // выталкиваем за край.
        if (minPivotX > maxPivotX)
            minPivotX = maxPivotX = (parent.xMin + parent.xMax) * 0.5f + (rect.pivot.x - 0.5f) * self.width;
        if (minPivotY > maxPivotY)
            minPivotY = maxPivotY = (parent.yMin + parent.yMax) * 0.5f + (rect.pivot.y - 0.5f) * self.height;

        pivotLocal.x = Mathf.Clamp(pivotLocal.x, minPivotX, maxPivotX);
        pivotLocal.y = Mathf.Clamp(pivotLocal.y, minPivotY, maxPivotY);

        rect.anchoredPosition = pivotLocal - anchorLocal;
    }

    public void SavePosition()
    {
        if (!EnsureRefs())
            return;

        if (string.IsNullOrEmpty(controlId))
        {
            Debug.LogWarning(name + ": controlId пустой.");
            return;
        }

        if (useSmartAnchors)
            SnapAnchorKeepingPosition();
        else
            ClampAnchoredPosition();

        Vector2 anchor = (rect.anchorMin + rect.anchorMax) * 0.5f;
        Vector2 pos = rect.anchoredPosition;

        PlayerPrefs.SetFloat(KeyAnchorX, anchor.x);
        PlayerPrefs.SetFloat(KeyAnchorY, anchor.y);
        PlayerPrefs.SetFloat(KeyPosX, pos.x);
        PlayerPrefs.SetFloat(KeyPosY, pos.y);

        PlayerPrefs.Save();

        Debug.Log("SAVED SMART " + controlId + " anchor=" + anchor + " pos=" + pos);
    }

    public void LoadPosition()
    {
        if (!EnsureRefs())
            return;

        if (string.IsNullOrEmpty(controlId))
            return;

        // Новый формат: anchor + anchoredPosition.
        if (
            PlayerPrefs.HasKey(KeyAnchorX) &&
            PlayerPrefs.HasKey(KeyAnchorY) &&
            PlayerPrefs.HasKey(KeyPosX) &&
            PlayerPrefs.HasKey(KeyPosY)
        )
        {
            float ax = PlayerPrefs.GetFloat(KeyAnchorX);
            float ay = PlayerPrefs.GetFloat(KeyAnchorY);
            float px = PlayerPrefs.GetFloat(KeyPosX);
            float py = PlayerPrefs.GetFloat(KeyPosY);

            Vector2 anchor = new Vector2(ax, ay);
            Vector2 pos = new Vector2(px, py);

            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.anchoredPosition = pos;

            // Сохранённая раскладка могла быть записана в превью другого размера
            // или старым багом без клампа. Возвращаем панель на экран и, если
            // нужно, перепривязываем якорь к ближайшему краю.
            if (useSmartAnchors)
                SnapAnchorKeepingPosition();
            else
                ClampAnchoredPosition();

            Debug.Log("LOADED SMART " + controlId + " anchor=" + rect.anchorMin + " pos=" + rect.anchoredPosition);
            return;
        }

        // Старый формат: normalized x/y.
        if (PlayerPrefs.HasKey(OldKeyX) && PlayerPrefs.HasKey(OldKeyY))
        {
            float x = PlayerPrefs.GetFloat(OldKeyX);
            float y = PlayerPrefs.GetFloat(OldKeyY);

            Vector2 normalized = new Vector2(x, y);
            normalized = ClampNormalized(normalized);

            rect.anchorMin = normalized;
            rect.anchorMax = normalized;
            rect.anchoredPosition = Vector2.zero;

            if (useSmartAnchors)
                SnapAnchorKeepingPosition();
            else
                ClampAnchoredPosition();

            Debug.Log("LOADED OLD " + controlId + " normalized=" + normalized);
        }
    }

    public void DeleteSavedPositions()
    {
        if (string.IsNullOrEmpty(controlId))
            return;

        // Новый формат
        PlayerPrefs.DeleteKey(KeyAnchorX);
        PlayerPrefs.DeleteKey(KeyAnchorY);
        PlayerPrefs.DeleteKey(KeyPosX);
        PlayerPrefs.DeleteKey(KeyPosY);

        // Старый формат
        PlayerPrefs.DeleteKey(OldKeyX);
        PlayerPrefs.DeleteKey(OldKeyY);

        // Возможные старые варианты
        PlayerPrefs.DeleteKey("mobile_free_v2_" + controlId + "_x");
        PlayerPrefs.DeleteKey("mobile_free_v2_" + controlId + "_y");
        PlayerPrefs.DeleteKey("mobile_free_v3_" + controlId + "_x");
        PlayerPrefs.DeleteKey("mobile_free_v3_" + controlId + "_y");
        PlayerPrefs.DeleteKey("mobile_free_v4_" + controlId + "_x");
        PlayerPrefs.DeleteKey("mobile_free_v4_" + controlId + "_y");

        PlayerPrefs.DeleteKey("mobile_layout_" + controlId + "_x");
        PlayerPrefs.DeleteKey("mobile_layout_" + controlId + "_y");

        PlayerPrefs.DeleteKey("mobile_control_" + controlId + "_x");
        PlayerPrefs.DeleteKey("mobile_control_" + controlId + "_y");

        PlayerPrefs.DeleteKey("mobile_control_" + controlId + "_anchor_x");
        PlayerPrefs.DeleteKey("mobile_control_" + controlId + "_anchor_y");
    }

    public void ResetPosition()
    {
        if (!EnsureRefs())
            return;

        DeleteSavedPositions();

        rect.anchorMin = defaultAnchorMin;
        rect.anchorMax = defaultAnchorMax;
        rect.anchoredPosition = defaultAnchoredPosition;

        if (useSmartAnchors)
            SnapAnchorKeepingPosition();
        else
            ClampAnchoredPosition();

        PlayerPrefs.Save();

        Debug.Log("Reset mobile position: " + controlId);
    }
}