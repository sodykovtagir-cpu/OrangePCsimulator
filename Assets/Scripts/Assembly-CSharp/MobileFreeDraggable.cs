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

    private Vector2 GetSmartAnchor(Vector2 normalizedCenter)
    {
        float ax = 0.5f;
        float ay = 0.5f;

        if (normalizedCenter.x <= edgeSnapZone)
            ax = 0f;
        else if (normalizedCenter.x >= 1f - edgeSnapZone)
            ax = 1f;

        if (normalizedCenter.y <= edgeSnapZone)
            ay = 0f;
        else if (normalizedCenter.y >= 1f - edgeSnapZone)
            ay = 1f;

        return new Vector2(ax, ay);
    }

    private void SnapAnchorKeepingPosition()
    {
        if (!EnsureRefs())
            return;

        Vector2 normalized = GetCurrentNormalizedPosition();
        normalized = ClampNormalized(normalized);

        Vector2 newAnchor = GetSmartAnchor(normalized);

        Vector2 pivotLocal = GetCurrentPivotLocalInParent();
        Vector2 anchorLocal = AnchorToLocalPoint(newAnchor);

        rect.anchorMin = newAnchor;
        rect.anchorMax = newAnchor;
        rect.anchoredPosition = pivotLocal - anchorLocal;

        Debug.Log("SMART ANCHOR " + controlId + " anchor=" + newAnchor + " pos=" + rect.anchoredPosition);
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

            Debug.Log("LOADED SMART " + controlId + " anchor=" + anchor + " pos=" + pos);
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

        PlayerPrefs.Save();

        Debug.Log("Reset mobile position: " + controlId);
    }
}